using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class StatusWidget : HudWidget
    {
        private const float DefaultWidth = 260f;
        private const float Height = 38f;
        private const float LabelIndent = 12f;
        private const float GapAboveMap = 8f;
        private static readonly Vector2 FallbackPosition = new Vector2(40f, 330f);

        private readonly List<UnitPart> _parts = new List<UnitPart>();
        private RectTransform _area;
        private TextMeshProUGUI _label;
        private RectTransform _barFill;
        private Image _barFillImage;

        private const float LogisticsHeight = 28f;
        private const float LogisticsGap = 4f;
        private const float ProbeInterval = 0.25f;
        private GameObject _logistics;
        private TextMeshProUGUI _logisticsLabel;
        private float _nextProbe;
        private GameHudParts.ArcGauge _gauge;
        private int _hullShown = -1;
        private int _gaugeShown = -1;
        private Gradient _gaugeGradient;

        public override void Build(RectTransform root, HudContext context)
        {
            CollectParts(context.Vehicle);

            _area = HudStyle.CreateRect("Status", root, Vector2.zero, FallbackPosition, new Vector2(DefaultWidth, Height));
            _area.pivot = Vector2.zero;
            _area.anchoredPosition = FallbackPosition;

            _gauge = GameHudParts.Fuel(root);
            if (_gauge != null)
            {
                _gauge.Root.anchoredPosition = _gauge.Offset;
                if (_gauge.Label != null) _gauge.Label.text = "HULL";
                _gaugeGradient = ThemeGradient();
            }
            else BuildBar();

            if (Towing.IsTractor(context.Vehicle) || Cargo.IsFlatbed(context.Vehicle)) BuildLogistics(_gauge != null ? 0f : Height + LogisticsGap);
        }

        private static Gradient ThemeGradient()
        {
            try
            {
                return NuclearOption.UIStyleSystem.ThemeManager.Active.ColorTheme.Gradient(new List<float> { 0f, 0f, 0.5f, 0.5f, 1f, 1f },
                    new List<float> { 0f, 0.099f, 0.1f, 0.339f, 0.34f, 1f });
            }
            catch
            {
                return null;
            }
        }

        private void BuildBar()
        {
            Image backing = HudStyle.CreateImage("Backing", _area, Vector2.zero, Vector2.zero, Vector2.zero, HudStyle.Panel);
            Stretch(backing.rectTransform, 0f, Height);

            _label = HudStyle.CreateText("HullLabel", _area, Vector2.zero, Vector2.zero, new Vector2(DefaultWidth, 26f),
                20f, TextAlignmentOptions.BottomLeft);
            Stretch(_label.rectTransform, 12f, Height);
            _label.rectTransform.offsetMin = new Vector2(LabelIndent, 12f);

            Image back = HudStyle.CreateImage("BarBack", _area, Vector2.zero, Vector2.zero, Vector2.zero, HudStyle.Panel);
            Stretch(back.rectTransform, 0f, 8f);

            _barFillImage = HudStyle.CreateImage("BarFill", _area, Vector2.zero, Vector2.zero, Vector2.zero, HudStyle.Normal);
            _barFill = _barFillImage.rectTransform;
            Stretch(_barFill, 0f, 8f);
        }

        private void BuildLogistics(float bottom)
        {
            GameHudParts.HmdBox box = GameHudParts.Bearing(_area);
            if (box != null)
            {
                _logistics = box.Root.gameObject;
                Stretch(box.Root, bottom, bottom + LogisticsHeight);
                _logisticsLabel = box.Main;
                float size = _logisticsLabel.fontSize;
                _logisticsLabel.enableAutoSizing = true;
                _logisticsLabel.fontSizeMax = size;
                _logisticsLabel.fontSizeMin = size * 0.6f;
            }
            else
            {
                Image logisticsBacking = HudStyle.CreateImage("Logistics", _area, Vector2.zero, Vector2.zero, Vector2.zero, HudStyle.Panel);
                Stretch(logisticsBacking.rectTransform, bottom, bottom + LogisticsHeight);
                _logistics = logisticsBacking.gameObject;
                _logisticsLabel = HudStyle.CreateText("LogisticsLabel", logisticsBacking.rectTransform, Vector2.zero, Vector2.zero,
                    new Vector2(DefaultWidth, LogisticsHeight), 17f, TextAlignmentOptions.MidlineLeft);
            }
            RectTransform labelRect = _logisticsLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(LabelIndent, 0f);
            labelRect.offsetMax = new Vector2(-6f, 0f);
            _logisticsLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _logisticsLabel.enableWordWrapping = false;
            _logisticsLabel.overflowMode = TextOverflowModes.Ellipsis;
            _logistics.SetActive(false);
        }

        private static void Stretch(RectTransform rect, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.zero;
            rect.offsetMin = new Vector2(0f, bottom);
            rect.offsetMax = new Vector2(0f, top);
        }

        private void CollectParts(GroundVehicle vehicle)
        {
            _parts.Clear();
            UnitPart[] all = vehicle.GetComponentsInChildren<UnitPart>(true);
            foreach (UnitPart part in all)
            {
                if (part != null && GameAccess.UnitPartCriticalPart(part)) _parts.Add(part);
            }
            if (_parts.Count == 0)
            {
                foreach (UnitPart part in all)
                {
                    if (part != null) _parts.Add(part);
                }
            }
        }

        public override void Refresh(HudContext context)
        {
            Place(context);

            float hull = 100f;
            bool any = false;
            foreach (UnitPart part in _parts)
            {
                if (part == null) continue;
                hull = Mathf.Min(hull, part.hitPoints);
                any = true;
            }
            hull = any ? Mathf.Clamp(hull, 0f, 100f) : 100f;

            if (_gauge != null)
            {
                KeepGaugeClear(context);
                RefreshGauge(hull / 100f);
            }
            else
            {
                Color color = hull > 60f ? HudStyle.Normal : hull > 30f ? HudStyle.Warning : HudStyle.Alert;
                int percent = Mathf.RoundToInt(hull);
                if (percent != _hullShown)
                {
                    _hullShown = percent;
                    _label.text = "HULL " + percent + "%";
                }
                _label.color = color;
                _barFillImage.color = color;
                _barFill.anchorMax = new Vector2(hull / 100f, 0f);
                _barFill.offsetMax = new Vector2(0f, 8f);
            }

            if (_logistics != null && Time.unscaledTime >= _nextProbe)
            {
                _nextProbe = Time.unscaledTime + ProbeInterval;
                RefreshLogistics(context.Vehicle);
            }
        }

        private const float GaugeGap = 16f;
        private readonly Vector3[] _arcCorners = new Vector3[4];

        private const float MaxGaugeShift = 200f;
        private const float ArcHalfWidth = 14f;
        private bool _gaugePlaced;

        private const float GaugeSettleTime = 0.5f;
        private float _gaugeShownAt = -1f;

        private void KeepGaugeClear(HudContext context)
        {
            if (_gaugePlaced || float.IsNaN(context.SpeedBoxRight)) return;
            if (_gaugeShownAt < 0f) _gaugeShownAt = Time.unscaledTime;
            if (Time.unscaledTime - _gaugeShownAt < GaugeSettleTime) return;
            _gaugePlaced = true;
            Graphic marker = _gauge.Label != null ? (Graphic)_gauge.Label : _gauge.Pointer;
            if (marker == null) return;
            marker.rectTransform.GetWorldCorners(_arcCorners);
            float arcCenter = ((_arcCorners[0].x + _arcCorners[2].x) * 0.5f - Screen.width * 0.5f) / context.ScaleFactor;
            float shift = Mathf.Clamp(context.SpeedBoxRight + GaugeGap + ArcHalfWidth - arcCenter, 0f, MaxGaugeShift);
            ModLog.Info("Hull gauge: arc at " + arcCenter.ToString("F0") + ", speed box right edge "
                + context.SpeedBoxRight.ToString("F0") + ", shift " + shift.ToString("F0"));
            if (shift > 0.5f) _gauge.Root.anchoredPosition += new Vector2(shift, 0f);
        }

        private void RefreshGauge(float level)
        {
            Color color = _gaugeGradient != null ? _gaugeGradient.Evaluate(level)
                : level > 0.34f ? HudStyle.Normal : level > 0.1f ? HudStyle.Warning : HudStyle.Alert;
            _gauge.Bar.fillAmount = level;
            _gauge.Bar.color = color;
            if (_gauge.Pivot != null) _gauge.Pivot.localEulerAngles = new Vector3(0f, 0f, -(level * 28f - 14f));
            if (_gauge.Pointer != null) _gauge.Pointer.color = color;
            if (_gauge.Label != null) _gauge.Label.color = color;
            int percent = Mathf.RoundToInt(level * 100f);
            if (percent != _gaugeShown)
            {
                _gaugeShown = percent;
                _gauge.Reading.text = percent + "%";
            }
            _gauge.Reading.color = color;
        }

        private void RefreshLogistics(GroundVehicle vehicle)
        {
            string key = "[" + ModConfig.DeployKey.Value + "] ";
            string text = null;
            Color color = HudStyle.Normal;
            bool client = NetBridge.IsClientOnly;
            ClientLoad clientLoad = client ? LogisticsLink.ClientLoadOf(vehicle) : null;
            bool clientLoaded = clientLoad != null && clientLoad.Active;
            string clientLoadName = clientLoaded && clientLoad.Unit != null ? clientLoad.Unit.unitName : null;
            if (Towing.IsTractor(vehicle))
            {
                TowHitch hitch = client ? null : Towing.HitchOf(vehicle);
                if (hitch != null && hitch.Trailer != null)
                {
                    text = "TOW  " + hitch.Trailer.unitName + "   " + Dim(key + "unhitch");
                }
                else if (clientLoaded)
                {
                    text = "TOW  " + (clientLoadName ?? "trailer") + "   " + Dim(key + "unhitch");
                }
                else
                {
                    HitchCheck check = Towing.FindTrailer(vehicle, out GroundVehicle trailer);
                    if (check == HitchCheck.Ready) text = key + "Hitch " + trailer.unitName;
                    else if (check == HitchCheck.Misaligned)
                    {
                        text = "Line up with " + trailer.unitName;
                        color = HudStyle.Warning;
                    }
                    else if (check == HitchCheck.Uneven)
                    {
                        text = trailer.unitName + ": find flatter ground";
                        color = HudStyle.Warning;
                    }
                }
            }
            else if (Cargo.IsFlatbed(vehicle))
            {
                CargoMount load = client ? null : Cargo.LoadOf(vehicle);
                FlatbedBed bed;
                if (load != null && load.Container != null)
                {
                    text = "CARGO  " + load.Container.unitName + "   " + Dim(key + "unload");
                }
                else if (clientLoaded)
                {
                    text = "CARGO  " + (clientLoadName ?? "container") + "   " + Dim(key + "unload");
                }
                else if ((bed = Cargo.BedOf(vehicle)) != null)
                {
                    GroundVehicle container = Cargo.FindContainer(vehicle, bed, out GroundVehicle tooBig, null);
                    if (container != null) text = key + "Load " + container.unitName;
                    else if (tooBig != null)
                    {
                        text = tooBig.unitName + " does not fit";
                        color = HudStyle.Warning;
                    }
                }
            }

            bool show = text != null;
            if (_logistics.activeSelf != show) _logistics.SetActive(show);
            if (!show) return;
            if (_logisticsLabel.text != text) _logisticsLabel.text = text;
            _logisticsLabel.color = color;
        }

        private static string Dim(string text)
        {
            return "<alpha=#80>" + text + "<alpha=#FF>";
        }

        private const float PlaceInterval = 0.25f;
        private float _nextPlace;
        private int _placedWidth;
        private int _placedHeight;
        private float _placedScale;

        private void Place(HudContext context)
        {
            float now = Time.unscaledTime;
            if (now < _nextPlace && Screen.width == _placedWidth && Screen.height == _placedHeight && context.ScaleFactor == _placedScale) return;
            _nextPlace = now + PlaceInterval;
            _placedWidth = Screen.width;
            _placedHeight = Screen.height;
            _placedScale = context.ScaleFactor;

            if (GameUiLinks.TryGetScreenRect(GameUiLinks.MinimapFrame(), out Rect map))
            {
                float scale = context.ScaleFactor;
                _area.position = new Vector3(map.xMin, map.yMax + GapAboveMap * scale, 0f);
                _area.sizeDelta = new Vector2(map.width / scale, Height);
            }
            else
            {
                _area.anchoredPosition = FallbackPosition;
                _area.sizeDelta = new Vector2(DefaultWidth, Height);
            }
        }
    }
}
