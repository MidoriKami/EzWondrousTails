using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Sheets = Lumina.Excel.GeneratedSheets;

namespace WondrousTailsSolver;

/// <summary>
/// Main plugin implementation.
/// </summary>
public sealed unsafe class WondrousTailsSolverPlugin : IDalamudPlugin
{
    private static readonly UIGlowPayload GoldenGlow = new(2);
    private static readonly UIForegroundPayload SecretDelimiter = new(51);
    private static readonly UIForegroundPayload HappyGreen = new(67);
    private static readonly UIForegroundPayload MellowYellow = new(66);
    private static readonly UIForegroundPayload PinkSalmon = new(561);
    private static readonly UIForegroundPayload AngryRed = new(704);
    private static readonly UIForegroundPayload ColorOff = UIForegroundPayload.UIForegroundOff;
    private static readonly UIGlowPayload GlowOff = UIGlowPayload.UIGlowOff;

    private static readonly TextPayload ErrorText = new("error");
    private static readonly TextPayload ChancesText = new("Line Chances: ");
    private static readonly TextPayload ShuffleText = new("\rShuffle Average: ");

    private AtkTextNode* probabilityTextNode = null;

    private readonly PerfectTails perfectTails = new();
    private readonly bool[] gameState = new bool[16];

    private Hook<DutyReceiveEventDelegate>? addonDutyReceiveEventHook;

