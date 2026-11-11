using System;
using System.Collections.Generic;
using CardKKK.Tabletop.Unity;
using UnityEditor;
using UnityEngine;

namespace CardKKK.Tabletop.Editor
{
    public sealed class CardTableConfigurationWindow : EditorWindow
    {
        [SerializeField] private CardTableConfiguration configuration;
        private SerializedObject serialized;
        private Vector2 scroll;
        private int tab;
        private string validation;
        private bool valid;
        private static readonly string[] Tabs = { "桌面规则", "卡牌目录", "初始桌面", "功能与资源" };

        [MenuItem("孤岛生存/历史演示/自由牌桌/游戏规则配置", priority = 20)]
        public static void Open()
        {
            var window = GetWindow<CardTableConfigurationWindow>("游戏规则配置");
            window.minSize = new Vector2(540, 500);
            window.BindSceneConfiguration();
            window.Show();
        }

        internal static void OpenConfiguration(CardTableConfiguration target)
        {
            var window = GetWindow<CardTableConfigurationWindow>("游戏规则配置");
            window.minSize = new Vector2(540, 500);
            window.configuration = target;
            window.SetSerializedObject();
            window.Show();
        }

        [MenuItem("孤岛生存/历史演示/自由牌桌/补齐缺少的默认配置", priority = 21)]
        public static void CreateMissingDefaults()
        {
            CardTableConfiguration created = CardTableDefaults.CreateMissing();
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        private void OnEnable() { BindSceneConfiguration(); }

        private void BindSceneConfiguration()
        {
            CardTableConfiguration active = FindSceneConfiguration();
            if (active != null) configuration = active;
            if (configuration == null)
                configuration = AssetDatabase.LoadAssetAtPath<CardTableConfiguration>(CardTableDefaults.ConfigurationPath);
            SetSerializedObject();
        }

        private static CardTableConfiguration FindSceneConfiguration()
        {
            CardTableDemo entry = UnityEngine.Object.FindObjectOfType<CardTableDemo>(true);
            if (entry == null) return null;
            // 读取场景序列化引用，窗口不依赖启动器的运行中状态。
            var sceneObject = new SerializedObject(entry);
            SerializedProperty property = sceneObject.FindProperty("configuration");
            return property == null ? null : property.objectReferenceValue as CardTableConfiguration;
        }

        private void SetSerializedObject()
        {
            serialized = configuration == null ? null : new SerializedObject(configuration);
            validation = null;
            valid = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("游戏规则配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这里编辑实际配置资产。停止运行后修改，下一次进入桌面时应用；运行中的桌面不会被配置窗口直接改写。当前框架是自由桌面，配方、任务与胜负规则尚未加入。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var selected = (CardTableConfiguration)EditorGUILayout.ObjectField("正在编辑的配置", configuration, typeof(CardTableConfiguration), false);
            if (EditorGUI.EndChangeCheck()) { configuration = selected; SetSerializedObject(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("读取当前场景配置")) BindSceneConfiguration();
                using (new EditorGUI.DisabledScope(configuration == null))
                    if (GUILayout.Button("在项目中定位")) { Selection.activeObject = configuration; EditorGUIUtility.PingObject(configuration); }
            }
            if (configuration == null)
            {
                EditorGUILayout.HelpBox("没有找到配置。可以选择已有资产，或补齐缺少的默认配置。已有配置不会被覆盖。", MessageType.Warning);
                if (GUILayout.Button("补齐缺少的默认配置")) { configuration = CardTableDefaults.CreateMissing(); SetSerializedObject(); }
                return;
            }
            CardTableConfiguration sceneConfiguration = FindSceneConfiguration();
            if (sceneConfiguration != null && sceneConfiguration != configuration)
                EditorGUILayout.HelpBox("当前编辑资产与场景使用的配置不同。点击“读取当前场景配置”即可编辑运行入口实际引用的资产。", MessageType.Warning);
            if (serialized == null || serialized.targetObject != configuration) SetSerializedObject();
            serialized.Update();
            tab = GUILayout.Toolbar(tab, Tabs);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: ConfigurationFields.DrawRules(serialized.FindProperty("Rules")); break;
                case 1: DrawCards(); break;
                case 2:
                    EditorGUILayout.HelpBox("每一项是一堆牌，位置以桌面左下角为原点。卡牌列表从底到顶，所有引用都必须已加入卡牌目录。关闭堆叠时，每堆只能保留一张牌。", MessageType.Info);
                    ConfigurationFields.DrawInitialPiles(serialized.FindProperty("InitialPiles"));
                    break;
                case 3:
                    ConfigurationFields.DrawFeatures(serialized.FindProperty("Features"));
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(serialized.FindProperty("Theme"), new GUIContent("美术与音频主题（可留空）"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("SaveFileName"), new GUIContent("存档文件名"));
                    break;
            }
            if (serialized.ApplyModifiedProperties()) { validation = null; valid = false; }
            if (tab == 3) ConfigurationFields.DrawCompilation(configuration);
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("检查配置")) valid = configuration.Validate(out validation);
                if (GUILayout.Button("保存配置资产")) { AssetDatabase.SaveAssets(); valid = configuration.Validate(out validation); }
            }
            if (validation != null || valid)
                EditorGUILayout.HelpBox(valid ? "配置检查通过。Unity 编译和实际运行请在编辑器中确认。" : validation, valid ? MessageType.Info : MessageType.Error);
        }

