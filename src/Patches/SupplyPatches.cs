using System;
using System.Reflection;
using HarmonyLib;

namespace PlayableVehicles
{
    [OptionalPatch("rearm messages for vehicles")]
    [HarmonyPatch]
    internal static class Unit_RpcRearm_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("Unit.RpcRearm patch");

        private static bool Prepare()
        {
            if (TargetMethod() != null) return true;
            ModLog.Warning("Unit.RpcRearm body (UserCode_RpcRearm_*) not found: no rearm messages for vehicles");
            return false;
        }

        private static MethodBase TargetMethod()
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(Unit)))
            {
                if (method.Name.StartsWith("UserCode_RpcRearm_")) return method;
            }
            return null;
        }

        private static void Postfix(Unit __instance, RearmEventArgs __0)
        {
            try
            {
                VehicleSupply.OnRearm(__instance, __0);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }
}
