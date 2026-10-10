using System.Collections.Generic;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class PurchaseClient
    {
        private const float ReplyTimeout = 3f;
        private const float ReplyGiveUp = 15f;

        private static bool _pending;
        private static float _pendingSince;
        private static bool _pendingDeploy;
        private static bool _late;
        private static readonly Dictionary<VehicleShop.CooldownGroup, float> _cooldownUntil = new Dictionary<VehicleShop.CooldownGroup, float>();
        private const float PossessTimeout = 5f;
        private static uint _possessId;
        private static float _possessUntil;
        private static bool _possessLate;

        public static bool Pending => _pending;
        public static float CooldownRemaining(VehicleDefinition definition)
        {
            return definition != null && _cooldownUntil.TryGetValue(VehicleShop.Group(definition), out float until)
                ? Mathf.Max(0f, until - Time.unscaledTime)
                : 0f;
        }

        public static void Request(VehicleDepot depot, VehicleDefinition definition, bool deploy = false)
        {
            if (_pending || depot == null || definition == null) return;
            int index = VehicleShop.IndexOf(definition);
            if (index < 0 || index > ushort.MaxValue) return;

            if (NetBridge.IsServer)
            {
                GameManager.GetLocalPlayer(out Player player);
                VehicleShop.Result result = PurchaseServer.TryPurchase(player, depot, definition, deploy, out GroundVehicle vehicle, out float wait);
                OnResult(result, definition, wait, vehicle != null ? vehicle.persistentID.Id : 0u, deploy, false);
                return;
            }

            if (!ClientLink.CanSendRequests())
            {
                if (deploy) DeployFailed();
                return;
            }

            _pending = true;
            _pendingSince = Time.unscaledTime;
            _pendingDeploy = deploy;
            _late = false;
            NetBridge.SendToServer(new PvPurchaseRequest { DepotId = depot.persistentID.Id, VehicleIndex = (ushort)index, Deploy = deploy }, Channel.Reliable);
        }

        public static void OnReply(INetworkPlayer _, PvPurchaseReply message)
        {
            bool late = _late || !_pending;
            bool deploy = _pendingDeploy;
            _pending = false;
            _pendingDeploy = false;
            _late = false;
            VehicleShop.TryGet(message.VehicleIndex, out VehicleDefinition definition);
            OnResult(message.Result, definition, message.Wait, message.VehicleId, deploy, late);
        }

        public static void Tick()
        {
            if (_pending)
            {
                float waited = Time.unscaledTime - _pendingSince;
                if (!_late && waited > ReplyTimeout)
                {
                    _late = true;
                    ModLog.Notify("Host is slow to confirm the purchase, waiting...");
                    if (_pendingDeploy) DeployFailed();
                }
                else if (waited > ReplyGiveUp)
                {
                    _pending = false;
                    _late = true;
                    ModLog.Notify("Host did not confirm the purchase");
                }
            }

            if (_possessId != 0) TryPossess();
        }

        public static void Reset()
        {
            _pending = false;
            _pendingDeploy = false;
            _late = false;
            _cooldownUntil.Clear();
            _possessId = 0;
            _possessLate = false;
        }

        private static void OnResult(VehicleShop.Result result, VehicleDefinition definition, float wait, uint vehicleId, bool deploy, bool late)
        {
            if ((result == VehicleShop.Result.Purchased || result == VehicleShop.Result.Cooldown) && definition != null)
            {
                _cooldownUntil[VehicleShop.Group(definition)] = Time.unscaledTime + wait;
            }
            if (result != VehicleShop.Result.Purchased) ModLog.Notify(VehicleShop.Describe(result, definition));

            if (!deploy) return;
            if (result == VehicleShop.Result.Purchased && vehicleId != 0)
            {
                _possessId = vehicleId;
                _possessUntil = Time.unscaledTime + PossessTimeout;
                _possessLate = late;
                TryPossess();
            }
            else if (!late)
            {
                DeployFailed();
            }
        }

        private static void TryPossess()
        {
            if (_possessLate && !StillWaitingForDeploy())
            {
                _possessId = 0;
                ModLog.Notify("Purchased vehicle is waiting at the depot");
                return;
            }
            if (UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = _possessId }), out Unit unit) && unit is GroundVehicle vehicle && !vehicle.disabled)
            {
                _possessId = 0;
                VehicleSession.EnterDeployed(vehicle);
            }
            else if (Time.unscaledTime > _possessUntil)
            {
                _possessId = 0;
                ModLog.Notify("Purchased vehicle did not arrive");
                if (!_possessLate) DeployFailed();
            }
        }

        private static bool StillWaitingForDeploy()
        {
            if (VehicleSession.IsActive || VehicleSelectionMenu.IsOpen) return false;
            if (!GameManager.GetLocalPlayer(out Player player) || player == null || player.AircraftSpawnPending) return false;
            return player.Aircraft == null || player.Aircraft.disabled;
        }

        private static void DeployFailed()
        {
            VehicleSelectionMenu.ReturnToPreviousView();
        }
    }
}
