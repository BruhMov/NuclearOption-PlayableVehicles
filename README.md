# Playable Ground Vehicles for Nuclear Option

*Русская версия: [README.ru.md](README.ru.md)*

Nuclear Option has hundreds of vehicles fighting on the ground, yet you only ever see them from a cockpit. This mod
puts you behind the wheel: tanks, IFVs, SAM launchers, artillery, supply trucks and tractors. Buy a vehicle at one of
your faction's depots or airbases and drive it straight into the fight, in single player or online.

The goal is for the mod to feel like part of the game. The HUD is assembled from the game's own HUD pieces, prices
come from the game, and kills, base captures and resupply are rewarded by the same rules that apply to pilots.

> This is an unofficial mod. It is not affiliated with the developers of Nuclear Option.

## Features

- **Buy vehicles** at your faction's depots and airbases for the in-game price, and sell them back when you're done.
  Order one from the tactical map without leaving your aircraft, or pick GROUND on the spawn screen and start the
  match on the ground.
- **Drive** in third person. The handling follows the same physics the AI vehicles use: suspension, grip, and more
  speed on roads than off them.
- **Fight** with every weapon the vehicle has: cannons, machine guns, ATGMs, SAMs. Lock targets, and the reticle with
  its reload ring shows where your shot will land.
- **Artillery and missile launchers.** A fire control computer works out direct or indirect fire, and you pick the
  target on the map. Ballistic and anti-air missiles are both supported.
- **Capture bases**: drive onto an enemy or neutral airfield and watch the capture progress.
- **Resupply**: take a supply truck to vehicles that asked for ammo and get paid for it, just like a transport pilot.
- **Towing and cargo**: a tractor can tow wheeled launchers, a flatbed truck can carry containers.
- **Missile warnings**, hit direction indicators, friend/foe markers and a minimap.

## Installation

