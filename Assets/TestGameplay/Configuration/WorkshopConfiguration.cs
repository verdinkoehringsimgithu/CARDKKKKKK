using System;
using System.Collections.Generic;
using CardKKK.TestGameplay.Core;
using UnityEngine;

namespace CardKKK.TestGameplay.Unity
{
    [Serializable]
    public sealed class WorkshopQuantitySetting
    {
        [InspectorName("物品 ID")] public string ItemId;
        [InspectorName("数量"), Min(1)] public int Quantity = 1;

        public ItemAmount ToData() => new ItemAmount(ItemId, Quantity);
    }

    [Serializable]
    public sealed class WorkshopItemSetting
    {
        [InspectorName("物品 ID")] public string Id;
        [InspectorName("名称")] public string DisplayName;
        [InspectorName("插画")] public Sprite Artwork;
    }

    [Serializable]
    public sealed class WorkshopScenerySetting
    {
        [InspectorName("工位或地点 ID")] public string Id;
        [InspectorName("名称")] public string DisplayName;
        [InspectorName("插画")] public Sprite Artwork;
    }

    [Serializable]
    public sealed class WorkshopRecipeSetting
    {
        [InspectorName("工艺 ID")] public string Id;
        [InspectorName("工艺名称")] public string DisplayName;
        [InspectorName("工位 ID")] public string FacilityId = "kiln";
        [InspectorName("认识工艺所需的曾获物品 ID")] public string UnlockItemId;
        [InspectorName("需要的工具 ID（不消耗）")] public string RequiredToolId;
        [InspectorName("投入")] public List<WorkshopQuantitySetting> Inputs = new List<WorkshopQuantitySetting>();
        [InspectorName("主产物")] public List<WorkshopQuantitySetting> Outputs = new List<WorkshopQuantitySetting>();
        [InspectorName("副产物")] public List<WorkshopQuantitySetting> Byproducts = new List<WorkshopQuantitySetting>();
        [InspectorName("人物准备分钟"), Min(1)] public int PreparationMinutes = 15;
        [InspectorName("后台加工分钟"), Min(0)] public int ProcessMinutes;
        [InspectorName("完成后安装炉衬")] public bool InstallLining;
        [InspectorName("有炉衬时替换木炭费用（-1 不替换）")] public int LinedCharcoalCost = -1;
        [InspectorName("产物只允许拥有一个")] public bool UniqueOutput;

        public RecipeDefinitionData ToData()
        {
            return new RecipeDefinitionData
            {
                Id = Id, Name = DisplayName, FacilityId = FacilityId,
                UnlockItemId = UnlockItemId, RequiredToolId = RequiredToolId,
                Inputs = WorkshopConfiguration.CopyAmounts(Inputs),
                Outputs = WorkshopConfiguration.CopyAmounts(Outputs),
                Byproducts = WorkshopConfiguration.CopyAmounts(Byproducts),
                PreparationMinutes = PreparationMinutes, ProcessMinutes = ProcessMinutes,
                InstallLining = InstallLining, LinedCharcoalCost = LinedCharcoalCost,
                UniqueOutput = UniqueOutput
            };
        }
    }

    [Serializable]
    public sealed class WorkshopGatherSetting
    {
        [InspectorName("采集 ID")] public string Id;
        [InspectorName("操作名称")] public string DisplayName;
        [InspectorName("地点 ID")] public string SiteId;
        [InspectorName("人物分钟"), Min(1)] public int Minutes = 30;
        [InspectorName("有铜斧时分钟（-1 不改变）")] public int MinutesWithAxe = -1;
        [InspectorName("采集产物")] public List<WorkshopQuantitySetting> Outputs = new List<WorkshopQuantitySetting>();

        public GatherDefinitionData ToData()
        {
            return new GatherDefinitionData
            {
                Id = Id, Name = DisplayName, SiteId = SiteId,
                Minutes = Minutes, MinutesWithAxe = MinutesWithAxe,
                Outputs = WorkshopConfiguration.CopyAmounts(Outputs)
            };
        }
    }

    /// <summary>独立测试玩法的数据与表现资源，不读写自由牌桌配置。</summary>
    [CreateAssetMenu(menuName = "测试玩法/工坊配置", fileName = "测试工坊配置")]
    public sealed class WorkshopConfiguration : ScriptableObject
    {
        [Header("规则与开局")]
        [InspectorName("内容版本")] public string ContentVersion = "workshop-1";
        [InspectorName("默认开局 ID")] public string DefaultScenarioId = "full";
        [InspectorName("物品")] public List<WorkshopItemSetting> Items = new List<WorkshopItemSetting>();
        [InspectorName("工位与地点")] public List<WorkshopScenerySetting> Scenery = new List<WorkshopScenerySetting>();
        [InspectorName("制作工艺")] public List<WorkshopRecipeSetting> Recipes = new List<WorkshopRecipeSetting>();
        [InspectorName("采集操作")] public List<WorkshopGatherSetting> Gathers = new List<WorkshopGatherSetting>();
        [InspectorName("开局配置")] public List<WorkshopScenario> Scenarios = new List<WorkshopScenario>();

