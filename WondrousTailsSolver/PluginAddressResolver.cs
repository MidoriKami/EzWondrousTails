using Dalamud.Game;
using Dalamud.Game.Internal;
using System;

namespace WondrousTailsSolver
{
    internal delegate void AtkTextNode_SetText_Delegate(IntPtr a1, IntPtr a2, IntPtr a3);
    internal delegate IntPtr AddonWeeklyBingo_Ctor_Delegate(IntPtr a1);

    internal sealed class PluginAddressResolver : BaseAddressResolver
    {
        private const string AtkTextNode_SetText_Signature = "4C 8B DC 53 55 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 89 73 18 48 8B EA";
        internal IntPtr AtkTextNode_SetText_Address;

        private const string AddonWeeklyBingo_Ctor_Signature = "48 89 5C 24 ?? 57 48 83 EC 20 48 8B D9 E8 ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 48 89 03 E8 ?? ?? ?? ?? 33 FF C6 83 ?? ?? ?? ?? ??";
        internal IntPtr AddonWeeklyBingo_Ctor_Address;

        protected override void Setup64Bit(SigScanner scanner)
        {
            AtkTextNode_SetText_Address = scanner.ScanText(AtkTextNode_SetText_Signature);
            AddonWeeklyBingo_Ctor_Address = scanner.ScanText(AddonWeeklyBingo_Ctor_Signature);
        }
    }
}
