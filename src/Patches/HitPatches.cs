using System;
using HarmonyLib;
using UnityEngine;

namespace PlayableVehicles
{
    [OptionalPatch("hit markers for bullet hits")]
    [HarmonyPatch(typeof(Unit), nameof(Unit.RegisterHit))]
    internal static class Unit_RegisterHit_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("Unit.RegisterHit patch");

        private static void Postfix(Unit __instance, Unit __0, Vector3 __1, Vector3 __2, WeaponInfo __3)
        {
            try
            {
                HitFeedback.OnBulletHit(__instance, __0, __1, __2, __3);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }

    [OptionalPatch("hit markers for damage, kill messages of owned vehicles")]
    [HarmonyPatch(typeof(Unit), nameof(Unit.RecordDamage))]
    internal static class Unit_RecordDamage_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("Unit.RecordDamage patch");

        private static void Postfix(Unit __instance, PersistentID __0, float __1)
        {
            try
            {
                HitFeedback.OnDamage(__instance, __0, __1);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
            try
            {
                OwnedUnitKills.OnDamage(__instance, __0, __1);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }
}
