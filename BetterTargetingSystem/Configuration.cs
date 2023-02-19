using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin;
using BetterTargetingSystem.Keybinds;
using System;

namespace BetterTargetingSystem;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public float Cone1Angle { get; set; } = 140;
    public float Cone1Distance { get; set; } = 5;
    public bool Cone2Enabled { get; set; } = true;
    public float Cone2Angle { get; set; } = 90;
    public float Cone2Distance { get; set; } = 15;
    public bool Cone3Enabled { get; set; } = true;
    public float Cone3Angle { get; set; } = 50;
    public float Cone3Distance { get; set; } = 40;
    public bool CloseTargetsCircleEnabled { get; set; } = true;
    public float CloseTargetsCircleRadius { get; set; } = 5;

    public Keybind TabTargetKeybind { get; set; } = new Keybind(VirtualKey.TAB, false, false, false);
    public Keybind ClosestTargetKeybind { get; set; } = new Keybind(VirtualKey.TAB, false, true, false);
    public Keybind LowestHealthTargetKeybind { get; set; } = new Keybind(VirtualKey.TAB, true, false, false);
    public Keybind BestAOETargetKeybind { get; set; } = new Keybind(null, false, false, false);

    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private DalamudPluginInterface? PluginInterface;

    public void Initialize(DalamudPluginInterface pluginInterface) => this.PluginInterface = pluginInterface;

    public void Save() => this.PluginInterface!.SavePluginConfig(this);
}