1. Install BepInEx 5. The easiest way is the ready-made pack for Nuclear Option on
   [Thunderstore](https://thunderstore.io/c/nuclear-option/p/BepInEx/BepInExPack/), just follow the steps on its page.
   Regular [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for Windows x64 works too.
2. Launch the game once and close it, so BepInEx can create its folders.
3. Download `PlayableVehicles.dll` from the
   [Releases](https://github.com/Rap3/NuclearOption-PlayableVehicles/releases) page.
4. Put it into the `BepInEx\plugins` folder inside your game folder. The quickest way to open the game folder is
   through Steam: right-click the game in your library, then Manage → Browse local files.
5. Start the game. If everything worked, vehicle depots get a marker on the map and the spawn screen shows an
   AIR / GROUND switch.

**To uninstall**, delete `BepInEx\plugins\PlayableVehicles.dll`.

## Controls

| Key | Action |
|---|---|
| F7 | Get in the selected vehicle, or get out |
| F7 with your own depot or airbase selected on the map | Open the vehicle shop |
| W A S D | Throttle, brake and reverse, steering |
| Mouse | Look around and aim the turret |
| Fire (LMB) | Fire the selected weapon |
| RMB | Gun sight |
| Mouse wheel | Camera distance |
| Tab, 1 / 2 / 3 | Switch weapons |
| Target Select / Target Cancel | Lock the target under the reticle / drop the lock |
| X | Deploy or stow (artillery outriggers, radar masts); on a tractor, hitch or unhitch a trailer; on a flatbed, load or unload a container |
| B | Fire control computer (deployed artillery and missile launchers) |

There are three ways to pick a vehicle to get into: press Target Select on it from your aircraft, click it on the
tactical map (M), or simply point the camera at it. When you get out, the camera goes back to your aircraft, or to
the spawn screen if you don't have one.

Fire, target lock, weapon switching and camera controls go through the game's own control settings, so your key
bindings and mouse sensitivity carry over. Gamepads work too: bind a stick to Move Forward/Back and Move Left/Right in
the game's controls menu. The mod's keys are ignored while the chat is open.

## Buying vehicles

Your faction's vehicle depots are marked on the tactical map. Select one, or one of your ground airbases, and press
F7. A shop opens that looks like
the aircraft selection screen: vehicles are grouped by type, and each one shows its price, weapons and description.
DEPLOY buys the vehicle and puts you in it right away. If you came in an aircraft, the autopilot keeps it flying
meanwhile. The arrows next to the base name switch between your depots and airbases. At an airbase the vehicle
appears on free ground near the control tower, away from the runways.

No aircraft? Switch the spawn screen to GROUND, pick a depot or an airbase on the map and open the shop with the
button at the bottom of the screen.

To sell a vehicle you bought, park it at a friendly depot (within 200 m) or airbase (within 800 m of the tower), get
out and open the shop there. SELL VEHICLE shows your vehicles at that point; pick one and press SELL. You get back
the purchase price reduced by hull damage and spent ammunition. BACK returns to buying.

A few rules:
- A vehicle you buy belongs to you: you get the reward for anything its guns destroy, even while you're driving
  something else.
- There's a short cooldown after each purchase, between 10 and 25 seconds depending on the vehicle type.
- Nuclear missile launchers unlock under the same escalation and rank rules as nuclear weapons for aircraft.
- If the mission doesn't allow donations to your faction, the shop is closed too.

## Artillery and missiles

Deploy the vehicle with X and open the fire control computer with B. A fire control window appears on the right and
the map on the left. Choose a target by clicking it on the map or by locking it, then pick direct or indirect fire.
The window shows the range, elevation and time of flight. Press ENGAGE and the turret lays itself onto the target;
you fire with the normal fire button. CEASE FIRE cancels the mission.

Missile launchers work the same way. Ballistic missiles need a ground target within range. Anti-air missiles need an
air target, and semi-active radar missiles also need a radar nearby: either the vehicle's own or a friendly one
within 300 metres.

## Towing and cargo

In the base game, emplacements can only be moved by helicopter. The mod lets you move them by road:

- **Trailers** (HLT-CRAM, HLT-HEL). Back a tractor up to the trailer's tow bar and press X. The trailer raises its
  legs and follows you. Press X again to unhitch; after a couple of seconds standing still it deploys on its own.
- **Containers** (Radar Container, Boltstrike RAM45 and StratoLance R9 launchers). Stop a flatbed truck next to the
  container and press X to load it. Press X again to drop it on the ground behind the truck.

> Towing is still rough. Trailers can misbehave on slopes and bumpy ground, and the hitch may break or fail to
> connect on the first try. If you run into trouble, please report it in
> [Issues](https://github.com/Rap3/NuclearOption-PlayableVehicles/issues).

## Resupply

When a vehicle drops below half its ammo, it asks for resupply. Park next to a friendly supply truck or ammo depot
and it will be topped up. An idle AI supply truck may also come to you.

You can drive a supply truck yourself as well. Nearby requests are listed on screen; bring ammo to a vehicle that is
standing still and you get the reward.

## Multiplayer

The host and everyone who wants to drive need the mod, and all on the same version. If the versions don't match,
the mod tells you who needs to update. Players without the mod can still play on the server, they just can't drive.

The host can restrict players to vehicles they bought themselves: set `VehicleAccess = OwnVehicles` in the
`[Server]` section of the mod's settings file.

## Settings

After the first launch, the file `BepInEx\config\PlayableVehicles.cfg` appears in your game folder.
Every setting has a short description inside the file; here is the full list. Edit the file while the game is closed.
Keys use Unity key names: `F7`, `X`, `UpArrow`, `Keypad5` and so on.

**[Controls]**

| Setting | Default | What it does |
|---|---|---|
| ToggleKey | F7 | Get in the selected vehicle or get out. With your own depot or airbase selected on the map, opens the vehicle shop |
| DeployKey | X | Deploy or stow the vehicle (only while standing still). On a tractor it hitches and unhitches a trailer, on a flatbed it loads and unloads a container |
| FireControlKey | B | Fire control computer for deployed artillery and missile launchers |
| ForwardKey, ReverseKey, LeftKey, RightKey | W, S, A, D | Throttle, brake and reverse, steering |
| StickDeadZone | 0.15 | Gamepad stick dead zone for throttle and steering, as a fraction of the stick travel |
| GameViewControls | true | The camera uses the game's own view controls, with its sensitivity and inversion. `false` switches to the mod's own mouse look (sensitivity is set in `[Camera]`) |

**[Server]** (applies on the host and in single player)

| Setting | Default | What it does |
|---|---|---|
| VehicleAccess | Open | `Open`: players can get into any vehicle of their faction. `OwnVehicles`: only into vehicles they bought. Vehicles a player brought in by helicopter or dropped from an aircraft stay open to the whole faction |
| HostFollowsAccess | true | The VehicleAccess rule also applies to the host. `false` lets the host get into anything |
| PurchaseCooldownLight | 10 | Cooldown after buying a light vehicle (trucks, light vehicles, APCs and IFVs, drones), seconds |
| PurchaseCooldownAirDefense | 15 | Cooldown after buying air defence (AA guns, SAM launchers, radars), seconds |
| PurchaseCooldownTanks | 25 | Cooldown after buying a tank, seconds |
| PurchaseCooldownArtillery | 25 | Cooldown after buying artillery, rocket artillery included, seconds |

**[Aircraft]**

| Setting | Default | What it does |
|---|---|---|
| Autopilot | true | While you're in a vehicle, your aircraft circles where you left it and a helicopter hovers. `false` leaves the controls centred |

**[Aiming]**

| Setting | Default | What it does |
|---|---|---|
| BallisticComputer | true | The turret allows for shell drop, so the shot lands where the crosshair points. `false`: the barrel points straight at the crosshair and you correct for range yourself using the barrel marker |
| LockAutoTrack | true | The turret tracks a locked target by itself, with lead. `false`: the turret follows the crosshair and the lead point is shown on screen |
| LockScreenRadius | 0.08 | How far from the screen centre Target Select looks for a target, as a fraction of the screen height |

**[Driving]**

| Setting | Default | What it does |
|---|---|---|
| VanillaPhysics | true | AI vehicle physics: suspension, grip, each vehicle's own acceleration and top speed. `false` switches to simplified arcade driving |
| TurnRate | 38 | Hull turn rate at full steering, degrees per second. Capped at 57 with AI physics |
| Acceleration | 14 | Arcade driving only: acceleration and braking, m/s per second |
| ThrottleDeadzone | 0.1 | Gamepad throttle below this fraction counts as zero and the vehicle brakes |
| FullSteeringSpeed | 4 | Wheeled vehicles: speed (m/s) at which steering reaches full strength. Wheeled vehicles can't turn on the spot |
| ReverseSpeedFactor | 0.45 | Reverse speed as a fraction of the vehicle's top speed |

**[Camera]**

| Setting | Default | What it does |
|---|---|---|
| MouseSensitivityX, MouseSensitivityY | 2.2, 1.4 | Horizontal and vertical mouse sensitivity. Only used with `GameViewControls = false` |
| Distance | 11.5 | Camera distance from the vehicle when you get in, metres |
| MaxDistance | 25 | How far the mouse wheel can pull the camera back, metres |
| ZoomFov | 6 | Field of view in the gun sight, degrees. Lower means more zoom |
| ShakeScale | 1 | Camera shake from shots, hits and nearby explosions. 0 turns it off, 2 is the maximum |
| MaxLookUp | 80 | How high you can look above the horizon, degrees. The barrel still can't go past the turret's own limit |

**[Overlay]**

| Setting | Default | What it does |
|---|---|---|
| ShowOverlay | true | Show the vehicle HUD. Only worth turning off when troubleshooting |
| MinimapLookAhead | 2000 | How many metres ahead of the vehicle the minimap is centred. 0 keeps the vehicle in the middle |
| UnitMarkerScale | 1 | Size of friend/foe markers compared to the aircraft HUD |
| FriendlyIconRange | 500 | Friendly units closer than this get a full icon, further ones a dot, metres |
| EnemyIconRange | 3000 | Enemy units closer than this get a full icon, further ones a small marker, metres |

## Known issues

- **Multiplayer has had limited testing.** It was tested in a PvE co-op session with a couple of clients. It hasn't
  been tested on dedicated servers or with more players, so desync and other network problems can't be ruled out.
- **Towing is rough** (see above), especially in multiplayer: for a client, the trailer lags behind the tractor by
  the ping.
- A client's shots are handled by the host, so with high ping they go off with a delay.
- Lead on moving targets is only calculated for a locked target.
- Static emplacements don't drive on their own, same as in the base game. You can only control their turret; move
  them by helicopter, tractor or truck.

## Feedback

Found a bug or have an idea? Open an [issue](https://github.com/Rap3/NuclearOption-PlayableVehicles/issues). If
something went wrong in game, attach `BepInEx\LogOutput.log` from your game folder, it helps a lot.

## License

[MIT](LICENSE). Nuclear Option and all its content belong to their respective owners; the mod contains no game files.
