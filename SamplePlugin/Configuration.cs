using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace Zhouyi;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    public bool DrawLine { get; set; } = false;
    public bool DrawPoints { get; set; } = true;
    public float LineSize { get; set; } = 2.0f;
    public float PointSize { get; set; } = 3.0f;
    public bool DrawHPMPValue { get; set; } = true;
    public bool DrawDebugMessage { get; set; } = true;
    public int SCH_GuDuKuoSanCount = 3;
    public float SCH_GuDuKuoSanHPLimit = 0.25f;
    public int SCH_GuDuRange = 25;
    public List<uint> _statusBlacklist { get; set; } = new List<uint>();
    public int 不选目标的指定状态id = 0;
    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }


    public uint[] OpcodesZoneDown = [];
    public uint[] OpcodesZoneUp = [723];
}
