using System;
using System.Collections.Generic;
using NuclearOption.Networking;

namespace PlayableVehicles
{
    internal static class OwnedUnitKills
    {
        private const float MinDamage = 0.1f;
        private const float MinShare = 0.01f;
        private const int PruneAbove = 256;

        private static readonly HashSet<Unit> _watched = new HashSet<Unit>();
        private static readonly Action<Unit> Handler = OnDisabled;
        private static readonly Predicate<Unit> IsGone = unit => unit == null || unit.disabled;

        public static void OnDamage(Unit damaged, PersistentID dealer, float damage)
        {
            if (damage < MinDamage || damaged == null || damaged.disabled || _watched.Contains(damaged)) return;
            if (damaged is Missile) return;
            if (!IsLocalOwnedVehicle(dealer, out GroundVehicle vehicle)) return;
            if (damaged.NetworkHQ == vehicle.NetworkHQ) return;

            if (_watched.Count > PruneAbove) _watched.RemoveWhere(IsGone);
            _watched.Add(damaged);
            damaged.onDisableUnit += Handler;
        }

        public static void Reset()
        {
            _watched.Clear();
        }

        private static void OnDisabled(Unit unit)
        {
            _watched.Remove(unit);
            if (unit == null) return;
            unit.onDisableUnit -= Handler;
            if (!unit.disabled || GameAccess.UnitDamageCredit == null) return;

            Dictionary<PersistentID, float> credit = GameAccess.UnitDamageCredit(unit);
            if (credit == null || credit.Count == 0) return;
            float total = 0f;
            foreach (KeyValuePair<PersistentID, float> pair in credit) total += pair.Value;
            if (total <= 0f) return;

            float best = 0f;
            PersistentID killer = PersistentID.None;
            foreach (KeyValuePair<PersistentID, float> pair in credit)
            {
                if (pair.Value / total < MinShare || !UnitRegistry.TryGetPersistentUnit(pair.Key, out _)) continue;
                if (pair.Value >= best)
                {
                    best = pair.Value;
                    killer = pair.Key;
                }
            }
            if (!IsLocalOwnedVehicle(killer, out GroundVehicle vehicle)) return;

            string name = vehicle.definition != null && !string.IsNullOrEmpty(vehicle.definition.unitName) ? vehicle.definition.unitName : vehicle.unitName;
            ModLog.Notify("Your " + name + " destroyed " + unit.unitName);
        }

        private static bool IsLocalOwnedVehicle(PersistentID id, out GroundVehicle vehicle)
        {
            vehicle = null;
            if (!id.IsValid || !UnitRegistry.TryGetPersistentUnit(id, out PersistentUnit record) || record.player == null) return false;
            if (!(record.unit is GroundVehicle owned) || owned == null || owned == VehicleSession.Vehicle) return false;
            if (!GameManager.GetLocalPlayer(out Player local) || record.player != local) return false;
            vehicle = owned;
            return true;
        }
    }
}
