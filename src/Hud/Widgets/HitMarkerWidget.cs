using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class HitMarkerWidget : HudWidget
    {
        private const int PoolSize = 16;
        private const float Lifetime = 0.2f;
        private const float SoundInterval = 0.05f;

        private sealed class Marker
        {
            public GameObject Root;
            public Unit Unit;
            public Vector3 LocalPoint;
            public bool HasPoint;
            public float Time = -1f;
        }

        private readonly List<Marker> _pool = new List<Marker>();
        private readonly List<HitFeedback.Hit> _newHits = new List<HitFeedback.Hit>();
        private int _next;
        private float _lastSound = -1f;

        public override void Build(RectTransform root, HudContext context)
        {
            HitFeedback.Clear();

            var layer = HudStyle.CreateFill("HitMarkers", root);
            GameObject template = VanillaTemplate();
            Vector3 templateScale = template != null ? template.transform.localScale : Vector3.one;
            float ratio = ScaleRatio(layer);

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = template != null ? Object.Instantiate(template, layer) : CreateCross(layer);
                go.name = "HitMarker";
                if (template != null) go.transform.localScale = templateScale * ratio;
                foreach (Graphic graphic in go.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                go.SetActive(false);
                _pool.Add(new Marker { Root = go });
            }
        }

        public override void Refresh(HudContext context)
        {
            _newHits.Clear();
            HitFeedback.Drain(_newHits);

            if (PlayerSettings.showHitMarkers)
            {
                foreach (HitFeedback.Hit hit in _newHits)
                {
                    Marker marker = _pool[_next];
                    _next = (_next + 1) % _pool.Count;
                    marker.Unit = hit.Unit;
                    marker.LocalPoint = hit.LocalPoint;
                    marker.HasPoint = hit.HasPoint;
                    marker.Time = Time.timeSinceLevelLoad;
                }
                if (_newHits.Count > 0) PlaySound();
            }

            foreach (Marker marker in _pool)
            {
                Position(marker, context.Camera);
            }
        }

        private static void Position(Marker marker, Camera camera)
        {
            bool alive = marker.Unit != null && marker.Time >= 0f && Time.timeSinceLevelLoad - marker.Time <= Lifetime;
            Vector3 screen = Vector3.zero;
            if (alive)
            {
                Vector3 world = marker.HasPoint ? marker.Unit.transform.TransformPoint(marker.LocalPoint) : TargetLock.AimPoint(marker.Unit);
                screen = camera.WorldToScreenPoint(world);
                alive = screen.z > 0f;
            }

            if (marker.Root.activeSelf != alive) marker.Root.SetActive(alive);
            if (alive) marker.Root.transform.position = new Vector3(screen.x, screen.y, 0f);
        }

        private void PlaySound()
        {
            if (Time.timeSinceLevelLoad - _lastSound < SoundInterval) return;
            _lastSound = Time.timeSinceLevelLoad;
            AudioClip clip = GameAssets.i != null ? GameAssets.i.hitMarkerSound : null;
            if (clip != null) SoundManager.PlayInterfaceOneShot(clip);
        }

        private static GameObject VanillaTemplate()
        {
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null || GameAccess.CombatHudHitMarker == null) return null;
            return GameAccess.CombatHudHitMarker(combatHud);
        }

        private static float ScaleRatio(RectTransform ourLayer)
        {
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null || combatHud.iconLayer == null) return 1f;
            Canvas canvas = combatHud.iconLayer.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return 1f;

            float theirs = combatHud.iconLayer.lossyScale.x;
            float ours = ourLayer.lossyScale.x;
            return theirs > 0f && ours > 0f ? theirs / ours : 1f;
        }

        private static GameObject CreateCross(RectTransform parent)
        {
            var rect = HudStyle.CreateRect("HitMarker", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            var a = HudStyle.CreateImage("A", rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 3f), HudStyle.Alert);
            var b = HudStyle.CreateImage("B", rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 3f), HudStyle.Alert);
            a.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            b.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
            return rect.gameObject;
        }

        public override void Dispose()
        {
            _pool.Clear();
            HitFeedback.Clear();
        }
    }
}
