using Dalamud.Game.ClientState.Keys;
using System;
using System.Linq;

namespace BetterTargetingSystem.Keybinds
{
    public class Keybind
    {
        public VirtualKey? Key;
        public bool ControlModifier;
        public bool ShiftModifier;
        public bool AltModifier;

        public Keybind(VirtualKey? key, bool ctrl, bool shift, bool alt)
        {
            this.Key = key;
            this.ControlModifier = ctrl;
            this.ShiftModifier = shift;
            this.AltModifier = alt;
        }

        public bool IsPressed()
        {
            return this.Key != null
                && Plugin.KeyState[(int)this.Key]
                && this.ControlModifier == Plugin.KeyState[(int)VirtualKey.CONTROL]
                && this.ShiftModifier == Plugin.KeyState[(int)VirtualKey.SHIFT]
                && this.AltModifier == Plugin.KeyState[(int)VirtualKey.MENU];
        }

        public override string ToString()
        {
            var keys = new string[]
            {
                this.ControlModifier ? "Ctrl" : "",
                this.AltModifier ? "Alt" : "",
                this.ShiftModifier ? "Shift" : "",
                this.Key != null ? this.Key.ToString()! : "",
            };

            return String.Join("+", keys.Where(s => s != ""));
        }
    }
}
