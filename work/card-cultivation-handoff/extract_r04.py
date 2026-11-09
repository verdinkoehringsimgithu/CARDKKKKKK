import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "work/card-cultivation-handoff"
REPORT_DIR = ROOT / "docs/handoffs/2026-11-09-卡牌桌面底层/证据"

with open(WORK / "inputactionasset_deserialized.json", "r", encoding="utf-8") as f:
    asset = json.load(f)

action_maps = asset.get("m_ActionMaps", [])
print(f"Total Action Maps: {len(action_maps)}")

maps_summary = []
for m in action_maps:
    map_name = m.get("m_Name")
    actions = m.get("m_Actions", [])
    bindings = m.get("m_Bindings", [])

    print(f"\n==========================================")
    print(f"Action Map: {map_name} ({len(actions)} actions, {len(bindings)} bindings)")
    print(f"==========================================")

    act_list = []
    for a in actions:
        a_name = a.get("m_Name")
        a_type = a.get("m_Type")
        a_id = a.get("m_Id")
        # Find bindings for this action
        a_bindings = []
        for b in bindings:
            if b.get("m_Action") == a_name or b.get("m_Id") == a_id:
                path = b.get("m_Path")
                interactions = b.get("m_Interactions")
                composite = b.get("m_Composite")
                part_of_composite = b.get("m_PartOfComposite")
                a_bindings.append({
                    "path": path,
                    "interactions": interactions,
                    "composite": composite,
                    "name": b.get("m_Name")
                })
                print(f"    Binding: {path} (interactions={interactions}, name={b.get('m_Name')})")
        act_list.append({
            "name": a_name,
            "type": a_type,
            "bindings": a_bindings
        })
    maps_summary.append({
        "map_name": map_name,
        "actions": act_list
    })

# Save full action maps JSON
REPORT_DIR.mkdir(parents=True, exist_ok=True)
json_path = REPORT_DIR / "R04-输入映射与键位表.json"
with open(json_path, "w", encoding="utf-8") as f:
    json.dump({
        "evidence_level": "E1",
        "source_asset": "assets/gamemain/configs/inputcontrols.inputactions",
        "pathID": 418656196238816513,
        "action_maps": maps_summary
    }, f, ensure_ascii=False, indent=2)

print(f"\nSaved JSON to {json_path}")
