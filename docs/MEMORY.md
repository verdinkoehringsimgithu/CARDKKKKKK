# 开发记忆

更新：2026-11-09。先读本文件和 AGENTS.md，再看实际代码与 Git 状态。当前「孤岛生存」以用户提供的出航版 PDF 为准，旧营地经营版 1.3 及其需求表为历史资料。用户自己执行 Unity、编译与运行验收。

## 当前授权与方向

- [INTEGRATION_REVIEW_20261109] 本轮复核后：job初始化、StagePair原子事务已在源码修正；同类并行产物原先仍按类型猜来源，现Settle记录非序列化ProducedByJobId/ProducedFromX/Y并经Copies传递，View读取来源；飞牌被拖起或入准备区会退出协程。未编译/运行。网页编辑器以Tools/RecipeEditorWeb/index.html为唯一内容源，根HTML仅跳转。整合状态见交接06；A~E规范非实现，原Survival暂留不可盲删。参考游戏与解包目录加入ignore；仅暂存项目源码/工具/交接，覆盖率配置保留未暂存。不提交推送。


- [DESKTOP_HANDOFF_SCOPE] 2026-11-09 用户要求直接拆解本地 `Card Cultivation`，先做部分拆包、理解和任务安排，建立交接文件夹给其他AI；列出的移动/重叠/弹出/弹飞/合并/吸附/高亮/场所UI/引导/计时器是不完整举例。当前阶段为底层研究交接，未授权本轮重写游戏。入口 `docs/handoffs/2026-11-09-卡牌桌面底层/00-从这里开始.md`，含证据、任务依赖、建议契约、32项操作验收和接手指令。
- [DESKTOP_HANDOFF_CORE] 原包 IL2CPP metadata v31；已提取 Assembly-CSharp 的1685类型和14376条全局字符串。7个选定StreamingAssets包还原UnityFS并LZ4解包为11节点，引擎2021.3.45f1；元数据/路径/节点/哈希索引在交接证据目录，二进制与两个纯Python复现脚本在 `work/card-cultivation-handoff/`。原始包不修改，不导入Assets。优先关注 BaseEntityLogic、BaseFormEntityLogic、WidgetResourceFormEntityLogic、SnapSlotEntityLogic、InputMgrComponent、TimerProgressControl；索引不是方法体。
- [DESKTOP_HANDOFF_VERIFY_NEXT] 已完成全部 28 篇底层交接规范文档（其中 R01/R03/R04 为局部资源/输入提取 E1/E2，R02 决策树为 E2/D 建议模型，A~E 组为工程化设计契约与验收矩阵）。规范交付为后续开发提供了清晰设计基准，但不等同于全套底层已在代码与场景中实装。当前已实装代码包括：VoyageEngine.StagePair 原子双牌组合事务、VoyageView 卡牌任务可见性修复、按核心结算记录来源的飞牌动效、右键快速入槽及防重叠落点偏移；但场所独立弹出窗口与独立卡槽 Prefab、60/85px 施密特吸附管线及有限步松弛物理避让仍属设计规范，尚未在场景与 Prefab 中落地。当前状态：进入分阶段实装阶段，不虚夸底层闭环。未运行参考游戏/Unity/编译/测试。
- [DESKTOP_R01] 2026-11-09 已完成R01：实现纯Python TypeTree反序列化器，从Card/Timer/Area/UIForms解析26个目标Prefab层级（1013节点、2587组件）。恢复BoxCollider2D尺寸(0.64, 0.852)、SortingLayerOrderControl三色(白/绿/红)、TimerProgressControl及场所桌面实体(Timer)+局部窗口(Form)解耦结构。产出R01-对象层级与参数.{json,md}。未启动编译器、Unity或测试。
- [DESKTOP_R02] 2026-11-09 已完成 R02：提取 GameAssembly.dll 40 个判定相关函数符号 VA 与元数据 Token（E1）。梳理松手四级判定决策树（UI拦截→表单卡槽→吸附槽→卡牌堆叠→桌面避让与边界收束），已更正证据等级：决策树分支顺序系根据符号职责推断的架构模型与本项目设计契约（E2/D），未反编译解析原生机器码控制流，不能断言为原游戏已确认行为。产出修正后的 R02-松手判定决策树.{json,md}。未启动编译器、Unity或测试。
- [DESKTOP_R03] 2026-11-09 已完成R03：还原5对场所/窗口Prefab结构、卡槽层次(主槽+辅槽)、双时钟同步(桌面实体原地timer_progress与窗口TimerGroup)、窗口关闭后台继续、cardGrid格位生成产物后DetachEntityFly飞向桌面落点的完整链路。产出R03-场所窗口与产物链.{json,md}。未启动编译器、Unity或测试。
- [DESKTOP_R04] 2026-11-09 已完成R04：反序列化inputcontrols.inputactions（PathID: 418656196238816513），完整提取10个Action Maps（gameplay, gameUIKeyboard等）与全部按键绑定。结合IL2CPP逆向厘清InputMgrComponent与CameraControl运行时路由：UI阻挡绝对优先(PositionIsOverUI)、持牌拖拽与相机平移互斥、滚轮平滑阻尼缩放(ZoomDecay)、Esc取消与lookBounds视口边界保护契约。产出R04-输入映射与键位表.{json,md}。未启动编译器、Unity或测试。
- [DESKTOP_A01] 2026-11-09 规范已记录（非整体实装）：A01：深度审计BoardEngine、VoyageEngine与IslandFoundation三引擎代码，确立“拒绝第四套平行桌面引擎”的三层分层复用架构：Layer 1 统一桌面空间与严密牌堆几何内核（复用并升级BoardEngine）；Layer 2 场所卡槽（SlotBinding）与独占任务锁（TaskLock）归属契约；Layer 3 玩法业务规则（保留Voyage 144条配方与时钟调度，废除旧集中暂存面板，转为实体场所卡槽）。产出A01-三引擎审计与复用契约.md。未启动编译器、Unity或测试。
- [DESKTOP_A02] 2026-11-09 规范已记录（非整体实装）：A02：规范TableResult统一命令结果、只读查询（QueryDrop/QueryCandidates）、核心表现事件（CardMoved, CardStacked, SlotAssigned, TaskStarted, CardDetachedFly等）与8类具体错误码。确立事务“零污染准则”、唯一所有权单射与状态版本Revision递增控制：失败绝对不扣料、不位移、不递增版本。产出A02-统一命令与事件契约.md。未启动编译器、Unity或测试。
- [DESKTOP_A03] 2026-11-09 规范已记录（非整体实装）：A03：冻结CardDefinition（静态定义）、TableRules（空间与配方规则）、TableTheme（外观与三色高亮主题）及运行时状态的四权分工。规范稳定技术ID格式与旧版别名迁移机制；确立写入三阶段原子安全协议（.tmp -> .bak -> replace）与读取三级严格校验（SHA256 + 规则指纹 + 归属拓扑图谱校验）。产出A03-配置与存档规范.md。未启动编译器、Unity或测试。
- [DESKTOP_A04] 2026-11-09 规范已记录（非整体实装）：A04：规范卡槽预留独占性、任务锁（TaskLock）工作期间自身计时冻结机制、消耗/磨损/返还/销毁全生命周期不变量与取消/完成操作的幂等性保护。产出A04-实例预留与任务锁规范.md。未启动编译器、Unity或测试。
- [DESKTOP_A05] 2026-11-09 规范已记录（非整体实装）：A05：建立确定性时钟流水线，规范同帧多事件5阶段全序排序（任务完成->设施充能->寿命腐败->生理扣除->生物移动）、多重暂停原因掩码（PauseFlags）与读档强制暂停策略。产出A05-时钟推进与事件顺序规范.md。未启动编译器、Unity或测试。
- [DESKTOP_B01] 2026-11-09 规范已记录（非整体实装）：B01：规范6px拖动死区阈值（点按不误拖）、抓取偏移零跳变法则、子堆与单牌拆分逻辑及原子取消平滑回弹。产出B01-拖拽会话与抓取偏移.md。未启动编译器、Unity或测试。
- [DESKTOP_B02] 2026-11-09 规范已记录（非整体实装）：B02：建立三层坐标映射体系（屏幕像素->1600x900逻辑桌面->视口表现）、视口缩放平移与逻辑坐标隔离、UI滚轮阻断及lookBounds边界保护。产出B02-多分辨率适配与视口平移缩放.md。未启动编译器、Unity或测试。
- [DESKTOP_B03] 2026-11-09 规范已记录（非整体实装）：B03：规范四级自顶向下落点判定流水线（UI卡槽->实体吸附槽->牌堆堆叠->桌面物理避让），确立Topmost命中优先法则与预览/提交同源评估引擎。产出B03-统一落点判定与命中优先级.md。未启动编译器、Unity或测试。
- [DESKTOP_B04] 2026-11-09 规范已记录（非整体实装）：B04：规范单一父级线性列表拓扑、无循环父子保证、高精度任意位置插入算法（CalculateInsertionIndex）、MaxPileSize上限防御及实体属性守恒铁律。产出B04-物理叠牌与堆叠组织规范.md。未启动编译器、Unity或测试。
- [DESKTOP_B05] 2026-11-09 规范已记录（非整体实装）：B05：规范物理推开与有限步松弛动力学（8轮收敛上限）、任务锁牌与设施无限质量约束、ClampPosBounds桌面刚性收束及桌角阿基米德螺旋搜索兜底。产出B05-碰撞避让与空间收束规范.md。未启动编译器、Unity或测试。
- [DESKTOP_B06] 2026-11-09 规范已记录（非整体实装）：B06：建立双阈值施密特触发器滞回防抖模型（Enter 60px / Exit 85px）、卡槽安全拔出解绑流水线与目标失效互反引用自愈机制。产出B06-槽位吸附与滞回防抖规范.md。未启动编译器、Unity或测试。
- [DESKTOP_B07] 2026-11-09 规范已记录（非整体实装）：B07：规范全局L0背景至L6系统模态的七层渲染与射线命中同序拓扑，确立拖拽动态提层与精确回位、R01三色状态（白/绿/红）与Tooltip防遮挡自适应镜像。产出B07-渲染层级与高亮规范.md。未启动编译器、Unity或测试。
- [DESKTOP_B08] 2026-11-09 规范已记录（非整体实装）：B08：建立异常中断矩阵（Esc取消、失焦自愈、目标销毁降级、切场景注销），确立按钮与快捷键单一命令控制器入口（Single Source of Command）与模态防穿透遮罩。产出B08-输入屏蔽与异常保护.md。未启动编译器、Unity或测试。
- [DESKTOP_C01] 2026-11-09 规范已记录（非整体实装）：C01：规范桌面常驻实体（timer_progress原地微型进度）与按需唤出的Canvas局部独立窗口（主槽+副槽1~4+cardGrid+BtnStart）解耦模型，保障同类多场所实例状态互斥隔离与窗口关闭后台静默推进。产出C01-场所实体与局部窗口模型.md。未启动编译器、Unity或测试。
- [DESKTOP_C02] 2026-11-09 规范已记录（非整体实装）：C02：规范定义ID与标签双轨配方匹配算法、各槽实例排他防重借、缺项精确提示、未知配方盲试保护及32条副产线辅助联动机制。产出C02-配方匹配算法与探索解锁规范.md。未启动编译器、Unity或测试。
- [DESKTOP_C03] 2026-11-09 规范已记录（非整体实装）：C03：建立局部精准卡槽高亮管线（精确排除预留/任务锁/坏死牌）、动态响应自适应刷新与失配具体原因即时气泡指引机制。产出C03-卡槽候选高亮与可用性引导规范.md。未启动编译器、Unity或测试。
- [DESKTOP_C04] 2026-11-09 规范已记录（非整体实装）：C04：建立准备→执行→完成→放出四阶段实体生命周期状态流、卡牌在槽内持续可见与状态追踪机制及窗口/桌面双进度同步展示规范。产出C04-任务全生命周期与实体状态可见性规范.md。未启动编译器、Unity或测试。
- [DESKTOP_C05] 2026-11-09 规范已记录（非整体实装）：C05：建立先提交核心归属后播动画的DetachEntityFly飞牌管线、批量产物径向环形扇出算法及着陆后AutoMergeOnSpawn自动合堆保底策略。产出C05-产物飞出散开与落点预留规范.md。未启动编译器、Unity或测试。
- [DESKTOP_C06] 2026-11-09 规范已记录（非整体实装）：C06：建立工作进度与自然恢复冷却双通道独立数据模型、储量次数归零锁定卡槽状态机及不可再生资源耗尽注销规范。产出C06-工作进度与资源恢复分离规范.md。未启动编译器、Unity或测试。
- [DESKTOP_C07] 2026-11-09 规范已记录（非整体实装）：C07：建立移动生物安全目标查询管线（排查拖拽持牌/卡槽锁牌/失效牌）、全暂停钩子冻结及目标中途被消耗时二次断言安全重查机制。产出C07-移动生物追踪与动态寻路规范.md。未启动编译器、Unity或测试。
- [DESKTOP_D01] 2026-11-09 规范已记录（非整体实装）：D01：规范7类核心卡牌动效参数（Move/Drop/Reject/Snap/Fly/Merge/Disintegrate）、抛物线弹道模型及读档重开瞬时对齐快照的中断安全准则。产出D01-表现层动画参数与取消规则规范.md。未启动编译器、Unity或测试。
- [DESKTOP_D02] 2026-11-09 规范已记录（非整体实装）：D02：落实去AI说明墙与按需提示层级、6类操作音效精准映射（PickUp/Drop/Snap/Reject/Start/Finish）、轻量短错误气泡反馈及Humanizer文案铁律。产出D02-悬停反馈与视听交互规范.md。未启动编译器、Unity或测试。
- [DESKTOP_D03] 2026-11-09 规范已记录（非整体实装）：D03：建立真实序列化Prefab编辑态可见层级标准、对象池复用6步零残留清洗协议（Sanitization Protocol）及场景注销事件/材质清理机制。产出D03-预制体规范与对象池清理机制.md。未启动编译器、Unity或测试。
- [DESKTOP_E01] 2026-11-09 规范已记录（非整体实装）：E01：建立手+资源点采集->产物弹飞->材料+工具生产点加工的全流程纵向操作闭环、多任务并行互斥隔离及V01~V32矩阵核心验收链路贯通规范。产出E01-纵向全链路整合流程规范.md。未启动编译器、Unity或测试。
- [DESKTOP_E02] 2026-11-09 规范已记录（非整体实装）：E02：规范各玩法存档跨目录物理隔离矩阵、完整拓扑图谱序列化标准（Cards/Piles/Facilities/Jobs/Global）及.tmp写入/.bak滚动备份/SHA256三级防损坏自愈机制。产出E02-独立存档与完整拓扑持久化规范.md。未启动编译器、Unity或测试。
- [DESKTOP_IMPLEMENT_VIEW] 2026-11-09 桌面交互与核心事务阶段实装：1) VoyageEngine.cs 新增 StagePair(first, second) 原子事务，在单一 Commit 校验两卡状态与容量上限，杜绝多卡入槽时单边提交的副作用；2) VoyageView.cs 彻底解除任务进行中卡牌隐藏（原 card.JobId != 0 => SetActive(false)），卡牌在执行中持续在桌可见并施加工作高亮框；3) 修复 job 短路声明导致的未赋值编译问题（CS0165）；4) 实装基于产物配方映射的多任务独立来源飞牌（FindProducerJob），解决并行任务产物来源错位；5) 支持桌面拖拽双卡原子入槽与右键 QuickAction；6) 明确记录：独立场所专属弹出窗口与独立卡槽 Prefab、60/85px 施密特吸附等仍处文档规范阶段，尚未在代码与 Prefab 中实装。未启动编译器、Unity或测试，未提交上传。
- [DESKTOP_E03] 2026-11-09 规范已记录（非整体实装）：E03：审查程序集依赖（Asmdef单向无环、Core纯C#无引擎依赖）、验证所有权单射与状态不变量证明，输出基于04验收矩阵的6步用户验收交互指引。产出E03-全工程静态审查与验收交付规范.md。未启动编译器、Unity或测试。

