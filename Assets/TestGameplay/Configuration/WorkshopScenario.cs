using System.Collections.Generic;
using CardKKK.TestGameplay.Core;
using UnityEngine;

namespace CardKKK.TestGameplay.Unity
{
    [CreateAssetMenu(menuName = "测试玩法/开局配置", fileName = "新开局")]
    public sealed class WorkshopScenario : ScriptableObject
    {
        [InspectorName("开局 ID")] public string Id;
        [InspectorName("开局名称")] public string DisplayName;
        [InspectorName("起始库存")] public List<WorkshopQuantitySetting> InitialInventory = new List<WorkshopQuantitySetting>();
        [InspectorName("开局已知工艺 ID")] public List<string> KnownRecipeIds = new List<string>();
        [InspectorName("本开局可用工艺 ID（空为全部）")] public List<string> AvailableRecipeIds = new List<string>();
        [InspectorName("目标物品 ID")] public string GoalItemId;
        [InspectorName("目标数量"), Min(1)] public int GoalQuantity = 1;
        [InspectorName("目标还需持铜斧采集木料")] public bool RequireAxeGather;

        public ScenarioData ToData()
        {
            return new ScenarioData
            {
                Id = Id, Name = DisplayName,
                InitialInventory = WorkshopConfiguration.CopyAmounts(InitialInventory),
                KnownRecipeIds = new List<string>(KnownRecipeIds ?? new List<string>()),
                AvailableRecipeIds = new List<string>(AvailableRecipeIds ?? new List<string>()),
                GoalItemId = GoalItemId, GoalQuantity = GoalQuantity,
                RequireAxeGather = RequireAxeGather
            };
        }
    }
}
