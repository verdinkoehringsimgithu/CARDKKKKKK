using System;
using System.Collections.Generic;
using CardKKK.Tabletop.Unity;
using UnityEditor;

namespace CardKKK.Tabletop.Editor
{
    /// <summary>只在规则窗口的明确按钮调用时修改工程符号；导入与打开窗口均不会触发。</summary>
    internal static class CardTableFeatureCompiler
    {
        internal static readonly string[] FieldNames = { "Flip", "Sort", "Shuffle", "Reorder", "Pop", "Tidy", "Save" };
        internal static readonly string[] Labels = { "翻面", "牌堆排序", "洗牌", "堆内调序", "抽出顶牌", "整理桌面", "保存与读取" };
        internal static readonly string[] Symbols =
        {
            "CARDTABLE_DISABLE_FLIP", "CARDTABLE_DISABLE_SORT", "CARDTABLE_DISABLE_SHUFFLE",
            "CARDTABLE_DISABLE_REORDER", "CARDTABLE_DISABLE_POP", "CARDTABLE_DISABLE_TIDY", "CARDTABLE_DISABLE_SAVE"
        };
        internal static readonly string[] ModulePaths =
        {
            "Assets/CardTable/Core/Operations/BoardEngine.Flip.cs",
            "Assets/CardTable/Core/Operations/BoardEngine.Sort.cs",
            "Assets/CardTable/Core/Operations/BoardEngine.Shuffle.cs",
            "Assets/CardTable/Core/Operations/BoardEngine.Reorder.cs",
            "Assets/CardTable/Core/Operations/BoardEngine.Pop.cs",
            "Assets/CardTable/Core/Operations/BoardEngine.Tidy.cs",
            "Assets/CardTable/Unity/BoardSaveStore.cs"
        };

        internal static BuildTargetGroup TargetGroup
        {
            get
            {
                BuildTargetGroup selected = EditorUserBuildSettings.selectedBuildTargetGroup;
                return selected == BuildTargetGroup.Unknown
                    ? BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget) : selected;
            }
        }

        internal static bool[] Selection(FeatureSelection features)
        {
            return new[] { features.Flip, features.Sort, features.Shuffle, features.Reorder, features.Pop, features.Tidy, features.Save };
        }

        internal static HashSet<string> ReadSymbols()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (TargetGroup == BuildTargetGroup.Unknown) return result;
            foreach (string symbol in PlayerSettings.GetScriptingDefineSymbolsForGroup(TargetGroup).Split(';'))
            {
                string clean = symbol.Trim();
                if (clean.Length > 0) result.Add(clean);
            }
            return result;
        }

        internal static bool Matches(FeatureSelection features)
        {
            if (features == null || TargetGroup == BuildTargetGroup.Unknown) return false;
            bool[] requested = Selection(features);
            HashSet<string> active = ReadSymbols();
            for (int i = 0; i < Symbols.Length; i++)
                if (active.Contains(Symbols[i]) == requested[i]) return false;
            return true;
        }

        internal static void Apply(CardTableConfiguration configuration)
        {
            if (configuration == null || configuration.Features == null) return;
            if (TargetGroup == BuildTargetGroup.Unknown)
            {
                EditorUtility.DisplayDialog("无法应用编译裁剪", "请先在构建设置中选择 Windows 平台。", "知道了");
                return;
            }
            HashSet<string> symbols = ReadSymbols();
            bool[] selected = Selection(configuration.Features);
            bool changed = false;
            for (int i = 0; i < Symbols.Length; i++)
                changed |= selected[i] ? symbols.Remove(Symbols[i]) : symbols.Add(Symbols[i]);
            AssetDatabase.SaveAssets();
            if (!changed) return;
            var ordered = new List<string>(symbols);
            ordered.Sort(StringComparer.Ordinal);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(TargetGroup, string.Join(";", ordered));
        }
    }
}
