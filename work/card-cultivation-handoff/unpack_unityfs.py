"""局部 UnityFS 解包；仅处理已还原的文件，纯 Python，无外部依赖。"""
from pathlib import Path
import struct,json,hashlib,re
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
REPORT=ROOT/'docs/handoffs/2026-11-09-卡牌桌面底层/证据/资源包节点.json'

def lz4(data, expected):
    out=bytearray(); p=0
    while p<len(data):
        token=data[p];p+=1; n=token>>4
        if n==15:
            while True:
                v=data[p];p+=1;n+=v
                if v!=255:break
        out.extend(data[p:p+n]);p+=n
        if p==len(data):break
        offset=int.from_bytes(data[p:p+2],'little');p+=2
        assert 0<offset<=len(out)
        n=(token&15)+4
        if (token&15)==15:
            while True:
                v=data[p];p+=1;n+=v
                if v!=255:break
        for _ in range(n):out.append(out[-offset])
    assert len(out)==expected,(len(out),expected)
    return bytes(out)

def decompress(data,size,flags):
    mode=flags&63
    if mode==0:
        assert len(data)==size
        return data
    if mode in (2,3):return lz4(data,size)
    raise ValueError(f'尚不支持压缩模式 {mode}')

def unpack(path):
    b=path.read_bytes();p=0
    def z():
        nonlocal p
        end=b.index(0,p);s=b[p:end].decode();p=end+1;return s
    assert z()=='UnityFS'
    version=struct.unpack_from('>I',b,p)[0];p+=4
    player=z();engine=z()
    size,compressed,raw,flags=struct.unpack_from('>QIII',b,p);p+=20
    assert size==len(b)
    if version>=7:p=(p+15)//16*16
    info_start=len(b)-compressed if flags&128 else p
    info=decompress(b[info_start:info_start+compressed],raw,flags)
    if not flags&128:p+=compressed
    if flags&512:p=(p+15)//16*16
    q=16;count=struct.unpack_from('>I',info,q)[0];q+=4
    blocks=[]
    for _ in range(count):
        us,cs,fl=struct.unpack_from('>IIH',info,q);q+=10
        blocks.append(decompress(b[p:p+cs],us,fl));p+=cs
    blob=b''.join(blocks)
    count=struct.unpack_from('>I',info,q)[0];q+=4
    result=[]
    dest=HERE/'unpacked'/path.relative_to(HERE/'decoded').with_suffix('')
    dest.mkdir(parents=True,exist_ok=True)
    for _ in range(count):
        off,n,fl=struct.unpack_from('>QQI',info,q);q+=20
        end=info.index(0,q);name=info[q:end].decode();q=end+1
        target=(dest/name).resolve()
        assert target.is_relative_to(dest.resolve()),name
        data=blob[off:off+n];assert len(data)==n
        target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
        result.append({'name':name,'size':n,'flags':fl,'sha256':hashlib.sha256(data).hexdigest()})
    return {'bundle':path.relative_to(ROOT).as_posix(),'engine':engine,'nodes':result}

if __name__=='__main__':
    result=[unpack(p) for p in sorted((HERE/'decoded').rglob('*.bundle'))]
    REPORT.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    paths=[]
    for path in sorted((HERE/'unpacked').rglob('*')):
        if not path.is_file() or path.suffix=='.resS':continue
        for match in re.finditer(rb'[ -~]{5,220}',path.read_bytes()):
            text=match.group().decode('ascii')
            if any(key in text.lower() for key in ['assets/','.prefab','.json','.bytes']):
                paths.append({'file':path.relative_to(ROOT).as_posix(),'offset':match.start(),'text':text})
    REPORT.with_name('资源路径字符串.json').write_text(json.dumps(paths,ensure_ascii=False,indent=2),encoding='utf8')
    print('已解包',len(result),'个包，共',sum(len(x['nodes']) for x in result),'个节点')
