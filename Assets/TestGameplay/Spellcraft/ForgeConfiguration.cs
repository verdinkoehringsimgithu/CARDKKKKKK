using System;
using System.Collections.Generic;
using CardKKK.Spellcraft.Core;
using UnityEngine;

namespace CardKKK.Spellcraft.Unity
{
    [CreateAssetMenu(fileName = "法术工坊配置", menuName = "孤岛生存/历史演示/法术工坊/法术工坊配置")]
    public sealed class ForgeConfiguration : ScriptableObject
    {
        [SerializeField, InspectorName("玩法规则")] private ForgeDefinition rules = new ForgeDefinition();
        [SerializeField, InspectorName("中文字体")] private Font font;
        [SerializeField, InspectorName("对象插画")] private Sprite[] artwork = new Sprite[16];
        [SerializeField, InspectorName("桌面背景")] private Sprite background;
        [SerializeField, InspectorName("卡牌外框")] private Sprite cardFrame;
        [SerializeField, Range(0.05f, 0.5f), InspectorName("反馈过渡秒数")] private float transitionSeconds = 0.18f;
        [SerializeField, HideInInspector] private TextAsset previousPresentation;

        [Serializable] private sealed class PresentationHistory { public List<PresentationEntry> Entries; }
        [Serializable] private sealed class PresentationEntry
        {
            public string Kind, Id, EncounterId, Name, Description;
            public int IntentIndex, SpriteIndex;
        }

        public Font Font => font;
        public Sprite Background => background;
        public Sprite CardFrame => cardFrame;
        public float TransitionSeconds => transitionSeconds;
        public Sprite ArtFor(int index) => artwork != null && index >= 0 && index < artwork.Length ? artwork[index] : null;

        public ForgeDefinition BuildDefinition()
        {
            if (rules == null) throw new ArgumentException("玩法规则为空。");
            if (rules.HandSize < 1 || rules.HandSize > 5)
                throw new ArgumentException("当前桌面支持1～5张手牌；增加槽位后再提高上限。");
            if (rules.Encounters != null)
                foreach (ForgeEncounter encounter in rules.Encounters)
                {
                    if (encounter == null) continue;
                    if (encounter.Enemies != null && encounter.Enemies.Count > 3)
                        throw new ArgumentException("当前桌面每场最多显示3个敌人。");
                    if (encounter.RewardModuleIds != null && encounter.RewardModuleIds.Count > 3)
                        throw new ArgumentException("当前奖励面板最多显示3个零件。");
                }
            return JsonUtility.FromJson<ForgeDefinition>(JsonUtility.ToJson(rules));
        }

        public bool Validate(out string error)
        {
            error = "";
            try { new ForgeEngine(BuildDefinition(), 1); return true; }
            catch (ArgumentException exception) { error = exception.Message; return false; }
            catch (InvalidOperationException exception) { error = exception.Message; return false; }
        }

        // 仅还原上次显示文本来校验旧版指纹，不回退任何数值、解锁或敌人规则。
        public ForgeDefinition PreviousPresentation(ForgeDefinition current)
        {
            if (previousPresentation == null) return null;
            PresentationHistory history = JsonUtility.FromJson<PresentationHistory>(previousPresentation.text);
            if (history == null || history.Entries == null) return null;
            ForgeDefinition old = JsonUtility.FromJson<ForgeDefinition>(JsonUtility.ToJson(current));
            foreach (PresentationEntry entry in history.Entries)
            {
                if (entry == null) continue;
                if (entry.Kind == "module")
                {
                    ForgeModule item = old.Modules.Find(x => x.Id == entry.Id);
                    if (item != null) { item.Name = entry.Name; item.Description = entry.Description; item.SpriteIndex = entry.SpriteIndex; }
                }
                else if (entry.Kind == "card")
                {
                    ForgeCard item = old.Cards.Find(x => x.Id == entry.Id);
                    if (item != null) { item.Name = entry.Name; item.Description = entry.Description; item.SpriteIndex = entry.SpriteIndex; }
                }
                else if (entry.Kind == "encounter")
                {
                    ForgeEncounter item = old.Encounters.Find(x => x.Id == entry.Id);
                    if (item != null) { item.Name = entry.Name; item.Description = entry.Description; }
                }
                else
                {
                    ForgeEncounter encounter = old.Encounters.Find(x => x.Id == entry.EncounterId);
                    ForgeEnemyTemplate enemy = encounter != null ? encounter.Enemies.Find(x => x.Id == entry.Id) : null;
                    if (enemy == null) continue;
                    if (entry.Kind == "enemy")
                    { enemy.Name = entry.Name; enemy.Description = entry.Description; enemy.SpriteIndex = entry.SpriteIndex; }
                    else if (entry.Kind == "intent" && entry.IntentIndex >= 0 && entry.IntentIndex < enemy.Intents.Count)
                    { enemy.Intents[entry.IntentIndex].Name = entry.Name; enemy.Intents[entry.IntentIndex].Description = entry.Description; }
                }
            }
            return old;
        }
    }
}
