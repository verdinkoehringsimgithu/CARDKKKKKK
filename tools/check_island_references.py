"""孤岛资源/序列化/C#语法静态核对；不调用Unity、编译器或游戏测试。"""
from pathlib import Path
import argparse
import collections
import json
import hashlib
import os
import re
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
ISLAND = ROOT / 'Assets/IslandSurvival'
GUID = re.compile(r'guid: ([0-9a-fA-F]+)')

def create_meta(path):
    output = Path(str(path)+'.meta')
    if output.exists(): return
    value = uuid.uuid5(uuid.NAMESPACE_URL, 'cardkkk/island/'+path.relative_to(ISLAND).as_posix()).hex
    prefix = 'fileFormatVersion: 2\nguid: '+value+'\n'
    if path.is_dir(): body = 'folderAsset: yes\nDefaultImporter:\n'
    elif path.suffix == '.cs': body = 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    elif path.suffix == '.asmdef': body = 'AssemblyDefinitionImporter:\n'
    else: body = 'DefaultImporter:\n'
    output.write_text(prefix+body+'  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--fill-meta',action='store_true'); parser.add_argument('--syntax',action='store_true'); args = parser.parse_args()
    errors=[]; files=list(ISLAND.rglob('*'))
    if args.fill_meta:
        create_meta(ISLAND)
        for path in files:
            if path.suffix != '.meta': create_meta(path)
    for path in [ISLAND]+files:
        if path.suffix != '.meta' and not Path(str(path)+'.meta').exists(): errors.append('缺meta '+str(path.relative_to(ROOT)))
    known = {}
    for base in ['Assets','Packages','Library/PackageCache']:
        for path in (ROOT/base).rglob('*.meta'):
            match=GUID.search(path.read_text(encoding='utf-8-sig',errors='replace'))
            if match: known.setdefault(match.group(1),[]).append(str(path.relative_to(ROOT)))
    used=set()
    for path in ISLAND.rglob('*.meta'):
        match=GUID.search(path.read_text(encoding='utf-8-sig'))
        if not match or not re.fullmatch('[a-fA-F0-9]{32}',match.group(1)): errors.append('非法GUID '+str(path))
        elif len(known[match.group(1)])!=1: errors.append('重复GUID '+str(path))
    for path in ISLAND.rglob('*'):
        if path.suffix not in ['.prefab','.unity','.asset']: continue
        text=path.read_text(encoding='utf-8-sig')
        for value in GUID.findall(text):
            used.add(value)
            if value not in known: errors.append('悬空外部GUID '+str(path.relative_to(ROOT))+' '+value)
        documents = re.findall(r'^--- !u!\d+ &(\d+)', text, re.M)
        if len(documents)!=len(set(documents)): errors.append('重复本地fileID '+str(path))
        own=set(documents)
        for line in text.splitlines():
            if 'guid:' in line: continue
            for fid in re.findall(r'\{fileID: (\d+)\}',line):
                if fid!='0' and fid not in own: errors.append('悬空本地fileID '+str(path.relative_to(ROOT))+' '+fid)
    manifest_path=ISLAND/'Art/art-manifest.json'
    art_count=0
    if manifest_path.exists():
        manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
        art_ids=[]
        for atlas in manifest['atlases']:
            image_path=ROOT/atlas['path']; source_meta=Path(str(image_path)+'.meta').read_text(encoding='utf-8')
            if hashlib.sha256(image_path.read_bytes()).hexdigest()[:12]!=atlas['sha12']:errors.append('图集摘要不符 '+atlas['path'])
            if 'guid: '+atlas['guid'] not in source_meta:errors.append('图集GUID不符 '+atlas['path'])
            for sprite in atlas['sprites']:
                art_ids.append(sprite['id']);art_count+=1
                card_text=(ROOT/sprite['card_asset']).read_text(encoding='utf-8')
                expected='{fileID: '+str(sprite['file_id'])+', guid: '+atlas['guid']+', type: 3}'
                if '  Artwork: '+expected not in card_text:errors.append('单卡插画映射不符 '+sprite['id'])
                if str(sprite['file_id']) not in source_meta:errors.append('Sprite本地ID不在图集meta '+sprite['id'])
        if len(art_ids)!=len(set(art_ids)) or art_count!=manifest['card_count']:errors.append('插画ID数量或唯一性不符')
    parsed=0; bound_fields=0
    if args.syntax:
        sys.path.insert(0,str(Path(os.environ['TEMP'])/'cardkkk-island-static-libs'))
        from tree_sitter import Language,Parser
        import tree_sitter_c_sharp
        cs = Parser(Language(tree_sitter_c_sharp.language()))
        components_by_script = collections.defaultdict(list)
        for prefab in ISLAND.rglob('*.prefab'):
            text = prefab.read_text(encoding='utf-8-sig')
            for block in re.split(r'(?=^--- !u!)', text, flags=re.M):
                script = re.search(r'^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}', block, re.M)
                if script:
                    components_by_script[script.group(1)].append(block)
        for path in ISLAND.rglob('*.cs'):
            source=path.read_bytes(); tree=cs.parse(source); parsed+=1
            script_meta=Path(str(path)+'.meta')
            script_guid=GUID.search(script_meta.read_text(encoding='utf-8')).group(1) if script_meta.exists() else ''
            components = components_by_script[script_guid]
            stack=[tree.root_node]
            while stack:
                node=stack.pop()
                if node.type=='ERROR' or node.is_missing:
                    errors.append('C#语法 '+str(path.relative_to(ROOT))+':'+str(node.start_point.row+1)+' '+source[node.start_byte:node.end_byte].decode('utf-8',errors='replace')[:80])
                if node.type=='field_declaration' and b'SerializeField' in source[node.start_byte:node.end_byte]:
                    for declaration in node.children:
                        if declaration.type!='variable_declaration':continue
                        for variable in declaration.children:
                            if variable.type!='variable_declarator':continue
                            field=variable.child_by_field_name('name')
                            if field is None:continue
                            field_name=source[field.start_byte:field.end_byte].decode('utf-8')
                            for block in components:
                                bound_fields+=1
                                value=re.search(r'^  '+re.escape(field_name)+r': (.+)$',block,re.M)
                                if value is None or value.group(1)=='{fileID: 0}': errors.append('未接序列化字段 '+path.name+' '+field_name)
                stack.extend(node.children)
    errors = list(dict.fromkeys(errors))
    print(json.dumps(dict(mode='static-only, no compilation/runtime/test execution',assetFiles=len(files),externalGuids=len(used),csharpSyntaxFiles=parsed,serializedFieldsChecked=bound_fields,artworkMappings=art_count,errors=errors),ensure_ascii=True,indent=2))
    return int(bool(errors))

if __name__=='__main__': raise SystemExit(main())
