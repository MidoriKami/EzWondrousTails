using System.Numerics;

using Dalamud.Interface.Windowing;
using ImGuiNET;
using WondrousTailsSolver.Config;

namespace WondrousTailsSolver;

/// <summary>
/// Configuration window for editing saved config values.
/// </summary>
public class ConfigurationWindow : Window
{
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationWindow"/> class.
    /// </summary>
    /// <param name="config">Configuration object to edit.</param>
    public ConfigurationWindow(Configuration config)
        : base("EzWondrousTails - Configuration Window")
    {
        this.configuration = config;

        this.Size = new Vector2(300.0f, 150.0f);
        this.SizeCondition = ImGuiCond.Always;

        this.Flags |= ImGuiWindowFlags.NoResize;
    }

    /// <inheritdoc/>
    public override void Draw()
    {
        if (ImGui.ColorEdit4("Current Duty Border Color", ref this.configuration.CurrentDutyColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf))
        {
            this.configuration.Save();
        }
    }

    /// <inheritdoc/>
    public override void OnClose()
    {
        this.configuration.Save();
    }
}