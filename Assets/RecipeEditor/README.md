# 游戏配方与工艺工坊系统规范（Recipe & Workshop Editor）

本模块位于 `Assets/RecipeEditor`，提供跨 Unity 与独立便携网页版（Web/EXE）双端运行的游戏配方与工坊编辑器。系统采用数据驱动、纯 C# 模型与标准 JSON 序列化，专为硬核生存、卡牌工艺制造类桌面游戏打造。

---

## 目录
1. [双端运行入口](#一双端运行入口)
2. [核心设计哲学与生存规则范式](#二核心设计哲学与生存规则范式)
3. [五大核心实体结构](#三五大核心实体结构)
4. [配方机制与主副产物模型](#四配方机制与主副产物模型)
5. [副产线与伴生修正系统（概率覆盖与拦截防出）](#五副产线与伴生修正系统概率覆盖与拦截防出)
6. [能力检定与卡牌点数贡献系统（冶炼、烹饪与分支选择）](#六能力检定与卡牌点数贡献系统冶炼烹饪与分支选择)
7. [拆包游戏参考展台（791 项全量官方数据）](#七拆包游戏参考展台791-项全量官方数据)
8. [标准配方、副产线与能力检定 JSON 规范（供其他 AI 对齐）](#八标准配方副产线与能力检定-json-规范供其他-ai-对齐)
9. [数据存取与完整性校验](#九数据存取与完整性校验)

---

## 一、双端运行入口

1. **Unity 编辑器端（EditorWindow）**：
   * 菜单入口：`卡牌桌面 > 配方编辑器` 或 `工具 > 配方编辑器`；
   * 绑定 `RecipeDatabaseAsset`（ScriptableObject 资产），支持一键“保存资产”、“导出 JSON”与“从 JSON 导入（增量合并 / 覆盖）”。
2. **独立便携网页端（原生单文件，无网络/无外部依赖）**：
   * 运行方式：直接双击根目录的 **`启动配方编辑器.bat`** 或 **`配方编辑器.html`**；
   * 文件源码：位于 `Tools/RecipeEditorWeb/index.html`；
   * 特性：纯原生纯单文件，经典 Windows 灰白紧凑 UI，支持浏览器本地保存、文件导入导出、一键克隆与搜索。两端 JSON 格式 100% 互通。
   * *注*：如在浏览器中未看到最新选项卡，按 `Ctrl + F5` 强制刷新即可。

---

## 二、核心设计哲学与生存规则范式

系统参考《卡牌生存：热带岛屿》、《奇幻森林》、《饥荒》和《别冻僵》，遵循四项底层法则：

### 1. 人员与工具的损耗维度分离
* **在场操作人员（Operators）**：代表村民、工匠、角色、学徒等生命个体。**不消耗卡牌本体，也没有卡牌耐久度**。人员的疲劳负荷与风险通过在配方的**副产物列表**中产出**【生理状态/病痛卡】**（如水疱、疲劳、脱水、割伤、中暑）来结算。
* **工具与器具（Tools）**：属于物理器材，不作为原料被整张卡吃掉，但**消耗自身耐久值（DurabilityCost）**。
* **工位设施（Workstations）**：作为制作发生的场所实体（如营火、土窑、大锅），**支持单次损耗度（DurabilityCost）**，反映设施磨损或燃料消耗。
* **原料（Ingredients）**：作为基础耗材，**完全消耗扣除**。

### 2. 标签与属性的正交分层
* **复用标签（Tag）**：只用于配方原料的**大类模糊匹配**（如 `#肉类`、`#燃料`、`#水`、`#矿石`），坚决避免在标签里堆砌“生食、熟食、干粮、永存”导致标签膨胀。
* **状态机链条（Transformation）**：
  * “生食 ➔ 熟食 ➔ 焦糊”走卡牌自身的**自身转变链**（或时间推进）；
  * 烧糊不是一种食材标签，而是一张**烹饪超时的惩罚性垃圾卡**（如【焦黑碳块】）。
* **保质期与永存**：走卡牌的**数值属性（Properties）**（如 `SpoilTime: 48小时`）；干粮/罐头等永存物品直接设为 `0` 或空，不设“永存标签”。

---

## 三、五大核心实体结构

### 1. 物品库（RecipeItem）
* `Id` (string)：全局唯一物品 ID。
* `Name` (string)：物品显示名。
* `Category` (string)：分类（材料、食物、工具、设施、人员个体等）。
* `MaxStack` (int)：最大堆叠上限（1 为不可堆叠）。
* `TagIds` (List\<string\>)：关联复用池中的标签 ID 列表。
* `Properties` (List\<RecipePropertyEntry\>)：自定义键值属性（如营养度、燃料值、腐败时间等）。
* `AbilityContributions` (List\<ItemAbilityContribution\>)：卡牌携带的默认能力点数与折算率。

### 2. 复用标签池（RecipeTag）
* `Id` (string)：标签唯一 ID（如 `tag_fuel`、`tag_meat`）。
* `Name` (string)：标签名。
* `ColorHex` (string)：色彩标识。
* `Category` (string)：标签分类。
* `Description` (string)：说明与引用计数。

### 3. 能力定义池（RecipeAbility）
* `Id` (string)：全局能力 ID（如 `heat`、`cooking`、`smelting`、`strength`）。
* `Name` (string)：能力名称。
* `Category` (string)：属性分类（物理/环境、技艺/专精、工匠/制造、角色/生理等）。
* `IconOrEmoji` (string)：图标或 Emoji。
* `ColorHex` (string)：色彩标识。
* `Description` (string)：说明与检定标准含义。

### 4. 合成场所 / 工位设施（RecipeLocation）
* `Id` (string)：场所唯一 ID。
* `Name` (string)：场所名。
* `SourceType` (enum)：
  * `0 (Custom)`：独立/全局场所（如随身、海滩、祭坛）；
  * `1 (Item)`：**实体物品作为工位**（如制作出的营火、土窑、工作台实体卡牌绑定为工位）。
* `BoundItemId` (string)：当 SourceType 为 1 时绑定的物品 ID。
* `Capacity` (int)：工位容量（可同时进行的合成并发数）。
* `DurabilityCost` (int)：单次制作对工位造成的设施磨损/燃料损耗（默认 0 暂不设）。
* `FeatureTags` (List\<string\>)：场所环境特性（如 `高温`、`通风`、`水源`）。

### 5. 配方定义与副产线（RecipeDefinition & RecipeAuxiliaryLine）
* 配方承载原料投入、操作人员、能力检定需求与产物分支；
* 副产线承载跨配方伴生修饰、概率覆盖与产出拦截。

---

## 四、配方机制与主副产物模型

配方模型由 `RecipeDefinition` 承载，支持两类核心制作模式：

### 1. 标准多对多合成（RecipeType.Synthesis）
* **等式左侧：原料与工具输入（Inputs）——不限数量**：
  * 支持按具体物品（ExactItem）或按标签（ByTag）匹配；
  * `Consume == true`：原料（完全消耗）；
  * `Consume == false`：工具/器具（非耗材·可损耗，带 `DurabilityCost` 磨损字段）；
  * `EfficiencyMultiplier`：该工序下的投入效率乘数（默认 1.0）。
* **等式右侧：产物列表（Outputs）——不限数量，主副分离**：
  * `OutputType`：明确标记为 **★ 主产物（Primary = 0）** 或 **⚑ 副产物（Byproduct = 1）**；
  * `TriggerCondition`：触发条件分支（**常规产出**、**★检定达标专属产出**、**⚠️未达标失败分支产出**）；
  * 支持概率产出（`Chance`: 0.0 ~ 1.0）；
  * 支持配置任意数量的主产物与副产物。

### 2. 自身转变配方（RecipeType.Transformation）
模拟物品的质变、烘烤、发酵、干燥或风化：
* **转变主体**：源物品 A（`SourceItemId`）及消耗数量；
* **附加条件列表（不限数量）**：
  * 所需工具（`RequireTool`，支持设置损耗度）；
  * 辅料消耗（`RequireMaterial`）；
  * 在场操作人员（`RequireOperator`，不消耗）；
  * 等待时间（`DurationMinutes`）；
  * 环境/温度门槛（`PropertyThreshold`）；
* **目标产物**：目标产物物品 B（主产物）与伴生副产物列表（`TransformationByproducts`）。

---

## 五、副产线与伴生修正系统（概率覆盖与拦截防出）

为了实现“额外输入带来的额外产出”或“特定装备/媒介卡牌改变产出逻辑”的高阶设计（如：佩戴安神手链制作任何带工匠标签的配方额外获得回理智卡；佩戴厚皮手套工作彻底防止手部烫伤起水疱），系统引入了独立的**副产线规则模型（RecipeAuxiliaryLine）**。

### 1. 核心设计原则
* **跨配方复合匹配（多选 + 组合模式）**：
  * **匹配标签（MatchTagIds）**：如匹配 `#工匠`、`#燃料`、`#生火`；
  * **匹配原料物品（MatchItemIds）**：如匹配特定输入物品【木棍】、【生肉】；
  * **匹配工位场所（MatchLocationIds）**：如限定在【土窑】、【营火】；
  * **组合逻辑模式（MatchCombineMode）**：支持 `满足任意一项匹配条件即可 (OR = 0)` 或 `必须同时满足所有配置条件分类 (AND = 1)`。只要宿主配方的原料、在场个体或场所满足规则，副产线即告激活！
* **额外投入媒介卡牌（ExtraInputs）**：
  * 激活副产线额外要求的媒介卡牌（如【安神手链】、【厚皮手套】、【粗盐】）；
  * 支持配置为 **非耗材器具（Consume = false）** 或 **消耗品原料（Consume = true）**；
  * 非耗材器具支持配置 **单次耐久损耗值（DurabilityCost）**（如手套单次磨损 1 耐久）。
* **产物修饰与概率覆盖 / 拦截机制（OutputModifiers）**：
  * 追加全新产物与拦截防止旧产物在底层采用统一的概率修饰模型（`RecipeOutputModifier`）：
    * **`Chance == 0%`（拦截防止 / 免疫损伤）**：若宿主配方原有此产物（如 30% 几率起水疱），副产线强制将其概率覆盖为 `0%`，彻底拦截该产物的出现；
    * **`Chance == 100%`（必定产出 / 保底保真）**：若宿主配方原为概率产出，强制覆盖为 `100%` 必出；
    * **`0% < Chance < 100%`（概率重写）**：重写原配方中该产物的发生几率；
    * **全新物品追加（Chance > 0%）**：若原配方中不存在该物品，则直接作为附加产物（如【宁神回响】回理智卡）生成出来；
  * **彻底阻断总开关（`SuppressBaseOutputs = true`）**：彻底掐断宿主配方原有全部产出，仅保留本副产线的产物（用于完全替代或配方报废）。

---

## 六、能力检定与卡牌点数贡献系统（冶炼、烹饪与分支选择）

为支持卡牌生存游戏中的**能力检定门槛**与**属性换算**，系统深度打通卡牌点数挂载与配方分支检定：

### 1. 卡牌携带默认点数（ItemAbilityContribution）
* **全卡牌通用**：所有卡牌（材料、工具、在场角色、设施等）均可携带默认能力点数（不填即为 0 点）；
* **基础数值（BaseValue）**：卡牌提供的标准能力点数（如木炭携带 `heat: 15`，村民携带 `strength: 12, cooking: 6, smelting: 10`）；
* **折算率（Efficiency，默认 1.0 即 100%）**：卡牌投入时按多少比例换算为配方所需能力（如木材湿气燃烧，`heat` 按 80% 折算；优质铁镐杠杆支撑，`strength` 按 125% 放大折算）；
* **跨能力换算目标（TargetAbilityId）**：留空表示直接贡献给同名能力，亦可指定跨能力属性换算；
* **工序效率乘数（EfficiencyMultiplier）**：配方原料槽与在场人员槽可单独设置效率乘数，进一步缩减或放大卡牌能力贡献。

### 2. 配方能力检定需求（RecipeAbilityCheck）
* **多重检定**：一个配方可配置多个能力检定项（如高炉炼铁要求 `heat >= 20` 且 `smelting >= 8`）；
* **操作符与门槛（Operator & RequiredValue）**：支持 `>=`、`==`、`<=`、`>`、`<`；
* **未达标失败分支反馈（FailureBranchNote）**：详细说明未达标的原因（如“炉温不足矿石凝结”、“厨艺欠佳火候失控焦糊”、“矿道塌方体力耗尽负伤”）。

### 3. 产物触发条件（OutputTriggerCondition）
产物槽通过 `TriggerCondition` 完美表达成功与失败分支，无需硬编码代码：
* **`Always = 0`（常规产出）**：无论检定达标与否，只要配方运行即可按自身几率产出；
* **`OnCheckSuccess = 1`（★检定达标专属产出）**：仅在所有能力检定全部通过时产出（如【喷香熟肉】、【纯铁锭】、【神秘矿宝盒】）；
* **`OnCheckFailure = 2`（⚠️未达标失败分支产出）**：检定未通过时触发的惩罚性产出（如【焦黑碳肉】、【炉渣废料】、【手腕挫伤卡】）。

---

## 七、拆包游戏参考展台（791 项全量官方数据）

编辑器内嵌三款经典卡牌生存游戏的全部真实官方配方数据，作为独立展示页签与主工作区完全隔离：
1. **《卡牌生存：热带岛屿》**（214 项官方蓝图，420 种卡牌物品）；
2. **《卡牌生存：奇幻森林》**（400 项官方蓝图，722 种卡牌物品）；
3. **《别冻僵》**（177 项制造、烹饪与风干配方，210 种卡牌物品）。

### 级联借用与批量引入功能
* **单项级联借用（★ 借用/复制到我的配方库）**：点击选中的参考配方，编辑器会自动分析其依赖的输入材料、工位场所、复用标签与产出物，深度克隆并自动级联补齐至本地正式库；
* **批量增量合并**：支持一键将某款游戏的完整配方包批量注入正式库，自动处理重名与 ID 冲突。

---

## 八、标准配方、副产线与能力检定 JSON 规范（供其他 AI 对齐）

如需让其他 AI 批量生成配方、副产线与能力检定数据，可直接提供以下 JSON Schema 示例：

```json
{
  "SchemaVersion": 1,
  "DatabaseName": "自定义游戏配方库",
  "Tags": [
    { "Id": "tag_fuel", "Name": "燃料", "ColorHex": "#E67E22", "Category": "属性", "Description": "燃烧提供热量的材料" },
    { "Id": "tag_worker", "Name": "工匠", "ColorHex": "#2980B9", "Category": "角色", "Description": "具备手工能力的在场人员" }
  ],
  "Abilities": [
    { "Id": "heat", "Name": "火力热量", "Category": "物理/环境", "Description": "提供高温热量", "IconOrEmoji": "🔥", "ColorHex": "#E67E22" },
    { "Id": "cooking", "Name": "烹饪技艺", "Category": "技艺/专精", "Description": "火候与调味掌控", "IconOrEmoji": "🍳", "ColorHex": "#27AE60" },
    { "Id": "smelting", "Name": "冶炼工艺", "Category": "工匠/制造", "Description": "高温金属加工能力", "IconOrEmoji": "⚒️", "ColorHex": "#9B59B6" },
    { "Id": "strength", "Name": "体力力量", "Category": "角色/生理", "Description": "体力负重与破障", "IconOrEmoji": "💪", "ColorHex": "#C0392B" }
  ],
  "Items": [
    {
      "Id": "charcoal",
      "Name": "木炭",
      "Category": "材料",
      "MaxStack": 50,
      "TagIds": ["tag_fuel"],
      "Properties": [],
      "AbilityContributions": [
        { "AbilityId": "heat", "BaseValue": 15, "Efficiency": 1.0, "TargetAbilityId": "", "Note": "纯净高热" }
      ]
    },
    {
      "Id": "villager",
      "Name": "村民工匠",
      "Category": "人员个体",
      "MaxStack": 1,
      "TagIds": ["tag_worker"],
      "Properties": [],
      "AbilityContributions": [
        { "AbilityId": "strength", "BaseValue": 12, "Efficiency": 1.0, "TargetAbilityId": "", "Note": "健硕身躯" },
        { "AbilityId": "smelting", "BaseValue": 10, "Efficiency": 1.0, "TargetAbilityId": "", "Note": "熟练工匠" }
      ]
    }
  ],
  "Locations": [
    {
      "Id": "loc_furnace",
      "Name": "土窑工位",
      "SourceType": 0,
      "BoundItemId": "",
      "Capacity": 1,
      "DurabilityCost": 0,
      "FeatureTags": ["高温"]
    }
  ],
  "Recipes": [
    {
      "Id": "recipe_smelt_iron",
      "Name": "高炉炼铁",
      "Type": 0,
      "Category": "冶炼",
      "CraftTime": 10.0,
      "LocationId": "loc_furnace",
      "Operators": [
        { "ItemId": "villager", "Count": 1, "EfficiencyMultiplier": 1.0 }
      ],
      "Inputs": [
        { "Type": 0, "ItemId": "iron_ore", "Count": 2, "EfficiencyMultiplier": 1.0 },
        { "Type": 1, "TagId": "tag_fuel", "Count": 2, "EfficiencyMultiplier": 1.0 }
      ],
      "AbilityChecks": [
        { "AbilityId": "heat", "Operator": ">=", "RequiredValue": 20, "FailureBranchNote": "温度不足铁矿无法融化" },
        { "AbilityId": "smelting", "Operator": ">=", "RequiredValue": 8, "FailureBranchNote": "工艺不精凝固废炉" }
      ],
      "Outputs": [
        { "ItemId": "iron_ingot", "Count": 1, "Chance": 1.0, "OutputType": 0, "TriggerCondition": 1 },
        { "ItemId": "slag", "Count": 1, "Chance": 0.5, "OutputType": 1, "TriggerCondition": 0 },
        { "ItemId": "slag", "Count": 2, "Chance": 1.0, "OutputType": 0, "TriggerCondition": 2 }
      ]
    }
  ],
  "AuxiliaryLines": [
    {
      "Id": "aux_bracelet_soothe",
      "Name": "手链抚慰效应 (跨配方追加回san卡)",
      "Description": "只要操作人员佩戴【安神手链】，进行任何带工匠标签或燃料投入的工艺时，将额外产出一张【宁神心绪】回复理智卡",
      "MatchCombineMode": 0,
      "MatchTagIds": ["tag_worker", "tag_fuel"],
      "MatchItemIds": [],
      "MatchLocationIds": [],
      "ExtraInputs": [
        { "MatchType": 0, "TargetId": "bracelet", "Quantity": 1, "Consume": false, "DurabilityCost": 0 }
      ],
      "SuppressBaseOutputs": false,
      "OutputModifiers": [
        { "ItemId": "calm_mind", "Quantity": 1, "Chance": 1.0, "OutputType": 1, "Note": "必定额外生成宁神卡" }
      ]
    },
    {
      "Id": "aux_gloves_protection",
      "Name": "厚皮手套防护 (0% 拦截防止损伤)",
      "Description": "在土窑或燃烧燃料的配方中，若额外投入【厚皮手套】(单次损耗 1 耐久)，将彻底拦截原可能产生的【手部水疱】(几率覆盖为 0%)",
      "MatchCombineMode": 0,
      "MatchTagIds": ["tag_fuel"],
      "MatchItemIds": [],
      "MatchLocationIds": ["loc_furnace"],
      "ExtraInputs": [
        { "MatchType": 0, "TargetId": "leather_gloves", "Quantity": 1, "Consume": false, "DurabilityCost": 1 }
      ],
      "SuppressBaseOutputs": false,
      "OutputModifiers": [
        { "ItemId": "hand_blister", "Quantity": 1, "Chance": 0.0, "OutputType": 1, "Note": "0%概率彻底拦截水疱" }
      ]
    }
  ]
}
```

---

## 九、数据存取与完整性校验

1. **实时等式预览**：
   * 配方详情区实时渲染直观公式，例如：
     ```text
     [能力检定: 🔥火力热量 >= 20 且 ⚒️冶炼工艺 >= 8] [工位: 土窑工位] [在场人员: 村民工匠 x1 (不消耗·无损耗)] [铁矿石 x2] + [#标签:燃料 x2] ──➔ [★铁锭 x1(检定达标)] + [⚑炉渣 x1(50%)] + [⚠️炉渣 x2(未达标失败分支)]
     ```
   * 副产线详情区实时渲染逻辑等式，例如：
     ```text
     [匹配: #燃料 或 #工匠] + [额外投入: 手链 x1(非耗材)] ──➔ [★追加新产出: 宁神心绪 x1(100%)]
     ```
2. **概览与校验（Data Integrity Checker）**：
   * 自动检测全库重复 ID、悬空标签/能力引用、不存在的物品/场所引用、副产线匹配项与修饰项完整性；
   * 检测非法数量、零产物配方及孤儿标签与孤儿能力；
   * 每条问题提供快捷“定位”按钮，一键直达对应项进行修复。
