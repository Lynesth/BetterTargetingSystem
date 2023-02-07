using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using BetterTargetingSystem.Keybinds;

namespace BetterTargetingSystem.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private Configuration Configuration;
        public Keybind CurrentKeys { get; private set; }

        private bool ModifyingKeybindTTK = false;
        private bool ModifyingKeybindCTK = false;
        private bool ModifyingKeybindLHTK = false;
        private bool ModifyingKeybindBAOETK = false;

        public ConfigWindow(Plugin plugin) : base(
            "Better Targeting System",
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.Size = new Vector2(185, 270);
            this.SizeCondition = ImGuiCond.Appearing;

            this.Configuration = plugin.Configuration;

            // Just making sure we're not using unallowed keys even after an update
            var ValidVirtualKeys = Plugin.KeyState.GetValidVirtualKeys();
            SupportedKeys.RemoveAll(key => ValidVirtualKeys.Contains(ImGuiHelpers.ImGuiKeyToVirtualKey(key)) == false);

            this.CurrentKeys = new Keybind(null, false, false, false);
        }

        public void Dispose() { }

        public override void Draw()
        {
            if (this.ModifyingKeybindTTK || this.ModifyingKeybindCTK || this.ModifyingKeybindLHTK || this.ModifyingKeybindBAOETK)
                this.CurrentKeys = GetKeys();

            var tabTargetKeybind = this.ModifyingKeybindTTK
                ? this.CurrentKeys.ToString() :
                (this.Configuration.TabTargetKeybind.Key != null ? this.Configuration.TabTargetKeybind.ToString() : "None");
            var closestTargetKeybind = this.ModifyingKeybindCTK
                ? this.CurrentKeys.ToString()
                : (this.Configuration.ClosestTargetKeybind.Key != null ? this.Configuration.ClosestTargetKeybind.ToString() : "None");
            var lowestHealthTargetKeybind = this.ModifyingKeybindLHTK
                ? this.CurrentKeys.ToString()
                : (this.Configuration.LowestHealthTargetKeybind.Key != null ? this.Configuration.LowestHealthTargetKeybind.ToString() : "None");
            var bestAOETargetKeybind = this.ModifyingKeybindBAOETK
                ? this.CurrentKeys.ToString()
                : (this.Configuration.BestAOETargetKeybind.Key != null ? this.Configuration.BestAOETargetKeybind.ToString() : "None");

            ImGui.Text("Keybinds Configuration:\n\n");
            ImGui.PushItemWidth(170);
            ImGui.Text("[Cycle Targets]");
            ImGui.InputText($"##ttk_Keybind", ref tabTargetKeybind, 200, ImGuiInputTextFlags.ReadOnly);
            if (ImGui.IsItemActive())
            {
                ImGui.SetTooltip("Use Backspace to remove keybind");
                this.ModifyingKeybindTTK = true;
                // Prevent trying to set Alt-Tab as a keybind
                if (this.CurrentKeys.Key != null && (this.CurrentKeys.Key != VirtualKey.TAB || this.CurrentKeys.AltModifier == false))
                {
                    this.Configuration.TabTargetKeybind = this.CurrentKeys;
                    this.Configuration.Save();
                    UnfocusInput();
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.Backspace))
                {
                    this.Configuration.TabTargetKeybind = new Keybind(null, false, false, false);
                    this.Configuration.Save();
                    UnfocusInput();
                }
            }
            else
            {
                this.ModifyingKeybindTTK = false;
            }

            ImGui.Text("\n");

            ImGui.Text("[Closest Target]");
            ImGui.InputText($"##ctk_Keybind", ref closestTargetKeybind, 200, ImGuiInputTextFlags.ReadOnly);
            if (ImGui.IsItemActive())
            {
                ImGui.SetTooltip("Use Backspace to remove keybind");
                this.ModifyingKeybindCTK = true;
                // Prevent trying to set Alt-Tab as a keybind
                if (this.CurrentKeys.Key != null && (this.CurrentKeys.Key != VirtualKey.TAB || this.CurrentKeys.AltModifier == false))
                {
                    this.Configuration.ClosestTargetKeybind = this.CurrentKeys;
                    this.Configuration.Save();
                    UnfocusInput();
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.Backspace))
                {
                    this.Configuration.ClosestTargetKeybind = new Keybind(null, false, false, false);
                    this.Configuration.Save();
                    UnfocusInput();
                }
            }
            else
            {
                this.ModifyingKeybindCTK = false;
            }

            ImGui.Text("\n");

            ImGui.Text("[Lowest Health Target]");
            ImGui.InputText($"##lhtk_Keybind", ref lowestHealthTargetKeybind, 200, ImGuiInputTextFlags.ReadOnly);
            if (ImGui.IsItemActive())
            {
                ImGui.SetTooltip("Use Backspace to remove keybind");
                this.ModifyingKeybindLHTK = true;
                // Prevent trying to set Alt-Tab as a keybind
                if (this.CurrentKeys.Key != null && (this.CurrentKeys.Key != VirtualKey.TAB || this.CurrentKeys.AltModifier == false))
                {
                    this.Configuration.LowestHealthTargetKeybind = this.CurrentKeys;
                    this.Configuration.Save();
                    UnfocusInput();
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.Backspace))
                {
                    this.Configuration.LowestHealthTargetKeybind = new Keybind(null, false, false, false);
                    this.Configuration.Save();
                    UnfocusInput();
                }
            }
            else
            {
                this.ModifyingKeybindLHTK = false;
            }

            ImGui.Text("\n");

            ImGui.Text("[Best AOE Target]");
            ImGui.InputText($"##baoetk_Keybind", ref bestAOETargetKeybind, 200, ImGuiInputTextFlags.ReadOnly);
            if (ImGui.IsItemActive())
            {
                ImGui.SetTooltip("Use Backspace to remove keybind");
                this.ModifyingKeybindBAOETK = true;
                // Prevent trying to set Alt-Tab as a keybind
                if (this.CurrentKeys.Key != null && (this.CurrentKeys.Key != VirtualKey.TAB || this.CurrentKeys.AltModifier == false))
                {
                    this.Configuration.BestAOETargetKeybind = this.CurrentKeys;
                    this.Configuration.Save();
                    UnfocusInput();
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.Backspace))
                {
                    this.Configuration.BestAOETargetKeybind = new Keybind(null, false, false, false);
                    this.Configuration.Save();
                    UnfocusInput();
                }
            }
            else
            {
                this.ModifyingKeybindBAOETK = false;
            }
        }

        private void UnfocusInput()
        {
            this.ModifyingKeybindTTK = false;
            this.ModifyingKeybindCTK = false;
            this.ModifyingKeybindLHTK = false;
            this.ModifyingKeybindBAOETK = false;
            this.CurrentKeys = new Keybind(null, false, false, false);
            ImGui.SetWindowFocus(null); // unfocus window to clear keyboard focus
            ImGui.SetWindowFocus(); // refocus window
        }

        private Keybind GetKeys()
        {
            VirtualKey? key = null;
            var io = ImGui.GetIO();
            var ctrl = io.KeyCtrl;
            var shift = io.KeyShift;
            var alt = io.KeyAlt;
            foreach (var k in SupportedKeys)
            {
                if (ImGui.IsKeyPressed(k))
                {
                    key = ImGuiHelpers.ImGuiKeyToVirtualKey(k);
                    break;
                }
            }
            return new Keybind(key, ctrl, shift, alt);
        }

        private static List<ImGuiKey> SupportedKeys = new List<ImGuiKey>()
        {
            ImGuiKey.Tab,
            ImGuiKey.CapsLock,
            ImGuiKey.Space,
            ImGuiKey.End,
            ImGuiKey.Home,
            ImGuiKey.LeftArrow,
            ImGuiKey.UpArrow,
            ImGuiKey.RightArrow,
            ImGuiKey.DownArrow,
            ImGuiKey.Insert,
            ImGuiKey.Delete,
            ImGuiKey._0,
            ImGuiKey._1,
            ImGuiKey._2,
            ImGuiKey._3,
            ImGuiKey._4,
            ImGuiKey._5,
            ImGuiKey._6,
            ImGuiKey._7,
            ImGuiKey._8,
            ImGuiKey._9,
            ImGuiKey.A,
            ImGuiKey.B,
            ImGuiKey.C,
            ImGuiKey.D,
            ImGuiKey.E,
            ImGuiKey.F,
            ImGuiKey.G,
            ImGuiKey.H,
            ImGuiKey.I,
            ImGuiKey.J,
            ImGuiKey.K,
            ImGuiKey.L,
            ImGuiKey.M,
            ImGuiKey.N,
            ImGuiKey.O,
            ImGuiKey.P,
            ImGuiKey.Q,
            ImGuiKey.R,
            ImGuiKey.S,
            ImGuiKey.T,
            ImGuiKey.U,
            ImGuiKey.V,
            ImGuiKey.W,
            ImGuiKey.X,
            ImGuiKey.Y,
            ImGuiKey.Z,
            ImGuiKey.Keypad0,
            ImGuiKey.Keypad1,
            ImGuiKey.Keypad2,
            ImGuiKey.Keypad3,
            ImGuiKey.Keypad4,
            ImGuiKey.Keypad5,
            ImGuiKey.Keypad6,
            ImGuiKey.Keypad7,
            ImGuiKey.Keypad8,
            ImGuiKey.Keypad9,
            ImGuiKey.KeypadMultiply,
            ImGuiKey.KeypadAdd,
            ImGuiKey.KeypadSubtract,
            ImGuiKey.KeypadDecimal,
            ImGuiKey.KeypadDivide,
            ImGuiKey.F1,
            ImGuiKey.F2,
            ImGuiKey.F3,
            ImGuiKey.F4,
            ImGuiKey.F5,
            ImGuiKey.F6,
            ImGuiKey.F7,
            ImGuiKey.F8,
            ImGuiKey.F9,
            ImGuiKey.F10,
            ImGuiKey.F11,
            ImGuiKey.F12,
            ImGuiKey.Semicolon,
            ImGuiKey.Equal,
            ImGuiKey.Comma,
            ImGuiKey.Minus,
            ImGuiKey.Period,
            ImGuiKey.Slash,
            ImGuiKey.GraveAccent,
            ImGuiKey.LeftBracket,
            ImGuiKey.Backslash,
            ImGuiKey.RightBracket,
            ImGuiKey.Apostrophe,
            //ImGuiKey.OEM_8, // Valid in VirtualKey but doesn't exist in ImGuiKey
            //ImGuiKey.OEM_102, // Valid in VirtualKey but doesn't exist in ImGuiKey
        };
    }
}