- [VOYAGE_SCOPE] 2026-11-09 用户说“以我发你的那个为准 继续制作”。设计源为 `D:/wechat/liaotian/xwechat_files/wxid_dm9mfnvg9wzm22_c773/msg/file/2026-11/孤岛_游戏设计说明书_独立交付版_修订稿.pdf`。同目录便携编辑器是旧格式工具，示例 JSON 有 107 条物品记录且配方为空。本项目运行使用新字段；沿用有效物品 ID，防水涂料保留 `item_101`，木偶使用 `item_108`。时长、概率、水位、耐久未定，规则资产保存可编辑联调值。自由牌桌、法术拆装仍走各自原入口。
- [VOYAGE_CORE] `Assets/IslandSurvival/Core/VoyageData.cs` 与 `VoyageEngine.*`。身体牌按实例锁定，统一输入槽支持保留、消耗、磨损与修复。准备阶段伤势跟随，倒计时继续；执行期间相关牌及伤势均锁定并冻结自身计时，结算时应用副产线、耐久和产物。饥饿随时间下降，SAN/饥饿归零判败；两种路线共用航线卡，合成出航卡判胜。状态与随机数随任务和卡牌位置保存。
- [VOYAGE_UI_SAVE] `Scenes/IslandSurvival.unity` 现引用 `Prefabs/VoyageDesk.prefab`。`VoyageBootstrap` 处理时钟、入口与存档，`VoyageView` 展示组合、任务、文本卡、液面与磨损。`VoyageSaveStore` 用独立 `voyage-auto-v1.json` / `voyage-manual-v1.json`，有规则指纹、摘要、原子替换、备份回读。新规则不会读取旧营地档。配置入口「孤岛生存 → 出航规则配置」，`VoyageRules.asset` 配 131 种卡牌、144 条组合、32 条副产线，`VoyageCards` 是各牌资源。`tools/build_island_voyage_content.py --replace` 会重写内容，保留已有插画引用，日常调数直接编辑资产。`IslandVoyageAtlas.png` 16 张插画通过 CardDefinition 接入；低 SAN 背景和普通卡面褪色，海滩声淡出，卡纸、操作反馈与特定思绪声音独立。
- [VOYAGE_VERIFY_NEXT] 静查 C# 语法、配置及 Prefab 引用，按依赖图分别核对研究/幻觉到 E06 的可达性。尚未验证 Unity 导入、鼠标操作、执行中存读档、数量和实际整局节奏。下一次从原桌面进入「孤岛生存」，核对腿×2＋海滩→帽贝岩、手＋帽贝岩→开壳→躯干进食，以及并行研究、到期冻结和恢复存档。部分牌用类别字样，后续补正式插画和细化数值。场所占用当前每张一项任务，主动中断退回投入。旧营地的历史锚点如下。

