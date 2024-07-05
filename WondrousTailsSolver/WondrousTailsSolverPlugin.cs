using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using WondrousTailsSolver.Config;

using Sheets = Lumina.Excel.GeneratedSheets;

namespace WondrousTailsSolver;

/// <summary>
/// Main plugin implementation.
/// </summary>
public sealed unsafe class WondrousTailsSolverPlugin : IDalamudPlugin
{
    private readonly PerfectTails perfectTails = new();
    private readonly bool[] gameState = new bool[16];

    private readonly WindowSystem windowSystem = new("EzWondrousTails");
    private readonly Configuration configuration;
    private readonly ConfigurationWindow configurationWindow;

    private AtkTextNode* probabilityTextNode = null;
    private AtkNineGridNode* currentDutyNode = null;

    private Hook<DutyReceiveEventDelegate>? addonDutyReceiveEventHook;

    /// <summary>
    /// Initializes a new instance of the <see cref="WondrousTailsSolverPlugin"/> class.
    /// </summary>
    /// <param name="pluginInterface">Dalamud plugin interface.</param>
    public WondrousTailsSolverPlugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        this.configuration = Service.Interface.GetPluginConfig() as Configuration ?? new Configuration();

        this.configurationWindow = new ConfigurationWindow(this.configuration);
        this.windowSystem.AddWindow(this.configurationWindow);

        Service.Interface.UiBuilder.Draw += this.windowSystem.Draw;
        Service.Interface.UiBuilder.OpenConfigUi += this.configurationWindow.Toggle;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "WeeklyBingo", this.AddonSetupDetour);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "WeeklyBingo", this.AddonDrawDetour);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "WeeklyBingo", this.AddonRefreshDetour);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "WeeklyBingo", this.AddonFinalizeDetour);
    }

    private delegate void DutyReceiveEventDelegate(IntPtr addonPtr, ushort a2, uint a3, IntPtr a4, IntPtr a5);

    /// <inheritdoc/>
    public void Dispose()
    {
        Service.AddonLifecycle.UnregisterListener(this.AddonSetupDetour);
        Service.AddonLifecycle.UnregisterListener(this.AddonDrawDetour);
        Service.AddonLifecycle.UnregisterListener(this.AddonRefreshDetour);
        Service.AddonLifecycle.UnregisterListener(this.AddonFinalizeDetour);

        this.addonDutyReceiveEventHook?.Dispose();

        Service.Interface.UiBuilder.Draw += null;
        Service.Interface.UiBuilder.OpenConfigUi += null;

        this.windowSystem.RemoveWindow(this.configurationWindow);
    }

    private void AddonSetupDetour(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonWeeklyBingo*)args.Addon;

        this.MakeTextNode(addon);

        var dutyReceiveEvent = (IntPtr)addon->DutySlotList.DutySlot1.addon->VirtualTable->ReceiveEvent;
        this.addonDutyReceiveEventHook ??= Service.Hooker.HookFromAddress<DutyReceiveEventDelegate>(dutyReceiveEvent, this.AddonDutyReceiveEventDetour);
        this.addonDutyReceiveEventHook?.Enable();
    }

    private void AddonDrawDetour(AddonEvent type, AddonArgs args)
    {
        if (this.currentDutyNode is not null)
        {
            currentDutyNode->AtkResNode.ToggleVisibility(true);
            currentDutyNode->AtkResNode.Color = this.configuration.CurrentDutyColor.ToByteColor();
        }
    }

    private void AddonRefreshDetour(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonWeeklyBingo*)args.Addon;

        // Update GameState
        foreach (var index in Enumerable.Range(0, 16))
        {
            this.gameState[index] = PlayerState.Instance()->IsWeeklyBingoStickerPlaced(index);
        }

        this.LogStickerState();

        // > 7 shuffling is disabled
        if (PlayerState.Instance()->WeeklyBingoNumPlacedStickers is >= 1 and <= 7)
        {
            this.probabilityTextNode->SetText(this.SolveAndGetProbabilitySeString().Encode());
        }

        // Find the node for the currently occupied duty
        if (Service.Condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95))
        {
            foreach (var index in Enumerable.Range(0, 16))
            {
                if (TaskLookup.GetInstanceListFromID(PlayerState.Instance()->WeeklyBingoOrderData[index]).Contains(Service.ClientState.TerritoryType))
                {
                    this.currentDutyNode = this.GetBorderResourceNode(addon, index);
                }
            }
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

                AgentContentsFinder.Instance()->OpenRegularDuty(cfc.RowId);
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Boom");
        }
    }

    private void AddonFinalizeDetour(AddonEvent type, AddonArgs args)
    {
        if (this.probabilityTextNode is not null)
        {
            this.probabilityTextNode->AtkResNode.Destroy(true);
            this.probabilityTextNode = null;
        }

        this.currentDutyNode = null;
    }

    private AtkNineGridNode* GetBorderResourceNode(AddonWeeklyBingo* addon, int dutySlot)
        => (AtkNineGridNode*)addon->DutySlotList[dutySlot].DutyButton->AtkComponentBase.UldManager.SearchNodeById(11);

    private void LogStickerState()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < this.gameState.Length; i++)
        {
            sb.Append(this.gameState[i] ? "X" : "O");
            if ((i + 1) % 4 == 0) sb.Append(' ');
        }

        Service.PluginLog.Debug($"State has changed: {sb}");
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
            return new SeStringBuilder()
                .AddText("Line Chances: ")
                .AddUiForeground("error ", 704)
                .AddUiForeground("error ", 704)
                .AddUiForeground("error ", 704)
                .Build();
        }

        var valuePayloads = this.StringFormatDoubles(values);
        var seString = new SeStringBuilder()
            .AddText("\nLine Chances: ");

        if (samples != null)
        {
            foreach (var (value, sample, valuePayload) in Enumerable.Range(0, values.Length).Select(i => (values[i], samples[i], valuePayloads[i])))
            {
                const double bound = 0.05;
                var sampleBoundLower = Math.Max(0, sample - bound);
                // var sampleBoundUpper = Math.Min(1, sample + bound);

                if (Math.Abs(value - 1) < 0.1f)
                    seString.AddUiGlow(valuePayload, 2);
                else if (value < 1 && value >= sample)
                    seString.AddUiForeground(valuePayload, 67);
                else if (sample > value && value > sampleBoundLower)
                    seString.AddUiForeground(valuePayload, 66);
                else if (sampleBoundLower > value && value > 0)
                    seString.AddUiForeground(valuePayload, 561);
                else if (value == 0)
                    seString.AddUiForeground(valuePayload, 704);
                else
                    seString.AddText(valuePayload);

                seString.AddText("  ");
            }

            seString.AddText("\rShuffle Average: ");
            seString.AddText(string.Join(" ", this.StringFormatDoubles(samples)));
        }
        else
        {
            seString.AddText(string.Join(" ", valuePayloads));
        }

        return seString.Build();
    }

    private string[] StringFormatDoubles(IEnumerable<double> values)
        => values.Select(v => $"{v * 100:F2}%").ToArray();

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
            this.probabilityTextNode->AtkResNode.NodeId = 1000;
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
}