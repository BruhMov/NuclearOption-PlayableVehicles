using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class HitDirectionWidget : HudWidget
    {
        private const float FadeTime = 1.5f;
        private const float ArcDegrees = 40f;
        private const float Diameter = 300f;
        private const int MaxArcs = 4;
        private const float MinDamage = 0.01f;
        private const float MinCreditGain = 0.05f;
        private const float RemoteDirectionWait = 0.3f;

        private sealed class Arc
        {
            public Image Image;
            public Vector3 Direction;
            public float Time = -100f;
        }

        private readonly List<Arc> _arcs = new List<Arc>();
        private readonly List<UnitPart> _parts = new List<UnitPart>();
        private readonly List<float> _hitPoints = new List<float>();
        private readonly Dictionary<PersistentID, float> _credit = new Dictionary<PersistentID, float>();
        private readonly List<Vector3> _remoteDirections = new List<Vector3>();
        private float _lastRemoteDirectionTime = -100f;
        private bool _hasPartDirection;
        private Vector3 _partDirection;
        private float _partDirectionTime;
        private Texture2D _fallbackTexture;
        private Sprite _fallbackSprite;

        public override void Build(RectTransform root, HudContext context)
        {
            foreach (UnitPart part in context.Vehicle.GetComponentsInChildren<UnitPart>(true))
            {
                if (part == null) continue;
                _parts.Add(part);
                _hitPoints.Add(part.hitPoints);
            }
            SnapshotCredit(context.Vehicle);

            Sprite sprite = null;
            Material material = null;
            GameHudParts.TurretCrosshair sample = GameHudParts.Crosshair(root);
            if (sample != null)
            {
                if (sample.Readiness != null)
                {
                    sprite = sample.Readiness.sprite;
                    material = sample.Readiness.material;
                }
                Object.Destroy(sample.Root.gameObject);
            }
            if (sprite == null)
            {
                _fallbackTexture = HudStyle.CreateRingTexture(128, 6f);
                _fallbackSprite = Sprite.Create(_fallbackTexture, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
                sprite = _fallbackSprite;
            }

            RectTransform layer = HudStyle.CreateFill("HitDirection", root);
            for (int i = 0; i < MaxArcs; i++)
            {
                Image image = HudStyle.CreateImage("HitArc", layer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Diameter, Diameter), HudStyle.Alert);
                image.sprite = sprite;
                if (material != null) image.material = material;
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = true;
                image.fillAmount = ArcDegrees / 360f;
                image.gameObject.SetActive(false);
                _arcs.Add(new Arc { Image = image });
            }
        }

        public override void Tick(HudContext context)
        {
            GroundVehicle vehicle = context.Vehicle;
            Vector3 damagedPart = Vector3.zero;
            bool hit = false;
            for (int i = 0; i < _parts.Count; i++)
            {
                UnitPart part = _parts[i];
                if (part == null) continue;
                float hp = part.hitPoints;
                if (hp < _hitPoints[i] - MinDamage)
                {
                    hit = true;
                    damagedPart = part.transform.position;
                }
                _hitPoints[i] = hp;
            }

            if (hit) VehicleSession.ViewCamera?.AddShake(0.35f, 0.3f);

            if (VehicleSession.IsClientDriver)
            {
                TickClient(vehicle, hit, damagedPart);
                return;
            }

            if (!hit) return;
            Unit dealer = NewDealer(vehicle);
            Vector3 direction = dealer != null ? dealer.transform.position - vehicle.transform.position : damagedPart - vehicle.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            Show(direction);
        }

        private void TickClient(GroundVehicle vehicle, bool hit, Vector3 damagedPart)
        {
            HitFeedback.DrainDamageDirections(_remoteDirections);
            foreach (Vector3 direction in _remoteDirections)
            {
                Show(direction);
                _lastRemoteDirectionTime = Time.time;
                _hasPartDirection = false;
            }
            _remoteDirections.Clear();

            if (hit && Time.time - _lastRemoteDirectionTime > RemoteDirectionWait)
            {
                Vector3 direction = damagedPart - vehicle.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude >= 0.01f)
                {
                    _hasPartDirection = true;
                    _partDirection = direction;
                    _partDirectionTime = Time.time;
                }
            }
            if (_hasPartDirection && Time.time - _partDirectionTime > RemoteDirectionWait)
            {
                _hasPartDirection = false;
                Show(_partDirection);
            }
        }

        private Unit NewDealer(GroundVehicle vehicle)
        {
            Dictionary<PersistentID, float> credit = GameAccess.UnitDamageCredit != null ? GameAccess.UnitDamageCredit(vehicle) : null;
            if (credit == null) return null;
            PersistentID best = default;
            float bestGain = 0f;
            foreach (KeyValuePair<PersistentID, float> pair in credit)
            {
                _credit.TryGetValue(pair.Key, out float before);
                float gain = pair.Value - before;
                if (gain >= MinCreditGain && gain > bestGain)
                {
                    bestGain = gain;
                    best = pair.Key;
                }
            }
            SnapshotCredit(vehicle);
            return bestGain > 0f && UnitRegistry.TryGetUnit(new PersistentID?(best), out Unit unit) ? unit : null;
        }

        private void SnapshotCredit(GroundVehicle vehicle)
        {
            Dictionary<PersistentID, float> credit = GameAccess.UnitDamageCredit != null ? GameAccess.UnitDamageCredit(vehicle) : null;
            _credit.Clear();
            if (credit == null) return;
            foreach (KeyValuePair<PersistentID, float> pair in credit) _credit[pair.Key] = pair.Value;
        }

        private void Show(Vector3 direction)
        {
            Arc target = null;
            foreach (Arc arc in _arcs)
            {
                if (Time.time - arc.Time < FadeTime && Vector3.Angle(arc.Direction, direction) < ArcDegrees * 0.5f)
                {
                    target = arc;
                    break;
                }
                if (target == null || arc.Time < target.Time) target = arc;
            }
            target.Direction = direction;
            target.Time = Time.time;
        }

        public override void Refresh(HudContext context)
        {
            Vector3 forward = context.Camera.transform.forward;
            forward.y = 0f;
            foreach (Arc arc in _arcs)
            {
                float age = Time.time - arc.Time;
                bool active = age < FadeTime && forward.sqrMagnitude > 1e-4f;
                if (arc.Image.gameObject.activeSelf != active) arc.Image.gameObject.SetActive(active);
                if (!active) continue;
                float angle = Vector3.SignedAngle(forward, arc.Direction, Vector3.up);
                arc.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, ArcDegrees * 0.5f - angle);
                arc.Image.color = HudStyle.WithAlpha(HudStyle.Alert, 1f - age / FadeTime);
            }
        }

        public override void Dispose()
        {
            if (_fallbackSprite != null) Object.Destroy(_fallbackSprite);
            if (_fallbackTexture != null) Object.Destroy(_fallbackTexture);
        }
    }
}
