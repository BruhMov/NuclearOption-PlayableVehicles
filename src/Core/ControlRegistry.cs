using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class ControlRegistry
    {
        private static readonly HashSet<Unit> _controlled = new HashSet<Unit>();
        private static readonly HashSet<Unit> _clientSimulated = new HashSet<Unit>();
        private static readonly HashSet<Unit> _remoteAimed = new HashSet<Unit>();

        private const float PruneInterval = 1f;
        private static float _nextPruneAt;
        private static readonly Predicate<Unit> IsDestroyed = unit => unit == null;

        public static int Count => _controlled.Count;

        public static void Tick()
        {
            float now = Time.unscaledTime;
            if (now < _nextPruneAt) return;
            _nextPruneAt = now + PruneInterval;

            if (_controlled.Count > 0) _controlled.RemoveWhere(IsDestroyed);
            if (_clientSimulated.Count > 0) _clientSimulated.RemoveWhere(IsDestroyed);
            if (_remoteAimed.Count > 0) _remoteAimed.RemoveWhere(IsDestroyed);
        }

        public static void Reset()
        {
            _controlled.Clear();
            _clientSimulated.Clear();
            _remoteAimed.Clear();
            _nextPruneAt = 0f;
        }

        public static void Forget(GroundVehicle vehicle)
        {
            if ((object)vehicle == null) return;
            _controlled.Remove(vehicle);
            _clientSimulated.Remove(vehicle);
            ArtilleryAi.Resume(vehicle);
        }

        public static void Add(Unit unit)
        {
            if (unit != null) _controlled.Add(unit);
        }

        public static void Remove(Unit unit)
        {
            if ((object)unit != null) _controlled.Remove(unit);
        }

        public static bool IsControlled(Unit unit)
        {
            return (object)unit != null && _controlled.Contains(unit);
        }

        public static void AddClientSimulated(Unit unit)
        {
            if (unit != null) _clientSimulated.Add(unit);
        }

        public static void RemoveClientSimulated(Unit unit)
        {
            if ((object)unit != null) _clientSimulated.Remove(unit);
        }

        public static bool IsClientSimulated(Unit unit)
        {
            return _clientSimulated.Count > 0 && (object)unit != null && _clientSimulated.Contains(unit);
        }

        public static void AddRemoteAimed(Unit unit)
        {
            if (unit != null) _remoteAimed.Add(unit);
        }

        public static void RemoveRemoteAimed(Unit unit)
        {
            if ((object)unit != null) _remoteAimed.Remove(unit);
        }

        public static bool TurretsOverridden(Unit unit)
        {
            if ((_controlled.Count == 0 && _remoteAimed.Count == 0) || (object)unit == null) return false;
            return _controlled.Contains(unit) || _remoteAimed.Contains(unit);
        }
    }
}
