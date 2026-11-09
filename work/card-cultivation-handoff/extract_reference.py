"""只读来源；输出局部资源包与 IL2CPP 元数据索引，不恢复方法体。"""
from pathlib import Path
import json, struct, hashlib, argparse

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Card Cultivation'
OUT = Path(__file__).resolve().parent
EVIDENCE = ROOT / 'docs/handoffs/2026-11-09-卡牌桌面底层/证据'

def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')

def extract():
    metadata = SOURCE / 'CardCultivation_Data/il2cpp_data/Metadata/global-metadata.dat'
    b = metadata.read_bytes()
    assert struct.unpack_from('<II', b) == (0xFAB11BAF, 31)
    def table(pos): return struct.unpack_from('<II', b, pos)
    so, sn = table(24)
    def string(i):
        assert 0 <= i < sn, i
        end = b.index(0, so+i, so+sn)
        return b[so+i:end].decode('utf-8')
    to, tn = table(160)
    mo, mn = table(48)
    fo, fn = table(96)
    assert tn % 88 == mn % 36 == fn % 12 == 0
    io, ins = table(168)
    image = next(struct.unpack_from('<10I', b, p) for p in range(io,io+ins,40)
                 if string(struct.unpack_from('<I',b,p)[0]) == 'Assembly-CSharp.dll')
    result=[]
    for index in range(image[2],image[2]+image[3]):
        v=struct.unpack_from('<16I8H2I',b,to+index*88)
        methods=[]
        for j in range(v[16]):
            k=v[9]+j
            m=struct.unpack_from('<7I4H',b,mo+k*36)
            assert m[1] == index, (index,k,m)
            methods.append({'index':k,'name':string(m[0]),'parameter_count':m[10], 'token':hex(m[6])})
        fields=[]
        for j in range(v[18]):
            k=v[8]+j
            f=struct.unpack_from('<3I',b,fo+k*12)
            fields.append({'index':k,'name':string(f[0]),'type_index':f[1]})
        result.append({'index':index,'namespace':string(v[1]),'name':string(v[0]),'fields':fields,'methods':methods})
    write_json(EVIDENCE/'程序集类型成员.json',result)
    lines=[]
    for t in result:
        lines.append(f"\n## [{t['index']}] {t['namespace']}.{t['name']}")
        lines.extend('F '+f['name'] for f in t['fields'])
        lines.extend(f"M {m['name']} ({m['parameter_count']} params) [{m['index']}]" for m in t['methods'])
    (EVIDENCE/'程序集类型成员.txt').write_text('\n'.join(lines),encoding='utf-8')
    lo,ln=table(8); ld,ldn=table(16)
    literals=[]
    for i,p in enumerate(range(lo,lo+ln,8)):
        n,o=struct.unpack_from('<II',b,p)
        assert o+n <= ldn
        literals.append({'index':i,'text':b[ld+o:ld+o+n].decode('utf-8',errors='replace')})
    write_json(EVIDENCE/'字符串常量.json',literals)
    inventory=[]
    for p in sorted(SOURCE.rglob('*')):
        if p.is_file(): inventory.append({'path':p.relative_to(SOURCE).as_posix(),'size':p.stat().st_size})
    write_json(EVIDENCE/'文件清单.json',inventory)
    selected=['Entities/Card.dat','Entities/Timer.dat','Entities/Area.dat','Entities/Animations.dat',
              'UI/UIForms.dat','LubanTables.dat','Configs.dat']
    bundles=[]
    for rel in selected:
        p=SOURCE/'CardCultivation_Data/StreamingAssets'/rel
        data=p.read_bytes()
        key=bytes(data[i]^b'UnityFS\0'[i] for i in range(4))
        assert all((data[i]^key[i%4])==b'UnityFS\0'[i] for i in range(8)),rel
        plain=bytes(c^key[i%4] for i,c in enumerate(data))
        target=OUT/'decoded'/Path(rel).with_suffix('.bundle')
        target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(plain)
        bundles.append({'source':rel,'output':str(target.relative_to(ROOT)), 'size':len(data),
                        'sha256_source':hashlib.sha256(data).hexdigest(),
                        'sha256_decoded':hashlib.sha256(plain).hexdigest(),
                        'header':plain[:48].decode('ascii',errors='replace')})
    write_json(EVIDENCE/'局部拆包清单.json',bundles)
    print(f'提取 {len(result)} 个类型；{len(literals)} 条字符串；{len(bundles)} 个资源包。')

if __name__=='__main__': extract()
