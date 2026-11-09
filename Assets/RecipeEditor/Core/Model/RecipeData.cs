using System;
using System.Collections.Generic;
using System.Text;

namespace CardKKK.RecipeEditor.Core
{
    /// <summary>
    /// 配方类型
    /// </summary>
    public enum RecipeType
    {
        /// <summary>
        /// 标准多对多合成（A + B + ... ──> C + D + ...）
        /// </summary>
        Synthesis = 0,

        /// <summary>
        /// 自身转变配方（物品 A + 条件 ──> 物品 B + 副产物）
        /// 例如：生肉 + 篝火(高温) + 烤制时间 ──> 烤肉 + 骨头
        /// </summary>
        Transformation = 1
    }

    /// <summary>
    /// 原料/输入槽或个体的匹配方式
    /// </summary>
    public enum IngredientMatchType
    {
        /// <summary>
        /// 精确匹配特定物品（输入 TargetId 为 ItemId）
        /// </summary>
        ExactItem = 0,

        /// <summary>
        /// 按标签匹配（输入 TargetId 为 TagId，任何具备该标签的物品皆可满足）
        /// </summary>
        ByTag = 1
    }

    /// <summary>
    /// 在场操作者/人员要求（非场所、不消耗、无损耗，代表需要某物品或某标签的人员在场操作/监督，例如：村民、铁匠、学徒、角色等）。
    /// 人员参与不消耗卡牌本体，也不产生装备损耗。
    /// </summary>
    [Serializable]
    public sealed class RecipeOperatorRequirement
    {
        public IngredientMatchType MatchType = IngredientMatchType.ExactItem;
        public string TargetId = "";
        public int Quantity = 1;
        public string Role = ""; // 角色分工说明，例如 "工匠"、"学徒"、"看守者"
        public string Note = "";

        /// <summary>
        /// 本操作个体的能力换算乘数（默认 1.0 即 100% 换算）
        /// </summary>
        public float EfficiencyMultiplier = 1.0f;

        public RecipeOperatorRequirement() { }

        public RecipeOperatorRequirement(IngredientMatchType matchType, string targetId, int quantity = 1, string role = "", string note = "", float efficiencyMultiplier = 1.0f)
        {
            MatchType = matchType;
            TargetId = targetId ?? string.Empty;
            Quantity = quantity > 0 ? quantity : 1;
            Role = role ?? string.Empty;
            Note = note ?? string.Empty;
            EfficiencyMultiplier = efficiencyMultiplier > 0f ? efficiencyMultiplier : 1.0f;
        }

        public RecipeOperatorRequirement Clone()
        {
            return new RecipeOperatorRequirement(MatchType, TargetId, Quantity, Role, Note, EfficiencyMultiplier);
        }
    }

    /// <summary>
    /// 配方输入槽（等式左侧项，支持不限量添加）。
    /// 原料完全消耗；工具/非耗材参与可损耗（当前版本预留损耗字段，默认 0 暂不设损耗）。
    /// </summary>
    [Serializable]
    public sealed class RecipeInputSlot
    {
        public IngredientMatchType MatchType = IngredientMatchType.ExactItem;
        public string TargetId = "";
        public int Quantity = 1;
        public bool Consume = true; // true 为作为原料消耗掉；false 为仅参与/作为工具或器具（非原料耗材，但可受耐久损耗）

        /// <summary>
        /// 单次制作损耗耐久（0 表示暂不设损耗；工具/非耗材可损耗耐久）。
        /// </summary>
        public int DurabilityCost = 0;

        /// <summary>
        /// 本输入原料/工具的能力换算乘数（默认 1.0 即 100% 换算）
        /// </summary>
        public float EfficiencyMultiplier = 1.0f;

        public string Note = "";

        public RecipeInputSlot() { }

        public RecipeInputSlot(IngredientMatchType matchType, string targetId, int quantity = 1, bool consume = true, int durabilityCost = 0, string note = "", float efficiencyMultiplier = 1.0f)
        {
            MatchType = matchType;
            TargetId = targetId ?? string.Empty;
            Quantity = quantity > 0 ? quantity : 1;
            Consume = consume;
            DurabilityCost = Math.Max(0, durabilityCost);
            Note = note ?? string.Empty;
            EfficiencyMultiplier = efficiencyMultiplier > 0f ? efficiencyMultiplier : 1.0f;
        }

