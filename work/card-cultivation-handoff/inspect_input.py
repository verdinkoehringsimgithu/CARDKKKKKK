import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GAME_ASSEMBLY_PATH = ROOT / "Card Cultivation/GameAssembly.dll"
METADATA_PATH = ROOT / "Card Cultivation/CardCultivation_Data/il2cpp_data/Metadata/global-metadata.dat"

b_dll = GAME_ASSEMBLY_PATH.read_bytes()
b_meta = METADATA_PATH.read_bytes()

def table(pos):
    return struct.unpack_from("<II", b_meta, pos)

so, sn = table(24)
def string(i):
    end = b_meta.index(0, so+i, so+sn)
    return b_meta[so+i:end].decode("utf-8")

to, tn = table(160)
mo, mn = table(48)
io, ins = table(168)
table_offset = 0x2a44168

for p in range(io, io+ins, 40):
    img = struct.unpack_from("<10I", b_meta, p)
    img_name = string(img[0])
    if img_name != "Assembly-CSharp.dll":
        continue
    type_start = img[2]
    type_count = img[3]
    for t_idx in range(type_start, type_start + type_count):
        v = struct.unpack_from("<16I8H2I", b_meta, to + t_idx * 88)
        type_name = string(v[0])
        type_ns = string(v[1])
        full_cls = f"{type_ns}.{type_name}" if type_ns else type_name
        m_start = v[9]
        m_count = v[16]
        if 'Input' in full_cls or 'Camera' in full_cls:
            print(f"=== Class: {full_cls} ===")
            for m_i in range(m_count):
                k = m_start + m_i
                m = struct.unpack_from("<7I4H", b_meta, mo + k * 36)
                m_name = string(m[0])
                va = struct.unpack_from("<Q", b_dll, table_offset + k * 8)[0]
                print(f"  {m_name} (params={m[10]}, va=0x{va:x})")
