using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class AimWidget : HudWidget
    {
        private const float OnTargetToleranceDegrees = 0.75f;
        private const float CenterScale = 0.6f;
        private const float CenterAlpha = 0.55f;

        private RectTransform _center;
        private GameHudParts.TurretCrosshair _gun;
        private RawImage _fallbackMarker;
        private Texture2D _ringTexture;

        public override void Build(RectTransform root, HudContext context)
        {
            GameHudParts.TurretCrosshair center = GameHudParts.Crosshair(root);
            if (center != null)
            {
                if (center.Circle != null) center.Circle.enabled = false;
                if (center.Readiness != null) center.Readiness.enabled = false;
                center.Cross.color = HudStyle.WithAlpha(HudStyle.Normal, CenterAlpha);
                _center = center.Root;
                _center.localScale *= CenterScale;
            }
            else
            {
                _center = HudStyle.CreateRect("Crosshair", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
                HudStyle.CreateImage("Up", _center, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(2f, 10f), HudStyle.Normal);
                HudStyle.CreateImage("Down", _center, new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(2f, 10f), HudStyle.Normal);
                HudStyle.CreateImage("Left", _center, new Vector2(0.5f, 0.5f), new Vector2(-12f, 0f), new Vector2(10f, 2f), HudStyle.Normal);
                HudStyle.CreateImage("Right", _center, new Vector2(0.5f, 0.5f), new Vector2(12f, 0f), new Vector2(10f, 2f), HudStyle.Normal);
                HudStyle.CreateImage("Dot", _center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 2f), HudStyle.Normal);
            }
            _center.anchoredPosition = Vector2.zero;

            _gun = GameHudParts.Crosshair(root);
            if (_gun == null)
            {
                _ringTexture = HudStyle.CreateRingTexture(64, 3f);
                _fallbackMarker = HudStyle.CreateRawImage("GunMarker", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f),
                    _ringTexture, HudStyle.Normal);
            }
        }

        public override void Refresh(HudContext context)
        {
            TurretController turrets = context.Turrets;

            bool zoomed = context.View.IsZoomed;
            if (_center.gameObject.activeSelf == zoomed) _center.gameObject.SetActive(!zoomed);

            GameObject marker = _gun != null ? _gun.Root.gameObject : _fallbackMarker.gameObject;
            if (!turrets.TryGetGunMarker(OnTargetToleranceDegrees, out Vector3 markerPoint, out bool onTarget))
            {
                if (marker.activeSelf) marker.SetActive(false);
                return;
            }

            Vector3 screen = context.Camera.WorldToScreenPoint(markerPoint);
            bool visible = screen.z > 0f;
            if (marker.activeSelf != visible) marker.SetActive(visible);
            if (!visible) return;

            marker.transform.position = new Vector3(screen.x, screen.y, 0f);
            bool outOfRange = turrets.Range == TurretController.RangeState.OutOfReach
                || turrets.Range == TurretController.RangeState.BeyondMaxRange;

            if (_gun == null)
            {
                _fallbackMarker.color = outOfRange ? HudStyle.Alert : onTarget ? HudStyle.Normal : HudStyle.Warning;
                return;
            }

            float reload = AmmoSync.Of(context.Vehicle, turrets.SelectedStation).ReloadProgress;
            bool reloading = reload > 0f;
            if (_gun.Readiness != null)
            {
                if (_gun.Readiness.enabled != reloading) _gun.Readiness.enabled = reloading;
                if (reloading) _gun.Readiness.fillAmount = reload;
            }
            if (_gun.Circle != null)
            {
                bool ready = onTarget && !reloading && !outOfRange;
                if (_gun.Circle.enabled != ready) _gun.Circle.enabled = ready;
            }
            _gun.Cross.color = outOfRange ? HudStyle.Alert : reloading ? HudStyle.Alert + HudStyle.Normal * 0.5f : HudStyle.Normal;
        }

        public override void Dispose()
        {
            if (_ringTexture != null) Object.Destroy(_ringTexture);
        }
    }
}
