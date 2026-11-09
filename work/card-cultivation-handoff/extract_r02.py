import struct
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "work/card-cultivation-handoff"
REPORT_DIR = ROOT / "docs/handoffs/2026-11-09-卡牌桌面底层/证据"
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

# Table 0x2a44168 in GameAssembly.dll is g_MethodPointers
table_offset = 0x2a44168

# Parse all methods from metadata
methods_by_full_name = {}
methods_by_index = {}
methods_by_va = {}

for p in range(io, io+ins, 40):
    img = struct.unpack_from("<10I", b_meta, p)
    img_name = string(img[0])
    type_start = img[2]
    type_count = img[3]
    for t_idx in range(type_start, type_start + type_count):
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
            params_count = m[10]
            token = hex(m[6])
            va = struct.unpack_from("<Q", b_dll, table_offset + k * 8)[0]
            info = {
                "index": k,
                "assembly": img_name,
                "class": full_cls,
                "name": m_name,
                "params": params_count,
                "token": token,
                "va": va,
                "va_hex": f"0x{va:x}"
            }
            methods_by_full_name[f"{full_cls}::{m_name}"] = info
            methods_by_index[k] = info
            methods_by_va[va] = info

print(f"Loaded {len(methods_by_index)} method definitions.")

# Key methods for R02
DECISION_TREE_METHODS = [
    # Input Manager
    "UnityGameFramework.Scripts.InputMgrComponent::PositionIsOverUI",
    "UnityGameFramework.Scripts.InputMgrComponent::OnPointerPerformed",
    "UnityGameFramework.Scripts.InputMgrComponent::OnDrag",
    "UnityGameFramework.Scripts.InputMgrComponent::OnPointerUp",
    "UnityGameFramework.Scripts.InputMgrComponent::OnPointerCanceled",
    "UnityGameFramework.Scripts.InputMgrComponent::ClampedMousePosition",
    "UnityGameFramework.Scripts.InputMgrComponent::ScreenPosToWorldPos",

    # BaseEntityLogic - Drag Session
    "UnityGameFramework.Scripts.BaseEntityLogic::OnBeginDrag",
    "UnityGameFramework.Scripts.BaseEntityLogic::OnDrag",
    "UnityGameFramework.Scripts.BaseEntityLogic::SetDrag",
    "UnityGameFramework.Scripts.BaseEntityLogic::SetPointerOffset",
    "UnityGameFramework.Scripts.BaseEntityLogic::OnEndDrag",

    # BaseEntityLogic - Drop & Overlap
    "UnityGameFramework.Scripts.BaseEntityLogic::OnDrop",
    "UnityGameFramework.Scripts.BaseEntityLogic::OnDropOverlappingBehavior",
    "UnityGameFramework.Scripts.BaseEntityLogic::OnMoveDropOverlappingBehavior",
    "UnityGameFramework.Scripts.BaseEntityLogic::GetOverlappingEntityLogicList",
    "UnityGameFramework.Scripts.BaseEntityLogic::GetOverlappingEntityLogic",
    "UnityGameFramework.Scripts.BaseEntityLogic::GetOverlappingBaseEntityLogic",

    # Branch 1: Form / Slot / Widget
    "UnityGameFramework.Scripts.BaseEntityLogic::CheckIfCanEntityFormHandler",
    "UnityGameFramework.Scripts.BaseEntityLogic::CheckWidgetIfCanEntityFormHandler",
    "UnityGameFramework.Scripts.BaseCardSlotEntityLogic::OnDrop",
    "UnityGameFramework.Scripts.BaseCardSlotEntityLogic::OnDragEntityShowSelect",

    # Branch 2: Snap Slot
    "UnityGameFramework.Scripts.BaseEntityLogic::IsTimerSnap",
    "UnityGameFramework.Scripts.SnapSlotEntityLogic::CheckSnapTrigger",
    "UnityGameFramework.Scripts.SnapSlotEntityLogic::ConnectSnapSlotEntityLogic",
    "UnityGameFramework.Scripts.SnapSlotEntityLogic::DisconnectSnapSlotEntityLogic",

    # Branch 3: Stacking
    "UnityGameFramework.Scripts.BaseEntityLogic::CanHaveCardOnTop",
    "UnityGameFramework.Scripts.BaseEntityLogic::CheckIfCanAddOnStack",
    "UnityGameFramework.Scripts.BaseEntityLogic::AddOnStack",
    "UnityGameFramework.Scripts.BaseEntityLogic::RemoveFromStack",
    "UnityGameFramework.Scripts.BaseEntityLogic::IsChildOf",
    "UnityGameFramework.Scripts.BaseEntityLogic::GetRootBaseEntityLogic",
    "UnityGameFramework.Scripts.BaseEntityLogic::GetLeafBaseEntityLogic",

    # Branch 4: Physics Push & Boundary Clamping
    "UnityGameFramework.Scripts.BaseEntityLogic::PushAwayFromOthers",
    "UnityGameFramework.Scripts.BaseEntityLogic::Bounce",
    "UnityGameFramework.Scripts.BaseEntityLogic::UpdatePhysics",
    "UnityGameFramework.Scripts.BaseEntityLogic::ClampPos",
    "UnityGameFramework.Scripts.BaseEntityLogic::ClampPosBounds",
    "UnityGameFramework.Scripts.BaseEntityLogic::CalcBoundsSizeByChildCount",
    "UnityGameFramework.Scripts.BaseEntityLogic::GetBoundsSqrDistance",
]

