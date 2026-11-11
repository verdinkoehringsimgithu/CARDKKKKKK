using System;
using System.Collections.Generic;
using System.Linq;
using CardKKK.IslandSurvival.Core;
using CardKKK.IslandSurvival.Unity;
using UnityEditor;
using UnityEngine;

namespace CardKKK.IslandSurvival.Editor
{
    /// <summary>读取真实配置；回归只由窗口按钮触发，使用与游玩及存档隔离的核心实例。</summary>
    public sealed class SurvivalVerificationWindow : EditorWindow
    {
        [SerializeField] private SurvivalConfiguration configuration;
        private SurvivalDefinition rules;
        private string configurationError = "", loadedAt = "";
        private int page, recipeIndex;
        private int[] alternativeChoices = Array.Empty<int>(), inputCounts = Array.Empty<int>();
        private bool includeTraitCost;
        private Vector2 scroll;
        private readonly CaseResult[] results = new CaseResult[5];
        private static readonly string[] CaseNames =
        {
            "正常等待不扣饱食精神、不凭空生病",
            "失败开工不改变状态、随机数和实例序号",
            "预览不提交，开始结果与预览一致",
            "取消自动加工保留当前轮费用并释放预留",
            "相同游戏分钟在1倍与3倍路径一致"
        };
        private static readonly string[] CaseDescriptions =
        {
            "用当前配置的新开局独立推进60游戏分钟；检查饱食、精神、I/R/V和伤口、异常。",
            "保留自然节点，移除R26消耗材料后尝试真实开工；要求失败并完整保留此前快照。",
            "为R01建立独立输入；用同一实例和参数预览、开工，比较扣料、任务、预留与随机状态。",
            "为R07投料和装燃料；开始后取消，再推进超过原周期，检查原料、燃料、产物和预留。",
            "相同种子和R26命令；分别以1分钟、3分钟批次推进180分钟，比较完整核心快照。"
        };

        [MenuItem("孤岛生存/历史演示/营地版/验收与收支工具")]
        private static void Open()
        {
            var window = GetWindow<SurvivalVerificationWindow>("孤岛验收与收支");
            window.minSize = new Vector2(800, 620);
            if (Selection.activeObject is SurvivalConfiguration selected) window.configuration = selected;
            window.LoadRules();
            window.Show();
        }

        private void OnEnable()
        {
            if (configuration == null)
            {
                string first = AssetDatabase.FindAssets("t:SurvivalConfiguration").OrderBy(g => AssetDatabase.GUIDToAssetPath(g), StringComparer.Ordinal).FirstOrDefault();
                if (!string.IsNullOrEmpty(first)) configuration = AssetDatabase.LoadAssetAtPath<SurvivalConfiguration>(AssetDatabase.GUIDToAssetPath(first));
            }
            LoadRules();
        }

