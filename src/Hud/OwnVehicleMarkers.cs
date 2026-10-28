using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal static class OwnVehicleMarkers
    {
        private static readonly HashSet<UnitMapIcon> _marked = new HashSet<UnitMapIcon>();
        private static int _playerFrame = -1;
        private static Player _localPlayer;

        public static void Update(UnitMapIcon icon, GroundVehicle vehicle)
        {
            Image image = icon.iconImage;
            if (image == null) return;

            bool own = IsOwn(vehicle) && !IsSelected(icon);
            if (own)
            {
                _marked.Add(icon);
                Color white = new Color(1f, 1f, 1f, image.color.a);
                if (image.color != white) image.color = white;
            }
            else if (_marked.Remove(icon))
            {
                icon.UpdateColor();
            }
        }

        public static void Reset()
        {
            _marked.Clear();
            _playerFrame = -1;
            _localPlayer = null;
        }

        private static bool IsOwn(GroundVehicle vehicle)
        {
            if (vehicle == null || vehicle.disabled) return false;
            if (VehicleSession.IsActive && vehicle == VehicleSession.Vehicle) return true;
            if (vehicle.Networkowner == null) return false;

            if (_playerFrame != Time.frameCount)
            {
                _playerFrame = Time.frameCount;
                if (!GameManager.GetLocalPlayer(out _localPlayer)) _localPlayer = null;
            }
            return _localPlayer != null && vehicle.Networkowner == _localPlayer;
        }

        private static bool IsSelected(UnitMapIcon icon)
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            return map != null && map.selectedIcons.Contains(icon);
        }
    }
}
