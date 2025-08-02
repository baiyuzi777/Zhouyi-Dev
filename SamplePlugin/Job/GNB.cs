using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Zhouyi.Utils;

namespace Zhouyi.Job
{
    public static unsafe class Zhouyi_GNB
    {
        public static string JobName { get; } = "转圈测试版本";
        public static uint JobID { get; } = 37;
        public static string Actor { get; } = "HeS";
        public static string Version { get; } = "1.0.0";

        private static bool IsStarted { get; set; } = false;
        private static bool Usezhuanquan { get; set; } = false;
        private static bool Usefangyu { get; set; } = true;

        private static bool _lbReady = false;
        private static long _lastLBlianxujianTick = 0;

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 添加调试信息变量
        public static int DebugNearbyCount { get; set; } = 0; // 用于存储附近人数的调试信息
        public static string DebugLastAction { get; set; } = "无"; // 用于存储最后执行的动作
        public static string DebugNebulaStatus { get; set; } = "未检测到"; // 星云状态调试信息
        public static string DebugFateMarkStatus { get; set; } = "未检测到"; // 命运之印预备状态调试信息

        // 检测自身是否有“星云”状态
        public static bool HasNebulaStatus()
        {
            const uint nebulaStatusId = 3051; // 星云状态的ID
            var me = Plugin.ClientState.LocalPlayer;

            if (me == null || me.StatusList == null)
            {
                return false;
            }

            // 遍历状态列表，检查是否存在星云状态
            return me.StatusList.Any(status => status.StatusId == nebulaStatusId);
        }

        // 检测自身是否有“命运之印预备”状态
        public static bool HasFateMarkReady()
        {
            const uint fateMarkReadyStatusId = 4293; // 命运之印预备状态的ID
            var me = Plugin.ClientState.LocalPlayer;

            if (me == null || me.StatusList == null)
            {
                return false;
            }

            // 遍历状态列表，检查是否存在命运之印预备状态
            return me.StatusList.Any(status => status.StatusId == fateMarkReadyStatusId);
        }

        public static void OnDraw(Plugin plugin)
        {
            var jobIconTexture = TexturesHelper.GetTextureFromIconId(62000 + JobID);
            if (jobIconTexture != null)
            {
                ImGui.Image(jobIconTexture.ImGuiHandle, new Vector2(50, 50));
                ImGui.SameLine();
            }

            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), $"名称：{JobName}");
            ImGui.Text($"作者：{Actor}");
            ImGui.Text($"版本：{Version}");
            ImGui.Separator();

            // 添加总开关
            var isstarted = plugin.Configuration.GNB_IsStarted;
            if (ImGui.Checkbox("启用功能", ref isstarted))
            {
                plugin.Configuration.GNB_IsStarted = isstarted;
                plugin.Configuration.Save();
                IsStarted = isstarted;
            }

            if (!IsStarted) return; // 如果未启用功能，则不显示其他选项

            // 转圈功能开关
            var zhuanquan = plugin.Configuration.GNB_Lianxujiancount > 0;
            if (ImGui.Checkbox("转圈", ref zhuanquan))
            {
                plugin.Configuration.GNB_Lianxujiancount = zhuanquan ? 1 : 0;
                plugin.Configuration.Save();
                Usezhuanquan = zhuanquan;
            }

            // 应急防御功能开关
            var fangyu = plugin.Configuration.GNB_UseFangyu;
            ImGui.SameLine();
            if (ImGui.Checkbox("应急防御", ref fangyu))
            {
                plugin.Configuration.GNB_UseFangyu = fangyu;
                plugin.Configuration.Save();
                Usefangyu = fangyu;
            }

            // 转圈人数阈值
            ImGui.Text("转圈人数阈值 (1-10):");
            ImGui.SameLine();
            int zhuanquanThreshold = plugin.Configuration.GNB_ZhuanquanThreshold;
            if (ImGui.SliderInt("##ZhuanquanThreshold", ref zhuanquanThreshold, 1, 10)) // 限制范围为1到10
            {
                plugin.Configuration.GNB_ZhuanquanThreshold = zhuanquanThreshold;
                plugin.Configuration.Save();
            }

            // 应急防御血量阈值
            ImGui.Text("应急防御血量阈值 (0-100):");
            ImGui.SameLine();
            int fangyuThreshold = plugin.Configuration.GNB_FangyuThreshold;
            if (ImGui.SliderInt("##FangyuThreshold", ref fangyuThreshold, 0, 100))
            {
                plugin.Configuration.GNB_FangyuThreshold = fangyuThreshold;
                plugin.Configuration.Save();
            }

            // 调试输出星云状态
            bool nebulaStatus = HasNebulaStatus();
            ImGui.Text($"星云状态: {(nebulaStatus ? "检测到" : "未检测到")}");