        [Header("测试玩法外观")]
        [InspectorName("字体")] public Font UiFont;
        [InspectorName("卡框")] public Sprite CardFrame;
        [InspectorName("桌面图像")] public Sprite TableSurface;
        [InspectorName("无桌面图时的背景色")] public Color BackgroundColor = new Color(0.08f, 0.13f, 0.12f, 1f);
        [InspectorName("无卡框时的卡面色")] public Color CardColor = new Color(0.88f, 0.83f, 0.71f, 1f);
        [InspectorName("卡面字色")] public Color TextColor = new Color(0.15f, 0.18f, 0.15f, 1f);
        [InspectorName("次要字色")] public Color MutedTextColor = new Color(0.67f, 0.73f, 0.67f, 1f);
        [InspectorName("选中色")] public Color AccentColor = new Color(0.82f, 0.64f, 0.34f, 1f);
        [InspectorName("过渡秒数"), Range(0.05f, 0.6f)] public float TransitionSeconds = 0.18f;

        public WorkshopDefinition ToDefinition()
        {
            var result = new WorkshopDefinition { ContentVersion = ContentVersion };
            foreach (WorkshopItemSetting item in Items)
            {
                if (item == null) throw new InvalidOperationException("物品目录含空项。");
                result.Items.Add(new ItemDefinitionData { Id = item.Id, Name = item.DisplayName });
            }
            foreach (WorkshopRecipeSetting recipe in Recipes)
            {
                if (recipe == null) throw new InvalidOperationException("制作工艺含空项。");
                result.Recipes.Add(recipe.ToData());
            }
            foreach (WorkshopGatherSetting gather in Gathers)
            {
                if (gather == null) throw new InvalidOperationException("采集操作含空项。");
                result.Gathers.Add(gather.ToData());
            }
            foreach (WorkshopScenario scenario in Scenarios)
            {
                if (scenario == null) throw new InvalidOperationException("开局配置含空引用。");
                result.Scenarios.Add(scenario.ToData());
            }
            return result;
        }

        public bool Validate(out string error)
        {
            try
            {
                if (Items == null || Recipes == null || Gathers == null || Scenarios == null || Scenery == null)
                    throw new InvalidOperationException("配置列表不能为 null。");
                var visualIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (WorkshopItemSetting item in Items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.DisplayName))
                        throw new InvalidOperationException("物品 ID 与名称不能为空。");
                    if (!visualIds.Add(item.Id)) throw new InvalidOperationException("物品 ID 重复：" + item.Id);
                }
                foreach (WorkshopScenerySetting scenery in Scenery)
                {
                    if (scenery == null || string.IsNullOrWhiteSpace(scenery.Id) || string.IsNullOrWhiteSpace(scenery.DisplayName))
                        throw new InvalidOperationException("工位或地点 ID 与名称不能为空。");
                    if (!visualIds.Add(scenery.Id)) throw new InvalidOperationException("对象 ID 重复：" + scenery.Id);
                }
                foreach (string required in new[] { "workbench", "kiln", "forest", "riverbank", "mine" })
                    if (!Scenery.Exists(entry => entry.Id == required))
                        throw new InvalidOperationException("本版须保留两个工位和三个地点的稳定 ID：" + required);
                foreach (WorkshopRecipeSetting recipe in Recipes)
                    if (recipe == null || !visualIds.Contains(recipe.FacilityId))
                        throw new InvalidOperationException("工艺引用了未配置外观的工位。");
                foreach (WorkshopGatherSetting gather in Gathers)
                    if (gather == null || !visualIds.Contains(gather.SiteId) ||
                        (gather.SiteId != "forest" && gather.SiteId != "riverbank" && gather.SiteId != "mine"))
                        throw new InvalidOperationException("采集引用了未配置外观的地点。");
                WorkshopDefinition definition = ToDefinition();
                bool hasDefault = false;
                foreach (ScenarioData scenario in definition.Scenarios)
                {
                    new WorkshopEngine(definition, scenario.Id);
                    if (scenario.Id == DefaultScenarioId) hasDefault = true;
                }
                if (!hasDefault) throw new InvalidOperationException("默认开局 ID 不在开局列表中。");
                if (float.IsNaN(TransitionSeconds) || float.IsInfinity(TransitionSeconds) || TransitionSeconds < 0.05f || TransitionSeconds > 0.6f)
                    throw new InvalidOperationException("过渡秒数必须在 0.05 到 0.6 之间。");
                error = "";
                return true;
            }
            catch (Exception exception)
            {
                error = "测试玩法配置无效：" + exception.Message;
                return false;
            }
        }

        public Sprite ArtworkFor(string id)
        {
            foreach (WorkshopItemSetting item in Items) if (item != null && item.Id == id) return item.Artwork;
            foreach (WorkshopScenerySetting scenery in Scenery) if (scenery != null && scenery.Id == id) return scenery.Artwork;
            return null;
        }

        public string DisplayNameFor(string id)
        {
            foreach (WorkshopItemSetting item in Items) if (item != null && item.Id == id) return item.DisplayName;
            foreach (WorkshopScenerySetting scenery in Scenery) if (scenery != null && scenery.Id == id) return scenery.DisplayName;
            return "未知物件";
        }

        public static List<ItemAmount> CopyAmounts(List<WorkshopQuantitySetting> source)
        {
            if (source == null) throw new InvalidOperationException("物品数量列表不能为 null。");
            var result = new List<ItemAmount>(source.Count);
            foreach (WorkshopQuantitySetting amount in source)
            {
                if (amount == null) throw new InvalidOperationException("物品数量列表含空项。");
                result.Add(amount.ToData());
            }
            return result;
        }
    }
}
