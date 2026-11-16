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

                var atomic = new VoyageEngine(definition);
                string atomicBefore = JsonUtility.ToJson(atomic.Snapshot);
                Require(!atomic.Put(location, location, 0, true).Success && atomicBefore == JsonUtility.ToJson(atomic.Snapshot),
                    "失败投入切换了场所或修改了归属。");
                bool accepted = atomic.CanPut(legs[0], location, 0, out _);
                Require(accepted && atomicBefore == JsonUtility.ToJson(atomic.Snapshot), "完整入槽预览不纯或拒绝有效投入。");
                Require(atomic.Put(legs[0], location, 0, true).Success && atomic.Snapshot.PreparationLocation == location && atomic.IsInSlot(legs[0]),
                    "投入与切换场所没有一起提交。");
                atomicBefore = JsonUtility.ToJson(atomic.Snapshot);
                Require(!atomic.StagePair(location, legs[1], true).Success && atomicBefore == JsonUtility.ToJson(atomic.Snapshot),
                    "失败组合切换了场所或留下半份投入。");
                var heldTarget = new VoyageEngine(definition);
                heldTarget.Hold(legs[1], true);
                Require(!heldTarget.CanStack(legs[0], legs[1], false, out _), "持牌仍能接收自动归堆。");
                heldTarget.Hold(legs[1], false);
                report.AppendLine("投入/切换原子性、完整纯预览、组合失败回滚及持牌目标保护：通过");

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
                engine.Hold(legs[0], true);
                string heldBefore = JsonUtility.ToJson(engine.Snapshot);
                Require(!engine.Start().Success && heldBefore == JsonUtility.ToJson(engine.Snapshot), "开工抢占了正在拖动的投入牌。");
                engine.Hold(legs[0], false);
                Require(engine.Start().Success, "真实配方无法开工。");
                VoyageJobState job = engine.Snapshot.Jobs.Single();
                Require(engine.Slots(location).All(slot => slot.Locked), "工作槽未锁定。");
                Require(engine.Slots(location).Single(slot => slot.Index == 0).CardId == legs[1] &&
                    engine.Slots(location).Single(slot => slot.Index == 1).CardId == legs[0], "开工后工作牌没有保持原槽位。");
                Require(!engine.IsInSlot(legs[0]) && !engine.IsInSlot(legs[1]), "工作视图重复占用了准备槽归属。");
                VoyageState runningSave = JsonUtility.FromJson<VoyageState>(JsonUtility.ToJson(engine.Snapshot));
                Require(engine.Restore(runningSave).Success && engine.Slots(location).Single(slot => slot.Index == 0).CardId == legs[1], "工作槽保存往返后位置丢失。");
                VoyageState oldRunning = engine.Snapshot;
                foreach (VoyageJobInput input in oldRunning.Jobs.Single().Inputs) input.WorkspaceSlot = 0;
                Require(engine.Restore(oldRunning).Success && engine.Slots(location).Count(slot => slot.CardId != 0) == job.Inputs.Count,
                    "旧版任务没有回退显示全部工作牌。");
                Require(engine.Restore(runningSave).Success, "恢复工作槽检查现场失败。");
                // Separate engine: completing a real job must clear its projection and ownership once.
                var completion = new VoyageEngine(definition);
                Require(completion.Restore(runningSave).Success && completion.SetPaused(false).Success, "完成链准备失败。");
                for (int remaining = job.Remaining; remaining > 0; remaining -= Math.Min(240, remaining))
                    Require(completion.Advance(Math.Min(240, remaining)).Success, "任务结算失败。");
                VoyageState completed = completion.Snapshot;
                Require(!completed.Jobs.Any(task => task.Id == job.Id) && completed.Cards.All(card => card.JobId != job.Id),
                    "完成后任务或投入锁残留。");
                Require(completion.Slots(location).All(slot => slot.CardId == 0 && !slot.Locked), "完成后工作槽没有清理。");
                Require(legs.All(id => completed.Cards.Count(card => card.Id == id) == 1), "海滩探索完成后身体实例丢失或重复。");
                string completedBeforeCancel = JsonUtility.ToJson(completed);
                Require(!completion.Cancel(job.Id).Success && completedBeforeCancel == JsonUtility.ToJson(completion.Snapshot),
                    "重复取消已完成任务改变了状态。");
                report.AppendLine("真实任务完成、槽清理、实例守恒及完成后取消回滚：通过");
                string busy = JsonUtility.ToJson(engine.Snapshot);
                Require(!engine.Put(invalid, location, 0).Success && busy == JsonUtility.ToJson(engine.Snapshot), "忙碌场所仍能收牌或拒绝修改了状态。");
                Require(engine.Cancel(job.Id).Success && engine.Snapshot.Cards.All(card => card.JobId == 0), "取消任务没有释放全部投入。");
                Require(engine.Slots(location).All(slot => slot.CardId == 0 && !slot.Locked), "取消后工作槽投影没有清理。");
                report.AppendLine("拒绝回滚、工作槽位置/归属/保存兼容和取消返还：通过");

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
