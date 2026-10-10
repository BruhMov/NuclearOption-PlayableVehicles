using System;
using HarmonyLib;
using UnityEngine;

namespace PlayableVehicles
{
    [OptionalPatch("minimap centered on the vehicle")]
    [HarmonyPatch(typeof(DynamicMap), "CenterMinimizedMap")]
    internal static class DynamicMap_CenterMinimizedMap_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("DynamicMap.CenterMinimizedMap patch");

        private static void Postfix(DynamicMap __instance)
        {
            if (!VehicleSession.IsActive) return;
            try
            {
                Center(__instance);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }

        private static void Center(DynamicMap map)
        {
            GroundVehicle vehicle = VehicleSession.Vehicle;
            if (vehicle == null || map.mapImage == null || map.mapBackground == null) return;

            Transform vehicleTransform = vehicle.transform;
            Transform image = map.mapImage.transform;
            float factor = map.mapDisplayFactor;

            Vector3 position = vehicleTransform.position.ToGlobalPosition().AsVector3() * factor;
            Vector3 forward = vehicleTransform.forward;
            forward.y = 0f;
            Vector3 center = position + forward.normalized * factor * ModConfig.MinimapLookAhead.Value;

            image.eulerAngles = new Vector3(0f, 0f, vehicleTransform.eulerAngles.y);
            image.localPosition = (image.right * -center.x + image.up * -center.z)
                * image.localScale.x * map.mapBackground.transform.localScale.x;

            if (map.viewIndicator != null)
            {
                Transform view = map.viewIndicator.transform;
                view.localPosition = new Vector3(position.x, position.z, 0f);
                CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
                float cameraYaw = csm != null ? csm.transform.eulerAngles.y : vehicleTransform.eulerAngles.y;
                view.eulerAngles = new Vector3(0f, 0f, image.eulerAngles.z - cameraYaw);
            }
        }
    }

    [OptionalPatch("map click selects a target instead of moving the camera")]
    [HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.ClickIcon))]
    internal static class UnitMapIcon_ClickIcon_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("UnitMapIcon.ClickIcon patch");

        private static bool Prefix(UnitMapIcon __instance)
        {
            if (!VehicleSession.IsActive) return true;
            try
            {
                Select(__instance);
                return false;
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }

        private static void Select(UnitMapIcon icon)
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            Unit unit = icon.unit;
            if (map == null || unit == null || !map.IsCursorInMapRectangle()) return;
            if (SceneSingleton<TargetListSelector>.i != null && SceneSingleton<TargetListSelector>.i.CheckExclusions(unit)) return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (map.selectedIcons.Contains(icon))
            {
                if (shift) map.DeselectIcon(unit);
                return;
            }
            if (!shift) map.UnselectAll();
            map.SelectIcon(unit);
        }
    }

    [OptionalPatch("Jump Map keeps the camera at the vehicle")]
    [HarmonyPatch(typeof(DynamicMap), "JumpCameraTo")]
    internal static class DynamicMap_JumpCameraTo_Patch
    {
        private static bool Prefix()
        {
            return !VehicleSession.IsActive;
        }
    }

    [OptionalPatch("airbase buttons hidden on the map while driving")]
    [HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.Maximize))]
    internal static class DynamicMap_Maximize_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("DynamicMap.Maximize patch");

        private static void Postfix(DynamicMap __instance)
        {
            try
            {
                if (!VehicleSession.IsActive || !DynamicMap.mapMaximized) return;
                __instance.HideAirbases();
                { GameplayUI single = SceneSingleton<GameplayUI>.i; if (single != null) single.HideSelectAirbase(); }
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }
}
