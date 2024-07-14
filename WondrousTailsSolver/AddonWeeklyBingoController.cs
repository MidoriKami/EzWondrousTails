using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using Lumina.Excel.GeneratedSheets;

namespace WondrousTailsSolver;

public unsafe class AddonWeeklyBingoController : IDisposable {
    private readonly IAddonEventHandle?[] eventHandles = new IAddonEventHandle?[16];

    private TextNode? probabilityTextNode;
    private readonly NineGridNode?[] currentDutyBorder = new NineGridNode[16];
    
    public AddonWeeklyBingoController() {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "WeeklyBingo", OnAddonSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "WeeklyBingo", OnAddonFinalize);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "WeeklyBingo", OnAddonRefresh);
    }
    
    public void Dispose() {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup, OnAddonFinalize, OnAddonRefresh);

        var addon = (AddonWeeklyBingo*) Service.GameGui.GetAddonByName("WeeklyBingo");
        if (addon is not null) {
            DetachNode(addon);
            ResetEventHandles();
            RemoveDutySlots(addon);
        }
    }
    
    private void OnAddonSetup(AddonEvent type, AddonArgs args) {
        var addonWeeklyBingo = (AddonWeeklyBingo*)args.Addon;

        var existingTextNode = addonWeeklyBingo->GetTextNodeById(34);
        if (existingTextNode is null) return;
        
        // Shrink existing node, the game doesn't need that space anyways.
        existingTextNode->SetHeight((ushort)(existingTextNode->GetHeight() / 2.0f));
        
        // Add new custom text node to ui
        AddTextNode(existingTextNode, addonWeeklyBingo);

        // Reset any event handles
        ResetEventHandles();
        
        // Register new event handles
        foreach (var index in Enumerable.Range(0, 16)) {
            var dutySlot = addonWeeklyBingo->DutySlotList[index];
        
            eventHandles[index] = Service.AddonEventManager.AddEvent((nint) addonWeeklyBingo, (nint) dutySlot.DutyButton->OwnerNode, AddonEventType.ButtonClick, OnDutySlotClick);
        }
        
        // Add new custom border nodes to ui
        AddBorderNodes(addonWeeklyBingo);
    }
    
    private void OnAddonFinalize(AddonEvent type, AddonArgs args) {
        DetachNode((AddonWeeklyBingo*)args.Addon);
        ResetEventHandles();
        RemoveDutySlots((AddonWeeklyBingo*) args.Addon);
    }
    
    private void OnAddonRefresh(AddonEvent type, AddonArgs args) {
        foreach (var index in Enumerable.Range(0, 16)) {
            System.PerfectTails.GameState[index] = PlayerState.Instance()->IsWeeklyBingoStickerPlaced(index);
        }
        
        foreach (var index in Enumerable.Range(0, 16)) {
            ref var borderNode = ref currentDutyBorder[index];
            if (borderNode is null) continue;

            borderNode.IsVisible = IsCurrentDuty(index);
        }

        if (probabilityTextNode is not null) {
            probabilityTextNode.Text = System.PerfectTails.SolveAndGetProbabilitySeString();
        }
    }

    private void AddTextNode(AtkTextNode* existingTextNode, AddonWeeklyBingo* addonWeeklyBingo) {
        probabilityTextNode = new TextNode {
            NodeID = 1000,
            NodeFlags = NodeFlags.Enabled | NodeFlags.Visible,
            Size = new Vector2(existingTextNode->GetWidth(), existingTextNode->GetHeight()),
            Position = new Vector2(existingTextNode->GetXFloat(), existingTextNode->GetYFloat() + existingTextNode->GetHeight()),
            TextColor = existingTextNode->TextColor.ToVector4(),
            TextOutlineColor = existingTextNode->EdgeColor.ToVector4(),
            BackgroundColor = existingTextNode->BackgroundColor.ToVector4(),
            FontSize = existingTextNode->FontSize,
            LineSpacing = existingTextNode->LineSpacing,
            CharSpacing = existingTextNode->CharSpacing,
            TextFlags = TextFlags.MultiLine | (TextFlags)existingTextNode->TextFlags,
            Text = System.PerfectTails.SolveAndGetProbabilitySeString(),
        };

        System.NativeController.AttachToAddon(probabilityTextNode, (AtkUnitBase*)addonWeeklyBingo, (AtkResNode*)existingTextNode, NodePosition.AfterTarget);
    }

    private void AddBorderNodes(AddonWeeklyBingo* addonWeeklyBingo) {
        foreach (var index in Enumerable.Range(0, 16)) {
            var dutySlot = addonWeeklyBingo->DutySlotList[index];
            var buttonNode = dutySlot.DutyButton->OwnerNode;

            var newBorderNode = new NineGridNode {
                Size = new Vector2(buttonNode->GetWidth(), buttonNode->GetHeight()) + new Vector2(0.0f, 4.0f),
                Position = new Vector2(buttonNode->GetXFloat(), buttonNode->GetYFloat()),
                Color = System.Configuration.CurrentDutyColor,
                NodeID = dutySlot.ResNode1->NodeId + 100,
                TextureCoordinates = new Vector2(2.0f, 2.0f) / 2.0f,
                TextureSize = new Vector2(144.0f, 96.0f) / 2.0f,
                IsVisible = IsCurrentDuty(index),
            };
            
            newBorderNode.LoadTexture("ui/uld/WeeklyBingo_hr1.tex");
                
            currentDutyBorder[index] = newBorderNode;
            System.NativeController.AttachToAddon(newBorderNode, (AtkUnitBase*)addonWeeklyBingo, (AtkResNode*)buttonNode, NodePosition.AfterTarget);
        }
    }

    private void OnDutySlotClick(AddonEventType atkEventType, IntPtr atkUnitBase, IntPtr atkResNode) {
        var dutyButtonNode = (AtkResNode*) atkResNode;
        var tileIndex = (int)dutyButtonNode->NodeId - 12;

        var selectedTask = PlayerState.Instance()->GetWeeklyBingoTaskStatus(tileIndex);
        var bingoData = PlayerState.Instance()->WeeklyBingoOrderData[tileIndex];
        
        if (selectedTask is PlayerState.WeeklyBingoTaskStatus.Open) {
            var dutiesForTask = TaskLookup.GetInstanceListFromId(bingoData);

            var territoryType = dutiesForTask.FirstOrDefault();
            var cfc = Service.DataManager.GetExcelSheet<ContentFinderCondition>()!.FirstOrDefault(cfc => cfc.TerritoryType.Row == territoryType);
            if (cfc == null) return;

            AgentContentsFinder.Instance()->OpenRegularDuty(cfc.RowId);
        }
    }

    private void DetachNode(AddonWeeklyBingo* addon) {
        if (probabilityTextNode is null) return;
        
        System.NativeController.DetachFromAddon(probabilityTextNode, (AtkUnitBase*)addon);
        probabilityTextNode.Dispose();
        probabilityTextNode = null;
    }

    private void RemoveDutySlots(AddonWeeklyBingo* addonWeeklyBingo) {
        foreach (var index in Enumerable.Range(0, 16)) {
            ref var borderNode = ref currentDutyBorder[index];
            if (borderNode is null) continue;
       
            System.NativeController.DetachFromAddon(borderNode, (AtkUnitBase*)addonWeeklyBingo);
            borderNode.Dispose();
            borderNode = null;
        }
    }

    private void ResetEventHandles() {
        foreach (var index in Enumerable.Range(0, 16)) {
            if (eventHandles[index] is {} handle) {
                Service.AddonEventManager.RemoveEvent(handle);
                eventHandles[index] = null;
            }
        }
    }

    private bool IsCurrentDuty(int index) {
        if (!Service.Condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95)) return false;

        var currentTaskId = PlayerState.Instance()->WeeklyBingoOrderData[index];
        var taskList = TaskLookup.GetInstanceListFromId(currentTaskId);
        
        return taskList.Contains(Service.ClientState.TerritoryType);
    }
}