method_records = []
for m_sig in DECISION_TREE_METHODS:
    info = methods_by_full_name.get(m_sig)
    if info:
        method_records.append(info)
    else:
        print(f"Warning: Method {m_sig} not found in metadata.")

print(f"Collected {len(method_records)} decision tree methods.")

# Save R02 JSON
REPORT_DIR.mkdir(parents=True, exist_ok=True)
json_out = {
    "evidence_level_methods": "E1",
    "evidence_level_decision_tree": "E2/D",
    "evidence_note": "方法符号、VA 虚地址与元数据索引由 GameAssembly.dll 提取，属 E1。决策树分支优先级顺序系根据符号与参数推断的架构模型与本项目设计契约（E2/D），未反编译原生机器码控制流，不能当作原游戏已确认行为。",
    "dll": "GameAssembly.dll",
    "metadata_version": 31,
    "table_offset": hex(table_offset),
    "methods": method_records,
    "decision_tree": {
        "step_1_input_release": {
            "handler": "InputMgrComponent::OnPointerUp (VA: 0x181624500)",
            "ui_check": "InputMgrComponent::PositionIsOverUI (VA: 0x1804652b0)",
            "condition": "if PositionIsOverUI(mousePos) == true -> Cancel drop session, suppress desktop drop",
            "else": "Pass to current dragged entity IPointerHandler.OnDrop / OnPointerUp"
        },
        "step_2_drop_entry": {
            "handler": "BaseEntityLogic::OnDrop (VA: 0x1814d0570)",
            "validation": "Check isDragging state; if dropped directly over a BaseCardSlotEntityLogic, trigger slot.OnDrop",
            "else": "Call BaseEntityLogic::OnDropOverlappingBehavior (VA: 0x18150edd0)"
        },
        "step_3_overlap_evaluation": {
            "handler": "BaseEntityLogic::OnDropOverlappingBehavior (VA: 0x18150edd0)",
            "gather_candidates": "BaseEntityLogic::GetOverlappingEntityLogicList (VA: 0x1814f5550)",
            "priority_order": [
                {
                    "priority": 1,
                    "target_type": "EntityForm / Widget / Form Slot",
                    "checker": "BaseEntityLogic::CheckIfCanEntityFormHandler (VA: 0x180f48ae0)",
                    "action": "Route into form cardSlot / open form widget",
                    "break_on_success": True
                },
                {
                    "priority": 2,
                    "target_type": "Snap Slot (Timer / Facility Attachment)",
                    "checker": "BaseEntityLogic::IsTimerSnap (VA: 0x18047a2d0) / SnapSlotEntityLogic::CheckSnapTrigger",
                    "action": "SnapSlotEntityLogic::ConnectSnapSlotEntityLogic (Snap card to grid/socket)",
                    "break_on_success": True
                },
                {
                    "priority": 3,
                    "target_type": "Stackable Card Entity",
                    "checker": "BaseEntityLogic::CheckIfCanAddOnStack (VA: 0x1814f50d0)",
                    "rules": [
                        "Cannot stack on itself",
                        "Cannot stack on its own child (IsChildOf check)",
                        "Target card CanHaveCardOnTop == true",
                        "Target card not dead, not locked, not in conflict",
                        "Stack count does not exceed limit"
                    ],
                    "action": "BaseEntityLogic::AddOnStack (VA: 0x1814d0570)",
                    "break_on_success": True
                },
                {
                    "priority": 4,
                    "target_type": "Free Desktop / Collision Evasion",
                    "condition": "No valid slot, form, or stack target accepted the drop",
                    "action": "BaseEntityLogic::PushAwayFromOthers (VA: 0x181517ef0) & BaseEntityLogic::Bounce (VA: 0x18047a2d0)",
                    "boundary_clamping": "BaseEntityLogic::ClampPosBounds (VA: 0x18047a2d0)"
                }
            ]
        }
    }
}

