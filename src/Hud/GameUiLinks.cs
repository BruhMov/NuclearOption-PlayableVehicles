using UnityEngine;

namespace PlayableVehicles
{
    internal static class GameUiLinks
    {
        private static UnitDebug _unitDebug;
        private static float _nextSearchTime;

        private static readonly Vector3[] _corners = new Vector3[4];
        private static RectTransform _canvasFor;
        private static Canvas _canvas;

        public static UnitDebug FindUnitDebug()
        {
            if (_unitDebug == null && Time.unscaledTime >= _nextSearchTime)
            {
                _unitDebug = UnityEngine.Object.FindObjectOfType<UnitDebug>();
                _nextSearchTime = Time.unscaledTime + 1f;
            }
            return _unitDebug;
        }

        public static RectTransform MinimapFrame()
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null || DynamicMap.mapMaximized || map.mapBackground == null) return null;
            return map.mapBackground.rectTransform;
        }

        public static bool TryGetScreenRect(RectTransform rect, out Rect screenRect)
        {
            screenRect = default;
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;

            if (_canvasFor != rect || _canvas == null)
            {
                _canvasFor = rect;
                _canvas = rect.GetComponentInParent<Canvas>();
            }
            Canvas canvas = _canvas;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            rect.GetWorldCorners(_corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, _corners[2]);
            screenRect = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            return screenRect.width > 1f && screenRect.height > 1f;
        }

        public static void Reset()
        {
            _canvasFor = null;
            _canvas = null;
            _unitDebug = null;
            _nextSearchTime = 0f;
        }
    }
}
