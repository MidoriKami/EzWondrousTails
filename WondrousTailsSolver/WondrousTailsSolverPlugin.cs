using Dalamud.Plugin;
using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WondrousTailsSolver
{
    public sealed class WondrousTailsSolverPlugin : IDalamudPlugin
    {
        public string Name => "ezWondrousTails";

        internal DalamudPluginInterface Interface;
        internal PluginAddressResolver Address;

        private Hook<AddonWeeklyBingo_Update_Delegate> AddonWeeklyBingo_Update_Hook;
        private AtkTextNode_SetText_Delegate AtkTextNode_SetText;

        private readonly bool[] GameState = new bool[16];
        private readonly PerfectTails PerfectTails = new();

        private UIGlowPayload GoldenGlow;
        private UIForegroundPayload SecretDelimiter;
        private UIForegroundPayload HappyGreen;
        private UIForegroundPayload MellowYellow;
        private UIForegroundPayload PinkSalmon;
        private UIForegroundPayload AngryRed;
        private UIForegroundPayload ColorOff;
        private UIGlowPayload GlowOff;

        private SeString LastCalculatedChancesSeString;
        private readonly TextPayload ErrorText = new("error");
        private readonly TextPayload ChancesText = new("Line Chances: ");
        private readonly TextPayload ShuffleText = new("\rShuffle Average: ");

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");

            Address = new PluginAddressResolver();
            Address.Setup(Interface.TargetModuleScanner);

            SecretDelimiter = new UIForegroundPayload(Interface.Data, 51);
            GoldenGlow = new UIGlowPayload(Interface.Data, 2);
            HappyGreen = new UIForegroundPayload(Interface.Data, 67);
            MellowYellow = new UIForegroundPayload(Interface.Data, 66);
            PinkSalmon = new UIForegroundPayload(Interface.Data, 561);
            AngryRed = new UIForegroundPayload(Interface.Data, 704);
            ColorOff = UIForegroundPayload.UIForegroundOff;
            GlowOff = UIGlowPayload.UIGlowOff;

            AddonWeeklyBingo_Update_Hook = new(Address.AddonWeeklyBingo_Update_Address, new AddonWeeklyBingo_Update_Delegate(AddonWeeklyBingo_Update_Detour), this);
            AddonWeeklyBingo_Update_Hook.Enable();

            AtkTextNode_SetText = Marshal.GetDelegateForFunctionPointer<AtkTextNode_SetText_Delegate>(Address.AtkTextNode_SetText_Address);
        }

        public void Dispose()
        {
            AddonWeeklyBingo_Update_Hook.Dispose();
        }

        private unsafe void AddonWeeklyBingo_Update_Detour(IntPtr addonPtr, float deltaLastUpdate)
        {
            AddonWeeklyBingo_Update_Hook.Original(addonPtr, deltaLastUpdate);

            try
            {
                var addon = (AddonWeeklyBingo*)addonPtr;

                if (!addon->AtkUnitBase.IsVisible || addon->AtkUnitBase.ULDData.LoadedState != 3)
                {
                    PluginLog.Debug("Addon not ready yet");
                    LastCalculatedChancesSeString = null;
                    return;
                }

                var stateChanged = UpdateGameState(addon);
                if (stateChanged)
                {
                    var placedStickers = GameState.Count(b => b);
                    if (placedStickers == 0 || placedStickers == 16 || placedStickers > 7)
                    {
                        // 0 and 16 are seen when the addon is loading. > 7 shuffling is disabled
                        LastCalculatedChancesSeString = null;
                        return;
                    }

                    var sb = new StringBuilder();
                    for (int i = 0; i < GameState.Length; i++)
                    {
                        sb.Append(GameState[i] ? "☒" : "☐");
                        if ((i + 1) % 4 == 0) sb.Append(" ");
                    }
                    PluginLog.Debug($"State has changed: {sb}");

                    var textNode = addon->StringThing.TextNode;
                    var existingBytes = ReadSeStringBytes(new IntPtr(textNode->NodeText.StringPtr));
                    var existingSeString = Interface.SeStringManager.Parse(existingBytes);

                    RemoveProbabilityString(existingSeString);

                    var probString = LastCalculatedChancesSeString = SolveAndGetProbabilitySeString();
                    existingSeString.Append(probString);

                    SetNodeText(textNode, existingSeString.Encode());
                }
                else if (LastCalculatedChancesSeString != null)
                {
                    var textNode = addon->StringThing.TextNode;
                    var existingBytes = ReadSeStringBytes(new IntPtr(textNode->NodeText.StringPtr));
                    var existingSeString = Interface.SeStringManager.Parse(existingBytes);

                    // Check for the Chances textPayload, if it doesn't exist we add the last known probString
                    if (!SeStringContainsProbabilityString(existingSeString))
                    {
                        existingSeString.Append(LastCalculatedChancesSeString);
                        SetNodeText(textNode, existingSeString.Encode());
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Boom");
            }
        }

        private unsafe bool UpdateGameState(AddonWeeklyBingo* addon)
        {
            var stateChanged = false;
            for (var i = 0; i < 16; i++)
            {
                var imageNode = (AtkImageNode*)addon->StickerSlotList[i].StickerSidebarResNode->ChildNode;

                if (imageNode == null)
                    return false;

                var state = imageNode->AtkResNode.Alpha_2 == 0;
                stateChanged |= GameState[i] != state;
                GameState[i] = state;
            }
            return stateChanged;
        }

        private byte[] ReadSeStringBytes(IntPtr stringPtr)
        {
            if (stringPtr == IntPtr.Zero)
                return null;

            var size = 0;
            while (Marshal.ReadByte(stringPtr, size) != 0)
                size++;

            var bytes = new byte[size];
            Marshal.Copy(stringPtr, bytes, 0, size);
            return bytes;
        }

        private SeString SolveAndGetProbabilitySeString()
        {
            var stickersPlaced = GameState.Count(s => s);

            // > 9 returns Error {-1,-1,-1} by the solver
            var values = PerfectTails.Solve(GameState);

            double[] samples = null;
            if (stickersPlaced > 0 && stickersPlaced <= 7)
                samples = PerfectTails.GetSample(stickersPlaced);

            if (values == PerfectTails.Error)
            {
                var seString = new SeString(new List<Payload>())
                    .Append(SecretDelimiter).Append("\r").Append(ColorOff)
                    .Append(ChancesText)
                    .Append(AngryRed).Append(ErrorText).Append(ColorOff).Append("  ")
                    .Append(AngryRed).Append(ErrorText).Append(ColorOff).Append("  ")
                    .Append(AngryRed).Append(ErrorText).Append(ColorOff);
                return seString;
            }
            else
            {
                var valuePayloads = StringFormatDoubles(values);
                var seString = new SeString(new List<Payload>())
                    .Append(SecretDelimiter).Append("\r").Append(ColorOff)
                    .Append(ChancesText);

                if (samples != null)
                {
                    foreach (var (value, sample, valuePayload) in Enumerable.Range(0, values.Length).Select(i => (values[i], samples[i], valuePayloads[i])))
                    {
                        var bound = 0.05;
                        var sampleBoundLower = Math.Max(0, sample - bound);
                        var sampleBoundUpper = Math.Min(1, sample + bound);

                        if (value == 1)
                            seString.Append(GoldenGlow).Append(valuePayload).Append(GlowOff);

                        else if (1 > value && value >= sample)
                            seString.Append(HappyGreen).Append(valuePayload).Append(ColorOff);

                        else if (sample > value && value > sampleBoundLower)
                            seString.Append(MellowYellow).Append(valuePayload).Append(ColorOff);

                        else if (sampleBoundLower > value && value > 0)
                            seString.Append(PinkSalmon).Append(valuePayload).Append(ColorOff);

                        else if (value == 0)
                            seString.Append(AngryRed).Append(valuePayload).Append(ColorOff);

                        else  // Just incase
                            seString.Append(valuePayload);

                        seString.Append("  ");
                    }

                    seString.Append(ShuffleText);
                    var sampleStrings = StringFormatDoubles(samples);
                    foreach (var sampleString in sampleStrings)
                        seString.Append(sampleString).Append("  ");
                }
                else
                {
                    foreach (var valueString in valuePayloads)
                        seString.Append(valueString).Append("  ");
                }

                /*
                UIForegroundPayload UIForeground(ushort id) => new UIForegroundPayload(Interface.Data, id);
                UIGlowPayload UIGlow(ushort id) => new UIGlowPayload(Interface.Data, id);

                var sheet = Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.UIColor>();
                int i = 1;
                List<uint> ignored = new();
                foreach (var row in sheet)
                {
                    if (ignored.Contains(row.RowId))
                        continue;

                    seString.Append(UIGlow((ushort)row.RowId));
                    //seString.Append(UIForeground((ushort)row.RowId));
                    seString.Append($"Ex{row.RowId:D3} ");
                    //seString.Append(ColorOff);
                    seString.Append(GlowOff);

                    i++;
                    if (i % 4 == 0)
                        seString.Append("\n");
                }
                */


                return seString;
            }
        }

        private TextPayload[] StringFormatDoubles(double[] values) => values.Select(v => new TextPayload($"{v * 100:F2}%")).ToArray();

        private bool SeStringTryFindDelimiter(SeString seString, out int index)
        {
            var secretBytes = SecretDelimiter.Encode();
            for (int i = 0; i < seString.Payloads.Count; i++)
            {
                var payload = seString.Payloads[i];
                if (payload is UIForegroundPayload)
                {
                    if (Enumerable.SequenceEqual(payload.Encode(), secretBytes))
                    {
                        index = i;
                        return true;
                    }
                }
            }
            index = -1;
            return false;
        }

        private bool SeStringContainsProbabilityString(SeString seString)
        {
            return SeStringTryFindDelimiter(seString, out var _);
        }

        private SeString RemoveProbabilityString(SeString seString)
        {
            if (SeStringTryFindDelimiter(seString, out var index))
                seString.Payloads.RemoveRange(index, seString.Payloads.Count - 1);
            return seString;
        }

        private unsafe void SetNodeText(AtkTextNode* textNode, byte[] bytes)
        {
            var textNodePtr = new IntPtr(textNode);
            var textPtr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, textPtr, bytes.Length);
            Marshal.WriteByte(textPtr, bytes.Length, 0);  // null terminated
            AtkTextNode_SetText(textNodePtr, textPtr, IntPtr.Zero);
            Marshal.FreeHGlobal(textPtr);
        }
    }
}
