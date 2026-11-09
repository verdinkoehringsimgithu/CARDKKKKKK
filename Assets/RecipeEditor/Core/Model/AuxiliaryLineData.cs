using System;
using System.Collections.Generic;
using System.Text;

namespace CardKKK.RecipeEditor.Core
{
    /// <summary>
    /// 副产线匹配组合模式
    /// </summary>
    public enum AuxiliaryMatchCombineMode
    {
        /// <summary>
        /// 满足任意一项条件即可匹配 (OR)
        /// </summary>
        Any = 0,

        /// <summary>
        /// 必须同时满足所有配置的条件分类 (AND)
        /// </summary>
        All = 1
    }

    /// <summary>
    /// 副产线产物修饰/覆盖项。
    /// 支持：
    /// 1. 防止/拦截产出 (Chance == 0%)：使宿主配方中原本存在的某产物彻底不产出（如手套免起水疱）；
    /// 2. 强制必定产出 (Chance == 100%)：使宿主配方原本有几率掉落的产物变为 100% 必定掉落；
    /// 3. 概率重写覆盖 (Chance == X%)：以副产线设定的概率覆盖宿主原本的几率；
    /// 4. 额外产物追加 (宿主原本没有的物品，Chance > 0%)：作为全新产出物追加掉落（如手链额外产出回san卡）。
    /// </summary>
    [Serializable]
    public sealed class RecipeOutputModifier
    {
        public string ItemId = "";
        public int Quantity = 1;

        /// <summary>
        /// 产出概率（0.0 ~ 1.0）。
        /// 0.0 表示彻底防止/拦截产出（使原配方不产出该项）；
        /// 1.0 表示必定产出（覆盖为100%）；
        /// 对宿主原配方没有的新物品，大于 0 则作为额外新产物追加。
        /// </summary>
        public float Chance = 1.0f;

        /// <summary>
        /// 产出物类别标记：主产物 (0) 或 副产物 (1)
        /// </summary>
        public RecipeOutputType OutputType = RecipeOutputType.Byproduct;

        public string Note = "";

        public RecipeOutputModifier() { }

        public RecipeOutputModifier(string itemId, int quantity = 1, float chance = 1.0f, RecipeOutputType outputType = RecipeOutputType.Byproduct, string note = "")
        {
            ItemId = itemId ?? string.Empty;
            Quantity = quantity > 0 ? quantity : 1;
            Chance = Math.Max(0f, Math.Min(1f, chance));
            OutputType = outputType;
            Note = note ?? string.Empty;
        }

        public RecipeOutputModifier Clone()
        {
            return new RecipeOutputModifier(ItemId, Quantity, Chance, OutputType, Note);
        }

        /// <summary>
        /// 是否属于“防止/拦截产出”项
        /// </summary>
        public bool IsPrevention => Chance <= 0.0001f;

        public override string ToString()
        {
            if (IsPrevention) return $"[🛡️ 拦截产出: {ItemId}]";
            string typeStr = OutputType == RecipeOutputType.Primary ? "★主产物" : "⚑副产物";
            string chanceStr = Chance < 0.999f ? $"({Math.Round(Chance * 100)}%)" : "(100%)";
            return $"[{ItemId} x{Quantity} {chanceStr} {typeStr}]";
        }
    }

    /// <summary>
    /// 副产线定义（基于复合条件的跨配方伴生修正规则）。
    /// 当宿主配方满足指定标签、物品或工位条件，且合成中投入了额外媒介卡牌时生效。
    /// 可实现：
    /// - 额外产物追加（如：只要配方有【手】，投入【手链】额外产出【回san卡】）；
    /// - 防止/拦截特定产出（如：只要配方有【重体力】，投入【厚皮手套】使【手部水疱】几率变为 0%）；
    /// - 概率拉满（如：投入【符文锤】使原本 15% 产出的优质钢锭变为 100% 必定掉落）；
    /// - 彻底阻断宿主原有产出（如：投入【污染毒粉】使原配方完全报废，仅生成有害烟雾）。
    /// </summary>
    [Serializable]
    public sealed class RecipeAuxiliaryLine
    {
        public string Id;
        public string Name;
        public string Description = "";

        // ==================== 1. 宿主配方多维度复合匹配（可多选） ====================
        /// <summary>
        /// 条件组合逻辑：满足任意一项 (Any / OR) 还是必须全部满足 (All / AND)
        /// </summary>
        public AuxiliaryMatchCombineMode MatchCombineMode = AuxiliaryMatchCombineMode.Any;

        /// <summary>
        /// 匹配标签列表（多选，宿主配方输入包含这些标签之一/全部即满足）
        /// </summary>
        public List<string> MatchTagIds = new List<string>();

        /// <summary>
        /// 匹配具体物品列表（多选，宿主配方输入包含这些物品之一/全部即满足）
        /// </summary>
        public List<string> MatchItemIds = new List<string>();

        /// <summary>
        /// 匹配工位场所列表（多选，宿主配方在这些场所之一发生即满足）
        /// </summary>
        public List<string> MatchLocationIds = new List<string>();

