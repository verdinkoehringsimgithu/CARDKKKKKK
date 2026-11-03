# 开发记忆

本文件是继续开发的入口。记录关键事实和锚点，每个主要阶段及上下文接近 80% 前更新。

## 当前锚点

- [SCOPE] 用户给出的两张需求图是首版依据。Unity 2022.3.62f3c1，Windows 鼠标，有限二维桌面，同类卡兼容堆叠。实现方向与边界在 `docs/superpowers/specs/2026-11-03-tabletop-foundation-design.md`。
- [REPO] 本地 `D:\wenjian\cardkkk`，origin 为 `https://github.com/verdinkoehringsimgithu/CARDKKKKKK.git`。框架首版已上传 `2243259`，本轮从此提交修复入口并中文化。已有上传授权，协作者权限此前已验证成功。
- [CORE] `Assets/CardTable/Core/BoardEngine.cs` 已实现，域接口在 `docs/tabletop-api.md`。同步主线程，独占拖拽会话，候选副本校验后原子提交。BeginDrag 缓存去除移动卡后的模型与空间索引，PreviewDrop 只查询邻近候选，CommitDrop 重新规划并完整校验。桌面左下坐标，Pile.Position 是底牌左下角，CardIds 从底到顶，后牌向上错开。排序稳定 Rank 升序，洗牌用种子 Fisher-Yates。
- [GEOMETRY] `Core/TableGeometry.cs` 为均匀空间哈希与精确包围盒。FindFree 最多 20000 个候选，最近指候选集中最近。TidyTable 为容量约束并查集合堆和高度降序最短列布局，可能拒绝其他全局装箱算法能放下的布局，失败保留原状态。Restore 拒绝重叠快照，自动重叠主要由 Spawn 内部候选处理；未来脚本移动可新增受控命令。
- [UI] 演示入口 `Assets/CardTable/Scenes/CardTableDemo.unity`，中文菜单 `卡牌桌面/打开卡牌桌面`。`Unity/CardTableDemo.cs`、`TableCardView.cs` 处理鼠标、占位、绿色/红色预览、插槽线、池化牌面、布局/翻面动画。动画与拖动互斥，失焦/Esc取消。P抽顶牌，Shift拖拆任意一张，S排序/H洗牌，左右键调序。SampleScene 保留，EditorBuildSettings 已将演示加入为首场景。
- [ENTRY] 用户反馈“啥都没有”后发现上一版只有启动脚本、界面在Awake中生成、默认入口未接。现 CardTableDemo 使用 ExecuteAlways/OnEnable 展示编辑态预览，生成界面集中在瞬态子对象中并标记不保存；禁用/销毁清理，运行态普通禁用/启用保留模型。记录构建模式，编辑态与运行态切换时重建，兼容关闭域/场景重载。CardTableMenu初始化默认Play入口，中文菜单可切换到运行当前场景。
- [LANG] 用户要求游戏内文字与上传/提交说明都使用中文。Core错误、存档错误、按钮、卡名、分类、状态与菜单已中文化，旧英文示例牌名显示时转换。主题新增UiFont，空值时使用Windows微软雅黑/黑体/宋体动态字体。英文技术ID保持稳定。
- [ART] `Assets/CardTable/Art` 预留 CardFaces/CardFrames/Table/Icons/Audio/SFX/Audio/Music。`CardTableTheme.asset` 已被演示场景引用，字段均可为空，说明在 Art/README.md。没有接入已解包的第三方游戏资源。
- [SAVE] `Unity/BoardSaveStore.cs` 实现格式版本1、JsonUtility Payload、SHA256完整性、同目录临时写入与原子替换、.bak恢复。损坏主文件不会轮换覆盖有效备份。保存运行卡属性、翻面、堆成员顺序、位置、层级。原生文件往返与备份恢复尚待 Unity 验收。
- [VERIFY] `Tests/Core` 的实际源码 Release 测试 18/18 通过，含3000次固定种子随机操作。100/500/1000张稀疏扩展桌面变化落点预览平均约1.4/2.9/4.0微秒，分配56B/次，同点缓存。不是渲染FPS。实际源码哈希及最终记录可通过git和tests复核。
- [USER] 用户明确说“检查编译不需要你来，我自己来看就行了”。已停止编译和 Unity运行验证，不再自动启动验证工程。此前隔离验证在测试工程的 uGUI测试依赖处失败，已移除该隔离测试脚本，不计入产品验证结果。用户的实际工程源码在正常导入，但未报告为运行验收通过。
- [NEXT] 用户点击“卡牌桌面/打开卡牌桌面”，应先看到预览；运行后看三类共九张牌，右侧中文按钮。按README的操作/预期结果表检查拖牌、Shift拆牌、跨类非法落点回原位、发牌合堆、排序/洗牌/抽顶牌、F5挪牌F9恢复。编译/实际运行仍由用户操作，本轮仅静态检查。后续开发继续简记README和本文件，Git提交说明统一中文。

## 协作与接续

每次接续先看 git status，再读本文件和 README。遇到用户已有改动先保留，验证代码是否与记录一致。AGENTS.md 已要求每次开发简记并定期保存上下文锚点。
