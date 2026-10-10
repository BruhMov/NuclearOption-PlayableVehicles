using UnityEngine;

namespace PlayableVehicles
{
    internal static class AimRaycast
    {
        private static readonly RaycastHit[] _hits = new RaycastHit[32];
        private static int _mask;

        private static int Mask
        {
            get
            {
                if (_mask == 0)
                {
                    _mask = PhysicsLayers.DefaultMask | PhysicsLayers.StaticsMask | PhysicsLayers.ShipsMask | PhysicsLayers.WaterMask;
                }
                return _mask;
            }
        }

        public static bool Cast(Vector3 origin, Vector3 direction, float maxDistance, Transform ignoreRoot, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, maxDistance, Mask, QueryTriggerInteraction.Ignore);

            nearest = default;
            bool found = false;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null) continue;
                if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }
    }
}