- [ISLAND_IMPLEMENT] 本轮开始按docs/design/孤岛桌面生存-完整设计.md v1.2及正式XLSX需求表开发。路径Assets/IslandSurvival。Core/SurvivalData.cs统一DTO、SurvivalEngine候选副本事务；Actions/Tasks、Body/Biology、Configuration由并行代理实现，根负责Inventory/Validation/Unity存档与真实Prefab场景集成。尚在开发中，无编译/运行验证。各新Assets文件须补合法.meta。数值可临时但必须配置可改，不能自行增加机制。最新工作进展以真实文件和后续锚点为准。
- [ISLAND_CARDS_13] 用户对策划评审指出偏数值模拟、缺卡牌联系，并明确纠正疾病应为《密教模拟器》式独立卡；反感把事实核对和基础设计筛查交给用户。已撤销“只做角色面板附着卡”的建议，按独立状态牌开发：CardKind.Condition第6类；伤口+流血/感染/疼痛/刺激/衰弱6定义；ItemState.ConditionId唯一引用身体canonical状态，拥有真实ID/坐标/任务锁、作为护理配方目标、状态结束取消目标任务再移除。独立状态桌面不计24/32/40生产容量，不是普通材料。开发者原始依据已查Steam HELLEBORE回复：Health变Affliction保留同张牌位置；不得凭印象再次把独立牌解释为面板图标。设计/Excel由配置代理更新1.3，原稿不改。
- [ISLAND_CORE_NOW] 新Core已含Inventory/Validation/Actions/Production/Tasks/Body/Biology/Conditions/Queries，候选事务、逐分钟调度、R01~31、生态/喂养/两段繁殖/状态护理等已落源码；默认52卡56动作，经SurvivalConfiguration.BuildDefinition桥接CardDefinition。UI真实Prefab沿旧IslandPreparation GUID，根View1610与Bootstrap，运行模板+编辑态PreviewRoot；SurvivalSaveStore独立island-v1.json原子/.bak/SHA与规则指纹。尚未编译/运行，根继续静态集成修复，不能称完整验收。
- [ISLAND_VERIFY_NOW] 已静查并修正：产出新Lot.AvailableAt防同分钟合堆重投；原槽腐败ResidueFrom且预留空壳先保留；潮汐同Amount合并预留；拆解复用目标Reservation.ReplacesItemId但取消前不借走净释放容量；40容量改8x6候选格(逻辑仍40)，CellHeight100；损坏区域既有成长/产叶继续只停新周期；精神0可暂放任务休息再恢复；每节点LastForcedDay；独立状态正反唯一引用。View已修Esc暂停、拖动暂停/结束恢复、失焦暂停、小容器子牌越界；配置代理仍审UI。用户仍自行编译/Unity运行，助手不启动。剩余meta/静态引用全检/需求对照/美术待本轮收口。

