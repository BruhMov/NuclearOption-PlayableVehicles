using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class SupplyMissionLock
    {
        private const float ScanInterval = 1f;

        private static readonly Dictionary<Rearmer, bool> _locked = new Dictionary<Rearmer, bool>();
        private static readonly List<Rearmer> _released = new List<Rearmer>();
        private static float _nextScan;

        public static void Lock(GroundVehicle vehicle)
        {
            if (vehicle == null || !NetBridge.IsServer) return;
            Rearmer rearmer = vehicle.GetComponent<Rearmer>();
            if (rearmer != null) LockRearmer(rearmer);
        }

        public static void Release(GroundVehicle vehicle)
        {
            if (vehicle == null || _locked.Count == 0) return;
            Rearmer rearmer = vehicle.GetComponent<Rearmer>();
            if (rearmer != null && _locked.Remove(rearmer)) rearmer.AvailableForMission = true;
        }

        public static void Tick()
        {
            if (_locked.Count > 0)
            {
                _released.Clear();
                foreach (KeyValuePair<Rearmer, bool> pair in _locked)
                {
                    Rearmer rearmer = pair.Key;
                    if (rearmer == null || rearmer.Unit == null || !ControlRegistry.IsControlled(rearmer.Unit)) _released.Add(rearmer);
                    else if (rearmer.AvailableForMission) rearmer.AvailableForMission = false;
                }
                foreach (Rearmer rearmer in _released)
                {
                    _locked.Remove(rearmer);
                    if (rearmer != null) rearmer.AvailableForMission = true;
                }
            }

            if (ControlRegistry.Count == 0 || Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;
            if (!NetBridge.IsServer) return;
            foreach (FactionHQ hq in FactionRegistry.GetAllHQs())
            {
                List<Rearmer> rearmers = hq != null && hq.RearmMissionController != null ? hq.RearmMissionController.Rearmers : null;
                if (rearmers == null) continue;
                foreach (Rearmer rearmer in rearmers)
                {
                    if (rearmer != null && rearmer.Unit is GroundVehicle && !_locked.ContainsKey(rearmer) && ControlRegistry.IsControlled(rearmer.Unit))
                    {
                        LockRearmer(rearmer);
                    }
                }
            }
        }

        public static void Reset()
        {
            _locked.Clear();
            _released.Clear();
            _nextScan = 0f;
        }

        private static void LockRearmer(Rearmer rearmer)
        {
            if (_locked.ContainsKey(rearmer)) return;
            _locked.Add(rearmer, rearmer.AvailableForMission);
            rearmer.AvailableForMission = false;

            FactionHQ hq = rearmer.Unit != null ? rearmer.Unit.NetworkHQ : null;
            List<RearmMissionController.RearmMission> missions = hq != null && hq.RearmMissionController != null ? hq.RearmMissionController.Missions : null;
            if (missions == null) return;
            foreach (RearmMissionController.RearmMission mission in missions)
            {
                if (mission != null && mission.Rearmer == rearmer) mission.AssignRearmer(null);
            }
        }
    }
}
