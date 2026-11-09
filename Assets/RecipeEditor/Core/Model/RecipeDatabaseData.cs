using System;
using System.Collections.Generic;

namespace CardKKK.RecipeEditor.Core
{
    /// <summary>
    /// 配方编辑器全局数据库容器。
    /// 集中管理所有复用标签库、物品库、合成场所库（含物品作为场所）以及合成与转变配方库。
    /// </summary>
    [Serializable]
    public sealed class RecipeDatabaseData
    {
        public int SchemaVersion = 1;
        public string DatabaseName = "默认游戏配方库";
        public string ContentVersion = "1.0.0";
        public string Description = "卡牌与物品配方、发生场所及可复用标签数据库";

        /// <summary>
        /// 全局复用标签池（物品与配方共用）
        /// </summary>
        public List<RecipeTag> Tags = new List<RecipeTag>();

        /// <summary>
        /// 全局复用能力/属性池（检定标准与卡牌点数类型）
        /// </summary>
        public List<RecipeAbility> Abilities = new List<RecipeAbility>();

        /// <summary>
        /// 物品库
        /// </summary>
        public List<RecipeItem> Items = new List<RecipeItem>();

        /// <summary>
        /// 合成场所库（独立场所 + 物品作为场所）
        /// </summary>
        public List<RecipeLocation> Locations = new List<RecipeLocation>();

        /// <summary>
        /// 配方库（标准合成与自身转变配方）
        /// </summary>
        public List<RecipeDefinition> Recipes = new List<RecipeDefinition>();

        /// <summary>
        /// 全局副产线规则库（跨配方生效的伴生修正、概率覆盖与产出拦截规则）
        /// </summary>
        public List<RecipeAuxiliaryLine> AuxiliaryLines = new List<RecipeAuxiliaryLine>();

        public RecipeDatabaseData() { }

        public RecipeDatabaseData Clone()
        {
            var db = new RecipeDatabaseData
            {
                SchemaVersion = SchemaVersion,
                DatabaseName = DatabaseName,
                ContentVersion = ContentVersion,
                Description = Description,
                Tags = new List<RecipeTag>(),
                Abilities = new List<RecipeAbility>(),
                Items = new List<RecipeItem>(),
                Locations = new List<RecipeLocation>(),
                Recipes = new List<RecipeDefinition>(),
                AuxiliaryLines = new List<RecipeAuxiliaryLine>()
            };

            if (Tags != null)
            {
                foreach (var tag in Tags)
                    if (tag != null) db.Tags.Add(tag.Clone());
            }

            if (Abilities != null)
            {
                foreach (var ab in Abilities)
                    if (ab != null) db.Abilities.Add(ab.Clone());
            }

            if (Items != null)
            {
                foreach (var item in Items)
                    if (item != null) db.Items.Add(item.Clone());
            }

            if (Locations != null)
            {
                foreach (var loc in Locations)
                    if (loc != null) db.Locations.Add(loc.Clone());
            }

            if (Recipes != null)
            {
                foreach (var rec in Recipes)
                    if (rec != null) db.Recipes.Add(rec.Clone());
            }

            if (AuxiliaryLines != null)
            {
                foreach (var aux in AuxiliaryLines)
                    if (aux != null) db.AuxiliaryLines.Add(aux.Clone());
            }

            return db;
        }

        // ==================== 快捷查找接口 ====================

        public RecipeTag FindTag(string tagId)
        {
            if (string.IsNullOrEmpty(tagId) || Tags == null) return null;
            return Tags.Find(t => t != null && t.Id == tagId);
        }

        public RecipeAbility FindAbility(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || Abilities == null) return null;
            return Abilities.Find(a => a != null && a.Id == abilityId);
        }

