using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HarmonyLib;

namespace PlayableVehicles
{
    [HarmonyPatch]
    internal static class MobileArtilleryAI_Loops_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("MobileArtilleryAI loops patch");

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return PatchLoader.DeclaredMethods(typeof(MobileArtilleryAI), nameof(MobileArtilleryAI_Loops_Patch),
                "TargetSearch", "DestinationSearch", "DriveToDestination", "Attack", "SeekRearm");
        }

        private static bool Prefix(MobileArtilleryAI __instance, ref UniTask __result)
        {
            try
            {
                if (!ArtilleryAi.IsPaused(__instance)) return true;
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
            __result = default;
            return false;
        }
    }

    [HarmonyPatch(typeof(FireControl), "HQTargetAssessment")]
    internal static class FireControl_HQTargetAssessment_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("FireControl.HQTargetAssessment patch");

        private static bool Prefix(FireControl __instance)
        {
            try
            {
                return !FireControlPause.Paused(__instance);
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }

    [HarmonyPatch]
    internal static class FireControl_Salvo_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("FireControl salvo patch");

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return PatchLoader.DeclaredMethods(typeof(FireControl), nameof(FireControl_Salvo_Patch), "PlanSalvo", "LaunchSalvo");
        }

        private static bool Prefix(FireControl __instance, ref UniTask __result)
        {
            try
            {
                if (!FireControlPause.Paused(__instance)) return true;
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
            __result = default;
            return false;
        }
    }

    internal static class FireControlPause
    {
        public static bool Paused(FireControl fireControl)
        {
            return GameAccess.FireControlAttachedUnit != null && ControlRegistry.IsControlled(GameAccess.FireControlAttachedUnit(fireControl));
        }
    }

    [HarmonyPatch]
    internal static class FireControl_QueuedAttackFire_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("FireControl.QueuedAttack.Fire patch");

        private static MethodBase TargetMethod()
        {
            System.Type attack = AccessTools.Inner(typeof(FireControl), "QueuedAttack");
            return attack != null ? AccessTools.DeclaredMethod(attack, "Fire") : null;
        }

        private static bool Prepare()
        {
            if (TargetMethod() != null) return true;
            ModLog.Warning("FireControl.QueuedAttack.Fire not found: a salvo queued before entering the vehicle may still fire");
            return false;
        }

        private static bool Prefix(Unit __0)
        {
            try
            {
                return !ControlRegistry.IsControlled(__0);
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }
}