        public RecipeInputSlot Clone()
        {
            return new RecipeInputSlot(MatchType, TargetId, Quantity, Consume, DurabilityCost, Note, EfficiencyMultiplier);
        }
    }

    /// <summary>
    /// 产出物类别标记：主产物（核心目标产物）或副产物（伴生副产品/废料/次要产物）
    /// 支持不限数量的主产物与副产物灵活配置
    /// </summary>
    public enum RecipeOutputType
    {
        /// <summary>
        /// 主产物（核心目标产出物，支持不限数量）
        /// </summary>
        Primary = 0,

        /// <summary>
        /// 副产物（伴生副产物/次要产物/废料，支持不限数量）
        /// </summary>
        Byproduct = 1
    }

    /// <summary>
    /// 配方产出槽（等式右侧项，支持不限量添加主产物与副产物）
    /// </summary>
    [Serializable]
    public sealed class RecipeOutputSlot
    {
        public string ItemId = "";
        public int Quantity = 1;
        public float Chance = 1.0f; // 0.0 ~ 1.0 (1.0 = 100% 必定掉落/产出)

        /// <summary>
        /// 产出类别标记：主产物 (Primary) 或 副产物 (Byproduct)，不限数量
        /// </summary>
        public RecipeOutputType OutputType = RecipeOutputType.Primary;

        /// <summary>
        /// 是否为副产物标记（保持字段向后兼容）
        /// </summary>
        public bool IsByproduct = false;

        /// <summary>
        /// 产出触发条件（默认 Always 均产出；可设为 OnCheckSuccess 仅检定达标产出，或 OnCheckFailure 仅检定失败分支产出）
        /// </summary>
        public OutputTriggerCondition TriggerCondition = OutputTriggerCondition.Always;

        public string Note = "";

        public RecipeOutputSlot() { }

        public RecipeOutputSlot(string itemId, int quantity = 1, float chance = 1.0f, RecipeOutputType outputType = RecipeOutputType.Primary, string note = "", OutputTriggerCondition triggerCondition = OutputTriggerCondition.Always)
        {
            ItemId = itemId ?? string.Empty;
            Quantity = quantity > 0 ? quantity : 1;
            Chance = Math.Max(0f, Math.Min(1f, chance));
            OutputType = outputType;
            IsByproduct = (outputType == RecipeOutputType.Byproduct);
            Note = note ?? string.Empty;
            TriggerCondition = triggerCondition;
        }

        public RecipeOutputSlot(string itemId, int quantity, float chance, bool isByproduct, string note = "", OutputTriggerCondition triggerCondition = OutputTriggerCondition.Always)
            : this(itemId, quantity, chance, isByproduct ? RecipeOutputType.Byproduct : RecipeOutputType.Primary, note, triggerCondition)
        {
        }

        public RecipeOutputSlot Clone()
        {
            var slot = new RecipeOutputSlot(ItemId, Quantity, Chance, OutputType, Note, TriggerCondition);
            slot.IsByproduct = IsByproduct || (OutputType == RecipeOutputType.Byproduct);
            return slot;
        }
    }

    /// <summary>
    /// 自身转变配方的附加条件类型
    /// </summary>
    public enum ConditionType
    {
        /// <summary>
        /// 需要特定工具或具备某标签的工具（不消耗），TargetId 可填 ItemId 或 TagId
        /// </summary>
        RequireTool = 0,

        /// <summary>
        /// 需要消耗辅助材料（消耗），TargetId 可填 ItemId 或 TagId
        /// </summary>
        RequireMaterial = 1,

        /// <summary>
        /// 需要时间维持/推进（分钟）
        /// </summary>
        DurationMinutes = 2,

        /// <summary>
        /// 场所/环境条件（例如要求特定场所或环境标签）
        /// </summary>
        EnvironmentCondition = 3,

