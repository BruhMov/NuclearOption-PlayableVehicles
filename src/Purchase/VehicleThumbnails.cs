using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class VehicleThumbnails
    {
        public const int Width = 256;
        public const int Height = 176;
        private const int PerFrame = 2;
        private const float MaxPartScale = 1.5f;
        private static readonly Vector3 CapturePoint = new Vector3(0f, 9000f, 0f);
        private static readonly Quaternion ViewRotation = Quaternion.Euler(22f, 215f, 0f);

        private static readonly Dictionary<VehicleDefinition, Texture2D> _cache = new Dictionary<VehicleDefinition, Texture2D>();
        private static readonly Queue<VehicleDefinition> _queue = new Queue<VehicleDefinition>();
        private static readonly HashSet<VehicleDefinition> _queued = new HashSet<VehicleDefinition>();
        private static readonly HashSet<VehicleDefinition> _failed = new HashSet<VehicleDefinition>();

        private static Camera _camera;
        private static Light _light;
        private static RenderTexture _target;

        public static Texture2D Get(VehicleDefinition definition)
        {
            if (definition == null) return null;
            if (_cache.TryGetValue(definition, out Texture2D texture) && texture != null) return texture;
            if (_failed.Contains(definition)) return null;
            if (_queued.Add(definition)) _queue.Enqueue(definition);
            return null;
        }

        public static void Tick()
        {
            for (int i = 0; i < PerFrame && _queue.Count > 0; i++)
            {
                VehicleDefinition definition = _queue.Dequeue();
                _queued.Remove(definition);
                try
                {
                    Texture2D texture = Render(definition);
                    if (texture != null) _cache[definition] = texture;
                    else _failed.Add(definition);
                }
                catch (System.Exception e)
                {
                    ModLog.Warning("Vehicle thumbnail for " + definition.unitName + " failed: " + e.Message);
                    _failed.Add(definition);
                }
            }
        }

        private static Texture2D Render(VehicleDefinition definition)
        {
            if (!EnsureRig()) return null;

            Vector3 origin = CapturePoint;
            GameObject model = VehiclePreview.Create(definition, origin, Quaternion.identity);
            if (model == null) return null;
            try
            {
                int layer = PhysicsLayers.PreviewRender;
                foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;

                float size = Mathf.Max(Mathf.Max(definition.length, definition.width), Mathf.Max(definition.height, 1f));
                bool any = false;
                Bounds bounds = default;
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Bounds part = renderer.bounds;
                    if (part.extents.magnitude > size * MaxPartScale) continue;
                    if (any) bounds.Encapsulate(part);
                    else bounds = part;
                    any = true;
                }
                if (!any) return null;
                float radius = Mathf.Clamp(bounds.extents.magnitude, 1f, size);

                Transform cam = _camera.transform;
                cam.rotation = ViewRotation;
                cam.position = bounds.center - cam.forward * radius * 4f;
                _camera.orthographicSize = radius * 0.62f;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = radius * 10f;
                _light.transform.position = bounds.center + cam.rotation * new Vector3(-0.5f, 1f, -1f) * radius * 1.5f;
                _light.range = radius * 6f;

                _camera.gameObject.SetActive(true);
                _light.enabled = true;
                RenderTexture previous = RenderTexture.active;
                try
                {
                    _camera.Render();
                    RenderTexture.active = _target;
                    var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                    texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                    texture.Apply();
                    texture.name = "PlayableVehicles_Thumbnail_" + definition.unitName;
                    return texture;
                }
                finally
                {
                    RenderTexture.active = previous;
                    _light.enabled = false;
                    _camera.gameObject.SetActive(false);
                }
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        private static bool EnsureRig()
        {
            if (_camera != null && _target != null && _target.IsCreated()) return true;

            if (_target == null || !_target.IsCreated())
            {
                if (_target != null) _target.Release();
                _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PlayableVehicles_ThumbnailTarget" };
                _target.Create();
            }
            if (_camera == null)
            {
                var go = new GameObject("PlayableVehicles_ThumbnailCamera");
                Object.DontDestroyOnLoad(go);
                go.SetActive(false);
                _camera = go.AddComponent<Camera>();
                _camera.orthographic = true;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _camera.cullingMask = 1 << PhysicsLayers.PreviewRender;
                _camera.aspect = (float)Width / Height;
                _camera.targetTexture = _target;
                _camera.allowHDR = false;
                _camera.allowMSAA = true;

                var lightGo = new GameObject("PlayableVehicles_ThumbnailLight");
                Object.DontDestroyOnLoad(lightGo);
                _light = lightGo.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.intensity = 3f;
                _light.cullingMask = 1 << PhysicsLayers.PreviewRender;
                _light.enabled = false;
            }
            return true;
        }
    }
}
