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
                if (!ControlRegistry.TurretsOverridden(GameAccess.TurretAttachedUnit(__instance))) return true;
                return PointDefence.RunsVanilla(__instance);
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }
}
