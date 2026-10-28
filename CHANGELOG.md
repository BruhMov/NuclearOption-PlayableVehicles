# Changelog

## 1.0.3

- **Multiplayer protocol 6: the host and everyone who drives must update together.**
- **Buy vehicles at your own airbases**, not only at vehicle depots. On the spawn screen in GROUND mode your ground
  airbases are shown and can be selected, F7 works with an airbase selected on the map, and the arrows in the shop
  cycle through airbases and depots. The vehicle appears on free ground near the control tower, away from runways and
  taxiways. Carriers are not purchase points.
- **Sell vehicles you bought.** Bring the vehicle to a friendly depot (within 200 m) or airbase (within 800 m of the
  tower), stop and get out, open the shop there and press SELL VEHICLE: the shop switches to your vehicles at that
  point, the camera shows the selected one, and SELL returns money. You get back the purchase price reduced by hull
  damage and spent ammunition; an undamaged vehicle with full ammo returns the full price.
- **Target lock range** now matches the vehicle's longest-range weapon (at least 10 km), so long-range SAMs can lock
  targets as far as their missiles reach.
- Compatibility with the game update of 2026-10-28 (map marker colours of your own vehicles).

## 1.0.2

- **Multiplayer (protocol 5, host and clients must update together)**: the host now measures each driver's ping and
  places their vehicle ahead by the delivery delay, so AI aims at and hits where the vehicle really is on the
  driver's screen. When the host registers a bullet hit on a client's vehicle, the driver sees the impact effect at
  that spot even if their local copy of the bullet missed.
- **Multiplayer: weapon panel and crosshair for clients.** The host now sends the driver the real state of their
  weapons: rounds loaded, total ammo and reloading. Before, a client saw a full magazine that never emptied, ammo
  draining from the reserve and no reload bar.
- **Multiplayer: clients rearm at bunkers, ammo containers and supply trucks.** The host takes the vehicle's height
  above the ground from the client's own physics, so a stopped client vehicle counts as standing on the ground.
- **Laser point defence** (the Spearhead MBT's secondary laser turret) shoots down incoming missiles on its own, like
  on AI vehicles, until you select its weapon station.

## 1.0.0

First public release.

- **Drive your faction's ground vehicles** in third person: tanks, IFVs, trucks, air defence, artillery and missile
  launchers. Driving uses a port of the game's own AI vehicle physics; turrets aim with the mouse, weapon stations
  switch with the game's weapon keys.
- **Buy vehicles at vehicle depots** at the game's prices, from the map (F7) or from the GROUND mode of the spawn
  screen, with a menu styled like the aircraft selection menu. Nuclear launchers follow the game's escalation and
  rank rules.
- **HUD built from the game's own HUD elements**: compass, speed and range boxes, turret crosshair with reload ring,
  weapon panels, hull integrity, target lock, missile warning with alarms, hit direction, minimap.
- **Artillery and missiles**: fire control computer with target selection on the map, direct and indirect fire,
  ballistic and air-defence missiles with the game's radar link rules.
- **Fits into the campaign**: capture airbases, resupply vehicles with a supply truck for the game's reward, earn
  money and rank for kills, get credit for kills by installations you bought.
- **Towing and cargo**: tow wheeled launchers with a tractor, carry containers on a flatbed truck.
- **Multiplayer**: clients drive without input lag, the host validates their input; the host and everyone who drives
  need the mod (same version). Server option to let players drive only their own vehicles.
- Configurable keys, dead zones, driving model, camera and camera shake in
  `BepInEx\config\PlayableVehicles.cfg`.
