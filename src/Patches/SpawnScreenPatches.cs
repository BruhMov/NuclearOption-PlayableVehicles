using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [OptionalPatch("purchase button on the spawn screen in GROUND mode")]
    [HarmonyPatch(typeof(GameplayUI), nameof(GameplayUI.SelectAircraft))]
    internal static class GameplayUI_SelectAircraft_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("GameplayUI.SelectAircraft patch");

        private static bool Prefix()
        {
            try
            {
                return !SpawnScreen.TryOpenSelectedDepot();
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }
}
