using UnityEngine;

namespace PlayableVehicles
{
    internal static class VehiclePreview
    {
        public static GameObject Create(VehicleDefinition definition, Vector3 groundPoint, Quaternion rotation)
        {
            GameObject prefab = definition != null ? definition.unitPrefab : null;
            if (prefab == null) return null;

            bool wasActive = prefab.activeSelf;
            GameObject clone;
            prefab.SetActive(false);
            try
            {
                clone = Object.Instantiate(prefab, groundPoint + Vector3.up * definition.spawnOffset.y, rotation);
            }
            finally
            {
                prefab.SetActive(wasActive);
            }

            clone.name = "PlayableVehicles_Preview_" + definition.unitName;
            Strip(clone);
            clone.SetActive(true);
            return clone;
        }

        private static void Strip(GameObject root)
        {
            for (int pass = 0; pass < 5; pass++)
            {
                bool removed = false;
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || Keep(component)) continue;
                    try
                    {
                        Object.DestroyImmediate(component);
                        removed = true;
                    }
                    catch
                    {
                    }
                }
                if (!removed) break;
            }
        }

        private static bool Keep(Component component)
        {
            return component is Transform || component is MeshFilter || component is MeshRenderer
                || component is SkinnedMeshRenderer || component is LODGroup;
        }

        public static float ViewDistance(VehicleDefinition definition)
        {
            float length = Mathf.Max(definition.length, 1f);
            float width = Mathf.Max(definition.width, 1f);
            float distance = (Mathf.Max(length, width * 0.7f) + Mathf.Max(width, length * 0.7f)) * 0.5f;
            float scale = Mathf.Clamp(Mathf.Pow(distance * 0.1f, 0.2f), 0.6f, 1.5f);
            return distance / scale;
        }
    }
}
