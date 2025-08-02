using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Zhouyi.Utils;
using System.Threading;
using Lumina.Excel;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using Zhouyi.Job;
using System.Collections.Generic;
using System.Linq;

namespace Zhouyi.Windows;

public class MainWindow : Window, IDisposable
{
    private string GoatImagePath;
    private Plugin Plugin;
    private float imageOpacity = 0.8f; // Default opacity (1.0 means fully opaque)
    public bool isVerified { get; set; } = false; // Track whether the user is verified
    private bool isVerifying = false; // Track if verification is in progress
    private object lockObj = new object(); // For thread safety when updating variables

    public MainWindow(Plugin plugin, string goatImagePath)
        : base("Zhouyi-请不要以付费形式购买！")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(550, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        GoatImagePath = goatImagePath;
        Plugin = plugin;
    }

    public static void OpenWebPage(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception e)
        {
            Console.WriteLine("无法打开网页: " + e.Message);
        }
    }

    public void Dispose() { }

    public override void Draw()
    {


        // Other UI elements go on top of the image
        float padding = 10.0f; // 右边距
        float buttonWidth = 100.0f; // 每个按钮的宽度
        float totalButtonWidth = 3 * buttonWidth + 2 * ImGui.GetStyle().ItemSpacing.X; // 三个按钮加上两个间距

        // 获取窗口的宽度
        float windowWidth = ImGui.GetWindowWidth();

        // 计算按钮开始的 X 位置，以右对齐
        float startX = windowWidth - totalButtonWidth - padding;

        ImGui.Text("Zhouyi_PvpAuto(BETA)");
        if (ImGui.IsItemHovered())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f)); // Set tooltip text to white
            ImGui.SetTooltip("本插件完全免费，不要听信一切需要付费的话术！");
            ImGui.PopStyleColor(); // Restore previous color
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX);

        if (ImGui.Button("Discord", new System.Numerics.Vector2(buttonWidth, 0)))
        {
            OpenWebPage("https://discord.gg/g8QKPAnCBa");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f)); // Set tooltip text to white
            ImGui.SetTooltip("点击前往Discord。");
            ImGui.PopStyleColor(); // Restore previous color
        }
        ImGui.SameLine();

        if (ImGui.Button("Github", new System.Numerics.Vector2(buttonWidth, 0)))
        {
            OpenWebPage("https://github.com/extrant");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f)); // Set tooltip text to white
            ImGui.SetTooltip("点击前往Github。");
            ImGui.PopStyleColor(); // Restore previous color
        }
        ImGui.SameLine();

        if (ImGui.Button("爱发电", new System.Numerics.Vector2(buttonWidth, 0)))
        {
            OpenWebPage("https://afdian.com/a/Sincraft0515");
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f)); // Set tooltip text to white
            ImGui.SetTooltip("所有插件均为免费提供，您无需支付任何费用\n如果您选择赞助，这将是一种无偿捐赠，我们不会因此提供任何形式的承诺或回报\n在决定赞助之前，请仔细考虑");
            ImGui.PopStyleColor(); // Restore previous color
        }


        ImGui.Separator();
        ImGui.Spacing();

        // 标签页实现
        if (ImGui.BeginTabBar("MyTabBar"))
        {
            // 第一个标签页：角色信息
            if (ImGui.BeginTabItem("角色信息"))
            {
                var me = Plugin.ClientState.LocalPlayer;
                unsafe
                {
                    if (me != null)
                    {
                        ImGui.Text($"当前职业 {me.Struct()->ClassJob}:{me.ClassJob.Value.Name.ToString()}");
                        ImGui.SameLine();
                        ImGui.Text($" 当前血量 {me.CurrentHp}/{me.MaxHp}");
                        ImGui.SameLine();

                        ImGui.Text($" 龟壳状态 {ActionManager.Instance()->IsActionOffCooldown(ActionType.Action, 29054)}");

                        ImGui.Separator();
                        ImGui.Spacing();

                        if (me.Struct()->ClassJob == 28)
                        {
                            Zhouyi_SCH.OnDraw(Plugin);
                        }
                        else if (me.Struct()->ClassJob == 37)
                        {
                            Zhouyi_GNB.OnDraw(Plugin); // 新增：GNB职业调用
                        }
                    }
                }
                ImGui.EndTabItem();
            }

            // 第二个标签页：设置
            if (ImGui.BeginTabItem("设置"))
            {
                ImGui.Text("这里是全局设置界面");
                ImGui.Separator();
                ImGui.Spacing();
                DrawStatusShield();
                ImGui.EndTabItem();
            }

            // 第三个标签页：帮助
            if (ImGui.BeginTabItem("帮助"))
            {
                ImGui.Text("这是帮助标签页");
                ImGui.TextWrapped("在这里您可以找到关于本插件的使用说明和常见问题解答。如果您遇到任何问题，请前往 Discord 社区寻求帮助。");

                ImGui.Separator();
                ImGui.Text("常见问题:");
                ImGui.Text("Q: 如何启用自动模式?");
                ImGui.Text("A: 在设置标签页中勾选自动模式选项。");

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }


    }




    public void DrawStatusShield()
    {
        var statusIdInput = Plugin.Configuration.不选目标的指定状态id;
        ImGui.InputInt("状态 ID", ref statusIdInput);
        if (ImGui.Button("增加指定状态ID"))
        {
            Plugin.Configuration.不选目标的指定状态id = statusIdInput;
            if (Plugin.Configuration._statusBlacklist.Contains((uint)Plugin.Configuration.不选目标的指定状态id)) { return; }
            Plugin.Configuration._statusBlacklist.Add((uint)Plugin.Configuration.不选目标的指定状态id);
            //ConfigurationToSave.Save();
            Plugin.PluginInterface.SavePluginConfig(Plugin.Configuration);
        }
        ImGui.Text("遍历选人的屏蔽状态：");
        if (Plugin.Configuration._statusBlacklist.Count > 0)
        {
            ImGui.Separator();
            // 创建表格
            if (ImGui.BeginTable("NoSelectStatusTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 80.0f);
                ImGui.TableSetupColumn("状态名称", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 80.0f);
                ImGui.TableHeadersRow();

                // 存储要删除的索引
                var StatusindicesToRemove = new List<int>();

                for (int i = 0; i < Plugin.Configuration._statusBlacklist.Count; i++)
                {
                    var status = Plugin.Configuration._statusBlacklist[i];
                    var statusRow = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>().GetRowOrDefault((uint)status);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    if (statusRow != null)
                    {
                        ImGui.Text($"{statusRow.Value.RowId}");

                        ImGui.TableNextColumn();
                        ImGui.Text($"{statusRow.Value.Name}");
                    }
                    else
                    {
                        ImGui.Text($"{status}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"ERROR!");
                    }

                    ImGui.TableNextColumn();
                    if (ImGui.Button($"删除##{i}"))
                    {
                        StatusindicesToRemove.Add(i);
                    }
                }

                // 在循环外部处理删除操作
                foreach (var index in StatusindicesToRemove.OrderByDescending(x => x))
                {
                    Plugin.Configuration._statusBlacklist.RemoveAt(index);
                }

                // 只在有删除操作时保存一次
                if (StatusindicesToRemove.Any())
                {
                    Plugin.Configuration.Save();
                }

                ImGui.EndTable();
            }
            else { ImGui.Text("暂无添加的状态"); }
        }
    }
}