        /// <summary>
        /// 属性数值门槛（如 温度 >= 200）
        /// </summary>
        PropertyThreshold = 4,

        /// <summary>
        /// 需要特定个体在场操作（非场所，不消耗），TargetId 可填 ItemId 或 TagId
        /// </summary>
        RequireOperator = 5
    }

    /// <summary>
    /// 自身转变配方的附加条件项
    /// </summary>
    [Serializable]
    public sealed class RecipeCondition
    {
        public ConditionType Type = ConditionType.RequireTool;
        public string TargetId = "";
        public int Value = 1;
        public string Operator = ">="; // >=, ==, <=

        /// <summary>
        /// 工具或设施单次损耗度（默认 0，表示暂不设损耗）
        /// </summary>
        public int DurabilityCost = 0;

        public string Note = "";

        public RecipeCondition() { }

        public RecipeCondition(ConditionType type, string targetId, int value = 1, string op = ">=", int durabilityCost = 0, string note = "")
        {
            Type = type;
            TargetId = targetId ?? string.Empty;
            Value = value;
            Operator = string.IsNullOrEmpty(op) ? ">=" : op;
            DurabilityCost = Math.Max(0, durabilityCost);
            Note = note ?? string.Empty;
        }

        public RecipeCondition Clone()
        {
            return new RecipeCondition(Type, TargetId, Value, Operator, DurabilityCost, Note);
        }
    }

    /// <summary>
    /// 配方定义。
    /// 1. 支持不限容量的标准多对多合成；
    /// 2. 支持物品自身转变配方（物品 A + 条件 ──> 物品 B）；
    /// 3. 支持指定合成场所（含物品作为场所）作为发生地；
    /// 4. 支持指定物品/标签作为“在场操作个体”（非场所、不消耗，代表需要某某在场操作）。
    /// </summary>
    [Serializable]
    public sealed class RecipeDefinition
    {
        public string Id;
        public string Name;
        public RecipeType Type = RecipeType.Synthesis;

        /// <summary>
        /// 发生场所 ID。为空时代表徒手/随身无限制；可关联独立场所或用作场所的物品。
        /// </summary>
        public string LocationId = "";

        /// <summary>
        /// 在场操作个体/人员需求（非场所、不消耗，需在场操作，例如村民、铁匠、工匠等）
        /// </summary>
        public List<RecipeOperatorRequirement> Operators = new List<RecipeOperatorRequirement>();

        /// <summary>
        /// 配方能力检定需求列表（需要多少指定能力值，例如烹饪>=10、热量>=50、力量>=15等）
        /// </summary>
        public List<RecipeAbilityCheck> AbilityChecks = new List<RecipeAbilityCheck>();

        /// <summary>
        /// 制作耗时（分钟），0 表示瞬时完成
        /// </summary>
        public int DurationMinutes = 0;

        public string Description = "";

        // ==================== 标准合成配方字段（等式左右容量不限） ====================
        /// <summary>
        /// 等式左侧：原料/需求输入项列表（不限量）
        /// </summary>
        public List<RecipeInputSlot> Inputs = new List<RecipeInputSlot>();

        /// <summary>
        /// 等式右侧：产物列表（不限量）
        /// </summary>
        public List<RecipeOutputSlot> Outputs = new List<RecipeOutputSlot>();

        // ==================== 自身转变配方字段（物品 A + 条件 ──> 物品 B） ====================
        /// <summary>
        /// 转变主体源物品 A 的 ID
        /// </summary>
        public string SourceItemId = "";

        /// <summary>
        /// 转变主体源物品 A 的消耗数量（通常为 1）
        /// </summary>
        public int SourceQuantity = 1;

        /// <summary>
        /// 转变附加条件列表（不限量，如所需工具、附加辅料、在场个体、维持时间、环境门槛等）
        /// </summary>
        public List<RecipeCondition> TransformationConditions = new List<RecipeCondition>();

        /// <summary>
        /// 转变后的目标产物物品 B 的 ID
        /// </summary>
        public string TargetItemId = "";

