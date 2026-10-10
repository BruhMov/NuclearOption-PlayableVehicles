using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal static class DepotMarkers
    {
        private const float IconScale = 15f;
        private const float SpawnScreenScale = 2.2f;

        private static readonly Dictionary<UnitMapIcon, Image> _markers = new Dictionary<UnitMapIcon, Image>();
        private static readonly Dictionary<Faction, Sprite> _sprites = new Dictionary<Faction, Sprite>();

        public static void Update(UnitMapIcon icon, VehicleDepot depot, float mapInverseScale)
        {
            FactionHQ hq = null;
            bool show = DynamicMap.mapMaximized && VehicleShop.Enabled && GameManager.GetLocalHQ(out hq)
                && VehicleShop.IsOwnDepot(depot, hq) && !hq.preventDonation;
            _markers.TryGetValue(icon, out Image marker);
            if (!show)
            {
                if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                return;
            }

            Sprite sprite = MarkerSprite(hq.faction);
            if (sprite == null || icon.iconImage == null) return;
            if (marker == null)
            {
                marker = Create(icon, sprite);
                _markers[icon] = marker;
            }
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);

            Transform iconTransform = icon.iconImage.transform;
            float optionsScale = SceneSingleton<MapOptions>.i != null ? SceneSingleton<MapOptions>.i.iconSize : 1f;
            bool button = SpawnScreen.GroundMode && SpawnScreen.Active;
            marker.raycastTarget = button;
            marker.transform.localPosition = iconTransform.localPosition;
            marker.transform.localScale = Vector3.one * (mapInverseScale * IconScale * optionsScale * (button ? SpawnScreenScale : 1f));
            marker.transform.eulerAngles = Vector3.zero;
            marker.color = icon.iconImage.color;
        }

        private static Image Create(UnitMapIcon icon, Sprite sprite)
        {
            var go = new GameObject("PlayableVehicles_DepotMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            Transform iconTransform = icon.iconImage.transform;
            rect.SetParent(iconTransform.parent, false);
            rect.SetSiblingIndex(iconTransform.GetSiblingIndex() + 1);
            var source = (RectTransform)icon.iconImage.transform;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.material = icon.iconImage.material;
            image.raycastTarget = false;
            go.AddComponent<DepotMarkerButton>().Icon = icon;
            return image;
        }

        private static Sprite MarkerSprite(Faction faction)
        {
            if (faction == null) return null;
            if (_sprites.TryGetValue(faction, out Sprite cached)) return cached;

            Sprite sprite = null;
            foreach (Faction.ConvoyGroup group in faction.GetConvoyGroups())
            {
                foreach (Faction.ConvoyUnit unit in group.Constituents)
                {
                    if (sprite == null && unit?.Type is VehicleDefinition vehicle && vehicle.vehicleType == VehicleType.MBT) sprite = vehicle.mapIcon;
                }
            }
            if (sprite == null && VehicleShop.Catalog != null)
            {
                foreach (VehicleDefinition vehicle in VehicleShop.Catalog)
                {
                    if (sprite == null && vehicle != null && vehicle.vehicleType == VehicleType.MBT) sprite = vehicle.mapIcon;
                }
            }
            _sprites[faction] = sprite;
            return sprite;
        }

        private sealed class DepotMarkerButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public UnitMapIcon Icon;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left && Icon != null && Icon.unit is VehicleDepot depot) SpawnScreen.SelectDepot(depot);
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (Icon != null) { DynamicMap single = SceneSingleton<DynamicMap>.i; if (single != null) single.DisplayTooltip(Icon); }
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                { DynamicMap single = SceneSingleton<DynamicMap>.i; if (single != null) single.HideTooltip(); }
            }
        }

        public static void Reset()
        {
            _markers.Clear();
        }

        public static void Prune()
        {
            if (_markers.Count == 0) return;
            List<UnitMapIcon> dead = null;
            foreach (KeyValuePair<UnitMapIcon, Image> pair in _markers)
            {
                if (pair.Key == null || pair.Value == null) (dead ??= new List<UnitMapIcon>()).Add(pair.Key);
            }
            if (dead == null) return;
            foreach (UnitMapIcon icon in dead)
            {
                Image marker = _markers[icon];
                if (marker != null) Object.Destroy(marker.gameObject);
                _markers.Remove(icon);
            }
        }
    }
}
