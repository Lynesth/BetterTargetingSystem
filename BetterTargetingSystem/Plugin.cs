using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Component.GUI;
using BetterTargetingSystem.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using DalamudCharacter = Dalamud.Game.ClientState.Objects.Types.Character;
using DalamudGameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using CameraManager = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using Dalamud.Utility.Signatures;

namespace BetterTargetingSystem;

public sealed unsafe class Plugin : IDalamudPlugin
{
    public string Name => "Better Targeting System";
    public string CommandName => "/bts";

    public List<uint> LastTargets { get; private set; } = new List<uint>();

    private DalamudPluginInterface PluginInterface { get; init; }
    private CommandManager CommandManager { get; init; }
    private static Framework Framework { get; set; } = null!;
    public Configuration Configuration { get; init; }

    public WindowSystem WindowSystem = new("BetterTargetingSystem");

    [PluginService] private static ClientState Client { get; set; } = null!;
    [PluginService] private static ObjectTable Objects { get; set; } = null!;
    [PluginService] private static TargetManager TargetManager { get; set; } = null!;
    [PluginService] private static ChatGui Chat { get; set; } = null!;
    [PluginService] private static GameGui GameGui { get; set; } = null!;
    [PluginService] internal static KeyState KeyState { get; set; } = null!;
    [PluginService] private static SigScanner SigScanner { get; set; } = null!;

    private ConfigWindow ConfigWindow { get; init; }

    // Shamelessly stolen, not sure what that game function exactly does but it works
    private delegate nint CanAttackDelegate(nint a1, nint objectAddress);
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3")]
    private CanAttackDelegate? CanAttackFunction = null!;

