using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class LockWidget : HudWidget
    {
        private const float BoxSize = 56f;
        private const float CornerLength = 14f;
        private const float LineWidth = 2f;
        private const float MinLeadPixels = 6f;

        private RectTransform _box;
        private RectTransform _lead;
        private TextMeshProUGUI _label;
        private Image[] _boxLines;
        private Image _boxSprite;
        private Image[] _leadLines;
        private Unit _labelTarget;
        private string _labelName;
        private readonly DistanceText _labelDistance = new DistanceText();
        private string _labelDistanceShown;
        private string _labelText;

        public override void Build(RectTransform root, HudContext context)
        {
            _box = HudStyle.CreateRect("LockBox", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BoxSize, BoxSize));
            float h = BoxSize / 2f;
            float c = CornerLength / 2f;
            Sprite targetSprite = GameAssets.i != null ? GameAssets.i.targetUnitSprite : null;
            if (targetSprite != null)
            {
                _boxSprite = HudStyle.CreateImage("TargetBox", _box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BoxSize, BoxSize), HudStyle.Alert);
                _boxSprite.sprite = targetSprite;
                _boxSprite.preserveAspect = true;
                _boxLines = new Image[] { _boxSprite };
            }
            else _boxLines = new[]
            {
                Line(_box, new Vector2(-h + c, h), new Vector2(CornerLength, LineWidth)),
                Line(_box, new Vector2(-h, h - c), new Vector2(LineWidth, CornerLength)),
                Line(_box, new Vector2(h - c, h), new Vector2(CornerLength, LineWidth)),
                Line(_box, new Vector2(h, h - c), new Vector2(LineWidth, CornerLength)),
                Line(_box, new Vector2(-h + c, -h), new Vector2(CornerLength, LineWidth)),
                Line(_box, new Vector2(-h, -h + c), new Vector2(LineWidth, CornerLength)),
                Line(_box, new Vector2(h - c, -h), new Vector2(CornerLength, LineWidth)),
                Line(_box, new Vector2(h, -h + c), new Vector2(LineWidth, CornerLength)),
            };

            _label = HudStyle.CreateText("LockLabel", _box, new Vector2(0.5f, 1f), new Vector2(0f, 22f), new Vector2(320f, 40f),
                16f, TextAlignmentOptions.Bottom);

            _lead = HudStyle.CreateRect("Lead", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            _lead.localEulerAngles = new Vector3(0f, 0f, 45f);
            _leadLines = new[]
            {
                Line(_lead, new Vector2(0f, 8f), new Vector2(16f, LineWidth)),
                Line(_lead, new Vector2(0f, -8f), new Vector2(16f, LineWidth)),
                Line(_lead, new Vector2(-8f, 0f), new Vector2(LineWidth, 16f)),
                Line(_lead, new Vector2(8f, 0f), new Vector2(LineWidth, 16f)),
            };

            _box.gameObject.SetActive(false);
            _lead.gameObject.SetActive(false);
        }

        private static Image Line(RectTransform parent, Vector2 position, Vector2 size)
        {
            return HudStyle.CreateImage("Line", parent, new Vector2(0.5f, 0.5f), position, size, HudStyle.Alert);
        }

        public override void Refresh(HudContext context)
        {
            TurretController turrets = context.Turrets;
            Unit target = turrets.LockedTarget;
            if (target == null)
            {
                SetActive(_box, false);
                SetActive(_lead, false);
                return;
            }

            Camera camera = context.Camera;
            Vector3 targetPoint = TargetLock.AimPoint(target);
            Vector3 targetScreen = camera.WorldToScreenPoint(targetPoint);
            bool targetVisible = targetScreen.z > 0f;
            SetActive(_box, targetVisible);
            if (targetVisible)
            {
                _box.position = new Vector3(targetScreen.x, targetScreen.y, 0f);
                float distance = Vector3.Distance(context.Vehicle.transform.position, targetPoint);
                string reading = _labelDistance.Get(distance);
                if (_labelText == null || target != _labelTarget || target.unitName != _labelName || !ReferenceEquals(reading, _labelDistanceShown))
                {
                    _labelTarget = target;
                    _labelName = target.unitName;
                    _labelDistanceShown = reading;
                    _labelText = _labelName + "\n<size=80%>TGT LOCKED  " + reading + "</size>";
                    _label.text = _labelText;
                }
            }

            Color color = turrets.Range == TurretController.RangeState.OutOfReach || turrets.Range == TurretController.RangeState.BeyondMaxRange
                ? HudStyle.Alert
                : HudStyle.Warning;
            SetColor(_boxLines, color);
            _label.color = color;

            Vector3 leadScreen = camera.WorldToScreenPoint(turrets.LeadPoint);
            bool leadVisible = targetVisible && leadScreen.z > 0f
                && Vector2.Distance(new Vector2(leadScreen.x, leadScreen.y), new Vector2(targetScreen.x, targetScreen.y)) >= MinLeadPixels;
            SetActive(_lead, leadVisible);
            if (leadVisible)
            {
                _lead.position = new Vector3(leadScreen.x, leadScreen.y, 0f);
                SetColor(_leadLines, color);
            }
        }

        private static void SetActive(RectTransform rect, bool active)
        {
            if (rect.gameObject.activeSelf != active) rect.gameObject.SetActive(active);
        }

        private static void SetColor(Image[] lines, Color color)
        {
            foreach (Image line in lines)
            {
                line.color = color;
            }
        }
    }
}
