using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace WondrousTailsSolver;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
public class Service {
    [PluginService] public static IDalamudPluginInterface Interface { get; set; }
    [PluginService] public static IDataManager DataManager { get; set; }
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; set; }
    [PluginService] public static IPluginLog PluginLog { get; set; }
    [PluginService] public static IGameGui GameGui { get; set; }
    [PluginService] public static IAddonEventManager AddonEventManager { get; set; }
    [PluginService] public static ICondition Condition { get; set; }
    [PluginService] public static IClientState ClientState { get; set; }
}