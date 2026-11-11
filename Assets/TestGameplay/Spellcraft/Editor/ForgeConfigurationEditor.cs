using System.Collections.Generic;
using CardKKK.Spellcraft.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardKKK.Spellcraft.Editor
{
    [CustomEditor(typeof(ForgeConfiguration))]
    public sealed class ForgeConfigurationEditor : UnityEditor.Editor
    {
        private int page;
        private string validation = "";
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            {"rules","玩法规则"},{"MaxHealth","生命上限"},{"EnergyPerTurn","每回合能量"},
            {"StartingSpark","每场起始火花"},{"SparkCap","火花上限"},{"HandSize","每回合手牌数"},
            {"MaxCircuitActivations","单装置每回合次数"},{"MaxEventsPerCommand","单命令事件上限"},
            {"HealAfterBattle","每次胜利恢复生命"},{"Modules","零件目录"},{"Cards","基础牌目录"},
            {"StartingDeck","初始牌组"},{"StartingUnlocked","初始已掌握零件"},{"Encounters","试炼顺序"},
            {"StartingCircuits","初始装置"},{"Id","稳定标识"},{"Name","名称"},{"Description","说明"},
            {"Kind","种类"},{"Value","效果强度"},{"SparkCost","火花消耗"},{"SpriteIndex","插画序号"},
            {"Trigger","触发条件"},{"Effect","执行效果"},{"Modifier","改装方式"},{"Cost","能量消耗"},
            {"Power","强度"},{"ReflectDamage","每次直伤反射"},{"Intents","循环行动"},{"Enemies","敌人"},
            {"RewardModuleIds","胜利三选一"},{"TriggerId","触发零件标识"},{"EffectId","效果零件标识"},
            {"ModifierId","改装零件标识"},{"font","中文字体"},{"artwork","对象插画"},
            {"background","桌面背景"},{"cardFrame","卡牌外框"},{"transitionSeconds","反馈过渡秒数"}
        };
        private static readonly Dictionary<string, string> EnumLabels = new Dictionary<string, string>
        {
            {"Trigger","触发"},{"Effect","效果"},{"Modifier","改装"},{"Manual","手动启动"},
            {"TurnStart","己方回合开始"},{"HitTaken","生命受伤"},{"Blocked","护盾挡击"},
            {"SparkSpent","消耗火花"},{"BurnTick","敌人燃烧"},{"Bolt","直伤"},{"Barrier","获得护盾"},
            {"Charge","充能或蓄力"},{"Ignite","施加燃烧"},{"Frost","施加冻结"},{"Drain","伤害并汲取生命"},
            {"None","原样执行"},{"Fork","分叉全体"},{"Echo","共鸣两次"},{"Overload","过载强化"},
            {"Strike","直接攻击"},{"Guard","获得护盾"},{"Discharge","耗尽火花放电"},
            {"Attack","攻击一次"},{"DoubleAttack","攻击两次"},{"DrainSpark","抽取火花"}
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            page = GUILayout.Toolbar(page, new[] { "规则与内容", "美术与反馈" });
            EditorGUILayout.Space();
            if (page == 0)
            {
                EditorGUILayout.HelpBox("规则在重新开始或下次进入时应用；修改规则后会拒绝旧规则存档。零件可复用，装置固定三槽。", MessageType.Info);
                Draw(serializedObject.FindProperty("rules"), "玩法规则");
            }
            else
                foreach (string name in new[] { "font", "artwork", "background", "cardFrame", "transitionSeconds" })
                    Draw(serializedObject.FindProperty(name), null);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            if (GUILayout.Button("检查规则配置"))
                validation = ((ForgeConfiguration)target).Validate(out string error) ? "配置结构有效；实际战斗由运行确认。" : error;
            if (!string.IsNullOrEmpty(validation)) EditorGUILayout.HelpBox(validation, MessageType.Info);
        }

        private static void Draw(SerializedProperty property, string title)
        {
            if (property == null) return;
            string label = title;
            if (label == null && !Labels.TryGetValue(property.name, out label)) label = property.displayName;
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label + "（" + property.arraySize + "）", true);
                if (!property.isExpanded) return;
                EditorGUI.indentLevel++;
                property.arraySize = Mathf.Clamp(EditorGUILayout.IntField("数量", property.arraySize), 0, 1000);
                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty item = property.GetArrayElementAtIndex(i);
                    SerializedProperty name = item.propertyType == SerializedPropertyType.Generic ? item.FindPropertyRelative("Name") : null;
                    Draw(item, (i + 1) + (name != null && !string.IsNullOrEmpty(name.stringValue) ? "  " + name.stringValue : ""));
                }
                EditorGUI.indentLevel--;
                return;
            }
            if (property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren)
            {
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label, true);
                if (!property.isExpanded) return;
                EditorGUI.indentLevel++;
                SerializedProperty child = property.Copy(), end = property.GetEndProperty();
                bool enter = true;
                SerializedProperty moduleKind = property.FindPropertyRelative("Kind");
                bool module = property.FindPropertyRelative("Trigger") != null;
                while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
                {
                    enter = false;
                    if (child.depth != property.depth + 1) continue;
                    if (module && moduleKind != null &&
                        ((child.name == "Trigger" && moduleKind.enumValueIndex != 0) ||
                         (child.name == "Effect" && moduleKind.enumValueIndex != 1) ||
                         (child.name == "Modifier" && moduleKind.enumValueIndex != 2) ||
                         ((child.name == "Value" || child.name == "SparkCost") && moduleKind.enumValueIndex != 1))) continue;
                    Draw(child, null);
                }
                EditorGUI.indentLevel--;
                return;
            }
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                string[] options = new string[property.enumNames.Length];
                for (int i = 0; i < options.Length; i++)
                    options[i] = EnumLabels.TryGetValue(property.enumNames[i], out string translated) ? translated : property.enumDisplayNames[i];
                property.enumValueIndex = EditorGUILayout.Popup(label, property.enumValueIndex, options);
            }
            else EditorGUILayout.PropertyField(property, new GUIContent(label), false);
        }

        [MenuItem("孤岛生存/历史演示/法术工坊/法术工坊规则")]
        private static void SelectConfiguration()
        {
            Object asset = AssetDatabase.LoadAssetAtPath<ForgeConfiguration>("Assets/TestGameplay/Spellcraft/Configuration/法术工坊配置.asset");
            Selection.activeObject = asset;
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        [MenuItem("孤岛生存/历史演示/法术工坊/打开法术工坊")]
        private static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Archives/Scenes/Spellcraft.unity");
        }
    }
}
