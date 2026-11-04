# 开发记忆

跨对话接续入口。实际代码与 Git 状态优先于旧说明，每次主要阶段与上下文接近80%前更新稳定锚点。

## 当前锚点

- [USER] 用户要求方便开发、可复用、易修改、规则可配置；不需要的功能应移除实现或完整条件编译，不能仅不调用。游戏、配置工具、README、Git提交与上传说明用中文。用户明确自行检查编译与Unity运行，不自动启动Unity、编译器或游戏测试。最新明确要求“我告诉你提交再提交”，此前持续提交/上传授权已被取代：平时只改本地，只有明确指令才提交或推送。
- [REPO] 本地 D:\wenjian\cardkkk，分支main；origin https://github.com/verdinkoehringsimgithu/CARDKKKKKK.git。此前HEAD892398c“接入现有卡面卡背和桌面素材”；2026-11-04用户已明确授权提交本批测试玩法、美术、策划和修复，本轮仅本地commit、不推送，新提交号与状态以git log/status为准。用户既有资源与Git身份保留。
- [SCOPE] Unity 2022.3.62f3c1，Windows单机鼠标。原自由牌桌保留；用户已授权按新工艺策划做独立“测试玩法”，并要求SpriteCook美术、额度不足才用image补且风格一致。当前新模块源码/资源已接入，不能称编译或Unity运行通过。
- [CONFIG] `Unity/Configuration` 提供 CardDefinition、CardTableConfiguration、TableRuleSettings、FeatureSelection、InitialPile。中文菜单 `卡牌桌面/游戏规则配置`，四页为桌面规则/卡牌目录/初始桌面/功能与资源。窗口默认读取场景启动器的实际配置引用，修改停止后下一次启动应用，不热改模型。默认配置 `Assets/CardTable/Configuration/默认游戏配置.asset`，GUID `a74fc672df4144e8a77723d488c1475b`。9种定义为archive/supply/tool各.3/.1/.2，初始3堆各3张，位置180/540/900,360。目录可新增定义并编辑中文名称/分类/序号/属性/插画。
- [VALIDATE] 配置验证检查ID/属性/引用/Windows存档文件名，并精确构造初始快照交Core.Restore检查几何、唯一归属、容量、当前堆叠规则；允许初始桌面为空。默认资产补齐仅创建缺文件，不覆盖设计者修改。规则窗口与Inspector使用中文枚举/属性/引用列表。
- [DEFINITION] `Core/CardCatalog.cs` 定义不可变CardDefinitionData与只读目录。Unity资产是静态定义唯一来源；Core保存深拷贝。`BoardEngine(rules,catalog=null)` 在生成/读取时解析静态名称/分类/序号，实例ID、FaceUp、当前Attributes保留。正常UI发牌用catalog.CreateCard获得定义默认属性。无catalog路径保留原导入/测试兼容。
- [RULES] 统一CanStack用于预览、提交、生成重叠、整理、Restore：SameFamily/SameDefinition/AnyCard；AllowStacking关闭时每堆单张。整堆/单张拖动权限独立。AutoMergeOnSpawn只处理生成区域重叠；AutoMergeOnTidy处理SnapDistance附近合堆。排序支持稳定升/降序或Ordinal名称升序。默认全功能启用。
- [MODULES] BoardEngine为partial，六组操作及独占helper已从单体真正迁出 `Core/Operations/BoardEngine.{Flip,Sort,Shuffle,Reorder,Pop,Tidy}.cs`。每个完整文件由CARDTABLE_DISABLE_*排除，同时检查运行Features。Save整类和Unity调用亦受CARDTABLE_DISABLE_SAVE。规则窗口按钮“应用功能裁剪到编译”仅显式点击时变更当前平台的七个自有符号，保留其他符号；工程级裁剪可能影响同平台其他配置，不自动导入时改设置。
- [CORE] 状态仍是候选副本统一校验后原子提交、同步主线程、独占拖拽。预览复用缓存索引，提交共用规划。TableGeometry空间哈希+精确矩形；FindFree最多20000候选；Tidy容量约束并查集+高度优先最短列布局，不保证全局最优，失败回滚。所有API文档已中文更新。
- [UI] 新结构：CardTableDemo薄启动器、CardTableController命令/选择、TableInputRouter快捷键、CardTableView呈现/牌池、TableActionButton按钮绑定、TableCardView牌面/动画。所有按钮与快捷键走Execute；成功模型Changed唯一刷新，取消/非法落点才额外归位。删除运行时整套UI生成器、MakeTemplate硬编码、重复刷新、玩家O键/手动重叠按钮、状态版本调试。自动避让核心仍保留。
- [PREFAB] `Prefabs/CardTableView.prefab` 为可修改的真实Canvas布局，`CardView.prefab` 为牌面；演示场景引用真实Prefab、默认配置与原Bootstrap脚本GUID。运行视图不使用ExecuteAlways，编辑预览归Editor独立逻辑。预览缓存真实文字/字体/桌面矩形原值，在保存、切换模式、重载、关闭前恢复，之后再释放临时字体与牌；不覆盖预览后手动改动。发牌列表显示目录全部定义。Default Play入口和构建场景保留。
- [CAMERA] 用户截图的“No cameras rendering”源于场景缺Camera、Overlay UI却仍能显示。已本地补背景相机：启用、MainCamera、Display0、全屏SolidColor、cullingMask0，只清背景；Canvas保持Overlay，根AudioListener仍唯一。没有启动Unity验证；停止Play并重开桌面场景才载入磁盘修改。本次不提交/推送。
- [ART] 用户要求现有美术先接上：Art/Assets/sprite_sheet.png保留GUID fe4b8f6afddd09c44a6e54fecb68558d，Multiple切棕面(166,674,68,109)/fileID21300000与棕背(54,145,68,109)/21300002，border4；Point/Clamp/无mipmap/无压缩。High Resolution/card_background.png GUID8cb3e03924b5a6548b28c86afabb4243转SingleSprite/border100。默认主题已引用三图、Table/CardColor白色保原图色，新增CardTextColor。源PNG未修改。Card目录还有独立空模板；展示海报和战斗样张不混用。没有物件插画/字体/音频，九定义Artwork保持空。详Art/README。
- [ART_VIEW] 新CardTableArt helper按Sprite.border设置Sliced/Simple，运行和Editor预览共用Surface应用；TableCardView有贴图时底色透明保留透明角，文字读Theme.CardTextColor。Editor ImageState缓存并恢复原图片/颜色/模式，与原字体/矩形清理一起执行，避免预览写进场景。仅静态检查，不启动Unity编译。
- [SAVE] BoardSaveStore保留格式v1、SHA256、10MB上限、同目录临时写/Flush/原子替换/.bak恢复；备份结构验证注入当前catalog，禁用Save功能直接Save/Load也拒绝。旧family.sample按旧Rank匹配迁移，默认映射1..3；旧演示额外发牌4..9需补定义映射，缺失整体拒绝且保留当前桌面，不虚称所有旧档自动兼容。实例属性/翻面/ID/牌序/位置保留，静态字段按新定义解析。
- [VERIFY] 本轮仅静态检查，不能报告编译/运行/测试通过。25脚本、7符号的128种组合扫描没有已裁剪方法残留引用；13个配置/Prefab/场景文件YAML可解析、内外部fileID与GUID有对应资产（含本地uGUI包）；CardTable无缺.meta、全Assets无GUID冲突。独立审查修正Editor对Unity.ugui的直接引用、唯一AudioListener、CanvasScaler Expand、预览保存恢复；最新预览已复核无新阻断。Tests/Core新增4组目录/迁移/堆叠/权限/排序场景源码，未执行；18组+3000随机操作通过只属于重构前历史版本。
- [DELIVERY] 2026-11-04用户明确说“提交git”，本批改动获本地提交授权；没有推送授权。助手没有主动运行本轮编译、游戏测试或Unity。用户首次实测暴露的布局问题已修，fid断言尚未由后续运行确认消失。
- [USER_STYLE] 用户明确否定上版策划与界面，认为太AI、说明字过多，要求用skill重做。已读humanizer、game-ui-design及其参考、game-design-theory。不要把长表格、数字核算或商店简介当成有趣设计的证据，回复简短直接。
- [DESIGN_REJECTED] 《余烬工坊》v0.1已被用户否定。十四天、两工人半日排班、三次相同修塔、H加减热条均停用；原策划和CSV/JSON已从活跃文件删除，历史在18a8b74。不要照旧案实施，也不要把其通关核算当作玩法成立。
- [DESIGN_CURRENT] docs/design/制作原型.md的8工艺已作为独立模块实施：快烧木4/人15/后台45→炭2灰1；封窑木3/人30/后台90→炭2焦油1；灰1土2人30装衬，普通熔铜炭2降1；助燃炭1油1后台30、炉衬不再减炭。完整开局造铜斧后取木一次完成；铜斧取木45→30，产4不变。无饥渴/漏水/固定截止。用户最新要求保留原版，不再替换或删除原牌桌操作。
- [DESIGN_BOUNDARY] 新测试模块不引用原牌桌程序集：自己的Core/Unity/Editor、数量库存/时钟/单窑/工艺/存档。原CardTable源码/Prefab/配置不改，原scene仅新增9000入口根（背景相机是之前已有本地改动）；BuildSettings只追加测试scene、保留原默认入口。测试模块没有发牌翻洗牌调序，与保留旧功能不冲突。
- [DESIGN_EVIDENCE] 参考记录.md核对热带岛Update6/22/29/39/正式版、森林合金、Stacklands岛屿QoL、WitchHand Nexus、密教作者文章、缺氧Automation和4张官方截图。核对开发者正文及原生公告ID，没有成功播放实机视频、没有亲自试玩。历史机制不当最新精确数值；本案配方是自定简化值。
- [DESIGN_CHECK] 纸面流程.md手算完整两路均435游戏分：AA+炉衬木投入9，AB助燃木投入8，含斧柄；灰盒送坩埚，5工艺全已知、4采集全开放、目标铜3，A/B只换库存。CSV是草表、未连Unity；JSON问题改为工艺取舍。独立新代理认为时钟/占用/解锁足以安排实现，复核修正灰盒阶段采集范围与虚假的“留焦油”提问。纸面可达不证明最优或好玩；无截止时分钟主要为比较指标，长期变化未证明。
- [UI_CLEANUP] 本轮真正删除Prefab中的顶部标题、常驻指南、counts统计、details占位组件，以及代码字段；短按钮、短选中信息，问号帮助默认关闭且按功能生成，Esc收起。正常成功不报长消息，可恢复错误4秒后收起；启动错误经ShowError持续显示。CardView加1px暗色Outline便于辨认同色牌边界。GUID/主题/动画/编辑预览保存恢复保留。两Prefab178/26对象的引用做过静态复核，未运行Unity或编译。outputs/桌面排版预览.png是按Prefab绘制的静态排版模拟，不是Unity运行截图。
- [NEXT] 完成本轮用户授权的本地提交；后续编译和Unity操作仍由用户检查，按具体反馈修复。没有新的明确指令时不自动提交或上传，不扩生存、天气、种植等范围。

