using Mirage;
using NuclearOption.Networking;

namespace PlayableVehicles
{
    internal static class VehicleAccess
    {
        public const string DeniedText = "Only your purchased vehicles or vehicles delivered by players can be driven";

        private static bool _rulesShown;

        public static VehicleAccessMode Mode => ModConfig.VehicleAccess.Value;

        public static bool CanDrive(Player player, GroundVehicle vehicle, bool localHost)
        {
            if (Mode == VehicleAccessMode.Open) return true;
            if (localHost && !ModConfig.HostFollowsAccess.Value) return true;
            if (player == null || vehicle == null) return false;

            if (PurchaseServer.TryGetBuyer(vehicle, out Player buyer)) return buyer == null || buyer == player;
            return vehicle.Networkowner != null;
        }

        public static void OnServerRules(INetworkPlayer _, PvServerRules message)
        {
            if (message.Access == VehicleAccessMode.OwnVehicles && !_rulesShown)
            {
                _rulesShown = true;
                ModLog.Notify("Server rules: " + DeniedText);
            }
        }

        public static void ClientReset()
        {
            _rulesShown = false;
        }
    }
}
