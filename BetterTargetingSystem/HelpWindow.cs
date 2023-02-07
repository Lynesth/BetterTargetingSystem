using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace BetterTargetingSystem.Windows;

public class HelpWindow : Window, IDisposable
{
    public HelpWindow(Plugin plugin) : base(
        "Better Targeting System - Help",
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.Size = new Vector2(185, 270);
        this.SizeCondition = ImGuiCond.Appearing;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.PushTextWrapPos(500);
        ImGui.Text("Better Targeting System is a plugin that tries to improve the way Tab targeting works. It uses cones of different sizes depending on distance to identify which targets can be acquired.");
        ImGui.Text("\nHere's what it does when you press the [Cycle Targets] keybind:");
        ImGui.Indent();
        ImGui.Text("- It first tries to target an enemy in front of your character in the direction your camera is facing (as explained above).");
        ImGui.Text("- If no enemies are in one of those cones in front of you, it will then target a very close enemy (< 5y).");
        ImGui.Text("- If there are no enemies nearby, it will target an enemy you are currently in combat with, using the enemy list.");
        ImGui.Text("- If there are still no enemies, it will then just default to any available target visible on your screen.");
        ImGui.Unindent();
        ImGui.Text("\nThe plugin will not target enemies you cannot interact with, such as those in another party's levequest / treasure hunt.");
        ImGui.Text("It also adds an extra keybind to target the lowest (absolute) health enemy as well as a keybind to target the \"best\" enemy for targeted aoes.");
        ImGui.Text("And lastly, it tries to not change the order in which enemies are cycled through (to be improved).");
        ImGui.Text("\n\nDo not hesitate to give feedback/suggestion and submit bug reports on the Github repository.");
    }
}
