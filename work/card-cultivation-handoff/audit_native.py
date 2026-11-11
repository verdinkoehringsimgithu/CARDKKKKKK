"""独立核对IL2CPP模块表及Token/RID映射；仅静态读取，不执行游戏。"""
from pathlib import Path
import struct, json, bisect, subprocess, re

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/handoffs/2026-11-09-卡牌桌面底层/复核'
OUT.mkdir(exist_ok=True)
b = (ROOT/'Card Cultivation/GameAssembly.dll').read_bytes()
pe = struct.unpack_from('<I',b,0x3c)[0]
opt=pe+24; image_base=struct.unpack_from('<Q',b,opt+24)[0]
sections=[]
for i in range(struct.unpack_from('<H',b,pe+6)[0]):
    p=opt+struct.unpack_from('<H',b,pe+20)[0]+40*i
    vs,rv,size,raw=struct.unpack_from('<4I',b,p+8)
    sections.append((rv,vs,raw,size,struct.unpack_from('<I',b,p+36)[0]))
def offset(va):
    for rv,vs,raw,size,_ in sections:
        if image_base+rv<=va<image_base+rv+size:return raw+va-image_base-rv
    raise ValueError(hex(va))
def address(off):
    for rv,vs,raw,size,_ in sections:
        if raw<=off<raw+size:return image_base+rv+off-raw
    raise ValueError(hex(off))
def executable(va):
    return any(image_base+rv<=va<image_base+rv+vs and flags&0x20000000 for rv,vs,_,_,flags in sections)

name=b.index(b'Assembly-CSharp.dll\0'); ref=struct.pack('<Q',address(name)); candidates=[]
for m in re.finditer(re.escape(ref),b):
    p=m.start(); count,table=struct.unpack_from('<2Q',b,p+8)
    if 0<count<100000:
        try:
            start=offset(table)
            pointers=struct.unpack_from('<'+'Q'*count,b,start)
            if all(v==0 or executable(v) for v in pointers):candidates.append((p,count,table,pointers))
        except (ValueError,struct.error):pass
assert len(candidates)==1,len(candidates)
module,count,table,pointers=candidates[0]
meta=(ROOT/'Card Cultivation/CardCultivation_Data/il2cpp_data/Metadata/global-metadata.dat').read_bytes()
def pair(pos):return struct.unpack_from('<2I',meta,pos)
so,sn=pair(24)
def string(i):return meta[so+i:meta.index(0,so+i)].decode('utf8')
io,ins=pair(168);to,tn=pair(160);mo,mn=pair(48)
img=next(struct.unpack_from('<10I',meta,p) for p in range(io,io+ins,40) if string(struct.unpack_from('<I',meta,p)[0])=='Assembly-CSharp.dll')
methods=[]
for t in range(img[2],img[2]+img[3]):
    v=struct.unpack_from('<16I8H2I',meta,to+t*88)
    cls=string(v[1])+'.'+string(v[0])
    for i in range(v[9],v[9]+v[16]):
        m=struct.unpack_from('<7I4H',meta,mo+i*36);rid=m[6]&0xffffff
        assert 1<=rid<=count
        methods.append({'index':i,'token':hex(m[6]),'rid':rid,'name':cls+'::'+string(m[0]),'va':pointers[rid-1]})
assert len(methods)==count and len({m['rid'] for m in methods})==count
old=json.loads((OUT.parent/'证据/R02-松手判定决策树.json').read_text(encoding='utf8'))
lookup={m['index']:m for m in methods}
comparison=[{'name':m.get('class','')+'::'+m['name'],'old':m['va_hex'],
             'corrected':hex(lookup[m['index']]['va']), 'same':m['va']==lookup[m['index']]['va']} for m in old['methods']]
report={'module_offset':hex(module),'method_count':count,'method_table_va':hex(table),
        'mapping':'Assembly-CSharp CodeGenModule.methodPointers[MethodDef.Token RID - 1]',
        'previous_mapping_comparison':comparison,'methods':methods}
