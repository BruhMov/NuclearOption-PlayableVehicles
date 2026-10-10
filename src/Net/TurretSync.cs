using System.Collections.Generic;
using Mirage;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class TurretSync
    {
        private const float SendInterval = 1f / 15f;
        private const float StaleTimeout = 1f;

        private sealed class RemoteAim
        {
            public GroundVehicle Vehicle;
            public Turret[] Turrets;
            public int Station;
            public GlobalPosition StationAim;
            public GlobalPosition FreeAim;
            public float LastTime;
        }

        private static float _nextSendTime;

        private static readonly Dictionary<uint, RemoteAim> _remote = new Dictionary<uint, RemoteAim>();
        private static readonly List<uint> _toRemove = new List<uint>();

        public static void ServerTick()
        {
            if (!NetBridge.HasModClients || !NetBridge.IsServer) return;
            if (Time.unscaledTime < _nextSendTime) return;
            _nextSendTime = Time.unscaledTime + SendInterval;

            if (VehicleSession.IsActive && VehicleSession.Driver != null && VehicleSession.Driver.Mode == VehicleDriver.DriveMode.Local)
            {
                Broadcast(VehicleSession.Vehicle, VehicleSession.Driver, null);
            }
            foreach (RemoteDrivers.Session session in RemoteDrivers.Sessions)
            {
                Broadcast(session.Vehicle, session.Driver, session.Connection);
            }
        }

        private static void Broadcast(GroundVehicle vehicle, VehicleDriver driver, INetworkPlayer exclude)
        {
            if (vehicle == null || vehicle.disabled || driver == null) return;

            TurretController turrets = driver.Turrets;
            GlobalPosition stationAim = turrets.StationAimPoint.ToGlobalPosition();
            GlobalPosition freeAim = turrets.CurrentAimPoint.ToGlobalPosition();
            PvTurretAim message = new PvTurretAim
            {
                VehicleId = vehicle.persistentID.Id,
                Station = (byte)Mathf.Clamp(turrets.SelectedStationIndex, 0, 255),
                StationAimX = stationAim.x,
                StationAimY = stationAim.y,
                StationAimZ = stationAim.z,
                FreeAimX = freeAim.x,
                FreeAimY = freeAim.y,
                FreeAimZ = freeAim.z,
            };

            NetBridge.SendToModClients(message, Channel.Unreliable, exclude);
        }

        public static void OnTurretAim(INetworkPlayer _, PvTurretAim message)
        {
            if (!_remote.TryGetValue(message.VehicleId, out RemoteAim aim))
            {
                if (!UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = message.VehicleId }), out Unit unit)) return;
                GroundVehicle vehicle = unit as GroundVehicle;
                if (vehicle == null || vehicle.disabled || IsOwnVehicle(vehicle)) return;

                aim = new RemoteAim { Vehicle = vehicle, Turrets = vehicle.GetComponentsInChildren<Turret>(true) };
                _remote.Add(message.VehicleId, aim);
                ControlRegistry.AddRemoteAimed(vehicle);
            }

            aim.Station = message.Station;
            aim.StationAim = new GlobalPosition(message.StationAimX, message.StationAimY, message.StationAimZ);
            aim.FreeAim = new GlobalPosition(message.FreeAimX, message.FreeAimY, message.FreeAimZ);
            aim.LastTime = Time.time;
        }

        public static void ClientFixedTick()
        {
            if (_remote.Count == 0) return;

            foreach (KeyValuePair<uint, RemoteAim> pair in _remote)
            {
                RemoteAim aim = pair.Value;
                if (aim.Vehicle == null || aim.Vehicle.disabled || IsOwnVehicle(aim.Vehicle) || Time.time - aim.LastTime > StaleTimeout)
                {
                    _toRemove.Add(pair.Key);
                    continue;
                }
                Aim(aim);
            }

            foreach (uint id in _toRemove)
            {
                ControlRegistry.RemoveRemoteAimed(_remote[id].Vehicle);
                _remote.Remove(id);
            }
            _toRemove.Clear();
        }

        public static void ClientReset()
        {
            foreach (RemoteAim aim in _remote.Values)
            {
                ControlRegistry.RemoveRemoteAimed(aim.Vehicle);
            }
            _remote.Clear();
        }

        private static void Aim(RemoteAim aim)
        {
            List<WeaponStation> stations = aim.Vehicle.weaponStations;
            WeaponStation station = stations != null && aim.Station < stations.Count ? stations[aim.Station] : null;
            Vector3 stationAim = aim.StationAim.ToLocalPosition();
            Vector3 freeAim = aim.FreeAim.ToLocalPosition();

            foreach (Turret turret in aim.Turrets)
            {
                if (turret == null || !turret.gameObject.activeInHierarchy) continue;
                bool stationTurret = station != null && station.Turrets != null && station.Turrets.Contains(turret);
                Vector3 target = stationTurret ? stationAim : freeAim;
                GameAccess.TurretAimTurret(turret, target - turret.transform.position);
            }
        }

        private static bool IsOwnVehicle(GroundVehicle vehicle)
        {
            return VehicleSession.IsActive && VehicleSession.Vehicle == vehicle;
        }
    }
}
