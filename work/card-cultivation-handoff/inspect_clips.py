import sys
import json
from pathlib import Path
sys.path.append('work/card-cultivation-handoff')
from extract_r01 import SerializedFile, parse_tree_node, TypeTreeReader

paths = [
    'work/card-cultivation-handoff/unpacked/Entities/Animations/CAB-1a1bfa3d42a01b9de5c6aefcaee38f40',
    'work/card-cultivation-handoff/unpacked/Entities/Timer/CAB-87748fae2fb1dd2d4c105ca6214540f4'
]

results = []
for p in paths:
    sf = SerializedFile(p)
    cab_name = Path(p).parent.name
    for path_id, obj in sf.objects.items():
        if obj['class_id'] == 74:
            t = sf.types[obj['type_id']]
            reader = TypeTreeReader(sf.raw_data, sf.data_offset + obj['byte_start'])
            clip_data, _ = parse_tree_node(t['nodes'], 0, reader)
            name = clip_data.get('m_Name')
            sample_rate = clip_data.get('m_SampleRate')
            bindings = clip_data.get('m_ClipBindingConstant', {}).get('genericBindings', [])
            muscle = clip_data.get('m_MuscleClip', {})
            val_deltas = muscle.get('m_ValueArrayDelta', [])
            m_clip = muscle.get('m_Clip', {})
            streamed = m_clip.get('m_StreamedClip', {})
            dense = m_clip.get('m_DenseClip', {})
            constant = m_clip.get('m_ConstantClip', {})
            results.append({
                'cab': cab_name,
                'path_id': path_id,
                'name': name,
                'sample_rate': sample_rate,
                'bindings_count': len(bindings),
                'bindings': bindings,
                'val_deltas_count': len(val_deltas),
                'val_deltas': val_deltas,
                'streamed_curve_count': streamed.get('m_CurveCount', 0),
                'dense_frame_count': dense.get('m_FrameCount', 0),
                'dense_curve_count': dense.get('m_CurveCount', 0),
                'constant_val_count': len(constant.get('data', [])) if isinstance(constant, dict) else 0,
                'raw_clip': clip_data
            })
            print(f"[{cab_name}] {name}: bindings={len(bindings)}, val_deltas={len(val_deltas)}, streamed_curves={streamed.get('m_CurveCount', 0)}, dense_frames={dense.get('m_FrameCount', 0)}")

with open('work/card-cultivation-handoff/clips_summary.json', 'w', encoding='utf-8') as f:
    # Save serializable subset
    out = []
    for r in results:
        c = dict(r)
        c.pop('raw_clip')
        out.append(c)
    json.dump(out, f, indent=2, ensure_ascii=False)
