using System.Collections.Generic;

namespace PlayableVehicles
{
    internal static class PointDefence
    {
        private static readonly HashSet<Turret> _autonomous = new HashSet<Turret>();

        public static bool IsPointDefenceStation(WeaponStation station)
        {
            if (station == null || station.Weapons == null) return false;
            foreach (Weapon weapon in station.Weapons)
            {
                if (weapon is Laser) return true;
            }
            return false;
        }

        public static bool IsAutonomous(GroundVehicle vehicle, WeaponStation selected, Turret turret)
        {
            if (vehicle == null || turret == null || vehicle.weaponStations == null) return false;
            if (selected != null && selected.Turrets != null && selected.Turrets.Contains(turret)) return false;
            foreach (WeaponStation station in vehicle.weaponStations)
            {
                if (station != selected && station.Turrets != null && station.Turrets.Contains(turret) && IsPointDefenceStation(station)) return true;
            }
            return false;
        }

        public static bool Mark(Turret turret, bool autonomous)
        {
            if (autonomous) _autonomous.Add(turret);
            else if (_autonomous.Count > 0) _autonomous.Remove(turret);
            return autonomous;
        }

        public static void Unmark(IEnumerable<Turret> turrets)
        {
            if (_autonomous.Count == 0 || turrets == null) return;
            foreach (Turret turret in turrets)
            {
                if ((object)turret != null) _autonomous.Remove(turret);
            }
        }

        public static void Reset()
        {
            _autonomous.Clear();
        }

        public static bool RunsVanilla(Turret turret)
        {
            if (_autonomous.Count == 0 || !_autonomous.Contains(turret)) return false;
            return turret.GetTarget() is Missile;
        }
    }
}
