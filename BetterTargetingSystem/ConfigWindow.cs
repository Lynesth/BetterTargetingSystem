using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

using BetterTargetingSystem.Keybinds;
using System.Collections.Generic;
using System.Linq;

namespace BetterTargetingSystem.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Plugin Plugin;
        private Configuration Configuration;
        public Keybind CurrentKeys { get; private set; }

        private List<KeybindInfo> Keybinds = new List<KeybindInfo>();
        private class KeybindInfo
        {
            public string title;
            public string name;
            public string value = "None";
            public bool EditingKeybind = false;
            public KeybindInfo(string title, string keybind)
            {
                this.title = title;
                this.name = keybind;
            }
        }

        public ConfigWindow(Plugin plugin) : base(
            "Better Targeting System",
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.Size = new Vector2(185, 270);
            this.SizeCondition = ImGuiCond.Appearing;

            this.Plugin = plugin;
            this.Configuration = plugin.Configuration;

            this.CurrentKeys = new Keybind();

            this.Keybinds.Add(new KeybindInfo("[Cycle Targets]", "TabTargetKeybind"));
            this.Keybinds.Add(new KeybindInfo("[Closest Target]", "ClosestTargetKeybind"));
            this.Keybinds.Add(new KeybindInfo("[Lowest Health Target]", "LowestHealthTargetKeybind"));
            this.Keybinds.Add(new KeybindInfo("[Highest Health Target]", "HighestHealthTargetKeybind"));
            this.Keybinds.Add(new KeybindInfo("[Best AOE Target]", "BestAOETargetKeybind"));
        }

        public void Dispose() { }

        public override void Draw() {
            if (ImGui.BeginTabBar("BTSConfigTabs", ImGuiTabBarFlags.None))
            {
                if (ImGui.BeginTabItem("Keybinds"))
                {
                    KeybindsConfig();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Settings"))
                {
                    SettingsConfig();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }

        private void KeybindsConfig()
        {
            if (this.Keybinds.Any(k => k.EditingKeybind == true))
                this.CurrentKeys = GetKeys();

            //ImGui.Text("Keybinds Configuration:\n\n");
            ImGui.PushItemWidth(170);

            foreach ( var keybind in this.Keybinds)
            {
                var confKeybind = this.Configuration.GetType().GetProperty(keybind.name);
                if (confKeybind == null)
                    continue;

                var confKeybindValue = confKeybind.GetValue(this.Configuration, null) as Keybind;
                if (confKeybindValue == null)
                    continue;

                keybind.value = keybind.EditingKeybind
                    ? this.CurrentKeys.ToString()
                    : confKeybindValue.Key != null ? confKeybindValue.ToString() : "None";

                ImGui.Text($"\n{keybind.title}");
                ImGui.InputText($"##{keybind.name}", ref keybind.value, 200, ImGuiInputTextFlags.ReadOnly);
                if (ImGui.IsItemActive())
                {
                    ImGui.SetTooltip("Use Backspace to remove keybind");
                    keybind.EditingKeybind = true;
                    // Prevent trying to set Alt-Tab as a keybind
                    if (this.CurrentKeys.Key != null && (this.CurrentKeys.Key != VirtualKey.TAB || this.CurrentKeys.AltModifier == false))
                    {
                        confKeybind.SetValue(this.Configuration, this.CurrentKeys);
                        this.Configuration.Save();
                        UnfocusInput();
                        keybind.EditingKeybind = false;
                    }
                    else if (ImGui.IsKeyPressed(ImGuiKey.Backspace))
                    {
                        confKeybind.SetValue(this.Configuration, new Keybind());
                        this.Configuration.Save();
                        UnfocusInput();
                        keybind.EditingKeybind = false;
                    }
                }
                else
                {
                    keybind.EditingKeybind = false;
                }
            }
        }

        private void UnfocusInput()
        {
            this.CurrentKeys = new Keybind();
            ImGui.SetWindowFocus(null); // unfocus window to clear keyboard focus
            ImGui.SetWindowFocus(); // refocus window
        }

        private void SettingsConfig()
        {
            ImGui.Text("\n[Cone 1]");
            if (ImGui.BeginTable("SettingsConfigTable", 2, ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 50);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 112);
                ImGui.TableNextColumn();
                ImGui.Text("Enabled:");
                ImGui.TableNextColumn();
                var alwaysEnabled = true;
                ImGui.BeginGroup();
                ImGui.BeginDisabled();
                ImGui.Checkbox("", ref alwaysEnabled);
                ImGui.EndDisabled();
                ImGui.EndGroup();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("At least one cone must be enabled,\nthis is why you can't disable this one.");
                ImGui.TableNextColumn();
                ImGui.Text("Angle:");
                ImGui.TableNextColumn();
                ImGui.PushItemWidth(112);
                var cone1Angle = Configuration.Cone1Angle;
                var minAngle = Configuration.Cone2Enabled
                    ? Configuration.Cone2Angle
                    : (Configuration.Cone3Enabled ? Configuration.Cone3Angle : 10);
                if (ImGui.DragFloat("##Cone1Angle", ref cone1Angle, .5f, minAngle, 360f))
                {
                    cone1Angle = Math.Clamp(cone1Angle, minAngle, 360f);
                    Configuration.Cone1Angle = (float)Math.Round(cone1Angle, 1);
                    Configuration.Save();
                }
                ImGui.TableNextColumn();
                ImGui.Text("Distance:");
                ImGui.TableNextColumn();
                ImGui.PushItemWidth(112);
                var cone1Distance = Configuration.Cone1Distance;
                var maxDistance = Configuration.Cone2Enabled
                    ? Configuration.Cone2Distance
                    : (Configuration.Cone3Enabled ? Configuration.Cone3Distance : 40);
                if (ImGui.DragFloat("##Cone1Distance", ref cone1Distance, .1f, 1f, maxDistance))
                {
                    cone1Distance = Math.Clamp(cone1Distance, 1f, maxDistance);
                    Configuration.Cone1Distance = (float)Math.Round(cone1Distance, 1);
                    Configuration.Save();
                }
                ImGui.EndTable();
            }

            ImGui.Text("\n[Cone 2]");
            if (ImGui.BeginTable("SettingsConfigTable", 2, ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 50);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 112);
                ImGui.TableNextColumn();
                ImGui.Text("Enabled:");
                ImGui.TableNextColumn();
                var cone2Enabled = Configuration.Cone2Enabled;
                if (ImGui.Checkbox("", ref cone2Enabled))
                {
                    Configuration.Cone2Enabled = cone2Enabled;
                    Configuration.Save();
                }
                ImGui.TableNextColumn();
                ImGui.Text("Angle:");
                ImGui.TableNextColumn();
                ImGui.PushItemWidth(112);
                var cone2Angle = Configuration.Cone2Angle;
                var minAngle = Configuration.Cone3Enabled
                    ? Configuration.Cone3Angle
                    : 10;
                if (ImGui.DragFloat("##Cone2Angle", ref cone2Angle, .5f, minAngle, Configuration.Cone1Angle))
                {
                    cone2Angle = Math.Clamp(cone2Angle, minAngle, Configuration.Cone1Angle);
                    Configuration.Cone2Angle = (float)Math.Round(cone2Angle, 1);
                    Configuration.Save();
                }
                ImGui.TableNextColumn();
                ImGui.Text("Distance:");
                ImGui.TableNextColumn();
                ImGui.PushItemWidth(112);
                var cone2Distance = Configuration.Cone2Distance;
                var maxDistance = Configuration.Cone3Enabled
                    ? Configuration.Cone3Distance
                    : 40;
                if (ImGui.DragFloat("##Cone2Distance", ref cone2Distance, .1f, Configuration.Cone1Distance, maxDistance))
                {
                    cone2Distance = Math.Clamp(cone2Distance, Configuration.Cone1Distance, maxDistance);
                    Configuration.Cone2Distance = (float)Math.Round(cone2Distance, 1);
                    Configuration.Save();
                }
                ImGui.EndTable();
            }

            ImGui.Text("\n[Cone 3]");
            if (ImGui.BeginTable("SettingsConfigTable", 2, ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 50);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 112);
                ImGui.TableNextColumn();
                ImGui.Text("Enabled:");
                ImGui.TableNextColumn();
                var cone3Enabled = Configuration.Cone3Enabled;
                if (ImGui.Checkbox("", ref cone3Enabled))
                {
                    Configuration.Cone3Enabled = cone3Enabled;
                    Configuration.Save();
                }
                ImGui.TableNextColumn();
                ImGui.Text("Angle:");
                ImGui.TableNextColumn();
                ImGui.PushItemWidth(112);
                var cone3Angle = Configuration.Cone3Angle;
                var maxAngle = Configuration.Cone2Enabled
                    ? Configuration.Cone2Angle
                    : Configuration.Cone1Angle;
                if (ImGui.DragFloat("##Cone3Angle", ref cone3Angle, .5f, 10f, maxAngle))
                {
                    cone3Angle = Math.Clamp(cone3Angle, 10f, maxAngle);
                    Configuration.Cone3Angle = (float)Math.Round(cone3Angle, 1);
                    Configuration.Save();
                }
                ImGui.TableNextColumn();
                ImGui.Text("Distance:");
                ImGui.TableNextColumn();
                ImGui.PushItemWidth(112);
                var cone3Distance = Configuration.Cone3Distance;
                var minDistance = Configuration.Cone2Enabled
                    ? Configuration.Cone2Distance
                    : Configuration.Cone1Distance;
                if (ImGui.DragFloat("##Cone3Distance", ref cone3Distance, .1f, minDistance, 40f))
                {
                    cone3Distance = Math.Clamp(cone3Distance, minDistance, 40f);
                    Configuration.Cone3Distance = (float)Math.Round(cone3Distance, 1);
                    Configuration.Save();
                }
                ImGui.EndTable();
            }

            ImGui.Text("\n[Close Targets Circle]");
            if (ImGui.BeginTable("SettingsConfigTable", 2, ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 50);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 112);
                ImGui.TableNextColumn();
                ImGui.Text("Enabled:");
                ImGui.TableNextColumn();
                var closeTargetsCircleEnabled = Configuration.CloseTargetsCircleEnabled;
                if (ImGui.Checkbox("", ref closeTargetsCircleEnabled))
                {
                    Configuration.CloseTargetsCircleEnabled = closeTargetsCircleEnabled;
                    Configuration.Save();
                }
                ImGui.TableNextColumn();
                ImGui.Text("Radius:");
                ImGui.TableNextColumn();
                ImGui.PushItemWidth(112);
                var closeTargetsCircleRadius = Configuration.CloseTargetsCircleRadius;
                if (ImGui.DragFloat("##CloseTargetsCircleRadius", ref closeTargetsCircleRadius, .1f, 1f, 40f))
                {
                    closeTargetsCircleRadius = Math.Clamp(closeTargetsCircleRadius, 1f, 40f);
                    Configuration.CloseTargetsCircleRadius = (float)Math.Round(closeTargetsCircleRadius, 1);
                    Configuration.Save();
                }
                ImGui.EndTable();
            }
            ImGui.NewLine();
            if (ImGui.Button("Reset settings to defaults", new Vector2(170,25)))
            {
                Configuration.Cone1Angle = 140;
                Configuration.Cone1Distance = 5;
                Configuration.Cone2Enabled = true;
                Configuration.Cone2Angle = 90;
                Configuration.Cone2Distance = 15;
                Configuration.Cone3Enabled = true;
                Configuration.Cone3Angle = 50;
                Configuration.Cone3Distance = 40;
                Configuration.CloseTargetsCircleEnabled = true;
                Configuration.CloseTargetsCircleRadius = 5;
                Configuration.Save();
            }

            Plugin.DebugMode.Draw();
        }

        private Keybind GetKeys()
        {
            VirtualKey? key = null;
            var io = ImGui.GetIO();
            var ctrl = io.KeyCtrl;
            var shift = io.KeyShift;
            var alt = io.KeyAlt;

            if (ImGui.IsKeyPressed(ImGuiKey.Tab))
                return new Keybind(VirtualKey.TAB, ctrl, shift, alt);

            Keybind.GetKeyboardState();
            foreach (var k in Keybind.SupportedKeys)
            {
                if (Keybind.IsKeyDown((int) k))
                {
                    key = k;
                    break;
                }
            }
            return new Keybind(key, ctrl, shift, alt);
        }
    }
}
