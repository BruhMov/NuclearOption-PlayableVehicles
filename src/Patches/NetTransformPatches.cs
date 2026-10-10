using System;
using HarmonyLib;
using NuclearOption.NetworkTransforms;

namespace PlayableVehicles
{
    [OptionalPatch("client driver smoothing in multiplayer")]
    [HarmonyPatch(typeof(GroundVehicleNetworkTransform), "VisualUpdate")]
    internal static class GroundVehicleNetworkTransform_VisualUpdate_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("GroundVehicleNetworkTransform.VisualUpdate patch");

        private static bool Prefix(GroundVehicleNetworkTransform __instance)
        {
            try
            {
                return !ControlRegistry.IsClientSimulated(__instance.GroundVehicle);
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }
}