- [UPLOAD_20261106] 用户明确要求修复Git上传并代为提交推送本次累积内容。上传前远端main为303ce22，本地未推送3a4e3fa误带work/survival-reference/package0/1/2.exe（约281/203/137MiB）。已建仅本地备份分支backup/upload-before-cleanup-20261106，并把当时暂存差异存入.git/codex-upload-staged-20261106.patch；soft重整尚未推送历史，停止跟踪三个安装包和三份pyc，原文件保留。共享.gitignore加入__pycache__/、*.py[cod]及两个研究/表格构建工作目录。代码、美术、孤岛入口、设计与正式XLSX正常纳入本次main；不要推送备份分支或使用push --all。具体上传完成与提交号以Git远端和本次答复为准，不推断已运行Unity。
- [REQUIREMENTS] 2026-11-06用户要求按系统做程序/策划/美术需求表，并强调程序未定内容须方便策划配置。已交付outputs/01a1aae4-8672-7cd2-8fde-a44094785f06/孤岛生存-开发需求表.xlsx：系统需求62条/21系统、策划配置30合同、美术46项、阶段验收6项；列明三方交付、前置、独立进度、运行验收及计算整体现状。入口三方交付完成但待运行，其他未开始，无伪造已验收。统一配置合同要求中文字段/单位/默认/校验/应用/旧档策略，修改实际生效；未实现字段不出现在运行配置。数值可临时，新增机制仍由程序实现。
- [REQUIREMENTS_VERIFY] 用bundled artifact-tool创建并核对四页预览；62 ID唯一、依赖无悬空/循环；更改部门进度与验收标记已验证不会提前显示已验收；公式无错误，导出4表筛选、冻结8行3列均存在。未运行原生Excel/Unity/编译/游戏测试。work/island-requirements含单一构建器/JSON/预览/node_modules联接，已本地exclude；后续改表应读现有XLSX保留用户进度，不能直接用初始构建器覆盖。
- [ISLAND_ENTRY] 2026-11-06用户要求“单开一个叫孤岛生存，放在测试玩法旁边”，追问后明确指游戏内按钮。已加到真实TestGameplayEntry.prefab，主场景接islandButton/Label/ScenePath；TestGameplayEntry单实例路由保留旧Enter和TryReturnToTable，入口互斥。Assets/IslandSurvival独立asmdef、Unity/IslandPreparationView.cs、Prefabs/IslandPreparation.prefab、Scenes/IslandSurvival.unity及meta已建立；准备页只显示标题/开发准备中/返回。Esc返回；直接运行独立场景无保留桌面时Single加载默认牌桌。BuildSettings第三项deeaf43f1b6350ed80d529afb3fd49c3。未实现生存规则，仅静态核对；用户自行编译运行。无Git提交。
- [ISLAND_PLAN] 已完成docs/design/孤岛生存-完成度与分阶段开发计划.md，设计成熟约75%属工程判断，核心玩法未实现。按1主程序+1设计/素材/验收持续投入、AI辅助：2~3周基础灰盒，4~6周身体闭环，7~11周生态，12~18周发布候选，含缓冲14~22周约3~5月。主程序50~76人日+配合35~53人日，阶段A后复估，不把数值待定设开工阻塞。首版验收改为实际完成首顿/下一顿/护理/工作中存档，不能只“等过3天”。当前仅授权并完成入口与计划，主体玩法未自动展开。
- [DESIGN_V1_2] 2026-11-06用户说“补充进去案子里”，已将三作参考整合到docs/design/孤岛桌面生存-完整设计.md 1.2，而非只附清单。第3节按里程碑减负/风险窗口；第6节伤口/流血/感染/疼痛/刺激、来源份额δ按动作投影、护理与暴露分层；第8节R27~31应急护理与一次干燥、材质维修；第9~10节区域有界负荷系数与人力净收益；第12~14节解锁、批量/护理队列、存档及22项验收同步。保留I/R/V与行动消耗，未引入饮水条、天气/战斗/烟湿授粉。未定数值不阻塞后续实现，可配置临时值；当前只改文档，未恢复开发、未编译/运行/测试/提交。
- [DESIGN_V1_2_REVIEW] 静态审阅后修正：局部减速从来源份额δ投影而非读取全身1−I；R13复用敷料使用/更换并明确完成时起效；区域减速后的螺收支只在φ=1稳态成立；收获中途延期先腐败并取消任务；R27~31与强采解锁条件明确。伤口份额Σδ=1−I，修复按比例分摊；不是新增一条可独立扣减的健康值。
- [REFERENCE_RESULT] 已交付docs/design/三款生存卡牌-异常配方与节奏拆解.md：三作各自异常因果/具体配方/阶段节奏，候选异常分工、配方层次、人力释放与当前I/R/V适配建议。最初仅独立研究，随后用户授权整合1.2（见上），仍未改游戏代码。参考包、库、原始导出保存在work/survival-reference，并通过本地.git/info/exclude排除；不入Assets、不复制第三方实现。
- [REFERENCE_VERIFY] 已目视三作官方截图。别冻僵官方简易刀截图显示6分钟，本地CraftTime20；开放伤截图显示每15分钟Condition−7.5，仅对截图版本成立。热带恢复血压额外耗营养、伤口/敷料/止血分工，森林孢子暴露与Lungrot独立、烟参与晾晒均有本地文案/资产与官方互证。精确阈值、微分公式、序列化生效路径和实际游玩未验证，不能宣称原作用现稿I/R模型。
- [REFERENCE_ACTIVE] 2026-11-06继续拆解用户zip中的Don't Freeze(B23064544)、Card Survival Fantasy Forest(B23970026)、Tropical Island(Build15545657)。已用用户密码静态解包，未运行EXE。工作目录work/survival-reference，独立Python库在python-libs；game0/1/2为包内容（game2嵌套rar已tar解开）。inspect_data.py提Unity文本与.NET类型元数据；catalog_content.py提本地化/资产名。evidence0/1/2分别对应三作。游戏Unity资源自定义类型树大量缺失，因此只用可读CSV/本地化、资产名和类型结构，不凭文案反推公式或全部生效引用。
- [REFERENCE_EVIDENCE] 工作目录已有三份*-official.md、官方news.json（别冻僵100条/热带74条）、forest-local.md、别冻僵导出CSV与local-afflictions报告。别冻僵状态表resources.assets PathID990有27行，Blueprint1014/Tools958/Material957/主目标966；热带本地化SimpCn.csv共14345条，森林Cn.csv共25657条。官方伤口/森林截图已目视检查；森林未来11月山区内容未发布不能算现有机制。最终应形成docs/design下独立参考拆解，不直接改1.1设计或开始开发。
- [SURVIVAL_MODEL] 用户否定固定日扣与逐条加法效果，明确正常行动消耗精神/饱食，健康/生命损伤由异常和事件触发；健康同时包含组织完整与抵抗恢复能力。已按“先写进去md”更新孤岛设计1.1第6节：内部I/R，H=100I^ωR^(1−ω)，负荷积分、异常严重度与三通道负担、修复/机能/生命公式、暴露风险。同步R11/R13/R25、贝饰、原每日96消耗结论、存档与验收。仅记录模型，系数/负荷/疗效时长/数值步长待标定，未运行测试；动物简化规则明确独立。下一步讨论两条伤病过程，未经授权不改玩法代码。
- [DESIGN_REQUEST] 2026-11-05用户提供D:/wechat/liaotian/xwechat_files/wxid_dm9mfnvg9wzm22_c773/msg/file/2026-11/writing-block.md，要求整理为可发布、完整、可执行、严谨的设计，必要时提问、不要过度思考。本次仅文档工作，不能推断用户授权替换法术玩法。
- [DESIGN_DOC] 新稿docs/design/孤岛桌面生存-完整设计.md：沿用原稿五类牌、自然节点、人造生态、废料和损耗；补充30日流程、固定分钟结算、配方/工具、芋/芦荟/虚构养殖螺、定向育种、存档与分阶段验收。数值和首版范围均为本稿设计决定，未试玩验证。与当前游戏实现分开，现有入口保持不变。
- [USER] Unity2022.3.62f3c1、Windows单机鼠标，两人开发；中文游戏/菜单/README/提交说明。反感AI口号、常驻说明墙和把游戏做成开发工具。
- [AUTH] 日常只改本地，明确说提交才commit、上传才push；2026-11-06本次上传已获明确授权，见UPLOAD_20261106。编译、Unity与游戏测试由用户自己检查，助手不运行。可做静态源码/引用/配置检查。
- [REPO] D:\wenjian\cardkkk，main，remote verdinkoehringsimgithu/CARDKKKKKK。本次上传前远端基点303ce22；尚未推送的大包提交已在本地备份后重整，实际最新HEAD以git log为准。
- [DIRECTION] 用户连续否定八配方工坊和河谷营地，要求复杂、多发散。六案后先说“你做就是”，随后明确“我感觉2挺好玩的，可塑性很强看你怎么设计了”。2指最新答复的“拆开法术重组触发/效果/改装”，不是文档旧排序的铺遗迹。已按此实现，拆巨兽及时停掉，没写进产品。
- [SCOPE] 保留Assets/CardTable原自由牌桌、功能、牌位与存档，只从“测试玩法”按钮加载独立法术场景。旧八配方源码/配置/场景/Prefab已逐文件删除，历史在303ce22，旧图片留档；原牌桌没删功能。
- [ART] 用户喜欢雪居之地的造型/色块，不能推断复制其角色、边框、雪地题材或规则。SpriteCook复查0额度，本轮按既有授权用内置image_gen，生成原创16对象图集、背景、卡框，全套统一深青蓝轮廓、奶白/珊瑚橙/琥珀/淡紫。旧三栏概念也被否定，不能再用。
- [CURRENT_REQUEST] UI重构后用户要求“看看有没有bug”。已分开静态复核Core、存档入口和UI，修复五类确定问题，下面BUGFIX锚点记录；未编译/运行/提交。此前窗口不适配与“余烬”等显示名重构已完成，不再另换玩法。玩法数值、稳定ID、解锁、结算顺序未改。

