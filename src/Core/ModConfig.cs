using BepInEx.Configuration;
using UnityEngine;

namespace PlayableVehicles
{
    internal enum VehicleAccessMode
    {
        Open,
        OwnVehicles,
    }

    internal static class ModConfig
    {
        public static ConfigEntry<KeyCode> ToggleKey { get; private set; }
        public static ConfigEntry<KeyCode> DeployKey { get; private set; }
        public static ConfigEntry<KeyCode> FireControlKey { get; private set; }
        public static ConfigEntry<KeyCode> ForwardKey { get; private set; }
        public static ConfigEntry<KeyCode> ReverseKey { get; private set; }
        public static ConfigEntry<KeyCode> LeftKey { get; private set; }
        public static ConfigEntry<KeyCode> RightKey { get; private set; }
        public static ConfigEntry<float> StickDeadZone { get; private set; }
        public static ConfigEntry<bool> GameViewControls { get; private set; }

        public static ConfigEntry<VehicleAccessMode> VehicleAccess { get; private set; }
        public static ConfigEntry<bool> HostFollowsAccess { get; private set; }
        public static ConfigEntry<float> CooldownLight { get; private set; }
        public static ConfigEntry<float> CooldownAirDefense { get; private set; }
        public static ConfigEntry<float> CooldownTanks { get; private set; }
        public static ConfigEntry<float> CooldownArtillery { get; private set; }

        public static ConfigEntry<bool> DebugLogging { get; private set; }

        public static ConfigEntry<bool> AircraftAutopilot { get; private set; }
        public static ConfigEntry<bool> BallisticComputer { get; private set; }
        public static ConfigEntry<bool> LockAutoTrack { get; private set; }
        public static ConfigEntry<float> LockScreenRadius { get; private set; }

        public static ConfigEntry<bool> VanillaPhysics { get; private set; }
        public static ConfigEntry<float> TurnRate { get; private set; }
        public static ConfigEntry<float> Acceleration { get; private set; }
        public static ConfigEntry<float> ThrottleDeadzone { get; private set; }
        public static ConfigEntry<float> FullSteeringSpeed { get; private set; }
        public static ConfigEntry<float> ReverseSpeedFactor { get; private set; }

        public static ConfigEntry<float> MouseSensitivityX { get; private set; }
        public static ConfigEntry<float> MouseSensitivityY { get; private set; }
        public static ConfigEntry<float> CameraDistance { get; private set; }
        public static ConfigEntry<float> CameraMaxDistance { get; private set; }
        public static ConfigEntry<float> ZoomFov { get; private set; }
        public static ConfigEntry<float> ShakeScale { get; private set; }
        public static ConfigEntry<float> CameraMaxLookUp { get; private set; }

        public static ConfigEntry<bool> ShowOverlay { get; private set; }
        public static ConfigEntry<float> MinimapLookAhead { get; private set; }
        public static ConfigEntry<float> UnitMarkerScale { get; private set; }
        public static ConfigEntry<float> FriendlyIconRange { get; private set; }
        public static ConfigEntry<float> EnemyIconRange { get; private set; }

