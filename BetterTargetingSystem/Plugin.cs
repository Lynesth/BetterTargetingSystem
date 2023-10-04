using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using BetterTargetingSystem.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using DalamudCharacter = Dalamud.Game.ClientState.Objects.Types.Character;
using DalamudGameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using CameraManager = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace BetterTargetingSystem;

public sealed unsafe class Plugin : IDalamudPlugin
{
    public string Name => "Better Targeting System";
    public string CommandConfig => "/bts";
    public string CommandHelp => "/btshelp";

    public IEnumerable<uint> LastConeTargets { get; private set; } = new List<uint>();
    public List<uint> CyclingTargets { get; private set; } = new List<uint>();

    private DalamudPluginInterface PluginInterface { get; init; }
    private ICommandManager CommandManager { get; init; }
    private IFramework Framework { get; init; }
    private IPluginLog PluginLog { get; init; }
    public Configuration Configuration { get; init; }

    public WindowSystem WindowSystem = new("BetterTargetingSystem");

    [PluginService] private static IClientState Client { get; set; } = null!;
    [PluginService] private static IObjectTable ObjectTable { get; set; } = null!;
    [PluginService] private static ITargetManager TargetManager { get; set; } = null!;
    [PluginService] private static IGameGui GameGui { get; set; } = null!;
    [PluginService] private static IGameInteropProvider GameInteropProvider { get; set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; set; } = null!;

    private ConfigWindow ConfigWindow { get; init; }
    private HelpWindow HelpWindow { get; init; }

