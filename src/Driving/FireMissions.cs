using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal enum FireMissionKind
    {
        None,
        Artillery,
        Ballistic,
        AirDefense,
    }

    internal static class FireMissions
    {
        private static readonly Dictionary<MissileDefinition, MissileSeeker> _seekers = new Dictionary<MissileDefinition, MissileSeeker>();

        // Vanilla assigns a radar to a SAM launcher once, when the launcher spawns, by writing
        // the radar unit's TargetDetector into GroundVehicle.radar (see RpcAssignRadar on the
        // game side). Nothing ever refreshes that reference, so when the assigned radar dies the
        // launcher keeps pointing at the destroyed detector and reports "No radar link" forever,
        // even after a new radar is built. The mod re-resolves the link itself: every
        // RadarLinkRefresh seconds it drops a dead or out-of-range assignment and looks for any
        // alive friendly radar within RadarLinkRange.
        internal const float RadarLinkRange = 300f;
        private const float RadarLinkRefresh = 1f;

        private sealed class RadarLink
        {
            public TargetDetector Assigned; // last detector we pushed into vehicle.radar
            public float NextScan;
        }

        private static readonly Dictionary<GroundVehicle, RadarLink> _radarLinks = new Dictionary<GroundVehicle, RadarLink>();

        public static FireMissionKind KindOf(WeaponStation station)
        {
            if (station == null) return FireMissionKind.None;
            if (station.Turrets != null)
            {
                foreach (Turret turret in station.Turrets)
                {
                    if (ArtillerySolver.IsArtillery(turret)) return FireMissionKind.Artillery;
                }
            }

            MissileSeeker seeker = Seeker(Launcher(station));
            if (seeker is BallisticMissileGuidance) return FireMissionKind.Ballistic;
            if (seeker is SARHSeeker || seeker is IRSeeker || seeker is ARHSeeker) return FireMissionKind.AirDefense;
            return FireMissionKind.None;
        }

        public static MissileLauncher Launcher(WeaponStation station)
        {
            if (station == null || station.Weapons == null) return null;
            foreach (Weapon weapon in station.Weapons)
            {
                if (weapon is MissileLauncher launcher && launcher.missile != null) return launcher;
            }
            return null;
        }

        public static MissileSeeker Seeker(MissileLauncher launcher)
        {
            MissileDefinition missile = launcher != null ? launcher.missile : null;
            if (missile == null) return null;
            if (!_seekers.TryGetValue(missile, out MissileSeeker seeker))
            {
                seeker = missile.unitPrefab != null ? missile.unitPrefab.GetComponentInChildren<MissileSeeker>(true) : null;
                _seekers[missile] = seeker;
            }
            return seeker;
        }

        public static bool NeedsRadar(WeaponStation station)
        {
            return Seeker(Launcher(station)) is SARHSeeker;
        }

        public static bool HasRadar(GroundVehicle vehicle)
        {
            if (vehicle == null) return false;
            // Only the host owns unit state and can safely write GroundVehicle.radar, which is
            // networked to every client. Clients keep trusting the replicated value so the fire
            // control screen doesn't flicker between host and client verdicts.
            if (!vehicle.IsServer) return vehicle.radar != null || NetBridge.IsClientOnly;
            return ResolveRadar(vehicle) != null;
        }

        // Re-link a SAM launcher to a live friendly radar within 300 m. The vanilla assignment
        // happens once at spawn (RpcAssignRadar writes the radar unit's TargetDetector into
        // GroundVehicle.radar) and never recovers when that radar dies, so we re-run it here:
        // drop the detector when its owner is destroyed or has driven out of range, then pick
        // the TargetDetector of the closest alive friendly radar unit in reach.
        private static TargetDetector ResolveRadar(GroundVehicle vehicle)
        {
            if (NeedsRadarFor(vehicle)) return LinkNearbyRadar(vehicle);
            _radarLinks.Remove(vehicle);
            return vehicle.radar;
        }

        private static TargetDetector LinkNearbyRadar(GroundVehicle vehicle)
        {
            RadarLink link = LinkOf(vehicle);
            TargetDetector current = vehicle.radar;

            // Remember what vanilla (or a previous pass of ours) assigned, so we can tell a
            // stale reference apart from a fresh one.
            if (current != null && link.Assigned == null) link.Assigned = current;
            if (current != null && IsStale(current, vehicle))
            {
                vehicle.radar = null;
                link.Assigned = null;
                current = null;
            }

            if (Time.time < link.NextScan) return vehicle.radar;
            link.NextScan = Time.time + RadarLinkRefresh;

            if (current != null) return current;

            TargetDetector best = FindRadar(vehicle);
            if (best != null && !ReferenceEquals(best, vehicle.radar))
            {
                vehicle.radar = best;
                link.Assigned = best;
            }
            return vehicle.radar;
        }

        private static RadarLink LinkOf(GroundVehicle vehicle)
        {
            if (!_radarLinks.TryGetValue(vehicle, out RadarLink link))
            {
                link = new RadarLink();
                _radarLinks[vehicle] = link;
            }
            return link;
        }

        private static bool NeedsRadarFor(GroundVehicle vehicle)
        {
            List<WeaponStation> stations = vehicle.weaponStations;
            if (stations == null) return false;
            for (int i = 0; i < stations.Count; i++)
            {
                if (NeedsRadar(stations[i])) return true;
            }
            return false;
        }

        // A radar link is only valid while the detector's owner unit is alive and within 300 m.
        // On the host a destroyed unit still exists as a Unity object for a while after death,
        // so check both the disabled flag and whether the game still knows the unit.
        private static bool IsStale(TargetDetector radar, GroundVehicle vehicle)
        {
            if (radar == null || !radar.gameObject.activeInHierarchy) return true;
            Unit owner = OwnerOf(radar);
            if (owner == null || owner.disabled) return true;
            if (!UnitRegistry.TryGetPersistentUnit(owner.persistentID, out PersistentUnit record) || record == null) return true;
            Vector3 offset = owner.transform.position - vehicle.transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude > RadarLinkRange * RadarLinkRange;
        }

        // Walk up the hierarchy to the Unit that owns this detector component - the same
        // relationship vanilla serialises when RpcAssignRadar writes a radar unit into the
        // launcher's GroundVehicle.radar field.
        private static Unit OwnerOf(TargetDetector detector)
        {
            Transform t = detector.transform.parent;
            while (t != null)
            {
                Unit unit = t.GetComponent<Unit>();
                if (unit != null) return unit;
                t = t.parent;
            }
            return detector.GetComponent<Unit>();
        }

        private static TargetDetector FindRadar(GroundVehicle vehicle)
        {
            FactionHQ hq = vehicle.NetworkHQ;
            if (hq == null) return null;

            Vector3 origin = vehicle.transform.position;
            float radiusSquared = RadarLinkRange * RadarLinkRange;
            Unit bestOwner = null;
            float bestDistance = radiusSquared;
            List<Unit> units = UnitRegistry.allUnits;
            for (int i = 0; i < units.Count; i++)
            {
                Unit candidate = units[i];
                if (candidate == null || ReferenceEquals(candidate, vehicle) || candidate.disabled) continue;
                if (!(candidate is GroundVehicle)) continue;
                if (candidate.NetworkHQ != hq) continue;
                TargetDetector detector = DetectorOn(candidate);
                if (detector == null) continue;
                Vector3 offset = candidate.transform.position - origin;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance > radiusSquared || distance >= bestDistance) continue;
                bestDistance = distance;
                bestOwner = candidate;
            }
            return bestOwner != null ? DetectorOn(bestOwner) : null;
        }

        // The radar component a unit provides to others, if it is a radar vehicle at all.
        // A GroundVehicle whose radar field points at a detector owned by a *different* unit is
        // a launcher linked to an external radar, not a radar source, so it must not be picked
        // as a link candidate (that would let dead links ping-pong between launchers). Anything
        // else that genuinely carries a live TargetDetector (dedicated radar vehicles, radar
        // emplacements) qualifies. Enemy radars are filtered out by the faction check in
        // FindRadar before we ever get here.
        private static TargetDetector DetectorOn(Unit unit)
        {
            if (unit is Scenery) return null;
            if (unit is GroundVehicle ground && ground.radar != null && !IsOwnDetector(ground)) return null;

            TargetDetector[] detectors = unit.GetComponentsInChildren<TargetDetector>(true);
            for (int i = 0; i < detectors.Length; i++)
            {
                TargetDetector detector = detectors[i];
                if (detector != null && detector.gameObject.activeInHierarchy) return detector;
            }
            return null;
        }

        // True when the vehicle's radar field refers to a detector mounted on the vehicle itself
        // (i.e. it owns its radar) rather than to another unit's radar.
        private static bool IsOwnDetector(GroundVehicle vehicle)
        {
            TargetDetector detector = vehicle.radar;
            if (detector == null) return false;
            Unit owner = OwnerOf(detector);
            return ReferenceEquals(owner, vehicle);
        }

        public static ArtillerySolver.Solution Check(FireMissionKind kind, GroundVehicle vehicle, WeaponStation station, Unit target)
        {
            var solution = new ArtillerySolver.Solution { Status = ArtillerySolver.Status.OutOfRange };
            WeaponInfo info = station != null ? station.WeaponInfo : null;
            if (vehicle == null || info == null || target == null) return solution;

            TargetRequirements req = info.targetRequirements;
            Vector3 origin = vehicle.transform.position;
            Vector3 point = TargetLock.AimPoint(target);
            Vector3 toTarget = point - origin;
            solution.Direction = toTarget;

            if (kind == FireMissionKind.Ballistic)
            {
                solution.Range = new Vector3(toTarget.x, 0f, toTarget.z).magnitude;
                solution.Status = Within(solution.Range, req);
                return solution;
            }

            solution.Range = toTarget.magnitude;

            if (!(target is Aircraft || target is Missile))
            {
                solution.Status = ArtillerySolver.Status.NotAirborne;
                return solution;
            }

            solution.Status = Within(solution.Range, req);

            if (solution.Status != ArtillerySolver.Status.Ready)
                return solution;

            if (NeedsRadar(station) && !HasRadar(vehicle))
                solution.Status = ArtillerySolver.Status.NoRadar;

            else if (req.lineOfSight && !TerrainClear(origin + Vector3.up * 3f, target))
                solution.Status = ArtillerySolver.Status.NoLineOfSight;

            return solution;
        }

        private static bool TerrainClear(Vector3 origin, Unit target)
        {
            Vector3 point = target.transform.position;
            return !Physics.Linecast(origin, point, out RaycastHit hit, PhysicsLayers.StaticsMask)
                || FastMath.InRange(hit.point, point, target.maxRadius * 2f);
        }

        private static ArtillerySolver.Status Within(float range, in TargetRequirements req)
        {
            if (req.maxRange > 0f && range > req.maxRange) return ArtillerySolver.Status.OutOfRange;
            if (req.minRange > 0f && range < req.minRange) return ArtillerySolver.Status.TooClose;
            return ArtillerySolver.Status.Ready;
        }
    }
}