- [TEST_ENTRY] Assets/TestGameplay/Unity/TestGameplayEntry.cs由原scene独立根9000装配，originalTableRoot1000；子Canvas入口Prefab c8dad487ad7c4e89a969d41ef55959d3。进入前停用旧根，Additive加载Scenes/TestGameplay.unity(guid980e6e81188b4bb79339541f05cc4fa2)，返回先停用/卸载testscene再恢复旧根，旧controller与牌位保留；entry本体不在旧根或被关Canvas下。原/测试事件系统与相机不同时启用。
- [TEST_CORE] Assets/TestGameplay/Core有WorkshopData/WorkshopEngine/WorkshopEngine.Validation与noEngineReferences asmdef。Engine唯一拥有Snapshot；DTO和Snapshot深拷贝；命令StartRecipe/Gather/WaitForNextCompletion/Restore/Restart统一候选校验提交；Changed通知隔离观察者异常，ObserverFailed由Bootstrap记Console。准备整次结算且占位；后台准备完成才开始；自动发主副产物；目标首次结算固定成绩。
- [TEST_CONFIG] Configuration/测试工坊配置.asset GUID364828e17c4549d9b186cb7c9c7f16e0，独立Items/Scenery/Recipes/Gathers/Scenarios、字体/色/插画；8工艺4采集。Scenarios为full/gray_a/gray_b，full默认；灰盒送坩埚，5工艺已知，目标铜3。菜单“测试玩法/选择玩法配置”，Inspector中文。CSV只策划对照不自动导入。
- [TEST_VIEW] Prefabs/WorkshopView.prefab GUID51c92a155d764a5092c172f9bd571220，rootGO1000/Rect1001/View1002/Canvas1003。真实序列化地点/工位/11库存槽/工艺行，编辑态显示full开局，运行接管池。拖料只筛工艺，明确按钮执行；所有命令走Bootstrap。Esc只View处理：取消拖动→清筛选→关详情→返回；新审查修ghost锚点为dragLayer.pivot，状态改变取消旧拖影。没有旧CardTable依赖。
- [TEST_ART] SpriteCook生成16物件同张4x4图并去底：raw4d8d901c-58e2-428d-8846-703aa766d1be、透明4500be7d-a135-5991-8d0f-25883f235a44；透明图集GUID765c257371394d98b96a811d33de0691，按行256格，Sprite21300000+2*i。新纸卡框参考图集生成raw31a583ad-b4eb-4c6c-8539-836f4f56029d、透明2d75ba86-4cfb-565f-acfa-f712c3fe60a1，GUID8ff72b8178c54ed79ec0e40b0e01f4f6，rect(188,70,648,886)、border64、PPU400。均已绑定配置及Prefab，无旧框引用。SpriteCook耗26额度（原40，余14）；未用image补。图源提示词/manifest在Art内，不存signedURL。已查看浅/深底预览。
- [TEST_SAVE] WorkshopSaveStore独立子目录persistentDataPath/TestGameplay/workshop-开局ID-v1.json，JsonUtility载荷、定义hash、SHA256、2MB上限、临时Flush/原子替换/.bak。备份旧主档前用临时Core引擎校验语义，坏主档不能覆盖好备份；首次恢复失败暂停退出自动保存，直至手动保存/重新开始。旧card-table.json不读写。游戏UI不显示IO内部路径，详情只记Console。
- [TEST_VERIFY] 未启动编译器、Unity、任何游戏测试。代理静态复核了435分路线、8工艺4采集/3开局、Scene/Prefab局部引用、原根与入口兄弟关系、独立asmdefs，修正存档备份/退出覆盖/Esc重复与拖影坐标问题。Noto CJK SC官方OFL字体独立导入，GUIDe629089c69d84961a5cc29dd704f6301/file12800000。最终静态整合已核对8个配置/场景/Prefab文件（1069对象视图Prefab）、全新资产meta、全Assets GUID无冲突、外部Sprite/fileID/字体引用、16图绑定和manifest摘要；无旧程序集引用。只属于静态核对。
- [TEST_UI_FIX] 2026-11-04用户截图：卡名数量全空、右工艺列表空，Console只有fidA != fidB。已静态定位并修Prefab：VerticalLayoutGroup4660的ChildControlWidth1/ForceExpandWidth0把无preferredWidth的工艺行压成0宽，改ForceExpandWidth1；Noto hhea1160/-288、units1000使23号行高33.304超过32，17卡heading/quantity高度改40，status54，artwork top59/high120留间距。TestGameplay.unity的14个本地ID改4200000000000+旧ID；带GUID的source/script/config fileID原样保留，与源Prefab无ID交集。跨文件同ID不必然非法，fid断言根因仍未确证；这项去重是具体修复候选，不能报告断言已实测消失。原牌桌/玩法规则/存档不改，未运行Unity或编译。

## 接续顺序

1. `git status`，读本文件和README，保留用户最新修改。
2. 按真实文件/代理进度完成WIP项，编译与Unity运行不要自行执行。
3. 完成后README简记本次变化与验证范围，更新本文件锚点；修改留本地，等待用户明确提交或上传指令。
