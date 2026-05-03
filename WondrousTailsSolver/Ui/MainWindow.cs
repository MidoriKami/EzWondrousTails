using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace WondrousTailsSolver.Ui;

internal sealed unsafe class MainWindow {
    public bool IsOpen;

    public void Draw() {
        if (!IsOpen) return;

        ImGui.SetNextWindowSize(new Vector2(350, 300), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("\u5929\u4E66\u6982\u7387\u52A9\u624B", ref IsOpen)) {
            ImGui.End();
            return;
        }

        var playerState = PlayerState.Instance();
        if (playerState is null || !playerState->HasWeeklyBingoJournal) {
            ImGui.TextWrapped("\u5F53\u524D\u89D2\u8272\u6CA1\u6709\u53EF\u7528\u7684\u5929\u4E66\u5468\u5E38\u518C\u3002");
            ImGui.End();
            return;
        }

        System.PerfectTails.RefreshGameState();

        ImGui.TextUnformatted($"\u5DF2\u8D34\u8D34\u7EB8\uFF1A{playerState->WeeklyBingoNumPlacedStickers}/9");
        ImGui.TextUnformatted($"\u005B\u80E1\u601D\u4E71\u60F3\u005D\u70B9\u6570\uFF1A{playerState->WeeklyBingoNumSecondChancePoints}");
        ImGui.Separator();
        ImGui.TextWrapped(System.PerfectTails.GetProbabilityText());
        ImGui.Spacing();
        ImGui.TextUnformatted("\u68CB\u76D8\u72B6\u6001");

        for (var row = 0; row < 4; row++) {
            var line = string.Empty;
            for (var column = 0; column < 4; column++) {
                var filled = System.PerfectTails.GameState[(row * 4) + column];
                line += filled ? "\u25A0 " : "\u25A1 ";
            }

            ImGui.TextUnformatted(line.TrimEnd());
        }

        ImGui.End();
    }
}
