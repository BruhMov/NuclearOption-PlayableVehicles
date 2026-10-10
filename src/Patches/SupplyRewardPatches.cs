using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [OptionalPatch("supply reward for players' supply vehicles")]
    [HarmonyPatch(typeof(Rearmer), nameof(Rearmer.ProcessRearmRequest))]
    internal static class Rearmer_ProcessRearmRequest_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("Rearmer.ProcessRearmRequest patch");

        private static void Prefix(Rearmer __instance, Unit __0)
        {
            try
            {
                SupplyRewards.Begin(__instance, __0);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }

        private static void Postfix(Rearmer __instance, Unit __0, bool __result)
        {
            try
            {
                SupplyRewards.End(__instance, __0, __result);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }

    [OptionalPatch("supply reward for players' supply vehicles")]
    [HarmonyPatch(typeof(Unit), nameof(Unit.RpcRearm))]
    internal static class Unit_RpcRearm_SupplyReward_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("Unit.RpcRearm supply reward patch");

        private static void Prefix(Unit __instance, RearmEventArgs __0)
        {
            try
            {
                SupplyRewards.OnRpcRearm(__instance, __0);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }
}
