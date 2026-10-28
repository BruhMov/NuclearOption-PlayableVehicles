using RoadPathfinding;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class AirbaseSpots
    {
        private const float MinRadius = 35f;
        private const float MaxRadius = 260f;
        private const float RingStep = 15f;
        private const float PointSpacing = 18f;
        private const float MaxSlope = 15f;
        private const float RunwayClearance = 60f;
        private const float TaxiClearance = 25f;
        private const float BodyMargin = 2f;
        private const float ProbeHeight = 60f;

        private static readonly Collider[] _overlap = new Collider[16];

        public static bool TryFind(Airbase airbase, Transform anchor, VehicleDefinition definition, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (airbase == null || anchor == null || definition == null) return false;

            Vector3 center = anchor.position;
            Vector3 away = airbase.center != null ? Vector3.ProjectOnPlane(center - airbase.center.position, Vector3.up) : Vector3.zero;
            float start = away.sqrMagnitude > 1f ? Mathf.Atan2(away.x, away.z) : 0f;
            float cos = Mathf.Cos(MaxSlope * Mathf.Deg2Rad);

            for (float radius = MinRadius; radius <= MaxRadius; radius += RingStep)
            {
                int count = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / PointSpacing));
                for (int i = 0; i < count; i++)
                {
                    int k = (i + 1) / 2 * (i % 2 == 0 ? 1 : -1);
                    float angle = start + k * 2f * Mathf.PI / count;
                    Vector3 flat = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                    if (!Ground(flat, out RaycastHit ground) || ground.normal.y < cos) continue;
                    if (NearRunway(airbase, ground.point) || NearTaxiway(airbase, ground.point)) continue;

                    Vector3 forward = Vector3.ProjectOnPlane(ground.point - center, Vector3.up);
                    if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                    Quaternion rot = Quaternion.LookRotation(forward.normalized, Vector3.up);
                    if (Occupied(ground, rot, definition)) continue;

                    position = ground.point;
                    rotation = rot;
                    return true;
                }
            }
            return false;
        }

        private static bool Ground(Vector3 flat, out RaycastHit ground)
        {
            Vector3 from = flat + Vector3.up * ProbeHeight;
            if (!Physics.Raycast(from, Vector3.down, out ground, ProbeHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            return ground.collider.attachedRigidbody == null && ground.collider.GetComponentInParent<Unit>() == null;
        }

        private static bool NearRunway(Airbase airbase, Vector3 point)
        {
            if (airbase.runways == null) return false;
            foreach (Airbase.Runway runway in airbase.runways)
            {
                if (runway == null || runway.Start == null || runway.End == null) continue;
                Vector3 a = runway.Start.position, b = runway.End.position;
                Vector3 ab = Vector3.ProjectOnPlane(b - a, Vector3.up);
                Vector3 ap = Vector3.ProjectOnPlane(point - a, Vector3.up);
                float t = ab.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
                float distance = (ap - ab * t).magnitude;
                if (distance < runway.GetWidth() * 0.5f + RunwayClearance) return true;
            }
            return false;
        }

        private static bool NearTaxiway(Airbase airbase, Vector3 point)
        {
            RoadNetwork taxi = GameAccess.AirbaseTaxiNetwork != null ? GameAccess.AirbaseTaxiNetwork(airbase) : null;
            if (taxi == null || !taxi.Exists()) return false;
            if (!taxi.TryGetNearestPoint(point.ToGlobalPosition(), out GlobalPosition nearest, out _)) return false;
            Vector3 offset = Vector3.ProjectOnPlane(nearest.ToLocalPosition() - point, Vector3.up);
            return offset.sqrMagnitude < TaxiClearance * TaxiClearance;
        }

        private static bool Occupied(RaycastHit ground, Quaternion rotation, VehicleDefinition definition)
        {
            Vector3 half = new Vector3(definition.width * 0.5f + BodyMargin, Mathf.Max(definition.height, 2f) * 0.5f, definition.length * 0.5f + BodyMargin);
            Vector3 center = ground.point + Vector3.up * (half.y + 0.5f);
            int count = Physics.OverlapBoxNonAlloc(center, half, _overlap, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _overlap[i];
                if (c == null || c == ground.collider || c.GetType().Name == "TerrainCollider") continue;
                return true;
            }
            return false;
        }
    }
}
