"""解析 RemoteResource.dat (GameFramework BuiltinVersionListSerializer V2)，输出完整资源清单与依赖图。"""
import struct
import io
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DAT_PATH = ROOT / 'Card Cultivation/CardCultivation_Data/StreamingAssets/RemoteResource.dat'
OUT_JSON = ROOT / 'docs/handoffs/2026-11-09-卡牌桌面底层/复核/RemoteResource完整清单.json'

def parse_from_bytes(data: bytes, out_json_path=None):
    stream = io.BytesIO(data)
    magic = stream.read(3)
    if magic != b'ECP':
        raise ValueError(f"Invalid magic: {magic}")
    ver_b = stream.read(1)
    if len(ver_b) != 1:
        raise EOFError("EOF reading version")
    version = ver_b[0]
    if version != 2:
        raise ValueError(f"Unsupported version: {version}")
    encrypt_bytes = stream.read(4)
    if len(encrypt_bytes) != 4:
        raise EOFError("EOF reading encrypt bytes")

    def read_exact(n):
        b = stream.read(n)
        if len(b) != n:
            raise EOFError(f"Unexpected EOF: expected {n} bytes, got {len(b)} at offset {stream.tell()}")
        return b

    def read_7bit_int():
        val = 0
        shift = 0
        bytes_read = 0
        while True:
            b = read_exact(1)
            bytes_read += 1
            if bytes_read > 5:
                raise ValueError(f"7-bit int exceeded max 5 bytes at offset {stream.tell()}")
            byte_val = b[0]
            val |= (byte_val & 0x7F) << shift
            if (byte_val & 0x80) == 0:
                break
            shift += 7
        return val

    def read_string():
        length_b = read_exact(1)
        length = length_b[0]
        if length == 0:
            return ""
        raw = read_exact(length)
        decrypted = bytes([raw[i] ^ encrypt_bytes[i % 4] for i in range(len(raw))])
        return decrypted.decode('utf-8', errors='strict')

    app_version = read_string()
    internal_res_version = read_7bit_int()
    asset_count = read_7bit_int()

    assets = []
    for i in range(asset_count):
        name = read_string()
        dep_count = read_7bit_int()
        deps = [read_7bit_int() for _ in range(dep_count)]
        assets.append({'index': i, 'name': name, 'dep_count': dep_count, 'deps': deps})

    resource_count = read_7bit_int()
    resources = []
    for i in range(resource_count):
        r_name = read_string()
        r_variant = read_string()
        r_ext = read_string()
        load_type = stream.read(1)[0]
        length = read_7bit_int()
        hash_code = struct.unpack('<i', stream.read(4))[0]
        r_asset_count = read_7bit_int()
        r_asset_indices = [read_7bit_int() for _ in range(r_asset_count)]
        resources.append({
            'index': i,
            'name': r_name,
            'variant': r_variant,
            'ext': r_ext,
            'load_type': load_type,
            'length': length,
            'hash_code': hash_code,
            'asset_count': r_asset_count,
            'asset_indices': r_asset_indices
        })

    group_count = read_7bit_int()
    groups = []
    for i in range(group_count):
        g_name = read_string()
        g_res_count = read_7bit_int()
        g_res_indices = [read_7bit_int() for _ in range(g_res_count)]
        groups.append({'name': g_name, 'resources': g_res_indices})

    # FileSystems 字段解析（第 4 个标准数组，紧跟 ResourceGroups 之后）
    file_system_count = read_7bit_int()
    file_systems = []
    for i in range(file_system_count):
        fs_name = read_string()
        fs_res_count = read_7bit_int()
        fs_res_indices = [read_7bit_int() for _ in range(fs_res_count)]
        file_systems.append({'name': fs_name, 'resources': fs_res_indices})

    consumed = stream.tell()
    remaining = stream.read()
    assert len(remaining) == 0, f"Unparsed trailing bytes remaining: {len(remaining)} at offset {consumed}"
    assert consumed == len(data), f"Consumed {consumed} != total {len(data)}"

    summary = {
        'file': 'CardCultivation_Data/StreamingAssets/RemoteResource.dat',
        'format': 'GameFramework BuiltinVersionListSerializer V2 (Magic: ECP, Version: 2)',
        'encrypt_key_hex': encrypt_bytes.hex(),
        'applicable_game_version': app_version,
        'internal_resource_version': internal_res_version,
        'total_assets': asset_count,
        'total_bundles': resource_count,
        'total_groups': group_count,
        'total_file_systems': file_system_count,
        'consumed_bytes': consumed,
        'eof_verified': (consumed == len(data)),
        'bundles': [
            {
                'name': r['name'] + (('.' + r['ext']) if r['ext'] else '.dat'),
                'raw_name': r['name'],
                'length': r['length'],
                'asset_count': r['asset_count'],
                'load_type': r['load_type'],
                'hash_code': r['hash_code'],
                'sample_assets': [assets[idx]['name'] for idx in r['asset_indices'][:5]]
            }
            for r in resources
        ]
    }

    full_data = {
        'summary': summary,
        'resources': resources,
        'assets': assets,
        'groups': groups,
        'file_systems': file_systems
    }

    target_out = out_json_path or OUT_JSON
    target_out.parent.mkdir(parents=True, exist_ok=True)
    with open(target_out, 'w', encoding='utf-8') as f:
        json.dump(full_data, f, ensure_ascii=False, indent=2)

    print(f"Parsed {consumed}/{len(data)} bytes (EOF verified). Saved to {target_out}")
    print(f"Summary: {resource_count} bundles, {asset_count} assets, {group_count} groups, {file_system_count} file systems.")
    return full_data

def parse():
    with open(DAT_PATH, 'rb') as f:
        data = f.read()
    return parse_from_bytes(data, OUT_JSON)

if __name__ == '__main__':
    parse()
    # 严格自测：任意截断（哪怕 1 字节）必须被严格拒绝
    with open(DAT_PATH, 'rb') as f:
        full_data = f.read()
    try:
        parse_from_bytes(full_data[:-1])
        raise AssertionError("Security check failed: truncated stream did not raise EOFError!")
    except EOFError:
        print("Self-test passed: Truncated stream strictly raised EOFError.")