    /// <summary>
    /// Initializes a new instance of the <see cref="WondrousTailsSolverPlugin"/> class.
    /// </summary>
    /// <param name="pluginInterface">Dalamud plugin interface.</param>
    public WondrousTailsSolverPlugin(DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "WeeklyBingo", this.AddonSetupDetour);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "WeeklyBingo", this.AddonRefreshDetour);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "WeeklyBingo", this.AddonFinalizeDetour);
    }

    private delegate void DutyReceiveEventDelegate(IntPtr addonPtr, ushort a2, uint a3, IntPtr a4, IntPtr a5);

    /// <inheritdoc/>
    public void Dispose()
    {
        Service.AddonLifecycle.UnregisterListener(this.AddonRefreshDetour, this.AddonSetupDetour, this.AddonFinalizeDetour);

        this.addonDutyReceiveEventHook?.Dispose();
    }

    // Color format is RGBA
    private static void SetDutySlotBorderColored(AddonWeeklyBingo* addon, int slot, Vector4 color)
    {
        var node = GetBorderResourceNode(addon, slot);
        if (node != null)
        {
            node->AtkResNode.ToggleVisibility(true);
            node->AtkResNode.Color = color.ToByteColor();
        }
    }

    private void MakeTextNode(AddonWeeklyBingo* addon)
    {
        if (addon->AtkUnitBase.UldManager.LoadedState is not AtkLoadState.Loaded) return;
        var textNode = addon->AtkUnitBase.GetTextNodeById(34);
        if (textNode is null) return;

        if (this.probabilityTextNode is null && addon is not null)
        {
            this.probabilityTextNode = IMemorySpace.GetUISpace()->Create<AtkTextNode>();

            this.probabilityTextNode->AtkResNode.NodeFlags = NodeFlags.Enabled;
            this.probabilityTextNode->AtkResNode.Type = NodeType.Text;
            this.probabilityTextNode->AtkResNode.NodeID = 1000;
            this.probabilityTextNode->AtkResNode.Height = (ushort)(textNode->AtkResNode.Height / 2.0f);
            this.probabilityTextNode->AtkResNode.Width = textNode->AtkResNode.Width;
            this.probabilityTextNode->AtkResNode.X = textNode->AtkResNode.X;
            this.probabilityTextNode->AtkResNode.Y = textNode->AtkResNode.Y + (textNode->AtkResNode.Height / 2.0f);

            this.probabilityTextNode->TextColor = textNode->TextColor;
            this.probabilityTextNode->EdgeColor = textNode->EdgeColor;
            this.probabilityTextNode->BackgroundColor = textNode->BackgroundColor;
            this.probabilityTextNode->AlignmentFontType = textNode->AlignmentFontType;
            this.probabilityTextNode->FontSize = textNode->FontSize;
            this.probabilityTextNode->LineSpacing = textNode->LineSpacing;
            this.probabilityTextNode->CharSpacing = textNode->CharSpacing;
            this.probabilityTextNode->TextFlags = (byte)((TextFlags)textNode->TextFlags | TextFlags.MultiLine);

            this.probabilityTextNode->SetText("Test Text");

            this.probabilityTextNode->AtkResNode.ParentNode = textNode->AtkResNode.ParentNode;
            this.probabilityTextNode->AtkResNode.NextSiblingNode = &textNode->AtkResNode;
            this.probabilityTextNode->AtkResNode.PrevSiblingNode = textNode->AtkResNode.PrevSiblingNode;
            this.probabilityTextNode->AtkResNode.ChildNode = null;
            textNode->AtkResNode.PrevSiblingNode->PrevSiblingNode->NextSiblingNode = &this.probabilityTextNode->AtkResNode;
            textNode->AtkResNode.PrevSiblingNode = &this.probabilityTextNode->AtkResNode;
            textNode->AtkResNode.ParentNode->ChildCount++;
            addon->AtkUnitBase.UldManager.UpdateDrawNodeList();
            addon->AtkUnitBase.UpdateCollisionNodeList(false);
        }
    }

    private static void ResetDutySlotBorder(AddonWeeklyBingo* addon, int slot, PlayerState.WeeklyBingoTaskStatus taskState)
    {
        var node = GetBorderResourceNode(addon, slot);
        if (node != null)
        {
            switch (taskState)
            {
                case PlayerState.WeeklyBingoTaskStatus.Open:
                    node->AtkResNode.ToggleVisibility(false);
                    break;

                case PlayerState.WeeklyBingoTaskStatus.Claimable:
                    node->AtkResNode.ToggleVisibility(true);
                    break;

                case PlayerState.WeeklyBingoTaskStatus.Claimed:
                    node->AtkResNode.ToggleVisibility(false);
                    break;
            }

            // Default Color
            node->AtkResNode.Color = new Vector4(1.0f, 1.0f, 1.0f, 0.678f).ToByteColor();
        }
    }

    private static AtkNineGridNode* GetBorderResourceNode(AddonWeeklyBingo* addon, int dutySlot) 
        => (AtkNineGridNode*)addon->DutySlotList[dutySlot].DutyButton->AtkComponentBase.UldManager.SearchNodeById(11);

    private void AddonSetupDetour(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonWeeklyBingo*)args.Addon;

        this.MakeTextNode(addon);

        var dutyReceiveEvent = (IntPtr)addon->DutySlotList.DutySlot1.vtbl[2];
        this.addonDutyReceiveEventHook ??= Service.Hooker.HookFromAddress<DutyReceiveEventDelegate>(dutyReceiveEvent, this.AddonDutyReceiveEventDetour);
        this.addonDutyReceiveEventHook?.Enable();
    }

    private void AddonRefreshDetour(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonWeeklyBingo*)args.Addon;

        this.UpdateGameState(addon);

        var placedStickers = PlayerState.Instance()->WeeklyBingoNumPlacedStickers;
        if (placedStickers > 7)
        {
            // > 7 shuffling is disabled
            return;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < this.gameState.Length; i++)
        {
            sb.Append(this.gameState[i] ? "X" : "O");
            if ((i + 1) % 4 == 0) sb.Append(' ');
        }

        Service.PluginLog.Debug($"State has changed: {sb}");

        this.probabilityTextNode->SetText(this.SolveAndGetProbabilitySeString().Encode());

        for (var i = 0; i < 16; ++i)
        {
            var taskButtonState = PlayerState.Instance()->GetWeeklyBingoTaskStatus(i);
            var instances = TaskLookup.GetInstanceListFromID(PlayerState.Instance()->WeeklyBingoOrderData[i]);

            if (instances.Contains(Service.ClientState.TerritoryType))
            {
                SetDutySlotBorderColored(addon, i, new Vector4(1.0f, 0.607f, 0.607f, 1.0f));
            }
            else
            {
                ResetDutySlotBorder(addon, i, taskButtonState);
            }
        }
    }

    private void AddonFinalizeDetour(AddonEvent type, AddonArgs args)
    {
        if (this.probabilityTextNode is not null)
        {
            this.probabilityTextNode->AtkResNode.Destroy(true);
            this.probabilityTextNode = null;
        }
    }

    private void AddonDutyReceiveEventDetour(IntPtr dutyPtr, ushort a2, uint a3, IntPtr a4, IntPtr a5)
    {
        this.addonDutyReceiveEventHook?.Original(dutyPtr, a2, a3, a4, a5);

        try
        {
            // Checks if the player is in a duty
            if (Service.Condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95))
                return;

            var duty = (DutySlot*)dutyPtr;
            if (PlayerState.Instance()->GetWeeklyBingoTaskStatus(duty->index) is PlayerState.WeeklyBingoTaskStatus.Open)
            {
                var dutiesForTask = TaskLookup.GetInstanceListFromID(PlayerState.Instance()->WeeklyBingoOrderData[duty->index]);

                var territoryType = dutiesForTask.FirstOrDefault();
                var cfc = Service.DataManager.GetExcelSheet<Sheets.ContentFinderCondition>()!
                    .FirstOrDefault(cfc => cfc.TerritoryType.Row == territoryType);

                if (cfc == null)
                    return;

                this.OpenRegularDuty(cfc.RowId);
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Boom");
        }
    }

    private bool UpdateGameState(AddonWeeklyBingo* addon)
    {
        var stateChanged = false;
        for (var i = 0; i < 16; i++)
        {
            var state = PlayerState.Instance()->IsWeeklyBingoStickerPlaced(i);
            stateChanged |= this.gameState[i] != state;
            this.gameState[i] = state;
        }

        return stateChanged;
    }

    private SeString SolveAndGetProbabilitySeString()
    {
        var stickersPlaced = this.gameState.Count(s => s);

        // > 9 returns Error {-1,-1,-1} by the solver
        var values = this.perfectTails.Solve(this.gameState);

        double[]? samples = null;
        if (stickersPlaced is > 0 and <= 7)
            samples = this.perfectTails.GetSample(stickersPlaced);

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
            var valuePayloads = this.StringFormatDoubles(values);
            var seString = new SeString(new List<Payload>())
                .Append(SecretDelimiter).Append("\r").Append(ColorOff)
                .Append(ChancesText);

            if (samples != null)
            {
                foreach (var (value, sample, valuePayload) in Enumerable.Range(0, values.Length).Select(i => (values[i], samples[i], valuePayloads[i])))
                {
                    const double bound = 0.05;
                    var sampleBoundLower = Math.Max(0, sample - bound);
                    // var sampleBoundUpper = Math.Min(1, sample + bound);

                    if (Math.Abs(value - 1) < 0.1f)
                    {
                        seString.Append(GoldenGlow).Append(valuePayload).Append(GlowOff);
                    }
                    else if (value < 1 && value >= sample)
                    {
                        seString.Append(HappyGreen).Append(valuePayload).Append(ColorOff);
                    }
                    else if (sample > value && value > sampleBoundLower)
                    {
                        seString.Append(MellowYellow).Append(valuePayload).Append(ColorOff);
                    }
                    else if (sampleBoundLower > value && value > 0)
                    {
                        seString.Append(PinkSalmon).Append(valuePayload).Append(ColorOff);
                    }
                    else if (value == 0)
                    {
                        seString.Append(AngryRed).Append(valuePayload).Append(ColorOff);
                    }
                    else
                    {
                        // Just in case
                        seString.Append(valuePayload);
                    }

                    seString.Append("  ");
                }

                seString.Append(ShuffleText);
                var sampleStrings = this.StringFormatDoubles(samples);
                foreach (var sampleString in sampleStrings)
                    seString.Append(sampleString).Append("  ");
            }
            else
            {
                foreach (var valueString in valuePayloads)
                    seString.Append(valueString).Append("  ");
            }

            return seString;
        }
    }

    private TextPayload[] StringFormatDoubles(IEnumerable<double> values)
        => values.Select(v => new TextPayload($"{v * 100:F2}%")).ToArray();

    private bool SeStringTryFindDelimiter(SeString seString, out int index)
    {
        var secretBytes = SecretDelimiter.Encode();
        for (var i = 0; i < seString.Payloads.Count; i++)
        {
            var payload = seString.Payloads[i];
            if (payload is UIForegroundPayload)
            {
                if (payload.Encode().SequenceEqual(secretBytes))
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
        => this.SeStringTryFindDelimiter(seString, out _);

    private void RemoveProbabilityString(SeString seString)
    {
        if (this.SeStringTryFindDelimiter(seString, out var index))
        {
            var removeCount = seString.Payloads.Count - index;
            try
            {
                seString.Payloads.RemoveRange(index, removeCount);
            }
            catch (ArgumentException)
            {
                Service.PluginLog.Warning($"ArgExc during RemoveProbabilityString, count={seString.Payloads.Count} index={index} removeCount={removeCount}");
            }
        }
    }

    private void OpenRegularDuty(uint contentFinderCondition)
    {
        AgentContentsFinder.Instance()->OpenRegularDuty(contentFinderCondition);
    }
}