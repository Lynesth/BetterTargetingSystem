using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using System;
using System.Numerics;
using DalamudGameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using CameraManager = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using System.Collections.Generic;
using Dalamud.Utility.Signatures;

namespace BetterTargetingSystem;

public unsafe class Utils
{
    private static RaptureAtkModule* RaptureAtkModule => CSFramework.Instance()->GetUiModule()->GetRaptureAtkModule();
    internal static bool IsTextInputActive => RaptureAtkModule->AtkModule.IsTextInputActive();

    internal static bool CanAttack(DalamudGameObject obj)
    {
        return Plugin.CanAttackFunction?.Invoke(142, obj.Address) == 1;
    }

    internal static float DistanceBetweenObjects(DalamudGameObject source, DalamudGameObject target)
    {
        return DistanceBetweenObjects(source.Position, target.Position, target.HitboxRadius);
    }
    internal static float DistanceBetweenObjects(Vector3 sourcePos, Vector3 targetPos, float targetHitboxRadius = 0)
    {
        // Might have to tinker a bit whether or not to include hitbox radius in calculation
        // Keeping the source object hitbox radius outside of the calculation for now
        var distance = Vector3.Distance(sourcePos, targetPos);
        //distance -= source.HitboxRadius;
        distance -= targetHitboxRadius;
        return distance;
    }

    internal static float GetCameraRotation()
    {
        // Gives the camera rotation in deg between -180 and 180
        var cameraRotation = RaptureAtkModule->AtkModule.AtkArrayDataHolder.NumberArrays[24]->IntArray[3];

        // Transform the [-180,180] rotation to rad with same 0 as a GameObject rotation
        // There might be an easier way to do that, but geometry and I aren't friends
        var sign = Math.Sign(cameraRotation) == -1 ? -1 : 1;
        var rotation = (float)((Math.Abs(cameraRotation * (Math.PI / 180)) - Math.PI) * sign);

        return rotation;
    }

    internal static bool IsInFrontOfCamera(DalamudGameObject obj, float maxAngle)
    {
        // This is still relying on camera orientation but the cone is from the player's position
        if (Plugin.Client.LocalPlayer == null)
            return false;

        var rotation = GetCameraRotation();
        var faceVec = new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation));

        var dir = obj.Position - Plugin.Client.LocalPlayer.Position;
        var dirVec = new Vector2(dir.Z, dir.X);
        var angle = Math.Acos(Vector2.Dot(dirVec, faceVec) / dirVec.Length() / faceVec.Length());
        return angle <= Math.PI * maxAngle / 360;
    }

    internal static bool IsInLineOfSight(GameObject* target, bool useCamera = false)
    {
        var sourcePos = FFXIVClientStructs.FFXIV.Common.Math.Vector3.Zero;
        if (useCamera)
        {
            // Using the camera's position as origin for raycast
            sourcePos = CameraManager.Instance()->CurrentCamera->Object.Position;
        }
        else
        {
            // Using player's position as origin for raycast
            if (Plugin.Client.LocalPlayer == null) return false;
            var player = (GameObject*)Plugin.Client.LocalPlayer.Address;
            sourcePos = player->Position;
            sourcePos.Y += 2;
        }

        var targetPos = target->Position;
        targetPos.Y += 2;

        var direction = targetPos - sourcePos;
        var distance = direction.Magnitude;

        direction = direction.Normalized;

        RaycastHit hit;
        var flags = stackalloc int[] { 0x4000, 0, 0x4000, 0 };
        var isLoSBlocked = CSFramework.Instance()->BGCollisionModule->RaycastEx(&hit, sourcePos, direction, distance, 1, flags);

        return isLoSBlocked == false;
    }

    internal static uint[] GetEnemyListObjectIds()
    {
        var addonByName = Plugin.GameGui.GetAddonByName("_EnemyList", 1);
        if (addonByName == IntPtr.Zero)
            return Array.Empty<uint>();

        var addon = (AddonEnemyList*)addonByName;
        var numArray = RaptureAtkModule->AtkModule.AtkArrayDataHolder.NumberArrays[21];
        var list = new List<uint>(addon->EnemyCount);
        for (var i = 0; i < addon->EnemyCount; i++)
        {
            var id = (uint)numArray->IntArray[8 + (i * 6)];
            list.Add(id);
        }
        return list.ToArray();
    }
}
