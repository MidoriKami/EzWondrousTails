using System.Drawing;
using System.Numerics;

using Dalamud.Configuration;
using Dalamud.Interface;

namespace WondrousTailsSolver;

/// <summary>
/// Class that represents configuration data for EzWondrousTails.
/// </summary>
public class Configuration : IPluginConfiguration {
    /// <summary>
    /// The color to use to set the border of the WondrousTails task.
    /// </summary>
    public Vector4 CurrentDutyColor = KnownColor.Red.Vector() with { W = 0.75f };

    /// <inheritdoc/>
    public int Version { get; set; }

    /// <summary>
    /// Saves this configuration file.
    /// </summary>
    public void Save() => Service.Interface.SavePluginConfig(this);
}