        private void DrawCards()
        {
            EditorGUILayout.HelpBox("每种牌单独一个定义资产。名称、分类、序号、初始属性和卡面插画都在定义里配置；同一种牌的多张实例复用同一个定义。发布后请保持定义 ID 稳定。", MessageType.Info);
            ConfigurationFields.DrawCardReferences(serialized.FindProperty("Cards"), "卡牌目录", "种");
            if (!GUILayout.Button("新建卡牌定义并加入目录")) return;
            string path = EditorUtility.SaveFilePanelInProject("新建卡牌定义", "新卡牌", "asset", "请选择卡牌定义的保存位置。", CardTableDefaults.CardsPath);
            if (string.IsNullOrEmpty(path)) return;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            serialized.ApplyModifiedProperties();
            var definition = CreateInstance<CardDefinition>();
            definition.Id = "card." + Guid.NewGuid().ToString("N");
            definition.DisplayName = "新卡牌";
            definition.Family = "新分类";
            AssetDatabase.CreateAsset(definition, path);
            Undo.RecordObject(configuration, "加入卡牌定义");
            if (configuration.Cards == null) configuration.Cards = new List<CardDefinition>();
            configuration.Cards.Add(definition);
            EditorUtility.SetDirty(configuration);
            serialized.Update();
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
            validation = null;
            valid = false;
        }
    }

    internal static class ConfigurationFields
    {
        private static readonly string[] StackLabels = { "相同分类", "相同定义", "任意卡牌" };
        private static readonly string[] SortLabels = { "序号升序（相同序号保留原顺序）", "序号降序（相同序号保留原顺序）", "卡牌名称升序" };
        private static void Field(SerializedProperty root, string name, string label)
        {
            EditorGUILayout.PropertyField(root.FindPropertyRelative(name), new GUIContent(label), true);
        }

        internal static void DrawRules(SerializedProperty rules)
        {
            EditorGUILayout.LabelField("桌面尺寸与容量", EditorStyles.boldLabel);
            Field(rules, "Width", "桌面宽度"); Field(rules, "Height", "桌面高度");
            Field(rules, "CardWidth", "卡牌宽度"); Field(rules, "CardHeight", "卡牌高度");
            Field(rules, "PileOffset", "堆内纵向错开"); Field(rules, "SnapDistance", "吸附距离");
            Field(rules, "Gap", "整理与空位间距"); Field(rules, "MaxCards", "全桌卡牌上限");
            Field(rules, "MaxPileSize", "单堆卡牌上限");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("堆叠与拖动", EditorStyles.boldLabel);
            Field(rules, "AllowStacking", "允许堆叠");
            SerializedProperty stack = rules.FindPropertyRelative("StackRule");
            using (new EditorGUI.DisabledScope(!rules.FindPropertyRelative("AllowStacking").boolValue))
                stack.intValue = EditorGUILayout.Popup("堆叠兼容方式", stack.intValue, StackLabels);
            Field(rules, "AllowWholePileDrag", "允许整堆拖动");
            Field(rules, "AllowSingleCardDrag", "允许拆出单张拖动");
            Field(rules, "AutoMergeOnSpawn", "生成重叠时合并兼容牌堆");
            Field(rules, "AutoMergeOnTidy", "整理时合并附近兼容牌堆");
            SerializedProperty sort = rules.FindPropertyRelative("SortRule");
            sort.intValue = EditorGUILayout.Popup("牌堆排序方式", sort.intValue, SortLabels);
            EditorGUILayout.HelpBox("吸附距离用于拖放吸附与整理附近合堆；生成仅处理重叠。间距用于找空位和整理。整理采用有限候选布局，空间不足时保留原桌面。修改规则后请同步调整初始桌面。", MessageType.None);
        }

