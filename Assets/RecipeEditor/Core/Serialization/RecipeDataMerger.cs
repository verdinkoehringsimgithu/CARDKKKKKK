using System;
using System.Collections.Generic;

namespace CardKKK.RecipeEditor.Core
{
    public enum MergeConflictPolicy
    {
        /// <summary>
        /// 遇到相同 ID 时跳过传入的新数据，保留现有数据
        /// </summary>
        Skip = 0,

        /// <summary>
        /// 遇到相同 ID 时用传入的新数据覆盖现有数据
        /// </summary>
        Overwrite = 1,

        /// <summary>
        /// 遇到相同 ID 时自动生成新 ID（例如 id_imported_1）
        /// </summary>
        RenameNew = 2
    }

    public sealed class MergeReport
    {
        public int TagsAdded;
        public int TagsOverwritten;
        public int AbilitiesAdded;
        public int AbilitiesOverwritten;
        public int ItemsAdded;
        public int ItemsOverwritten;
        public int LocationsAdded;
        public int LocationsOverwritten;
        public int RecipesAdded;
        public int RecipesOverwritten;
        public int AuxiliaryLinesAdded;
        public int AuxiliaryLinesOverwritten;

        public override string ToString()
        {
            return $"新增: 标签 {TagsAdded}, 能力 {AbilitiesAdded}, 物品 {ItemsAdded}, 场所 {LocationsAdded}, 配方 {RecipesAdded}, 副产线 {AuxiliaryLinesAdded} | 覆盖: 标签 {TagsOverwritten}, 能力 {AbilitiesOverwritten}, 物品 {ItemsOverwritten}, 场所 {LocationsOverwritten}, 配方 {RecipesOverwritten}, 副产线 {AuxiliaryLinesOverwritten}";
        }
    }

