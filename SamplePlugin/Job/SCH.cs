using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;
using Lumina.Excel.Sheets.Experimental;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Zhouyi.Utils;

namespace Zhouyi.Job;

public unsafe class Zhouyi_SCH
{
    public static string JobName { get; } = "学者测试版本";
    public static uint JobID { get; } = 28;
    public static string Actor { get; } = "Siren";
    public static string Version { get; } = "1.0.1";

    private static bool IsStarted { get; set; } = false;
    private static bool UseJiyan { get; set; } = false;
    private static bool UseGudu { get; set; } = true;

    // 记录最近一次蛊毒／扩散相关技能释放的时间（毫秒）
    private static long _lastGuDuRelatedTick = 0;

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static void OnDraw(Plugin plugin)
    {
        var jobIconTexture = TexturesHelper.GetTextureFromIconId(62000 + JobID); // 获取职业图标纹理
        var cursorPos = ImGui.GetCursorPos();
        if (jobIconTexture != null)
        { ImGui.Image(jobIconTexture.ImGuiHandle, new System.Numerics.Vector2(50,50)); }
        // 设置文本与图片的间距
        ImGui.SameLine();
        ImGui.SetCursorPosY(cursorPos.Y + 5); // 垂直居中偏移

        // 使用分组保持布局紧凑
        ImGui.BeginGroup();
        ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.8f, 0.0f, 1.0f), $"名称：{JobName}");
        ImGui.Text($"作者：{Actor}");
        ImGui.Text($"版本：{Version}");
        ImGui.EndGroup();
        ImGui.Separator();
        ImGui.Spacing();
        var isstarted = IsStarted;
        if (ImGui.Checkbox("开启", ref isstarted)) {IsStarted=isstarted; }
        var jiyan = UseJiyan;
        var gudu = UseGudu;
        ImGui.Separator();
        ImGui.Spacing();
        if (ImGui.Checkbox("极炎法", ref jiyan)) { UseJiyan = jiyan; }
        ImGui.SameLine();
        if (ImGui.Checkbox("自动蛊毒法", ref gudu)) { UseGudu = gudu; }
        var count = plugin.Configuration.SCH_GuDuKuoSanCount;

        if (ImGui.SliderInt("扩毒人数", ref count, 0, 10))
        {
            plugin.Configuration.SCH_GuDuKuoSanCount = count;
            plugin.Configuration.Save();
        }
        var hplimit = plugin.Configuration.SCH_GuDuKuoSanHPLimit;
        if (ImGui.SliderFloat("遍历血量阈值", ref hplimit, 0, 1, "%.2f"))
        {
            plugin.Configuration.SCH_GuDuKuoSanHPLimit = hplimit;
            plugin.Configuration.Save();
        }
        var gudurange = plugin.Configuration.SCH_GuDuRange;
        if (ImGui.SliderInt("起手毒判定距离", ref gudurange, 0, 25))
        {
            plugin.Configuration.SCH_GuDuRange = gudurange;
            plugin.Configuration.Save();
        }





    }

    public static void OnUpdate(Plugin plugin)
    {
        if (!IsStarted) { return; }
        var me = Plugin.ClientState.LocalPlayer;
        if (me != null)
        {
            if (me.StatusList.Any(status => status.StatusId == 3054)) { return; }
            List<IBattleChara> table = Zhouyi_PVPAPI.GameObjects();
            if (table != null)
            {
                if (UseGudu && Zhouyi_PVPAPI.IsActionCooldownReady(29233))
                {
                    IBattleChara gudu_target = GetBestGuDuFaTarget(table, plugin.Configuration.SCH_GuDuRange, 15, plugin, triggerCount: plugin.Configuration.SCH_GuDuKuoSanCount);
                    if (gudu_target != null && Zhouyi_PVPAPI.IsActionCooldownReady(29236) && !me.StatusList.Any(status => status.StatusId == 3094))
                    {
                        ActionManager.Instance()->UseAction(ActionType.Action, 29236);
                        _lastGuDuRelatedTick = Now;
                        return;
                    }

                    if (gudu_target != null && me.StatusList.Any(status => status.StatusId == 3094))
                    {
                        Plugin.TargetManager.Target = gudu_target;
                        ActionManager.Instance()->UseAction(ActionType.Action, 29233, gudu_target.GameObjectId);
                        _lastGuDuRelatedTick = Now;
                        return;
                    }


                }

                if (Zhouyi_PVPAPI.IsActionCooldownReady(29234))
                {
                    IBattleChara zhankai_target = GetBestSpreadTarget(table, 30, 15, plugin, triggerCount: plugin.Configuration.SCH_GuDuKuoSanCount);
                    if (zhankai_target != null)
                    {
                        ActionManager.Instance()->UseAction(ActionType.Action, 29234, zhankai_target.GameObjectId);
                        _lastGuDuRelatedTick = Now;
                        return;
                    }
                }

                IBattleChara near_target = Zhouyi_PVPAPI.GetNearestTarget(table, 25, plugin);

                if (near_target != null)
                {

                    if (UseJiyan
                        && !Zhouyi_PVPAPI.IsMoving()
                        && Zhouyi_PVPAPI.IsActionCooldownReady(29231)
                        && !me.StatusList.Any(status => status.StatusId == 3094)
                        // 避免刚释放蛊毒/扩散后立即判定状态缺失导致误放 29231
                        && (Now - _lastGuDuRelatedTick) > 1200)
                    {
                        ActionManager.Instance()->UseAction(ActionType.Action, 29231, near_target.GameObjectId);
                        return;
                    }
                }
            }
        }
    }

    internal unsafe static IBattleChara GetBestGuDuFaTarget(List<IBattleChara> dict, float castRange, float spreadRadius, Plugin plugin, int triggerCount = 3, bool onlyplayer = false)
    {
        // 该方法用于寻找在技能施放范围(castRange)内，且自身未被我方施加"蛊毒法"(statusId=3089)
        // 的敌人作为中心目标，使得其周围(spreadRadius)内同样未被我方施加该状态的敌人数量最多。
        // 若满足人数达到 triggerCount，则返回该目标，否则返回 null。

        const uint statusId = 3089; // 蛊毒法状态ID
        IBattleChara bestTarget = null;
        int bestCount = triggerCount - 1; // 至少需要满足 triggerCount 才触发

        if (dict == null || dict.Count == 0)
            return null;
        if (Plugin.ClientState.LocalPlayer == null)
            return null;

        var myId = Plugin.ClientState.LocalPlayer.EntityId;

        foreach (var candidate in dict)
        {
            if (candidate == null || candidate.IsDead)
                continue;
            if (onlyplayer && candidate.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                continue;

            // 与自身距离检查
            float distanceFromMe = Vector3.Distance(candidate.Position, Plugin.ClientState.LocalPlayer.Position);
            if (distanceFromMe > castRange)
                continue;
            float hppercent = candidate.CurrentHp / candidate.MaxHp;
            if (hppercent < plugin.Configuration.SCH_GuDuKuoSanHPLimit)
                continue;

            // 视线/距离合法性检查
            if (!Zhouyi_PVPAPI.CanAttackTarget((GameObject*)Plugin.ClientState.LocalPlayer.Address, (GameObject*)candidate.Address, castRange))
                continue;

            // 若目标已拥有稳定的蛊毒法(剩余时间>=5秒)，则无需再次施放
            if (HasStatusFromMe(candidate, statusId, myId))
                continue;

            // 统计其周围 spreadRadius 内未被我方稳定蛊毒法影响的敌人数
            int notPoisonedCount = 0;
            foreach (var around in dict)
            {
                if (around == null || around.IsDead)
                    continue;
                if (onlyplayer && around.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                    continue;
                if (Vector3.Distance(candidate.Position, around.Position) > spreadRadius)
                    continue;

                if (!HasStatusFromMe(around, statusId, myId))
                    notPoisonedCount++;
            }

            // 修复：只有当未中毒人数 >= triggerCount 时才选择该目标
            if (notPoisonedCount >= triggerCount && notPoisonedCount > bestCount)
            {
                bestCount = notPoisonedCount;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// 判断指定目标身上是否存在由本地玩家施加、剩余时间不少于 5 秒的指定状态。
    /// </summary>
    private static bool HasStatusFromMe(IBattleChara target, uint statusId, ulong myId, float minRemainingTime = 5f)
    {
        if (target == null) return false;

        foreach (var status in target.StatusList)
        {
            if (status.StatusId == statusId && status.SourceId == myId && status.RemainingTime >= minRemainingTime)
            {
                return true;
            }
        }
        return false;
    }

    internal unsafe static IBattleChara GetBestSpreadTarget(List<IBattleChara> dict, float castRange, float spreadRadius, Plugin plugin, int triggerCount = 3, bool onlyplayer = false)
    {
        // 该方法用于寻找在技能施放范围(castRange)内，且已被我方施加"蛊毒法"(statusId=3089) 的目标，
        // 其周围(spreadRadius)内依旧未被我方施加蛊毒法的敌人数最多。
        // 人数达到 triggerCount 时返回该目标，否则返回 null。
        const uint statusId = 3089;
        IBattleChara bestTarget = null;
        int bestCount = triggerCount - 1;

        if (dict == null || dict.Count == 0)
            return null;
        if (Plugin.ClientState.LocalPlayer == null)
            return null;

        var myId = Plugin.ClientState.LocalPlayer.EntityId;

        foreach (var candidate in dict)
        {
            if (candidate == null || candidate.IsDead)
                continue;
            if (onlyplayer && candidate.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                continue;

            float distanceFromMe = Vector3.Distance(candidate.Position, Plugin.ClientState.LocalPlayer.Position);
            if (distanceFromMe > castRange)
                continue;
            float hppercent = candidate.CurrentHp / candidate.MaxHp;
            if (hppercent < plugin.Configuration.SCH_GuDuKuoSanHPLimit)
                continue;
            if (!Zhouyi_PVPAPI.CanAttackTarget((GameObject*)Plugin.ClientState.LocalPlayer.Address, (GameObject*)candidate.Address, castRange))
                continue;

            // 目标身上必须有由我施加并且剩余时间>=5秒的蛊毒法
            if (!HasStatusFromMe(candidate, statusId, myId))
                continue;

            int notPoisonedCount = 0;
            foreach (var around in dict)
            {
                if (around == null || around.IsDead)
                    continue;
                if (onlyplayer && around.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                    continue;
                if (Vector3.Distance(candidate.Position, around.Position) > spreadRadius)
                    continue;

                if (!HasStatusFromMe(around, statusId, myId))
                    notPoisonedCount++;
            }

            // 修复：只有当未中毒人数 >= triggerCount 时才选择该目标
            if (notPoisonedCount > bestCount)
            {
                bestCount = notPoisonedCount;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }


}

