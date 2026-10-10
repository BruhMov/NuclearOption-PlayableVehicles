using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class UnitMarkersWidget : HudWidget
    {
        private const float SyncInterval = 0.5f;
        private const float OutdatedThreshold = 20f;
        private const float PositionThreshold = 0.5f;
        private const float ScaleThreshold = 0.005f;

        private sealed class Marker
        {
            public Unit Unit;
            public Image Image;
            public RectTransform Rect;
            public Sprite Icon;
            public Color Color;
            public bool Enemy;
            public bool AlwaysMaximized;
            public float CustomScale;
            public bool CategoryMaximized;
            public FactionMode Mode;
            public bool Outdated;

            public bool HasPosition;
            public float X, Y;
            public float Scale;
            public Color WrittenColor;
            public bool HasColor;
        }

        private readonly Dictionary<Unit, Marker> _markers = new Dictionary<Unit, Marker>();
        private readonly HashSet<Unit> _wanted = new HashSet<Unit>();
        private readonly List<Unit> _toRemove = new List<Unit>();
        private RectTransform _layer;
        private Vector2 _baseSize = new Vector2(1f, 1f);
        private GameObject _prefab;
        private float _scaleRatio = 1f;
        private float _nextSync;
        private Sprite _minimizedHostile;
        private bool _redescribe;
        private HUDOptions _options;
        private bool _subscribed;

        public override void Build(RectTransform root, HudContext context)
        {
            _layer = HudStyle.CreateFill("UnitMarkers", root);
            _layer.gameObject.AddComponent<Canvas>();

            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud != null)
            {
                _minimizedHostile = combatHud.minimizedHostile;
                GameObject prefab = GameAccess.CombatHudUnitMarkerPrefab != null ? GameAccess.CombatHudUnitMarkerPrefab(combatHud) : null;
                if (prefab != null && prefab.GetComponent<Image>() != null) _prefab = prefab;
                if (prefab != null && prefab.transform is RectTransform prefabRect && prefabRect.sizeDelta.x > 0f)
                {
                    _baseSize = prefabRect.sizeDelta;
                }
            }
            _nextSync = 0f;

            NuclearOption.UIStyleSystem.ThemeManager.ThemeGroupChanged += OnStyleChanged;
            PlayerSettings.OnApplyOptions += OnStyleChanged;
            _options = SceneSingleton<HUDOptions>.i;
            if (_options != null) _options.OnApplyOptions += OnStyleChanged;
            _subscribed = true;
        }

        private void OnStyleChanged()
        {
            _redescribe = true;
            _nextSync = 0f;
        }

        public override void Refresh(HudContext context)
        {
            GroundVehicle vehicle = context.Vehicle;
            FactionHQ hq = vehicle.NetworkHQ;
            if (hq == null)
            {
                SetAllVisible(false);
                return;
            }

            if (Time.unscaledTime >= _nextSync)
            {
                _nextSync = Time.unscaledTime + SyncInterval;
                Sync(hq, vehicle);
                UpdateScaleRatio();
            }

            Camera camera = context.Camera;
            Transform cameraTransform = camera.transform;
            GlobalPosition viewPosition = cameraTransform.GlobalPosition();
            float scaleFactor = _scaleRatio * ModConfig.UnitMarkerScale.Value;

            foreach (Marker marker in _markers.Values)
            {
                UpdateMarker(marker, hq, camera, viewPosition, scaleFactor);
            }
        }

        private void Sync(FactionHQ hq, GroundVehicle vehicle)
        {
            _wanted.Clear();
            foreach (PersistentID id in hq.factionUnits)
            {
                AddWanted(id, vehicle);
            }
            foreach (PersistentID id in hq.trackingDatabase.Keys)
            {
                AddWanted(id, vehicle);
            }

            _toRemove.Clear();
            foreach (KeyValuePair<Unit, Marker> pair in _markers)
            {
                if (pair.Key == null || !_wanted.Contains(pair.Key)) _toRemove.Add(pair.Key);
            }
            foreach (Unit unit in _toRemove)
            {
                if (_markers.TryGetValue(unit, out Marker marker) && marker.Image != null) Object.Destroy(marker.Image.gameObject);
                _markers.Remove(unit);
            }

            bool redescribe = _redescribe;
            _redescribe = false;
            foreach (Unit unit in _wanted)
            {
                bool created = false;
                if (!_markers.TryGetValue(unit, out Marker marker))
                {
                    Image image = CreateImage(unit);
                    marker = new Marker { Unit = unit, Image = image, Rect = image.rectTransform };
                    _markers.Add(unit, marker);
                    created = true;
                }
                FactionMode mode = DynamicMap.GetFactionMode(unit.NetworkHQ, false);
                if (created || redescribe || mode != marker.Mode) Describe(marker, mode);
                marker.Outdated = !hq.IsTargetPositionAccurate(unit, OutdatedThreshold);
            }
        }

        private void AddWanted(PersistentID id, GroundVehicle vehicle)
        {
            if (!UnitRegistry.TryGetUnit(new PersistentID?(id), out Unit unit)) return;
            if (unit == null || unit.disabled || unit == vehicle || unit is Scenery) return;
            if (unit is PilotDismounted && SceneSingleton<MapOptions>.i != null && !SceneSingleton<MapOptions>.i.showPilotIcons) return;
            _wanted.Add(unit);
        }

        private Image CreateImage(Unit unit)
        {
            Image image;
            if (_prefab != null)
            {
                GameObject go = Object.Instantiate(_prefab, _layer);
                go.name = unit.unitName;
                image = go.GetComponent<Image>();
            }
            else
            {
                image = HudStyle.CreateImage(unit.unitName, _layer, new Vector2(0.5f, 0.5f), Vector2.zero, _baseSize, Color.clear);
                image.preserveAspect = true;
            }
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private static void Describe(Marker marker, FactionMode mode)
        {
            Unit unit = marker.Unit;
            UnitDefinition definition = unit.definition;

            Color neutral = Color.white, friendly = Color.green, hostile = Color.red;
            try
            {
                var theme = NuclearOption.UIStyleSystem.ThemeManager.Active?.ColorTheme;
                if (theme != null)
                {
                    neutral = theme.HudUnitNeutral;
                    friendly = theme.HudUnitFriendly;
                    hostile = theme.HudUnitHostile;
                }
            }
            catch { }

            marker.Mode = mode;
            switch (mode)
            {
                case FactionMode.Enemy:
                    marker.Icon = definition != null ? definition.hostileIcon : null;
                    marker.Color = hostile;
                    marker.Enemy = true;
                    break;
                case FactionMode.Friendly:
                    marker.Icon = definition != null ? definition.friendlyIcon : null;
                    marker.Color = friendly;
                    marker.Enemy = false;
                    break;
                default:
                    marker.Icon = definition != null ? definition.friendlyIcon : null;
                    marker.Color = neutral;
                    marker.Enemy = false;
                    break;
            }

            marker.AlwaysMaximized = unit is Aircraft;
            marker.CustomScale = (definition != null ? definition.iconSize : 1f) * PlayerSettings.hmdIconSize;
            HUDOptions options = SceneSingleton<HUDOptions>.i;
            marker.CategoryMaximized = options == null || options.CheckMaximizeIcon(unit) > 0f;
        }

        private void UpdateMarker(Marker marker, FactionHQ hq, Camera camera, GlobalPosition viewPosition, float scaleFactor)
        {
            Unit unit = marker.Unit;
            Image image = marker.Image;
            if (unit == null || image == null || unit.disabled || !hq.TryGetKnownPosition(unit, out GlobalPosition known))
            {
                SetVisible(image, false);
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(known.ToLocalPosition());
            if (screen.z <= 0f)
            {
                SetVisible(image, false);
                return;
            }
            SetVisible(image, true);

            if (!marker.HasPosition || Mathf.Abs(screen.x - marker.X) >= PositionThreshold || Mathf.Abs(screen.y - marker.Y) >= PositionThreshold)
            {
                marker.HasPosition = true;
                marker.X = screen.x;
                marker.Y = screen.y;
                marker.Rect.position = new Vector3(screen.x, screen.y, 0f);
            }

            float distance = FastMath.Distance(viewPosition, known);
            bool maximized;
            float scale;
            Sprite sprite;
            if (marker.AlwaysMaximized)
            {
                maximized = true;
                scale = marker.CustomScale * Mathf.Lerp(1f, 0.45f, distance * 4E-05f - 0.5f);
                sprite = marker.Icon;
            }
            else
            {
                float range = marker.Enemy ? ModConfig.EnemyIconRange.Value : ModConfig.FriendlyIconRange.Value;
                maximized = marker.CategoryMaximized && distance <= range;
                if (maximized)
                {
                    scale = marker.CustomScale;
                    sprite = marker.Icon;
                }
                else if (marker.Enemy)
                {
                    scale = 6f;
                    sprite = _minimizedHostile;
                }
                else
                {
                    scale = 3f;
                    sprite = null;
                }
            }

            if (image.sprite != sprite) image.sprite = sprite;

            float finalScale = scale * scaleFactor;
            if (marker.Scale == 0f || Mathf.Abs(finalScale - marker.Scale) > ScaleThreshold * marker.Scale)
            {
                marker.Scale = finalScale;
                marker.Rect.localScale = Vector3.one * finalScale;
            }

            Color color = HudStyle.WithAlpha(marker.Color, marker.Outdated && maximized ? 0.5f : 1f);
            if (!marker.HasColor || color != marker.WrittenColor)
            {
                marker.HasColor = true;
                marker.WrittenColor = color;
                image.color = color;
            }
        }

        private void UpdateScaleRatio()
        {
            _scaleRatio = 1f;
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null || combatHud.iconLayer == null) return;

            Canvas canvas = combatHud.iconLayer.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return;

            float theirs = combatHud.iconLayer.lossyScale.x;
            float ours = _layer.lossyScale.x;
            if (theirs > 0f && ours > 0f) _scaleRatio = theirs / ours;
        }

        private void SetAllVisible(bool visible)
        {
            foreach (Marker marker in _markers.Values)
            {
                SetVisible(marker.Image, visible);
            }
        }

        private static void SetVisible(Image image, bool visible)
        {
            if (image != null && image.enabled != visible) image.enabled = visible;
        }

        public override void Dispose()
        {
            if (_subscribed)
            {
                NuclearOption.UIStyleSystem.ThemeManager.ThemeGroupChanged -= OnStyleChanged;
                PlayerSettings.OnApplyOptions -= OnStyleChanged;
                if (_options != null) _options.OnApplyOptions -= OnStyleChanged;
                _subscribed = false;
            }
            _options = null;
            _markers.Clear();
            _wanted.Clear();
        }
    }
}