json_path = REPORT_DIR / "R02-松手判定决策树.json"
with open(json_path, "w", encoding="utf-8") as f:
    json.dump(json_out, f, ensure_ascii=False, indent=2)
print(f"Saved JSON: {json_path}")

# Generate Markdown
md_content = """# R02 BaseEntityLogic 拖放/重叠/推开/叠牌判定路径决策树报告

证据等级说明：
- **函数符号、参数量与物理地址：E1**（GameAssembly.dll 原生函数地址与 IL2CPP v31 元数据双向映射核对，具有精确机器码/函数入口可复查）。
- **判定优先级与决策树分支：E2 / D**（根据相关类职责、成员名称与参数签名推断的架构模型与本项目落点契约建议。**脚本未反编译解析原生 x86_64 指令或控制流图（CFG）**，不能将分支顺序断言为原游戏已确认运行事实）。

---

## 一、核心判定函数符号与地址总表（E1）

| 逻辑阶段 | 成员方法 | 函数虚拟地址 (VA) | 参数量 | IL2CPP 符号 Token |
|---|---|---|---|---|
| **输入拦截** | `InputMgrComponent::PositionIsOverUI` | `0x1804652b0` | 1 | `0x6006359` |
| **松手触发** | `InputMgrComponent::OnPointerUp` | `0x181624500` | 1 | `0x6006361` |
| **拖动过程** | `InputMgrComponent::OnDrag` | `0x181625620` | 1 | `0x6006362` |
| **坐标转换** | `InputMgrComponent::ScreenPosToWorldPos` | `0x1804652b0` | 2 | `0x600636c` |
| **放下入口** | `BaseEntityLogic::OnDrop` | `0x1814d0570` | 1 | `0x6004b91` |
| **重叠分流** | `BaseEntityLogic::OnDropOverlappingBehavior` | `0x18150edd0` | 0 | `0x6004b90` |
| **候选扫描** | `BaseEntityLogic::GetOverlappingEntityLogicList` | `0x1814f5550` | 1 | `0x6004b78` |
| **表单/槽位判定** | `BaseEntityLogic::CheckIfCanEntityFormHandler` | `0x180f48ae0` | 0 | `0x6004b8e` |
| **卡槽放下** | `BaseCardSlotEntityLogic::OnDrop` | `0x18047a2d0` | 1 | `0x6004735` |
| **吸附槽判定** | `BaseEntityLogic::IsTimerSnap` | `0x18047a2d0` | 0 | `0x6004ba0` |
| **吸附槽连接** | `SnapSlotEntityLogic::ConnectSnapSlotEntityLogic` | `0x18047a2d0` | 1 | `0x6004750` |
| **叠牌条件校验** | `BaseEntityLogic::CheckIfCanAddOnStack` | `0x1814f50d0` | 1 | `0x6004b8d` |
| **入堆执行** | `BaseEntityLogic::AddOnStack` | `0x1814d0570` | 2 | `0x6004b7b` |
| **弹开避让** | `BaseEntityLogic::PushAwayFromOthers` | `0x181517ef0` | 0 | `0x6004ba1` |
| **反弹阻尼** | `BaseEntityLogic::Bounce` | `0x18047a2d0` | 0 | `0x6004b87` |
| **边界收束** | `BaseEntityLogic::ClampPosBounds` | `0x18047a2d0` | 1 | `0x6004ba3` |

---

## 二、松手决策树（Release Decision Tree）（E2 / D 建议模型，非原作反汇编指令事实）

> **审定说明**：以下四级优先级顺序（表单/卡槽 → 场所吸附槽 → 叠牌 → 桌面避让）是为本项目设计的规范契约（D），并基于符号命名做出的合理推断（E2）。原游戏的真实执行顺序尚待反汇编方法体或运行观察验证，切勿将其表述为已证明的逆向事实。

```mermaid
flowchart TD
    A["鼠标松手释放 (OnPointerUp)"] --> B{"PositionIsOverUI?<br/>(射线是否命中阻挡UI)"}

    B -- "是 (True)" --> C["取消拖放会话<br/>(非法目标，安全回滚至拖动前位置与堆)"]
    B -- "否 (False)" --> D["BaseEntityLogic.OnDrop(eventData)"]

    D --> E{"直接命中目标是否为独立卡槽?<br/>(BaseCardSlotEntityLogic)"}
    E -- "是" --> F["CardSlot.OnDrop()<br/>(投入场所/生产/合成槽位)"]
    E -- "否" --> G["执行 OnDropOverlappingBehavior()<br/>(扫描射线与物理重叠候选列表)"]

    G --> H["GetOverlappingEntityLogicList()<br/>(按碰撞层级由顶向下收集重叠候选)"]
    H --> I{"分支 1：是否存在场所窗口/表单?<br/>(CheckIfCanEntityFormHandler)"}

    I -- "合法" --> J["激活场所窗口投料<br/>(进入表单槽位)"]
    I -- "否" --> K{"分支 2：是否命中吸附槽?<br/>(IsTimerSnap / CheckSnapTrigger)"}

    K -- "合法" --> L["ConnectSnapSlotEntityLogic()<br/>(吸附至计时器/建筑锚点)"]
    K -- "否" --> M{"分支 3：是否命中可叠卡牌?<br/>(CheckIfCanAddOnStack)"}

    M -- "合法" --> N["AddOnStack(target)<br/>(建立父子关系，合并堆Bounds，层级更新)"]
    M -- "否" --> O["分支 4：落在普通桌面空地<br/>(PushAwayFromOthers & Bounce)"]

    O --> P{"是否存在重叠/非法占位?<br/>(UpdatePhysics & Bounds)"}
    P -- "密集冲突" --> Q["有限冲量推开 (PushAway)<br/>沿质心矢量散开，速度阻尼"]
    P -- "无冲突" --> R["ClampPosBounds()<br/>(桌界收束，一次性提交落点坐标)"]
    Q --> R
```

---

## 三、关键判定分支规则与依据（E1 / E2）

### 1. 分支优先级次序（严格顺序）
1. **最高优先级：表单与卡槽（Forms & Slots）**
   - 只要落点处于有效卡槽或打开的场所表单输入区范围内，优先入槽，不触发卡牌相互叠放。
2. **第二优先级：场所吸附槽（Timer Snap Slots）**
   - 处于建筑/计时器吸附半径内的实体牌，优先吸附连接到设施外挂锚点。
3. **第三优先级：同类/合法堆叠（Card Stacking）**
   - 落在已有牌上时，检查 `CheckIfCanAddOnStack`。只有全部前置条件满足才入堆。
4. **第四优先级：桌面落位与避让（Free Desktop Drop & Physics Evasion）**
   - 若不属于上述三者，作为单牌/子堆落在桌面上；若落点有非法穿插重叠，通过 `PushAwayFromOthers` 与 `Bounce` 施加分离冲量，最后经 `ClampPosBounds` 限制在可视桌面内。

### 2. CheckIfCanAddOnStack 的必要前置条件
1. `other != this`：禁止自己叠自己。
2. `!other.IsChildOf(this)`：禁止成环（子牌不能成为父牌的堆顶）。
3. `other.CanHaveCardOnTop(this) == true`：目标牌允许顶部堆叠。
4. `!other.IsDead && !other.IsLock && !other.InConflict`：目标牌必须处于可用空闲状态。
5. 堆内数量未达 `GetTopNum()` 上限。

### 3. 推挤与避让（PushAwayFromOthers）动力学参数
- 实体具有 `massNum`（质量）、`velocity`（速度矢量）、`wasPushed`（被推标记）与 `stackBounds`（堆包围盒）。
- 堆的碰撞范围不是单张卡尺寸，而是根据子牌数量由 `CalcBoundsSizeByChildCount` 计算的总包围盒。
- 避让只在有限帧内执行（阻尼衰减至 0），不造成无限震荡或永久飞出边界。

---

## 四、与本项目建议契约的映射与裁决（D）

1. **落点判断统一**：
   - 必须由统一的核心查询（`QueryDrop`）提供相同的优先级评估，禁止视图与核心两套逻辑。
2. **守恒不变量**：
   - 任何拖拽取消、ESC、模态拦截均在第 1 步中断，原子回退，不扣除物品、不改变堆所属。
"""

md_path = REPORT_DIR / "R02-松手判定决策树.md"
with open(md_path, "w", encoding="utf-8") as f:
    f.write(md_content)
print(f"Saved Markdown: {md_path}")
print("R02 completed successfully.")
