using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class ArtilleryAi
    {
        private const int PausedState = 99;
        private const float RestartDelay = 6f;
        private static readonly HashSet<MobileArtilleryAI> _paused = new HashSet<MobileArtilleryAI>();
        private static readonly Dictionary<MobileArtilleryAI, float> _pausedAt = new Dictionary<MobileArtilleryAI, float>();
        private static readonly Dictionary<MobileArtilleryAI, float> _restartAt = new Dictionary<MobileArtilleryAI, float>();
        private static readonly List<MobileArtilleryAI> _due = new List<MobileArtilleryAI>();

        private const float PruneInterval = 1f;
        private static float _nextPruneAt;
        private static readonly Predicate<MobileArtilleryAI> IsDestroyed = ai => ai == null;

        public static bool IsPaused(MobileArtilleryAI ai)
        {
            return _paused.Contains(ai);
        }

        public static void Tick()
        {
            RunDueRestarts();
            float now = Time.unscaledTime;
            if (now < _nextPruneAt) return;
            _nextPruneAt = now + PruneInterval;
            if (_paused.Count > 0) _paused.RemoveWhere(IsDestroyed);
            if (_pausedAt.Count > _paused.Count)
            {
                _due.Clear();
                foreach (MobileArtilleryAI ai in _pausedAt.Keys)
                {
                    if (ai == null || !_paused.Contains(ai)) _due.Add(ai);
                }
                foreach (MobileArtilleryAI ai in _due) _pausedAt.Remove(ai);
            }
        }

        public static void Reset()
        {
            _paused.Clear();
            _pausedAt.Clear();
            _restartAt.Clear();
            _nextPruneAt = 0f;
        }

        public static void Pause(GroundVehicle vehicle)
        {
            if (vehicle == null || !vehicle.IsServer || GameAccess.MobileArtilleryAISetState == null) return;
            _paused.RemoveWhere(IsDestroyed);
            foreach (MobileArtilleryAI ai in vehicle.GetComponentsInChildren<MobileArtilleryAI>(true))
            {
                _paused.Add(ai);
                if (!_restartAt.Remove(ai)) _pausedAt[ai] = Time.time;
                GameAccess.MobileArtilleryAISetState(ai, PausedState);
            }
        }

        public static void Resume(GroundVehicle vehicle)
        {
            if (vehicle == null)
            {
                if (_paused.Count > 0) _paused.RemoveWhere(IsDestroyed);
                return;
            }
            foreach (MobileArtilleryAI ai in vehicle.GetComponentsInChildren<MobileArtilleryAI>(true))
            {
                if (!_paused.Contains(ai)) continue;
                float restartAt = (_pausedAt.TryGetValue(ai, out float pausedAt) ? pausedAt : 0f) + RestartDelay;
                if (Time.time < restartAt)
                {
                    _restartAt[ai] = restartAt;
                    continue;
                }
                Restart(ai, vehicle);
            }
        }

        private static void Restart(MobileArtilleryAI ai, GroundVehicle vehicle)
        {
            _paused.Remove(ai);
            _pausedAt.Remove(ai);
            _restartAt.Remove(ai);
            if (vehicle != null && !vehicle.disabled && vehicle.IsServer && GameAccess.MobileArtilleryAITargetSearch != null)
            {
                GameAccess.MobileArtilleryAITargetSearch(ai).Forget();
            }
        }

        private static void RunDueRestarts()
        {
            if (_restartAt.Count == 0) return;
            float now = Time.time;
            _due.Clear();
            foreach (KeyValuePair<MobileArtilleryAI, float> pair in _restartAt)
            {
                if (pair.Key == null || now >= pair.Value) _due.Add(pair.Key);
            }
            foreach (MobileArtilleryAI ai in _due)
            {
                if (ai == null)
                {
                    _restartAt.Remove(ai);
                    continue;
                }
                Restart(ai, ai.GetComponentInParent<GroundVehicle>());
            }
        }
    }
}
