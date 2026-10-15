using UnityEngine;

namespace PlayableVehicles
{
    internal static class VehicleSupply
    {
        private static GroundVehicle _vehicle;
        private static bool _requested;

        public static void Tick(GroundVehicle vehicle)
        {
            if (vehicle != _vehicle)
            {
                _vehicle = vehicle;
                _requested = AmmoSync.RearmRequested(vehicle);
                return;
            }
            if (vehicle == null) return;

            bool requested = AmmoSync.RearmRequested(vehicle);
            if (requested && !_requested)
            {
                ModLog.Notify("Ammo low: stop near a friendly supply vehicle or ammo depot to rearm");
            }
            _requested = requested;
        }

        public static void Reset()
        {
            _vehicle = null;
            _requested = false;
        }

        public static void OnRearm(Unit unit, RearmEventArgs args)
        {
            if (!VehicleSession.IsActive || unit == null || unit != VehicleSession.Vehicle || args.Stations == null) return;

            int stations = Mathf.Min(unit.weaponStations.Count, args.Stations.Length);
            if (stations == 0) return;
            int worst = -4;
            float complete = 0f;
            for (int i = 0; i < stations; i++)
            {
                int rounds = args.Stations[i];
                if (rounds < 0) worst = Mathf.Max(worst, rounds);
                else complete += 1f;
            }

            string text = string.Format("Rearmed {0:F0}% complete", 100f * complete / stations);
            switch (worst)
            {
                case -3: text += " insufficient warheads"; break;
                case -2: text += " insufficient funds"; break;
                case -1: text += " insufficient ammo"; break;
            }
            if (args.Rearmer != null) text += " by " + args.Rearmer.unitName;
            ModLog.Notify(text);
        }
    }
}
