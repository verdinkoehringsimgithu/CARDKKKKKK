import struct
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
METADATA_PATH = ROOT / "Card Cultivation/CardCultivation_Data/il2cpp_data/Metadata/global-metadata.dat"
GAME_ASSEMBLY_PATH = ROOT / "Card Cultivation/GameAssembly.dll"

b_meta = METADATA_PATH.read_bytes()
b_dll = GAME_ASSEMBLY_PATH.read_bytes()

def table(pos):
    return struct.unpack_from("<II", b_meta, pos)

so, sn = table(24)
def string(i):
    end = b_meta.index(0, so+i, so+sn)
    return b_meta[so+i:end].decode("utf-8")

to, tn = table(160) # type definitions
mo, mn = table(48)  # method definitions
io, ins = table(168) # image definitions

table_offset = 0x2a44168 # g_MethodPointers

images = []
all_methods = {}
for p in range(io, io+ins, 40):
    img = struct.unpack_from("<10I", b_meta, p)
    img_name = string(img[0])
    type_start = img[2]
    type_count = img[3]
    images.append((img_name, type_start, type_count))

print("Found images:", [x[0] for x in images])

for img_name, t_start, t_count in images:
    for t_idx in range(t_start, t_start + t_count):
        v = struct.unpack_from("<16I8H2I", b_meta, to + t_idx * 88)
        type_name = string(v[0])
        type_ns = string(v[1])
        full_cls = f"{type_ns}.{type_name}" if type_ns else type_name
        m_start = v[9]
        m_count = v[16]
        for m_i in range(m_count):
            k = m_start + m_i
            m = struct.unpack_from("<7I4H", b_meta, mo + k * 36)
            m_name = string(m[0])
            va = struct.unpack_from("<Q", b_dll, table_offset + k * 8)[0]
            all_methods[va] = f"{full_cls}::{m_name} ({img_name})"

print(f"Total methods mapped across ALL images: {len(all_methods)}")

# Now test target VA 0x181c94b91 or near:
target = 0x18150ee7b + 0x785d16
print(f"Checking target 0x{target:x}:")
for off in range(-32, 33):
    if (target + off) in all_methods:
        print(f"  Match offset {off}: {all_methods[target+off]}")
