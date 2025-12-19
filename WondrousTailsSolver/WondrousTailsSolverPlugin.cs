using Dalamud.Plugin;
using KamiToolKit;

namespace WondrousTailsSolver;

public sealed class WondrousTailsSolverPlugin : IDalamudPlugin {
    public WondrousTailsSolverPlugin(IDalamudPluginInterface pluginInterface) {
        System.PerfectTails = new PerfectTails();
        System.AddonWeeklyBingoController = new AddonWeeklyBingoController(pluginInterface);
    }

    public void Dispose() {
        System.AddonWeeklyBingoController.Dispose();
    }
}