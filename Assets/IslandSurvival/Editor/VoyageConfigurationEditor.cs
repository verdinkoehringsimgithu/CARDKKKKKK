using System;
using System.Collections.Generic;
using CardKKK.IslandSurvival.Core;
using CardKKK.IslandSurvival.Unity;
using UnityEditor;
using UnityEngine;

namespace CardKKK.IslandSurvival.Editor
{
    [CustomEditor(typeof(VoyageConfiguration))]
    internal sealed class VoyageConfigurationEditor : UnityEditor.Editor
    {
        private static readonly string[] Tabs = { "总则", "开局", "卡牌", "配方", "副产线" };
        private int tab;
        private string search = "", validation;

        [MenuItem("孤岛生存/配置/出航规则")]
        private static void SelectRules()
        {
            var rules = AssetDatabase.LoadAssetAtPath<VoyageConfiguration>("Assets/IslandSurvival/Configuration/VoyageRules.asset");
            if (rules == null) { EditorUtility.DisplayDialog("出航规则", "找不到出航规则资产。", "关闭"); return; }
            Selection.activeObject = rules;
            EditorGUIUtility.PingObject(rules);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty rules = serializedObject.FindProperty("Rules");
            EditorGUILayout.HelpBox("按《孤岛》修订稿录入。时长、概率、耐久和水位变化为可调联调值。修改后重新进入或开新局生效。", MessageType.Info);
            int selected = GUILayout.Toolbar(tab, Tabs);
            if (selected != tab) { tab = selected; search = ""; }
            EditorGUILayout.Space();
            switch (tab)
            {
                case 0:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("Tabletop"), new GUIContent("牌桌交互规则"));
                    Field(rules, "RealSecondsPerMinute", "每游戏分钟的现实秒数");
                    Field(rules, "HungerPerMinute", "每分钟饥饿水位损耗");
                    Field(rules, "Seed", "新局随机种子");
                    Field(rules, "BirdStep", "每分钟移动距离（桌面比例）");
                    Field(rules, "BirdRange", "觅食检测距离（桌面比例）");
                    Field(rules, "BirdReach", "接触触发距离（桌面比例）");
                    break;
                case 1:
                    Field(rules, "StartCards", "开局卡牌与位置");
                    break;
                case 2:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("Cards"), new GUIContent("卡牌资产目录"), true);
                    DrawList(rules.FindPropertyRelative("Cards"), "卡牌规则");
                    break;
                case 3:
                    DrawList(rules.FindPropertyRelative("Recipes"), "配方");
                    break;
                case 4:
                    DrawList(rules.FindPropertyRelative("AuxiliaryLines"), "副产线");
                    break;
            }
            if (serializedObject.ApplyModifiedProperties()) validation = null;
            if (GUILayout.Button("检查规则和卡牌引用")) validation = ((VoyageConfiguration)target).CheckConfiguration() ?? "";
            if (validation != null)
                EditorGUILayout.HelpBox(validation.Length == 0 ? "静态规则检查通过。" : validation, validation.Length == 0 ? MessageType.Info : MessageType.Error);
        }

        private void DrawList(SerializedProperty list, string label)
        {
            search = EditorGUILayout.TextField("按名称或 ID 查找", search);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label + "（" + list.arraySize + "）", EditorStyles.boldLabel);
                if (GUILayout.Button("新增一项", GUILayout.Width(84)))
                {
                    int index = list.arraySize;
                    list.arraySize++;
                    SerializedProperty added = list.GetArrayElementAtIndex(index);
                    ResetEntry(added);
                    added.isExpanded = true;
                    search = "";
                }
            }
            int remove = -1;
            var config = (VoyageConfiguration)target;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("Id").stringValue;
                string name = list.name == "Cards" ? config.FindCard(id)?.DisplayName : entry.FindPropertyRelative("Name")?.stringValue;
                if (search.Length > 0 && !Includes(id, search) && !Includes(name, search)) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    string display = string.IsNullOrEmpty(name) ? id : name + "  ·  " + id;
                    EditorGUILayout.PropertyField(entry, new GUIContent(string.IsNullOrEmpty(display) ? "待填写" : display), true);
                    if (GUILayout.Button("移除", GUILayout.Width(44)) && EditorUtility.DisplayDialog("移除配置", "移除这一项？", "移除", "取消")) remove = i;
                }
            }
            if (remove >= 0) list.DeleteArrayElementAtIndex(remove);
        }

        private static bool Includes(string value, string query) => !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void Field(SerializedProperty root, string field, string label)
        {
            EditorGUILayout.PropertyField(root.FindPropertyRelative(field), new GUIContent(label), true);
        }

        private static void ResetEntry(SerializedProperty entry)
        {
            SerializedProperty iterator = entry.Copy(), end = entry.GetEndProperty();
            for (bool next = iterator.NextVisible(true); next && !SerializedProperty.EqualContents(iterator, end); next = iterator.NextVisible(false))
            {
                if (iterator.isArray && iterator.propertyType != SerializedPropertyType.String) { iterator.arraySize = 0; continue; }
                switch (iterator.propertyType)
                {
                    case SerializedPropertyType.String: iterator.stringValue = ""; break;
                    case SerializedPropertyType.Integer: iterator.intValue = iterator.name == "Minutes" ? 1 : 0; break;
                    case SerializedPropertyType.Boolean: iterator.boolValue = false; break;
                    case SerializedPropertyType.Float: iterator.doubleValue = iterator.name == "OutputMultiplier" ? 1 : 0; break;
                    case SerializedPropertyType.Enum: iterator.enumValueIndex = 0; break;
                }
            }
        }
    }

    internal abstract class VoyageRuleDrawer : PropertyDrawer
    {
        protected abstract IEnumerable<(string Field, string Label)> Fields { get; }
        private static float Line => EditorGUIUtility.singleLineHeight;
        private static float Gap => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = Line;
            if (!property.isExpanded) return height;
            foreach (var field in Fields)
                height += Gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field.Field), new GUIContent(field.Label), true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var line = new Rect(position.x, position.y, position.width, Line);
                property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
                if (!property.isExpanded) return;
                using (new EditorGUI.IndentLevelScope())
                    foreach (var field in Fields)
                    {
                        SerializedProperty child = property.FindPropertyRelative(field.Field);
                        var title = new GUIContent(field.Label);
                        line.y += line.height + Gap;
                        line.height = EditorGUI.GetPropertyHeight(child, title, true);
                        EditorGUI.PropertyField(line, child, title, true);
                    }
            }
        }
    }

    [CustomPropertyDrawer(typeof(VoyageCardRule))]
    internal sealed class VoyageCardRuleDrawer : VoyageRuleDrawer
    {
        protected override IEnumerable<(string Field, string Label)> Fields => Entries;
        private static readonly (string, string)[] Entries = {
            ("Id", "卡牌资产 ID"), ("Description", "卡面文字"), ("Tags", "匹配标签"), ("GrantsRecipes", "公开配方 ID"),
            ("TimerMinutes", "自身计时（分钟）"), ("TransformId", "到期转化 ID"), ("Charges", "可用次数"), ("RechargeMinutes", "恢复周期（分钟）"),
            ("InjuryLevel", "伤势等级"), ("LimbId", "关联身体定义 ID"), ("NextInjuryId", "二合一后定义 ID"), ("LimbDamage", "三级伤势扣耐久"),
            ("SeekRecipeId", "接触觅食配方 ID"), ("SpawnHunger", "生成时饥饿水位变化"), ("SpawnSan", "生成时 SAN 变化")
        };
    }

    [CustomPropertyDrawer(typeof(VoyageRecipeRule))]
    internal sealed class VoyageRecipeRuleDrawer : VoyageRuleDrawer
    {
        protected override IEnumerable<(string Field, string Label)> Fields => Entries;
        private static readonly (string, string)[] Entries = {
            ("Id", "配方 ID"), ("Name", "名称"), ("Tag", "配方标签"), ("InitiallyKnown", "开局已知"), ("Automatic", "接触自动触发"),
            ("LocationId", "场所卡 ID"), ("LocationWear", "场所扣耐久"), ("LocationCharges", "场所扣次数"), ("Minutes", "执行时长（分钟）"),
            ("Inputs", "输入（身体／工具／材料）"), ("Outputs", "产物"), ("DiscoveryCardId", "盲试获得的配方卡 ID"),
            ("HungerDelta", "完成时饥饿水位变化"), ("SanDelta", "完成时 SAN 变化")
        };
    }

    [CustomPropertyDrawer(typeof(VoyageAuxiliaryRule))]
    internal sealed class VoyageAuxiliaryRuleDrawer : VoyageRuleDrawer
    {
        protected override IEnumerable<(string Field, string Label)> Fields => Entries;
        private static readonly (string, string)[] Entries = {
            ("Id", "副产线 ID"), ("CardId", "附加卡牌 ID"), ("RecipeTag", "所需配方标签"), ("RequiredInputId", "配方所需输入 ID"),
            ("DurabilityCost", "附加卡扣耐久"), ("MinutesDelta", "追加时长（分钟）"), ("OutputMultiplier", "基础产物概率倍率"),
            ("SanDelta", "完成时 SAN 变化"), ("Outputs", "额外产物")
        };
    }

    [CustomPropertyDrawer(typeof(VoyageInputRule))]
    internal sealed class VoyageInputRuleDrawer : VoyageRuleDrawer
    {
        protected override IEnumerable<(string Field, string Label)> Fields => Entries;
        private static readonly (string, string)[] Entries = {
            ("CardId", "指定卡牌 ID"), ("Tag", "匹配标签（可单独使用）"), ("Quantity", "数量"), ("IsConsumed", "完成时消耗"),
            ("DurabilityCost", "完成时扣耐久"), ("DurabilityRestore", "完成时修复耐久"), ("ChargesRestore", "完成时恢复次数"), ("MatchLimb", "伤势需对应槽内身体牌")
        };
    }

    [CustomPropertyDrawer(typeof(VoyageOutputRule))]
    internal sealed class VoyageOutputRuleDrawer : VoyageRuleDrawer
    {
        protected override IEnumerable<(string Field, string Label)> Fields => Entries;
        private static readonly (string, string)[] Entries = {
            ("CardId", "产物 ID"), ("Quantity", "数量"), ("Chance", "触发概率（0～1）"), ("Group", "互斥组（留空独立判定）"), ("Unique", "现存时不重复产生")
        };
    }

    [CustomPropertyDrawer(typeof(VoyageStartCard))]
    internal sealed class VoyageStartCardDrawer : VoyageRuleDrawer
    {
        protected override IEnumerable<(string Field, string Label)> Fields => Entries;
        private static readonly (string, string)[] Entries = { ("CardId", "卡牌 ID"), ("X", "横坐标（0～1）"), ("Y", "纵坐标（0～1）") };
    }
}