            // 调试输出命运之印预备状态
            bool fateMarkReady = HasFateMarkReady();
            ImGui.Text($"命运之印预备状态: {(fateMarkReady ? "检测到" : "未检测到")}");

            // 调试输出周围人数
            ImGui.Text($"周围人数: {DebugNearbyCount}");
        }

        public static void OnStart(Plugin plugin)
        {
            IsStarted = plugin.Configuration.GNB_IsStarted;
            Usezhuanquan = plugin.Configuration.GNB_Lianxujiancount > 0;
            Usefangyu = plugin.Configuration.GNB_UseFangyu;
            _lbReady = false;
            _lastLBlianxujianTick = 0;
        }

        public static void OnUpdate(Plugin plugin)
        {
            if (!plugin.Configuration.GNB_IsStarted) { return; }
            var me = Plugin.ClientState.LocalPlayer;
            if (me == null) { return; }

            int zhuanquanThreshold = plugin.Configuration.GNB_ZhuanquanThreshold;
            float fangyuThreshold = plugin.Configuration.GNB_FangyuThreshold / 100f;

            // 统计自身5米范围人数
            int nearbyCount = 0;
            foreach (var obj in Zhouyi_PVPAPI.GameObjects())
            {
                if (obj == null || obj.IsDead) continue;
                if (Vector3.Distance(me.Position, obj.Position) <= 5f) // 固定为5米
                    nearbyCount++;
            }

            // 更新调试信息
            DebugNearbyCount = nearbyCount;

            // 满足人数阈值条件则执行命运之环
            if (nearbyCount >= Math.Clamp(zhuanquanThreshold, 1, 10)) // 限制阈值范围为1到10
            {
                bool fateRingUsed = false;

                // 检查命运之环是否可以使用
                if (Zhouyi_PVPAPI.IsActionCooldownReady(41511)) // 命运之环
                {
                    ActionManager.Instance()->UseAction(ActionType.Action, 41511);
                    fateRingUsed = true;
                    DebugLastAction = "命运之环";

                    // 记录命运之环的释放时间
                    _lastLBlianxujianTick = Now;

                    // 立即释放命运之印
                    if (Zhouyi_PVPAPI.IsActionCooldownReady(41442)) // 命运之印
                    {
                        ActionManager.Instance()->UseAction(ActionType.Action, 41442);
                        DebugLastAction = "命运之印";
                    }
                }

                // 如果命运之环成功释放，则设置 LB 准备状态
                if (fateRingUsed)
                {
                    _lbReady = true;
                }
            }

            // 检测自身是否有“命运之印预备”状态并释放命运之印
            if (HasFateMarkReady() && Zhouyi_PVPAPI.IsActionCooldownReady(41442)) // 命运之印
            {
                ActionManager.Instance()->UseAction(ActionType.Action, 41442);
                DebugLastAction = "命运之印（状态检测）";
            }

            // 更新星云状态调试信息
            DebugNebulaStatus = HasNebulaStatus() ? "检测到" : "未检测到";

            // 更新命运之印预备状态调试信息
            DebugFateMarkStatus = HasFateMarkReady() ? "检测到" : "未检测到";

            // 限制LB技能（连续剑）的释放条件
            if (HasNebulaStatus() && nearbyCount >= zhuanquanThreshold) // 检测到星云状态且周围人数达到阈值时释放
            {
                // 检查是否满足释放LB技能的条件
                if (Zhouyi_PVPAPI.IsActionCooldownReady(29130)) // 连续剑
                {
                    ActionManager.Instance()->UseAction(ActionType.Action, 29130);
                    DebugLastAction = "连续剑";
                    _lbReady = false; // 重置 LB 准备状态
                    _lastLBlianxujianTick = 0;
                }
            }

            // 应急防御逻辑（仅战斗状态下且自身HP低于配置的百分比自动释放）
            if (plugin.Configuration.GNB_UseFangyu && Plugin.Condition[ConditionFlag.InCombat])
            {
                if (me.CurrentHp < me.MaxHp * fangyuThreshold)
                {
                    if (Zhouyi_PVPAPI.IsActionCooldownReady(41443)) // 刚玉之心
                    {
                        ActionManager.Instance()->UseAction(ActionType.Action, 41443);
                        DebugLastAction = "刚玉之心";
                    }
                    if (Zhouyi_PVPAPI.IsActionCooldownReady(43244)) // 铁壁
                    {
                        ActionManager.Instance()->UseAction(ActionType.Action, 43244);
                        DebugLastAction = "铁壁";
                    }
                }
            }
        }

        public static void OnStop()
        {
            IsStarted = false;
            Usezhuanquan = false;
            Usefangyu = false;
            _lbReady = false;
            _lastLBlianxujianTick = 0;
        }
    }
}