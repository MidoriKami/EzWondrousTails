using Dalamud.Game.Addon.Lifecycle;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace WondrousTailsSolver;

internal sealed class DalamudServices {
    private static bool initialized;

    public static void Initialize(IDalamudPluginInterface pluginInterface) {
        if (initialized) {
            return;
        }

        pluginInterface.Create<DalamudServices>();
        initialized = true;
    }

    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
}