## 当前法术拆装实现

- [ENTRY] Assets/TestGameplay/Unity/TestGameplayEntry.cs保留独立入口控制。原场景Assets/CardTable/Scenes/CardTableDemo.unity的gameplayScenePath已改为Assets/TestGameplay/Spellcraft/Scenes/Spellcraft.unity；BuildSettings原场景第一项，新场景第二项。先停原根1000再additive加载，返回卸载并恢复原牌桌内存状态。新入口GUID与旧路径不要混用。
- [MODULE] 新代码均在Assets/TestGameplay/Spellcraft。Core程序集CardKKK.Spellcraft.Core(noEngineReferences:true)；Unity程序集CardKKK.Spellcraft.Unity引用新Core、Unity.ugui和仅含入口的CardKKK.TestGameplay.Unity；Editor程序集独立。旧WorkshopCore程序集与引用已移除。
- [CORE] Core/ForgeData.cs DTO/枚举、ForgeCopies.cs深拷贝、ForgeEngine.cs唯一状态/命令/事件、ForgeEngine.Validation.cs校验。namespace CardKKK.Spellcraft.Core。Definition/State均深拷贝。ConfigureCircuit/ClearCircuit/StartBattle/SelectEnemy/PlayCard/ActivateCircuit/EndTurn/ChooseReward/Restore通过候选副本、校验、一次提交；失败不扣费不弃牌不推进RNG。
- [PREVIEW] PreviewCard/PreviewCircuit→ForgePreview{Success,Error,State,Events}共用实际结算流水线，commit=false，不发Changed、不推进随机。严禁另写简化伤害预测。UI用DescribeIntent显示与实际一致的意图。
- [EVENTS] 3槽，每个由Trigger/Effect/Modifier标识组成，方案可复用，工坊免费拆装。自动/手动都先扣火花占次数，再FIFO排SparkSpent与效果；手动另花1能量。每槽每回合最多2次；缺火花/次数满仅抑制并说明，单命令128事件预算超出才撤回。Changed观察者异常隔离。xorshift32完整RandomState入档。
- [FLOW] Workshop→Battle→Reward→Workshop，前三胜各领1新零件，领后关卡+1；末关Victory，生命0Defeat。Reward的EncounterIndex仍指刚打完关卡。战胜立即停止事件/敌后续攻击。四场显示名现为机械甲虫、反伤组合、火花干扰、强化组合；ID未变。
- [RULES] 初始42HP，每回合3能量，每场3火花、上限12、5手牌；战后治疗9。盾到下个己方回合开始归零；火花跨回合。EndTurn：燃烧扣HP绕盾并减层→敌行动（冻结跳过减层、保留意图）→弃手抽牌/补能量/归零次数/TurnStart。敌盾到其下次行动前清零；敌攻击每4回合增强1，蓄力只用于下一次攻击/双击。燃烧不反弹，直伤致命仍反弹，双方同死判败且不复活汲取。
- [CONTENT] Configuration/法术工坊配置.asset，18种零件、5种基础牌、10张起始牌组、4场遭遇。初始9已知；3次奖励各选1。默认3装置：manual→bolt，hit→spark，spent→guard。首次手动炮按规则花1能量共3火花→4直伤+8盾（放电盾自触发2次），这是纸面规则分析，未运行。自励0费不会产生SparkSpent。Fork/Echo强度减1最低1，Overload额外1火花+2强度；Echo基本费用只扣1次。
- [CONFIG] ForgeConfiguration.cs资源桥接，BuildDefinition深拷贝并检查当前界面上限1～5手牌/3敌/3奖励；Core固定3槽。Editor/ForgeConfigurationEditor.cs为中文Inspector，按零件种类隐藏无效字段。菜单测试玩法/法术工坊规则、打开法术工坊。配置在重开或再进入应用，改规则指纹后旧档拒读。
- [UI] Unity/ForgeBootstrap.cs命令与装配；ForgeView.cs快照显示、零件选择、目标/快捷键/完整预演；ForgeCardView/ForgeDeviceView播放提交后动画。Prefabs/ForgeView.prefab真实预置，编辑态可见，无Start造桌面。布局源码tools/build_spellcraft_ui.py，重新生成会覆盖Prefab和Scene，不自动执行。
- [UI_LAYOUT] 最新结构：CanvasScaler参考1600×900、Expand；SafeArea无固定pos/size inset；FitViewport用AspectRatioFitter(FitInParent=3)，其子逻辑桌面由新增Unity/ForgeDesktopLayout.cs整体等比缩放。Fitter改变外层Rect，脚本改变内层scale，不能合并成同一Rect驱动。横向布局取消ForceExpand，敌牌192×252、手牌168×252、装置494×210；横向装置/零件行不使用竖版卡框。
- [UI_PAGES] 主桌面为顶栏/角色敌牌反馈/三装置/手牌或备战。拆装页不透明且隐藏主桌面；三列各500×536，选项476×112，名字20/32高、正文17/54高。底栏汇总1140×192，完整规则可滚动，确认与清空独立右列。菜单保存/读取/重开/返回，Esc关拆装或开关菜单。QWE装置、1～5手牌、Space结束回合、回车确认焦点。InputManager仅新增SpellcraftSubmit(return/enter)，测试场景独用，原Submit含space必须保留原状，否则空格会双重执行。
- [UI_FEEDBACK] 预演tooltipScroll保留上次悬停结果，可移入右侧滚动，不再210字截断；行动/选目标刷新。assemblyScroll展示当前组合触发/完整效果/改装。日志七条单换行，每条最多27字符，保证新事件在184高框内。PlaceMessage按页放置：主桌面(32,601,1536,26)、拆装(32,649,1536,26)、其余(300,804,1000,64)，不能放回覆盖清空按钮的旧(300,824,1000,56)。菜单读取不同阶段时导航优先菜单，避免焦点穿透。
- [SAVE] Unity/ForgeSaveStore.cs：persistentDataPath/TestGameplay/spellcraft-v1.json，封装Version=2，SHA256、2MB、原子写/.bak。每个成功命令与退出自动保存；失败暂停自动覆盖，手动保存/重开恢复。规则指纹现排除Name/Description/SpriteIndex，改显示不使新版档失效。ForgeConfiguration.PreviousPresentation用隐藏TextAsset Configuration/PreviousPresentation.json还原本次改名前的53条显示记录，仅用于旧v1指纹兼容，不回退任何数值。旧v1仅接受完整当前或本次改名前的显示指纹，不泛化接收其他规则。Restart同步重建Engine与SaveStore。原card-table.json/workshop-*.json均不读写。
- [NAMES] 当前零件按功能叫手动启动/受伤触发/格挡触发/耗火花触发/回合开始/燃烧触发，电击器/护盾器/充能器/喷火器/冷冻器/吸血器/重型电击器/备用电池，无改装/全体作用/重复两次/增强输出；基础牌防御、全部放电替换旧显示名。历史文档及隐藏迁移JSON可能含旧名称，不是玩家当前显示；技术ID和素材文件名不应为清字样而改动。
- [BUGFIX_TARGET] 原View局部selectedEnemy与Core.SelectedEnemy分叉，回合开始自动电击可能打错目标，读档也不还原目标框。现Core.SelectEnemy(int)走Battle/RequireTarget候选事务；View.TargetRequested→Bootstrap.Complete(engine.SelectEnemy)→Render+自动保存。View仅用只读属性SelectedEnemy=>state.SelectedEnemy，已删局部可写字段与FirstLivingEnemy。目标框、出牌、手动与预演统一；不耗资源/随机。自动反击本来会在Activate改变核心目标，UI现在如实跟随。
- [BUGFIX_NAV] 原SetNavigationScope给inactive手牌槽设None，Render只在阶段变化时重设；开关菜单后结束回合补牌仍Battle，后排牌键盘不可达。现Render每次RefreshNavigation重设当前scope，仅阶段改变或焦点为空/隐藏/不可用/越出当前页时转移焦点，菜单和拆装优先。
- [BUGFIX_FEEDBACK] Render先隐藏战后桌面，Present再给隐藏敌牌写末击，下一场会播放旧数字；现卡/装置Present跳过!isActiveAndEnabled。ForgeCardView反馈原点只首次Initialize捕获，ResetFeedback在隐藏/结束/重初始化还原；防重开逐次上移。卡/装置OnDisable清hover后PaintSelection，保留真正selected、去掉旧悬停边框；装置恢复初始连接色。

