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

        private AtkTextNode_SetText_Delegate AtkTextNode_SetText;

        private const int TotalStickers = PerfectTails.TotalStickers;
        private const int TotalLanes = PerfectTails.StickersPerLane;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");

            Address = new PluginAddressResolver();
            Address.Setup(Interface.TargetModuleScanner);

            AtkTextNode_SetText = Marshal.GetDelegateForFunctionPointer<AtkTextNode_SetText_Delegate>(Address.AtkTextNode_SetText_Address);

            QueueLoopTask = Task.Run(() => GameUpdaterLoop(LoopTokenSource.Token));
        }

        public void Dispose()
        {
            LoopTokenSource?.Cancel();
        }

        private Task QueueLoopTask;
        private readonly CancellationTokenSource LoopTokenSource = new CancellationTokenSource();
        private readonly bool[] GameState = new bool[TotalStickers];
        private string LastTextModification = "A standard default text placeholder that likely won't appear";
        private readonly PerfectTails PerfectTails = new PerfectTails();

        private async void GameUpdaterLoop(CancellationToken token)
        {
            for (int i = 0; i < GameState.Length; i++)
                GameState[i] = true;

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100);
                GameUpdater(token);
            }
        }

        private unsafe void GameUpdater(CancellationToken token)
        {
            var addonPtr = Interface.Framework.Gui.GetUiObjectByName("WeeklyBingo", 1);
            if (addonPtr == IntPtr.Zero)
                return;

            var addon = (AddonWeeklyBingo*)addonPtr;
            if (addon == null)
                return;

            if (!addon->AtkUnitBase.IsVisible || addon->AtkUnitBase.ULDData.LoadedState != 3 || addon->AtkUnitBase.ULDData.NodeListCount < 96)
                return;

            var textNode = (AtkTextNode*)addon->AtkUnitBase.ULDData.NodeList[96];
            if (textNode == null)
                return;

            var stateChanged = UpdateGameState(addon);
            var currentText = Marshal.PtrToStringAnsi(new IntPtr(textNode->NodeText.StringPtr));

            if (stateChanged)
                UpdateTextModification();

            if (!currentText.Contains("1 Line") && !currentText.Contains("2 Lines") && !currentText.Contains("3 lines"))
            {
                var textNodePtr = new IntPtr(textNode);
                var textPtr = Marshal.StringToHGlobalAnsi($"{currentText}\n{LastTextModification}");
                AtkTextNode_SetText(textNodePtr, textPtr, IntPtr.Zero);
                Marshal.FreeHGlobal(textPtr);
            }
        }
        private unsafe bool UpdateGameState(AddonWeeklyBingo* addon)
        {
            bool stateChanged = false;
            for (var i = 0; i < TotalStickers; i++)
            {
                var node = addon->StickerSlotList[i].StickerComponentBase;
                if (node == null)
                    return false;

                var parentNode = node->OwnerNode->AtkResNode.ParentNode;
                var state = parentNode->IsVisible;
                stateChanged |= GameState[i] != state;
                GameState[i] = state;
            }
            return stateChanged;
        }

        private void UpdateTextModification()
        {
            var stickersPlaced = GameState.Count(s => s);
            if (stickersPlaced == 9)
                return;

            var sb = new StringBuilder();
            var probs = PerfectTails.Solve(GameState);
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
            LastTextModification = sb.ToString();
        }
    }
}