        internal static void DrawFeatures(SerializedProperty features)
        {
            EditorGUILayout.LabelField("本配置允许的功能", EditorStyles.boldLabel);
            for (int i = 0; i < CardTableFeatureCompiler.FieldNames.Length; i++)
                Field(features, CardTableFeatureCompiler.FieldNames[i], CardTableFeatureCompiler.Labels[i]);
        }

        internal static void DrawCardReferences(SerializedProperty cards, string label, string unit)
        {
            EditorGUILayout.LabelField(label + "（" + cards.arraySize + unit + "）", EditorStyles.boldLabel);
            for (int i = 0; i < cards.arraySize; i++)
            {
                SerializedProperty card = cards.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(card, new GUIContent("第 " + (i + 1) + unit));
                    using (new EditorGUI.DisabledScope(i == 0))
                        if (GUILayout.Button("上移", GUILayout.Width(42))) cards.MoveArrayElement(i, i - 1);
                    using (new EditorGUI.DisabledScope(i == cards.arraySize - 1))
                        if (GUILayout.Button("下移", GUILayout.Width(42))) cards.MoveArrayElement(i, i + 1);
                    if (GUILayout.Button("移除", GUILayout.Width(46)))
                    {
                        card.objectReferenceValue = null;
                        cards.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }
            if (GUILayout.Button("增加已有卡牌引用"))
            {
                int index = cards.arraySize;
                cards.arraySize++;
                cards.GetArrayElementAtIndex(index).objectReferenceValue = null;
            }
        }

        internal static void DrawInitialPiles(SerializedProperty piles)
        {
            for (int i = 0; i < piles.arraySize; i++)
            {
                SerializedProperty pile = piles.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        pile.isExpanded = EditorGUILayout.Foldout(pile.isExpanded, "第 " + (i + 1) + " 堆", true);
                        if (GUILayout.Button("删除此堆", GUILayout.Width(74))) { piles.DeleteArrayElementAtIndex(i); break; }
                    }
                    if (!pile.isExpanded) continue;
                    SerializedProperty position = pile.FindPropertyRelative("Position");
                    position.vector2Value = EditorGUILayout.Vector2Field("底牌左下角位置", position.vector2Value);
                    DrawCardReferences(pile.FindPropertyRelative("Cards"), "卡牌顺序（从底到顶）", "张");
                }
            }
            if (GUILayout.Button("增加初始牌堆"))
            {
                int index = piles.arraySize;
                piles.arraySize++;
                SerializedProperty added = piles.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("Position").vector2Value = Vector2.zero;
                added.FindPropertyRelative("Cards").ClearArray();
                added.isExpanded = true;
            }
        }

