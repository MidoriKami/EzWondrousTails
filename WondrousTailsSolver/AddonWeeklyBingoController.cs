using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace WondrousTailsSolver;

public unsafe class AddonWeeklyBingoController : IDisposable {
    private const string AddonName = "WeeklyBingo";
    private const string InstructionOriginalSegment = "\u7A7A\u767D\u5904\u8D34\u4E0A\u5370\u82B1";
    private const string InstructionReplacementSegment = "\u7A7A\u767D\u5904\u8D34\u4E0A\u5370\u82B111111111111111111";
    private const string ProbabilityPrefix = "\u8FDE\u7EBF\u6982\u7387\uFF1A";
    private const string AveragePrefix = "\u91CD\u6392\u5E73\u5747\uFF1A";

    private uint instructionTextNodeId;
    private string? instructionOriginalText;
    private ushort instructionOriginalHeight;
    private TextFlags instructionOriginalFlags;
    private bool disposed;

    public AddonWeeklyBingoController(IDalamudPluginInterface pluginInterface) {
        DalamudServices.Initialize(pluginInterface);

        DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, OnAddonEvent);
        DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, OnAddonEvent);
        DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, OnAddonEvent);
        DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnAddonEvent);
        DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, OnAddonEvent);

        var currentAddon = GetOpenAddon();
        if (currentAddon is not null) {
            AddonRefresh(currentAddon);
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }

        var currentAddon = GetOpenAddon();
        if (currentAddon is not null) {
            RestoreInstructionText(currentAddon);
        }

        DalamudServices.AddonLifecycle.UnregisterListener(OnAddonEvent);

        instructionTextNodeId = 0;
        instructionOriginalText = null;
        instructionOriginalHeight = 0;
        instructionOriginalFlags = 0;
        disposed = true;
    }

    private void OnAddonEvent(AddonEvent type, AddonArgs args) {
        var addon = (AddonWeeklyBingo*)args.Addon.Address;

        switch (type) {
            case AddonEvent.PostSetup:
                AddonRefresh(addon);
                return;

            case AddonEvent.PreFinalize:
                RestoreInstructionText(addon);
                return;

            case AddonEvent.PostRefresh or AddonEvent.PostRequestedUpdate or AddonEvent.PostUpdate:
                AddonRefresh(addon);
                return;
        }
    }

    private void AddonRefresh(AddonWeeklyBingo* addon) {
        foreach (var index in Enumerable.Range(0, 16)) {
            System.PerfectTails.GameState[index] = PlayerState.Instance()->IsWeeklyBingoStickerPlaced(index);
        }

        UpdateInstructionText(addon);
    }

    private void UpdateInstructionText(AddonWeeklyBingo* addon) {
        var instructionNode = GetInstructionTextNode(addon);
        if (instructionNode is null) {
            return;
        }

        var currentText = SeString.Parse(instructionNode->NodeText).TextValue;
        if (string.IsNullOrEmpty(currentText)) {
            return;
        }

        var baseText = instructionOriginalText ?? NormalizeInstructionText(currentText);
        if (!baseText.Contains(InstructionOriginalSegment, StringComparison.Ordinal)) {
            return;
        }

        instructionOriginalText = baseText;

        if (instructionOriginalHeight == 0) {
            instructionOriginalHeight = instructionNode->GetHeight();
        }

        if (instructionOriginalFlags == 0) {
            instructionOriginalFlags = (TextFlags)instructionNode->TextFlags;
        }

        instructionNode->TextFlags |= TextFlags.MultiLine;

        var lineSpacing = instructionNode->LineSpacing > 0 ? instructionNode->LineSpacing : (byte)16;
        var desiredHeight = (ushort)(instructionOriginalHeight + (lineSpacing * 2));
        if (instructionNode->GetHeight() < desiredHeight) {
            instructionNode->SetHeight(desiredHeight);
        }

        var (probabilityLine, averageLine) = System.PerfectTails.GetInlineDisplayLines();
        var replacedText = BuildInstructionDisplayText(baseText, probabilityLine, averageLine);
        if (!string.Equals(replacedText, currentText, StringComparison.Ordinal)) {
            instructionNode->SetText(replacedText);
        }
    }

    private void RestoreInstructionText(AddonWeeklyBingo* addon) {
        if (instructionTextNodeId == 0 || string.IsNullOrEmpty(instructionOriginalText)) {
            return;
        }

        var instructionNode = addon->GetTextNodeById(instructionTextNodeId);
        if (instructionNode is null) {
            return;
        }

        instructionNode->SetText(instructionOriginalText);

        if (instructionOriginalHeight > 0) {
            instructionNode->SetHeight(instructionOriginalHeight);
        }

        if (instructionOriginalFlags != 0) {
            instructionNode->TextFlags = instructionOriginalFlags;
        }
    }

    private AtkTextNode* GetInstructionTextNode(AddonWeeklyBingo* addon) {
        if (instructionTextNodeId != 0) {
            var cachedNode = addon->GetTextNodeById(instructionTextNodeId);
            if (IsInstructionNode(cachedNode)) {
                return cachedNode;
            }
        }

        foreach (var node in addon->UldManager.Nodes) {
            if (node.Value is null || node.Value->Type is not NodeType.Text) {
                continue;
            }

            var candidate = (AtkTextNode*)node.Value;
            if (!IsInstructionNode(candidate)) {
                continue;
            }

            instructionTextNodeId = candidate->AtkResNode.NodeId;
            return candidate;
        }

        return null;
    }

    private static string NormalizeInstructionText(string text) {
        var normalized = text.Replace(InstructionReplacementSegment, InstructionOriginalSegment, StringComparison.Ordinal);
        var lines = normalized
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith(ProbabilityPrefix, StringComparison.Ordinal)
                        && !line.StartsWith(AveragePrefix, StringComparison.Ordinal))
            .ToArray();

        return string.Join("\r", lines);
    }

    private static string BuildInstructionDisplayText(string baseText, string probabilityLine, string averageLine) {
        var lines = new List<string>();
        var inserted = false;

        foreach (var line in baseText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)) {
            if (!inserted && line.Contains(InstructionOriginalSegment, StringComparison.Ordinal)) {
                lines.Add(line.TrimEnd('\u3002', ' '));
                lines.Add(string.Empty);
                lines.Add(probabilityLine);
                lines.Add(averageLine);
                inserted = true;
                continue;
            }

            lines.Add(line);
        }

        return string.Join("\r", lines);
    }

    private static bool IsInstructionNode(AtkTextNode* node) {
        if (node is null || node->NodeText.AsSpan().Length == 0) {
            return false;
        }

        var text = SeString.Parse(node->NodeText).TextValue;
        return text.Contains(InstructionOriginalSegment, StringComparison.Ordinal)
               || text.Contains(InstructionReplacementSegment, StringComparison.Ordinal)
               || text.Contains(ProbabilityPrefix, StringComparison.Ordinal)
               || text.Contains(AveragePrefix, StringComparison.Ordinal);
    }

    private static AddonWeeklyBingo* GetOpenAddon() {
        var address = DalamudServices.GameGui.GetAddonByName(AddonName).Address;
        return address == nint.Zero ? null : (AddonWeeklyBingo*)address;
    }
}
