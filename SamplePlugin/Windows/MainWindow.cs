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
        : base("Zhouyi-请不要以付费形式购买！", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(550, 300),
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

        // Load the image from the file
        var goatImage = Plugin.TextureProvider.GetFromFile(GoatImagePath).GetWrapOrDefault();

        if (goatImage != null)
        {
            var windowSize = ImGui.GetWindowSize();
            var drawList = ImGui.GetWindowDrawList();

            var imageColor = new Vector4(1.0f, 1.0f, 1.0f, imageOpacity);

            drawList.AddImage(goatImage.ImGuiHandle, ImGui.GetWindowPos(), ImGui.GetWindowPos() + windowSize, Vector2.Zero, Vector2.One, ImGui.ColorConvertFloat4ToU32(imageColor));
        }
        else
        {
            ImGui.Text("Image not found.");
        }
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.0f, 0.0f, 0.0f, 1.0f)); // Black text
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.7f, 0.7f, 1.0f)); // Gray button
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.0f, 0.5f, 1.0f, 1.0f)); // Lighter blue button on hover
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.0f, 0.3f, 1.0f, 1.0f));  // Darker blue button on click


        // Other UI elements go on top of the image
        float padding = 10.0f; // 右边距
        float buttonWidth = 100.0f; // 每个按钮的宽度
        float totalButtonWidth = 3 * buttonWidth + 2 * ImGui.GetStyle().ItemSpacing.X; // 三个按钮加上两个间距

        // 获取窗口的宽度
        float windowWidth = ImGui.GetWindowWidth();

        // 计算按钮开始的 X 位置，以右对齐
        float startX = windowWidth - totalButtonWidth - padding;

        ImGui.Text("你好，我是你的私人天才妹妹助手！");
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

        if (ImGui.Button("配置"))
        {
            Plugin.ToggleConfigUI();
        }
        ImGui.SameLine();
        if (ImGui.Button("显示/隐藏 透视窗口"))
        {
            Plugin.ToggleESP();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("调试按钮:");
        ImGui.SameLine();
        if (ImGui.Button("刷新绘制列表"))
        {
            Plugin.EnableESP();
        }

        ImGui.Text("调整图片透明度:");
        ImGui.SameLine();
        ImGui.SliderFloat("Opacity", ref imageOpacity, 0.0f, 1.0f); // Slider to adjust opacity
        ImGui.Text("当前版本：");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
        ImGui.Text("2024/10/3");
        ImGui.PopStyleColor();

        if (Plugin.PluginInterface.IsDev)
        {
            lock (lockObj)
            {

                if (isVerified)
                {
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
                    ImGui.Text("插件不可用！");
                    ImGui.PopStyleColor();
                }

                if (!isVerified)
                {
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.0f, 1.0f, 0.0f, 1.0f));
                    ImGui.Text("插件可用！");
                    ImGui.PopStyleColor();
                }
            }
        }
        else
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            ImGui.Text("禁止本地加载！");
            isVerified = true;
            ImGui.PopStyleColor();
        }
        ImGui.Text("如果您对图片不喜欢的话可以自己替换插件目录下的");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
        ImGui.Text("background.png");
        ImGui.PopStyleColor();


        ImGui.PopStyleColor(4);


    }
}
