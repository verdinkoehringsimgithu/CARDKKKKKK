# 核心接口约定

命名空间 `CardKKK.Tabletop.Core`。纯 C#，不引用 Unity。所有操作同步，由主线程调用；引擎独占已提交状态，外部通过命令修改，通过快照读取。

## 定义、实例与规则

- `CardDefinitionData(id, label, family, rank, attributes, legacyDefinitionId = null, legacyRank = 0)`：不可变定义。属性输入和公开读取都深拷贝，不共享可变实例属性。
- `CardCatalog(IEnumerable<CardDefinitionData>)`：唯一非空 ID 的只读定义目录。`TryGet` 查找当前 ID；`CreateCard(id)` 创建含默认属性的独立实例 DTO；`TryResolveSavedCard` 先尝试旧 ID + 旧序号迁移，再尝试当前 ID。
- `CardData`：序列化 DTO，字段为实例 `Id`、`DefinitionId`、已解析 `Label/Family/Rank`、`FaceUp`、`List<CardAttribute> Attributes`。定义目录存在时，静态字段以目录为准；实例翻面和属性以当前实例为准。
- `CardAttribute`：`Key` 与整数 `Value`。目前只存储数据，不包含自动效果解释器。
- `PileData`：`Id`、`TablePoint Position`、`List<string> CardIds`。位置为底牌左下角，成员从底到顶。
- `BoardSnapshot`：`SchemaVersion = 1`、`Revision`、`Cards`、`Piles`。堆列表从后到前。`Capture()` 返回防御性深拷贝。
- `TablePoint`：`X/Y` 浮点数；桌面左下为原点，后续牌向上错开。
- `BoardRules`：桌面宽高、卡牌宽高、堆内错开、吸附距离、间距、全桌上限、单堆上限，以及下列策略与开关。构造引擎时复制并验证，`Engine.Rules` 返回副本。

| 字段 | 含义 |
|---|---|
| `AllowStacking` | 允许多张同堆；关闭时所有堆只能单张 |
| `StackRule` | `SameFamily / SameDefinition / AnyCard` |
| `SortRule` | `RankAscending / RankDescending / NameAscending` |
| `AllowWholePileDrag / AllowSingleCardDrag` | 分别允许整堆与单张拖动 |
| `AutoMergeOnSpawn` | 生成位置重叠时合并兼容堆，否则找空位 |
| `AutoMergeOnTidy` | 整理时是否合并附近兼容堆 |
| `Features` | 翻面、排序、洗牌、堆内调序、抽牌、整理、存档的位标志 |

默认尺寸 1600×900，卡片 100×140，错开 22，吸附距离 65，间距 12，全局上限 1000，单堆上限 20。宽高和错开为有限正数，吸附距离和间距可为零。堆高度为 `CardHeight + (count - 1) * PileOffset`。容量上限不能放宽几何边界。

## 引擎入口

```csharp
var catalog = new CardCatalog(definitions);
var engine = new BoardEngine(rules, catalog);
var template = catalog.CreateCard("herb.mint");
OperationResult result = engine.Spawn(template, new TablePoint(200, 200));
```

构造器为 `BoardEngine(BoardRules rules, CardCatalog catalog = null)`。目录为 null 的路径保留给原导入 API 与独立核心检查；正常 Unity 入口始终注入目录。

始终保留的底座：

```csharp
BoardSnapshot Capture();
OperationResult Spawn(CardData template, TablePoint position);
OperationResult BringToFront(string pileId);
OperationResult BeginDrag(string pileId, string cardId = null);
DropPreview PreviewDrop(TablePoint position);
OperationResult CommitDrop(TablePoint position);
OperationResult CancelDrag();
OperationResult ResolveOverlaps();
OperationResult Restore(BoardSnapshot snapshot);
```

可选模块（对应 `CARDTABLE_DISABLE_*` 被定义时，完整方法不参与编译）：

```csharp
OperationResult FlipCard(string cardId);             // FLIP
OperationResult FlipPile(string pileId);             // FLIP
OperationResult SortPile(string pileId);             // SORT
OperationResult ShufflePile(string pileId, int seed);// SHUFFLE
OperationResult ReorderCard(string pileId, string cardId, int index); // REORDER
OperationResult PopCard(string pileId, string cardId = null);        // POP
OperationResult TidyTable();                         // TIDY
```

