"""全文件指纹、全部.dat包目录、全部序列化节点目录普查；不执行游戏。"""
from pathlib import Path
import hashlib, json, struct, sys, collections, time
from unpack_unityfs import decompress
from extract_r01 import SerializedFile

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'Card Cultivation'
WORK=Path(__file__).resolve().parent
OUT=ROOT/'docs/handoffs/2026-11-09-卡牌桌面底层/复核'
OUT.mkdir(exist_ok=True)

def save_json(name,value):
    destination=OUT/name
    temporary=destination.with_suffix('.writing')
    for attempt in range(5):
        try:
            temporary.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8')
            temporary.replace(destination)
            return
        except OSError:
            if attempt==4:raise
            time.sleep(.2)

def inspect_stripped(path):
    """仅读取v22无TypeTree文件的类型/对象表，不猜测对象字段。"""
    b=path.read_bytes()
    assert struct.unpack_from('>I',b,8)[0]==22 and b[16]==0
    size,offset=struct.unpack_from('>QQ',b,24)
    assert size==len(b) and 48<=offset<=size
    p=b.index(0,48)+1
    platform=struct.unpack_from('<I',b,p)[0];p+=4
    enabled=b[p];p+=1
    assert not enabled,'有TypeTree的文件应走原解析器'
    count=struct.unpack_from('<I',b,p)[0];p+=4
    assert count<100000
    types=[]
    for _ in range(count):
        cid=struct.unpack_from('<i',b,p)[0];p+=7
        if cid==114:p+=16
        p+=16
        # 此包剥离TypeTree的文件也未写入树依赖数组，不能按带树布局继续读。
        assert p<=offset
        types.append(cid)
    count=struct.unpack_from('<I',b,p)[0];p+=4
    assert count<1000000
    classes=collections.Counter()
    for _ in range(count):
        p=(p+3)&~3
        pid,start,n,tid=struct.unpack_from('<qQIi',b,p);p+=24
        assert 0<=tid<len(types) and offset+start+n<=size
        classes[str(types[tid])]+=1
    assert p<=offset
    return {'objects':count,'types':len(types),'platform':platform,'class_counts':dict(classes),
            'depth':'无TypeTree：类型表和全部对象范围已校验，字段及行为未解析'}

def xor(data,key,start=0):
    result=bytearray(data)
    for lane in range(4):
        trans=bytes(i^key[(start+lane)%4] for i in range(256))
        result[lane::4]=result[lane::4].translate(trans)
    return bytes(result)

def inspect_bundle(path):
    with path.open('rb') as file:
        encoded=file.read(128)
        key=bytes(encoded[i]^b'UnityFS\0'[i] for i in range(4))
        header=xor(encoded,key)
        if not header.startswith(b'UnityFS\0'):return {'status':'非已识别UnityFS，未解析','prefix':encoded[:16].hex()}
        p=8;version=struct.unpack_from('>I',header,p)[0];p+=4
        def string():
            nonlocal p
            end=header.index(0,p);s=header[p:end].decode();p=end+1;return s
        player=string();engine=string();size,packed,raw,flags=struct.unpack_from('>QIII',header,p);p+=20
        assert size==path.stat().st_size
        if version>=7:p=(p+15)//16*16
        pos=size-packed if flags&128 else p
        file.seek(pos);info=decompress(xor(file.read(packed),key,pos),raw,flags)
        if not flags&128:p+=packed
        if flags&512:p=(p+15)//16*16
        q=16;count=struct.unpack_from('>I',info,q)[0];q+=4
        blocks=[];offset=0
        for _ in range(count):
            us,cs,fl=struct.unpack_from('>IIH',info,q);q+=10
            blocks.append((offset,us,p,cs,fl));offset+=us;p+=cs
        count=struct.unpack_from('>I',info,q)[0];q+=4
        nodes=[]
        for _ in range(count):
            start,n,fl=struct.unpack_from('>QQI',info,q);q+=20;end=info.index(0,q);name=info[q:end].decode();q=end+1
            node={'name':name,'size':n,'offset':start,'flags':fl}
            # resS/resource为原始媒体载荷。目录与范围已核对，像素/音频不逐个解码。
            if name.endswith(('.resS','.resource')):
                node['depth']='节点目录及数据范围已读取；媒体载荷未逐个解码'
            else:
                destination=WORK/'catalogs'/path.relative_to(SOURCE).with_suffix('')/name
                assert destination.resolve().is_relative_to((WORK/'catalogs').resolve())
                destination.parent.mkdir(parents=True,exist_ok=True)
                with destination.open('wb') as output:
                    for bo,us,cp,cs,bf in blocks:
                        if bo>=start+n or bo+us<=start:continue
                        file.seek(cp);data=decompress(xor(file.read(cs),key,cp),us,bf)
                        output.write(data[max(0,start-bo):min(us,start+n-bo)])
                assert destination.stat().st_size==n
                try:
                    asset=SerializedFile(destination)
                    node.update(depth='序列化对象表、资产目录及MonoScript目录已读取',
                                objects=len(asset.objects), assets=asset.container,
                                class_counts=dict(collections.Counter(str(o['class_id']) for o in asset.objects.values())),
                                scripts=list(asset.scripts.values()))
                except Exception as error:
                    node.update(depth='节点已提取，序列化解析失败待补',error=type(error).__name__+': '+str(error))
            assert start+n<=offset
            nodes.append(node)
        return {'status':'UnityFS全部节点目录已解析','engine':engine,'blocks':len(blocks),'nodes':nodes}

