using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using DalamudGameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace BetterTargetingSystem;

public unsafe class DebugMode
{
    private readonly Plugin Plugin;

    public DebugMode(Plugin plugin)
    {
        this.Plugin = plugin;
    }

    public void DrawCones()
    {
        if (Plugin.Client.LocalPlayer == null)
            return;

        var pos = Plugin.Client.LocalPlayer.Position;

        Plugin.GameGui.WorldToScreen(new Vector3(
            pos.X,
            pos.Y,
            pos.Z),
            out Vector2 v2Local);
        ImGui.GetWindowDrawList().PathLineTo(v2Local);

        var cone1HalfAngle = Plugin.Configuration.Cone1Angle / 2;
        var cone2HalfAngle = Plugin.Configuration.Cone2Angle / 2;
        var cone3HalfAngle = Plugin.Configuration.Cone3Angle / 2;
        var rotation = Utils.GetCameraRotation();
        for (var degrees = -cone1HalfAngle; degrees <= cone1HalfAngle; degrees += 0.1f)
        {
            float distance = Plugin.Configuration.Cone1Distance;
            if (Plugin.Configuration.Cone3Enabled && (degrees >= -cone3HalfAngle && degrees < cone3HalfAngle))
                distance = Plugin.Configuration.Cone3Distance;
            else if (Plugin.Configuration.Cone2Enabled && (degrees >= -cone2HalfAngle && degrees < cone2HalfAngle))
                distance = Plugin.Configuration.Cone2Distance;

            float rad = (float)(degrees * Math.PI / 180) + rotation;
            Plugin.GameGui.WorldToScreen(new Vector3(
                    pos.X + distance * (float)Math.Sin(rad),
                    pos.Y,
                    pos.Z + distance * (float)Math.Cos(rad)),
                out Vector2 vector2);

            ImGui.GetWindowDrawList().PathLineTo(vector2);
        }

        ImGui.GetWindowDrawList().PathLineTo(v2Local);

        var color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.4f));
        ImGui.GetWindowDrawList().PathFillConvex(color);
    }

    public void DrawCircle()
    {
        if (Plugin.Client.LocalPlayer == null)
            return;

        var distance = Plugin.Configuration.CloseTargetsCircleRadius;
        var pos = Plugin.Client.LocalPlayer.Position;
        for (var degrees = 0; degrees <= 360; degrees += 10)
        {
            float rad = (float)(degrees * Math.PI / 180);
            Plugin.GameGui.WorldToScreen(new Vector3(
                    pos.X + distance * (float)Math.Sin(rad),
                    pos.Y,
                    pos.Z + distance * (float)Math.Cos(rad)),
                out Vector2 vector2);

            ImGui.GetWindowDrawList().PathLineTo(vector2);
        }

        var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 1, 0.4f));
        ImGui.GetWindowDrawList().PathFillConvex(color);
    }

    public void Draw()
    {
        if (Plugin.Client.LocalPlayer == null)
            return;

        if (Plugin.Condition[ConditionFlag.InCombat] || Plugin.Condition[ConditionFlag.InFlight]
            || Plugin.Condition[ConditionFlag.BoundByDuty] || Plugin.Condition[ConditionFlag.BetweenAreas])
            return;

        ImGui.Begin("Canvas",
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);
        ImGui.SetWindowSize(ImGui.GetIO().DisplaySize);

        DrawCones();

        if (Plugin.Configuration.CloseTargetsCircleEnabled)
            DrawCircle();

        var (ConeTargets, CloseTargets, _, OnScreenTargets) = Plugin.GetTargets();
        if (ConeTargets.Count > 0 || CloseTargets.Count > 0)
        {
            foreach (var target in ConeTargets)
                HighlightTarget(target, new Vector4(1, 0, 0, 1));

            foreach (var target in CloseTargets.ExceptBy(ConeTargets.Select(o => o.ObjectId), o => o.ObjectId))
                HighlightTarget(target, new Vector4(0, 0.8f, 1, 1));
        }
        else
        {
            foreach (var target in OnScreenTargets)
                HighlightTarget(target, new Vector4(0, 1, 0, 1));
        }
        ImGui.End();
    }

    private void HighlightTarget(DalamudGameObject target, Vector4 colour)
    {
        Plugin.GameGui.WorldToScreen(target.Position, out var screenPos);
        var camera = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager.Instance()->CurrentCamera->Object;
        var distance = Utils.DistanceBetweenObjects(camera.Position, target.Position, 0);
        var size = (int)Math.Round(100 * (25 / distance)) * Math.Max(target.HitboxRadius, ((GameObject*)target.Address)->Height);
        ImGui.GetWindowDrawList().AddRect(
            new Vector2(screenPos.X - (size / 2), screenPos.Y),
            new Vector2(screenPos.X + (size / 2), screenPos.Y - size),
            ImGui.GetColorU32(colour),
            0f,
            ImDrawFlags.None,
            3f);
    }
}
