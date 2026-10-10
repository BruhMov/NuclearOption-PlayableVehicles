using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PlayableVehicles
{
    [HarmonyPatch]
    internal static class CameraState_UpdateState_Patch
    {
        private static readonly Type[] StateTypes =
        {
            typeof(CameraOrbitState), typeof(CameraChaseState), typeof(CameraCockpitState),
            typeof(CameraControlledState), typeof(CameraEncyclopediaState), typeof(CameraFreeState),
            typeof(CameraRelativeState), typeof(CameraSelectionState), typeof(CameraTVState)
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var found = new List<MethodBase>();
            foreach (Type type in StateTypes)
            {
                MethodInfo method = AccessTools.DeclaredMethod(type, "UpdateState");
                if (method != null) found.Add(method);
                else ModLog.Warning("CameraState_UpdateState_Patch: " + type.Name + ".UpdateState not found - not patched");
            }
            if (found.Count < StateTypes.Length)
            {
                ModLog.Warning("CameraState_UpdateState_Patch: " + found.Count + " of " + StateTypes.Length + " camera states patched");
            }
            WarnUnknownStates();
            return found;
        }

        private static void WarnUnknownStates()
        {
            try
            {
                var known = new HashSet<Type>(StateTypes);
                foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(CameraBaseState).Assembly))
                {
                    if (type.IsAbstract || !typeof(CameraBaseState).IsAssignableFrom(type) || known.Contains(type)) continue;
                    if (AccessTools.DeclaredMethod(type, "UpdateState") == null) continue;
                    ModLog.Warning("CameraState_UpdateState_Patch: unknown camera state " + type.FullName
                        + " (new in this game version?) - its UpdateState is not blocked while driving");
                }
            }
            catch (Exception e)
            {
                ModLog.Warning("CameraState_UpdateState_Patch: camera state scan failed: " + e.Message);
            }
        }

        private static bool Prefix()
        {
            return !VehicleSession.IsActive;
        }
    }

    [OptionalPatch("camera shake in vehicle")]
    [HarmonyPatch(typeof(CameraStateManager), nameof(CameraStateManager.ShakeCamera))]
    internal static class CameraStateManager_ShakeCamera_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("CameraStateManager.ShakeCamera patch");

        private static void Postfix(float __0, float __1)
        {
            try
            {
                if (VehicleSession.IsActive) VehicleSession.ViewCamera?.AddShake(__0, __1);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }

    [HarmonyPatch(typeof(CameraStateManager), "LateUpdate")]
    internal static class CameraStateManager_LateUpdate_Patch
    {
        private static void Postfix()
        {
            VehicleSession.UpdateCamera();
        }
    }
}
