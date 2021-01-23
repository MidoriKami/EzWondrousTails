using Dalamud.Game;
using Dalamud.Game.Internal;
using System;

namespace WondrousTailsSolver
{
    internal delegate void AtkTextNode_SetText_Delegate(IntPtr a1, IntPtr a2, IntPtr a3);

    internal sealed class PluginAddressResolver : BaseAddressResolver
    {
        private const string AtkTextNode_SetText_Signature = "4C 8B DC 53 55 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 89 73 18 48 8B EA";
        internal IntPtr AtkTextNode_SetText_Address;

        protected override void Setup64Bit(SigScanner scanner)
        {
            AtkTextNode_SetText_Address = scanner.ScanText(AtkTextNode_SetText_Signature);
        }
    }
}
