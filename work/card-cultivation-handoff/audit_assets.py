"""重新提取本轮相关槽位/窗口/资源点/动画对象，类型名以包内TypeTree为准。"""
from pathlib import Path
import json
from extract_r01 import SerializedFile, CLASS_NAMES
from unpack_unityfs import unpack

ROOT=Path(__file__).resolve().parents[2]
WORK=Path(__file__).resolve().parent
OUT=ROOT/'docs/handoffs/2026-11-09-卡牌桌面底层/复核'
OUT.mkdir(exist_ok=True)

source=ROOT/'Card Cultivation/CardCultivation_Data/StreamingAssets/Entities/Map.dat'
data=source.read_bytes(); key=bytes(data[i]^b'UnityFS\0'[i] for i in range(4))
assert all((data[i]^key[i%4])==b'UnityFS\0'[i] for i in range(8))
target=WORK/'decoded/Entities/Map.bundle'
target.parent.mkdir(parents=True,exist_ok=True)
if not target.exists():target.write_bytes(bytes(v^key[i%4] for i,v in enumerate(data)))
nodes=unpack(target)
(OUT/'新增Map资源包节点.json').write_text(json.dumps(nodes,ensure_ascii=False,indent=2),encoding='utf8')

def hierarchy(asset, pid, visited=None):
    visited=set() if visited is None else visited
    if pid in visited:return {'pathID':pid,'error':'cycle'}
    visited.add(pid)
    go=asset.deserialize_object(pid)
    if not isinstance(go,dict) or 'm_Component' not in go:return {'pathID':pid,'error':'not GameObject','data':go}
    node={'name':go.get('m_Name'),'pathID':pid,'components':[],'children':[]}
    for ref in go['m_Component']:
        ptr=ref['component']; cp=ptr['pathID']
        if ptr['fileID']!=0 or cp not in asset.objects:
            node['components'].append({'external':ptr});continue
        entry=asset.objects[cp];cid=entry['class_id'];value=asset.deserialize_object(cp)
        record={'pathID':cp,'classID':cid,'typeName':CLASS_NAMES.get(cid,str(cid)),'data':value}
        if isinstance(value,dict) and cid==114:
            script=value.get('m_Script',{})
            info=asset.scripts.get(script.get('pathID')) if script.get('fileID')==0 else None
            if info:record['script']=info
        node['components'].append(record)
        if cid in (4,224) and isinstance(value,dict):
            for child in value.get('m_Children',[]):
                if child['fileID']!=0:
                    node['children'].append({'external_transform':child});continue
                tf=asset.deserialize_object(child['pathID'])
                if not isinstance(tf,dict):continue
                owner=tf.get('m_GameObject',{})
                if owner.get('fileID')==0:node['children'].append(hierarchy(asset,owner['pathID'],visited))
    return node

output=[]; input_check={}; type_errors=[]
for bundle in ['Entities/Card','Entities/Timer','Entities/Map','Entities/Animations','Configs','UI/UIForms']:
    files=[p for p in (WORK/'unpacked'/bundle).glob('CAB-*') if p.suffix not in ('.resS','.resource')]
    assert len(files)==1,(bundle,files)
    asset=SerializedFile(files[0])
    for entry in asset.types:
        if entry['class_id']!=114 and entry['nodes']:
            real=entry['nodes'][0]['type']
            if entry['class_id'] in CLASS_NAMES and CLASS_NAMES[entry['class_id']]!=real:
                type_errors.append({'class_id':entry['class_id'],'old':CLASS_NAMES[entry['class_id']],'actual':real,'bundle':bundle})
            CLASS_NAMES[entry['class_id']]=real
    if bundle=='Configs':
        decoded=asset.deserialize_object(418656196238816513)
        stored=json.loads((WORK/'inputactionasset_deserialized.json').read_text(encoding='utf8'))
        input_check={'pathID':418656196238816513,'decoded':decoded is not None,
                     'action_maps_equal':decoded.get('m_ActionMaps')==stored.get('m_ActionMaps'),
                     'action_map_count':len(decoded.get('m_ActionMaps',[]))}
    names=[]
    for path,pid in asset.container.items():
        base=path.rsplit('/',1)[-1]
        wanted=(bundle=='Entities/Timer' and base in ['cardslot.prefab','snapslot.prefab','workform.prefab','worktimer.prefab','produceform.prefab','producetimer.prefab','exploreform.prefab','exploretimer.prefab'])
        wanted|=(bundle=='Entities/Card' and base in ['role.prefab','materials.prefab'])
        wanted|=(bundle=='Entities/Map' and path.endswith('/book-qixingbaodian.prefab'))
        wanted|=(bundle=='UI/UIForms' and any(key in base for key in ['resourceform','recipeslot','widgetrecipe','widgetresource']))
        if wanted and path.endswith('.prefab'):
            output.append({'bundle':bundle,'asset_path':path,'pathID':pid,'hierarchy':hierarchy(asset,pid)})
        if bundle=='Entities/Animations' and any(key in base for key in ['workformexpan','workformidle','produceformidle','exploreformexpan']):
            output.append({'bundle':bundle,'asset_path':path,'pathID':pid,'data':asset.deserialize_object(pid)})
    print(bundle,'对象范围',len(asset.container))
(OUT/'槽位资源点与动画直接提取.json').write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf8')
(OUT/'旧类型标签与输入配置复核.json').write_text(json.dumps({'type_label_corrections':type_errors,'input':input_check},ensure_ascii=False,indent=2),encoding='utf8')
print('本轮对象',len(output),'输入配置重新解析',input_check)
