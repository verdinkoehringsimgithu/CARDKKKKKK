using System;

namespace CardKKK.RecipeEditor.Core
{
    /// <summary>
    /// 全局能力/属性定义（例如：烹饪技艺、冶炼技艺、火力热量、力量、敏捷、魔力等）。
    /// 作为配方检定与卡牌点数贡献的标准标尺。
    /// </summary>
    [Serializable]
    public sealed class RecipeAbility
    {
        public string Id = "";
        public string Name = "";
        public string Category = "通用属性";
        public string Description = "";
        public string IconOrEmoji = "⚡";
        public string ColorHex = "#3498DB";

        public RecipeAbility() { }

        public RecipeAbility(string id, string name, string category = "通用属性", string description = "", string icon = "⚡", string colorHex = "#3498DB")
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Category = string.IsNullOrEmpty(category) ? "通用属性" : category;
            Description = description ?? string.Empty;
            IconOrEmoji = string.IsNullOrEmpty(icon) ? "⚡" : icon;
            ColorHex = string.IsNullOrEmpty(colorHex) ? "#3498DB" : colorHex;
        }

        public RecipeAbility Clone()
        {
            return new RecipeAbility(Id, Name, Category, Description, IconOrEmoji, ColorHex);
        }
    }

    /// <summary>
    /// 卡牌携带的默认能力点数条目。
    /// 所有卡牌均可挂载点数（不填即为0），并可配置换算系数（默认 100%）。
    /// </summary>
    [Serializable]
    public sealed class ItemAbilityContribution
    {
        /// <summary>
        /// 本卡牌所提供的能力标识（如 heat, cooking, strength）
        /// </summary>
        public string AbilityId = "";

        /// <summary>
        /// 默认携带点数（不填即为 0，运行时卡牌实例可受状态影响发生浮动，此处为默认配置值）
        /// </summary>
        public float BaseValue = 0f;

        /// <summary>
        /// 换算系数 / 贡献加权率（默认 1.0 即 100% 换算）
        /// 例如：0.5 表示 50% 效率折算，1.2 表示 120% 增幅加成
        /// </summary>
        public float Efficiency = 1.0f;

        /// <summary>
        /// 目标换算能力（可选跨能力折算，留空表示同名直达换算）
        /// </summary>
        public string TargetAbilityId = "";

        public string Note = "";

        public ItemAbilityContribution() { }

        public ItemAbilityContribution(string abilityId, float baseValue, float efficiency = 1.0f, string targetAbilityId = "", string note = "")
        {
            AbilityId = abilityId ?? string.Empty;
            BaseValue = baseValue;
            Efficiency = efficiency > 0f ? efficiency : 1.0f;
            TargetAbilityId = targetAbilityId ?? string.Empty;
            Note = note ?? string.Empty;
        }

        public ItemAbilityContribution Clone()
        {
            return new ItemAbilityContribution(AbilityId, BaseValue, Efficiency, TargetAbilityId, Note);
        }
    }

    /// <summary>
    /// 配方能力检定需求项（配方需要多少指定能力值才能成功或触发分支）
    /// </summary>
    [Serializable]
    public sealed class RecipeAbilityCheck
    {
        /// <summary>
        /// 检定的能力 ID（如 cooking, heat, forge）
        /// </summary>
        public string AbilityId = "";

        /// <summary>
        /// 检定所需的门槛数值
        /// </summary>
        public float RequiredValue = 1f;

        /// <summary>
        /// 比较判定符号（>=, ==, <=, >, <，默认 >=）
        /// </summary>
        public string Operator = ">=";

        /// <summary>
        /// 未达标时的分支说明或判定反馈（例如“烹饪失败烧糊”、“事件检定失败受伤”）
        /// </summary>
        public string FailureBranchNote = "";

        public string Note = "";

        public RecipeAbilityCheck() { }

        public RecipeAbilityCheck(string abilityId, float requiredValue, string op = ">=", string failureBranchNote = "", string note = "")
        {
            AbilityId = abilityId ?? string.Empty;
            RequiredValue = requiredValue;
            Operator = string.IsNullOrEmpty(op) ? ">=" : op;
            FailureBranchNote = failureBranchNote ?? string.Empty;
            Note = note ?? string.Empty;
        }

        public RecipeAbilityCheck Clone()
        {
            return new RecipeAbilityCheck(AbilityId, RequiredValue, Operator, FailureBranchNote, Note);
        }
    }

    /// <summary>
    /// 产出触发条件（用于支持能力检定达标成功分支 vs 未达标失败分支）
    /// </summary>
    public enum OutputTriggerCondition
    {
        /// <summary>
        /// 无论检定是否达标均产生（常规产出）
        /// </summary>
        Always = 0,

        /// <summary>
        /// 仅在能力检定成功/达标时产出（成功分支产物、完美品质产物）
        /// </summary>
        OnCheckSuccess = 1,

        /// <summary>
        /// 仅在能力检定未达标/失败时产出（失败分支产物、烧糊焦炭、损伤病痛卡）
        /// </summary>
        OnCheckFailure = 2
    }
}
