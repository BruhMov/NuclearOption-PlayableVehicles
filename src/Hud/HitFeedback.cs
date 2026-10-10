using System.Collections.Generic;
using Mirage;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class HitFeedback
    {
        public struct Hit
        {
            public Unit Unit;
            public Vector3 LocalPoint;
            public bool HasPoint;
        }

        private const float MinDamage = 0.1f;
        private const float DedupWindow = 0.1f;

        private static readonly List<Hit> _pending = new List<Hit>();
        private static readonly List<Vector3> _damageDirections = new List<Vector3>();
        private static Unit _lastBulletUnit;
        private static float _lastBulletTime;

        public static void OnBulletHit(Unit shooter, Unit hitUnit, Vector3 localPoint)
        {
            GroundVehicle vehicle = LocalVehicle;
            if (vehicle == null || !ReferenceEquals(shooter, vehicle))
            {
                RemoteDrivers.OnBulletHit(shooter, hitUnit, localPoint);
                return;
            }
            if (!IsHitOnForeignUnit(vehicle, hitUnit)) return;

            _pending.Add(new Hit { Unit = hitUnit, LocalPoint = localPoint, HasPoint = true });
            _lastBulletUnit = hitUnit;
            _lastBulletTime = Time.time;
        }

        public static void OnDamage(Unit damaged, PersistentID dealer, float damage)
        {
            if (damage < MinDamage) return;
            RemoteDrivers.OnVehicleDamaged(damaged, dealer);
            GroundVehicle vehicle = LocalVehicle;
            if (vehicle == null || dealer != vehicle.persistentID)
            {
                RemoteDrivers.OnDamage(damaged, dealer);
                return;
            }
            if (!IsHitOnForeignUnit(vehicle, damaged)) return;
            if (damaged == _lastBulletUnit && Time.time - _lastBulletTime < DedupWindow) return;

            _pending.Add(new Hit { Unit = damaged, HasPoint = false });
        }

        public static void OnRemoteHit(INetworkPlayer _, PvHit message)
        {
            if (!VehicleSession.IsClientDriver) return;
            if (!UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = message.TargetId }), out Unit unit) || unit == null) return;

            _pending.Add(new Hit { Unit = unit, LocalPoint = new Vector3(message.X, message.Y, message.Z), HasPoint = message.HasPoint });
        }

        public static void OnDamageDirection(INetworkPlayer _, PvDamageDirection message)
        {
            GroundVehicle vehicle = VehicleSession.Vehicle;
            if (!VehicleSession.IsClientDriver || (object)vehicle == null || vehicle.persistentID.Id != message.VehicleId) return;
            Vector3 direction = new Vector3(message.DirX, 0f, message.DirZ);
            if (float.IsNaN(direction.x) || float.IsNaN(direction.z) || float.IsInfinity(direction.x) || float.IsInfinity(direction.z)) return;
            if (direction.sqrMagnitude < 0.01f) return;
            _damageDirections.Add(direction);
        }

        public static void DrainDamageDirections(List<Vector3> into)
        {
            into.AddRange(_damageDirections);
            _damageDirections.Clear();
        }

        public static bool IsHitOnForeignUnit(GroundVehicle vehicle, Unit target)
        {
            return vehicle != null && target != null && target != vehicle && target.NetworkHQ != vehicle.NetworkHQ;
        }

        public static void Drain(List<Hit> into)
        {
            into.AddRange(_pending);
            _pending.Clear();
        }

        public static void Clear()
        {
            _pending.Clear();
            _damageDirections.Clear();
            _lastBulletUnit = null;
        }

        private static GroundVehicle LocalVehicle =>
            VehicleSession.IsActive && !VehicleSession.IsClientDriver ? VehicleSession.Vehicle : null;
    }
}
