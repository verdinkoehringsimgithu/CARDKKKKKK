# 开发记忆

本文件是继续开发的入口。记录关键事实和锚点，每个主要阶段及上下文接近 80% 前更新。

## 当前锚点

- [SCOPE] 用户给出的两张需求图是首版依据。Unity 2022.3.62f3c1，Windows 鼠标，有限二维桌面，同类卡兼容堆叠。实现方向与边界在 `docs/superpowers/specs/2026-11-03-tabletop-foundation-design.md`。
- [REPO] 本地 `D:\wenjian\cardkkk`，origin 为 `https://github.com/verdinkoehringsimgithu/CARDKKKKKK.git`。本轮开始 main 与远程一致，起点 `e8bde6b`。已有上传授权，协作者权限此前已验证成功。
- [CORE] `Assets/CardTable/Core/BoardEngine.cs` 已实现，域接口在 `docs/tabletop-api.md`。同步主线程，独占拖拽会话，候选副本校验后原子提交。BeginDrag 缓存去除移动卡后的模型与空间索引，PreviewDrop 只查询邻近候选，CommitDrop 重新规划并完整校验。桌面左下坐标，Pile.Position 是底牌左下角，CardIds 从底到顶，后牌向上错开。排序稳定 Rank 升序，洗牌用种子 Fisher-Yates。
- [GEOMETRY] `Core/TableGeometry.cs` 为均匀空间哈希与精确包围盒。FindFree 最多 20000 个候选，最近指候选集中最近。TidyTable 为容量约束并查集合堆和高度降序最短列布局，可能拒绝其他全局装箱算法能放下的布局，失败保留原状态。Restore 拒绝重叠快照，自动重叠主要由 Spawn 内部候选处理；未来脚本移动可新增受控命令。
- [UI] 演示入口 `Assets/CardTable/Scenes/CardTableDemo.unity`，菜单 `CardKKK/Open Card Table Demo`。`Unity/CardTableDemo.cs`、`TableCardView.cs` 处理鼠标、占位、绿色/红色预览、插槽线、池化牌面、布局/翻面动画。动画与拖动互斥，失焦/Esc取消。P 抽顶牌，Shift 拖拆任意一张，S排序/H洗牌，左右键堆内调序。SampleScene 与 ProjectSettings 保留原状。
- [ART] `Assets/CardTable/Art` 预留 CardFaces/CardFrames/Table/Icons/Audio/SFX/Audio/Music。`CardTableTheme.asset` 已被演示场景引用，字段均可为空，说明在 Art/README.md。没有接入已解包的第三方游戏资源。
- [SAVE] `Unity/BoardSaveStore.cs` 实现格式版本1、JsonUtility Payload、SHA256完整性、同目录临时写入与原子替换、.bak恢复。损坏主文件不会轮换覆盖有效备份。保存运行卡属性、翻面、堆成员顺序、位置、层级。原生文件往返与备份恢复尚待 Unity 验收。
- [VERIFY] `Tests/Core` 的实际源码 Release 测试 18/18 通过，含3000次固定种子随机操作。100/500/1000张稀疏扩展桌面变化落点预览平均约1.4/2.9/4.0微秒，分配56B/次，同点缓存。不是渲染FPS。实际源码哈希及最终记录可通过git和tests复核。
- [USER] 用户明确说“检查编译不需要你来，我自己来看就行了”。已停止编译和 Unity运行验证，不再自动启动验证工程。此前隔离验证在测试工程的 uGUI测试依赖处失败，已移除该隔离测试脚本，不计入产品验证结果。用户的实际工程源码在正常导入，但未报告为运行验收通过。
- [NEXT] 用户在 Unity打开演示并自行检查编译/运行，先验收鼠标拖放、插牌、取消、调序、动画互斥、F5/F9和备份恢复。如有报错优先按实际日志修复。然后再确定内容层玩法，沿用现有核心。每次开发继续简记README并更新本文件。

## 协作与接续

每次接续先看 git status，再读本文件和 README。遇到用户已有改动先保留，验证代码是否与记录一致。AGENTS.md 已要求每次开发简记并定期保存上下文锚点。
