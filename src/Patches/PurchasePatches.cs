using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [OptionalPatch("vehicle owner and purchase hint in map tooltips")]
    [HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.GetInfoText))]
    internal static class UnitMapIcon_GetInfoText_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("UnitMapIcon.GetInfoText patch");

        private static void Postfix(UnitMapIcon __instance, ref string __result)
        {
            try
            {
                if (__instance.unit is GroundVehicle vehicle && vehicle.Networkowner != null)
                {
                    __result += "\n" + KillCredit.DisplayName(vehicle.Networkowner);
                    return;
                }
                if (!VehicleShop.Enabled || !(__instance.unit is VehicleDepot depot)) return;
                if (!GameManager.GetLocalHQ(out FactionHQ hq) || !VehicleShop.IsOwnDepot(depot, hq) || hq.preventDonation) return;
                __result += "\nSelect + [" + ModConfig.ToggleKey.Value + "]: purchase vehicles";
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }

    [OptionalPatch("depot and own vehicle map markers")]
    [HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.UpdateIcon))]
    internal static class UnitMapIcon_UpdateIcon_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("UnitMapIcon.UpdateIcon patch");

        private static void Postfix(UnitMapIcon __instance, float __1)
        {
            try
            {
                if (__instance.unit is VehicleDepot depot) DepotMarkers.Update(__instance, depot, __1);
                else if (__instance.unit is GroundVehicle vehicle) OwnVehicleMarkers.Update(__instance, vehicle);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }

    [OptionalPatch("airbase selection for vehicle purchase on the spawn screen")]
    [HarmonyPatch(typeof(AirbaseMapIcon), nameof(AirbaseMapIcon.ClickIcon))]
    internal static class AirbaseMapIcon_ClickIcon_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("AirbaseMapIcon.ClickIcon patch");

        private static bool Prefix(AirbaseMapIcon __instance)
        {
            try
            {
                return !SpawnScreen.SelectAirbase(__instance.airbase);
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }
}
