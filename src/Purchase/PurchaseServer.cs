using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mirage;
using Channel = Mirage.Channel;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class PurchaseServer
    {
        private const float GateClearDistance = 25f;
        private const float BusyTimeout = 30f;

        private sealed class GateLock
        {
            public GroundVehicle Vehicle;
            public float Time;
        }

        private static readonly Dictionary<(ulong, VehicleShop.CooldownGroup), float> _nextPurchaseTime =
            new Dictionary<(ulong, VehicleShop.CooldownGroup), float>();
        private static readonly Dictionary<VehicleDepot, GateLock> _gates = new Dictionary<VehicleDepot, GateLock>();
        private static readonly Dictionary<GroundVehicle, Player> _buyers = new Dictionary<GroundVehicle, Player>();
        private static readonly List<GroundVehicle> _deadBuyers = new List<GroundVehicle>();

        public static bool TryGetBuyer(GroundVehicle vehicle, out Player buyer)
        {
            return _buyers.TryGetValue(vehicle, out buyer);
        }

        public static void OnRequest(INetworkPlayer connection, PvPurchaseRequest message)
        {
            Player player = connection.Identity != null ? connection.Identity.GetComponent<Player>() : null;
            VehicleDepot depot = UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = message.DepotId }), out Unit unit) ? unit as VehicleDepot : null;
            VehicleShop.TryGet(message.VehicleIndex, out VehicleDefinition definition);

            VehicleShop.Result result = TryPurchase(player, depot, definition, message.Deploy, out GroundVehicle vehicle, out float wait);
            connection.Send(new PvPurchaseReply
            {
                Result = result,
                VehicleIndex = message.VehicleIndex,
                VehicleId = vehicle != null ? vehicle.persistentID.Id : 0u,
                Wait = wait,
            }, Channel.Reliable);
        }

        public static VehicleShop.Result TryPurchase(Player player, VehicleDepot depot, VehicleDefinition definition, bool deploy,
            out GroundVehicle vehicle, out float wait)
        {
            vehicle = null;
            wait = 0f;
            if (!NetBridge.IsServer) return VehicleShop.Result.Unavailable;

            VehicleShop.Result result = VehicleShop.Check(player, depot, definition);
            if (result != VehicleShop.Result.Purchased) return result;

            float now = Time.time;
            var cooldownKey = (PlayerKey(player), VehicleShop.Group(definition));
            if (_nextPurchaseTime.TryGetValue(cooldownKey, out float next) && now < next)
            {
                wait = next - now;
                return VehicleShop.Result.Cooldown;
            }
            if (IsGateBusy(depot, now)) return VehicleShop.Result.DepotBusy;

            float price = VehicleShop.Price(definition);
            vehicle = Spawn(depot, definition, player, !deploy || VehicleDriver.IsStaticDefinition(definition));
            if (vehicle == null) return VehicleShop.Result.Unavailable;

            player.AddAllocation(-price);
            VehicleShop.ConsumeWarheads(player.HQ, definition);
            float cooldown = VehicleShop.Cooldown(definition);
            _nextPurchaseTime[cooldownKey] = now + cooldown;
            wait = cooldown;
            _gates[depot] = new GateLock { Vehicle = vehicle, Time = now };
            PruneBuyers();
            _buyers[vehicle] = player;

            GameAccess.PlayerConvoyDonationMessage(player, definition.unitName);
            ModLog.Info("Vehicle purchase: " + player + " bought " + definition.unitName + " for " + UnitConverter.ValueReading(price) + " at " + depot.UniqueName);
            return VehicleShop.Result.Purchased;
        }

        private static ulong PlayerKey(Player player)
        {
            if (player.SteamID != 0UL) return player.SteamID;
            if (player.PlayerIndex > 0) return 0x8000000000000000UL | (uint)player.PlayerIndex;
            return 0x4000000000000000UL | player.NetId;
        }

        private static bool IsGateBusy(VehicleDepot depot, float now)
        {
            if (Time.timeSinceLevelLoad - GameAccess.VehicleDepotLastSpawnedTime(depot) < GameAccess.VehicleDepotSpawnCooldown(depot)) return true;
            if (!_gates.TryGetValue(depot, out GateLock gate)) return false;

            Transform spawn = GameAccess.VehicleDepotSpawnTransform(depot);
            bool stillThere = gate.Vehicle != null && !gate.Vehicle.disabled && spawn != null
                && (gate.Vehicle.transform.position - spawn.position).sqrMagnitude < GateClearDistance * GateClearDistance;
            if (stillThere && now - gate.Time < BusyTimeout) return true;

            _gates.Remove(depot);
            return false;
        }

        private static GroundVehicle Spawn(VehicleDepot depot, VehicleDefinition definition, Player owner, bool driveOut)
        {
            Transform spawn = GameAccess.VehicleDepotSpawnTransform(depot);
            Spawner spawner = NetworkSceneSingleton<Spawner>.i;
            if (spawn == null || spawner == null) return null;

            GameAccess.VehicleDepotLastSpawnedTime(depot) = Time.timeSinceLevelLoad;
            GroundVehicle vehicle = spawner.SpawnVehicle(definition.unitPrefab, spawn.GlobalPosition() + Vector3.up * definition.spawnOffset.y,
                spawn.rotation, Vector3.zero, depot.NetworkHQ, null, 1f, true, owner);
            if (vehicle == null) return null;

            if (driveOut)
            {
                GameAccess.VehicleDepotMoveVehicleFromDepot(depot, vehicle).Forget();
                vehicle.MoveFromDepot();
            }
            return vehicle;
        }

        private static void PruneBuyers()
        {
            _deadBuyers.Clear();
            foreach (GroundVehicle vehicle in _buyers.Keys)
            {
                if (vehicle == null || vehicle.disabled) _deadBuyers.Add(vehicle);
            }
            foreach (GroundVehicle vehicle in _deadBuyers) _buyers.Remove(vehicle);
        }

        public static void Reset()
        {
            _nextPurchaseTime.Clear();
            _gates.Clear();
            _buyers.Clear();
        }
    }
}