inventory=[]
files=sorted(p for p in SOURCE.rglob('*') if p.is_file())
for i,path in enumerate(files):
    h=hashlib.sha256()
    with path.open('rb') as f:
        prefix=f.read(32);h.update(prefix)
        for chunk in iter(lambda:f.read(8*1024*1024),b''):h.update(chunk)
    item={'path':path.relative_to(SOURCE).as_posix(),'size':path.stat().st_size,'sha256':h.hexdigest(),
          'header':prefix[:16].hex(),'depth':'完整字节指纹及文件头；内容未深入分析','relevance':'待按内容核对，未判为无用'}
    if path.suffix.lower()=='.dat':
        try:item['bundle']=inspect_bundle(path);item['depth']=item['bundle']['status']
        except Exception as error:item['error']=type(error).__name__+': '+str(error)
    elif len(prefix)>=20 and struct.unpack_from('>I',prefix,8)[0]==22:
        try:
            item['serialized_file']=inspect_stripped(path)
            item['depth']=item['serialized_file']['depth']
        except Exception as error:
            item['serialized_error']=type(error).__name__+': '+str(error)
            item['depth']='SerializedFile文件头已确认，内容解析未完成'
    inventory.append(item)
    # 每个文件写入进度，失败可审查，不把中断的扫描标为完成。
    save_json('全包逐文件普查.json',inventory)
    if (i+1)%10==0:print(f'已扫描 {i+1}/{len(files)}',flush=True)
bundles=[x for x in inventory if 'bundle' in x]
nodes=[n for x in bundles for n in x['bundle'].get('nodes',[])]
summary={'files':len(files),'processed':len(inventory),'bytes':sum(x['size'] for x in inventory),
         'dat_files':sum(p.suffix.lower()=='.dat' for p in files),'unityfs':sum(x['bundle']['status'].startswith('UnityFS') for x in bundles),
         'nodes':len(nodes),'catalogs_parsed':sum('assets' in n for n in nodes),'asset_entries':sum(len(n.get('assets',{})) for n in nodes),
         'builtin_serialized_parsed':sum('serialized_file'in x for x in inventory),
         'builtin_serialized_failures':[{'path':x['path'],'error':x['serialized_error']} for x in inventory if 'serialized_error'in x],
         'unparsed':[{'path':x['path'],'error':x['error']} for x in inventory if 'error'in x]+[
             {'node':n['name'],'error':n['error']} for n in nodes if 'error'in n]}
(OUT/'全包普查汇总.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
assets=[{'bundle':x['path'],'asset':a,'pathID':pid} for x in bundles for n in x['bundle'].get('nodes',[]) for a,pid in n.get('assets',{}).items()]
scripts={s['full_name']:s for n in nodes for s in n.get('scripts',[])}
(OUT/'全包资产路径索引.json').write_text(json.dumps(assets,ensure_ascii=False,indent=2),encoding='utf8')
(OUT/'全包脚本身份索引.json').write_text(json.dumps(list(scripts.values()),ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(summary,ensure_ascii=False),flush=True)