        // ==================== 2. 副产线自身额外投入项（不限数量） ====================
        /// <summary>
        /// 额外投入的媒介卡牌（如【手链】、【厚皮手套】、【粗盐】），支持原料消耗或工具耐久磨损
        /// </summary>
        public List<RecipeInputSlot> ExtraInputs = new List<RecipeInputSlot>();

        // ==================== 3. 产物修饰与覆盖列表（不限数量） ====================
        /// <summary>
        /// 是否彻底阻断宿主配方原本的所有产出（若为 true，宿主原产出全部清空，只保留本副产线的产物）
        /// </summary>
        public bool SuppressBaseOutputs = false;

        /// <summary>
        /// 产物修饰列表（覆盖几率、拦截防止、或额外追加产物）
        /// </summary>
        public List<RecipeOutputModifier> OutputModifiers = new List<RecipeOutputModifier>();

        public RecipeAuxiliaryLine() { }

        public RecipeAuxiliaryLine(string id, string name, string description = "")
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public RecipeAuxiliaryLine Clone()
        {
            var line = new RecipeAuxiliaryLine(Id, Name, Description)
            {
                MatchCombineMode = MatchCombineMode,
                SuppressBaseOutputs = SuppressBaseOutputs,
                MatchTagIds = new List<string>(MatchTagIds ?? new List<string>()),
                MatchItemIds = new List<string>(MatchItemIds ?? new List<string>()),
                MatchLocationIds = new List<string>(MatchLocationIds ?? new List<string>()),
                ExtraInputs = new List<RecipeInputSlot>(),
                OutputModifiers = new List<RecipeOutputModifier>()
            };

            if (ExtraInputs != null)
            {
                foreach (var inp in ExtraInputs)
                    if (inp != null) line.ExtraInputs.Add(inp.Clone());
            }

            if (OutputModifiers != null)
            {
                foreach (var mod in OutputModifiers)
                    if (mod != null) line.OutputModifiers.Add(mod.Clone());
            }

            return line;
        }

        /// <summary>
        /// 生成用于界面显示的副产线逻辑摘要公式
        /// </summary>
        public string GetSummaryDisplay(Func<string, string> itemNameResolver = null, Func<string, string> tagNameResolver = null, Func<string, string> locationNameResolver = null)
        {
            string ResolveItem(string id) => itemNameResolver != null ? itemNameResolver(id) : id;
            string ResolveTag(string id) => tagNameResolver != null ? tagNameResolver(id) : id;
            string ResolveLocation(string id) => locationNameResolver != null ? locationNameResolver(id) : id;

            var sb = new StringBuilder();

            // 1. 匹配条件前缀
            var condParts = new List<string>();
            if (MatchTagIds != null && MatchTagIds.Count > 0)
            {
                foreach (var t in MatchTagIds) condParts.Add($"#标签:{ResolveTag(t)}");
            }
            if (MatchItemIds != null && MatchItemIds.Count > 0)
            {
                foreach (var i in MatchItemIds) condParts.Add($"物品:{ResolveItem(i)}");
            }
            if (MatchLocationIds != null && MatchLocationIds.Count > 0)
            {
                foreach (var l in MatchLocationIds) condParts.Add($"工位:{ResolveLocation(l)}");
            }

            string opStr = MatchCombineMode == AuxiliaryMatchCombineMode.All ? " 且 " : " 或 ";
            string condStr = condParts.Count > 0 ? string.Join(opStr, condParts) : "任意配方";
            sb.Append($"[匹配: {condStr}]");

            // 2. 额外投入媒介
            if (ExtraInputs != null && ExtraInputs.Count > 0)
            {
                sb.Append(" + [额外投入: ");
                for (int i = 0; i < ExtraInputs.Count; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    var inp = ExtraInputs[i];
                    string name = inp.MatchType == IngredientMatchType.ByTag ? $"#标签:{ResolveTag(inp.TargetId)}" : ResolveItem(inp.TargetId);
                    string wearTag = !inp.Consume ? (inp.DurabilityCost > 0 ? $"(损耗:{inp.DurabilityCost})" : "(非耗材)") : "(消耗)";
                    sb.Append($"{name} x{inp.Quantity}{wearTag}");
                }
                sb.Append("]");
            }

            sb.Append(" ──▶ ");

            // 3. 产出修饰效果
            if (SuppressBaseOutputs)
            {
                sb.Append("[🚫阻断原配方全部产出] ");
            }

            if (OutputModifiers == null || OutputModifiers.Count == 0)
            {
                sb.Append("(无附加产出修饰)");
            }
            else
            {
                for (int i = 0; i < OutputModifiers.Count; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    var mod = OutputModifiers[i];
                    string name = ResolveItem(mod.ItemId);
                    if (mod.IsPrevention)
                    {
                        sb.Append($"[🛡️拦截防止: {name} (0%)]");
                    }
                    else
                    {
                        string typeStr = mod.OutputType == RecipeOutputType.Primary ? "★主产物" : "⚑副产物";
                        string chanceStr = mod.Chance < 0.999f ? $"({Math.Round(mod.Chance * 100)}%)" : "(100%)";
                        sb.Append($"[{name} x{mod.Quantity}{chanceStr} {typeStr}]");
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
