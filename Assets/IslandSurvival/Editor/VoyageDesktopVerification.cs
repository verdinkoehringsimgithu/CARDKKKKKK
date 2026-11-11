using System;
using System.Linq;
using System.Text;
using CardKKK.IslandSurvival.Core;
using CardKKK.IslandSurvival.Unity;
using UnityEditor;
using UnityEngine;

namespace CardKKK.IslandSurvival.Editor
{
    // Only an explicit menu click runs these checks. No scene, save file or runtime singleton is touched.
    internal static class VoyageDesktopVerification
    {
        [MenuItem("孤岛生存/开发工具/检查牌堆与场所交互")]
        private static void Run()
        {
            var report = new StringBuilder();
            try
            {
                VoyageConfiguration asset = AssetDatabase.LoadAssetAtPath<VoyageConfiguration>("Assets/IslandSurvival/Configuration/VoyageRules.asset");
                if (asset == null) throw new InvalidOperationException("找不到出航配置。");
                VoyageDefinition definition = asset.BuildDefinition();
                var engine = new VoyageEngine(definition);
                var definitions = definition.Cards.ToDictionary(card => card.Id);
                VoyageState initial = engine.Snapshot;
                int location = initial.Cards.First(card => definitions[card.CardId].Name == "海滩").Id;
                int[] legs = initial.Cards.Where(card => definitions[card.CardId].Name == "腿").Select(card => card.Id).ToArray();
                Require(legs.Length >= 2, "开局至少需要两张腿。");
                int count = initial.Cards.Count;

                if (definition.Desk.AllowStacking && definition.Desk.MaxStackSize >= 2)
                {
                    Require(engine.Stack(legs[0], legs[1], false).Success, "身体牌未能叠放。");
                    Require(engine.Snapshot.Cards.Count == count && engine.DragCards(legs[1], false).Count == 2, "堆叠丢失实例或没有形成牌堆。");
                    VoyageState roundTrip = JsonUtility.FromJson<VoyageState>(JsonUtility.ToJson(engine.Snapshot));
                    Require(engine.Restore(roundTrip).Success && engine.DragCards(legs[1], false).Count == 2, "保存结构往返后牌堆丢失。");
                    report.AppendLine("牌堆实例、整组查询和序列化往返：通过");
                }

                string beforeQuery = JsonUtility.ToJson(engine.Snapshot);
                engine.CanPut(legs[0], location, 0, out _);
                engine.PreviewDeskDrop(legs[0], .5f, .4f, false, out _, out _, out _);
                Require(beforeQuery == JsonUtility.ToJson(engine.Snapshot), "准入或落点预览修改了已提交状态。");
                Require(engine.OpenPreparation(location).Success, "无法打开海滩。");
                Require(engine.Put(legs[0], location, 0).Success, "第一张腿未进入指定槽。");
                Require(engine.Snapshot.Cards.All(card => card.Id != legs[0] || card.StackParent == 0), "入槽未从牌堆摘出单牌。");
                Require(engine.Put(legs[1], location, 0).Success, "占用槽位无法替换。");
                Require(engine.IsInSlot(legs[1]) && !engine.IsInSlot(legs[0]), "替换后新旧牌重复占用。");
                Require(engine.Put(legs[0], location, 1).Success, "第二槽无法接收腿。");
                Require(engine.Preview().Match == VoyageMatch.Ready, "两张腿没有组成真实海滩配方。");
                report.AppendLine("纯预览、单牌摘堆、指定槽替换及配方接入：通过");

                int invalid = engine.Snapshot.Cards.Where(card => card.Id != location && card.JobId == 0)
                    .Select(card => card.Id).First(id => !engine.CanPut(id, location, 0, out _));
                string beforeReject = JsonUtility.ToJson(engine.Snapshot);
                Require(!engine.Put(invalid, location, 0).Success, "不兼容牌被放入。");
                Require(beforeReject == JsonUtility.ToJson(engine.Snapshot), "失败入槽改变了状态。");
                Require(engine.Start().Success, "真实配方无法开工。");
                VoyageJobState job = engine.Snapshot.Jobs.Single();
                Require(engine.Slots(location).All(slot => slot.CardId == 0 && slot.Locked), "开工后准备槽未清空/锁定。");
                string busy = JsonUtility.ToJson(engine.Snapshot);
                Require(!engine.Put(invalid, location, 0).Success && busy == JsonUtility.ToJson(engine.Snapshot), "忙碌场所仍能收牌或拒绝修改了状态。");
                Require(engine.Cancel(job.Id).Success && engine.Snapshot.Cards.All(card => card.JobId == 0), "取消任务没有释放全部投入。");
                report.AppendLine("拒绝回滚、任务占用和取消返还：通过");

                Require(engine.Put(legs[0], location, 0).Success, "旧档迁移准备失败。");
                VoyageState legacy = engine.Snapshot;
                legacy.DeskVersion = 0;
                legacy.Workspaces.Clear();
                foreach (VoyageCardState card in legacy.Cards) card.StackParent = 0;
                Require(engine.Restore(legacy).Success && engine.IsInSlot(legs[0]), "旧准备列表未迁移到实际槽位。");
                VoyageState flatLegacy = engine.Snapshot;
                flatLegacy.DeskVersion = 0;
                flatLegacy.Workspaces.Clear();
                flatLegacy.Preparations.Clear();
                flatLegacy.PreparationLocation = 0;
                flatLegacy.Staged.Clear();
                flatLegacy.Staged.Add(location);
                flatLegacy.Staged.Add(legs[0]);
                Require(engine.Restore(flatLegacy).Success && engine.Snapshot.PreparationLocation == location && engine.IsInSlot(legs[0]),
                    "旧扁平组合中的场所本体未被迁移为独立场所。");
                VoyageCardState head = initial.Cards.First(card => definitions[card.CardId].Name == "头");
                bool fits = engine.PreviewDeskDrop(legs[1], head.X, head.Y, false, out float dropX, out float dropY, out string reason);
                Require(fits, "拥挤落点预览失败：" + reason);
                Require(engine.DropOnDesk(legs[1], head.X, head.Y, false).Success, "预览可落下但实际落牌失败。");
                VoyageCardState landed = engine.Snapshot.Cards.Single(card => card.Id == legs[1]);
                Require(landed.X == dropX && landed.Y == dropY, "落点预览与提交位置不一致。");
                report.AppendLine("桌面避让预览与提交一致：通过");
                VoyageState malformed = engine.Snapshot;
                VoyageWorkspace workspace = malformed.Workspaces.Single(item => item.Location == location);
                workspace.Slots[1].CardId = workspace.Slots[0].CardId;
                string committed = JsonUtility.ToJson(engine.Snapshot);
                Require(!engine.Restore(malformed).Success && committed == JsonUtility.ToJson(engine.Snapshot), "重复占槽存档未被拒绝或破坏已提交状态。");
                report.AppendLine("旧列表迁移、重复归属拒绝与恢复回滚：通过");
                report.AppendLine("本入口只核对核心。实际拖拽、画面、声音和旧文件指纹迁移仍需Play验收。");
            }
            catch (Exception exception)
            {
                report.AppendLine("未通过：" + exception.Message);
                Debug.LogException(exception);
            }
            EditorUtility.DisplayDialog("牌桌核心检查", report.ToString(), "关闭");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
