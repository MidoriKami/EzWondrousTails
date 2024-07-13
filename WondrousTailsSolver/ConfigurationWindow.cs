using System.Numerics;

using ImGuiNET;
using Window = KamiLib.Window.Window;

namespace WondrousTailsSolver;

public class ConfigurationWindow : Window {
    private readonly Configuration configuration;

    public ConfigurationWindow(Configuration config) : base("EzWondrousTails - Configuration Window", new Vector2(200.0f, 200.0f)) {
        this.configuration = config;

        this.Flags |= ImGuiWindowFlags.NoResize;
    }

    protected override void DrawContents() {
        if (ImGui.ColorEdit4("Current Duty Border Color", ref this.configuration.CurrentDutyColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf)) {
            this.configuration.Save();
        }
    }
}