using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace WondrousTailsSolver
{
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
        internal static DataManager DataManager { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud signature scanner.
        /// </summary>
        [PluginService]
        internal static SigScanner SigScanner { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud Client State.
        /// </summary>
        [PluginService]
        internal static ClientState ClientState { get; private set; } = null!;
    }
}
