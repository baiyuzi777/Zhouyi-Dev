using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace Zhouyi;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // 通用配置
    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    public bool DrawLine { get; set; } = false;
    public bool DrawPoints { get; set; } = true;
    public float LineSize { get; set; } = 2.0f;
    public float PointSize { get; set; } = 3.0f;
    public bool DrawHPMPValue { get; set; } = true;
    public bool DrawDebugMessage { get; set; } = true;

    // SCH 配置
    public int SCH_GuDuKuoSanCount = 3;
    public float SCH_GuDuKuoSanHPLimit = 0.25f;
    public int SCH_GuDuRange = 25;

    // GNB 配置

    public int GNB_ZhuanquanThreshold = 3; // 转圈人数阈值（默认值为3，范围1-10）
    public int GNB_FangyuThreshold = 50;  // 应急防御血量阈值（0-100）
   
    public bool GNB_UseFangyu { get; set; } = true; // 是否启用应急防御
    public bool GNB_IsStarted { get; set; } = true; // 是否启用功能
    public int GNB_Lianxujiancount { get; set; } = 0; // 转圈功能开关（0: 关闭, 1: 开启）

    // 状态黑名单
    public List<uint> _statusBlacklist { get; set; } = new List<uint>();
    public int 不选目标的指定状态id = 0;

    // Opcodes
    public uint[] OpcodesZoneDown = [];
    public uint[] OpcodesZoneUp = [723];

    // 保存配置
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
