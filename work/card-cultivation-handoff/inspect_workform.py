import json
import sys
sys.path.append('work/card-cultivation-handoff')
from extract_r01 import SerializedFile, parse_tree_node, TypeTreeReader

p = 'work/card-cultivation-handoff/unpacked/Entities/Animations/CAB-1a1bfa3d42a01b9de5c6aefcaee38f40'
sf = SerializedFile(p)

target_clips = {}
for path_id, obj in sf.objects.items():
    if obj['class_id'] == 74:
        t = sf.types[obj['type_id']]
        reader = TypeTreeReader(sf.raw_data, sf.data_offset + obj['byte_start'])
        clip_data, _ = parse_tree_node(t['nodes'], 0, reader)
        name = clip_data.get('m_Name')
        if name in ['WorkFormExpan01', 'WorkFormIdle', 'ProduceFromIdle']:
            target_clips[name] = clip_data

print("Found target clips:", list(target_clips.keys()))

for name, c in target_clips.items():
    print(f"\n==================== {name} ====================")
    print(f"m_SampleRate: {c.get('m_SampleRate')}")
    print(f"m_MuscleClipSize: {c.get('m_MuscleClipSize')}")
    bindings = c.get('m_ClipBindingConstant', {}).get('genericBindings', [])
    print(f"genericBindings count: {len(bindings)}")
    for i, b in enumerate(bindings):
        print(f"  [{i}] path={b.get('path')}, attribute={b.get('attribute')}, typeID={b.get('typeID')}, customType={b.get('customType')}, isPPtrCurve={b.get('isPPtrCurve')}, isIntCurve={b.get('isIntCurve')}")
    
    muscle = c.get('m_MuscleClip', {})
    val_deltas = muscle.get('m_ValueArrayDelta', [])
    print(f"m_ValueArrayDelta count: {len(val_deltas)}")
    for i, vd in enumerate(val_deltas):
        print(f"  [{i}] m_Start={vd.get('m_Start'):.4f}, m_Stop={vd.get('m_Stop'):.4f}")
    
    m_clip = muscle.get('m_Clip', {})
    streamed = m_clip.get('m_StreamedClip', {})
    dense = m_clip.get('m_DenseClip', {})
    constant = m_clip.get('m_ConstantClip', {})
    print("StreamedClip:", {k: v for k, v in streamed.items() if k != 'data'})
    print("DenseClip:", {k: v for k, v in dense.items() if k != 'm_SampleArray'})
    if 'm_SampleArray' in dense:
        sample_arr = dense['m_SampleArray']
        print(f"DenseClip m_SampleArray len: {len(sample_arr)}")
        # print first few samples
        print(f"DenseClip first 20 samples: {sample_arr[:20]}")
    print("ConstantClip:", constant)
