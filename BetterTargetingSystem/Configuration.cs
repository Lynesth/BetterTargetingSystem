using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin;
using BetterTargetingSystem.Keybinds;
using System;

namespace BetterTargetingSystem
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public Keybind TabTargetKeybind { get; set; } = new Keybind(VirtualKey.TAB, false, false, false);
        public Keybind ClosestTargetKeybind { get; set; } = new Keybind(VirtualKey.TAB, false, true, false);
        public Keybind LowestHealthTargetKeybind { get; set; } = new Keybind(VirtualKey.TAB, true, false, false);

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? PluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}
