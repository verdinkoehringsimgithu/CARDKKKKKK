using System;
using System.Collections.Generic;
using CardKKK.Island.Core;
using UnityEngine;

namespace CardKKK.Island.Unity
{
    [Serializable]
    public sealed class InitialCard
    {
        public string InstanceId;
        public string DefinitionId;
        public string RegionId;
        public int Quantity = 1;
        public Vector2 Position;
    }

    [CreateAssetMenu(menuName = "孤岛底层/玩法配置", fileName = "孤岛玩法配置")]
    public sealed class IslandConfiguration : ScriptableObject
    {
        public IslandDefinition Definition = new IslandDefinition();
        public List<InitialCard> InitialCards = new List<InitialCard>();

        public IslandEngine CreateEngine()
        {
            // Validate the catalog before consulting designer-supplied initial cards.
            if (Definition == null) throw new ArgumentException("缺少玩法定义。");
            var initial = new IslandSnapshot { ContentVersion = Definition.ContentVersion };
            new IslandEngine(Definition, initial);
            if (InitialCards == null) throw new ArgumentException("开局列表不能为空。");
            foreach (var seed in InitialCards)
            {
                if (seed == null) throw new ArgumentException("开局卡牌不能为空。");
                var template = Definition.Cards.Find(card => card.Id == seed.DefinitionId);
                if (template == null) throw new ArgumentException("开局定义不存在：" + seed.DefinitionId);
                var instance = new CardInstance
                {
                    Id = seed.InstanceId, DefinitionId = seed.DefinitionId, RegionId = seed.RegionId,
                    Quantity = seed.Quantity, X = seed.Position.x, Y = seed.Position.y,
                    ExpiresAt = template.LifetimeMinutes
                };
                foreach (var value in template.InitialValues)
                    instance.Values.Add(new NumberValue { Key = value.Key, Value = value.Value });
                initial.Cards.Add(instance);
            }
            return new IslandEngine(Definition, initial);
        }
    }
}
