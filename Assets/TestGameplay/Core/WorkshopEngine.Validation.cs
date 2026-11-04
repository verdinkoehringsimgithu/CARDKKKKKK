using System;
using System.Collections.Generic;

namespace CardKKK.TestGameplay.Core
{
    public sealed partial class WorkshopEngine
    {
        private void ValidateDefinition()
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition), "测试玩法配置为空。");
            RequireText(definition.ContentVersion, "内容版本");
            if (definition.Items == null || definition.Items.Count == 0 ||
                definition.Recipes == null || definition.Gathers == null ||
                definition.Scenarios == null || definition.Scenarios.Count == 0)
                throw new ArgumentException("物品、工艺、采集与开局列表必须完整。");
            foreach (ItemDefinitionData item in definition.Items)
            {
                if (item == null) throw new ArgumentException("物品目录包含空项。");
                RequireText(item.Id, "物品标识"); RequireText(item.Name, "物品名称");
                if (items.ContainsKey(item.Id)) throw new ArgumentException("物品标识重复：" + item.Id);
                items.Add(item.Id, item);
            }
            var permanentTools = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecipeDefinitionData recipe in definition.Recipes)
            {
                if (recipe == null) throw new ArgumentException("工艺目录包含空项。");
                RequireText(recipe.Id, "工艺标识"); RequireText(recipe.Name, "工艺名称");
                if (recipes.ContainsKey(recipe.Id)) throw new ArgumentException("工艺标识重复：" + recipe.Id);
                if (recipe.FacilityId != "kiln" && recipe.FacilityId != "workbench")
                    throw new ArgumentException("工艺只能使用土窑或工作台：" + recipe.Name);
                if (recipe.PreparationMinutes <= 0 || recipe.ProcessMinutes < 0)
                    throw new ArgumentException("工艺准备时间必须为正，后台时间不能为负：" + recipe.Name);
                if ((long)recipe.PreparationMinutes + recipe.ProcessMinutes > int.MaxValue)
                    throw new ArgumentException("工艺总时长超出可保存范围：" + recipe.Name);
                ValidateAmounts(recipe.Inputs, recipe.Name + "的投入", true);
                ValidateAmounts(recipe.Outputs, recipe.Name + "的主产物", false);
                ValidateAmounts(recipe.Byproducts, recipe.Name + "的副产物", false);
                if (!recipe.InstallLining && recipe.Outputs.Count + recipe.Byproducts.Count == 0)
                    throw new ArgumentException("工艺必须产生物品或安装炉衬：" + recipe.Name);
                foreach (ItemAmount output in recipe.Outputs)
                    if (Contains(recipe.Byproducts, output.ItemId))
                        throw new ArgumentException("同一物品不能同时列为主产物与副产物：" + recipe.Name);
                if (!string.IsNullOrWhiteSpace(recipe.UnlockItemId) && !items.ContainsKey(recipe.UnlockItemId))
                    throw new ArgumentException("解锁物品不存在：" + recipe.Name);
                if (!string.IsNullOrWhiteSpace(recipe.RequiredToolId))
                {
                    if (!items.ContainsKey(recipe.RequiredToolId))
                        throw new ArgumentException("所需工具不存在：" + recipe.Name);
                    permanentTools.Add(recipe.RequiredToolId);
                }
                if (recipe.InstallLining && (recipe.FacilityId != "kiln" || recipe.ProcessMinutes != 0 ||
                    recipe.Outputs.Count != 0 || recipe.Byproducts.Count != 0))
                    throw new ArgumentException("炉衬必须在土窑由人物直接装好，不能另有产物或后台批次。");
                if (recipe.LinedCharcoalCost != -1)
                {
                    ItemAmount charcoal = null;
                    foreach (ItemAmount input in recipe.Inputs)
                        if (input.ItemId == CharcoalId) charcoal = input;
                    if (recipe.FacilityId != "kiln" || recipe.LinedCharcoalCost < 1 ||
                        charcoal == null || recipe.LinedCharcoalCost > charcoal.Quantity)
                        throw new ArgumentException("炉衬只能将已有木炭费用替换为正数且不能涨价：" + recipe.Name);
                }
                if (recipe.UniqueOutput)
                {
                    if (recipe.Outputs.Count == 0) throw new ArgumentException("唯一产物工艺缺少主产物：" + recipe.Name);
                    foreach (ItemAmount output in recipe.Outputs)
                    {
                        if (output.Quantity != 1) throw new ArgumentException("唯一工具每批只能产出一个：" + recipe.Name);
                        uniqueItems.Add(output.ItemId);
                    }
                }
                recipes.Add(recipe.Id, recipe);
            }
            foreach (GatherDefinitionData gather in definition.Gathers)
            {
                if (gather == null) throw new ArgumentException("采集目录包含空项。");
                RequireText(gather.Id, "采集标识"); RequireText(gather.Name, "采集名称"); RequireText(gather.SiteId, "地点标识");
                if (gathers.ContainsKey(gather.Id)) throw new ArgumentException("采集标识重复：" + gather.Id);
                ValidateAmounts(gather.Outputs, gather.Name + "的产物", true);
                if (gather.Minutes <= 0 || (gather.MinutesWithAxe != -1 && gather.MinutesWithAxe <= 0))
                    throw new ArgumentException("采集时间必须为正数：" + gather.Name);
                if (gather.MinutesWithAxe != -1)
                {
                    if (!items.ContainsKey(AxeId) || !Contains(gather.Outputs, WoodId))
                        throw new ArgumentException("铜斧加速仅用于木料采集。");
                    permanentTools.Add(AxeId);
                }
                foreach (ItemAmount output in gather.Outputs)
                    if (uniqueItems.Contains(output.ItemId))
                        throw new ArgumentException("唯一工具不能作为无限采集产物：" + gather.Name);
                gathers.Add(gather.Id, gather);
            }
            foreach (RecipeDefinitionData recipe in definition.Recipes)
            {
                foreach (ItemAmount input in recipe.Inputs)
                    if (permanentTools.Contains(input.ItemId))
                        throw new ArgumentException("本版工具不消耗，不能列为制作投入：" + recipe.Name);
                // 唯一工具不能通过另一种非唯一配方重复制造。
                if (!recipe.UniqueOutput)
                    foreach (ItemAmount output in recipe.Outputs)
                        if (uniqueItems.Contains(output.ItemId))
                            throw new ArgumentException("唯一工具的其他配方也必须限制重复制作：" + recipe.Name);
                foreach (ItemAmount byproduct in recipe.Byproducts)
                    if (uniqueItems.Contains(byproduct.ItemId))
                        throw new ArgumentException("唯一工具不能作为副产物：" + recipe.Name);
            }
            foreach (ScenarioData scenario in definition.Scenarios)
            {
                if (scenario == null) throw new ArgumentException("开局目录包含空项。");
                RequireText(scenario.Id, "开局标识"); RequireText(scenario.Name, "开局名称");
                if (scenarios.ContainsKey(scenario.Id)) throw new ArgumentException("开局标识重复：" + scenario.Id);
                ValidateAmounts(scenario.InitialInventory, scenario.Name + "的初始库存", false);
                foreach (ItemAmount entry in scenario.InitialInventory)
                    if (uniqueItems.Contains(entry.ItemId) && entry.Quantity > 1)
                        throw new ArgumentException("初始唯一工具不能超过一个：" + scenario.Name);
                ValidateRecipeIds(scenario.KnownRecipeIds, scenario.Name + "的已知工艺");
                ValidateRecipeIds(scenario.AvailableRecipeIds, scenario.Name + "的可用工艺");
                foreach (string recipeId in scenario.KnownRecipeIds)
                    if (!Available(scenario, recipeId))
                        throw new ArgumentException("已知工艺必须也在开局可用范围内：" + scenario.Name);
                if (scenario.GoalItemId == null || !items.ContainsKey(scenario.GoalItemId) || scenario.GoalQuantity <= 0)
                    throw new ArgumentException("目标物品与数量无效：" + scenario.Name);
                if (uniqueItems.Contains(scenario.GoalItemId) && scenario.GoalQuantity != 1)
                    throw new ArgumentException("唯一工具目标数量只能为一个：" + scenario.Name);
                if (scenario.RequireAxeGather && !items.ContainsKey(AxeId))
                    throw new ArgumentException("需要持斧采集的目标缺少铜斧定义。");
                scenarios.Add(scenario.Id, scenario);
            }
        }

        private void ValidateAmounts(List<ItemAmount> amounts, string context, bool required)
        {
            if (amounts == null || (required && amounts.Count == 0))
                throw new ArgumentException(context + "列表不能为空。");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemAmount amount in amounts)
                if (amount == null || amount.ItemId == null || !items.ContainsKey(amount.ItemId) ||
                    amount.Quantity <= 0 || !seen.Add(amount.ItemId))
                    throw new ArgumentException(context + "必须引用已存在物品，数量为正且不重复。");
        }

        private void ValidateRecipeIds(List<string> ids, string context)
        {
            if (ids == null) throw new ArgumentException(context + "列表为空引用。");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
                if (id == null || !recipes.ContainsKey(id) || !seen.Add(id))
                    throw new ArgumentException(context + "存在无效或重复工艺。");
        }

        private static void RequireText(string value, string context)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
                throw new ArgumentException(context + "必须为 1～128 个非空字符。");
        }

        private string ValidateState(WorkshopSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Inventory == null || snapshot.EverObtained == null || snapshot.Jobs == null)
                return "存档的库存、已知物品或工位数据不完整。";
            if (snapshot.Version != 1 || snapshot.ContentVersion != definition.ContentVersion)
                return "存档与当前测试玩法的内容版本不一致。";
            if (snapshot.ScenarioId == null || !scenarios.TryGetValue(snapshot.ScenarioId, out ScenarioData scenario))
                return "存档引用了不存在的开局。";
            if (snapshot.Minutes < 0 || snapshot.Revision < 0 || snapshot.WoodSpent < 0 || snapshot.AxeGatherCount < 0)
                return "存档中的时间、版本或累计数量不能为负。";
            if (snapshot.Inventory.Count > items.Count || snapshot.EverObtained.Count > items.Count || snapshot.Jobs.Count > 2)
                return "存档的数据数量超出本版规则范围。";
            var inventoryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemAmount entry in snapshot.Inventory)
            {
                if (entry == null || entry.ItemId == null || !items.ContainsKey(entry.ItemId) ||
                    entry.Quantity <= 0 || !inventoryIds.Add(entry.ItemId))
                    return "存档库存包含无效物品、数量或重复项。";
                if (uniqueItems.Contains(entry.ItemId) && entry.Quantity > 1)
                    return "存档中的唯一工具超过一个。";
            }
            var obtainedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in snapshot.EverObtained)
                if (id == null || !items.ContainsKey(id) || !obtainedIds.Add(id))
                    return "存档的曾获得物品记录无效或重复。";
            foreach (string id in inventoryIds)
                if (!obtainedIds.Contains(id)) return "库存物品缺少曾获得记录。";
            foreach (ItemAmount initial in scenario.InitialInventory)
                if (!obtainedIds.Contains(initial.ItemId)) return "存档缺少开局物品的获得记录。";
            var batchIds = new HashSet<string>(StringComparer.Ordinal);
            var facilities = new HashSet<string>(StringComparer.Ordinal);
            var uniqueProducing = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorkshopJob job in snapshot.Jobs)
            {
                if (job == null || string.IsNullOrWhiteSpace(job.BatchId) || job.BatchId.Length > 128 ||
                    !batchIds.Add(job.BatchId) || job.RecipeId == null || !recipes.TryGetValue(job.RecipeId, out RecipeDefinitionData recipe))
                    return "存档加工批次标识或工艺无效。";
                if (!Available(scenario, recipe.Id) || !Known(snapshot, scenario, recipe) || recipe.ProcessMinutes <= 0)
                    return "存档包含未开放或没有后台过程的加工批次。";
                if (job.FacilityId != recipe.FacilityId || !facilities.Add(job.FacilityId))
                    return "工位归属错误或同一工位存在多个批次。";
                if (job.StartedMinute < 0 || job.StartedMinute > snapshot.Minutes ||
                    job.FinishMinute <= snapshot.Minutes ||
                    (long)job.StartedMinute + recipe.ProcessMinutes != job.FinishMinute)
                    return "存档批次的开始与完成时间不合法。";
                if (!string.IsNullOrWhiteSpace(recipe.RequiredToolId) && Count(snapshot, recipe.RequiredToolId) < 1)
                    return "加工批次缺少所需工具。";
                foreach (ItemAmount output in recipe.Outputs)
                    if (uniqueItems.Contains(output.ItemId) &&
                        (Count(snapshot, output.ItemId) > 0 || !uniqueProducing.Add(output.ItemId)))
                        return "唯一工具重复拥有或重复制作。";
            }
            if (snapshot.KilnLined)
            {
                bool supported = false;
                foreach (RecipeDefinitionData recipe in definition.Recipes)
                {
                    if (!recipe.InstallLining || !Available(scenario, recipe.Id)) continue;
                    bool hasHistory = true;
                    foreach (ItemAmount input in recipe.Inputs)
                        if (!obtainedIds.Contains(input.ItemId)) hasHistory = false;
                    if (hasHistory) { supported = true; break; }
                }
                if (!supported) return "炉衬状态与开局工艺、材料记录不一致。";
            }
            if (snapshot.GoalComplete)
            {
                if (snapshot.CompletionMinutes < 0 || snapshot.CompletionMinutes > snapshot.Minutes ||
                    snapshot.CompletionWoodSpent < 0 || snapshot.CompletionWoodSpent > snapshot.WoodSpent ||
                    !obtainedIds.Contains(scenario.GoalItemId) ||
                    (scenario.RequireAxeGather && snapshot.AxeGatherCount == 0))
                    return "存档的完成记录不合法。";
            }
            else
            {
                if (snapshot.CompletionMinutes != 0 || snapshot.CompletionWoodSpent != 0)
                    return "未完成目标的存档不应有结算成绩。";
                if (Count(snapshot, scenario.GoalItemId) >= scenario.GoalQuantity &&
                    (!scenario.RequireAxeGather || snapshot.AxeGatherCount > 0))
                    return "存档目标状态与当前进度不一致。";
            }
            if (snapshot.AxeGatherCount > 0 && !obtainedIds.Contains(AxeId))
                return "持斧采集记录缺少铜斧获得记录。";
            return null;
        }
    }
}