        /// <summary>
        /// 转变产物物品 B 的数量
        /// </summary>
        public int TargetQuantity = 1;

        /// <summary>
        /// 伴生副产物列表（不限量，例如 产生熔渣、废料等）
        /// </summary>
        public List<RecipeOutputSlot> TransformationByproducts = new List<RecipeOutputSlot>();

        public RecipeDefinition() { }

        public RecipeDefinition(string id, string name, RecipeType type = RecipeType.Synthesis, string locationId = "")
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Type = type;
            LocationId = locationId ?? string.Empty;
        }

        public RecipeDefinition Clone()
        {
            var recipe = new RecipeDefinition(Id, Name, Type, LocationId)
            {
                DurationMinutes = DurationMinutes,
                Description = Description,
                SourceItemId = SourceItemId,
                SourceQuantity = SourceQuantity,
                TargetItemId = TargetItemId,
                TargetQuantity = TargetQuantity,
                Operators = new List<RecipeOperatorRequirement>(),
                AbilityChecks = new List<RecipeAbilityCheck>(),
                Inputs = new List<RecipeInputSlot>(),
                Outputs = new List<RecipeOutputSlot>(),
                TransformationConditions = new List<RecipeCondition>(),
                TransformationByproducts = new List<RecipeOutputSlot>()
            };

            if (Operators != null)
            {
                foreach (var op in Operators)
                    if (op != null) recipe.Operators.Add(op.Clone());
            }

            if (AbilityChecks != null)
            {
                foreach (var ac in AbilityChecks)
                    if (ac != null) recipe.AbilityChecks.Add(ac.Clone());
            }

            if (Inputs != null)
            {
                foreach (var input in Inputs)
                    if (input != null) recipe.Inputs.Add(input.Clone());
            }

            if (Outputs != null)
            {
                foreach (var output in Outputs)
                    if (output != null) recipe.Outputs.Add(output.Clone());
            }

            if (TransformationConditions != null)
            {
                foreach (var cond in TransformationConditions)
                    if (cond != null) recipe.TransformationConditions.Add(cond.Clone());
            }

            if (TransformationByproducts != null)
            {
                foreach (var bp in TransformationByproducts)
                    if (bp != null) recipe.TransformationByproducts.Add(bp.Clone());
            }

            return recipe;
        }

