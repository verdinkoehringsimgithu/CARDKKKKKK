using System;
using System.Collections.Generic;
using System.Linq;
using CardKKK.IslandSurvival.Core;
using CardKKK.IslandSurvival.Unity;
using CardKKK.Tabletop.Unity;
using UnityEditor;
using UnityEngine;

namespace CardKKK.IslandSurvival.Editor
{
    [CustomEditor(typeof(SurvivalConfiguration))]
    public sealed class SurvivalConfigurationEditor : UnityEditor.Editor
    {
        private static readonly string[] Pages = { "时间与桌面", "卡牌与插画", "配方与动作", "自然与潮汐", "生物与性状", "身体与异常", "开局与目标" };
        private int page;
        private string result = "", search = "";
        private CardDefinition editingCard;
        private static readonly HashSet<string> PresentationFields = new HashSet<string> { "Name", "Family", "SortRank", "Kind", "StackLimit", "Footprint", "InitialDurability", "Structure", "Hardness" };
        private static readonly Dictionary<string, string> Labels = LabelsFrom(
            "Version=规则版本|TemporaryBalance=使用临时平衡参数|RealSecondsPerMinute=现实秒／游戏分钟|StartMinute=开局时刻（从00:00计分钟）|StartCapacity=初始占地容量|MaxMinutesPerAdvance=单次推进上限（游戏分钟）|Columns=整理列数|Rows=整理行数|Width=生产区宽度（逻辑像素）|Height=生产区高度（逻辑像素）|CellWidth=单格宽度（逻辑像素）|CellHeight=单格高度（逻辑像素）|Cards=卡牌机制规则|Recipes=配方与动作|Nodes=自然节点|Biology=生物规则|Traits=可选性状|Afflictions=异常规则|Start=初始物资|FirstTide=首日额外潮汐|TideFixed=每天固定潮汐|TideRandom=随机追加候选（等概率一种）|TidePeriodic=周期保底潮汐|Expansions=扩建阶段|Body=身体全局参数|Goal=稳定营地目标|Id=稳定ID|Name=显示名称|Description=说明|Family=分类|SortRank=排序|Kind=规则类型|Role=机制角色|ConditionRuleId=关联异常规则ID|WoundCard=独立伤口牌|StackLimit=堆叠上限|Footprint=占地单位|Nutrition=每份营养（饱食百分比点）|ShelfMinutes=保质期（游戏分钟，0为永久）|FuelValue=每份燃料值|OrganicWeight=未处理有机负荷权重|FoodSource=食物来源分组|DecayId=腐败变为|CountsFood=计入食物目标|Ability=工具能力|Hardness=工具硬度|InitialDurability=初始耐久|Repairable=允许修理|RepairLoss=维修损失最大耐久|RepairMinutes=维修标准工时（游戏分钟）|RepairToolId=不消耗的维修磨具|DepletedStatus=耐久耗尽状态|Salvage=拆解／拆除返还|BuildingCost=建造成本记录|Structure=初始结构耐久|DailyWear=每日06:00结构损耗|WorkWear=每周期结构损耗|InputSlots=输入材料堆槽|OutputSlots=输出材料堆槽|PlantSlots=种植槽|AdultSlots=成年槽|YoungSlots=幼体槽|StorageSlots=仓储材料堆槽|FuelCapacity=燃料上限|AshEveryFuel=每累计消耗燃料产灰|AshId=燃料灰烬定义|CarryingCapacity=区域有机处理承载C|LoadPower=区域负荷指数p|SpecialtyEfficiency=专用动作效率倍率|SpecialtyTags=专用动作标签|OrnamentRestBonus=装备后休息倍率增量|Inputs=输入组|Outputs=产出|TargetDefinitions=允许目标卡牌|RequiredRole=主要设备／目标角色|SecondaryRole=协同设备角色|RequiredAbility=要求工具能力|RequiredHardness=最低工具硬度|ToolWear=每周期刀具损耗|Minutes=标准人工／加工时间（游戏分钟）|Physical=体力强度W|Cognitive=认知负荷C|Danger=危险强度D|Fuel=每周期燃料消耗|Exposure=污染接触系数|Tags=动作功能标签|Human=占用唯一人力|Environmental=受区域有机负荷减速|InitialKnown=开局可见|TargetRequired=必须选择目标|Permanent=永久移除目标|RequiresCareMilestone=强采护理与普通采集前置|DiscoverMaterial=首次取得材料后可见|DiscoverMilestone=达成里程碑后可见|Care=护理方式|CareStrength=护理强度|CareDuration=护理持续时间（游戏分钟）|ExtraTraitCost=定向繁殖额外材料|AutomaticMinutes=人工设置后的自动时间（游戏分钟）|ExtraTraitMinutes=定向繁殖额外人工时间（游戏分钟）|RepairAmount=建筑修复结构量|AllowPartialInputs=允许不足上限的同种材料|ForcedGather=高危强行采集|DefinitionId=卡牌定义|Count=数量|TraitId=产物性状|Alternatives=替代材料（只择同种）|Consumed=开工消耗（关闭则占用）|CardId=节点卡牌|Initial=初始储量|Daily=每次补充|Capacity=储量上限|RefreshMinutes=刷新周期（游戏分钟）|Tide=读取当天潮汐包|EveryDays=每隔天数|Items=物资|Species=适用生物角色|DurationMultiplier=生长时间倍率|AddedOutputs=额外产物|MatureId=成年／成熟定义|SeedId=种苗／幼体定义|GrowMinutes=成长时间（游戏分钟）|ProductionMinutes=生产时间（游戏分钟）|MatureHoldMinutes=成熟保留时间（游戏分钟，0为无限）|FeedWindowMinutes=喂养窗口（游戏分钟）|DivisionCooldownMinutes=分株停产时间（游戏分钟）|Feed=每周期／喂养窗口投入|Production=正常产出|Death=死亡／腐烂产物|BreedMinimumHealth=亲本最低健康（0～100）|HealthGain=喂养成功每窗口健康恢复|HealthLoss=未喂养每窗口健康损失|StoredLimit=株内储叶上限|ProductionItem=株内储存产物|Growth=自行发展率／游戏小时|Clearance=基础消退率／游戏小时|FunctionExponent=机能对清除的指数|StructureWeight=结构通道权重|FunctionWeight=机能通道权重|VitalWeight=生命通道权重|StructureThreshold=结构影响阈值|FunctionThreshold=机能影响阈值|VitalThreshold=生命影响阈值|CurvePower=通道曲线指数|CognitiveMultiplier=认知负荷倍率|WoundRelated=绑定独立伤口|WorkTags=额外障碍适用动作|X=初始横坐标（逻辑像素）|Y=初始纵坐标（逻辑像素）|Milestone=前置里程碑|Cost=扩建材料|HealthWeight=健康几何平均结构权重ω|FoodRate=饱食消耗系数／游戏小时|MindRate=精神消耗系数／游戏小时|PhysicalPower=体力负荷指数α|CognitivePower=认知负荷指数β|DangerPower=危险负荷指数γ|DangerMindWeight=危险精神负荷权重ρ|StructureEfficiency=局部结构效率权重λI|FunctionEfficiency=全身机能效率权重λR|MindEfficiency=精神效率权重λs|OtherEfficiency=额外障碍效率权重λB|EfficiencyMin=效率下限|EfficiencyMax=效率上限|EfficiencyPower=效率缺口指数|RepairRate=组织修复系数／游戏小时|StructureDamageRate=异常结构损伤系数／游戏小时|NutritionHalf=营养支持半饱和值Kf|FunctionFloor=营养缓冲机能下限r0|FunctionStructurePower=结构对机能目标指数b|WorsenHours=机能恶化时间常数（游戏小时）|RecoverHours=机能恢复时间常数（游戏小时）|VitalRecovery=生命恢复系数／游戏小时|VitalDamage=异常生命损伤系数／游戏小时|RestRate=休息精神恢复系数／游戏小时|HungerInput=未付营养映射异常输入系数|PollutionHalf=污染饱和半量Kw|ExposureScale=暴露输入系数|RiskRate=累计危险事件系数／游戏小时|ForcedInjuryChance=强采事件危险参数|InjuryStructure=事件结构伤害（归一化）|InjuryVital=事件生命伤害（归一化）|GelStrength=凝胶护理强度|LeafStrength=原叶护理强度|CleanAmount=清洁增加量|DirtRate=接触污染积累率|SafeEventMinutes=同源高危恢复窗口（游戏分钟）|FoodWarning=低饱食暂停阈值|VitalWarning=低生命暂停阈值|TreatmentMax=治疗倍率上限|TreatmentHalf=治疗曲线半效强度|ExposureWarning=累计暴露预警阈值|MinimumDay=最早检查天数|WindowDays=连续统计天数|CompostCount=窗口内最少堆肥次数|MinimumHealth=最低健康（归一化）|MinimumVital=最低生命（归一化）|MinimumMind=最低精神（归一化）|DailyFood=每日净新增营养|StoredFood=合格可食库存营养|MinimumShelfMinutes=库存最短剩余保质期（游戏分钟）|SourceCount=窗口内最少食物来源种类|MaxWaste=每日未处理有机废物上限|CompostRecipeId=普通堆肥配方|AcceleratedCompostId=加速堆肥配方|BoneKnifeId=要求制作的替代刀具|WasteId=统计废物定义");
        private static readonly Dictionary<string, string> EnumLabels = LabelsFrom(
            "None=无|Material=材料|Tool=工具|Facility=设施|Area=区域|Creature=生物|Condition=状态牌|NaturalNode=自然节点|Fire=篝火|Garden=花圃|Pen=围栏|Compost=堆肥区|Storage=仓储|DryingRack=晾晒架|AloePlant=种植芦荟|YamPlant=芋株|AdultSnail=成年养殖螺|YoungSnail=幼螺|Ornament=饰品|Craft=制作／加工|Gather=节点采集|Destroy=破坏／搬离|Repair=修理|Salvage=工具拆解|Demolish=建筑拆除|Rest=休息|Plant=种植|Harvest=采收|Divide=分株|Breed=繁殖|SelectSeed=定向选育|Care=伤口护理|Clean=废料清理|Expand=生产区扩建|Dressing=使用／更换敷料|Gel=凝胶护理|Leaf=芦荟叶护理|Bleeding=流血|Infection=局部感染|Pain=疼痛|Irritation=环境刺激|Hunger=饥饿性衰弱");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SurvivalConfiguration config = (SurvivalConfiguration)target;
            EditorGUILayout.HelpBox("按完整设计1.2配置。未定数值均为临时值；修改后保存资产，重新进入或开始新游戏生效。规则改变的旧档由读档校验决定兼容性，拒读时保留文件。", MessageType.Info);
            page = GUILayout.SelectionGrid(page, Pages, 3);
            EditorGUILayout.Space();
            switch (page)
            {
                case 0:
                    Hint("一天1440游戏分钟。0.5现实秒/游戏分钟即12现实分钟/天。24容量采用6×4、32容量采用8×4；40容量采用8×6共48个几何候选位置，逻辑占地上限仍为40，便于排满2×2区域。自然节点占地0。安全结算顺序固定，不做配置开关。");
                    Fields("Version", "TemporaryBalance", "RealSecondsPerMinute", "StartMinute", "StartCapacity", "MaxMinutesPerAdvance", "Columns", "Rows", "Width", "Height", "CellWidth", "CellHeight");
                    break;
                case 1:
                    Hint("名称、分类、排序、初始属性和插画只改下列CardDefinition资产。机制规则中的对应字段会在加载时桥接，不重复维护。浮点曲线、槽位、工具工艺属于机制规则。");
                    DrawCatalog(config);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("ContentNotes"), new GUIContent("来源与端点审查注释"), true);
                    EditorGUILayout.Space();
                    search = EditorGUILayout.TextField("筛选卡牌ID或名称", search);
                    Draw(serializedObject.FindProperty("Rules.Cards"), config);
                    break;
                case 2:
                    Hint("时长单位为游戏分钟；自动等待不占人力。W/C/D及风险均为临时值。输入的替代组每次择同种，非消耗项仅占用。输出、设备、工具损耗与实际操作共用核心规则。");
                    search = EditorGUILayout.TextField("筛选配方ID或名称", search);
                    Draw(serializedObject.FindProperty("Rules.Recipes"), config);
                    break;
                case 3:
                    Hint("节点日刷新以06:00为界。潮汐先合并每天固定、周期保底和首日额外，再等概率追加一个候选；未收包次日替换。初始储量不得超过上限。");
                    Fields("Nodes", "FirstTide", "TideFixed", "TideRandom", "TidePeriodic");
                    break;
                case 4:
                    Hint("周期采用游戏分钟。生物健康使用0～100，与玩家I/R/V模型分离；每个角色只能一套生物规则。喂养窗口按时间运行，不随减产延长。性状每株/只限一个。");
                    Fields("Biology", "Traits");
                    break;
                case 5:
                    Hint("玩家状态归一化0～1；H=100·I^ω·R^(1−ω)。身体系数、异常强度/阈值/权重为临时值。只有人工劳动消耗饱食和精神；异常/事件进入损伤通道。率按游戏小时，不按现实帧。");
                    Fields("Body", "Afflictions");
                    break;
                case 6:
                    Hint("开局第1天06:00暂停。目标最早第31天06:00检查最近连续3天；口粮不计稳定营地食物。扩建按列表顺序读取，每阶段须已有里程碑。");
                    Fields("Start", "Expansions", "Goal");
                    break;
            }
            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed) result = "";
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("检查配置"))
                {
                    result = config.CheckConfiguration();
                    if (string.IsNullOrEmpty(result)) result = "静态配置检查未发现错误；实际编译和运行由Unity验证。";
                }
                if (GUILayout.Button("保存配置资产"))
                {
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                    result = "已保存资产。重新进入或开始新游戏后加载。";
                }
                if (GUILayout.Button("查看局部关系")) SurvivalRelationsWindow.Open(config);
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        private void Fields(params string[] names)
        {
            SurvivalConfiguration config = (SurvivalConfiguration)target;
            foreach (string name in names) Draw(serializedObject.FindProperty("Rules." + name), config);
        }
        private static void Hint(string message) { EditorGUILayout.HelpBox(message, MessageType.None); }
        private void DrawCatalog(SurvivalConfiguration config)
        {
            SerializedProperty cards = serializedObject.FindProperty("Cards");
            EditorGUILayout.PropertyField(cards, new GUIContent("卡牌资产目录"), false);
            if (cards.isExpanded)
            {
                EditorGUI.indentLevel++;
                cards.arraySize = Math.Max(0, EditorGUILayout.IntField("卡牌数量", cards.arraySize));
                for (int i = 0; i < cards.arraySize; i++)
                {
                    SerializedProperty item = cards.GetArrayElementAtIndex(i);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(item, new GUIContent("卡牌 " + (i + 1)));
                        if (GUILayout.Button("编辑", GUILayout.Width(48))) editingCard = item.objectReferenceValue as CardDefinition;
                    }
                }
                EditorGUI.indentLevel--;
            }
            if (editingCard == null) return;
            EditorGUILayout.LabelField("卡牌资产：" + editingCard.DisplayName, EditorStyles.boldLabel);
            var cardObject = new SerializedObject(editingCard);
            cardObject.Update();
            foreach (string field in new[] { "Id", "DisplayName", "Family", "SortRank", "Artwork" })
            {
                string label = field == "Id" ? "稳定ID（存档引用）" : field == "DisplayName" ? "显示名称" : field == "Family" ? "分类" : field == "SortRank" ? "排序" : "插画";
                SerializedProperty property = cardObject.FindProperty(field);
                if (field == "Family")
                {
                    string[] families = { "材料", "工具", "设施", "区域", "生物", "状态" };
                    int current = Array.IndexOf(families, property.stringValue);
                    int index = EditorGUILayout.Popup(label, Math.Max(current, 0), families);
                    if (current != index) property.stringValue = families[index];
                }
                else EditorGUILayout.PropertyField(property, new GUIContent(label));
            }
            SerializedProperty attributes = cardObject.FindProperty("Attributes");
            Hint("初始属性：堆叠上限≥1，占地单位≥0，初始耐久/结构耐久/硬度≥0。所有卡均保留五项；属性键不可重名。无插画时显示正常文字卡。");
            for (int i = 0; i < attributes.arraySize; i++)
            {
                SerializedProperty attribute = attributes.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(attribute.FindPropertyRelative("Value"), new GUIContent(attribute.FindPropertyRelative("Key").stringValue));
            }
            if (GUILayout.Button("补齐五项初始属性"))
            {
                string[] keys = { SurvivalConfiguration.StackAttribute, SurvivalConfiguration.FootprintAttribute, SurvivalConfiguration.DurabilityAttribute, SurvivalConfiguration.StructureAttribute, SurvivalConfiguration.HardnessAttribute };
                foreach (string key in keys)
                {
                    bool exists = false;
                    for (int i = 0; i < attributes.arraySize; i++) exists |= attributes.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue == key;
                    if (exists) continue;
                    int index = attributes.arraySize++;
                    SerializedProperty attribute = attributes.GetArrayElementAtIndex(index);
                    attribute.FindPropertyRelative("Key").stringValue = key;
                    attribute.FindPropertyRelative("Value").intValue = key == SurvivalConfiguration.StackAttribute ? (editingCard.Family == "材料" ? 20 : 1) : key == SurvivalConfiguration.FootprintAttribute ? (editingCard.Family == "区域" ? 4 : editingCard.Family == "状态" ? 0 : 1) : 0;
                }
            }
            if (cardObject.ApplyModifiedProperties()) { EditorUtility.SetDirty(editingCard); result = ""; }
            if (GUILayout.Button("在项目中定位卡牌资产")) EditorGUIUtility.PingObject(editingCard);
        }

        private void Draw(SerializedProperty property, SurvivalConfiguration config)
        {
            if (property == null) return;
            bool cardRule = property.propertyPath.StartsWith("Rules.Cards.Array.data[", StringComparison.Ordinal) && property.propertyPath.Split('.').Length == 5;
            if (cardRule && PresentationFields.Contains(property.name)) return;
            if (cardRule && new[] { "InputSlots", "OutputSlots", "PlantSlots", "AdultSlots", "YoungSlots", "StorageSlots", "DailyWear", "WorkWear", "FuelCapacity", "AshEveryFuel", "AshId", "CarryingCapacity", "LoadPower" }.Contains(property.name))
            {
                string parent = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.'));
                string id = serializedObject.FindProperty(parent + ".Id").stringValue;
                CardDefinition card = config.FindCard(id);
                if (card != null && card.Family != "设施" && card.Family != "区域") return;
            }
            string label = Label(property.name);
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label + "（" + property.arraySize + "）", true);
                if (!property.isExpanded) return;
                EditorGUI.indentLevel++;
                int size = EditorGUILayout.IntField("条目数量", property.arraySize);
                if (size >= 0 && size != property.arraySize) property.arraySize = size;
                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty item = property.GetArrayElementAtIndex(i);
                    if ((property.name == "Recipes" || property.name == "Cards") && !Matches(item, config)) continue;
                    Draw(item, config);
                }
                EditorGUI.indentLevel--;
                return;
            }
            if (property.propertyType == SerializedPropertyType.Generic)
            {
                SerializedProperty id = property.FindPropertyRelative("Id");
                SerializedProperty name = property.FindPropertyRelative("Name");
                if (id != null)
                {
                    CardDefinition card = config.FindCard(id.stringValue);
                    label = (card != null ? card.DisplayName : name != null ? name.stringValue : "条目") + "  [" + id.stringValue + "]";
                }
                else if (property.name.StartsWith("data[", StringComparison.Ordinal)) label = "条目 " + (ArrayIndex(property) + 1);
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label, true);
                if (!property.isExpanded) return;
                EditorGUI.indentLevel++;
                SerializedProperty end = property.GetEndProperty(), child = property.Copy();
                if (child.NextVisible(true)) do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    Draw(child, config);
                } while (child.NextVisible(false));
                EditorGUI.indentLevel--;
                return;
            }
            var content = new GUIContent(label, Tooltip(property));
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                if (property.enumNames.Length == 7 && property.enumNames[1] == "Hand")
                    property.intValue = EditorGUILayout.MaskField(content, property.intValue, new[] { "手工", "重体力", "采集", "切削", "护理", "休息" });
                else
                {
                    string[] options = new string[property.enumNames.Length];
                    for (int i = 0; i < options.Length; i++) options[i] = property.name == "Care" && property.enumNames[i] == "Clean" ? "基础清洁" : EnumLabels.TryGetValue(property.enumNames[i], out string translated) ? translated : property.enumNames[i];
                    property.enumValueIndex = EditorGUILayout.Popup(content, property.enumValueIndex, options);
                }
            }
            else if (IsCardReference(property)) DrawCardReference(property, content, config);
            else if (property.propertyType == SerializedPropertyType.String && property.name == "Description")
            {
                EditorGUILayout.LabelField(content);
                property.stringValue = EditorGUILayout.TextArea(property.stringValue, GUILayout.MinHeight(40));
            }
            else EditorGUILayout.PropertyField(property, content, false);
        }
        private bool Matches(SerializedProperty item, SurvivalConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            string id = item.FindPropertyRelative("Id")?.stringValue ?? "";
            string name = config.FindCard(id)?.DisplayName ?? item.FindPropertyRelative("Name")?.stringValue ?? "";
            return id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static int ArrayIndex(SerializedProperty property)
        {
            int index = property.name.IndexOf('['), end = property.name.IndexOf(']');
            return index >= 0 && end > index && int.TryParse(property.name.Substring(index + 1, end - index - 1), out int number) ? number : 0;
        }
        private static bool IsCardReference(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String) return false;
            switch (property.name)
            {
                case "DefinitionId": case "CardId": case "MatureId": case "SeedId": case "DecayId": case "RepairToolId": case "AshId": case "ProductionItem": case "BoneKnifeId": case "WasteId": return true;
            }
            return property.propertyPath.Contains(".Alternatives.Array.data[") || property.propertyPath.Contains(".TargetDefinitions.Array.data[");
        }
        private static void DrawCardReference(SerializedProperty property, GUIContent label, SurvivalConfiguration config)
        {
            var ids = new List<string> { "" };
            var labels = new List<string> { "无" };
            foreach (CardDefinition card in config.Cards)
            {
                if (card == null) continue;
                ids.Add(card.Id); labels.Add(card.DisplayName + " [" + card.Id + "]");
            }
            int current = ids.IndexOf(property.stringValue);
            if (current < 0) { ids.Add(property.stringValue); labels.Add("失效引用：" + property.stringValue); current = ids.Count - 1; }
            int selected = EditorGUILayout.Popup(label, current, labels.ToArray());
            property.stringValue = ids[selected];
        }
        private static string Tooltip(SerializedProperty property)
        {
            string key = property.name;
            if (key.EndsWith("Threshold", StringComparison.Ordinal)) return "范围0≤阈值<1；超过该值才进入相应损伤通道。默认临时值见资产。";
            if (key.EndsWith("Weight", StringComparison.Ordinal) || key.StartsWith("Minimum", StringComparison.Ordinal) && key != "MinimumDay" && key != "MinimumShelfMinutes" || key.EndsWith("Warning", StringComparison.Ordinal)) return "归一化值，通常0～1；默认临时值见资产。检查配置会拒绝非法范围。";
            if (key.Contains("Minutes")) return "单位：游戏分钟。持续时间应大于0；0仅用于不启用该周期或永久保留。默认值见资产。";
            if (key == "DiscoverMaterial") return "卡牌稳定ID；多个可发现来源用 | 分隔，首次取得任意一种即可。空值表示不使用材料发现。";
            if (key == "DiscoverMilestone" || key == "Milestone") return "已实现的里程碑：first_harvest（首次人工收获）、first_breed（首次繁殖）。";
            if (key == "Ability" || key == "RequiredAbility") return "当前能力为cut（切割）；空值表示无工具条件。新能力需要程序实现。";
            if (key == "Id") return "稳定引用ID；已使用的ID不随显示名称修改。修改会影响配方引用与旧档兼容。";
            return "以当前资产值为默认；标记临时平衡时数值待标定。数量不得为负，材料/对象引用须存在。";
        }
        private static string Label(string name) { return Labels.TryGetValue(name, out string value) ? value : name.StartsWith("data[", StringComparison.Ordinal) ? "卡牌定义" : name; }
        private static Dictionary<string, string> LabelsFrom(string text)
        {
            var result = new Dictionary<string, string>();
            foreach (string pair in text.Split('|')) { int at = pair.IndexOf('='); if (at > 0) result[pair.Substring(0, at)] = pair.Substring(at + 1); }
            return result;
        }
    }

    public sealed class SurvivalConfigurationWindow : EditorWindow
    {
        private SurvivalConfiguration configuration;
        private UnityEditor.Editor inspector;
        private Vector2 scroll;
        [MenuItem("孤岛生存/旧版营地/规则配置")]
        public static void Open()
        {
            var window = GetWindow<SurvivalConfigurationWindow>("孤岛生存规则");
            window.minSize = new Vector2(720, 560);
            window.configuration = AssetDatabase.LoadAssetAtPath<SurvivalConfiguration>("Assets/IslandSurvival/Configuration/孤岛生存规则.asset");
            window.Show();
        }
        private void OnGUI()
        {
            var next = (SurvivalConfiguration)EditorGUILayout.ObjectField("生存规则资产", configuration, typeof(SurvivalConfiguration), false);
            if (next != configuration) { configuration = next; if (inspector != null) DestroyImmediate(inspector); inspector = null; }
            if (configuration == null) { EditorGUILayout.HelpBox("选择已有规则资产，或通过项目右键 创建→孤岛生存→生存规则 新建。", MessageType.Info); return; }
            if (inspector == null || inspector.target != configuration) { if (inspector != null) DestroyImmediate(inspector); inspector = UnityEditor.Editor.CreateEditor(configuration); }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
        private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
    }

    public sealed class SurvivalRelationsWindow : EditorWindow
    {
        private SurvivalConfiguration configuration;
        private string selected = "";
        private Vector2 scroll;
        public static void Open(SurvivalConfiguration config)
        {
            var window = GetWindow<SurvivalRelationsWindow>("孤岛局部关系");
            window.configuration = config;
            window.minSize = new Vector2(650, 420);
            window.Show();
        }
        private void OnGUI()
        {
            configuration = (SurvivalConfiguration)EditorGUILayout.ObjectField("规则资产", configuration, typeof(SurvivalConfiguration), false);
            if (configuration == null) return;
            var cards = configuration.Cards;
            var ids = new List<string>(); var names = new List<string>();
            foreach (CardDefinition card in cards) if (card != null) { ids.Add(card.Id); names.Add(card.DisplayName + " [" + card.Id + "]"); }
            if (ids.Count == 0) return;
            int index = Math.Max(0, ids.IndexOf(selected));
            selected = ids[EditorGUILayout.Popup("关系中心", index, names.ToArray())];
            EditorGUILayout.HelpBox("只展示选中卡牌前后各一层的已配置工艺。点击配方可定位规则资产，输入/输出在规则配置的配方页编辑；此视图不改写数据。", MessageType.None);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (RecipeRule recipe in configuration.Rules.Recipes)
            {
                bool relevant = recipe.TargetDefinitions.Contains(selected);
                foreach (Ingredient input in recipe.Inputs) relevant |= input.Alternatives.Contains(selected);
                foreach (Amount output in recipe.Outputs) relevant |= output.DefinitionId == selected;
                if (!relevant) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (GUILayout.Button(recipe.Name + " [" + recipe.Id + "]", EditorStyles.boldLabel)) { Selection.activeObject = configuration; EditorGUIUtility.PingObject(configuration); }
                    var inputLabels = new List<string>();
                    foreach (Ingredient ingredient in recipe.Inputs)
                    {
                        var alternatives = new List<string>();
                        foreach (string id in ingredient.Alternatives) alternatives.Add(configuration.FindCard(id)?.DisplayName ?? id);
                        inputLabels.Add(string.Join(" 或 ", alternatives) + "×" + ingredient.Count + (ingredient.Consumed ? "" : "（占用）"));
                    }
                    var outputLabels = new List<string>();
                    foreach (Amount amount in recipe.Outputs) outputLabels.Add((configuration.FindCard(amount.DefinitionId)?.DisplayName ?? amount.DefinitionId) + "×" + amount.Count);
                    EditorGUILayout.LabelField(string.Join(" + ", inputLabels) + " → " + string.Join(" + ", outputLabels), EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField((recipe.Human ? "人力 " : "自动 ") + recipe.Minutes + "游戏分钟；耐久损耗 " + recipe.ToolWear + "；燃料 " + recipe.Fuel, EditorStyles.wordWrappedLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
