using UnityEngine;

namespace PlayableVehicles
{
    internal static class FireControlMap
    {
        private static readonly Vector3[] _corners = new Vector3[4];
        private static bool _docked;
        private static bool _wasMaximized;
        private static Vector2Int _dockedScreen;

        public static void Open(RectTransform panel)
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.mapBackground == null || map.maximizedMapAnchor == null) return;

            _wasMaximized = DynamicMap.mapMaximized;
            if (!_wasMaximized) map.Maximize();
            if (!DynamicMap.mapMaximized) return;

            { GameplayUI single = SceneSingleton<GameplayUI>.i; if (single != null) single.HideSelectAirbase(); }
            _docked = true;
            Dock(map, panel);
            HideMfd();
        }

        public static void Tick(RectTransform panel)
        {
            if (!_docked) return;
            if (!DynamicMap.mapMaximized)
            {
                _docked = false;
                return;
            }
            if (_dockedScreen.x != Screen.width || _dockedScreen.y != Screen.height) Dock(SceneSingleton<DynamicMap>.i, panel);
        }

        public static void Reset()
        {
            _docked = false;
            _wasMaximized = false;
            _dockedScreen = default;
        }

        public static void Close()
        {
            if (!_docked) return;
            _docked = false;
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null || !DynamicMap.mapMaximized) return;

            map.Minimize();
            if (_wasMaximized) map.Maximize();
        }

        private static void HideMfd()
        {
            VirtualMFD mfd = Object.FindObjectOfType<VirtualMFD>();
            if (mfd == null) return;
            mfd.ToggleAllButtons(false);
            mfd.HideAllLeftScreens();
            mfd.HideAllRightScreens();
        }

        private static void Dock(DynamicMap map, RectTransform panel)
        {
            if (map == null) return;
            _dockedScreen = new Vector2Int(Screen.width, Screen.height);

            Transform root = map.transform;
            root.localScale = Vector3.one;
            root.position = map.maximizedMapAnchor.position;
            Canvas.ForceUpdateCanvases();

            float rightEdge = Screen.width * 0.68f;
            if (panel != null)
            {
                panel.GetWorldCorners(_corners);
                if (_corners[0].x > Screen.width * 0.3f) rightEdge = _corners[0].x;
            }

            map.mapBackground.rectTransform.GetWorldCorners(_corners);
            float width = _corners[2].x - _corners[0].x;
            float height = _corners[2].y - _corners[0].y;
            if (width < 1f || height < 1f) return;

            float margin = Screen.height * 0.03f;
            float scale = Mathf.Min((rightEdge - 2f * margin) / width, (Screen.height - 2f * margin) / height, 1f);
            root.localScale = Vector3.one * scale;

            map.mapBackground.rectTransform.GetWorldCorners(_corners);
            Vector3 center = (_corners[0] + _corners[2]) * 0.5f;
            Vector3 target = new Vector3(margin + width * scale * 0.5f, Screen.height * 0.5f, center.z);
            root.position += target - center;
        }
    }
}
