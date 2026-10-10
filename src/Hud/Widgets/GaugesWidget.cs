using TMPro;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class GaugesWidget : HudWidget
    {
        private TextMeshProUGUI _speed;
        private TextMeshProUGUI _range;
        private TextMeshProUGUI _rangeStatus;
        private RectTransform _speedRoot;
        private RectTransform _rangeRoot;
        private readonly SpeedGroundText _speedText = new SpeedGroundText();
        private readonly DistanceText _distanceText = new DistanceText();
        private readonly DistanceText _maxText = new DistanceText("MAX ");
        private readonly DistanceText _minText = new DistanceText("MIN ");
        private float _placedSide = float.NaN;

        public override void Build(RectTransform root, HudContext context)
        {
            GameHudParts.HmdBox speed = GameHudParts.Speed(root);
            GameHudParts.HmdBox range = GameHudParts.Altitude(root);
            if (speed != null)
            {
                _speedRoot = speed.Root;
                _speed = speed.Main;
            }
            else
            {
                _speed = HudStyle.CreateText("Speed", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 32f), 26f, TextAlignmentOptions.Right);
                _speedRoot = _speed.rectTransform;
            }
            if (range != null)
            {
                _rangeRoot = range.Root;
                _range = range.Main;
                _rangeStatus = GameHudParts.CopyText(range.Main, range.Root);
                if (_rangeStatus != null)
                {
                    RectTransform status = _rangeStatus.rectTransform;
                    status.anchorMin = new Vector2(0f, 0f);
                    status.anchorMax = new Vector2(1f, 0f);
                    status.pivot = new Vector2(0.5f, 1f);
                    status.sizeDelta = new Vector2(80f, _range.rectTransform.rect.height);
                    status.anchoredPosition = new Vector2(0f, -4f);
                    _rangeStatus.alignment = TextAlignmentOptions.Center;
                    _rangeStatus.fontSize = _range.fontSize * 0.7f;
                }
            }
            else
            {
                _range = HudStyle.CreateText("Range", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 32f), 26f, TextAlignmentOptions.Left);
                _rangeRoot = _range.rectTransform;
            }
            if (_rangeStatus == null)
            {
                _rangeStatus = HudStyle.CreateText("RangeStatus", _rangeRoot, new Vector2(0.5f, 0f), new Vector2(0f, -14f), new Vector2(240f, 22f),
                    16f, TextAlignmentOptions.Center);
            }
        }

        public override void Refresh(HudContext context)
        {
            float side = Mathf.Max(PlayerSettings.hmdSideDist, 200f);
            if (side != _placedSide)
            {
                _placedSide = side;
                _speedRoot.anchoredPosition = new Vector2(-side, 0f);
                _rangeRoot.anchoredPosition = new Vector2(side, 0f);
            }
            context.SpeedBoxRight = -side + _speedRoot.rect.width * _speedRoot.localScale.x * (1f - _speedRoot.pivot.x);

            string speed = _speedText.Get(Mathf.Abs(context.Vehicle.speed));
            if (_speed.text != speed) _speed.text = speed;
            _speed.color = HudStyle.Normal;
            UpdateRange(context.Turrets);
        }

        private void UpdateRange(TurretController turrets)
        {
            if (turrets.Range == TurretController.RangeState.Unknown)
            {
                Set("---", "", HudStyle.Normal);
                return;
            }

            string distance = _distanceText.Get(turrets.AimDistance);
            switch (turrets.Range)
            {
                case TurretController.RangeState.OutOfReach:
                    Set(distance, "OUT OF RANGE", HudStyle.Alert);
                    break;
                case TurretController.RangeState.BeyondMaxRange:
                    Set(distance, _maxText.Get(turrets.MaxRange), HudStyle.Alert);
                    break;
                case TurretController.RangeState.TooClose:
                    Set(distance, _minText.Get(turrets.MinRange), HudStyle.Warning);
                    break;
                default:
                    Set(distance, "IN RANGE", HudStyle.Normal);
                    break;
            }
        }

        private void Set(string range, string status, Color color)
        {
            if (_range.text != range) _range.text = range;
            if (_rangeStatus.text != status) _rangeStatus.text = status;
            _range.color = color;
            _rangeStatus.color = color;
        }
    }
}
