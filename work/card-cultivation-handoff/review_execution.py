"""交付复核：对照指令原文、严格读取资源清单；不运行游戏或项目测试。"""
from pathlib import Path
import re,json,subprocess,io,types,collections
ROOT=Path(__file__).resolve().parents[2]
AUDIT=ROOT/'docs/handoffs/2026-11-09-卡牌桌面底层/复核'
OUT=Path(__file__).resolve().parent/'review'
OUT.mkdir(exist_ok=True)
dumpbin='C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Tools/MSVC/14.44.35207/bin/Hostx64/x64/dumpbin.exe'
reports=sorted(AUDIT.glob('证据链*.md'))
claims=[]
for p in reports:
    for line_number,line in enumerate(p.read_text(encoding='utf8').splitlines(),1):
        m=re.match(r'\s*(?:;\s*)?([0-9A-Fa-f]{16}):\s*(.+)',line)
        if m:claims.append(dict(report=p.name,line=line_number,address=int(m[1],16),claim=m[2].split(';')[0].strip()))
actual={}
for block in sorted({x['address']&~0xffff for x in claims}):
    r=subprocess.run([dumpbin,'/nologo','/disasm:nobytes',f'/range:0x{block:x},0x{block+65535:x}',str(ROOT/'Card Cultivation/GameAssembly.dll')],capture_output=True)
    raw=r.stdout.decode('utf8',errors='replace')
    (OUT/f'{block:x}.asm.txt').write_text(raw,encoding='utf8')
    for a,instruction in re.findall(r'^\s*([0-9A-F]{16}):\s*(.+)$',raw,re.M):actual[int(a,16)]=instruction.strip()
for c in claims:
    c['actual']=actual.get(c['address'])
    c['mnemonic_matches']=c['actual'] is not None and c['claim'].split()[0].lower()==c['actual'].split()[0].lower()
summary={p.name:{'quoted_instructions':sum(x['report']==p.name for x in claims),'missing_or_wrong_mnemonic':sum(x['report']==p.name and not x['mnemonic_matches'] for x in claims)} for p in reports}
(OUT/'指令逐地址复核.json').write_text(json.dumps({'summary':summary,'claims':claims},ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(summary,ensure_ascii=False))
for p in reports:
    bad=[x for x in claims if x['report']==p.name and not x['mnemonic_matches']]
    for c in bad[:3]:print(p.name,c['line'],hex(c['address']),'报告:',c['claim'],'原文:',c['actual'])

# 在独立输出路径复现原解析器，并把所有短读变为错误。
streams=[]
class StrictStream(io.BytesIO):
    def __init__(self,data):super().__init__(data);streams.append(self)
    def read(self,n=-1):
        b=super().read(n)
        if n>=0 and len(b)!=n:raise EOFError(f'{self.tell()}: expected {n}, got {len(b)}')
        return b
script=ROOT/'work/card-cultivation-handoff/extract_remote_resource.py'
ns={'__file__':str(script),'__name__':'review'}
exec(compile(script.read_text(encoding='utf8'),str(script),'exec'),ns)
ns['io']=types.SimpleNamespace(BytesIO=StrictStream)
ns['OUT_JSON']=OUT/'RemoteResource复现.json'
ns['parse']()
data=json.loads(ns['OUT_JSON'].read_text(encoding='utf8'))
stored=json.loads((AUDIT/'RemoteResource完整清单.json').read_text(encoding='utf8'))
assets=data['assets'];resources=data['resources']
catalog=json.loads((AUDIT/'全包资产路径索引.json').read_text(encoding='utf8'))
membership=collections.Counter(idx for r in resources for idx in r['asset_indices'])
remote_paths={a['name'] for a in assets};local_paths={a['asset'] for a in catalog}
bundle_errors=[]
for r in resources:
    path=ROOT/'Card Cultivation/CardCultivation_Data/StreamingAssets'/(r['name']+'.'+(r['ext'] or 'dat'))
    if not path.exists() or path.stat().st_size!=r['length']:bundle_errors.append(str(path))
checks={'identical_to_delivered_json':data==stored,'bytes_consumed':streams[0].tell(),'file_bytes':len(streams[0].getvalue()),
        'assets':len(assets),'bundles':len(resources),'bundle_size_errors':bundle_errors,
        'all_assets_exactly_one_bundle':set(membership)==set(range(len(assets))) and set(membership.values())=={1},
        'dependency_indices_valid':all(0<=i<len(assets) for a in assets for i in a['deps']),
        'group_indices_valid':all(0<=i<len(resources) for g in data['groups'] for i in g['resources']),
        'asset_path_sets_match':remote_paths==local_paths,
        'normalized_asset_path_sets_match':{x.lower() for x in remote_paths}=={x.lower() for x in local_paths},
        'remaining_bytes_hex':streams[0].getvalue()[streams[0].tell():].hex(),
        'directed_dependency_edges':sum(len(a['deps']) for a in assets)}
(OUT/'RemoteResource独立复核.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(checks,ensure_ascii=False))
