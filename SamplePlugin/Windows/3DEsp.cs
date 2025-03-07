using Dalamud.Interface.Windowing;
using Zhouyi;
using System;
using System.Collections.Generic;
using ImGuiNET;
using System.Numerics;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Zhouyi.Utils;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using ECommons;
//using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ECommons.DalamudServices;
using System.Linq;
using Lumina.Data.Parsing.Layer;
using FFXIVClientStructs.FFXIV.Common.Lua;

namespace Zhouyi.Windows;
public unsafe class ESP : Window, IDisposable
{
    private Dictionary<(uint sourceId, uint skillId), (DateTime endTime, double totalCooldown)> cooldownEndTimes = new Dictionary<(uint, uint), (DateTime, double)>();
    private Configuration Configuration;
    private CanAttackDelegate CanAttack;
    private delegate int CanAttackDelegate(int arg, IntPtr objectAddress);
    private delegate void ReceiveActionEffectDelegate(uint sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
    private Hook<ReceiveActionEffectDelegate> ReceiveActionEffectHook;
    public static readonly string ReceiveActionEffectSig = "E8 ?? ?? ?? ?? 48 8B 8D F0 03 00 00";
    private MainWindow mainWindow;
    public ESP(Plugin plugin, MainWindow mainWindow) : base("IM ESP")
    {
        //ImDrawListPtr drawListPtr;
        Flags = ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoInputs |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoMouseInputs |
                ImGuiWindowFlags.NoScrollWithMouse |
                ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoNav |
                ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoFocusOnAppearing;
        var mainViewPort = ImGui.GetMainViewport();
        //Size = mainViewPort.Size;//new Vector2(232, 90);
        //Position = mainViewPort.Pos;
        //SizeCondition = ImGuiCond.Always;
        //ImGui.SetWindowPos(mainViewPort.Pos);
        //ImGui.SetWindowSize(mainViewPort.Size);
        //drawListPtr = ImGui.GetWindowDrawList();
        Configuration = plugin.Configuration;

        IntPtr receiveActionEffectPtr = Plugin.SigScanner.ScanText(ReceiveActionEffectSig);
        ReceiveActionEffectHook = Plugin.Hook.HookFromSignature<ReceiveActionEffectDelegate>(ReceiveActionEffectSig, ReceiveActionEffect);
        ReceiveActionEffectHook.Enable();

        this.mainWindow = mainWindow;
        Plugin.Framework.Update += this.OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {


        //if (Plugin.ClientState.LocalPlayer != null && Plugin.ClientState.IsPvP)
        if (Plugin.ClientState.LocalPlayer != null)
        {
            RefreshEnemyActorsAndAutoSelect();
        }
    }
    private List<Dictionary<string, object>> attackableActorsIds = new List<Dictionary<string, object>>();
    private void RefreshEnemyActorsAndAutoSelect()
    {


        if (Plugin.ClientState.LocalPlayer == null)
        {
            return;
        }
        lock (attackableActorsIds)
        {
            attackableActorsIds.Clear();
            foreach (var obj in Plugin.ObjectTable)
            {
                try
                {
                    //if (obj.ObjectKind == ObjectKind.Player && obj.GameObjectId != Plugin.ClientState.LocalPlayer.GameObjectId && CanAttack(142, obj.Address) == 1)
                    // 检查对象是否是玩家，并且不是当前玩家自己
                    if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player && obj.GameObjectId != Plugin.ClientState.LocalPlayer.GameObjectId)
                    {
                        // 确保 obj 是 IPlayerCharacter 类型
                        var character = obj as IPlayerCharacter;
                        if (character != null)
                        {
                            var Job = character.ClassJob.RowId;
                            var MaxHp = character.MaxHp;
                            var CurrentHp = character.CurrentHp;
                            var MaxMp = character.MaxMp;
                            var CurrentMp = character.CurrentMp;
                            var Name = character.Name;
                            var Pos = character.Position;
                            var ObjectId = character.GameObjectId;

                            //attackableActorsIds.Add(obj);
                            attackableActorsIds.Add(new Dictionary<string, object>
                            {
                                { "GameObjectId", ObjectId },
                                { "Name", Name },
                                { "Pos", Pos },
                                { "Job", Job },
                                { "MaxHp", MaxHp },
                                { "CurrentHp", CurrentHp },
                                { "MaxMp", MaxMp },
                                { "CurrentMp", CurrentMp }
                            });
                        }
                    }
                }
                catch (Exception)
                {

                    continue;
                }
            }
        }
    }
    public void Dispose() {
        attackableActorsIds.Clear();
        Plugin.Framework.Update -= this.OnFrameworkUpdate;
        ReceiveActionEffectHook?.Disable();
        ReceiveActionEffectHook?.Dispose();
    }


    public void TryOn()
    {
        attackableActorsIds.Clear();

    }
    private float elapsedTime = 0.0f;
    private bool isVisible = true; // 控制是否显示文本
    
    public override void Draw()
    {
        if (mainWindow.isVerified && Configuration.DrawDebugMessage) {
            ImGui.Text($"请到DC获取最新资讯");
            return;
        }
        var mainViewPort = ImGui.GetMainViewport();
        var currentWindowPos = mainViewPort.Pos;
        var currentWindowSize = mainViewPort.Size;
        var territory = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()!.GetRowOrDefault(Plugin.ClientState.TerritoryType);
        DateTime now = DateTime.UtcNow;
        float barWidth = 100;  // 固定的条形宽度
        float barHeight = 5;   // 降低的条形高度
        float outlineThickness = 1.0f;  // 描边厚度
        float textPadding = 5.0f;  // 数值与条形之间的间距
        var drawList = ImGui.GetWindowDrawList();
        var mypos = false;
        var onmypos = new Vector2(0, 0);


        HashSet<string> characters = new HashSet<string> { "s1p1", "w1p1","s5p1", "s5p3", "s5p2","s5p4","s5p5", "f1h1" };


        // 在每帧中设置窗口的大小和位置，确保它能根据主窗口的变化实时适应，可能会导致抖动（目前没有解决方案）
        ImGui.SetWindowPos(currentWindowPos);
        ImGui.SetWindowSize(currentWindowSize);
        if (Plugin.ClientState.LocalPlayer != null)
        { mypos = Plugin.GameGui.WorldToScreen(Plugin.ClientState.LocalPlayer.Position, out onmypos); }
        else
        {
            mypos = true;
            onmypos = new Vector2(currentWindowSize.X / 2, currentWindowSize.Y);
        }
        if (Configuration.DrawDebugMessage)
        {
            //var visibleOnScreen = gameGui.WorldToScreen(gameObject.Position, out var onScreenPosition);
            ImGui.Text($"学易方无大过，易其可不学乎?");
            ImGui.Text($"{attackableActorsIds}");
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.0f, 1.0f, 0.0f, 1.0f));
            ImGui.Text($"当前版本2024/10/3 感谢你使用Zhouyi");
            ImGui.PopStyleColor();


            // 在每帧更新
            if (Plugin.ClientState.LocalPlayer != null)
            {
                if (territory != null)
                { ImGui.Text($"Name:{territory.Value.Name}{territory.Value.PlaceNameRegion.Value.Name}{territory.Value.PlaceNameZone.Value.Name}{territory.Value.PlaceName.Value.Name}"); }
            }
            // 这里是控制显示你没有加载角色的报错
            else
            {
                attackableActorsIds.Clear();
                elapsedTime += ImGui.GetIO().DeltaTime;

                // 当累计时间超过1秒时，切换状态并重置计时器
                if (elapsedTime >= 1.0f)
                {
                    isVisible = !isVisible;
                    elapsedTime = 0.0f;
                }


                for (int i = 0; i < 20; i++)
                {
                    if (isVisible)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
                        ImGui.Text($"你没有加载角色！请等待角色加载！");
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        ImGui.Text($"");
                    }


                }

            }
        }

        if (territory != null && characters.Contains(territory.Value.Name.ToString()))
        {
            foreach (var actorInfo in attackableActorsIds)
            {
                if (actorInfo is Dictionary<string, object> dict)
                {
                    if (dict["Pos"] is Vector3 position)
                    {

                        var visibleOnScreen = Plugin.GameGui.WorldToScreen(position, out var onScreenPosition);

                        if (visibleOnScreen)
                        {
                            // 获取数值
                            float maxHp = dict["MaxHp"] != null ? Convert.ToSingle(dict["MaxHp"]) : 1;
                            float currentHp = dict["CurrentHp"] != null ? Convert.ToSingle(dict["CurrentHp"]) : 0;
                            float maxMp = dict["MaxMp"] != null ? Convert.ToSingle(dict["MaxMp"]) : 1;
                            float currentMp = dict["CurrentMp"] != null ? Convert.ToSingle(dict["CurrentMp"]) : 0;

                            // 计算百分比长度
                            float hpBarWidth = barWidth * (currentHp / maxHp); // 当前血量条长度
                            float mpBarWidth = barWidth * (currentMp / maxMp); // 当前蓝量条长度
                                                                               // 计算百分比
                            float hpPercentage = (currentHp / maxHp) * 100f;
                            // 定义绘制位置（让条形在玩家屏幕坐标的下方）
                            Vector2 hpBarPos = new Vector2(onScreenPosition.X - 100, onScreenPosition.Y + 20); // 血条位置
                            Vector2 mpBarPos = new Vector2(onScreenPosition.X - 100, onScreenPosition.Y + 20 + barHeight);  // 蓝条位置，紧贴血条

                            // 绘制血条
                            drawList.AddRectFilled(hpBarPos, new Vector2(hpBarPos.X + hpBarWidth, hpBarPos.Y + barHeight), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, .5f)));
                            drawList.AddRect(hpBarPos, new Vector2(hpBarPos.X + barWidth, hpBarPos.Y + barHeight), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 1)), 0, 0, outlineThickness);

                            // 绘制蓝条
                            drawList.AddRectFilled(mpBarPos, new Vector2(mpBarPos.X + mpBarWidth, mpBarPos.Y + barHeight), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 1, .5f)));
                            drawList.AddRect(mpBarPos, new Vector2(mpBarPos.X + barWidth, mpBarPos.Y + barHeight), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 1)), 0, 0, outlineThickness);

                            if (Configuration.DrawHPMPValue)
                            {
                                // 绘制数值
                                drawList.AddText(new Vector2(hpBarPos.X + barWidth + textPadding, hpBarPos.Y - 5), ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), $"{currentHp}/{maxHp}");
                                drawList.AddText(new Vector2(mpBarPos.X + barWidth + textPadding, mpBarPos.Y + textPadding - 5), ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), $"{currentMp}/{maxMp}");
                            }

                            // 玩家名字
                            Vector2 namePos = new Vector2(hpBarPos.X, mpBarPos.Y + barHeight + 5);
                            var nameSize = ImGui.CalcTextSize($"{dict["Name"]}");
                            float backgroundPaddingX = 5.0f;
                            float backgroundPaddingY = 2.0f;

                            // 职业图标相关
                            var jobId = Convert.ToUInt32(dict["Job"]); // 获取职业ID
                            var jobIconTexture = TexturesHelper.GetTextureFromIconId(62000 + jobId); // 获取职业图标纹理
                            Vector2 iconPos = new Vector2(namePos.X + nameSize.X + 5, namePos.Y); // 图标紧跟在名字的右边，5像素的间距
                            Vector2 iconSize = new Vector2(20, 20); // 图标的大小

                            // 调整背景框大小，使其包含职业图标
                            Vector2 backgroundTopLeft = new Vector2(namePos.X - backgroundPaddingX, namePos.Y - backgroundPaddingY);
                            Vector2 backgroundBottomRight = new Vector2(iconPos.X + iconSize.X + backgroundPaddingX, namePos.Y + nameSize.Y + backgroundPaddingY);

                            // 绘制背景框 & 名字 & 图标
                            drawList.AddRectFilled(backgroundTopLeft, backgroundBottomRight, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f)));
                            drawList.AddText(namePos, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), $"{dict["Name"]}");

                            if (jobIconTexture != null)
                            { drawList.AddImage(jobIconTexture.ImGuiHandle, iconPos, new Vector2(iconPos.X + iconSize.X, iconPos.Y + iconSize.Y)); }


                            // 绘制血量百分比
                            drawList.AddText(new Vector2(hpBarPos.X, hpBarPos.Y - 15), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 1)), $"{hpPercentage:0.0}%");

                            // 绘制技能冷却倒计时
                            var sourceId = Convert.ToUInt32(dict["GameObjectId"]);
                            DrawCooldownBars(sourceId, onScreenPosition, drawList, 20, 8);
                            if (Configuration.DrawPoints)
                            {
                                // 红点
                                float radius = Configuration.PointSize;  // 点的大小
                                drawList.AddCircleFilled(onScreenPosition, radius, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1)));
                                drawList.AddCircleFilled(onmypos, radius, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1)));
                            }
                            if (Configuration.DrawLine)

                            {
                                // 线
                                if (mypos)
                                {
                                    Vector2 screenTopLeft = onmypos;
                                    drawList.AddLine(onScreenPosition, screenTopLeft, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, .5f)), Configuration.LineSize); // 2.0f 是线的厚度
                                }
                                else
                                {
                                    Vector2 screenTopLeft = new Vector2(currentWindowSize.X / 2, currentWindowSize.Y);
                                    drawList.AddLine(onScreenPosition, screenTopLeft, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, .5f)), Configuration.LineSize); // 2.0f 是线的厚度
                                                                                                                                                                         //ImGui.Image(TexturesHelper.GetTextureFromIconId(62000 + dict["Job"]).ImGuiHandle, new Vector2(100, 100));
                                }
                            }




                        }
                    }
                }
                cooldownEndTimes = cooldownEndTimes
                    .Where(c => (c.Value.endTime - now).TotalSeconds > 0)
                    .ToDictionary(c => c.Key, c => c.Value);

            }
        }
        else
        {
            if (Configuration.DrawDebugMessage)
            {
                ImGui.Text($"你不在绘制的区域！");
            }
        }
        if (Configuration.DrawDebugMessage)
        {
            if (mypos) { ImGui.Text($"{onmypos}"); }
            else { ImGui.Text($"{new Vector2(currentWindowSize.X / 2, currentWindowSize.Y)}"); }

            if (Plugin.ClientState.LocalPlayer != null)
            {
                ImGui.Text($"{Plugin.ClientState.LocalPlayer.Name.TextValue}");
                ImGui.Text($"{Plugin.ClientState.LocalPlayer.HomeWorld.Value.Name}");
            }
        }
            //ImGui.Image(TexturesHelper.GetTextureFromIconId(199701).ImGuiHandle, new Vector2(100,100));

        }

    private void DrawCooldownBars(uint sourceId, Vector2 actorScreenPosition, ImDrawListPtr drawList, float radius, float thickness)
    {
        DateTime now = DateTime.UtcNow;
        Vector2 baseCooldownBarPos = new Vector2(actorScreenPosition.X - radius - 70, actorScreenPosition.Y - radius + 10);  
        int iconsPerRow = 3; 
        int iconCount = 0;  

        foreach (var cooldown in cooldownEndTimes)
        {
            if (cooldown.Key.sourceId == sourceId)
            {
                var (endTime, totalCooldown) = cooldown.Value;
                double remainingTime = (endTime - now).TotalSeconds;

                if (remainingTime > 0)
                {
                    // 动态调整图标的位置，防止重叠
                    Vector2 cooldownBarPos = baseCooldownBarPos;

                    // 如果图标数量超过一行，换行
                    if (iconCount >= iconsPerRow)
                    {
                        baseCooldownBarPos.X = actorScreenPosition.X - radius - 110; 
                        baseCooldownBarPos.Y += radius * 2 - 75; 
                        iconCount = 0; 
                    }

                    baseCooldownBarPos.X += radius * 2;  // 每个技能图标之间的水平间距

                    // 获取技能信息
                    uint skillId = cooldown.Key.skillId;
                    var actionData = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(skillId).Value;
                    string skillName = actionData?.Name.ToString() ?? "Unknown Skill";
                    var skillicon = actionData?.Icon ?? null;

                    // 计算进度
                    float cooldownProgress = (float)(remainingTime / totalCooldown); // 冷却进度百分比
                    float progressHeight = radius * 1.5f * cooldownProgress;  // 计算冷却条的高度

                    // 绘制技能图标
                    var skilliconTexture = TexturesHelper.GetTextureFromIconId((uint)skillicon);
                    Vector2 iconSize = new Vector2(radius * 1.5f, radius * 1.5f);  // 图标大小
                    Vector2 iconPos = new Vector2(cooldownBarPos.X - iconSize.X / 2, cooldownBarPos.Y - iconSize.Y / 2);  // 图标居中于位置

                    if (skilliconTexture != null)
                    {
                        // 绘制技能图标
                        drawList.AddImage(skilliconTexture.ImGuiHandle, iconPos, new Vector2(iconPos.X + iconSize.X, iconPos.Y + iconSize.Y));
                    }

                    // 绘制覆盖的冷却进度条 (从上到下)
                    Vector2 progressBarStart = new Vector2(iconPos.X, iconPos.Y);  // 冷却条开始位置（从图标顶部开始）
                    Vector2 progressBarEnd = new Vector2(iconPos.X + iconSize.X, iconPos.Y + progressHeight);  // 冷却条结束位置

                    // 绘制透明度遮罩
                    drawList.AddRectFilled(progressBarStart, progressBarEnd, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f)));  // 半透明的黑色冷却进度条

                    // 绘制倒计时文本
                    Vector2 textPos = new Vector2(cooldownBarPos.X - 15, cooldownBarPos.Y - radius * 0.5f);  // 显示在图标上方中间

                    // 绘制黑色边缘（略微偏移）
                    drawList.AddText(new Vector2(textPos.X - 1, textPos.Y - 1), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 1)), $"{remainingTime:F1}s");
                    drawList.AddText(new Vector2(textPos.X + 1, textPos.Y - 1), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 1)), $"{remainingTime:F1}s");
                    drawList.AddText(new Vector2(textPos.X - 1, textPos.Y + 1), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 1)), $"{remainingTime:F1}s");
                    drawList.AddText(new Vector2(textPos.X + 1, textPos.Y + 1), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 1)), $"{remainingTime:F1}s");

                    // 绘制红色的文本在中心
                    drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1)), $"{remainingTime:F1}s");

                    iconCount++;
                }
            }
        }
    }





    private void ReceiveActionEffect(uint sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail)
    {
        ReceiveActionEffectHook!.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);
        unsafe
        {
            uint actionId = *((uint*)effectHeader.ToPointer() + 0x2);  // 获取技能ID
            byte targetType = *((byte*)effectHeader.ToPointer() + 0x1F);  // 获取动作类型，1表示技能
            if (targetType != 1)
            {
                return;  // If not action type, return immediately
            }

            if (targetType == 1)
            {
                var recastTime = float.Parse(GetRecastTime(ActionType.Action, actionId));

                // 如果冷却时间大于10秒，记录冷却时间和总冷却时间
                if (recastTime > 10.0f)
                {
                    DateTime cooldownEndTime = DateTime.UtcNow.AddSeconds(recastTime);
                    cooldownEndTimes[(sourceId, actionId)] = (cooldownEndTime, recastTime);  // 记录总冷却时间
                }

                if (Configuration.DrawDebugMessage)
                { Plugin.Log.Debug($"Source:{sourceId} AcID:{actionId} AcType{targetType} RT:{recastTime}"); }
            }
        }
    }

    public static uint GetSpellActionId(uint actionId) => ActionManager.Instance()->GetAdjustedActionId(actionId);
    public static string GetRecastTime(ActionType type, uint actionId)
    {
        //float recast = ActionManager.Instance()->GetRecastTime(type, GetSpellActionId(actionId));
        var data = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(GetSpellActionId(actionId));
        var recast = $"{data.Value.Recast100ms / 10.0:f1}";
        return recast;
    }
}
