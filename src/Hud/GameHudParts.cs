using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal static class GameHudParts
    {
        internal sealed class HmdBox
        {
            public RectTransform Root;
            public TextMeshProUGUI Main;
        }

        internal sealed class TurretCrosshair
        {
            public RectTransform Root;
            public Image Circle;
            public Image Readiness;
            public Image Cross;
        }

        internal sealed class ArcGauge
        {
            public RectTransform Root;
            public Image Bar;
            public Image Pointer;
            public Transform Pivot;
            public TextMeshProUGUI Reading;
            public TextMeshProUGUI Label;
            public Vector2 Offset;
        }

        internal sealed class WeaponPanel
        {
            public RectTransform Root;
            public Image Icon;
            public Image ReloadImage;
            public RectTransform Reload;
            public RectTransform Panel;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Ammo;
            public Vector2 Size;
        }

        internal sealed class CapturePanel
        {
            public RectTransform Root;
            public RectTransform Bar;
            public Image BarImage;
            public Image PanelImage;
            public TMP_Text Title;
            public TMP_Text Status;
            public CanvasGroup Group;
            public Vector2 BarSize;
            public Vector2 Offset;
        }

        public static HmdBox Speed(Transform parent)
        {
            SpeedGauge gauge = HmdApp(GameAccess.HmdSpeed) as SpeedGauge;
            if (gauge == null || GameAccess.SpeedGaugeText == null) return null;
            SpeedGauge copy = Clone(gauge, parent, out RectTransform root);
            var box = new HmdBox { Root = root, Main = Own(root, GameAccess.SpeedGaugeText(copy)) };
            if (GameAccess.SpeedGaugeOverspeed != null) Hide(Own(root, GameAccess.SpeedGaugeOverspeed(copy)));
            Object.Destroy(copy);
            return box.Main != null ? box : null;
        }

        public static HmdBox Altitude(Transform parent)
        {
            Altitude gauge = HmdApp(GameAccess.HmdAltitude) as Altitude;
            if (gauge == null || GameAccess.AltitudeAbsText == null) return null;
            Altitude copy = Clone(gauge, parent, out RectTransform root);
            TextMeshProUGUI inside = Own(root, GameAccess.AltitudeRadarText != null ? GameAccess.AltitudeRadarText(copy) : null)
                ?? Own(root, GameAccess.AltitudeAbsText(copy));
            Object.Destroy(copy);
            return inside != null ? new HmdBox { Root = root, Main = inside } : null;
        }

        public static HmdBox Bearing(Transform parent)
        {
            Bearing gauge = HmdApp(GameAccess.HmdBearing) as Bearing;
            if (gauge == null || GameAccess.BearingText == null) return null;
            Bearing copy = Clone(gauge, parent, out RectTransform root);
            var box = new HmdBox { Root = root, Main = Own(root, GameAccess.BearingText(copy)) };
            Object.Destroy(copy);
            return box.Main != null ? box : null;
        }

        public static ArcGauge Fuel(Transform parent)
        {
            if (GameAccess.FuelGaugeBar == null || GameAccess.FuelGaugeReading == null || GameAccess.FuelGaugeLabel == null) return null;
            FuelGauge source = null;
            Transform extras = null;
            List<AircraftDefinition> aircraft = Encyclopedia.i != null ? Encyclopedia.i.aircraft : null;
            if (aircraft != null)
            {
                foreach (AircraftDefinition definition in aircraft)
                {
                    GameObject prefab = definition != null && definition.aircraftParameters != null ? definition.aircraftParameters.HUDExtras : null;
                    source = prefab != null ? prefab.GetComponentInChildren<FuelGauge>(true) : null;
                    if (source == null) continue;
                    extras = prefab.transform;
                    break;
                }
            }
            if (source == null) return null;

            FlightHud flightHud = SceneSingleton<FlightHud>.i;
            Transform center = flightHud != null && GameAccess.FlightHudCenter != null ? GameAccess.FlightHudCenter(flightHud) : null;
            PrefabPlacement(extras, source.transform, center, out Vector2 offset, out float scale);

            FuelGauge copy = Clone(source, parent, out RectTransform root);
            if (scale > 0.01f && scale < 10f) root.localScale = Vector3.one * scale;
            var gauge = new ArcGauge
            {
                Root = root,
                Bar = Own(root, GameAccess.FuelGaugeBar(copy)),
                Pointer = Own(root, GameAccess.FuelGaugePointer != null ? GameAccess.FuelGaugePointer(copy) : null),
                Pivot = Own(root, GameAccess.FuelGaugePivot != null ? GameAccess.FuelGaugePivot(copy) : null),
                Reading = Own(root, GameAccess.FuelGaugeReading(copy)),
                Label = Own(root, GameAccess.FuelGaugeLabel(copy)),
                Offset = offset,
            };
            Object.Destroy(copy);
            if (gauge.Bar == null || gauge.Reading == null)
            {
                Object.Destroy(root.gameObject);
                return null;
            }
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.enabled = true;
            ModLog.Info("Hull gauge from the fuel gauge: offset " + offset + ", size " + root.sizeDelta + ", scale " + root.localScale.x.ToString("F2"));
            return gauge;
        }

        public static RawImage Compass(Transform parent)
        {
            FlightHud flightHud = SceneSingleton<FlightHud>.i;
            RawImage source = flightHud != null && GameAccess.FlightHudCompass != null ? GameAccess.FlightHudCompass(flightHud) : null;
            if (source == null) return null;
            RawImage copy = Clone(source, parent, out _);
            for (int i = copy.transform.childCount - 1; i >= 0; i--) Object.Destroy(copy.transform.GetChild(i).gameObject);
            return copy;
        }

        public static TurretCrosshair Crosshair(Transform parent)
        {
            HUDTurretState state = TurretState();
            GameObject prefab = state != null && GameAccess.TurretStateCrosshairPrefab != null ? GameAccess.TurretStateCrosshairPrefab(state) : null;
            if (prefab == null || GameAccess.TurretCrosshairCircle == null || GameAccess.TurretCrosshairReadiness == null
                || GameAccess.TurretCrosshairCross == null) return null;
            HUDTurretCrosshair source = prefab.GetComponent<HUDTurretCrosshair>();
            if (source == null) return null;
            HUDTurretCrosshair copy = Clone(source, parent, out RectTransform root);
            var crosshair = new TurretCrosshair
            {
                Root = root,
                Circle = Own(root, GameAccess.TurretCrosshairCircle(copy)),
                Readiness = Own(root, GameAccess.TurretCrosshairReadiness(copy)),
                Cross = Own(root, GameAccess.TurretCrosshairCross(copy)),
            };
            Object.Destroy(copy);
            return crosshair.Cross != null ? crosshair : null;
        }

        public static CapturePanel Capture(Transform parent)
        {
            if (GameAccess.CombatHudCargoUI == null || GameAccess.CargoCapturePanel == null || GameAccess.CargoCaptureBar == null
                || GameAccess.CargoCaptureText == null || GameAccess.CargoAirbaseTitle == null) return null;
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            GameObject prefab = combatHud != null ? GameAccess.CombatHudCargoUI(combatHud) : null;
            HUDCargoState state = prefab != null ? prefab.GetComponentInChildren<HUDCargoState>(true) : null;
            GameObject panel = state != null ? GameAccess.CargoCapturePanel(state) : null;
            var panelRect = panel != null ? panel.transform as RectTransform : null;
            if (panelRect == null || !panelRect.IsChildOf(prefab.transform)) return null;

            FlightHud flightHud = SceneSingleton<FlightHud>.i;
            PrefabPlacement(prefab.transform, panelRect, flightHud != null ? flightHud.HMDCenter : null, out Vector2 offset, out float scale);

            Clone(panelRect, parent, out RectTransform root);
            if (root == null) return null;
            if (scale > 0.01f && scale < 10f) root.localScale = Vector3.one * scale;
            var capture = new CapturePanel
            {
                Root = root,
                Bar = Twin(panelRect, root, GameAccess.CargoCaptureBar(state)),
                BarImage = Twin(panelRect, root, GameAccess.CargoCaptureBarImage != null ? GameAccess.CargoCaptureBarImage(state) : null),
                PanelImage = Twin(panelRect, root, GameAccess.CargoCapturePanelImage != null ? GameAccess.CargoCapturePanelImage(state) : null),
                Title = Twin(panelRect, root, GameAccess.CargoAirbaseTitle(state)),
                Status = Twin(panelRect, root, GameAccess.CargoCaptureText(state)),
                Group = Twin(panelRect, root, GameAccess.CargoCaptureCanvasGroup != null ? GameAccess.CargoCaptureCanvasGroup(state) : null),
                Offset = offset,
            };
            if (capture.Bar == null || capture.Status == null || capture.Title == null)
            {
                Object.Destroy(root.gameObject);
                return null;
            }
            if (capture.Group == null) capture.Group = root.gameObject.AddComponent<CanvasGroup>();
            capture.Group.interactable = false;
            capture.Group.blocksRaycasts = false;
            capture.BarSize = capture.Bar.sizeDelta;
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.enabled = true;
            ModLog.Info("Capture panel from the cargo HUD: offset " + offset + ", size " + root.sizeDelta + ", scale " + root.localScale.x.ToString("F2"));
            return capture;
        }

        private static void PrefabPlacement(Transform prefabRoot, Transform part, Transform center, out Vector2 offset, out float scale)
        {
            Vector3 inRoot = Vector3.Scale(prefabRoot.localScale, prefabRoot.InverseTransformPoint(part.position));
            offset = (Vector2)(prefabRoot.localPosition + prefabRoot.localRotation * inRoot);
            scale = part.lossyScale.x / Mathf.Max(prefabRoot.lossyScale.x, 1e-5f) * prefabRoot.localScale.x;
            Canvas canvas = center != null ? center.GetComponentInParent<Canvas>(true) : null;
            if (canvas != null && canvas.rootCanvas.transform.lossyScale.x > 1e-5f)
            {
                float centerScale = center.lossyScale.x / canvas.rootCanvas.transform.lossyScale.x;
                offset *= centerScale;
                scale *= centerScale;
            }
        }

        private static T Twin<T>(Transform sourceRoot, Transform copyRoot, T sourceComponent) where T : Component
        {
            if (sourceComponent == null || sourceRoot == null || copyRoot == null) return null;
            Transform source = sourceComponent.transform;
            if (!source.IsChildOf(sourceRoot)) return null;
            var path = new List<int>();
            for (Transform t = source; t != sourceRoot; t = t.parent) path.Add(t.GetSiblingIndex());
            Transform target = copyRoot;
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (path[i] >= target.childCount) return null;
                target = target.GetChild(path[i]);
            }
            return target.GetComponent<T>();
        }

        public static TextMeshProUGUI CopyText(TextMeshProUGUI source, Transform parent)
        {
            if (source == null) return null;
            GameObject copy = Object.Instantiate(source.gameObject, parent, false);
            copy.name = source.gameObject.name + "_copy";
            for (int i = copy.transform.childCount - 1; i >= 0; i--) Object.Destroy(copy.transform.GetChild(i).gameObject);
            copy.SetActive(true);
            var text = copy.GetComponent<TextMeshProUGUI>();
            text.enabled = true;
            text.raycastTarget = false;
            return text;
        }

        public static WeaponPanel Weapon(Transform parent)
        {
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            WeaponStatus source = combatHud != null && GameAccess.CombatHudWeaponStatus != null ? GameAccess.CombatHudWeaponStatus(combatHud) : null;
            if (source == null || GameAccess.WeaponStatusName == null || GameAccess.WeaponStatusAmmo == null || GameAccess.WeaponStatusImage == null)
                return null;
            WeaponStatus copy = Clone(source, parent, out RectTransform root);
            copy.enabled = false;
            var panel = new WeaponPanel
            {
                Root = root,
                Icon = Own(root, GameAccess.WeaponStatusImage(copy)),
                Name = Own(root, GameAccess.WeaponStatusName(copy)),
                Ammo = Own(root, GameAccess.WeaponStatusAmmo(copy)),
                ReloadImage = Own(root, GameAccess.WeaponStatusReloadImage != null ? GameAccess.WeaponStatusReloadImage(copy) : null),
                Reload = Own(root, GameAccess.WeaponStatusReload != null ? GameAccess.WeaponStatusReload(copy) : null),
                Panel = Own(root, GameAccess.WeaponStatusPanel != null ? GameAccess.WeaponStatusPanel(copy) : null),
                Size = root.sizeDelta,
            };
            if (GameAccess.WeaponStatusSafety != null) Hide(Own(root, GameAccess.WeaponStatusSafety(copy)));
            Object.Destroy(copy);
            return panel.Ammo != null ? panel : null;
        }

        private static HUDApp HmdApp(HarmonyLib.AccessTools.FieldRef<HeadMountedDisplay, HUDApp> field)
        {
            HeadMountedDisplay hmd = SceneSingleton<HeadMountedDisplay>.i;
            return hmd != null && field != null ? field(hmd) : null;
        }

        private static HUDTurretState TurretState()
        {
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            GameObject turretUi = combatHud != null && GameAccess.CombatHudTurretUI != null ? GameAccess.CombatHudTurretUI(combatHud) : null;
            return turretUi != null ? turretUi.GetComponentInChildren<HUDTurretState>(true) : null;
        }

        private static T Clone<T>(T source, Transform parent, out RectTransform root) where T : Component
        {
            var sourceRect = source.transform as RectTransform;
            Vector2 size = sourceRect != null ? sourceRect.rect.size : Vector2.zero;
            Canvas sourceCanvas = source.GetComponentInParent<Canvas>(true);
            Transform canvasRoot = sourceCanvas != null ? sourceCanvas.rootCanvas.transform : null;
            float scale = canvasRoot != null && canvasRoot.lossyScale.x > 1e-5f ? source.transform.lossyScale.x / canvasRoot.lossyScale.x : 1f;
            if (scale <= 0.01f || scale > 10f) scale = 1f;
            GameObject copy = Object.Instantiate(source.gameObject, parent, false);
            copy.name = "Game_" + source.gameObject.name;
            root = copy.transform as RectTransform;
            if (root != null)
            {
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.localScale = Vector3.one * scale;
                root.localRotation = Quaternion.identity;
                if (size.x > 0f && size.y > 0f) root.sizeDelta = size;
                root.anchoredPosition = Vector2.zero;
            }
            foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            copy.SetActive(true);
            return copy.GetComponent<T>();
        }

        private static T Own<T>(RectTransform root, T component) where T : Component
        {
            return component != null && root != null && component.transform.IsChildOf(root) ? component : null;
        }

        private static void Hide(Behaviour behaviour)
        {
            if (behaviour != null) behaviour.enabled = false;
        }
    }
}
