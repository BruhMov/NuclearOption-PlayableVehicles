using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [HarmonyPatch(typeof(CombatHUD), "LateUpdate")]
    internal static class CombatHUD_LateUpdate_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("CombatHUD.LateUpdate patch");

        private static bool Prefix(CombatHUD __instance)
        {
            if (!VehicleSession.IsActive) return true;

            try
            {
                GameAccess.CombatHudUpdateHitMarkers(__instance);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
            return false;
        }
    }

    [OptionalPatch("aircraft weapon HUD without an aircraft")]
    [HarmonyPatch(typeof(CombatHUD), "FixedUpdate")]
    internal static class CombatHUD_FixedUpdate_Patch
    {
        private static bool Prefix(CombatHUD __instance)
        {
            return !VehicleSession.IsActive || __instance.aircraft != null;
        }
    }

    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.CreateMarker))]
    internal static class CombatHUD_CreateMarker_Patch
    {
        private static bool Prefix()
        {
            return !VehicleSession.IsActive;
        }
    }

    [OptionalPatch("player's target in the unit info line")]
    [HarmonyPatch(typeof(UnitDebug), "Update")]
    internal static class UnitDebug_Update_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("UnitDebug.Update patch");

        private static void Postfix(UnitDebug __instance)
        {
            try
            {
                if (!VehicleSession.IsActive || VehicleSession.Driver == null || GameAccess.UnitDebugTarget == null
                    || GameAccess.UnitDebugFollowingUnit == null) return;
                if (GameAccess.UnitDebugFollowingUnit(__instance) != VehicleSession.Vehicle) return;
                TMPro.TMP_Text text = GameAccess.UnitDebugTarget(__instance);
                if (text == null) return;

                TurretController turrets = VehicleSession.Driver.Turrets;
                Unit target = turrets.MissionActive ? turrets.MissionTarget : turrets.LockedTarget;
                string value = target != null && !target.disabled ? target.unitName : "none";
                if (text.text != value) text.text = value;
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }
}
