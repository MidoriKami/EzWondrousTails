using System.Drawing;
using System.Numerics;

using Dalamud.Configuration;
using Dalamud.Interface;

namespace WondrousTailsSolver;

public class Configuration : IPluginConfiguration {
    public int Version { get; set; }
    
    public Vector4 CurrentDutyColor = KnownColor.Red.Vector() with { W = 0.75f };

    public void Save() 
        => Service.Interface.SavePluginConfig(this);
}