调用可选方法的外部代码也必须使用对应条件编译。即使实现已编译，运行配置 `Features` 关闭时仍拒绝该操作，不做静默成功的空调用。

`OperationResult` 返回 `Success/Error/PileId/CardId`。拒绝操作不改变已提交状态。`DropPreview` 包含有效性、实际位置、目标堆、插入序号、原因和移动成员；无目标堆代表自由放置。`CurrentDrag` 返回本次源堆与移动成员快照。

## 状态、拖放与通知

- 核心只有一个修改入口体系：候选副本 → 统一规则与几何验证 → 原子提交 → `Changed` 通知。通知中的重入修改被拒绝，某个观察者异常不阻止其他观察者。
- `Spawn` 不信任传入实例 ID，始终生成新的唯一 ID。带目录时按定义 ID 解析名称、分类和序号；默认属性须通过 `catalog.CreateCard` 获得。生成点被阻挡时进行有界空位搜索。
- `BeginDrag(pileId)` 移动整堆；传入 cardId 只拆这一张。开始时缓存移除移动成员后的候选基础与空间索引，拖动过程中不改变提交状态。
- 拖动独占模型，其他修改拒绝。预览与提交共用落点规划；非法提交结束拖动并保留原状态，取消也保留原状态。单张可回到原堆重新插入。
- 目标选择使用空间哈希与精确距离/矩形，优先最近兼容堆，距离相同时取前层。落入不兼容或满容量堆内部会明确拒绝。合堆后的完整占用区域仍须在桌面内且不撞其他堆。
- 引擎不负责动画锁。Unity 命令层在过渡动画未完成时拒绝新的修改，失焦/Esc取消拖动；动画只显示核心已提交状态。

## 布局与复杂度边界

排序采用 O(n log n) 比较排序，使用原位置作为同值次序；名称升序按 Ordinal，保证运行环境无关的确定次序。洗牌为指定种子的 Fisher–Yates O(n)，不改变成员。

空位搜索最多检查 20000 个候选，先按距离、再按坐标确定次序；“最近”指候选集合中最近，不是连续空间的数学全局解。`ResolveOverlaps` 在候选上处理重叠，兼容且容量允许时合并，否则挪到验证过的空位；步骤有上限。

整理使用确定次序的容量约束并查集做附近合堆，再按高度优先放入最短列。无法布局会整体拒绝；不是精确全局装箱求解器。空间哈希只负责粗查，最后始终做精确矩形检查。

## 读档与文件适配器

`Restore` 在替换状态前验证版本、有限位置、边界、唯一 ID、成员唯一归属、当前堆叠规则和重叠。带目录时解析所有定义，并保留实例当前属性、翻面、位置、成员序与 ID。未知定义或新规则不兼容时整体拒绝。有效读取在拖动期间也拒绝，不取消现有拖动来强行加载。

命名空间 `CardKKK.Tabletop.Unity` 中，`BoardSaveStore(path, catalog = null)` 提供 `Save(engine)` 和 `Load(engine)`，完整类由 `CARDTABLE_DISABLE_SAVE` 裁剪。运行 Save 功能位关闭时直接调用也拒绝。格式版本1使用 JsonUtility 的快照载荷、SHA256 完整性、10 MB 上限、同目录临时写入、原子替换和 `.bak` 恢复。只有通过当前核心规则检查的旧主文件才轮换为有效备份。

默认配置仅提供旧示例牌 `archive/supply/tool.sample` 序号1～3映射。旧序号4～9需要设计者补充定义，不能无依据地自动映射到其他牌。

## Unity 分工与验证状态

`CardTableDemo` 校验并装配配置、Controller、View、Input；`CardTableController.Execute(TableAction, definitionId)` 是按钮与快捷键的共同入口。`CardTableView` 和 `TableCardView` 显示快照，`TableInputRouter` 映射按键，`TableActionButton` 绑定按钮。`CardTableView.prefab` 和 `CardView.prefab` 承载可修改的序列化布局。

本次只做源码与序列化引用静态检查，未运行编译、核心检查工程或 Unity。`Tests/Core` 默认编译实际 Core 源码且启用全部可选模块；新增四组规则/目录场景尚未执行。重构前的18组通过记录是历史结果。本版本运行状态由用户在自己的 Unity 编辑器中检查。
