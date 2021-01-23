using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Threading.Tasks;
using System.Threading;
using Dalamud.Hooking;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace WondrousTailsSolver
{
    public sealed class WondrousTailsSolverPlugin : IDalamudPlugin
    {
        public string Name => "ezWondrousTails";

        internal DalamudPluginInterface Interface;
        internal PluginAddressResolver Address;

        private Hook<AddonWeeklyBingo_Ctor_Delegate> AddonWeeklyBingo_Ctor_Hook;
        private AtkTextNode_SetText_Delegate AtkTextNode_SetText;
        private CancellationTokenSource LoopTokenSource;

        private const int TotalStickers = PerfectTails.TotalStickers;
        private const int TotalLanes = PerfectTails.StickersPerLane;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");

            Address = new PluginAddressResolver();
            Address.Setup(Interface.TargetModuleScanner);

            AddonWeeklyBingo_Ctor_Hook = new Hook<AddonWeeklyBingo_Ctor_Delegate>(Address.AddonWeeklyBingo_Ctor_Address, new AddonWeeklyBingo_Ctor_Delegate(AddonWeeklyBingo_Ctor_Detour), this);
            AtkTextNode_SetText = Marshal.GetDelegateForFunctionPointer<AtkTextNode_SetText_Delegate>(Address.AtkTextNode_SetText_Address);

            AddonWeeklyBingo_Ctor_Hook.Enable();
        }

        public void Dispose()
        {
            LoopTokenSource?.Cancel();

            AddonWeeklyBingo_Ctor_Hook?.Disable();
            AddonWeeklyBingo_Ctor_Hook?.Dispose();
        }

        #region Hooks

        private IntPtr AddonWeeklyBingo_Ctor_Detour(IntPtr a1)
        {
            var addonPtr = AddonWeeklyBingo_Ctor_Hook.Original(a1);

            for (int i = 0; i < TotalStickers; i++)
                GameState[i] = true;

            LoopTokenSource?.Cancel();
            LoopTokenSource = new CancellationTokenSource();
            Task.Run(() => UpdateTextLoop(addonPtr, LoopTokenSource.Token));
            return addonPtr;
        }

        private unsafe void UpdateTextLoop(IntPtr addonPtr, CancellationToken token)
        {
            if (addonPtr == IntPtr.Zero)
                return;

            var addon = (AddonWeeklyBingo*)addonPtr;

            if (addon == null)
                return;

            while (!token.IsCancellationRequested && addon->AtkUnitBase.IsVisible)
            {
                if (addon->AtkUnitBase.ULDData.LoadedState == 3 && addon->AtkUnitBase.ULDData.NodeListCount >= 96)
                {
                    var textNode = (AtkTextNode*)addon->AtkUnitBase.ULDData.NodeList[96];
                    if (textNode != null)
                    {
                        UpdateText(addon, textNode);
                    }
                }
                Task.Delay(100).Wait();
            }
        }

        private readonly bool[] GameState = new bool[TotalStickers];

        private unsafe void UpdateText(AddonWeeklyBingo* addon, AtkTextNode* textNode)
        {
            bool stateChanged = false;
            for (var i = 0; i < TotalStickers; i++)
            {
                var node = addon->StickerSlotList[i].StickerComponentBase;
                if (node == null)
                    return;

                var parentNode = node->OwnerNode->AtkResNode.ParentNode;
                var state = parentNode->IsVisible;
                stateChanged |= GameState[i] != state;
                GameState[i] = state;
            }

            if (!stateChanged)
                return;

            var stickersPlaced = GameState.Count(s => s);
            if (stickersPlaced == 9)
                return;

            var currentText = Marshal.PtrToStringAnsi(new IntPtr(textNode->NodeText.StringPtr));

            var sb = new StringBuilder();
            var probs = PerfectTails.Solve(GameState);
            sb.AppendLine(currentText);
            sb.AppendLine($"1 Line: {probs[0] * 100:F2}%");
            sb.AppendLine($"2 Lines: {probs[1] * 100:F2}%");
            sb.AppendLine($"3 Lines: {probs[2] * 100:F2}%");

            if (stickersPlaced > 0 && stickersPlaced <= 7)
            {
                var sample = PerfectTails.GetSample(stickersPlaced);
                sb.Append($"Shuffle Average: ");
                sb.Append($"{sample[0] * 100:F2}%   ");
                sb.Append($"{sample[1] * 100:F2}%   ");
                sb.Append($"{sample[2] * 100:F2}%   ");
            }

            var textNodePtr = new IntPtr(textNode);
            var textPtr = Marshal.StringToHGlobalAnsi(sb.ToString());
            AtkTextNode_SetText(new IntPtr(textNode), textPtr, IntPtr.Zero);
            Marshal.FreeHGlobal(textPtr);
        }

        #endregion

        private readonly PerfectTails PerfectTails = new PerfectTails();
    }
}
