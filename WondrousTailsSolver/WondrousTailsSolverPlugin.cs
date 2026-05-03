using Dalamud.Plugin;
using WondrousTailsSolver.Ui;

namespace WondrousTailsSolver;

public sealed class WondrousTailsSolverPlugin : IDalamudPlugin {
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly MainWindow mainWindow = new();
    private readonly ConfigWindow configWindow = new();

    public WondrousTailsSolverPlugin(IDalamudPluginInterface pluginInterface) {
        this.pluginInterface = pluginInterface;

        System.PerfectTails = new PerfectTails();
        System.AddonWeeklyBingoController = new AddonWeeklyBingoController(pluginInterface);

        pluginInterface.UiBuilder.Draw += DrawUi;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
    }

    public void Dispose() {
        pluginInterface.UiBuilder.Draw -= DrawUi;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        System.AddonWeeklyBingoController.Dispose();
    }

    private void DrawUi() {
        mainWindow.Draw();
        configWindow.Draw();
    }

    private void OpenMainUi() {
        mainWindow.IsOpen = true;
    }

    private void OpenConfigUi() {
        configWindow.IsOpen = true;
    }
}
