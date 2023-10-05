using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace WondrousTailsSolver;

/// <summary>
/// Dalamud and plugin services.
/// </summary>
internal class Service
{
    /// <summary>
    /// Gets the Dalamud plugin interface.
    /// </summary>
    [PluginService]
    internal static DalamudPluginInterface Interface { get; private set; } = null!;

    /// <summary>
    /// Gets the Dalamud data manager.
    /// </summary>
    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

    /// <summary>
    /// Gets the Dalamud signature scanner.
    /// </summary>
    [PluginService]
    internal static ISigScanner SigScanner { get; private set; } = null!;

    /// <summary>
    /// Gets the Dalamud Client State.
    /// </summary>
    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    /// <summary>
    /// Gets the Dalamud Condition class.
    /// </summary>
    [PluginService]
    internal static ICondition Condition { get; private set; } = null!;

    /// <summary>
    /// Gets the Dalamud Hooker class.
    /// </summary>
    [PluginService]
    internal static IGameInteropProvider Hooker { get; private set; } = null!;

    /// <summary>
    /// Gets the Dalamud AddonLifecycle class.
    /// </summary>
    [PluginService]
    internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    /// <summary>
    /// Gets the Dalamud Service.PluginLog class.
    /// </summary>
    [PluginService]
    internal static IPluginLog PluginLog { get; private set; } = null!;
}