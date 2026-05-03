using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace WondrousTailsSolver.Ui;

internal sealed class ConfigWindow {
    public bool IsOpen;

    public void Draw() {
        if (!IsOpen) return;

        ImGui.SetNextWindowSize(new Vector2(350, 300), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("\u5929\u4E66\u6982\u7387\u52A9\u624B\u8BBE\u7F6E", ref IsOpen, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted("\u8BBE\u7F6E");
        ImGui.Separator();
        ImGui.TextWrapped("\u5F53\u524D\u7248\u672C\u6682\u65E0\u53EF\u914D\u7F6E\u9009\u9879\u3002");
        ImGui.End();
    }
}