(OUT/'原生方法地址独立核对.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print('模块方法数',count,'旧R02地址不一致',sum(not x['same'] for x in comparison),'/',len(comparison))

# 异常处理目录提供函数边界，避免凭猜测长度截断。
rv,size=struct.unpack_from('<2I',b,opt+112+3*8);p=offset(image_base+rv)
ranges=[struct.unpack_from('<3I',b,q)[:2] for q in range(p,p+size,12)]
starts=[a for a,z in ranges]
known_starts=sorted({m['va'] for m in methods if m['va']})
focus={
 'BaseEntityLogic':['OnDrop','OnDropOverlappingBehavior','OnMoveDropOverlappingBehavior','OnBeginDrag','OnDrag','OnEndDrag','SetDrag','CanHaveCardOnTop','CheckIfCanAddOnStack','AddOnStack','RemoveFromStack','SetParent','SetChild','GetOverlappingEntityLogic','GetOverlappingEntityLogicList','CheckIfCanEntityFormHandler','IsTimerSnap','PushAwayFromOthers','Bounce','UpdatePhysics','OnPointerEnter','OnPointerExit','OnPlayerSelect','ShowSelectEffect','SetSelectShow'],
 'InputMgrComponent':['OnPointerPerformed','OnPointerCanceled','OnPointerUp','OnDrag','PositionIsOverUI'],
 'BaseCardSlotEntityLogic':['OnDrop','OnDragEntityShowSelect','AddEntityForm','OnPointerClick','OnPointerUp','OnBeginDrag'],
 'BaseFormEntityLogic':['RemoveEntity','OnAttached','OnDetached','AddEntityForm','CanAddEntity','CanAddEntityForm','MatchRecipe','ChooseSlotIndex','OnStart','OnClose','OnDrag'],
 'BaseTimerEntityLogic':['OnDrop','AddEntity','OnDragEntityShowSelect','DetachEntityFly','StartTimer','OnTimerProgressEnd'],
 'WidgetResourceCardSlotEntityLogic':['OnDrop','OnPointerUp','AddEntityForm','OnDragEntityShowSelect','AttachEntity'],
 'ResourceWidgetEntityLogic':['OnDragEntityShowSelect','AddEntityForm','CreateWidgetForm'],
 'SortingLayerOrderControl':['SetSelectShow'],
 'CardBehaviourControl':['FlyPosition','JumpTowards','RotWobble']}
dumpbin=Path('C:/Program Files/Microsoft Visual Studio/2022/Community/VC/Tools/MSVC/14.44.35207/bin/Hostx64/x64/dumpbin.exe')
selected=[]
for m in methods:
    cls,method=m['name'].rsplit('::',1)
    if method not in focus.get(cls.rsplit('.',1)[-1],[]):continue
    if not m['va']:continue
    va=m['va']; idx=bisect.bisect_right(starts,va-image_base)-1
    has_unwind=idx>=0 and ranges[idx][0]<=va-image_base<ranges[idx][1]
    if not has_unwind and b[offset(va)]!=0xc3:
        selected.append({**m,'status':'无异常表边界，未反汇编'});continue
    unwind_end=image_base+ranges[idx][1] if has_unwind else va+1
    next_index=bisect.bisect_right(known_starts,va)
    end=min(known_starts[next_index] if next_index<len(known_starts) else unwind_end,va+65536)
    end=max(end,unwind_end) if has_unwind else va+1
    r=subprocess.run([str(dumpbin),'/nologo','/disasm:nobytes',f'/range:0x{va:x},0x{end-1:x}',str(ROOT/'Card Cultivation/GameAssembly.dll')],capture_output=True)
    text=r.stdout.decode('utf8',errors='replace').replace('\r\r\n','\n').replace('\r\n','\n')
    filename=f"{m['rid']:05d}-{cls.rsplit('.',1)[-1]}-{method}.asm.txt"
    assert re.search(r'^\s+[0-9A-F]{16}:',text,re.M),filename
    (OUT/filename).write_text(text,encoding='utf8')
    selected.append({**m,'end':hex(end),'unwind_region_end':hex(unwind_end),'range_note':'到下一已知方法入口的候选范围，可能含填充或未命名辅助函数','output':filename,'exit_code':r.returncode})
(OUT/'交互函数反汇编清单.json').write_text(json.dumps(selected,ensure_ascii=False,indent=2),encoding='utf8')
# 注释仅用于导航：共享地址保留全部别名，不能挑一个名字当作唯一调用目标。
by_va={}
for method in methods:by_va.setdefault(method['va'],[]).append(method['name'])
call_edges=[]
for item in selected:
    if 'output' not in item:continue
    source=(OUT/item['output']).read_text(encoding='utf8')
    def annotate(match):
        names=by_va.get(int(match[2],16),[])
        if names:call_edges.append({'caller':item['name'],'instruction':match[0], 'targets':names})
        return match[0]+(' ; '+(names[0] if len(names)==1 else f'共享地址（{len(names)}个别名），不唯一归因') if names else '')
    annotated=re.sub(r'\b(call|jmp)\s+([0-9A-Fa-f]{16})\b',annotate,source)
    (OUT/(Path(item['output']).stem+'.annotated.txt')).write_text(annotated,encoding='utf8')
(OUT/'已解析直接调用边.json').write_text(json.dumps(call_edges,ensure_ascii=False,indent=2),encoding='utf8')
print('目标函数记录',len(selected),'反汇编文件',sum('output' in x for x in selected))
