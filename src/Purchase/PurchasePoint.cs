using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class PurchasePoint
    {
        public enum PointKind : byte
        {
            Depot,
            Airbase,
        }

        private const float DepotSellRadius = 200f;
        private const float AirbaseSellRadius = 800f;

        private static readonly Dictionary<Object, PurchasePoint> _cache = new Dictionary<Object, PurchasePoint>();

        public readonly VehicleDepot Depot;
        public readonly Airbase Airbase;

        private PurchasePoint(VehicleDepot depot, Airbase airbase)
        {
            Depot = depot;
            Airbase = airbase;
        }

        public static PurchasePoint Of(VehicleDepot depot)
        {
            if (depot == null) return null;
            if (!_cache.TryGetValue(depot, out PurchasePoint point)) _cache[depot] = point = new PurchasePoint(depot, null);
            return point;
        }

        public static PurchasePoint Of(Airbase airbase)
        {
            if (airbase == null) return null;
            if (!_cache.TryGetValue(airbase, out PurchasePoint point)) _cache[airbase] = point = new PurchasePoint(null, airbase);
            return point;
        }

        public static void Reset()
        {
            _cache.Clear();
        }

        public PointKind Kind => Depot != null ? PointKind.Depot : PointKind.Airbase;
        public bool IsAirbase => Airbase != null;

        public uint Id => Depot != null ? Depot.persistentID.Id : Airbase != null ? Airbase.NetId : 0u;

        public static PurchasePoint Resolve(PointKind kind, uint id)
        {
            if (kind == PointKind.Depot)
            {
                return UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = id }), out Unit unit) ? Of(unit as VehicleDepot) : null;
            }
            foreach (Airbase airbase in FactionRegistry.airbaseLookup.Values)
            {
                if (airbase != null && airbase.NetId == id) return Of(airbase);
            }
            return null;
        }

        public bool Alive => Depot != null ? !Depot.disabled : Airbase != null && !Airbase.disabled;

        public string Name
        {
            get
            {
                if (Depot != null) return Depot.unitName;
                if (Airbase != null && Airbase.SavedAirbase != null && !string.IsNullOrEmpty(Airbase.SavedAirbase.DisplayName)) return Airbase.SavedAirbase.DisplayName;
                return Airbase != null ? Airbase.name : "";
            }
        }

        public string SortKey => Depot != null ? "1" + Depot.UniqueName : Airbase != null && Airbase.SavedAirbase != null ? "0" + Airbase.SavedAirbase.UniqueName : "";

        public Transform Anchor
        {
            get
            {
                if (Depot != null) return Depot.transform;
                if (Airbase == null) return null;
                if (Airbase.MapTower != null) return Airbase.MapTower.transform;
                return Airbase.center != null ? Airbase.center : Airbase.transform;
            }
        }

        public float SellRadius => IsAirbase ? AirbaseSellRadius : DepotSellRadius;

        public bool IsOwn(FactionHQ hq)
        {
            if (hq == null || !Alive) return false;
            if (Depot != null) return Depot.NetworkHQ == hq;
            return !Airbase.AttachedAirbase && Airbase.CurrentHQ == hq;
        }

        public bool InSellRange(Vector3 position)
        {
            Transform anchor = Anchor;
            return anchor != null && (position - anchor.position).sqrMagnitude <= SellRadius * SellRadius;
        }

        public static List<PurchasePoint> AllOwn(FactionHQ hq)
        {
            var points = new List<PurchasePoint>();
            if (hq == null) return points;
            foreach (Airbase airbase in FactionRegistry.airbaseLookup.Values)
            {
                PurchasePoint point = Of(airbase);
                if (point != null && point.IsOwn(hq)) points.Add(point);
            }
            foreach (VehicleDepot depot in Object.FindObjectsOfType<VehicleDepot>())
            {
                PurchasePoint point = Of(depot);
                if (point != null && point.IsOwn(hq)) points.Add(point);
            }
            points.Sort((a, b) => string.CompareOrdinal(a.SortKey, b.SortKey));
            return points;
        }

        public override string ToString()
        {
            return (IsAirbase ? "airbase " : "depot ") + Name;
        }
    }
}
