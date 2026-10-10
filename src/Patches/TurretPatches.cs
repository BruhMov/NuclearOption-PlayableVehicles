using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [HarmonyPatch(typeof(Turret), "FixedUpdate")]
    internal static class Turret_FixedUpdate_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("Turret.FixedUpdate patch");

        private static bool Prefix(Turret __instance)
        {
            try
            {
                return !ControlRegistry.TurretsOverridden(GameAccess.TurretAttachedUnit(__instance));
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }
}
