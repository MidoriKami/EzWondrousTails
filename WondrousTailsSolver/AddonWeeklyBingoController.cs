using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Classes.Controllers;

namespace WondrousTailsSolver;

public unsafe class AddonWeeklyBingoController : AddonController<AddonWeeklyBingo> {
    private uint targetTextNodeId;
    private ushort originalTextNodeHeight;
    private TextFlags originalTextFlags;
    private string lastInjectedText = string.Empty;

    public AddonWeeklyBingoController(IDalamudPluginInterface pluginInterface) : base("WeeklyBingo") {
        KamiToolKitLibrary.Initialize(pluginInterface);
        OnAttach += AttachNodes;
        OnRefresh += AddonRefresh;
        OnUpdate += AddonRefresh;
        OnDetach += DetachNodes;
        Enable();
    }

    private void AttachNodes(AddonWeeklyBingo* addon) {
        var existingTextNode = GetTargetTextNode(addon);
        if (existingTextNode is null) return;

        targetTextNodeId = existingTextNode->AtkResNode.NodeId;
        originalTextNodeHeight = existingTextNode->GetHeight();
        originalTextFlags = (TextFlags)existingTextNode->TextFlags;
        existingTextNode->TextFlags |= TextFlags.MultiLine;

        var calculatedExtraHeight = (ushort)(existingTextNode->LineSpacing * 3);
        var extraHeight = calculatedExtraHeight > 24 ? calculatedExtraHeight : (ushort)24;
        existingTextNode->SetHeight((ushort)(originalTextNodeHeight + extraHeight));

        AddonRefresh(addon);
    }

    private void AddonRefresh(AddonWeeklyBingo* addon) {
        foreach (var index in Enumerable.Range(0, 16)) {
            System.PerfectTails.GameState[index] = PlayerState.Instance()->IsWeeklyBingoStickerPlaced(index);
        }

        var existingTextNode = GetTargetTextNode(addon);
        if (existingTextNode is null) return;

        var baseText = SeString.Parse(existingTextNode->NodeText).TextValue;
        if (!string.IsNullOrEmpty(lastInjectedText)) {
            var injectedIndex = baseText.IndexOf(lastInjectedText, StringComparison.Ordinal);
            if (injectedIndex >= 0) {
                baseText = baseText[..injectedIndex];
            }
        }

        baseText = baseText.TrimEnd('\r', '\n', ' ');
        lastInjectedText = System.PerfectTails.SolveAndGetProbabilitySeString().TextValue;

        existingTextNode->SetText(string.IsNullOrEmpty(baseText)
            ? lastInjectedText
            : $"{baseText}\r{lastInjectedText}");
    }

    private void DetachNodes(AddonWeeklyBingo* addon) {
        var existingTextNode = GetTargetTextNode(addon);
        if (existingTextNode is not null) {
            var baseText = SeString.Parse(existingTextNode->NodeText).TextValue;
            if (!string.IsNullOrEmpty(lastInjectedText)) {
                var injectedIndex = baseText.IndexOf(lastInjectedText, StringComparison.Ordinal);
                if (injectedIndex >= 0) {
                    baseText = baseText[..injectedIndex].TrimEnd('\r', '\n', ' ');
                    existingTextNode->SetText(baseText);
                }
            }

            if (originalTextNodeHeight > 0) {
                existingTextNode->SetHeight(originalTextNodeHeight);
            }

            existingTextNode->TextFlags = originalTextFlags;
        }

        targetTextNodeId = 0;
        originalTextNodeHeight = 0;
        originalTextFlags = 0;
        lastInjectedText = string.Empty;
    }

    private AtkTextNode* GetTargetTextNode(AddonWeeklyBingo* addon) {
        if (targetTextNodeId != 0) {
            var cachedNode = addon->GetTextNodeById(targetTextNodeId);
            if (IsCandidateNode(cachedNode)) {
                return cachedNode;
            }
        }

        var textNode = addon->GetTextNodeById(34);
        if (IsCandidateNode(textNode)) {
            return textNode;
        }

        AtkTextNode* bestNode = null;
        foreach (var node in addon->UldManager.Nodes) {
            if (node.Value is null || node.Value->Type is not NodeType.Text) continue;

            var candidate = (AtkTextNode*)node.Value;
            if (!IsCandidateNode(candidate)) continue;

            if (bestNode is null
                || candidate->GetWidth() > bestNode->GetWidth()
                || (candidate->GetWidth() == bestNode->GetWidth() && candidate->GetYFloat() < bestNode->GetYFloat())) {
                bestNode = candidate;
            }
        }

        return bestNode;
    }

    private static bool IsCandidateNode(AtkTextNode* node) {
        if (node is null) return false;
        if (node->NodeText.AsSpan().Length == 0) return false;

        var text = SeString.Parse(node->NodeText).TextValue.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        if (node->GetWidth() < 250 || node->GetHeight() < 20) return false;

        var y = node->GetYFloat();
        return y is > 40 and < 220;
    }
}