    /// <summary>
    /// 配方数据智能合并器，负责在导入时将外部数据库安全地合并到当前数据库中。
    /// </summary>
    public static class RecipeDataMerger
    {
        public static MergeReport Merge(RecipeDatabaseData target, RecipeDatabaseData source, MergeConflictPolicy policy)
        {
            var report = new MergeReport();
            if (target == null || source == null) return report;

            // 1. 合并标签
            if (source.Tags != null)
            {
                foreach (var sTag in source.Tags)
                {
                    if (sTag == null || string.IsNullOrEmpty(sTag.Id)) continue;
                    var existing = target.FindTag(sTag.Id);
                    if (existing == null)
                    {
                        target.Tags.Add(sTag.Clone());
                        report.TagsAdded++;
                    }
                    else if (policy == MergeConflictPolicy.Overwrite)
                    {
                        int index = target.Tags.IndexOf(existing);
                        target.Tags[index] = sTag.Clone();
                        report.TagsOverwritten++;
                    }
                    else if (policy == MergeConflictPolicy.RenameNew)
                    {
                        var clone = sTag.Clone();
                        clone.Id = GenerateUniqueId(sTag.Id, id => target.FindTag(id) != null);
                        target.Tags.Add(clone);
                        report.TagsAdded++;
                    }
                }
            }

            // 1.5 合并能力池
            if (source.Abilities != null)
            {
                foreach (var sAb in source.Abilities)
                {
                    if (sAb == null || string.IsNullOrEmpty(sAb.Id)) continue;
                    var existing = target.FindAbility(sAb.Id);
                    if (existing == null)
                    {
                        target.Abilities.Add(sAb.Clone());
                        report.AbilitiesAdded++;
                    }
                    else if (policy == MergeConflictPolicy.Overwrite)
                    {
                        int index = target.Abilities.IndexOf(existing);
                        target.Abilities[index] = sAb.Clone();
                        report.AbilitiesOverwritten++;
                    }
                    else if (policy == MergeConflictPolicy.RenameNew)
                    {
                        var clone = sAb.Clone();
                        clone.Id = GenerateUniqueId(sAb.Id, id => target.FindAbility(id) != null);
                        target.Abilities.Add(clone);
                        report.AbilitiesAdded++;
                    }
                }
            }

            // 2. 合并物品
            if (source.Items != null)
            {
                foreach (var sItem in source.Items)
                {
                    if (sItem == null || string.IsNullOrEmpty(sItem.Id)) continue;
                    var existing = target.FindItem(sItem.Id);
                    if (existing == null)
                    {
                        target.Items.Add(sItem.Clone());
                        report.ItemsAdded++;
                    }
                    else if (policy == MergeConflictPolicy.Overwrite)
                    {
                        int index = target.Items.IndexOf(existing);
                        target.Items[index] = sItem.Clone();
                        report.ItemsOverwritten++;
                    }
                    else if (policy == MergeConflictPolicy.RenameNew)
                    {
                        var clone = sItem.Clone();
                        clone.Id = GenerateUniqueId(sItem.Id, id => target.FindItem(id) != null);
                        target.Items.Add(clone);
                        report.ItemsAdded++;
                    }
                }
            }

            // 3. 合并场所
            if (source.Locations != null)
            {
                foreach (var sLoc in source.Locations)
                {
                    if (sLoc == null || string.IsNullOrEmpty(sLoc.Id)) continue;
                    var existing = target.FindLocation(sLoc.Id);
                    if (existing == null)
                    {
                        target.Locations.Add(sLoc.Clone());
                        report.LocationsAdded++;
                    }
                    else if (policy == MergeConflictPolicy.Overwrite)
                    {
                        int index = target.Locations.IndexOf(existing);
                        target.Locations[index] = sLoc.Clone();
                        report.LocationsOverwritten++;
                    }
                    else if (policy == MergeConflictPolicy.RenameNew)
                    {
                        var clone = sLoc.Clone();
                        clone.Id = GenerateUniqueId(sLoc.Id, id => target.FindLocation(id) != null);
                        target.Locations.Add(clone);
                        report.LocationsAdded++;
                    }
                }
            }

            // 4. 合并配方
            if (source.Recipes != null)
            {
                foreach (var sRecipe in source.Recipes)
                {
                    if (sRecipe == null || string.IsNullOrEmpty(sRecipe.Id)) continue;
                    var existing = target.FindRecipe(sRecipe.Id);
                    if (existing == null)
                    {
                        target.Recipes.Add(sRecipe.Clone());
                        report.RecipesAdded++;
                    }
                    else if (policy == MergeConflictPolicy.Overwrite)
                    {
                        int index = target.Recipes.IndexOf(existing);
                        target.Recipes[index] = sRecipe.Clone();
                        report.RecipesOverwritten++;
                    }
                    else if (policy == MergeConflictPolicy.RenameNew)
                    {
                        var clone = sRecipe.Clone();
                        clone.Id = GenerateUniqueId(sRecipe.Id, id => target.FindRecipe(id) != null);
                        target.Recipes.Add(clone);
                        report.RecipesAdded++;
                    }
                }
            }

            // 5. 合并副产线
            if (source.AuxiliaryLines != null)
            {
                if (target.AuxiliaryLines == null) target.AuxiliaryLines = new List<RecipeAuxiliaryLine>();
                foreach (var sAux in source.AuxiliaryLines)
                {
                    if (sAux == null || string.IsNullOrEmpty(sAux.Id)) continue;
                    var existing = target.FindAuxiliaryLine(sAux.Id);
                    if (existing == null)
                    {
                        target.AuxiliaryLines.Add(sAux.Clone());
                        report.AuxiliaryLinesAdded++;
                    }
                    else if (policy == MergeConflictPolicy.Overwrite)
                    {
                        int index = target.AuxiliaryLines.IndexOf(existing);
                        target.AuxiliaryLines[index] = sAux.Clone();
                        report.AuxiliaryLinesOverwritten++;
                    }
                    else if (policy == MergeConflictPolicy.RenameNew)
                    {
                        var clone = sAux.Clone();
                        clone.Id = GenerateUniqueId(sAux.Id, id => target.FindAuxiliaryLine(id) != null);
                        target.AuxiliaryLines.Add(clone);
                        report.AuxiliaryLinesAdded++;
                    }
                }
            }

            return report;
        }

        private static string GenerateUniqueId(string baseId, Predicate<string> existsCheck)
        {
            int counter = 1;
            string candidate = $"{baseId}_imported";
            while (existsCheck(candidate))
            {
                candidate = $"{baseId}_imported_{counter++}";
            }
            return candidate;
        }
    }
}
