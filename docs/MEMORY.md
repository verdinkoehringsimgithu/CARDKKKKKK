# 开发记忆

跨对话接续入口。实际代码与 Git 状态优先于旧说明，每次主要阶段与上下文接近80%前更新稳定锚点。

## 当前锚点

- [USER] 用户要求方便开发、可复用、易修改、规则可配置；不需要的功能应移除实现或完整条件编译，不能仅不调用。游戏、配置工具、README、Git提交与上传说明用中文。用户明确自行检查编译与Unity运行，本轮不启动Unity、编译器或测试。已有上传授权，协作者身份此前已验证，提交后正常push，不force。
- [REPO] 本地 `D:\wenjian\cardkkk`；origin `https://github.com/verdinkoehringsimgithu/CARDKKKKKK.git`。桌面重构 `60b2784` 已上传，本轮策划基于此提交，开工时工作区干净。用户此前加入的 `.agents` 技能及 `Assets/CardTable/Art/Assets` 资源保留。Git原有身份不改。
- [SCOPE] Unity 2022.3.62f3c1，Windows单机、鼠标、有限二维桌面、统一卡片尺寸、不旋转。实现自由卡牌桌面，具体配方、委托、回合与胜负尚未选定，不添加假配置。旧需求在 `docs/superpowers/specs/2026-11-03-tabletop-foundation-design.md`。本轮规则与结构见 README、`docs/configuration-guide.md`、`docs/tabletop-api.md`。
- [CONFIG] `Unity/Configuration` 提供 CardDefinition、CardTableConfiguration、TableRuleSettings、FeatureSelection、InitialPile。中文菜单 `卡牌桌面/游戏规则配置`，四页为桌面规则/卡牌目录/初始桌面/功能与资源。窗口默认读取场景启动器的实际配置引用，修改停止后下一次启动应用，不热改模型。默认配置 `Assets/CardTable/Configuration/默认游戏配置.asset`，GUID `a74fc672df4144e8a77723d488c1475b`。9种定义为archive/supply/tool各.3/.1/.2，初始3堆各3张，位置180/540/900,360。目录可新增定义并编辑中文名称/分类/序号/属性/插画。
- [VALIDATE] 配置验证检查ID/属性/引用/Windows存档文件名，并精确构造初始快照交Core.Restore检查几何、唯一归属、容量、当前堆叠规则；允许初始桌面为空。默认资产补齐仅创建缺文件，不覆盖设计者修改。规则窗口与Inspector使用中文枚举/属性/引用列表。
- [DEFINITION] `Core/CardCatalog.cs` 定义不可变CardDefinitionData与只读目录。Unity资产是静态定义唯一来源；Core保存深拷贝。`BoardEngine(rules,catalog=null)` 在生成/读取时解析静态名称/分类/序号，实例ID、FaceUp、当前Attributes保留。正常UI发牌用catalog.CreateCard获得定义默认属性。无catalog路径保留原导入/测试兼容。
- [RULES] 统一CanStack用于预览、提交、生成重叠、整理、Restore：SameFamily/SameDefinition/AnyCard；AllowStacking关闭时每堆单张。整堆/单张拖动权限独立。AutoMergeOnSpawn只处理生成区域重叠；AutoMergeOnTidy处理SnapDistance附近合堆。排序支持稳定升/降序或Ordinal名称升序。默认全功能启用。
- [MODULES] BoardEngine为partial，六组操作及独占helper已从单体真正迁出 `Core/Operations/BoardEngine.{Flip,Sort,Shuffle,Reorder,Pop,Tidy}.cs`。每个完整文件由CARDTABLE_DISABLE_*排除，同时检查运行Features。Save整类和Unity调用亦受CARDTABLE_DISABLE_SAVE。规则窗口按钮“应用功能裁剪到编译”仅显式点击时变更当前平台的七个自有符号，保留其他符号；工程级裁剪可能影响同平台其他配置，不自动导入时改设置。
- [CORE] 状态仍是候选副本统一校验后原子提交、同步主线程、独占拖拽。预览复用缓存索引，提交共用规划。TableGeometry空间哈希+精确矩形；FindFree最多20000候选；Tidy容量约束并查集+高度优先最短列布局，不保证全局最优，失败回滚。所有API文档已中文更新。
- [UI] 新结构：CardTableDemo薄启动器、CardTableController命令/选择、TableInputRouter快捷键、CardTableView呈现/牌池、TableActionButton按钮绑定、TableCardView牌面/动画。所有按钮与快捷键走Execute；成功模型Changed唯一刷新，取消/非法落点才额外归位。删除运行时整套UI生成器、MakeTemplate硬编码、重复刷新、玩家O键/手动重叠按钮、状态版本调试。自动避让核心仍保留。
- [PREFAB] `Prefabs/CardTableView.prefab` 为可修改的真实Canvas布局，`CardView.prefab` 为牌面；演示场景引用真实Prefab、默认配置与原Bootstrap脚本GUID。运行视图不使用ExecuteAlways，编辑预览归Editor独立逻辑。预览缓存真实文字/字体/桌面矩形原值，在保存、切换模式、重载、关闭前恢复，之后再释放临时字体与牌；不覆盖预览后手动改动。发牌列表显示目录全部定义。Default Play入口和构建场景保留。
- [ART] Art/CardFaces、CardFrames、Table、Icons、Audio/SFX、Audio/Music预留；用户Art/Assets保留不自动接入。单张CardDefinition.Artwork负责插画；主题仅通用卡框/卡背/桌面/字体/声音，原CardFaces重复映射与空序列化字段已删。中文OS字体保留，空图/音频继续占位/静音。详Art/README。
- [SAVE] BoardSaveStore保留格式v1、SHA256、10MB上限、同目录临时写/Flush/原子替换/.bak恢复；备份结构验证注入当前catalog，禁用Save功能直接Save/Load也拒绝。旧family.sample按旧Rank匹配迁移，默认映射1..3；旧演示额外发牌4..9需补定义映射，缺失整体拒绝且保留当前桌面，不虚称所有旧档自动兼容。实例属性/翻面/ID/牌序/位置保留，静态字段按新定义解析。
- [VERIFY] 本轮仅静态检查，不能报告编译/运行/测试通过。25脚本、7符号的128种组合扫描没有已裁剪方法残留引用；13个配置/Prefab/场景文件YAML可解析、内外部fileID与GUID有对应资产（含本地uGUI包）；CardTable无缺.meta、全Assets无GUID冲突。独立审查修正Editor对Unity.ugui的直接引用、唯一AudioListener、CanvasScaler Expand、预览保存恢复；最新预览已复核无新阻断。Tests/Core新增4组目录/迁移/堆叠/权限/排序场景源码，未执行；18组+3000随机操作通过只属于重构前历史版本。
- [DELIVERY] 本轮代码、实际配置资产、Prefab、文档与静态整合已完成，保留用户既有资源，无额外玩法。中文提交与上传沿用已有授权，最终提交号/同步状态以git log和status为准。没有运行编译、核心检查工程或Unity，不得把静态扫描写成编译通过。
- [USER_STYLE] 用户明确否定上版策划与界面，认为太AI、说明字过多，要求用skill重做。已读humanizer、game-ui-design及其参考、game-design-theory。不要把长表格、数字核算或商店简介当成有趣设计的证据，回复简短直接。
- [DESIGN_REJECTED] 《余烬工坊》v0.1已被用户否定。十四天、两工人半日排班、三次相同修塔、H加减热条均停用；原策划和CSV/JSON已从活跃文件删除，历史在18a8b74。不要照旧案实施，也不要把其通关核算当作玩法成立。
- [DESIGN_CURRENT] 新稿 `docs/design/制作原型.md`：单人行动耗时推进锅火与窑的后台过程；先试露天燃烧的供热/木炭与封窑的木炭/焦油，焦油修水罐减少漏水和重复取水。15分钟只是刻度，动作时长不同；参数是待试玩假设。参考原文及官方截图在 `docs/design/参考记录.md`。只写设计，尚未实现生产。
- [UI_CLEANUP] 本轮真正删除Prefab中的顶部标题、常驻指南、counts统计、details占位组件，以及代码字段；短按钮、短选中信息，问号帮助默认关闭且按功能生成，Esc收起。正常成功不报长消息，可恢复错误4秒后收起；启动错误经ShowError持续显示。CardView加1px暗色Outline便于辨认同色牌边界。GUID/主题/动画/编辑预览保存恢复保留。两Prefab178/26对象的引用做过静态复核，未运行Unity或编译。outputs/桌面排版预览.png是按Prefab绘制的静态排版模拟，不是Unity运行截图。
- [NEXT] 当前运行仍是清理后的自由卡牌桌面。后续生产实现先完成同一时钟、锅火供热关系，再做封窑和水罐；由整局事务统一库存/过程/时间，不拼多次Spawn。先观察玩家有没有因加工状态改变安排，再扩种植、天气等系统。

## 接续顺序

1. `git status`，读本文件和README，保留用户最新修改。
2. 按真实文件/代理进度完成WIP项，编译与Unity运行不要自行执行。
3. 完成后README简记本次变化与验证范围；更新本文件锚点，中文提交并按已有授权上传。