## 资源与稳定GUID

- [ART_FILES] Spellcraft/Art/SpellcraftAtlas.png：1254方图、真实RGBA，4×4每格313.5。SpriteIDs21300000+2*i，自上到下：inventor/beetle/moth/golem/cannon/shield/battery/ember/frost/siphon/gauntlet/clock/breastplate/branch/bells/reactor。少量极低alpha残留如实留原像素。
- 背景SpellcraftWorkshop.png1672×941；框SpellcraftCardFrame.png1024×1536、中心alpha254/外沿0，边距L144 B480 R144 T216。Art/GenerationPrompts.md保留完整实际提示词和内置工具模式；manifest.json有sha12/尺寸/切片；无签名URL。
- [GUID] atlas cd2b4d319fad49cfba036409d8b3884e；background f99513b667db493b90054647f7d849a9；frame dc044df145cb4648be54e99e02711ee8；字体沿用Assets/TestGameplay/Art/NotoSansCJKsc-Regular.otf（e629089c69d84961a5cc29dd704f6301，file12800000，保留OFL）。配置234d8a2a518849f4a2659d4284b22d36，config脚本e0c62e14f8664fd89193a9a9c855fc06，Bootstrap6ed40fdc02b94b67ab19666af459d2f3，Viewad17e93c4c3d4aafbec6c9f4a334b528，Prefab97f0da7da9554d61991d8d43faf34a44，scene b460ea0a1bfe4bf495d4741780537ba8。场景本地ID8.5e12区间，源Prefab独立ID。
- [UI_GUID] ForgeDesktopLayout脚本d93c854365cf49c48a9da81d2fa944ec；PreviousPresentation TextAsset a61c4f3a4c1b4ea0b7d7d4f1803d34f9(file4900000)。最新Prefab源ForgeView组件fileID3123，Scene stripped引用已重生成匹配；旧3196/3105不可沿用。
- [CARD_GUID_FIX] 用户实际打开项目触发Safe Mode。Editor.log报ForgeView的10处CS0246找不到ForgeCardView；现有CardKKK.Spellcraft.Unity.rsp确实漏掉ForgeCardView.cs，同目录其他脚本正常。根因是该脚本meta GUID误写33位（旧324cdebeeeec4b57b72cd749c9710404e），已统一改为合法67bf3dc66cd64adab76c9d2efc0e717a，含Prefab30处引用和tools/build_spellcraft_ui.py常量。生成器的常量、ext()和meta()现都校验32位十六进制。之前仅查GUID能对应、没查长度，静态检查遗漏，不能推锅给缓存；不删Library、不乱加using。尚未重跑Unity/编译，等用户编辑器重新导入。

