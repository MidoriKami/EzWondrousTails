using Dalamud.Plugin;
using KamiLib.Window;
using KamiToolKit;

namespace WondrousTailsSolver;

public sealed class WondrousTailsSolverPlugin : IDalamudPlugin {
    public WondrousTailsSolverPlugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();

        System.PerfectTails = new PerfectTails();
        System.Configuration = Service.Interface.GetPluginConfig() as Configuration ?? new Configuration();

        System.NativeController = new NativeController(pluginInterface);
        
        System.WindowManager = new WindowManager(pluginInterface);
        System.WindowManager.AddWindow(new ConfigurationWindow(System.Configuration), WindowFlags.IsConfigWindow | WindowFlags.RequireLoggedIn);

        System.AddonWeeklyBingoController = new AddonWeeklyBingoController();
    }

    public void Dispose() {
        System.AddonWeeklyBingoController.Dispose();
        System.WindowManager.Dispose();
        System.NativeController.Dispose();
    }
}