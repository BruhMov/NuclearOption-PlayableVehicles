using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class CompassWidget : HudWidget
    {
        private const float Top = -30f;
        private const float GapBelowTape = 22f;
        private const float TapeSpan = 90f;

        private RawImage _tape;
        private RectTransform _tapeRect;
        private TextMeshProUGUI _turretHeading;
        private TextMeshProUGUI _hullMark;
        private TextMeshProUGUI _hullHeading;
        private int _turretShown = -1;
        private int _hullShown = -1;

        public override void Build(RectTransform root, HudContext context)
        {
            _tape = GameHudParts.Compass(root);
            GameHudParts.HmdBox bearing = _tape != null ? GameHudParts.Bearing(root) : null;
            if (_tape == null || bearing == null)
            {
                if (_tape != null) Object.Destroy(_tape.gameObject);
                _tape = null;
                BuildFallback(root);
                return;
            }

            _tapeRect = _tape.rectTransform;
            PinTop(_tapeRect, Top);

            PinTop(bearing.Root, Top - _tapeRect.sizeDelta.y - GapBelowTape);
            _turretHeading = bearing.Main;

            _hullMark = HudStyle.CreateText("HullMark", _tapeRect, new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(60f, 18f),
                14f, TextAlignmentOptions.Center);
            _hullMark.text = "HULL";
        }

        private void BuildFallback(RectTransform root)
        {
            HudStyle.CreateImage("Backing", root, new Vector2(0.5f, 1f), new Vector2(0f, Top - 40f), new Vector2(170f, 64f), HudStyle.Panel);
            _turretHeading = HudStyle.CreateText("TurretHeading", root, new Vector2(0.5f, 1f), new Vector2(0f, Top - 28f), new Vector2(160f, 32f),
                26f, TextAlignmentOptions.Center);
            _hullHeading = HudStyle.CreateText("HullHeading", root, new Vector2(0.5f, 1f), new Vector2(0f, Top - 56f), new Vector2(160f, 24f),
                18f, TextAlignmentOptions.Center);
            _hullHeading.color = HudStyle.WithAlpha(HudStyle.Normal, 0.85f);
        }

        private static void PinTop(RectTransform rect, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        public override void Refresh(HudContext context)
        {
            Turret turret = context.Turrets.PrimaryTurret;
            float hullYaw = context.Vehicle.transform.eulerAngles.y;
            float turretYaw = turret != null ? turret.transform.eulerAngles.y : hullYaw;

            int turretHeading = Heading(turretYaw);
            if (turretHeading != _turretShown)
            {
                _turretShown = turretHeading;
                _turretHeading.text = turretHeading.ToString("D3") + "°";
            }
            if (_hullHeading != null)
            {
                int hullHeading = Heading(hullYaw);
                if (hullHeading != _hullShown)
                {
                    _hullShown = hullHeading;
                    _hullHeading.text = "HULL " + hullHeading.ToString("D3") + "°";
                }
            }
            if (_tape == null) return;

            _tape.uvRect = new Rect((turretYaw + 135f) / 360f, 0f, TapeSpan / 360f, 1f);

            float offset = Mathf.DeltaAngle(turretYaw, hullYaw) / TapeSpan;
            bool onTape = Mathf.Abs(offset) <= 0.5f;
            if (_hullMark.gameObject.activeSelf != onTape) _hullMark.gameObject.SetActive(onTape);
            if (onTape) _hullMark.rectTransform.anchoredPosition = new Vector2(offset * _tapeRect.sizeDelta.x, -10f);
        }

        private static int Heading(float yaw)
        {
            int heading = Mathf.RoundToInt(yaw) % 360;
            if (heading < 0) heading += 360;
            return heading;
        }
    }
}
