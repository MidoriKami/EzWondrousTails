using Dalamud.Game;
using Dalamud.Game.Internal;
using System;

namespace WondrousTailsSolver
{
    internal delegate void AddonWeeklyBingo_Update_Delegate(IntPtr addonPtr, float deltaLastUpdate);
    internal delegate void AtkTextNode_SetText_Delegate(IntPtr a1, IntPtr a2, IntPtr a3);

    internal sealed class PluginAddressResolver : BaseAddressResolver
    {
        private const string AddonWeeklyBingo_Update_Signature = "40 53 48 83 EC 30 F6 81 ?? ?? ?? ?? ?? 48 8B D9 0F 29 74 24 ?? 0F 28 F1 0F 84 ?? ?? ?? ?? 80 B9 ?? ?? ?? ?? ?? 48 89 6C 24 ??";
        private const string AtkTextNode_SetText_Signature = "4C 8B DC 53 55 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 89 73 18 48 8B EA";

        internal IntPtr AddonWeeklyBingo_Update_Address;
        internal IntPtr AtkTextNode_SetText_Address;

        protected override void Setup64Bit(SigScanner scanner)
        {
            AddonWeeklyBingo_Update_Address = scanner.ScanText(AddonWeeklyBingo_Update_Signature);
            AtkTextNode_SetText_Address = scanner.ScanText(AtkTextNode_SetText_Signature);
        }
    }
}
