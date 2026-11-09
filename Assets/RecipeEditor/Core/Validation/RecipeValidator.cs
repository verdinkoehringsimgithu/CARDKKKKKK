using System;
using System.Collections.Generic;

namespace CardKKK.RecipeEditor.Core
{
    public enum ValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    [Serializable]
    public sealed class ValidationMessage
    {
        public ValidationSeverity Severity;
        public string SourceType; // "Tag", "Item", "Location", "Recipe", "Database"
        public string SourceId;
        public string Message;

        public ValidationMessage(ValidationSeverity severity, string sourceType, string sourceId, string message)
        {
            Severity = severity;
            SourceType = sourceType ?? "";
            SourceId = sourceId ?? "";
            Message = message ?? "";
        }

        public override string ToString()
        {
            string prefix = Severity == ValidationSeverity.Error ? "[错误]" : (Severity == ValidationSeverity.Warning ? "[警告]" : "[提示]");
            return $"{prefix} [{SourceType}: {SourceId}] {Message}";
        }
    }

    public static class RecipeValidator
    {
        public static List<ValidationMessage> Validate(RecipeDatabaseData db)
        {
            var messages = new List<ValidationMessage>();
            if (db == null)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Error, "Database", "", "数据库对象为空。"));
                return messages;
            }

            var tagIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var abilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var locationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var recipeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var referencedTagIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. 校验标签
            if (db.Tags != null)
            {
                for (int i = 0; i < db.Tags.Count; i++)
                {
                    var tag = db.Tags[i];
                    if (tag == null)
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Tag", $"Index_{i}", "标签列表中存在空项。"));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(tag.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Tag", tag.Name, "标签 ID 不能为空或纯空白。"));
                    }
                    else if (!tagIds.Add(tag.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Tag", tag.Id, $"存在重复的标签 ID: '{tag.Id}'。"));
                    }

                    if (string.IsNullOrWhiteSpace(tag.Name))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Tag", tag.Id, $"标签 '{tag.Id}' 未设置显示名称。"));
                    }
                }
            }

            // 1.5 校验能力池
            if (db.Abilities != null)
            {
                for (int i = 0; i < db.Abilities.Count; i++)
                {
                    var ab = db.Abilities[i];
                    if (ab == null)
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Ability", $"Index_{i}", "能力列表中存在空项。"));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(ab.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Ability", ab.Name, "能力 ID 不能为空或纯空白。"));
                    }
                    else if (!abilityIds.Add(ab.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Ability", ab.Id, $"存在重复的能力 ID: '{ab.Id}'。"));
                    }

                    if (string.IsNullOrWhiteSpace(ab.Name))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Ability", ab.Id, $"能力 '{ab.Id}' 未设置显示名称。"));
                    }
                }
            }

            // 2. 校验物品
            if (db.Items != null)
            {
                for (int i = 0; i < db.Items.Count; i++)
                {
                    var item = db.Items[i];
                    if (item == null)
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Item", $"Index_{i}", "物品列表中存在空项。"));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Item", item.Name, "物品 ID 不能为空或纯空白。"));
                    }
                    else if (!itemIds.Add(item.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Item", item.Id, $"存在重复的物品 ID: '{item.Id}'。"));
                    }

                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Item", item.Id, $"物品 '{item.Id}' 未设置显示名称。"));
                    }

                    if (item.MaxStack <= 0)
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Item", item.Id, $"物品 '{item.Name}' 堆叠上限小于等于 0。"));
                    }

                    // 校验绑定的标签有效性
                    if (item.TagIds != null)
                    {
                        foreach (var tagId in item.TagIds)
                        {
                            if (string.IsNullOrEmpty(tagId)) continue;
                            referencedTagIds.Add(tagId);
                            if (!tagIds.Contains(tagId))
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Error, "Item", item.Id, $"物品 '{item.Name}' 引用了不存在的标签 ID: '{tagId}'。"));
                            }
                        }
                    }

                    if (item.AbilityContributions != null)
                    {
                        foreach (var ac in item.AbilityContributions)
                        {
                            if (ac == null) continue;
                            if (string.IsNullOrEmpty(ac.AbilityId))
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Item", item.Id, $"物品 '{item.Name}' 存在未指定能力 ID 的能力点数配置。"));
                            }
                            else if (abilityIds.Count > 0 && !abilityIds.Contains(ac.AbilityId))
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Item", item.Id, $"物品 '{item.Name}' 挂载的能力 '{ac.AbilityId}' 在全局能力池中未定义。"));
                            }
                            if (ac.Efficiency <= 0f)
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Item", item.Id, $"物品 '{item.Name}' 能力 '{ac.AbilityId}' 换算率必须大于 0。"));
                            }
                        }
                    }
                }
            }

            // 3. 校验合成场所（包含物品作为场所）
            if (db.Locations != null)
            {
                for (int i = 0; i < db.Locations.Count; i++)
                {
                    var loc = db.Locations[i];
                    if (loc == null)
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Location", $"Index_{i}", "合成场所列表中存在空项。"));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(loc.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Location", loc.Name, "合成场所 ID 不能为空。"));
                    }
                    else if (!locationIds.Add(loc.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Location", loc.Id, $"存在重复的场所 ID: '{loc.Id}'。"));
                    }

                    if (loc.Capacity <= 0)
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Location", loc.Id, $"场所 '{loc.Name}' 工位容量小于等于 0。"));
                    }

                    if (loc.DurabilityCost < 0)
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Location", loc.Id, $"场所 '{loc.Name}' 设施损耗度不能为负数。"));
                    }

                    // 如果是以物品作为合成场所，校验该物品是否存在
                    if (loc.SourceType == LocationSourceType.Item)
                    {
                        if (string.IsNullOrEmpty(loc.BoundItemId))
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Error, "Location", loc.Id, $"场所 '{loc.Name}' 设为物品场所，但未指定关联的物品 ID。"));
                        }
                        else if (!itemIds.Contains(loc.BoundItemId))
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Error, "Location", loc.Id, $"场所 '{loc.Name}' 关联的物品 ID '{loc.BoundItemId}' 在物品库中不存在。"));
                        }
                        else
                        {
                            usedItemIds.Add(loc.BoundItemId);
                        }
                    }
                }
            }

            // 4. 校验配方（合成配方与自身转变配方）
            if (db.Recipes != null)
            {
                for (int i = 0; i < db.Recipes.Count; i++)
                {
                    var recipe = db.Recipes[i];
                    if (recipe == null)
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", $"Index_{i}", "配方列表中存在空项。"));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(recipe.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Name, "配方 ID 不能为空。"));
                    }
                    else if (!recipeIds.Add(recipe.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"存在重复的配方 ID: '{recipe.Id}'。"));
                    }

                    // 检查发生场所引用
                    if (!string.IsNullOrEmpty(recipe.LocationId) && !locationIds.Contains(recipe.LocationId))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 指定的发生场所 '{recipe.LocationId}' 在场所库中不存在。"));
                    }

                    // 检查配方能力检定需求
                    if (recipe.AbilityChecks != null)
                    {
                        foreach (var chk in recipe.AbilityChecks)
                        {
                            if (chk == null) continue;
                            if (string.IsNullOrEmpty(chk.AbilityId))
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 存在未指定能力 ID 的检定要求。"));
                            }
                            else if (abilityIds.Count > 0 && !abilityIds.Contains(chk.AbilityId))
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 检定的能力 '{chk.AbilityId}' 在全局能力池中未定义。"));
                            }
                        }
                    }

                    // 检查在场操作个体/人员需求
                    if (recipe.Operators != null)
                    {
                        foreach (var op in recipe.Operators)
                        {
                            if (op == null) continue;
                            if (op.Quantity <= 0)
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 在场操作者数量必须大于 0。"));
                            }

                            if (op.MatchType == IngredientMatchType.ExactItem)
                            {
                                if (string.IsNullOrEmpty(op.TargetId) || !itemIds.Contains(op.TargetId))
                                {
                                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 在场操作者引用的物品 '{op.TargetId}' 不存在。"));
                                }
                                else
                                {
                                    usedItemIds.Add(op.TargetId);
                                }
                            }
                            else if (op.MatchType == IngredientMatchType.ByTag)
                            {
                                if (string.IsNullOrEmpty(op.TargetId) || !tagIds.Contains(op.TargetId))
                                {
                                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 在场操作者引用的标签 '{op.TargetId}' 不存在。"));
                                }
                                else
                                {
                                    referencedTagIds.Add(op.TargetId);
                                }
                            }
                        }
                    }

                    if (recipe.Type == RecipeType.Transformation)
                    {
                        // 自身转变配方检查
                        if (string.IsNullOrWhiteSpace(recipe.SourceItemId))
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"转变配方 '{recipe.Name}' 未指定转变主体物品（源物品 A）。"));
                        }
                        else if (!itemIds.Contains(recipe.SourceItemId))
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"转变配方 '{recipe.Name}' 的源物品 '{recipe.SourceItemId}' 在物品库中不存在。"));
                        }
                        else
                        {
                            usedItemIds.Add(recipe.SourceItemId);
                        }

                        if (recipe.SourceQuantity <= 0)
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"转变配方 '{recipe.Name}' 的源物品消耗数量必须大于 0。"));
                        }

                        if (string.IsNullOrWhiteSpace(recipe.TargetItemId))
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"转变配方 '{recipe.Name}' 未指定转变目标物品（目标物品 B）。"));
                        }
                        else if (!itemIds.Contains(recipe.TargetItemId))
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"转变配方 '{recipe.Name}' 的目标产物 '{recipe.TargetItemId}' 在物品库中不存在。"));
                        }
                        else
                        {
                            usedItemIds.Add(recipe.TargetItemId);
                        }

                        if (recipe.TargetQuantity <= 0)
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"转变配方 '{recipe.Name}' 的目标产物数量必须大于 0。"));
                        }

                        if (!string.IsNullOrEmpty(recipe.SourceItemId) && recipe.SourceItemId.Equals(recipe.TargetItemId, StringComparison.OrdinalIgnoreCase) && (recipe.TransformationConditions == null || recipe.TransformationConditions.Count == 0))
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Recipe", recipe.Id, $"转变配方 '{recipe.Name}' 源物品与目标物品相同，且无附加转变条件，可能为冗余配方。"));
                        }

                        // 转变条件校验
                        if (recipe.TransformationConditions != null)
                        {
                            foreach (var cond in recipe.TransformationConditions)
                            {
                                if (cond == null) continue;
                                if (cond.Type == ConditionType.RequireTool || cond.Type == ConditionType.RequireMaterial || cond.Type == ConditionType.RequireOperator)
                                {
                                    if (!string.IsNullOrEmpty(cond.TargetId))
                                    {
                                        bool isItem = itemIds.Contains(cond.TargetId);
                                        bool isTag = tagIds.Contains(cond.TargetId);
                                        if (!isItem && !isTag)
                                        {
                                            messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Recipe", recipe.Id, $"转变配方 '{recipe.Name}' 条件引用的对象 '{cond.TargetId}' 既非物品也非标签。"));
                                        }
                                        else
                                        {
                                            if (isItem) usedItemIds.Add(cond.TargetId);
                                            if (isTag) referencedTagIds.Add(cond.TargetId);
                                        }
                                    }
                                }
                            }
                        }

                        // 伴生产物校验
                        if (recipe.TransformationByproducts != null)
                        {
                            foreach (var bp in recipe.TransformationByproducts)
                            {
                                if (bp == null) continue;
                                if (string.IsNullOrEmpty(bp.ItemId) || !itemIds.Contains(bp.ItemId))
                                {
                                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"转变配方 '{recipe.Name}' 伴生副产物 '{bp.ItemId}' 在物品库中不存在。"));
                                }
                                else
                                {
                                    usedItemIds.Add(bp.ItemId);
                                }
                            }
                        }
                    }
                    else
                    {
                        // 标准多对多合成配方检查
                        if (recipe.Inputs == null || recipe.Inputs.Count == 0)
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Recipe", recipe.Id, $"标准配方 '{recipe.Name}' 没有原料输入项（等式左侧为空）。"));
                        }
                        else
                        {
                            foreach (var input in recipe.Inputs)
                            {
                                if (input == null) continue;
                                if (input.Quantity <= 0)
                                {
                                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 输入材料数量必须大于 0。"));
                                }

                                if (input.DurabilityCost < 0)
                                {
                                    messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 材料 '{input.TargetId}' 耐久损耗不能为负数。"));
                                }

                                if (input.MatchType == IngredientMatchType.ExactItem)
                                {
                                    if (string.IsNullOrEmpty(input.TargetId) || !itemIds.Contains(input.TargetId))
                                    {
                                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 输入材料引用的物品 '{input.TargetId}' 不存在。"));
                                    }
                                    else
                                    {
                                        usedItemIds.Add(input.TargetId);
                                    }
                                }
                                else if (input.MatchType == IngredientMatchType.ByTag)
                                {
                                    if (string.IsNullOrEmpty(input.TargetId) || !tagIds.Contains(input.TargetId))
                                    {
                                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 输入材料引用的标签 '{input.TargetId}' 不存在。"));
                                    }
                                    else
                                    {
                                        referencedTagIds.Add(input.TargetId);
                                    }
                                }
                            }
                        }

                        if (recipe.Outputs == null || recipe.Outputs.Count == 0)
                        {
                            messages.Add(new ValidationMessage(ValidationSeverity.Warning, "Recipe", recipe.Id, $"标准配方 '{recipe.Name}' 没有产物（等式右侧为空）。"));
                        }
                        else
                        {
                            foreach (var output in recipe.Outputs)
                            {
                                if (output == null) continue;
                                if (output.Quantity <= 0)
                                {
                                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 产出物数量必须大于 0。"));
                                }

                                if (string.IsNullOrEmpty(output.ItemId) || !itemIds.Contains(output.ItemId))
                                {
                                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "Recipe", recipe.Id, $"配方 '{recipe.Name}' 产出物引用的物品 '{output.ItemId}' 不存在。"));
                                }
                                else
                                {
                                    usedItemIds.Add(output.ItemId);
                                }
                            }
                        }
                    }
                }
            }

            // 5. 校验副产线（伴生修正与产出覆盖规则）
            var auxIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (db.AuxiliaryLines != null)
            {
                for (int i = 0; i < db.AuxiliaryLines.Count; i++)
                {
                    var aux = db.AuxiliaryLines[i];
                    if (aux == null)
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "AuxiliaryLine", $"Index_{i}", "副产线列表中存在空项。"));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(aux.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "AuxiliaryLine", aux.Name, "副产线 ID 不能为空。"));
                    }
                    else if (!auxIds.Add(aux.Id))
                    {
                        messages.Add(new ValidationMessage(ValidationSeverity.Error, "AuxiliaryLine", aux.Id, $"存在重复的副产线 ID: '{aux.Id}'。"));
                    }

                    // 检查匹配标签
                    if (aux.MatchTagIds != null)
                    {
                        foreach (var mt in aux.MatchTagIds)
                        {
                            if (!string.IsNullOrEmpty(mt) && !tagIds.Contains(mt))
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Warning, "AuxiliaryLine", aux.Id, $"副产线 '{aux.Name}' 匹配的标签 '{mt}' 在标签池中不存在。"));
                            }
                            else if (!string.IsNullOrEmpty(mt))
                            {
                                referencedTagIds.Add(mt);
                            }
                        }
                    }

                    // 检查匹配物品
                    if (aux.MatchItemIds != null)
                    {
                        foreach (var mi in aux.MatchItemIds)
                        {
                            if (!string.IsNullOrEmpty(mi) && !itemIds.Contains(mi))
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Warning, "AuxiliaryLine", aux.Id, $"副产线 '{aux.Name}' 匹配的物品 '{mi}' 在物品库中不存在。"));
                            }
                            else if (!string.IsNullOrEmpty(mi))
                            {
                                usedItemIds.Add(mi);
                            }
                        }
                    }

                    // 检查匹配工位
                    if (aux.MatchLocationIds != null)
                    {
                        foreach (var ml in aux.MatchLocationIds)
                        {
                            if (!string.IsNullOrEmpty(ml) && !locationIds.Contains(ml))
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Warning, "AuxiliaryLine", aux.Id, $"副产线 '{aux.Name}' 匹配的工位 '{ml}' 在场所库中不存在。"));
                            }
                        }
                    }

                    // 检查额外投入项
                    if (aux.ExtraInputs != null)
                    {
                        foreach (var inp in aux.ExtraInputs)
                        {
                            if (inp == null) continue;
                            if (inp.MatchType == IngredientMatchType.ExactItem)
                            {
                                if (!string.IsNullOrEmpty(inp.TargetId) && !itemIds.Contains(inp.TargetId))
                                {
                                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "AuxiliaryLine", aux.Id, $"副产线 '{aux.Name}' 额外投入引用的物品 '{inp.TargetId}' 不存在。"));
                                }
                                else if (!string.IsNullOrEmpty(inp.TargetId))
                                {
                                    usedItemIds.Add(inp.TargetId);
                                }
                            }
                            else if (inp.MatchType == IngredientMatchType.ByTag)
                            {
                                if (!string.IsNullOrEmpty(inp.TargetId) && !tagIds.Contains(inp.TargetId))
                                {
                                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "AuxiliaryLine", aux.Id, $"副产线 '{aux.Name}' 额外投入引用的标签 '{inp.TargetId}' 不存在。"));
                                }
                                else if (!string.IsNullOrEmpty(inp.TargetId))
                                {
                                    referencedTagIds.Add(inp.TargetId);
                                }
                            }
                        }
                    }

                    // 检查产出修饰项
                    if (aux.OutputModifiers != null)
                    {
                        foreach (var mod in aux.OutputModifiers)
                        {
                            if (mod == null) continue;
                            if (string.IsNullOrEmpty(mod.ItemId) || !itemIds.Contains(mod.ItemId))
                            {
                                messages.Add(new ValidationMessage(ValidationSeverity.Error, "AuxiliaryLine", aux.Id, $"副产线 '{aux.Name}' 产出修饰引用的物品 '{mod.ItemId}' 在物品库中不存在。"));
                            }
                            else
                            {
                                usedItemIds.Add(mod.ItemId);
                            }
                        }
                    }
                }
            }

            // 6. 孤儿标签与闲置物品提示（Warning）
            foreach (var tid in tagIds)
            {
                if (!referencedTagIds.Contains(tid))
                {
                    messages.Add(new ValidationMessage(ValidationSeverity.Info, "Tag", tid, $"标签 '{tid}' 尚未被任何物品或配方使用。"));
                }
            }

            return messages;
        }
    }
}
