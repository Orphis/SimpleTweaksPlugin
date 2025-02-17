﻿using System;
using System.ComponentModel;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Target HP")]
[TweakDescription("Displays the exact (or optionally rounded) value of target's hitpoints.")]
public unsafe class TargetHP : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public DisplayFormat DisplayFormat = DisplayFormat.OneDecimalPrecision;
        public Vector2 Position = new(0);
        public bool UseCustomColor;
        public Vector4 CustomColor = new(1);
        public byte FontSize = 14;
        public bool HideAutoAttack;
        public bool AlignLeft;

        public bool NoFocus;
        public Vector2 FocusPosition = new(0);
        public bool FocusUseCustomColor;
        public Vector4 FocusCustomColor = new(1);
        public byte FocusFontSize = 14;
        public bool FocusAlignLeft;
    }

    public enum DisplayFormat {
        [Description("Full Number")] FullNumber,

        [Description("Full Number, with Separators")]
        FullNumberSeparators,
        [Description("Short Number")] ZeroDecimalPrecision,
        [Description("1 Decimal")] OneDecimalPrecision,
        [Description("2 Decimal")] TwoDecimalPrecision,
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree =>
        (ref bool hasChanged) => {
            if (ImGui.BeginCombo("Display Format###targetHpFormat", $"{Config.DisplayFormat.GetDescription()} ({FormatNumber(5555555, Config.DisplayFormat)})")) {
                foreach (var v in (DisplayFormat[])Enum.GetValues(typeof(DisplayFormat))) {
                    if (!ImGui.Selectable($"{v.GetDescription()} ({FormatNumber(5555555, v)})##targetHpFormatSelect", Config.DisplayFormat == v)) continue;
                    Config.DisplayFormat = v;
                    hasChanged = true;
                }

                ImGui.EndCombo();
            }

            hasChanged |= ImGui.Checkbox("Align Left##AdjustTargetHPAlignLeft", ref Config.AlignLeft);
            ImGui.SetNextItemWidth(150);
            hasChanged |= ImGui.InputFloat("X Offset##AdjustTargetHPPositionX", ref Config.Position.X, 1, 5, "%.0f");
            ImGui.SetNextItemWidth(150);
            hasChanged |= ImGui.InputFloat("Y Offset##AdjustTargetHPPositionY", ref Config.Position.Y, 1, 5, "%0.f");
            ImGui.SetNextItemWidth(150);
            hasChanged |= ImGuiExt.InputByte("Font Size##TargetHPFontSize", ref Config.FontSize);

            hasChanged |= ImGui.Checkbox("Custom Color?##TargetHPUseCustomColor", ref Config.UseCustomColor);
            if (Config.UseCustomColor) {
                ImGui.SameLine();
                hasChanged |= ImGui.ColorEdit4("##TargetHPCustomColor", ref Config.CustomColor);
            }

            hasChanged |= ImGui.Checkbox("Hide Auto Attack Icon", ref Config.HideAutoAttack);

            ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.Checkbox("Disable Focus Target HP", ref Config.NoFocus);

            if (!Config.NoFocus) {
                hasChanged |= ImGui.Checkbox("Align Left on Focus Target##AdjustTargetHPFocusAlignLeft", ref Config.FocusAlignLeft);
                ImGui.SetNextItemWidth(150);
                hasChanged |= ImGui.InputFloat("Focus Target X Offset##AdjustTargetHPFocusPositionX", ref Config.FocusPosition.X, 1, 5, "%.0f");
                ImGui.SetNextItemWidth(150);
                hasChanged |= ImGui.InputFloat("Focus Target Y Offset##AdjustTargetHPFocusPositionY", ref Config.FocusPosition.Y, 1, 5, "%0.f");
                ImGui.SetNextItemWidth(150);
                hasChanged |= ImGuiExt.InputByte("Font Size##TargetHPFocusFontSize", ref Config.FocusFontSize);
                hasChanged |= ImGui.Checkbox("Custom Color?##TargetHPFocusUseCustomColor", ref Config.FocusUseCustomColor);
                if (Config.FocusUseCustomColor) {
                    ImGui.SameLine();
                    hasChanged |= ImGui.ColorEdit4("##TargetHPFocusCustomColor", ref Config.FocusCustomColor);
                }
            }
        };

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
    }

    protected override void Disable() {
        SaveConfig(Config);
        Update(true);
    }

    [FrameworkUpdate]
    private void FrameworkUpdate() {
        try {
            Update();
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private void Update(bool reset = false) {
        var target = Service.Targets.SoftTarget ?? Service.Targets.Target;
        if (target != null || reset) {
            var ui = Common.GetUnitBase("_TargetInfo");
            if (ui != null && (ui->IsVisible || reset)) {
                UpdateMainTarget(ui, target, reset);
            }

            var splitUi = Common.GetUnitBase("_TargetInfoMainTarget");
            if (splitUi != null && (splitUi->IsVisible || reset)) {
                UpdateMainTargetSplit(splitUi, target, reset);
            }
        }

        if (Service.Targets.FocusTarget != null || reset) {
            var ui = Common.GetUnitBase("_FocusTargetInfo");
            if (ui != null && (ui->IsVisible || reset)) {
                UpdateFocusTarget(ui, Service.Targets.FocusTarget, reset);
            }
        }
    }

    private void UpdateMainTarget(AtkUnitBase* unitBase, IGameObject target, bool reset = false) {
        if (unitBase == null || unitBase->UldManager.NodeList == null || unitBase->UldManager.NodeListCount < 40) return;
        var gauge = (AtkComponentNode*)unitBase->UldManager.NodeList[36];
        var textNode = (AtkTextNode*)unitBase->UldManager.NodeList[39];
        UiHelper.SetSize(unitBase->UldManager.NodeList[37], reset || !Config.HideAutoAttack ? 44 : 0, reset || !Config.HideAutoAttack ? 20 : 0);
        UpdateGaugeBar(gauge, textNode, target, Config.Position, Config.UseCustomColor ? Config.CustomColor : null, Config.FontSize, Config.AlignLeft, reset);
    }

    private void UpdateFocusTarget(AtkUnitBase* unitBase, IGameObject target, bool reset = false) {
        if (Config.NoFocus) reset = true;
        if (unitBase == null || unitBase->UldManager.NodeList == null || unitBase->UldManager.NodeListCount < 11) return;
        var gauge = (AtkComponentNode*)unitBase->UldManager.NodeList[2];
        var textNode = (AtkTextNode*)unitBase->UldManager.NodeList[10];
        UpdateGaugeBar(gauge, textNode, target, Config.FocusPosition, Config.FocusUseCustomColor ? Config.FocusCustomColor : null, Config.FocusFontSize, Config.FocusAlignLeft, reset);
    }

    private void UpdateMainTargetSplit(AtkUnitBase* unitBase, IGameObject target, bool reset = false) {
        if (unitBase == null || unitBase->UldManager.NodeList == null || unitBase->UldManager.NodeListCount < 9) return;
        var gauge = (AtkComponentNode*)unitBase->UldManager.NodeList[5];
        var textNode = (AtkTextNode*)unitBase->UldManager.NodeList[8];
        UiHelper.SetSize(unitBase->UldManager.NodeList[6], reset || !Config.HideAutoAttack ? 44 : 0, reset || !Config.HideAutoAttack ? 20 : 0);
        UpdateGaugeBar(gauge, textNode, target, Config.Position, Config.UseCustomColor ? Config.CustomColor : null, Config.FontSize, Config.AlignLeft, reset);
    }

    private void UpdateGaugeBar(AtkComponentNode* gauge, AtkTextNode* cloneTextNode, IGameObject target, Vector2 positionOffset, Vector4? customColor, byte fontSize, bool alignLeft, bool reset) {
        if (gauge == null || (ushort)gauge->AtkResNode.Type < 1000) return;

        AtkTextNode* textNode = null;

        for (var i = 5; i < gauge->Component->UldManager.NodeListCount; i++) {
            var node = gauge->Component->UldManager.NodeList[i];
            if (node->Type == NodeType.Text && node->NodeId == CustomNodes.TargetHP) {
                textNode = (AtkTextNode*)node;
                break;
            }
        }

        if (textNode == null && reset) return; // Nothing to clean

        if (textNode == null) {
            textNode = UiHelper.CloneNode(cloneTextNode);
            textNode->AtkResNode.NodeId = CustomNodes.TargetHP;
            var newStrPtr = UiHelper.Alloc(512);
            textNode->NodeText.StringPtr = (byte*)newStrPtr;
            textNode->NodeText.BufSize = 512;
            textNode->SetText("");
            UiHelper.ExpandNodeList(gauge, 1);
            gauge->Component->UldManager.NodeList[gauge->Component->UldManager.NodeListCount++] = (AtkResNode*)textNode;

            var nextNode = gauge->Component->UldManager.RootNode;
            while (nextNode->PrevSiblingNode != null) {
                nextNode = nextNode->PrevSiblingNode;
            }

            textNode->AtkResNode.ParentNode = (AtkResNode*)gauge;
            textNode->AtkResNode.ChildNode = null;
            textNode->AtkResNode.PrevSiblingNode = null;
            textNode->AtkResNode.NextSiblingNode = nextNode;
            nextNode->PrevSiblingNode = (AtkResNode*)textNode;
        }

        if (reset) {
            textNode->AtkResNode.ToggleVisibility(false);
            return;
        }

        textNode->AlignmentFontType = (byte)(alignLeft ? AlignmentType.BottomLeft : AlignmentType.BottomRight);

        UiHelper.SetPosition(textNode, positionOffset.X, positionOffset.Y);
        UiHelper.SetSize(textNode, gauge->AtkResNode.Width - 5, gauge->AtkResNode.Height);
        textNode->AtkResNode.ToggleVisibility(true);
        if (!customColor.HasValue) {
            textNode->TextColor = cloneTextNode->TextColor;
        } else {
            textNode->TextColor.A = (byte)(customColor.Value.W * 255);
            textNode->TextColor.R = (byte)(customColor.Value.X * 255);
            textNode->TextColor.G = (byte)(customColor.Value.Y * 255);
            textNode->TextColor.B = (byte)(customColor.Value.Z * 255);
        }

        textNode->EdgeColor = cloneTextNode->EdgeColor;
        textNode->FontSize = fontSize;

        if (target is ICharacter chara) {
            textNode->SetText($"{FormatNumber(chara.CurrentHp)}/{FormatNumber(chara.MaxHp)}");
        } else {
            textNode->SetText("");
        }
    }

    private string FormatNumber(uint num, DisplayFormat? displayFormat = null) {
        displayFormat ??= Config.DisplayFormat;
        if (displayFormat == DisplayFormat.FullNumber) return num.ToString(Culture);
        if (displayFormat == DisplayFormat.FullNumberSeparators) return num.ToString("N0", Culture);

        var fStr = displayFormat switch {
            DisplayFormat.OneDecimalPrecision => "F1",
            DisplayFormat.TwoDecimalPrecision => "F2",
            _ => "F0"
        };

        return num switch {
            >= 1000000 => $"{(num / 1000000f).ToString(fStr, Culture)}M",
            >= 1000 => $"{(num / 1000f).ToString(fStr, Culture)}K",
            _ => $"{num}"
        };
    }
}