        private void LoadRules()
        {
            rules = null; configurationError = "";
            try
            {
                if (configuration == null) { configurationError = "请选择孤岛生存规则资产。"; return; }
                SurvivalDefinition loaded = configuration.BuildDefinition();
                string error = SurvivalEngine.ValidateDefinition(loaded);
                if (!string.IsNullOrEmpty(error)) { configurationError = error; return; }
                rules = loaded;
                recipeIndex = Mathf.Clamp(recipeIndex, 0, Math.Max(0, rules.Recipes.Count - 1));
                ResetChoices();
                loadedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception exception) { configurationError = exception.GetType().Name + "：" + exception.Message; }
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            var selected = (SurvivalConfiguration)EditorGUILayout.ObjectField("规则资产", configuration, typeof(SurvivalConfiguration), false);
            if (EditorGUI.EndChangeCheck()) { configuration = selected; LoadRules(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新读取所选配置", GUILayout.Width(150))) LoadRules();
                if (configuration != null) EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(configuration), GUILayout.Height(19));
            }
            if (rules == null)
            {
                EditorGUILayout.HelpBox(configurationError, MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField("配置版本 " + rules.Version + (rules.TemporaryBalance ? " · 当前使用临时数值" : " · 当前未标记临时数值") + " · 读取于 " + loadedAt, EditorStyles.miniLabel);
            page = GUILayout.Toolbar(page, new[] { "当前配方收支", "由你执行核心回归" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            try { if (page == 0) DrawBalance(); else DrawVerification(); }
            catch (Exception exception) { EditorGUILayout.HelpBox("窗口读取失败：" + exception.GetType().Name + "：" + exception.Message, MessageType.Error); }
            EditorGUILayout.EndScrollView();
        }

        private void ResetChoices()
        {
            if (rules == null || rules.Recipes.Count == 0) return;
            RecipeRule recipe = rules.Recipes[recipeIndex];
            alternativeChoices = new int[recipe.Inputs.Count];
            inputCounts = recipe.Inputs.Select(i => i.Count).ToArray();
            includeTraitCost = false;
        }

        private CardRule Card(string id) => rules.Cards.Find(c => c.Id == id);
        private string Name(string id) => Card(id)?.Name ?? id;

        private void DrawBalance()
        {
            EditorGUILayout.HelpBox("这里显示一个正常周期的局部配方收支。使用当前配置的营养值和输入输出，自动等待单列；没有叠加前置采集、制具、种植、维护或风险成本，不能作为全产业链净收益。修改资产后请重新读取。", MessageType.Info);
            string[] labels = rules.Recipes.Select(r => r.Id + " · " + r.Name).ToArray();
            int choice = EditorGUILayout.Popup("工艺", recipeIndex, labels);
            if (choice != recipeIndex) { recipeIndex = choice; ResetChoices(); }
            RecipeRule recipe = rules.Recipes[recipeIndex];
            if (alternativeChoices.Length != recipe.Inputs.Count) ResetChoices();
            if (!string.IsNullOrEmpty(recipe.Description)) EditorGUILayout.LabelField(recipe.Description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("输入选择", EditorStyles.boldLabel);
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                Ingredient input = recipe.Inputs[i];
                alternativeChoices[i] = Mathf.Clamp(alternativeChoices[i], 0, input.Alternatives.Count - 1);
                alternativeChoices[i] = EditorGUILayout.Popup("第" + (i + 1) + "组" + (input.Consumed ? "（消耗）" : "（占用不消耗）"), alternativeChoices[i], input.Alternatives.Select(Name).ToArray());
                if (recipe.AllowPartialInputs && input.Consumed)
                    inputCounts[i] = EditorGUILayout.IntSlider("本次计划数量", inputCounts[i], 1, input.Count);
                else { inputCounts[i] = input.Count; EditorGUILayout.LabelField("数量", input.Count.ToString()); }
                double minimum = input.Alternatives.Min(id => Card(id).Nutrition) * (recipe.AllowPartialInputs && input.Consumed ? 1 : input.Count);
                double maximum = input.Alternatives.Max(id => Card(id).Nutrition) * input.Count;
                if (input.Consumed) EditorGUILayout.LabelField("该组投入营养区间", Number(minimum) + " ～ " + Number(maximum));
            }
            if (recipe.Inputs.Count == 0) EditorGUILayout.LabelField("无配方材料输入。目标实例的动态成本在下方边界中说明。");
            if (recipe.ExtraTraitCost.Count > 0 || recipe.ExtraTraitMinutes > 0)
                includeTraitCost = EditorGUILayout.Toggle("计入定向选育附加投入", includeTraitCost);

            var consumed = new Dictionary<string, int>(StringComparer.Ordinal);
            var outputs = new Dictionary<string, int>(StringComparer.Ordinal);
            var held = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                Ingredient input = recipe.Inputs[i];
                Add(input.Consumed ? consumed : held, input.Alternatives[alternativeChoices[i]], inputCounts[i]);
            }
            if (includeTraitCost) foreach (Amount extra in recipe.ExtraTraitCost) Add(consumed, extra.DefinitionId, extra.Count);
            foreach (Amount output in recipe.Outputs) Add(outputs, output.DefinitionId, output.Count);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("材料向量（当前选择）", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            { GUILayout.Label("材料", GUILayout.MinWidth(200)); GUILayout.Label("消耗", GUILayout.Width(70)); GUILayout.Label("产出", GUILayout.Width(70)); GUILayout.Label("净变化", GUILayout.Width(80)); }
            foreach (string id in consumed.Keys.Concat(outputs.Keys).Distinct().Where(id => Card(id).Kind == CardKind.Material).OrderBy(id => Card(id).SortRank).ThenBy(id => id, StringComparer.Ordinal))
            {
                int input = Value(consumed, id), output = Value(outputs, id);
                using (new EditorGUILayout.HorizontalScope())
                { GUILayout.Label(Name(id), GUILayout.MinWidth(200)); GUILayout.Label(input.ToString(), GUILayout.Width(70)); GUILayout.Label(output.ToString(), GUILayout.Width(70)); GUILayout.Label(Signed(output - input), GUILayout.Width(80)); }
            }
            foreach (var entry in held) EditorGUILayout.LabelField("占用不消耗", Name(entry.Key) + " × " + entry.Value);
            string other = string.Join("、", outputs.Where(p => Card(p.Key).Kind != CardKind.Material).Select(p => Name(p.Key) + " × " + p.Value));
            if (other != "") EditorGUILayout.LabelField("工具、设施或生物产出", other, EditorStyles.wordWrappedLabel);

            double outputNutrition = outputs.Sum(p => Card(p.Key).Kind == CardKind.Material ? Card(p.Key).Nutrition * p.Value : 0);
            double selectedNutrition = consumed.Sum(p => Card(p.Key).Nutrition * p.Value);
            double minInput = recipe.Inputs.Where(i => i.Consumed).Sum(i => i.Alternatives.Min(id => Card(id).Nutrition) * (recipe.AllowPartialInputs ? 1 : i.Count));
            double maxInput = recipe.Inputs.Where(i => i.Consumed).Sum(i => i.Alternatives.Max(id => Card(id).Nutrition) * i.Count);
            double extraNutrition = includeTraitCost ? recipe.ExtraTraitCost.Sum(a => Card(a.DefinitionId).Nutrition * a.Count) : 0;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("营养", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("当前选择：产出 / 消耗 / 增量", Number(outputNutrition) + " / " + Number(selectedNutrition) + " / " + Signed(outputNutrition - selectedNutrition));
            EditorGUILayout.LabelField("全部替代组的营养增量上下界", Signed(outputNutrition - maxInput - extraNutrition) + " ～ " + Signed(outputNutrition - minInput - extraNutrition));
            EditorGUILayout.LabelField("单位", "食物营养点，100点对应归一化饱食1；没有扣除劳动饱食成本。", EditorStyles.wordWrappedLabel);

            double manual = recipe.Human ? recipe.Minutes + (includeTraitCost ? recipe.ExtraTraitMinutes : 0) : 0;
            double automatic = recipe.Human ? recipe.AutomaticMinutes : recipe.Minutes;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("标准时间与损耗（效率、环境系数均为1）", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("人力工时", Minutes(manual));
            EditorGUILayout.LabelField("自动等待", Minutes(automatic));
            EditorGUILayout.LabelField("单周期历时", Minutes(manual + automatic));
            if (recipe.TargetRequired || recipe.RequiredRole != CardRole.None) EditorGUILayout.LabelField("主目标占用", Minutes(manual + automatic));
            if (recipe.SecondaryRole != CardRole.None) EditorGUILayout.LabelField("协同设备占用", Minutes(manual + automatic) + "（与主目标同一周期，不重复计人力）");
            EditorGUILayout.LabelField("燃料 / 工具耐久", Number(recipe.Fuel) + " / " + recipe.ToolWear);
            if (recipe.Environmental) EditorGUILayout.HelpBox("该周期会受区域负荷减速；上面列的是环境系数1的标准值。", MessageType.Info);
            if (recipe.Kind == RecipeKind.Gather || recipe.Kind == RecipeKind.Repair || recipe.Kind == RecipeKind.Salvage || recipe.Kind == RecipeKind.Demolish || recipe.Kind == RecipeKind.Expand || recipe.Kind == RecipeKind.Harvest || recipe.Kind == RecipeKind.Breed)
                EditorGUILayout.HelpBox("此工艺还会读取目标储量、维修属性、回收表、扩建阶段、生物状态或性状。上表只展开配方资产中的静态输入输出；实际开工请核对具体目标预览。", MessageType.Warning);
            EditorGUILayout.HelpBox("燃料累计灰烬、副产物的目标状态修正、性状产量和长期循环均未代入。这里不会声称某条完整生产链已经盈利或平衡。", MessageType.Info);
        }

        private void DrawVerification()
        {
            EditorGUILayout.HelpBox("本窗口没有自动执行回归。只有点击下方“由你执行”才会创建独立SurvivalEngine；不读取或修改正在玩的核心、场景对象、存档及配置资产。这里的结果仅属于你点击时的配置。", MessageType.Info);
            EditorGUILayout.HelpBox("覆盖5个核心性质；不是Unity输入、场景、图像、存档文件、完整22项验收或性能结果。夹具只克隆规则并调整Start；R01/R26/R07用于默认设计路径，配置不满足前提时会明确说明。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("由你执行全部5项", GUILayout.Height(28))) for (int i = 0; i < CaseNames.Length; i++) RunCase(i);
                if (GUILayout.Button("清除本窗口结果", GUILayout.Width(145))) Array.Clear(results, 0, results.Length);
            }
            for (int i = 0; i < CaseNames.Length; i++)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField((i + 1) + ". " + CaseNames[i], EditorStyles.boldLabel);
                EditorGUILayout.LabelField(CaseDescriptions[i], EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("由你执行第" + (i + 1) + "项", GUILayout.Width(150))) RunCase(i);
                CaseResult result = results[i];
                EditorGUILayout.HelpBox(result == null ? "未执行。当前仅提供回归源码。" : result.Message,
                    result == null ? MessageType.None : result.Passed ? MessageType.Info : result.Prerequisite ? MessageType.Warning : MessageType.Error);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("执行边界", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("推进工具会在隔离副本内确认暂停，以到达案例指定游戏分钟。倍率案例比较时仅归一化Speed和命令Revision；其余任务、预留、库存、身体、随机数和历史保留比较。配置改动后需重新执行。", EditorStyles.wordWrappedLabel);
        }

        private void RunCase(int index)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            try
            {
                if (configuration == null) throw new CasePrerequisite("请先选择规则资产。");
                SurvivalDefinition source = configuration.BuildDefinition();
                string error = SurvivalEngine.ValidateDefinition(source);
                if (error != "") throw new CasePrerequisite("配置校验未通过：" + error);
                string detail;
                switch (index)
                {
                    case 0: detail = VerifyWaiting(source); break;
                    case 1: detail = VerifyFailedStart(source); break;
                    case 2: detail = VerifyPreview(source); break;
                    case 3: detail = VerifyAutomaticCancel(source); break;
                    default: detail = VerifySpeed(source); break;
                }
                results[index] = new CaseResult { Passed = true, Message = "通过（由你点击执行） · " + stamp + "\n配置版本 " + source.Version + " · " + AssetDatabase.GetAssetPath(configuration) + "\n" + detail };
            }
            catch (CasePrerequisite exception) { results[index] = new CaseResult { Prerequisite = true, Message = "未执行到性质断言：前提不满足 · " + stamp + "\n" + exception.Message }; }
            catch (Exception exception) { results[index] = new CaseResult { Message = "未通过 · " + stamp + "\n" + exception.GetType().Name + "：" + exception.Message }; }
            Repaint();
        }

        private static string VerifyWaiting(SurvivalDefinition source)
        {
            var engine = new SurvivalEngine(Clone(source), 19349663u);
            SurvivalState before = engine.Snapshot;
            AdvanceExactly(engine, 60, 1);
            SurvivalState after = engine.Snapshot;
            Near(before.Body.Food, after.Body.Food, "正常等待改变了饱食");
            Near(before.Body.Mind, after.Body.Mind, "正常等待改变了精神");
            Near(before.Body.Integrity, after.Body.Integrity, "正常等待改变了结构完整度");
            Near(before.Body.Function, after.Body.Function, "正常等待改变了生理机能");
            Near(before.Body.Vitality, after.Body.Vitality, "正常等待改变了生命");
            Require(after.Body.Injuries.Count == 0 && after.Body.Afflictions.Count == 0, "健康开局等待后产生了伤口或异常。");
            return "已在独立新局推进60分钟；f/s/I/R/V保持，伤口与异常均为0。";
        }

        private static string VerifyFailedStart(SurvivalDefinition source)
        {
            SurvivalDefinition fixture = Fixture(source);
            RecipeRule recipe = ManualRecipe(fixture, "R26");
            RequireCase(recipe.Inputs.Any(i => i.Consumed), "R26当前没有消耗输入，不适用于缺料失败案例。");
            var engine = new SurvivalEngine(fixture, 19349663u);
            RequireKnown(engine, recipe.Id);
            SurvivalState before = engine.Snapshot;
            string original = JsonUtility.ToJson(before);
            SurvivalResult result = engine.StartRecipe(recipe.Id);
            Require(!result.Success, "缺少R26输入时开工却成功。");
            SurvivalState after = engine.Snapshot;
            Require(JsonUtility.ToJson(after) == original, "失败操作改变了完整状态；原因为：" + result.Error);
            Require(before.RandomState == after.RandomState && before.NextId == after.NextId && before.Minute == after.Minute, "失败操作改变随机数、实例序号或时刻。");
            return "已确认缺料失败，拒绝原因：" + result.Error + "\n完整快照、RNG、NextId与时刻未变。";
        }

        private static string VerifyPreview(SurvivalDefinition source)
        {
            SurvivalDefinition fixture = Fixture(source);
            RecipeRule recipe = ManualRecipe(fixture, "R01");
            AddInputs(fixture, recipe);
            var engine = new SurvivalEngine(fixture, 19349663u);
            RequireKnown(engine, recipe.Id);
            string ingredient = IngredientInstance(engine.Snapshot, recipe);
            string before = JsonUtility.ToJson(engine.Snapshot);
            SurvivalResult preview = engine.PreviewRecipe(recipe.Id, ingredientId: ingredient);
            Success(preview, "R01预览");
            Require(JsonUtility.ToJson(engine.Snapshot) == before, "预览提交了状态或推进了随机数/序号。");
            SurvivalResult started = engine.StartRecipe(recipe.Id, ingredientId: ingredient);
            Success(started, "R01开始");
            Require(preview.State != null, "成功预览没有返回候选状态。");
            EqualStates(preview.State, engine.Snapshot, false, "R01实际开工与预览候选不同");
            Near(preview.EstimatedMinutes, started.EstimatedMinutes, "预览与实际预计时长不同");
            Near(preview.EstimatedFood, started.EstimatedFood, "预览与实际预计饱食成本不同");
            Near(preview.EstimatedMind, started.EstimatedMind, "预览与实际预计精神成本不同");
            Require(preview.Warning == started.Warning, "预览与实际警告不同。");
            return "同一R01命令与选定材料实例：预览未提交；实际开工的库存、任务、预留、随机状态和费用与预览一致（忽略命令Revision）。";
        }

        private static string VerifyAutomaticCancel(SurvivalDefinition source)
        {
            SurvivalDefinition fixture = Fixture(source);
            RecipeRule recipe = fixture.Recipes.Find(r => r.Id == "R07");
            RequireCase(recipe != null && !recipe.Human && recipe.RequiredRole == CardRole.Fire && recipe.SecondaryRole == CardRole.None && recipe.ToolWear == 0 && string.IsNullOrEmpty(recipe.RequiredAbility), "本案例要求R07为默认的单篝火、无刀具自动加工。");
            RequireCase(recipe.Minutes > 0 && recipe.Minutes <= 720 && recipe.Fuel > 0, "R07需要正燃料和不超过720分钟的自动周期。");
            CardRule fireRule = fixture.Cards.FirstOrDefault(c => c.Role == CardRole.Fire && c.Structure > 0);
            RequireCase(fireRule != null, "缺少可工作的篝火卡定义。");
            CardRule fuelRule = fixture.Cards.Where(c => c.Kind == CardKind.Material && c.FuelValue > 0 && !recipe.Inputs.Any(i => i.Alternatives.Contains(c.Id))).OrderBy(c => c.FuelValue).FirstOrDefault();
            RequireCase(fuelRule != null, "本案例需一种独立于加工原料的可装材料燃料。");
            int fuels = (int)Math.Ceiling(recipe.Fuel / fuelRule.FuelValue);
            RequireCase(fuels * fuelRule.FuelValue <= fireRule.FuelCapacity, "燃料单份值和篝火容量不适合此案例的装料。");
            foreach (Ingredient input in recipe.Inputs) foreach (string id in input.Alternatives)
                RequireCase(fixture.Cards.Find(c => c.Id == id).ShelfMinutes == 0 || fixture.Cards.Find(c => c.Id == id).ShelfMinutes > recipe.Minutes, "输入的保质期不足以完成R07；此为配置前提，不会改写保质期。" );
            AddStart(fixture, fireRule.Id, 1);
            AddInputs(fixture, recipe);
            AddStart(fixture, fuelRule.Id, fuels);
            var engine = new SurvivalEngine(fixture, 19349663u);
            RequireKnown(engine, recipe.Id);
            ItemState fire = engine.Snapshot.Items.Last(i => i.DefinitionId == fireRule.Id);
            foreach (string id in recipe.Inputs.Select(i => i.Alternatives[0]).Distinct())
            {
                foreach (string itemId in engine.Snapshot.Items.Where(i => i.DefinitionId == id && i.OwnerId == "").Select(i => i.Id).ToArray())
                    Success(engine.Transfer(itemId, fire.Id, Place.Input), "向篝火显式投料");
            }
            ItemState fuel = engine.Snapshot.Items.Last(i => i.DefinitionId == fuelRule.Id && i.OwnerId == "");
            Success(engine.Refuel(fire.Id, fuel.Id, fuels), "装入燃料");
            SurvivalState before = engine.Snapshot;
            Success(engine.StartRecipe(recipe.Id, fire.Id), "启动R07");
            SurvivalState started = engine.Snapshot;
            TaskState task = started.Tasks.Single(t => t.RecipeId == recipe.Id && t.TargetId == fire.Id);
            Require(!task.Human, "R07错误占用人力。");
            foreach (var input in task.Consumed.GroupBy(a => a.DefinitionId))
                Near(Count(before, input.Key) - input.Sum(a => a.Count), Count(started, input.Key), "开工没有正确扣除" + input.Key);
            Near(before.Items.Find(i => i.Id == fire.Id).Fuel - recipe.Fuel, started.Items.Find(i => i.Id == fire.Id).Fuel, "开工燃料扣除错误");
            string[] outputIds = started.Reservations.Where(r => r.TaskId == task.Id).Select(r => r.DefinitionId).Distinct().ToArray();
            Require(outputIds.Length > 0, "R07没有生成预留，此配置不能覆盖取消产出性质。");
            Success(engine.CancelTask(task.Id), "取消R07");
            SurvivalState cancelled = engine.Snapshot;
            foreach (var input in task.Consumed.GroupBy(a => a.DefinitionId)) Near(Count(started, input.Key), Count(cancelled, input.Key), "取消退还了" + input.Key);
            Near(started.Items.Find(i => i.Id == fire.Id).Fuel, cancelled.Items.Find(i => i.Id == fire.Id).Fuel, "取消退还了本轮燃料");
            Require(!cancelled.Tasks.Any(t => t.Id == task.Id) && !cancelled.Reservations.Any(r => r.TaskId == task.Id), "取消后任务或预留仍存在。");
            AdvanceExactly(engine, (int)Math.Ceiling(recipe.Minutes) + 1, 3);
            SurvivalState after = engine.Snapshot;
            foreach (string id in outputIds) Near(Count(cancelled, id), Count(after, id), "取消后仍交付了产物" + id);
            Require(!after.Tasks.Any(t => t.Id == task.Id) && !after.Reservations.Any(r => r.TaskId == task.Id), "推进后取消任务或预留重新出现。");
            return "已开始并取消R07：当前轮原料与燃料未退，预留与任务已释放；越过原周期后没有迟到产物。";
        }

        private static string VerifySpeed(SurvivalDefinition source)
        {
            SurvivalDefinition fixture = Fixture(source);
            RecipeRule recipe = ManualRecipe(fixture, "R26");
            AddInputs(fixture, recipe);
            var one = new SurvivalEngine(Clone(fixture), 19349663u);
            var three = new SurvivalEngine(Clone(fixture), 19349663u);
            RequireKnown(one, recipe.Id); RequireKnown(three, recipe.Id);
            Success(one.SetSpeed(1), "设置1倍"); Success(three.SetSpeed(3), "设置3倍");
            Success(one.StartRecipe(recipe.Id, ingredientId: IngredientInstance(one.Snapshot, recipe)), "1倍路径R26开工");
            Success(three.StartRecipe(recipe.Id, ingredientId: IngredientInstance(three.Snapshot, recipe)), "3倍路径R26开工");
            AdvanceExactly(one, 180, 1); AdvanceExactly(three, 180, 3);
            EqualStates(one.Snapshot, three.Snapshot, true, "1倍与3倍路径在相同游戏分钟得到不同状态");
            return "同种子、同R26命令推进180游戏分钟，完整状态一致；仅忽略声明倍率Speed与外层命令Revision，不忽略库存、身体、随机数、序号、任务、预留或历史。";
        }

        private static SurvivalDefinition Fixture(SurvivalDefinition source)
        {
            SurvivalDefinition fixture = Clone(source);
            fixture.Start = fixture.Start.Where(i => fixture.Cards.Find(c => c.Id == i.DefinitionId).Role == CardRole.NaturalNode).ToList();
            return fixture;
        }
        private static SurvivalDefinition Clone(SurvivalDefinition source) => JsonUtility.FromJson<SurvivalDefinition>(JsonUtility.ToJson(source));
        private static RecipeRule ManualRecipe(SurvivalDefinition definition, string id)
        {
            RecipeRule recipe = definition.Recipes.Find(r => r.Id == id);
            RequireCase(recipe != null && recipe.Human && !recipe.TargetRequired && recipe.RequiredRole == CardRole.None && recipe.SecondaryRole == CardRole.None && string.IsNullOrEmpty(recipe.RequiredAbility) && recipe.AutomaticMinutes == 0,
                id + "不再是本案例要求的默认徒手工序；请按新规则调整回归源码。");
            RequireCase(recipe.Inputs.Count > 0 && recipe.Inputs.All(i => i.Alternatives.Count > 0 && i.Alternatives.All(a => definition.Cards.Find(c => c.Id == a).Kind == CardKind.Material)), id + "缺少材料输入前提。");
            return recipe;
        }
        private static void AddInputs(SurvivalDefinition definition, RecipeRule recipe)
        {
            foreach (var input in recipe.Inputs.Select(i => new { Id = i.Alternatives[0], i.Count }).GroupBy(i => i.Id)) AddStart(definition, input.Key, input.Sum(i => i.Count));
        }
        private static void AddStart(SurvivalDefinition definition, string id, int count)
        {
            CardRule card = definition.Cards.Find(c => c.Id == id);
            RequireCase(card != null && count > 0, "案例夹具缺少合法卡牌：" + id);
            int limit = card.Kind == CardKind.Material ? card.StackLimit : 1;
            while (count > 0)
            { int n = Math.Min(count, limit); definition.Start.Add(new StartItem { DefinitionId = id, Count = n, X = -1, Y = -1 }); count -= n; }
        }
        private static string IngredientInstance(SurvivalState state, RecipeRule recipe)
        {
            string id = recipe.Inputs[0].Alternatives[0];
            ItemState item = state.Items.FirstOrDefault(i => i.DefinitionId == id && i.Quantity > 0);
            RequireCase(item != null, "案例缺少选定输入实例：" + id);
            return item.Id;
        }
        private static void AdvanceExactly(SurvivalEngine engine, int minutes, int batch)
        {
            long end = engine.Snapshot.Minute + minutes;
            int maximum = engine.Rules.MaxMinutesPerAdvance;
            int guard = minutes * 3 + 20;
            while (engine.Snapshot.Minute < end && guard-- > 0)
            {
                Require(!engine.Dead, "隔离推进期间死亡：" + engine.Snapshot.FailureReason);
                if (engine.Paused) Success(engine.SetPaused(false), "在隔离副本确认暂停");
                int n = (int)Math.Min(end - engine.Snapshot.Minute, Math.Min(batch, maximum));
                long before = engine.Snapshot.Minute;
                Success(engine.Advance(n), "推进隔离核心");
                Require(engine.Snapshot.Minute > before, "时钟未推进；通知：" + engine.Snapshot.Notice);
            }
            Require(engine.Snapshot.Minute == end, "未推进到指定游戏分钟，通知：" + engine.Snapshot.Notice);
        }
        private static void EqualStates(SurvivalState expected, SurvivalState actual, bool ignoreSpeed, string message)
        {
            SurvivalState left = JsonUtility.FromJson<SurvivalState>(JsonUtility.ToJson(expected));
            SurvivalState right = JsonUtility.FromJson<SurvivalState>(JsonUtility.ToJson(actual));
            left.Revision = right.Revision = 0;
            if (ignoreSpeed) left.Speed = right.Speed = 1;
            string a = JsonUtility.ToJson(left), b = JsonUtility.ToJson(right);
            if (a == b) return;
            int index = 0; while (index < a.Length && index < b.Length && a[index] == b[index]) index++;
            int start = Math.Max(0, index - 40);
            throw new InvalidOperationException(message + "。差异位置" + index + "\n预期：" + a.Substring(start, Math.Min(180, a.Length - start)) + "\n实际：" + b.Substring(start, Math.Min(180, b.Length - start)));
        }
        private static int Count(SurvivalState state, string id) => state.Items.Where(i => i.DefinitionId == id).Sum(i => i.Quantity);
        private static void Success(SurvivalResult result, string context) { Require(result != null && result.Success, context + "被拒绝：" + (result == null ? "没有结果" : result.Error)); }
        private static void RequireKnown(SurvivalEngine engine, string recipeId) { RequireCase(engine.KnownRecipeIds().Contains(recipeId), recipeId + "在案例夹具中尚未满足发现条件；请按当前解锁规则准备夹具。"); }
        private static void Near(double expected, double actual, string message) { Require(Math.Abs(expected - actual) <= 0.000000001, message + "；预期" + expected.ToString("G17") + "，实际" + actual.ToString("G17")); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void RequireCase(bool condition, string message) { if (!condition) throw new CasePrerequisite(message); }
        private static int Value(Dictionary<string, int> values, string id) { int count; return values.TryGetValue(id, out count) ? count : 0; }
        private static void Add(Dictionary<string, int> values, string id, int count) { values[id] = Value(values, id) + count; }
        private static string Number(double value) => value.ToString("0.###");
        private static string Signed(double value) => value > 0 ? "+" + Number(value) : Number(value);
        private static string Minutes(double minutes) => Number(minutes) + "分钟（" + Number(minutes / 60) + "小时）";
        private sealed class CasePrerequisite : Exception { public CasePrerequisite(string message) : base(message) { } }
        private sealed class CaseResult { public bool Passed, Prerequisite; public string Message; }
    }
}
