# CARDKKKKKK

Unity 2022.3.62f3c1 单机卡牌桌面项目。

## 首版方向

先建立可以复用的卡牌桌面交互框架，验证生成、拖拽、堆叠、预览、整理与存档。当前以鼠标、单玩家、固定卡片尺寸和有限桌面为边界。

详细需求、难度、算法取舍和验收边界见 [开发设计](docs/superpowers/specs/2026-11-03-tabletop-foundation-design.md)。跨对话接续从 [开发记忆](docs/MEMORY.md) 开始。

## 运行入口

1. 用 Unity 2022.3.62f3c1 打开项目。
2. 选择菜单 `CardKKK > Open Card Table Demo`，打开 `Assets/CardTable/Scenes/CardTableDemo.unity`。
3. 点击 Play。初始显示三个示例牌堆，可通过顶部按钮继续发牌。

| 操作 | 输入 |
|---|---|
| 选中并置顶 | 左键点击卡牌 |
| 移动整堆 | 普通拖拽 |
| 拆出单张、插入其他牌堆 | 按住 Shift 拖拽卡牌 |
| 预览落点 | 拖拽时绿色可放、红色不可放，横线指示插入位置 |
| 取消、非法落点归位 | Esc，或松开到非法位置 |
| 翻面 | Space |
| 牌堆稳定排序 / 洗牌 | S / H |
| 抽顶牌并自动找空位 | P |
| 选中卡片在堆内调整顺序 | 左右方向键 |
| 桌面整理、附近兼容牌合堆 | R |
| 处理非拖拽重叠 | O |
| 保存 / 读取 | F5 / F9 |

操作也有界面按钮。相同 Family 兼容，Rank 决定排序。布局动画与拖拽互斥，正在拖动时其他修改被拒绝，失焦会取消拖动。

## 代码入口与边界

| 路径 | 作用 |
|---|---|
| `Assets/CardTable/Core/BoardEngine.cs` | 状态唯一写入口、操作事务、拖拽规划、排序、洗牌、堆叠和布局 |
| `Assets/CardTable/Core/TableGeometry.cs` | 空间哈希与精确矩形查询 |
| `Assets/CardTable/Core/TableData.cs` | 规则、卡牌、牌堆、快照与返回结果 |
| `Assets/CardTable/Unity/CardTableDemo.cs` | 输入、选择、预览与演示 UI |
| `Assets/CardTable/Unity/TableCardView.cs` | 牌面占位、位移与翻面动画 |
| `Assets/CardTable/Unity/BoardSaveStore.cs` | 版本化存档、完整性校验、临时写入、原子替换与备份恢复 |

默认桌面 1600×900、卡片 100×140、单堆最多 20 张、全局最多 1000 张。几何空间限制与数量上限同时生效。Windows、单鼠标、固定尺寸、无旋转是本轮边界。玩法层的战斗、配方、剧情与联网留待后续需求确定。

拖拽开始时建立空间索引，预览复用索引并查询邻近对象。排序为稳定 O(n log n)，洗牌为 Fisher-Yates O(n)。最近空位从有限候选中选择，整理使用容量约束并查集合堆与高度优先列布局。它们可能在候选或启发式范围内找不到布局，此时完整保留原状态。

存档位于 `Application.persistentDataPath/card-table.json`，上次有效版本使用 `.bak`。版本 1 保存卡牌实例、属性、翻面、牌堆成员顺序、桌面位置与前后层级。

## 美术接入位置

`Assets/CardTable/Art` 预留 CardFaces（卡面）、CardFrames（卡框）、Table（桌面）、Icons（图标）、Audio/SFX（音效）与 Audio/Music（音乐）。首版使用文字、色块和过渡动画占位，资源由主题配置接入。

通过 `CardKKK > Select Art Theme` 选择主题，替换 `CardTableTheme.asset` 的 Sprite、AudioClip 和颜色字段。卡面图片导入为 Sprite 后按 DefinitionId 绑定，文字仍由 Unity 排版。详细目录和字段对应见 [美术占位说明](Assets/CardTable/Art/README.md)。

## 验证记录

纯 C# 核心测试通过 18/18，包含 3000 次固定种子随机操作，检查唯一归属、数量守恒、几何边界、稳定排序、洗牌、预览与提交一致、失败回滚及异常快照拒绝。

```powershell
dotnet run --project Tests/Core/CardTable.Core.Tests.csproj -c Release
```

在本机 .NET 8 Release 下，稀疏扩展桌面 100/500/1000 张牌、各 2000 次变化落点预览，平均约 1.4/2.9/4.0 微秒，每次预览分配 56 字节。相同落点复用缓存。测量仅覆盖领域计算，不代表 Unity 渲染帧率。

按用户要求，后续 Unity 编译与实际运行检查由用户执行。需要在编辑器确认拖拽、动画、不同分辨率、F5/F9 存档往返与损坏主文件后的备份恢复。本轮未完成这些运行验收。

## 开发记录

- 2026-11-03，完成桌面框架首版，加入事务式牌堆操作、缓存空间索引预览、稳定排序/Fisher-Yates 洗牌、布局、存档和占位演示。预留美术入口，建立 AGENTS 与 MEMORY 锚点。核心 18/18 测试和 3000 次随机操作通过，Unity 编译及运行验收由用户接手。
