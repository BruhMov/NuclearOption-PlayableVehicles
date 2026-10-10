using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [OptionalPatch("guided shells without a target")]
    [HarmonyPatch(typeof(InertialSeekerShell), nameof(InertialSeekerShell.Initialize))]
    internal static class GuidedShell_NoTarget_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("InertialSeekerShell.Initialize patch");

        private static void Postfix(InertialSeekerShell __instance)
        {
            try
            {
                if (__instance == null || GameAccess.SeekerTargetUnit == null) return;
                if (GameAccess.SeekerTargetUnit(__instance) != null) return;
                if (GameAccess.InertialSeekerUseDatalink != null) GameAccess.InertialSeekerUseDatalink(__instance) = false;
                if (GameAccess.InertialSeekerUseLaser != null) GameAccess.InertialSeekerUseLaser(__instance) = false;
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }
}