## 验证边界和接续

- [VERIFY] 本轮只静态审查，不启动编译器/Unity/测试。独立审查静读费用、深拷贝、牌总数、RNG、阶段/死亡、奖励二分匹配、预演、存档迁移/备份与入口，未发现其他可确定的正常操作错误；五类修复后又独立复核接线和状态路径。不能说测试通过或没有任何bug。冻结+重复两次+充能存在长期控制的平衡风险，未擅自削弱，需用户实际体验。
- [CLEANUP] 自动审批拒绝整目录Remove-Item -Recurse；已改用可审阅逐文本文件apply_patch，清理旧代码/场景/配置，不触碰旧图片和原牌桌。不要重复尝试批量递归删除。
- [FID] 旧工坊的fidA!=fidB根因未确证，新场景保持独立本地ID，不据此说旧断言已修好；不删Library。此前旧牌名/列表0宽和Noto32px框问题是历史，现新UI仍要检查真实字体框高。
- [NEXT] UI重构已写入本地。最新Prefab2124个YAML对象、场景14个，源View3123；静态解析5347个资产/包GUID，27个外部GUID引用均可解析。几何核对1280×720、1366×768、1920×897(用户截图比例)、1920×1080、1920×1200、2560×1080，卡行宽度均未超容器，全部Assets meta GUID为32位十六进制。独立复核发现的通知遮挡、七事件裁切、菜单读取焦点、完整规则入口和空格双提交五项已处理；最后移除预演开头64字截断和已无订阅的Unfocused事件。停止当前Play再进入测试玩法加载新Prefab。实际编译、渲染、交互与旧档读取仍由用户确认；静态审查不等于运行通过。未提交/推送。

## 原牌桌维护入口

- [OLD_CORE] Assets/CardTable/Core/BoardEngine纯C#候选事务；CardCatalog只读；Flip/Sort/Shuffle/Reorder/Pop/Tidy/Save完整条件编译边界，不能仅藏按钮或自动改编译符号。
- [OLD_UI] Assets/CardTable/Unity内CardTableDemo/CardTableController/TableInputRouter/CardTableView/TableCardView；Prefabs真实布局，Editor/CardTablePreview独立预览并恢复临时值。默认规则Assets/CardTable/Configuration/默认游戏配置.asset，菜单卡牌桌面/游戏规则配置。
- [OLD_SAVE] BoardSaveStore v1、SHA256、10MB/.bak；重构前18组+3000随机操作只属历史，不是当前验证。详细接口docs/tabletop-api.md、配置docs/configuration-guide.md。