        public RecipeItem FindItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || Items == null) return null;
            return Items.Find(i => i != null && i.Id == itemId);
        }

        public RecipeLocation FindLocation(string locationId)
        {
            if (string.IsNullOrEmpty(locationId) || Locations == null) return null;
            return Locations.Find(l => l != null && l.Id == locationId);
        }

        public RecipeDefinition FindRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId) || Recipes == null) return null;
            return Recipes.Find(r => r != null && r.Id == recipeId);
        }

        public RecipeAuxiliaryLine FindAuxiliaryLine(string lineId)
        {
            if (string.IsNullOrEmpty(lineId) || AuxiliaryLines == null) return null;
            return AuxiliaryLines.Find(a => a != null && a.Id == lineId);
        }

        /// <summary>
        /// 查找是否有合成场所绑定了指定的物品 ID
        /// </summary>
        public RecipeLocation FindLocationByBoundItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || Locations == null) return null;
            return Locations.Find(l => l != null && l.SourceType == LocationSourceType.Item && l.BoundItemId == itemId);
        }

        /// <summary>
        /// 将某个物品直接添加或注册为合成场所
        /// </summary>
        public RecipeLocation AddItemAsLocation(string itemId, int capacity = 1)
        {
            var item = FindItem(itemId);
            if (item == null) return null;

            // 检查是否已存在
            var existing = FindLocationByBoundItem(itemId);
            if (existing != null) return existing;

            string locId = "loc_" + item.Id;
            // 确保 locId 唯一
            int index = 1;
            while (FindLocation(locId) != null)
            {
                locId = $"loc_{item.Id}_{index++}";
            }

            var loc = new RecipeLocation(locId, item.Name, item.Id, item.Description, capacity);
            if (Locations == null) Locations = new List<RecipeLocation>();
            Locations.Add(loc);
            return loc;
        }

        public string GetItemDisplayName(string itemId)
        {
            var item = FindItem(itemId);
            return item != null ? item.Name : (string.IsNullOrEmpty(itemId) ? "未命名" : itemId);
        }

        public string GetTagDisplayName(string tagId)
        {
            var tag = FindTag(tagId);
            return tag != null ? tag.Name : (string.IsNullOrEmpty(tagId) ? "未命名" : tagId);
        }

        public string GetAbilityDisplayName(string abilityId)
        {
            var ab = FindAbility(abilityId);
            return ab != null ? $"{ab.IconOrEmoji} {ab.Name}" : (string.IsNullOrEmpty(abilityId) ? "未指定能力" : abilityId);
        }

        public int GetAbilityUsageCount(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return 0;
            int count = 0;
            if (Items != null)
            {
                foreach (var it in Items)
                {
                    if (it?.AbilityContributions != null && it.AbilityContributions.Exists(ac => ac != null && ac.AbilityId == abilityId))
                        count++;
                }
            }
            if (Recipes != null)
            {
                foreach (var r in Recipes)
                {
                    if (r?.AbilityChecks != null && r.AbilityChecks.Exists(chk => chk != null && chk.AbilityId == abilityId))
                        count++;
                }
            }
            return count;
        }

        public string GetLocationDisplayName(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return "随身/无限制";
            var loc = FindLocation(locationId);
            if (loc == null) return locationId;
            return loc.SourceType == LocationSourceType.Item ? $"{loc.Name} [物品场所]" : loc.Name;
        }

        /// <summary>
        /// 统计某个标签被多少个物品引用
        /// </summary>
        public int GetTagUsageCount(string tagId)
        {
            if (string.IsNullOrEmpty(tagId) || Items == null) return 0;
            int count = 0;
            foreach (var item in Items)
            {
                if (item != null && item.HasTag(tagId)) count++;
            }
            return count;
        }

        /// <summary>
        /// 获取在特定场所发生的所有配方列表
        /// </summary>
        public List<RecipeDefinition> GetRecipesAtLocation(string locationId)
        {
            var result = new List<RecipeDefinition>();
            if (Recipes == null) return result;
            foreach (var r in Recipes)
            {
                if (r == null) continue;
                if (string.IsNullOrEmpty(locationId))
                {
                    if (string.IsNullOrEmpty(r.LocationId)) result.Add(r);
                }
                else
                {
                    if (r.LocationId == locationId) result.Add(r);
                }
            }
            return result;
        }
    }
}
