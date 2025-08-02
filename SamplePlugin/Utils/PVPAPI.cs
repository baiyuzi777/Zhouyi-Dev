using Dalamud.Configuration;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMJIGatheringHouse;

namespace Zhouyi.Utils
{
    public static class Zhouyi_PVPAPI
    {
        public unsafe static bool IsActionCooldownReady(uint action) { return ActionManager.Instance()->IsActionOffCooldown(ActionType.Action, action); }
        public unsafe static bool CanAttackTarget(GameObject* source, GameObject* target, float distense)
        {
            if (!IsBlocked(source, target) && !IsOutOfRange(source, target, distense)) { return true; } 

            return false;

        }//这里只检查视线和距离

        public unsafe static bool IsMoving()
        {
            return AgentMap.Instance()->IsPlayerMoving;
        }

        public static unsafe bool IsBlocked(GameObject* source, GameObject* target)
        {
            var sourcePos = *source->GetPosition();
            var targetPos = *target->GetPosition();

            sourcePos.Y += 2;
            targetPos.Y += 2;

            var offset = targetPos - sourcePos;
            var maxDist = offset.Magnitude;
            var direction = offset / maxDist;
            bool hasHit = BGCollisionModule.RaycastMaterialFilter(sourcePos, direction, out _, maxDist);

            if (hasHit)
            {
                return true; // 有碰撞，视线被阻挡
            }

            return false; // 没有碰撞，视线清晰
        }
        public static unsafe bool IsOutOfRange(GameObject* source, GameObject* target, float distense)
        {
            if (Vector3.Distance(source->Position, target->Position) <= distense) { return false; }
            return true; // 超出范围
        }

        private static readonly CanAttackDelegate CanAttack = InitializeCanAttackDelegate();

        private delegate int CanAttackDelegate(int arg, IntPtr objectAddress);

        private static CanAttackDelegate InitializeCanAttackDelegate()
        {
            try
            {
                // 使用静态字段存储扫描结果，避免重复扫描
                var functionPtr = Plugin.SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3");
                return Marshal.GetDelegateForFunctionPointer<CanAttackDelegate>(functionPtr);
            }
            catch (Exception ex)
            {
                return (arg, addr) => 0; // 默认返回0
            }
        }

        public static unsafe List<IBattleChara> GameObjects()
        {
            var List_all = Plugin.ObjectTable; // Corrected to use Plugin.ObjectTable directly  
            List<IBattleChara> dict = new();

            foreach (var unit in List_all) // Plugin.ObjectTable is enumerable  
            {
                if (Plugin.ClientState.LocalPlayer == null)
                    continue;
                if (unit.GameObjectId == Plugin.ClientState.LocalPlayer.GameObjectId)
                    continue;
                if (CanAttack(142, unit.Address) != 1)
                    continue;
                dict.Add((IBattleChara)unit); // Cast to IBattleChara  
            }
            return dict;
        }//这里只遍历以及检查是否可以被攻击
        
        internal unsafe static IBattleChara GetNearestTarget(List<IBattleChara> dict,float dis,Plugin plugin,bool onlyplayer = false)
        {
            IBattleChara nearest = null;
            float minDistance = dis;

            if (dict == null || dict.Count == 0)
                return null;

            foreach (var obj in dict)
            {
                float distance = Vector3.Distance(obj.Position, Plugin.ClientState.LocalPlayer.Position);
                if (Plugin.ClientState.LocalPlayer == null)
                    continue;
                if (obj == null)
                    continue;
                if (onlyplayer && obj.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                    continue;
                if (obj == null || obj.IsDead || distance > dis)
                    continue;
                if (!CanAttackTarget((GameObject*)Plugin.ClientState.LocalPlayer.Address, (GameObject*)obj.Address,dis))
                    continue;
                foreach (var status in obj.StatusList)
                {
                    if (plugin.Configuration._statusBlacklist.Contains(status.StatusId)) { continue; }
                }
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = obj;
                }
            }

            return nearest;
        }

        // 新增方法：获取技能剩余冷却时间
        public unsafe static float GetNowActionRemainingTime(uint action)
        {
            return ActionManager.Instance()->GetRecastTime(ActionType.Action, action) - ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Action, action);
        }
    }
}