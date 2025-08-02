using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using ECommons.Logging;
using ImGuiNET;
using Zhouyi.Utils;

namespace Zhouyi.Windows;

public unsafe class ConfigWindow : Window, IDisposable
{





    private Configuration Configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Zhouyi Test Config Window")
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(260, 400);
        SizeCondition = ImGuiCond.Always;
        
        Configuration = plugin.Configuration;
    }

    public void Dispose() 
    {
    }


    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {

        var drawline = Configuration.DrawLine;
        var linesize = Configuration.LineSize;
        var drawpoints = Configuration.DrawPoints;
        var pointsize = Configuration.PointSize;
        var drawhpmp = Configuration.DrawHPMPValue;
        var drawdebug = Configuration.DrawDebugMessage;
        if (ImGui.Checkbox("绘制透视线", ref drawline))
        {
            Configuration.DrawLine = drawline;
            Configuration.Save();
        }
        if (Configuration.DrawLine)
        {
            if (ImGui.SliderFloat("透视线粗细", ref linesize, 0, 10, "%.0f"))
            {
                Configuration.LineSize = linesize;
            }
        }
        
        if (ImGui.Checkbox("绘制透视点", ref drawpoints))
        {
            Configuration.DrawPoints = drawpoints;
            Configuration.Save();
        }
        if (Configuration.DrawPoints)
        {
            if (ImGui.SliderFloat("透视点大小", ref pointsize, 0, 10, "%.0f"))
            {
                Configuration.PointSize = pointsize;
            }
        }
        if (ImGui.Checkbox("绘制HP/MP数值", ref drawhpmp))
        {
            Configuration.DrawHPMPValue = drawhpmp;
            Configuration.Save();
        }
        if (ImGui.Checkbox("绘制DEBUG信息", ref drawdebug))
        {
            Configuration.DrawDebugMessage = drawdebug;
            Configuration.Save();
        }

    }



}
