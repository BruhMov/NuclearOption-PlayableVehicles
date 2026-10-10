using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class CaptureWidget : HudWidget
    {
        private const float CheckInterval = 1f;
        private static readonly Vector2 FallbackOffset = new Vector2(0f, -300f);
        private static readonly Vector2 FallbackSize = new Vector2(360f, 56f);

        private GameHudParts.CapturePanel _panel;
        private TextMeshProUGUI _hint;

        private Airbase _airbase;
        private float _lastCheck = float.NegativeInfinity;
        private float _fadeTarget;
        private float _fadeSmoothed;
        private float _progressSmoothed = 0.5f;
        private float _progressVelocity;

        public override void Build(RectTransform root, HudContext context)
        {
            _panel = GameHudParts.Capture(root);
            if (_panel != null) _panel.Root.anchoredPosition = _panel.Offset;
            else _panel = BuildFallback(root);

            _hint = CreateHint(_panel);
            _panel.Root.gameObject.SetActive(false);
            _panel.Group.alpha = 0f;
        }

        public override void Refresh(HudContext context)
        {
            GroundVehicle vehicle = context.Vehicle;
            if (Time.timeSinceLevelLoad - _lastCheck >= CheckInterval || Time.timeSinceLevelLoad < _lastCheck)
            {
                _lastCheck = Time.timeSinceLevelLoad;
                RefreshNearestAirbase(vehicle);
            }

            GameObject panel = _panel.Root.gameObject;
            if (_fadeTarget == 0f && _fadeSmoothed <= 0f)
            {
                if (panel.activeSelf) panel.SetActive(false);
                return;
            }
            if (!panel.activeSelf) panel.SetActive(true);

            _fadeSmoothed += Mathf.Clamp(_fadeTarget - _fadeSmoothed, -2f * Time.deltaTime, 2f * Time.deltaTime);
            _panel.Group.alpha = _fadeSmoothed;
            if (_fadeTarget > 0f && _airbase != null && _airbase.capture != null)
            {
                float balance = _airbase.capture.controlBalance;
                _progressSmoothed = balance < 1f
                    ? Mathf.SmoothDamp(_progressSmoothed, balance, ref _progressVelocity, 1f)
                    : 1f;
                _panel.Bar.sizeDelta = _panel.BarSize * new Vector2(_progressSmoothed, 1f);
            }
        }

        private void RefreshNearestAirbase(GroundVehicle vehicle)
        {
            _airbase = null;
            float nearest = float.MaxValue;
            GlobalPosition position = vehicle.GlobalPosition();
            foreach (KeyValuePair<string, Airbase> pair in FactionRegistry.airbaseLookup)
            {
                Airbase airbase = pair.Value;
                if (airbase == null || airbase.center == null || airbase.capture == null) continue;
                if (FastMath.InRange(airbase.center.GlobalPosition(), position, Mathf.Min(airbase.GetRadius(), nearest)))
                {
                    nearest = FastMath.Distance(airbase.center.GlobalPosition(), position);
                    _airbase = airbase;
                }
            }
            _fadeTarget = _airbase != null ? 1f : 0f;
            if (_airbase == null) return;

            FactionHQ ownHq = vehicle.NetworkHQ;
            FactionHQ baseHq = _airbase.CurrentHQ;
            float balance = _airbase.capture.controlBalance;

            string tag = baseHq != null ? baseHq.faction.factionTag : ownHq != null ? ownHq.faction.factionTag : "";
            string action = balance == 1f ? "" : baseHq != null ? " Defending" : " Capturing";
            string percent = balance < 1f ? string.Format(" {0:F0}%", balance * 100f) : "";
            SetText(_panel.Status, "(" + tag + action + percent + ")");
            SetText(_panel.Title, _airbase.SavedAirbase != null ? _airbase.SavedAirbase.DisplayName : _airbase.name);

            Color color = baseHq == null || baseHq == ownHq ? HudStyle.Normal : HudStyle.Alert + HudStyle.Normal * 0.25f;
            if (_panel.BarImage != null) _panel.BarImage.color = color;
            if (_panel.PanelImage != null) _panel.PanelImage.color = color.MultiplyRGB(0.1f, 0.7f);
            _panel.Title.color = color;
            _panel.Status.color = color;

            bool cannotCapture = vehicle.CaptureStrength <= 0f && (baseHq != ownHq || balance < 1f);
            if (_hint != null && _hint.gameObject.activeSelf != cannotCapture) _hint.gameObject.SetActive(cannotCapture);
            if (_hint != null) _hint.color = HudStyle.Warning;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }

        private static TextMeshProUGUI CreateHint(GameHudParts.CapturePanel panel)
        {
            TextMeshProUGUI hint = GameHudParts.CopyText(panel.Status as TextMeshProUGUI, panel.Root);
            if (hint == null)
            {
                hint = HudStyle.CreateText("CaptureHint", panel.Root, Vector2.zero, Vector2.zero, Vector2.zero, 18f, TextAlignmentOptions.Center);
            }
            RectTransform rect = hint.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(Mathf.Max(panel.Root.sizeDelta.x * 1.5f, 300f), Mathf.Max(panel.Status.rectTransform.rect.height, 24f));
            rect.anchoredPosition = new Vector2(0f, -4f);
            hint.alignment = TextAlignmentOptions.Center;
            hint.enableWordWrapping = false;
            hint.overflowMode = TextOverflowModes.Overflow;
            hint.text = "this vehicle can't capture";
            hint.color = HudStyle.Warning;
            hint.gameObject.SetActive(false);
            return hint;
        }

        private static GameHudParts.CapturePanel BuildFallback(RectTransform root)
        {
            Image backing = HudStyle.CreateImage("CapturePanel", root, new Vector2(0.5f, 0.5f), FallbackOffset, FallbackSize, HudStyle.Panel);
            RectTransform panel = backing.rectTransform;

            Image bar = HudStyle.CreateImage("CaptureBar", panel, new Vector2(0f, 0f), Vector2.zero, new Vector2(FallbackSize.x, 6f), HudStyle.Normal);
            bar.rectTransform.pivot = Vector2.zero;
            bar.rectTransform.anchoredPosition = Vector2.zero;

            TextMeshProUGUI title = HudStyle.CreateText("AirbaseTitle", panel, new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                new Vector2(FallbackSize.x - 12f, 26f), 22f, TextAlignmentOptions.Center);
            TextMeshProUGUI status = HudStyle.CreateText("CaptureText", panel, new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                new Vector2(FallbackSize.x - 12f, 22f), 18f, TextAlignmentOptions.Center);

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            return new GameHudParts.CapturePanel
            {
                Root = panel,
                Bar = bar.rectTransform,
                BarImage = bar,
                PanelImage = backing,
                Title = title,
                Status = status,
                Group = group,
                BarSize = bar.rectTransform.sizeDelta,
                Offset = FallbackOffset,
            };
        }
    }
}