        /// <summary>
        /// 生成用于界面显示的直观配方等式字符串
        /// </summary>
        public string GetFormulaDisplay(Func<string, string> itemNameResolver = null, Func<string, string> tagNameResolver = null, Func<string, string> locationNameResolver = null)
        {
            string ResolveItem(string id) => itemNameResolver != null ? itemNameResolver(id) : id;
            string ResolveTag(string id) => tagNameResolver != null ? tagNameResolver(id) : id;
            string ResolveLocation(string id) => locationNameResolver != null ? locationNameResolver(id) : id;

            var sb = new StringBuilder();

            // 发生场所前缀
            if (!string.IsNullOrEmpty(LocationId))
            {
                sb.Append($"[工位: {ResolveLocation(LocationId)}] ");
            }

            // 在场操作人员（不消耗、无损耗）
            if (Operators != null && Operators.Count > 0)
            {
                foreach (var op in Operators)
                {
                    string targetName = op.MatchType == IngredientMatchType.ByTag
                        ? $"#标签:{ResolveTag(op.TargetId)}"
                        : ResolveItem(op.TargetId);
                    string roleStr = string.IsNullOrEmpty(op.Role) ? "" : $"({op.Role})";
                    sb.Append($"[在场人员: {targetName} x{op.Quantity}{roleStr} (不消耗·无损耗)] ");
                }
            }

            // 配方能力检定门槛
            if (AbilityChecks != null && AbilityChecks.Count > 0)
            {
                sb.Append("[能力检定: ");
                for (int i = 0; i < AbilityChecks.Count; i++)
                {
                    if (i > 0) sb.Append(" 且 ");
                    var chk = AbilityChecks[i];
                    sb.Append($"{chk.AbilityId} {chk.Operator} {chk.RequiredValue}");
                }
                sb.Append("] ");
            }

            if (Type == RecipeType.Transformation)
            {
                // 自身转变格式：[主体A x数量] + (条件...) ──> [目标B x数量] (+ 副产物...)
                string srcName = string.IsNullOrEmpty(SourceItemId) ? "(未指定源物品)" : ResolveItem(SourceItemId);
                sb.Append($"[{srcName} x{SourceQuantity}]");

                if (TransformationConditions != null && TransformationConditions.Count > 0)
                {
                    foreach (var cond in TransformationConditions)
                    {
                        sb.Append(" + ");
                        switch (cond.Type)
                        {
                            case ConditionType.RequireTool:
                                string toolWear = cond.DurabilityCost > 0 ? $"损耗:{cond.DurabilityCost}" : "可损耗·暂不设";
                                sb.Append($"[工具: {ResolveItem(cond.TargetId)} ({toolWear})]");
                                break;
                            case ConditionType.RequireMaterial:
                                sb.Append($"[辅料: {ResolveItem(cond.TargetId)} x{cond.Value}]");
                                break;
                            case ConditionType.RequireOperator:
                                sb.Append($"[在场人员: {ResolveItem(cond.TargetId)} x{cond.Value} (不消耗·无损耗)]");
                                break;
                            case ConditionType.DurationMinutes:
                                sb.Append($"[等待 {cond.Value}分]");
                                break;
                            case ConditionType.EnvironmentCondition:
                                sb.Append($"[环境: {cond.TargetId}]");
                                break;
                            case ConditionType.PropertyThreshold:
                                sb.Append($"[{cond.TargetId} {cond.Operator} {cond.Value}]");
                                break;
                        }
                    }
                }

                sb.Append(" ──> ");

                string dstName = string.IsNullOrEmpty(TargetItemId) ? "(未指定目标物品)" : ResolveItem(TargetItemId);
                sb.Append($"[{dstName} x{TargetQuantity}]");

                if (TransformationByproducts != null && TransformationByproducts.Count > 0)
                {
                    foreach (var bp in TransformationByproducts)
                    {
                        sb.Append($" + [{ResolveItem(bp.ItemId)} x{bp.Quantity} (副产物)]");
                    }
                }
            }
            else
            {
                // 标准合成等式格式：[输入1] + [输入2] ──> [产物1] + [产物2]
                if (Inputs == null || Inputs.Count == 0)
                {
                    sb.Append("(无原料输入)");
                }
                else
                {
                    for (int i = 0; i < Inputs.Count; i++)
                    {
                        if (i > 0) sb.Append(" + ");
                        var inSlot = Inputs[i];
                        string name = inSlot.MatchType == IngredientMatchType.ByTag
                            ? $"#标签:{ResolveTag(inSlot.TargetId)}"
                            : ResolveItem(inSlot.TargetId);

                        string consumeTag;
                        if (inSlot.Consume)
                        {
                            consumeTag = "";
                        }
                        else
                        {
                            string wearTag = inSlot.DurabilityCost > 0 ? $"损耗:{inSlot.DurabilityCost}" : "可损耗·暂不设";
                            consumeTag = $" (工具·{wearTag})";
                        }
                        sb.Append($"[{name} x{inSlot.Quantity}{consumeTag}]");
                    }
                }

                sb.Append(" ──> ");

                if (Outputs == null || Outputs.Count == 0)
                {
                    sb.Append("(无产出物)");
                }
                else
                {
                    for (int i = 0; i < Outputs.Count; i++)
                    {
                        if (i > 0) sb.Append(" + ");
                        var outSlot = Outputs[i];
                        string name = ResolveItem(outSlot.ItemId);
                        bool isBp = outSlot.OutputType == RecipeOutputType.Byproduct || outSlot.IsByproduct;
                        string typeTag = isBp ? "(副产物)" : "(主产物)";
                        string chanceTag = outSlot.Chance < 1.0f ? $"({outSlot.Chance * 100:0}%)" : "";
                        sb.Append($"[{name} x{outSlot.Quantity}{chanceTag}{typeTag}]");
                    }
                }
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? Id : $"{Name} ({Id})";
        }
    }
}