    private static IntPtr InputTextPtr;

    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] CommandManager commandManager,
        Framework framework)
    {
        SignatureHelper.Initialise(this);

        try
        {
            // Stolen from TPie
            InputTextPtr = *(IntPtr*)((IntPtr)AtkStage.GetSingleton() + 0x28) + 0x188E;
        }
        catch (Exception)
        {
            Chat.PrintError($"[{Name}]\nAn error occured, unloading plugin.");
            throw new Exception("Failed to retrieve input text pointer.");
        }

        this.PluginInterface = pluginInterface;
        this.CommandManager = commandManager;

        this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.Initialize(this.PluginInterface);

        Framework = framework;
        Framework.Update += Update;

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        this.PluginInterface.UiBuilder.Draw += DrawUI;
        this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        this.CommandManager.AddHandler(CommandName, new CommandInfo(ShowConfigWindow));
    }

    public void Dispose()
    {
        Framework.Update -= Update;
        this.WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        this.CommandManager.RemoveHandler(CommandName);
    }

    public static void Log(string message) => PluginLog.Debug(message);

    private void ShowConfigWindow(string command, string args) => this.DrawConfigUI();

    private void DrawUI() => this.WindowSystem.Draw();

    public void DrawConfigUI()
    {
        var window = WindowSystem.GetWindow("Better Targeting System");
        window?.Toggle();
    }

    private bool IsInputTextActive()
    {
        return InputTextPtr != IntPtr.Zero && *(bool*)InputTextPtr;
    }

    public void Update(Framework framework)
    {
        if (Client.IsLoggedIn == false)
            return;

        if (IsInputTextActive())
            return;

        if (Configuration.TabTargetKeybind.IsPressed())
        {
            KeyState[(int)Configuration.TabTargetKeybind.Key!] = false;
            NextTarget();
            return;
        }

        if (Configuration.ClosestTargetKeybind.IsPressed())
        {
            KeyState[(int)Configuration.ClosestTargetKeybind.Key!] = false;
            TargetClosest();
            return;
        }

        if (Configuration.LowestHealthTargetKeybind.IsPressed())
        {
            KeyState[(int)Configuration.LowestHealthTargetKeybind.Key!] = false;
            TargetLowestHealth();
            return;
        }
    }

    private void TargetLowestHealth() => TargetClosest(true);

    private void TargetClosest(bool lowestHealth = false)
    {
        var (Targets, CloseTargets, EnemyListTargets, OnScreenTargets) = GetTargets();

        // All objects in Targets and CloseTargets are in OnScreenTargets so it's not necessary to test them
        if (EnemyListTargets.Count == 0 && OnScreenTargets.Count == 0)
            return;

        var _targets = OnScreenTargets.Count > 0 ? OnScreenTargets : EnemyListTargets;

        var _target = lowestHealth
            ? _targets.OrderBy(o => (o as DalamudCharacter)?.CurrentHp).ThenBy(DistanceToPlayer).First()
            : _targets.OrderBy(DistanceToPlayer).First();

        TargetManager.SetTarget(_target);
    }

    private void NextTarget()
    {
        var (Targets, CloseTargets, EnemyListTargets, OnScreenTargets) = GetTargets();

        // All objects in Targets and CloseTargets are in OnScreenTargets so it's not necessary to test them
        if (EnemyListTargets.Count == 0 && OnScreenTargets.Count == 0)
            return;

        var _targets = Targets;
        if (Targets.Count > 0)
        {
            if (Targets.Count == 1 && this.LastTargets.ToHashSet().SetEquals(Targets.Select(o => o.ObjectId).ToHashSet()))
            {
                if (CloseTargets.Count > 0)
                    _targets.AddRange(CloseTargets);
                else
                    _targets.AddRange(EnemyListTargets);

                _targets = _targets.Distinct().ToList();
            }
        }
        else if (CloseTargets.Count > 0)
            _targets = CloseTargets;
        else if (EnemyListTargets.Count > 0)
            _targets = EnemyListTargets;
        else
            _targets = OnScreenTargets;

        var _currentTarget = TargetManager.Target;
        if (_currentTarget == null)
        {
            _targets = _targets.OrderBy(DistanceToPlayer).ToList();
            this.LastTargets = _targets.Select(o => o.ObjectId).ToList();
            TargetManager.SetTarget(_targets[0]);
            return;
        }

        if (this.LastTargets.ToHashSet().SetEquals(_targets.Select(o => o.ObjectId).ToHashSet()) == false)
        {
            _targets = _targets.OrderBy(DistanceToPlayer).ToList();
            this.LastTargets = _targets.Select(o => o.ObjectId).ToList();
        }

        var i = 0;
        for (i = 0; i < _targets.Count; i++)
        {
            if (this.LastTargets[i] == _currentTarget.ObjectId)
                break;
        }

        var id = i < this.LastTargets.Count - 1 ? this.LastTargets[i + 1] : this.LastTargets[0];

        TargetManager.SetTarget(_targets.First(o => o.ObjectId == id));
    }

    private bool CanAttack(DalamudGameObject obj)
    {
        return CanAttackFunction?.Invoke(142, obj.Address) == 1;
    }

    internal static float DistanceToPlayer(DalamudGameObject obj)
    {
        if (Client.LocalPlayer == null)
            return 0;

        // Might have to tinker a bit whether or not to include hitbox radius in calculation
        // Keeping the player hitbox radius outside of the calculation for now
        var distance = Vector3.Distance(Client.LocalPlayer.Position, obj.Position);
        //distance -= Client.LocalPlayer.HitboxRadius;
        distance -= obj.HitboxRadius;
        return distance;
    }

    internal static bool IsInFrontOfCamera(DalamudGameObject obj, int maxAngle)
    {
        // This is still relying on camera orientation but the cone is from the player's position
        if (Client.LocalPlayer == null)
            return false;

        // Gives the camera rotation in deg between -180 and 180
        var cameraRotation = CSFramework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder.NumberArrays[24]->IntArray[3];

        // Transform the [-180,180] rotation to rad with same 0 as a GameObject rotation
        // There might be an easier way to do that, but geometry and I aren't friends
        var sign = Math.Sign(cameraRotation) == -1 ? -1 : 1;
        var rotation = (float)((Math.Abs(cameraRotation * (Math.PI / 180)) - Math.PI) * sign);

        var faceVec = new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation));

        var dir = obj.Position - Client.LocalPlayer.Position;
        var dirVec = new Vector2(dir.Z, dir.X);
        var angle = Math.Acos(Vector2.Dot(dirVec, faceVec) / dirVec.Length() / faceVec.Length());
        return angle <= Math.PI * maxAngle / 360;
    }

    private bool IsInLineOfSight(GameObject* target, bool useCamera = false)
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
            if (Client.LocalPlayer == null) return false;
            var player = (GameObject*)Client.LocalPlayer.Address;
            sourcePos = player->Position;
            sourcePos.Y += 2;
        }

        var hit = stackalloc RaycastHit[1];
        var flags = stackalloc int[] { 0x4000, 0x4000 };

        var targetPos = target->Position;
        targetPos.Y += 2;

        var direction = targetPos - sourcePos;
        var distance = direction.Magnitude;

        direction = direction.Normalized;
        var isLoSBlocked = CSFramework.Instance()->BGCollisionModule->RaycastEx(hit, sourcePos, direction, distance, 1, flags);

        return isLoSBlocked == false;
    }

    public record ObjectsList(List<DalamudGameObject> Targets, List<DalamudGameObject> CloseTargets, List<DalamudGameObject> TargetsEnemy, List<DalamudGameObject> OnScreenTargets);
    private ObjectsList GetTargets()
    {
        /* Always return 4 lists.
            * The enemies in a cone in front of the player
            * The enemies in a close radius around the player
            * The enemies in the Enemy List Addon
            * All the targets on screen
            */
        var TargetsList = new List<DalamudGameObject>();
        var CloseTargetsList = new List<DalamudGameObject>();
        var TargetsEnemyList = new List<DalamudGameObject>();
        var OnScreenTargetsList = new List<DalamudGameObject>();

        if (Client.LocalPlayer == null)
            return new ObjectsList(TargetsList, CloseTargetsList, TargetsEnemyList, OnScreenTargetsList);

        // There might be a way to store this and just update the values if they actually change
        var device = Device.Instance();
        float deviceWidth = device->Width;
        float deviceHeight = device->Height;

        var PotentialTargets = Objects.Where(
            o => (ObjectKind.BattleNpc.Equals(o.ObjectKind)
                || ObjectKind.Player.Equals(o.ObjectKind))
            && o != Client.LocalPlayer
            && CanAttack(o)
        );

        var EnemyList = GetEnemyList();

        foreach (var obj in PotentialTargets)
        {
            // In the enemy list addon, adding it to the Enemy list
            if (EnemyList.Contains(obj.ObjectId))
                TargetsEnemyList.Add(obj);

            var o = (GameObject*)obj.Address;
            if (o == null) continue;

            if (o->GetIsTargetable() == false) continue;

            // If the object is part of another party's treasure hunt, we ignore it
            if (o->EventId.Type == EventHandlerType.TreasureHuntDirector && o->NamePlateIconId != 60094) continue;

            var distance = DistanceToPlayer(obj);

            // This is a bit less than the max distance to target something the vanilla way
            if (distance > 49) continue;

            /*
                * Check if object is visible on screen or not.
                * Using both WorldToScreenPoint and WorldToScreen because
                *  - the former is more accurate for actual X,Y position on screen
                *  - the latter returns whether or not the object is "in front" of the camera
                * This isn't exactly how I'd like to do it but since I couldn't find how to get
                * the "bounding box" of a game object or the dimensions of its model, this will have to do.
                */
            var screenPos = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera.WorldToScreenPoint(o->Position);
            if (screenPos.X < 0
                || screenPos.X > deviceWidth
                || screenPos.Y < 0
                || screenPos.Y > deviceHeight) continue;
            if (GameGui.WorldToScreen(o->Position, out _) == false) continue;

            // Check actual line of sight from camera to object (blocked by walls, etc)
            if (IsInLineOfSight(o, true) == false) continue;

            // On screen and in light of sight of the camera, adding it to the On Screen list
            OnScreenTargetsList.Add(obj);

            // Further than 40y, don't care about targeting it
            if (distance > 40) continue;

            // Default cone angle for very close targets, getting wider the closer the target is
            var angle = 140;
            if (distance > 15)
                angle = 75;
            else if (distance > 5)
                angle = 90;
            else
                // Close to the player, adding it to the Close targets list
                CloseTargetsList.Add(obj);

            if (IsInFrontOfCamera(obj, angle) == false) continue;

            // In front of the player, adding it to the default list
            TargetsList.Add(obj);
        }

        return new ObjectsList(TargetsList, CloseTargetsList, TargetsEnemyList, OnScreenTargetsList);
    }

    private static uint[] GetEnemyList()
    {
        var addonByName = GameGui.GetAddonByName("_EnemyList", 1);
        if (addonByName == IntPtr.Zero)
        {
            Log("EnemyList not found!");
            return Array.Empty<uint>();
        }

        var addon = (AddonEnemyList*)addonByName;
        var rap = CSFramework.Instance()->GetUiModule()->GetRaptureAtkModule();
        var numArray = rap->AtkModule.AtkArrayDataHolder.NumberArrays[21];
        var list = new List<uint>(addon->EnemyCount);
        for (var i = 0; i < addon->EnemyCount; i++)
        {
            var id = (uint)numArray->IntArray[8 + (i * 6)];
            list.Add(id);
        }
        return list.ToArray();
    }
}