        public static void Bind(ConfigFile config)
        {
            ToggleKey = config.Bind("Controls", "ToggleKey", KeyCode.F7,
                "Get in the selected vehicle or get out. With your own vehicle depot selected on the map, opens the vehicle shop");
            DeployKey = config.Bind("Controls", "DeployKey", KeyCode.X,
                "Deploy or stow the vehicle (artillery outriggers, radar masts), only while standing still; a deployed vehicle " +
                "can't move. On a tractor it hitches or unhitches a trailer, on a flatbed truck it loads or unloads a container");
            FireControlKey = config.Bind("Controls", "FireControlKey", KeyCode.B,
                "Fire control computer for deployed artillery and missile launchers: pick a target on the map or with " +
                "Target Select, choose direct or indirect fire, the turret aims by itself and you fire with the normal Fire button");
            ForwardKey = config.Bind("Controls", "ForwardKey", KeyCode.W, "Throttle");
            ReverseKey = config.Bind("Controls", "ReverseKey", KeyCode.S, "Brake / reverse");
            LeftKey = config.Bind("Controls", "LeftKey", KeyCode.A, "Steer left");
            RightKey = config.Bind("Controls", "RightKey", KeyCode.D, "Steer right");
            StickDeadZone = config.Bind("Controls", "StickDeadZone", 0.15f, new ConfigDescription(
                "Gamepad/joystick dead zone for throttle and steering (the game's Move Forward/Back and Move Left/Right " +
                "bindings), as a fraction of the axis travel. Keys always give full throttle and steering",
                new AcceptableValueRange<float>(0f, 0.9f)));
            GameViewControls = config.Bind("Controls", "GameViewControls", true,
                "Use the game's camera controls in vehicles: Pan View / Tilt View (mouse, numpad, gamepad), Zoom View " +
                "(mouse wheel), Free Look for the gun sight (RMB), with the sensitivity and inversion from the game's control " +
                "settings. false - the mod's own mouse look (sensitivity from MouseSensitivityX/Y in [Camera], gun sight on RMB)");

            VehicleAccess = config.Bind("Server", "VehicleAccess", VehicleAccessMode.Open,
                "Who may get into which vehicle. Set by the host (ignored on clients) or in single player. " +
                "Open - any vehicle of your faction. OwnVehicles - only vehicles you bought at a depot; vehicles brought " +
                "in by a player (helicopter sling, dropped from an aircraft) stay open to the whole faction");
            HostFollowsAccess = config.Bind("Server", "HostFollowsAccess", true,
                "VehicleAccess also applies to the host (and in single player). false - the host may get into any vehicle of their faction");

            var cooldownRange = new AcceptableValueRange<float>(0f, 600f);
            CooldownLight = config.Bind("Server", "PurchaseCooldownLight", 10f, new ConfigDescription(
                "Cooldown after buying a light vehicle (trucks, light vehicles, APCs/IFVs, drones), seconds", cooldownRange));
            CooldownAirDefense = config.Bind("Server", "PurchaseCooldownAirDefense", 15f, new ConfigDescription(
                "Cooldown after buying air defence (AA guns, SAM launchers, radars), seconds", cooldownRange));
            CooldownTanks = config.Bind("Server", "PurchaseCooldownTanks", 25f, new ConfigDescription(
                "Cooldown after buying a tank, seconds", cooldownRange));
            CooldownArtillery = config.Bind("Server", "PurchaseCooldownArtillery", 25f, new ConfigDescription(
                "Cooldown after buying artillery (including rocket artillery), seconds", cooldownRange));

            AircraftAutopilot = config.Bind("Aircraft", "Autopilot", true,
                "While you are in a vehicle, the game's autopilot flies your aircraft: it circles where you left it " +
                "(a helicopter hovers) and holds its height above the terrain. false - the controls are left centred");

            DebugLogging = config.Bind("General", "DebugLogging", false,
                "Verbose diagnostics in BepInEx LogOutput.log: every air-target check logs the target's real type, " +
                "radarAlt and world Y. Needed to debug 'Not an air target' rejects; throttled, safe to leave on while testing");

            BallisticComputer = config.Bind("Aiming", "BallisticComputer", true,
                "The turret raises the barrel to allow for shell drop, so the shot lands where the crosshair points " +
                "(range is taken along the sight line, like the game's AI turrets). false - the barrel points straight at " +
                "the crosshair and you correct for range yourself using the barrel marker, which shows the impact point");

            LockAutoTrack = config.Bind("Aiming", "LockAutoTrack", true,
                "The turret follows a locked target by itself, with lead. false - the turret follows the crosshair and the " +
                "lead point is shown in the HUD for manual aiming");
            LockScreenRadius = config.Bind("Aiming", "LockScreenRadius", 0.08f,
                new ConfigDescription("How far from the screen centre Target Select looks for a target, as a fraction of the screen height",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            VanillaPhysics = config.Bind("Driving", "VanillaPhysics", true,
                "Drive with the same physics as AI vehicles: suspension, grip, and each vehicle's own acceleration and top speed " +
                "on and off road. false - simplified arcade driving (TurnRate and Acceleration below)");
            TurnRate = config.Bind("Driving", "TurnRate", 38f,
                new ConfigDescription("Hull turn rate at full steering, degrees per second (with vanilla physics capped at 57, the AI limit)", new AcceptableValueRange<float>(5f, 180f)));
            Acceleration = config.Bind("Driving", "Acceleration", 14f,
                new ConfigDescription("Arcade physics only: acceleration and braking, m/s per second", new AcceptableValueRange<float>(1f, 60f)));
            ThrottleDeadzone = config.Bind("Driving", "ThrottleDeadzone", 0.1f,
                new ConfigDescription("Vanilla physics: throttle below this fraction (gamepad) counts as no throttle and the vehicle brakes like an AI " +
                    "vehicle would. Partial throttle above it doesn't brake; throttle against the direction of travel brakes", new AcceptableValueRange<float>(0f, 0.5f)));
            FullSteeringSpeed = config.Bind("Driving", "FullSteeringSpeed", 4f,
                new ConfigDescription("Vanilla physics, wheeled vehicles (not tanks): full steering from this speed, weaker below it, " +
                    "none when standing still, m/s", new AcceptableValueRange<float>(0.5f, 20f)));
            ReverseSpeedFactor = config.Bind("Driving", "ReverseSpeedFactor", 0.45f,
                new ConfigDescription("Vanilla physics: top reverse speed as a fraction of the vehicle's top speed",
                    new AcceptableValueRange<float>(0.1f, 1f)));

            MouseSensitivityX = config.Bind("Camera", "MouseSensitivityX", 2.2f,
                new ConfigDescription("Horizontal camera turn per unit of mouse movement, degrees. Only with [Controls] " +
                    "GameViewControls = false (otherwise the game's view sensitivity is used)", new AcceptableValueRange<float>(0.1f, 20f)));
            MouseSensitivityY = config.Bind("Camera", "MouseSensitivityY", 1.4f,
                new ConfigDescription("Vertical camera turn per unit of mouse movement, degrees. Only with [Controls] " +
                    "GameViewControls = false", new AcceptableValueRange<float>(0.1f, 20f)));
            CameraDistance = config.Bind("Camera", "Distance", 11.5f,
                new ConfigDescription("Starting camera distance from the vehicle, metres", new AcceptableValueRange<float>(3.5f, 60f)));
            CameraMaxDistance = config.Bind("Camera", "MaxDistance", 25f,
                new ConfigDescription("Furthest the mouse wheel can pull the camera back, metres", new AcceptableValueRange<float>(3.5f, 100f)));
            ZoomFov = config.Bind("Camera", "ZoomFov", 6f,
                new ConfigDescription("Field of view in the gun sight (RMB), degrees", new AcceptableValueRange<float>(2f, 40f)));
            ShakeScale = config.Bind("Camera", "ShakeScale", 1f,
                new ConfigDescription("Camera shake from shots, hits and nearby explosions (0 - no shake)",
                    new AcceptableValueRange<float>(0f, 2f)));
            CameraMaxLookUp = config.Bind("Camera", "MaxLookUp", 80f,
                new ConfigDescription("How high you can look (and aim) above the horizon, degrees. " +
                    "The turret's own elevation limit still applies", new AcceptableValueRange<float>(10f, 89f)));

            ShowOverlay = config.Bind("Overlay", "ShowOverlay", true,
                "Show the vehicle HUD (crosshair, weapons, status, compass, gun sight). Turn it off only to troubleshoot");
            MinimapLookAhead = config.Bind("Overlay", "MinimapLookAhead", 2000f,
                new ConfigDescription("How far ahead of the vehicle the minimap is centred, metres (aircraft use 4000). 0 - the vehicle is in the centre",
                    new AcceptableValueRange<float>(0f, 4000f)));
            UnitMarkerScale = config.Bind("Overlay", "UnitMarkerScale", 1f,
                new ConfigDescription("Size of friend/foe markers compared to the aircraft HUD (1 = same as in the game)",
                    new AcceptableValueRange<float>(0.2f, 2f)));
            FriendlyIconRange = config.Bind("Overlay", "FriendlyIconRange", 500f,
                new ConfigDescription("Friendly units closer than this are shown with a full icon, further ones as a dot, metres (aircraft always get an icon)",
                    new AcceptableValueRange<float>(0f, 20000f)));
            EnemyIconRange = config.Bind("Overlay", "EnemyIconRange", 3000f,
                new ConfigDescription("Enemy units closer than this are shown with a full icon, further ones as a small marker, metres",
                    new AcceptableValueRange<float>(0f, 20000f)));
        }
    }
}
