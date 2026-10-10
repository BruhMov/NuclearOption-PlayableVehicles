using NuclearOption.Networking;

namespace PlayableVehicles
{
    internal static class SupplyRewards
    {
        private static bool _active;
        private static Unit _target;
        private static Player _player;
        private static int[] _stations;

        public static void Begin(Rearmer rearmer, Unit target)
        {
            _active = false;
            _target = null;
            _player = null;
            _stations = null;
            if (rearmer == null || target == null || !NetBridge.IsServer) return;
            if (!(rearmer.Unit is GroundVehicle vehicle) || vehicle == null) return;
            Player player = RewardedPlayer(vehicle);
            if (player == null) return;

            _active = true;
            _target = target;
            _player = player;
        }

        public static void OnRpcRearm(Unit unit, RearmEventArgs args)
        {
            if (!_active || (object)unit != _target) return;
            _stations = args.Stations;
        }

        public static void End(Rearmer rearmer, Unit target, bool success)
        {
            if (!_active) return;
            Player player = _player;
            int[] stations = _stations;
            _active = false;
            _target = null;
            _player = null;
            _stations = null;

            if (!success || stations == null || player == null || target == null || rearmer == null || rearmer.Unit == null) return;
            FactionHQ hq = rearmer.Unit.NetworkHQ;
            if (hq == null) return;

            float value = 0f;
            int count = System.Math.Min(stations.Length, target.weaponStations.Count);
            for (int i = 0; i < count; i++)
            {
                if (stations[i] > 0) value += stations[i] * target.weaponStations[i].WeaponInfo.costPerRound;
            }
            if (value <= 0f || NuclearOption.Networking.PlayerHelper.GetPlayer(target) == player) return;
            hq.ReportSupplyAction(player, target, value);
            ModLog.Info("[VEHICLE] Supply reward: " + KillCredit.DisplayName(player) + " rearmed " + target.unitName
                + " from " + rearmer.Unit.unitName + ", value " + value.ToString("F1"));
        }

        private static Player RewardedPlayer(GroundVehicle vehicle)
        {
            if (KillCredit.TryGetActiveDriver(vehicle, out Player driver) && driver != null) return driver;
            if (UnitRegistry.TryGetPersistentUnit(vehicle.persistentID, out PersistentUnit unit) && unit.player != null) return unit.player;
            return null;
        }
    }
}