        internal static void DrawCompilation(CardTableConfiguration configuration)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("从编译中移除未选功能", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("勾选框决定本配置是否允许操作。“应用功能裁剪到编译”会根据勾选结果修改当前构建平台的 7 个专用编译符号，用 #if 排除对应核心实现及界面调用。裁剪作用于整个工程，同平台下其他配置也受影响。重新启用后再次点击按钮才会编译回来。打开窗口或导入资产不会自动修改编译符号。", MessageType.Info);
            EditorGUILayout.LabelField("作用平台", CardTableFeatureCompiler.TargetGroup == BuildTargetGroup.Standalone ? "Windows / 桌面平台" : CardTableFeatureCompiler.TargetGroup.ToString());
            bool matches = CardTableFeatureCompiler.Matches(configuration.Features);
            EditorGUILayout.HelpBox(matches ? "当前平台的编译裁剪与本配置一致。" : "当前平台的编译裁剪与本配置不同；已裁剪的功能即使勾选也不可用。", matches ? MessageType.None : MessageType.Warning);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                if (GUILayout.Button("应用功能裁剪到编译")) CardTableFeatureCompiler.Apply(configuration);
            HashSet<string> active = CardTableFeatureCompiler.ReadSymbols();
            for (int i = 0; i < CardTableFeatureCompiler.Symbols.Length; i++)
            {
                EditorGUILayout.LabelField(CardTableFeatureCompiler.Labels[i] + (active.Contains(CardTableFeatureCompiler.Symbols[i]) ? "：已裁剪" : "：已编译"), EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(CardTableFeatureCompiler.ModulePaths[i], EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
    }

    [CustomEditor(typeof(CardTableConfiguration))]
    internal sealed class CardTableConfigurationInspector : UnityEditor.Editor
    {
        private string validation;
        private bool valid;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (GUILayout.Button("在游戏规则窗口中编辑")) CardTableConfigurationWindow.OpenConfiguration((CardTableConfiguration)target);
            ConfigurationFields.DrawRules(serializedObject.FindProperty("Rules"));
            EditorGUILayout.Space();
            ConfigurationFields.DrawFeatures(serializedObject.FindProperty("Features"));
            ConfigurationFields.DrawCardReferences(serializedObject.FindProperty("Cards"), "卡牌目录", "种");
            ConfigurationFields.DrawInitialPiles(serializedObject.FindProperty("InitialPiles"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Theme"), new GUIContent("美术与音频主题（可留空）"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SaveFileName"), new GUIContent("存档文件名"));
            if (serializedObject.ApplyModifiedProperties()) { validation = null; valid = false; }
            if (GUILayout.Button("检查配置")) valid = ((CardTableConfiguration)target).Validate(out validation);
            if (validation != null || valid)
                EditorGUILayout.HelpBox(valid ? "配置检查通过。" : validation, valid ? MessageType.Info : MessageType.Error);
        }
    }

    [CustomEditor(typeof(CardDefinition))]
    internal sealed class CardDefinitionInspector : UnityEditor.Editor
    {
        private bool migration;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (string name in new[] { "Id", "DisplayName", "Family", "SortRank", "Artwork" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            SerializedProperty attributes = serializedObject.FindProperty("Attributes");
            EditorGUILayout.LabelField("初始实例属性", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这些整数属性会复制到新生成的卡牌实例，存档保留实例的修改。目前框架负责存取属性，不会自动解释属性为战斗、配方或胜负规则。", MessageType.None);
            for (int i = 0; i < attributes.arraySize; i++)
            {
                SerializedProperty attribute = attributes.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(attribute.FindPropertyRelative("Key"), new GUIContent("属性名"));
                    EditorGUILayout.PropertyField(attribute.FindPropertyRelative("Value"), new GUIContent("数值"));
                    if (GUILayout.Button("删除", GUILayout.Width(46))) { attributes.DeleteArrayElementAtIndex(i); break; }
                }
            }
            if (GUILayout.Button("增加属性"))
            {
                int index = attributes.arraySize++;
                SerializedProperty added = attributes.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("Key").stringValue = "新属性";
                added.FindPropertyRelative("Value").intValue = 0;
            }
            migration = EditorGUILayout.Foldout(migration, "旧版存档迁移（新牌无需填写）", true);
            if (migration)
            {
                EditorGUILayout.HelpBox("旧定义 ID 与旧序号共同匹配存档。每个旧序号都需要对应定义；未配置的旧牌会拒绝读取并保留当前桌面。", MessageType.None);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("LegacyDefinitionId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("LegacyRank"));
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    internal static class CardTableDefaults
    {
        internal const string ConfigurationPath = "Assets/CardTable/Configuration/默认游戏配置.asset";
        internal const string CardsPath = "Assets/CardTable/Configuration/Cards";

        internal static CardTableConfiguration CreateMissing()
        {
            EnsureFolder("Assets/CardTable", "Configuration");
            EnsureFolder("Assets/CardTable/Configuration", "Cards");
            string[] families = { "archive", "supply", "tool" };
            string[] labels = { "藏书", "物资", "工具" };
            int[] ranks = { 3, 1, 2 };
            var definitions = new List<CardDefinition>();
            for (int family = 0; family < families.Length; family++)
                foreach (int rank in ranks)
                {
                    string path = CardsPath + "/" + labels[family] + rank + ".asset";
                    var definition = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
                    if (definition == null && !AssetExists(path))
                    {
                        definition = ScriptableObject.CreateInstance<CardDefinition>();
                        definition.Id = families[family] + "." + rank;
                        definition.DisplayName = labels[family] + " " + rank;
                        definition.Family = families[family];
                        definition.SortRank = rank;
                        definition.LegacyDefinitionId = families[family] + ".sample";
                        definition.LegacyRank = rank;
                        AssetDatabase.CreateAsset(definition, path);
                    }
                    definitions.Add(definition);
                }
            var configuration = AssetDatabase.LoadAssetAtPath<CardTableConfiguration>(ConfigurationPath);
            if (configuration == null && !AssetExists(ConfigurationPath))
            {
                configuration = ScriptableObject.CreateInstance<CardTableConfiguration>();
                configuration.Cards = definitions;
                configuration.Theme = AssetDatabase.LoadAssetAtPath<CardTableTheme>(AssetDatabase.GUIDToAssetPath("beea84b396b68bd99131fc1e320b3382"));
                for (int family = 0; family < families.Length; family++)
                    configuration.InitialPiles.Add(new InitialPile
                    {
                        Position = new Vector2(180 + family * 360, 360),
                        Cards = definitions.GetRange(family * 3, 3)
                    });
                AssetDatabase.CreateAsset(configuration, ConfigurationPath);
            }
            AssetDatabase.SaveAssets();
            return configuration;
        }

        private static bool AssetExists(string path)
        {
            return AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path);
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
