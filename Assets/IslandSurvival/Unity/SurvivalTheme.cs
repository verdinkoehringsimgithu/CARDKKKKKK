using CardKKK.IslandSurvival.Core;
using UnityEngine;

namespace CardKKK.IslandSurvival.Unity
{
    [CreateAssetMenu(menuName = "孤岛生存/界面主题", fileName = "孤岛生存主题")]
    public sealed class SurvivalTheme : ScriptableObject
    {
        [InspectorName("材料牌底色")] public Color Material = new Color(.22f,.30f,.32f);
        [InspectorName("工具牌底色")] public Color Tool = new Color(.28f,.30f,.35f);
        [InspectorName("设施牌底色")] public Color Facility = new Color(.25f,.32f,.30f);
        [InspectorName("区域牌底色")] public Color Area = new Color(.16f,.36f,.33f);
        [InspectorName("生物牌底色")] public Color Creature = new Color(.29f,.34f,.26f);
        [InspectorName("状态牌底色")] public Color Condition = new Color(.46f,.20f,.22f);
        [InspectorName("变化闪色")] public Color Pulse = new Color(.75f,.60f,.32f);
        [InspectorName("变化时长（现实秒）"),Min(.05f)] public float PulseSeconds = .3f;
        [InspectorName("成功声音")] public AudioClip Success;
        [InspectorName("拒绝声音")] public AudioClip Denied;
        [InspectorName("危险声音")] public AudioClip Warning;
        [InspectorName("音量"),Range(0,1)] public float Volume = .25f;
        [InspectorName("可执行工艺颜色")] public Color Ready = new Color(.16f,.36f,.30f);
        [InspectorName("缺少条件的工艺颜色")] public Color MissingMaterial = new Color(.40f,.30f,.17f);
        [InspectorName("缺少设备的工艺颜色")] public Color MissingFacility = new Color(.39f,.22f,.23f);
        [InspectorName("制作人员与资源来源"),TextArea(6,12)] public string Credits = "设计与制作：项目作者\n开发协作：Codex\n插画：项目内原创生成资产\n声音：项目内原创合成\n字体：Noto Sans CJK SC · SIL Open Font License";
        public Color CardColor(CardKind kind)
        {
            switch (kind) { case CardKind.Tool:return Tool; case CardKind.Facility:return Facility; case CardKind.Area:return Area; case CardKind.Creature:return Creature; case CardKind.Condition:return Condition; default:return Material; }
        }
    }
}
