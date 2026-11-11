import struct
import json
import sys
sys.path.append('work/card-cultivation-handoff')
from extract_r01 import SerializedFile, parse_tree_node, TypeTreeReader

p = 'work/card-cultivation-handoff/unpacked/Entities/Animations/CAB-1a1bfa3d42a01b9de5c6aefcaee38f40'
sf = SerializedFile(p)

clip_data = None
for path_id, obj in sf.objects.items():
    if obj['class_id'] == 74:
        t = sf.types[obj['type_id']]
        reader = TypeTreeReader(sf.raw_data, sf.data_offset + obj['byte_start'])
        c, _ = parse_tree_node(t['nodes'], 0, reader)
        if c.get('m_Name') == 'WorkFormExpan01':
            clip_data = c
            break

d = clip_data['m_MuscleClip']['m_Clip']['data']
streamed = d['m_StreamedClip']
raw_u32 = streamed['data'] # list of uint32
raw_bytes = struct.pack(f'<{len(raw_u32)}I', *raw_u32)

print(f"Total uint32: {len(raw_u32)}, Total bytes: {len(raw_bytes)}")

# In Unity, StreamedClip.data consists of StreamedFrames:
# struct StreamedFrame {
#    float time;
#    int curveCount;
#    StreamedCurveKey curves[curveCount];
# }
# struct StreamedCurveKey {
#    int index;
#    float coeff[4]; // or value, inSlope, outSlope or similar cubic Hermite coefficients
# }

offset = 0
frame_idx = 0
frames = []

while offset + 8 <= len(raw_bytes):
    time, curve_count = struct.unpack_from('<fI', raw_bytes, offset)
    print(f"\n--- Frame {frame_idx}: time={time:.4f}s, curve_count={curve_count} (offset={offset}) ---")
    offset += 8
    
    # Let's inspect the curve keys in this frame
    # A standard Unity StreamedCurveKey has index (uint32) and coeff[4] (4 floats) -> 20 bytes
    # or index and value/slopes. Let's see how many bytes per curve
    frame_curves = []
    # Check if curve_count is reasonable
    if curve_count > 50 or curve_count < 0:
        print(f"Unreasonable curve count {curve_count}, stopping.")
        break
        
    for c_i in range(curve_count):
        if offset + 20 > len(raw_bytes):
            print(f"Truncated at curve {c_i}")
            break
        c_idx, c0, c1, c2, c3 = struct.unpack_from('<Iffff', raw_bytes, offset)
        offset += 20
        frame_curves.append({
            'curve_idx': c_idx,
            'c0': c0,
            'c1': c1,
            'c2': c2,
            'c3': c3
        })
        print(f"  Curve [{c_idx}]: coeff0={c0:.4f}, coeff1={c1:.4f}, coeff2={c2:.4f}, coeff3={c3:.4f}")
    
    frames.append({'time': time, 'curves': frame_curves})
    frame_idx += 1
    if offset >= len(raw_bytes):
        break