    // Shamelessly stolen, not sure what that game function exactly does but it works
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3")]
    private CanAttackDelegate? CanAttackFunction = null!;
    private delegate nint CanAttackDelegate(nint a1, nint objectAddress);


    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] ICommandManager commandManager,
        [RequiredVersion("1.0")] IPluginLog pluginLog,
        [RequiredVersion("1.0")] IFramework framework)
    {
        GameInteropProvider.InitializeFromAttributes(this);

        this.PluginInterface = pluginInterface;
        this.CommandManager = commandManager;
        this.PluginLog = pluginLog;

        this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.Initialize(this.PluginInterface);

        Framework = framework;
        Framework.Update += Update;
        Client.TerritoryChanged += ClearLists;

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        HelpWindow = new HelpWindow(this);
        WindowSystem.AddWindow(HelpWindow);
        this.PluginInterface.UiBuilder.Draw += DrawUI;
        this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        this.CommandManager.AddHandler(CommandConfig, new CommandInfo(ShowConfigWindow)
            { HelpMessage = "Open the configuration window." });
        this.CommandManager.AddHandler(CommandHelp, new CommandInfo(ShowHelpWindow)
            { HelpMessage = "What does this plugin do?" });
    }

    public void Dispose()
    {
        Framework.Update -= Update;
        Client.TerritoryChanged -= ClearLists;
        this.CommandManager.RemoveHandler(CommandConfig);
        this.CommandManager.RemoveHandler(CommandHelp);
        this.WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        HelpWindow.Dispose();
    }

    public void Log(string message) => PluginLog.Debug(message);
    private void ShowConfigWindow(string command, string args) => this.DrawConfigUI();
    private void ShowHelpWindow(string command, string args) => HelpWindow.Toggle();
    private void DrawUI() => this.WindowSystem.Draw();
    public void DrawConfigUI() => ConfigWindow.Toggle();

    private RaptureAtkModule* RaptureAtkModule => CSFramework.Instance()->GetUiModule()->GetRaptureAtkModule();
    private bool IsTextInputActive => RaptureAtkModule->AtkModule.IsTextInputActive();

    public void ClearLists(ushort territoryType)
    {
        // Attempt to fix a very rare bug I can't reproduce
        this.LastConeTargets = new List<uint>();
        this.CyclingTargets = new List<uint>();
    }

    public void Update(IFramework framework)
    {
        if (Client.IsLoggedIn == false)
            return;
        
        if (IsTextInputActive || ImGuiNET.ImGui.GetIO().WantCaptureKeyboard)
            return;

        Keybinds.Keybind.GetKeyboardState();

        if (Configuration.TabTargetKeybind.IsPressed())
        {
            try { KeyState[(int)Configuration.TabTargetKeybind.Key!] = false; } catch { }
            NextTarget();
            return;
        }

        if (Configuration.ClosestTargetKeybind.IsPressed())
        {
            try { KeyState[(int)Configuration.ClosestTargetKeybind.Key!] = false; } catch { }
            TargetClosest();
            return;
        }

        // Don't allow extra keybinds in PvP
        if (Client.IsPvPExcludingDen)
            return;

        if (Configuration.LowestHealthTargetKeybind.IsPressed())
        {
            try { KeyState[(int)Configuration.LowestHealthTargetKeybind.Key!] = false; } catch { }
            TargetLowestHealth();
            return;
        }

        if (Configuration.BestAOETargetKeybind.IsPressed())
        {
            try { KeyState[(int)Configuration.BestAOETargetKeybind.Key!] = false; } catch { }
            TargetBestAOE();
            return;
        }
    }

    private void TargetLowestHealth() => TargetClosest(true);

    private void TargetClosest(bool lowestHealth = false)
    {
        if (Client.LocalPlayer == null)
            return;

        var (Targets, CloseTargets, EnemyListTargets, OnScreenTargets) = GetTargets();

        // All objects in Targets and CloseTargets are in OnScreenTargets so it's not necessary to test them
        if (EnemyListTargets.Count == 0 && OnScreenTargets.Count == 0)
            return;

        var _targets = OnScreenTargets.Count > 0 ? OnScreenTargets : EnemyListTargets;

        var _target = lowestHealth
            ? _targets.OrderBy(o => (o as DalamudCharacter)?.CurrentHp).ThenBy(o => DistanceBetweenObjects(Client.LocalPlayer, o)).First()
            : _targets.OrderBy(o => DistanceBetweenObjects(Client.LocalPlayer, o)).First();

        TargetManager.Target = _target;
    }

    private class AOETarget
    {
        public DalamudGameObject obj;
        public int inRange = 0;
        public AOETarget(DalamudGameObject obj) => this.obj = obj;
    }
    private void TargetBestAOE()
    {
        if (Client.LocalPlayer == null)
            return;

        var (Targets, CloseTargets, EnemyListTargets, OnScreenTargets) = GetTargets();

        if (OnScreenTargets.Count == 0)
            return;

        var groupManager = GroupManager.Instance();
        if (groupManager != null)
        {
            EnemyListTargets.AddRange(OnScreenTargets.Where(o =>
                EnemyListTargets.Contains(o) == false
                && ((o as DalamudCharacter)?.StatusFlags & StatusFlags.InCombat) != 0
                && groupManager->IsObjectIDInParty((uint)o.TargetObjectId)
            ));
        }

        if (EnemyListTargets.Count == 0)
            return;

        var AOETargetsList = new List<AOETarget>();
        foreach (var enemy in EnemyListTargets)
        {
            var AOETarget = new AOETarget(enemy);
            foreach (var other in EnemyListTargets)
            {
                if (other == enemy) continue;
                if (DistanceBetweenObjects(enemy, other) > 5) continue;
                AOETarget.inRange += 1;
            }
            AOETargetsList.Add(AOETarget);
        }

        var _targets = AOETargetsList.Where(o => OnScreenTargets.Contains(o.obj)).ToList();

        if (_targets.Count == 0)
            return;

        var _target = _targets.OrderByDescending(o => o.inRange).ThenByDescending(o => (o.obj as DalamudCharacter)?.CurrentHp).First().obj;

        TargetManager.Target = _target;
    }

    private void NextTarget()
    {
        if (Client.LocalPlayer == null)
            return;

        var (Targets, CloseTargets, EnemyListTargets, OnScreenTargets) = GetTargets();

        // All objects in Targets and CloseTargets are in OnScreenTargets so it's not necessary to test them
        if (EnemyListTargets.Count == 0 && OnScreenTargets.Count == 0)
            return;

        var _currentTarget = TargetManager.Target;
        var _previousTarget = TargetManager.PreviousTarget;
        var _targetObjectId = _currentTarget?.ObjectId ?? _previousTarget?.ObjectId ?? 0;

        // Targets in the frontal cone
        if (Targets.Count > 0)
        {
            Targets = Targets.OrderBy(o => DistanceBetweenObjects(Client.LocalPlayer, o)).ToList();

            var TargetsObjectIds = Targets.Select(o => o.ObjectId);
            // Same cone targets as last cycle
            if (this.LastConeTargets.ToHashSet().SetEquals(TargetsObjectIds.ToHashSet()))
            {
                // Add the close targets to the list of potential targets
                var _potentialTargets = Targets.UnionBy(CloseTargets, o => o.ObjectId).ToList();
                var _potentialTargetsObjectIds = _potentialTargets.Select(o => o.ObjectId);

                // New enemies to be added
                if (_potentialTargetsObjectIds.Any(o => this.CyclingTargets.Contains(o) == false))
                    this.CyclingTargets = this.CyclingTargets.Union(_potentialTargetsObjectIds).ToList();

                // We simply select the next target
                this.CyclingTargets = this.CyclingTargets.Intersect(_potentialTargetsObjectIds).ToList();
                var index = this.CyclingTargets.FindIndex(o => o == _targetObjectId);
                if (index == this.CyclingTargets.Count - 1) index = -1;
                TargetManager.Target = _potentialTargets.Find(o => o.ObjectId == this.CyclingTargets[index + 1]);
            }
            else
            {
                var _potentialTargets = Targets;
                var _potentialTargetsObjectIds = _potentialTargets.Select(o => o.ObjectId).ToList();
                var index = _potentialTargetsObjectIds.FindIndex(o => o == _targetObjectId);
                if (index == _potentialTargetsObjectIds.Count - 1) index = -1;
                TargetManager.Target = _potentialTargets.Find(o => o.ObjectId == _potentialTargetsObjectIds[index + 1]);

                this.LastConeTargets = TargetsObjectIds;
                this.CyclingTargets = _potentialTargetsObjectIds;
            }

            return;
        }

        if (CloseTargets.Count > 0)
        {
            var _potentialTargetsObjectIds = CloseTargets.Select(o => o.ObjectId);

            if (_potentialTargetsObjectIds.Any(o => this.CyclingTargets.Contains(o) == false))
                this.CyclingTargets = this.CyclingTargets.Union(_potentialTargetsObjectIds).ToList();

            // We simply select the next target
            this.CyclingTargets = this.CyclingTargets.Intersect(_potentialTargetsObjectIds).ToList();
            var index = this.CyclingTargets.FindIndex(o => o == _targetObjectId);
            if (index == this.CyclingTargets.Count - 1) index = -1;
            TargetManager.Target = CloseTargets.Find(o => o.ObjectId == this.CyclingTargets[index + 1]);

            return;
        }

        if (EnemyListTargets.Count > 0)
        {
            var _potentialTargetsObjectIds = EnemyListTargets.Select(o => o.ObjectId);

            if (_potentialTargetsObjectIds.Any(o => this.CyclingTargets.Contains(o) == false))
                this.CyclingTargets = this.CyclingTargets.Union(_potentialTargetsObjectIds).ToList();

            // We simply select the next target
            this.CyclingTargets = this.CyclingTargets.Intersect(_potentialTargetsObjectIds).ToList();
            var index = this.CyclingTargets.FindIndex(o => o == _targetObjectId);
            if (index == this.CyclingTargets.Count - 1) index = -1;
            TargetManager.Target = EnemyListTargets.Find(o => o.ObjectId == this.CyclingTargets[index + 1]);

            return;
        }

        if (OnScreenTargets.Count > 0)
        {
            OnScreenTargets = OnScreenTargets.OrderBy(o => DistanceBetweenObjects(Client.LocalPlayer, o)).ToList();
            var _potentialTargetsObjectIds = OnScreenTargets.Select(o => o.ObjectId);

            if (_potentialTargetsObjectIds.Any(o => this.CyclingTargets.Contains(o) == false))
                this.CyclingTargets = this.CyclingTargets.Union(_potentialTargetsObjectIds).ToList();

            // We simply select the next target
            this.CyclingTargets = this.CyclingTargets.Intersect(_potentialTargetsObjectIds).ToList();
            var index = this.CyclingTargets.FindIndex(o => o == _targetObjectId);
            if (index == this.CyclingTargets.Count - 1) index = -1;
            TargetManager.Target = OnScreenTargets.Find(o => o.ObjectId == this.CyclingTargets[index + 1]);
        }
    }

    private bool CanAttack(DalamudGameObject obj)
    {
        return CanAttackFunction?.Invoke(142, obj.Address) == 1;
    }

    internal float DistanceBetweenObjects(DalamudGameObject source, DalamudGameObject target)
    {
        // Might have to tinker a bit whether or not to include hitbox radius in calculation
        // Keeping the source object hitbox radius outside of the calculation for now
        var distance = Vector3.Distance(source.Position, target.Position);
        //distance -= source.HitboxRadius;
        distance -= target.HitboxRadius;
        return distance;
    }

    internal bool IsInFrontOfCamera(DalamudGameObject obj, int maxAngle)
    {
        // This is still relying on camera orientation but the cone is from the player's position
        if (Client.LocalPlayer == null)
            return false;

        // Gives the camera rotation in deg between -180 and 180
        var cameraRotation = RaptureAtkModule->AtkModule.AtkArrayDataHolder.NumberArrays[24]->IntArray[3];

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

        var Player = Client.LocalPlayer != null ? (GameObject*)Client.LocalPlayer.Address : null;
        if (Player == null)
            return new ObjectsList(TargetsList, CloseTargetsList, TargetsEnemyList, OnScreenTargetsList);

        // There might be a way to store this and just update the values if they actually change
        var device = Device.Instance();
        float deviceWidth = device->Width;
        float deviceHeight = device->Height;

        var PotentialTargets = ObjectTable.Where(
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

            // If the object is part of another party's treasure hunt/leve, we ignore it
            if ((o->EventId.Type == EventHandlerType.TreasureHuntDirector || o->EventId.Type == EventHandlerType.BattleLeveDirector)
                && o->EventId.Id != Player->EventId.Id)
                continue;

            var distance = DistanceBetweenObjects(Client.LocalPlayer!, obj);

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

    private uint[] GetEnemyList()
    {
        if (Client.IsPvPExcludingDen)
            return Array.Empty<uint>();

        var addonByName = GameGui.GetAddonByName("_EnemyList", 1);
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
