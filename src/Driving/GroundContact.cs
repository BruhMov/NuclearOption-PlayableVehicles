using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class GroundContact
    {
        private const float MinRideHeight = 0.1f;
        private const float MaxRideHeight = 5f;
        private const float ProbeDepthBelowRide = 3f;
        private const float MaxTiltDegrees = 40f;

        private readonly Vector3[] _cornerHits = new Vector3[4];
        private readonly bool[] _cornerHit = new bool[4];
        private readonly Vector3[] _cornerNormals = new Vector3[4];

        private float _halfLength;
        private float _halfWidth;

        public float RideHeight { get; private set; }

        public void Init(GroundVehicle vehicle, Rigidbody rb)
        {
            UnitDefinition def = vehicle.definition;
            float fallbackHalf = Mathf.Max(vehicle.maxRadius * 0.5f, 1f);
            _halfLength = def != null && def.length > 0.5f ? def.length * 0.4f : fallbackHalf;
            _halfWidth = def != null && def.width > 0.5f ? def.width * 0.4f : fallbackHalf * 0.6f;

            RideHeight = MeasureRideHeight(vehicle, rb);
        }

        private float MeasureRideHeight(GroundVehicle vehicle, Rigidbody rb)
        {
            Vector3 origin = rb.position + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f + MaxRideHeight, PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore))
            {
                float measured = rb.position.y - hit.point.y;
                if (measured >= MinRideHeight && measured <= MaxRideHeight) return measured;
            }

            float spawnOffset = vehicle.definition != null ? vehicle.definition.spawnOffset.y : 0f;
            return Mathf.Clamp(spawnOffset, MinRideHeight, MaxRideHeight);
        }

        public bool Sample(Rigidbody rb, out float groundHeight, out Vector3 normal)
        {
            Quaternion rot = rb.rotation;
            Vector3 center = rb.position;
            Vector3 fwd = rot * Vector3.forward * _halfLength;
            Vector3 right = rot * Vector3.right * _halfWidth;

            float startAbove = 1f + _halfLength * 0.6f;
            float maxDistance = startAbove + RideHeight + ProbeDepthBelowRide;

            Probe(0, center + fwd - right, startAbove, maxDistance);
            Probe(1, center + fwd + right, startAbove, maxDistance);
            Probe(2, center - fwd - right, startAbove, maxDistance);
            Probe(3, center - fwd + right, startAbove, maxDistance);

            int hits = 0;
            float heightSum = 0f;
            Vector3 normalSum = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                if (!_cornerHit[i]) continue;
                hits++;
                heightSum += _cornerHits[i].y;
                normalSum += _cornerNormals[i];
            }

            if (hits == 0)
            {
                groundHeight = 0f;
                normal = Vector3.up;
                return false;
            }

            groundHeight = heightSum / hits;

            if (hits == 4)
            {
                Vector3 diagA = _cornerHits[0] - _cornerHits[3];
                Vector3 diagB = _cornerHits[1] - _cornerHits[2];
                normal = Vector3.Cross(diagB, diagA).normalized;
                if (normal.y < 0f) normal = -normal;
                if (normal.sqrMagnitude < 0.5f) normal = (normalSum / hits).normalized;
            }
            else
            {
                normal = (normalSum / hits).normalized;
            }

            normal = Vector3.RotateTowards(Vector3.up, normal, MaxTiltDegrees * Mathf.Deg2Rad, 0f);
            return true;
        }

        private void Probe(int index, Vector3 point, float startAbove, float maxDistance)
        {
            Vector3 origin = point + Vector3.up * startAbove;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore))
            {
                _cornerHit[index] = true;
                _cornerHits[index] = hit.point;
                _cornerNormals[index] = hit.normal;
            }
            else
            {
                _cornerHit[index] = false;
            }
        }
    }
}
