using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;

namespace WondrousTailsSolver;

public unsafe class AddonWeeklyBingoController : IDisposable {
    private TextNode? probabilityTextNode;
    
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
        }
    }
    
    private void OnAddonSetup(AddonEvent type, AddonArgs args) {
        var addonWeeklyBingo = (AddonWeeklyBingo*)args.Addon;

        var existingTextNode = addonWeeklyBingo->GetTextNodeById(34);
        if (existingTextNode is null) return;
        
        // Shrink existing node, the game doesn't need that space anyways.
        existingTextNode->SetHeight((ushort)(existingTextNode->GetHeight() * 2.0f / 3.0f));
        
        // Add new custom text node to ui
        AddTextNode(existingTextNode, addonWeeklyBingo);
    }
    
    private void OnAddonFinalize(AddonEvent type, AddonArgs args) {
        DetachNode((AddonWeeklyBingo*)args.Addon);
    }
    
    private void OnAddonRefresh(AddonEvent type, AddonArgs args) {
        foreach (var index in Enumerable.Range(0, 16)) {
            System.PerfectTails.GameState[index] = PlayerState.Instance()->IsWeeklyBingoStickerPlaced(index);
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

    private void DetachNode(AddonWeeklyBingo* addon) {
        if (probabilityTextNode is null) return;
        
        System.NativeController.DetachFromAddon(probabilityTextNode, (AtkUnitBase*)addon);
        probabilityTextNode.Dispose();
        probabilityTextNode = null;
    }
}