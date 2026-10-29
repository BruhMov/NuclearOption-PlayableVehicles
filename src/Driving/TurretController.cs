using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class TurretController
    {
        private readonly List<Turret> _turrets = new List<Turret>();
        private readonly List<bool> _turretWasEnabled = new List<bool>();
        private readonly TargetLock _lock = new TargetLock();
        private GroundVehicle _vehicle;
        private bool _fireHeld;
        private WeaponStation _weaponTargetStation;

        public bool Silent { get; set; }
        public bool FireHeld => _fireHeld;
        public System.Action<PvNoticeKind> RemoteNotice { get; set; }

        internal const string NoRadarText = "No radar link: a friendly radar or fire control vehicle must be within 300 m";
        private const float NoRadarNoticeInterval = 3f;

        public byte FirePresses { get; private set; }

        private const float RemoteFireTimeout = 0.5f;
        private const int MaxPressesPerPacket = 16;
        private const int MaxPendingShots = 3;
        private const float PendingShotLifetime = 0.5f;
        private bool _remoteFire;
        private float _remoteInputTime;
        private bool _remotePressesKnown;
        private byte _remotePresses;
        private int _pendingShots;
        private float _pendingShotsUntil;

        private const float MaxAimDistance = 6000f;
        private const float NoHitAimDistance = 4000f;
        private const float SolveInterval = 0.1f;

        private Vector3 _crosshairOffset;
        private bool _crosshairReachable = true;
        private Vector3 _leadVector;
        private Vector3 _interceptOffset;
        private bool _interceptReachable = true;
        private bool _hasGunSolution;
        private float _nextSolveTime;
        private int _solvedStationIndex = -1;
        private Unit _solvedTarget;

        public Turret PrimaryTurret => _turrets.Count > 0 ? _turrets[0] : null;
        public int SelectedStationIndex { get; private set; }
        public Vector3 CurrentAimPoint { get; private set; }
        public bool HasAimHit { get; private set; }
        public Vector3 TurretForward { get; private set; }

        public Unit LockedTarget => _lock.Target;
        public bool IsTracking => _lock.IsLocked && ModConfig.LockAutoTrack.Value;
        public Vector3 LeadPoint { get; private set; }
        public Vector3 AimTargetPoint { get; private set; }
        public Vector3 StationAimPoint { get; private set; }
        private bool AimTargetKnown => IsTracking || HasAimHit;

        public enum RangeState
        {
            Unknown,
            InRange,
            TooClose,
            BeyondMaxRange,
            OutOfReach,
        }

        public RangeState Range { get; private set; }
        public float AimDistance { get; private set; }
        public float MinRange { get; private set; }
        public float MaxRange { get; private set; }

        public Unit MissionTarget { get; private set; }
        public FireMode MissionMode { get; private set; }
        public ArtillerySolver.Solution MissionSolution { get; private set; }
        public bool MissionActive => MissionTarget != null && MissionMode != FireMode.None;

        // The branch the WEAPON is asked to fire: Low or High. A fixed slider angle cannot be
        // forwarded to the game's gun aiming, so it is reported as its nearest branch and the
        // barrel itself is steered by AimTurrets (see ManualAim).
        public FireMode MissionFireMode => HasManualElevation ? (MissionElevation <= 45f ? FireMode.Low : FireMode.High) : MissionMode;

        // Manual elevation override chosen with the fire-control slider. The target stays locked
        // at the assigned position; instead of letting the solver pick the Low or High branch,
        // the player fixes the exact barrel angle and every solution is sampled at that angle.
        public const float MinMissionElevation = -10f;
        public const float MaxMissionElevation = 85f;
        public const float ElevationStep = 0.5f;
        private float _missionElevation = float.NaN;
        public bool HasManualElevation => !float.IsNaN(_missionElevation);
        public float MissionElevation => _missionElevation;

        // Slider bounds for a turret: prefer the game's own travel limits when they look real
        // (the fields are optional and unreliable - some turrets report 0/0 or inverted values),
        // otherwise fall back to the wide physical envelope so the slider always works.
        public static void MissionElevationBounds(Turret turret, out float minDegrees, out float maxDegrees)
        {
            minDegrees = MinMissionElevation;
            maxDegrees = MaxMissionElevation;
            if (turret == null || GameAccess.TurretMaxElevation == null) return;
            float max = GameAccess.TurretMaxElevation(turret);
            float min = GameAccess.TurretMinElevation != null ? GameAccess.TurretMinElevation(turret) : 0f;
            if (max > 20f && max <= 90f) maxDegrees = Mathf.Min(max, MaxMissionElevation);
            if (min < 10f) minDegrees = Mathf.Max(min, MinMissionElevation);
            if (minDegrees >= maxDegrees)
            {
                minDegrees = MinMissionElevation;
                maxDegrees = MaxMissionElevation;
            }
        }

        public void SetMissionElevation(float degrees)
        {
            MissionElevationBounds(ArtilleryTurret(SelectedStation), out float min, out float max);
            _missionElevation = Mathf.Clamp(degrees, min, max);
            // Force an immediate re-solve so dragging the slider re-aims the gun live.
            _nextMissionSolve = 0f;
            _missionSolvedTarget = null;
        }

        public void ClearMissionElevation()
        {
            _missionElevation = float.NaN;
            _nextMissionSolve = 0f;
            _missionSolvedTarget = null;
        }

        private const float MissionSolveInterval = 0.5f;
        private float _nextNoRadarNotice;
        private float _nextMissionSolve;
        private Unit _missionSolvedTarget;
        private FireMode _missionSolvedMode;
        private float _missionSolvedElevation = float.NaN;

        public bool HasArtillery => ArtilleryTurret(SelectedStation) != null;
        public FireMissionKind MissionKind => FireMissions.KindOf(SelectedStation);

        // Public entry point for the fire control panel's Engage button: program the same
        // target lock the mouse-select path creates, so missile seekers receive their target
        // via Weapon.SetTarget when the station fires. TargetLock.Set re-validates the target
        // (known foreign unit) before accepting it.
        public void SetLock(Unit target)
        {
            _lock.Set(_vehicle, target);
        }

        // The angle the slider had when the mission was armed, so re-arming on the same target
        // does not silently drop back to AUTO (which would snap the barrel to a Low/High branch).
        private float _missionElevationAtSet = float.NaN;

        public void SetMission(Unit target, FireMode mode)
        {
            float manual = _missionElevation;
            if (target == null) manual = float.NaN;
            MissionTarget = target;
            MissionMode = target != null ? mode : FireMode.None;
            _missionElevationAtSet = manual;
            // Engaging with a fixed slider angle must keep firing at that angle: AimTurret and
            // the weapon's own aiming routine both re-solve and would otherwise pull the barrel
            // back to the nearest Low/High branch. Re-assert the override after the call chain
            // settles (next frame), which also forces an immediate re-solve at that elevation.
            if (!float.IsNaN(manual)) _reassertElevation = true;
        }

        private bool _reassertElevation;

        public void ClearMission(string reason)
        {
            // The slider selection is per-mission: dropping the mission drops the fixed angle
            // so the next target starts from the automatic Low/High behaviour again.
            ClearMissionElevation();
            if (!MissionActive) return;
            MissionTarget = null;
            MissionMode = FireMode.None;
            if (reason != null && !Silent) ModLog.Notify(reason);
        }

        public Turret ArtilleryTurret(WeaponStation station)
        {
            if (station == null || station.Turrets == null) return null;
            foreach (Turret turret in station.Turrets)
            {
                if (ArtillerySolver.IsArtillery(turret)) return turret;
            }
            return null;
        }

        public WeaponStation SelectedStation
        {
            get
            {
                List<WeaponStation> stations = _vehicle != null ? _vehicle.weaponStations : null;
                return stations != null && SelectedStationIndex >= 0 && SelectedStationIndex < stations.Count ? stations[SelectedStationIndex] : null;
            }
        }

        public void Attach(GroundVehicle vehicle)
        {
            _vehicle = vehicle;
            _turrets.Clear();
            _turretWasEnabled.Clear();
            _lock.Clear();
            MissionTarget = null;
            MissionMode = FireMode.None;
            ClearMissionElevation();
            _solvedStationIndex = -1;
            _weaponTargetStation = null;
            _fireHeld = false;
            _remoteFire = false;
            _remotePressesKnown = false;
            _pendingShots = 0;

            foreach (Turret turret in vehicle.GetComponentsInChildren<Turret>(true))
            {
                if (turret == null) continue;
                _turrets.Add(turret);
                _turretWasEnabled.Add(turret.enabled);
                turret.enabled = true;
            }

            SelectedStationIndex = 0;
        }

        public void Release()
        {
            for (int i = 0; i < _turrets.Count; i++)
            {
                if (_turrets[i] != null) _turrets[i].enabled = _turretWasEnabled[i];
            }
            PointDefence.Unmark(_turrets);
            _turrets.Clear();
            _turretWasEnabled.Clear();
            _lock.Clear();
            MissionTarget = null;
            MissionMode = FireMode.None;
            SetWeaponTarget(null, null);
            _fireHeld = false;
            _remoteFire = false;
            _pendingShots = 0;
            RemoteNotice = null;
            _vehicle = null;
            // The rig cache is keyed by Turret instances; since those get destroyed with the
            // vehicle, leaving stale entries behind leaks their Unity object references.
            _rigs.Clear();
        }

        public void AimTurrets()
        {
            _lock.Validate(_vehicle, !Silent);
            WeaponStation station = SelectedStation;
            bool hasGun = Ballistics.TryGetGunProfile(station, StationBarrel(station), out GunProfile profile);

            UpdateSolutions(hasGun, profile);
            UpdateAimTarget(hasGun);
            UpdateRange(station, hasGun);

            bool applyBallistics = hasGun && ModConfig.BallisticComputer.Value && AimTargetKnown;
            Vector3 ballisticOffset = IsTracking ? _interceptOffset : _crosshairOffset;
            StationAimPoint = applyBallistics ? AimTargetPoint + ballisticOffset : AimTargetPoint;
            bool mission = UpdateMission(station, hasGun, profile);

            for (int i = 0; i < _turrets.Count; i++)
            {
                Turret turret = _turrets[i];
                if (turret == null || !turret.gameObject.activeInHierarchy) continue;
                if (PointDefence.Mark(turret, PointDefence.IsAutonomous(_vehicle, station, turret)) && PointDefence.RunsVanilla(turret)) continue;

                bool stationTurret = station != null && station.Turrets != null && station.Turrets.Contains(turret);
                if (mission && stationTurret && ArtillerySolver.IsArtillery(turret))
                {
                    // Fixed slider angle: AimTurret would re-solve and snap the barrel back to a
                    // Low/High branch, so drive the transforms directly instead.
                    if (HasManualElevation) ManualAim(turret, MissionSolution.Direction, MissionElevation);
                    else GameAccess.TurretAimTurret(turret, MissionSolution.Direction);
                    if (i == 0) TurretForward = turret.transform.forward;
                    continue;
                }
                Vector3 target = stationTurret ? StationAimPoint : CurrentAimPoint;
                GameAccess.TurretAimTurret(turret, target - turret.transform.position);

                if (i == 0) TurretForward = turret.transform.forward;
            }
        }

        private bool UpdateMission(WeaponStation station, bool hasGun, in GunProfile profile)
        {
            if (!MissionActive) return false;
            if (MissionTarget == null || MissionTarget.disabled)
            {
                ClearMission("Fire mission complete: target destroyed");
                return false;
            }
            if (!DeployedForMission(_vehicle))
            {
                ClearMission("Fire mission cancelled: vehicle stowed");
                return false;
            }
            FireMissionKind kind = FireMissions.KindOf(station);
            // The mission was armed for a specific class of weapon; switching to a station of a
            // different class drops it. General is the catch-all class, so an artillery or missile
            // mission handed to a plain gun station (or the other way round) still counts as a
            // weapon change and is cancelled.
            bool missionIsArtillery = MissionMode != FireMode.Missile;
            bool stationIsArtillery = kind == FireMissionKind.Artillery;
            if (stationIsArtillery != missionIsArtillery)
            {
                ClearMission("Fire mission cancelled: weapon changed");
                return false;
            }
            if (kind != FireMissionKind.Artillery)
            {
                StationAimPoint = TargetLock.AimPoint(MissionTarget);
                return false;
            }
            Turret artillery = ArtilleryTurret(station);

            Transform barrel = StationBarrel(station);
            Vector3 muzzle = hasGun && profile.Muzzle != null ? profile.Muzzle.position : (barrel != null ? barrel.position : artillery.transform.position);
            bool due = Time.time >= _nextMissionSolve || _missionSolvedTarget != MissionTarget || _missionSolvedMode != MissionMode
                || _missionSolvedElevation != _missionElevation;
            if (due)
            {
                _nextMissionSolve = Time.time + MissionSolveInterval;
                _missionSolvedTarget = MissionTarget;
                _missionSolvedMode = MissionMode;
                _missionSolvedElevation = _missionElevation;
                MissionSolution = HasManualElevation
                    ? SolveAtFixedElevation(artillery, hasGun, profile, muzzle, MissionTarget, _missionElevation)
                    : SolveArtillery(artillery, station.WeaponInfo, hasGun, profile, muzzle, MissionTarget, MissionMode);
            }
            ArtillerySolver.Solution solution = MissionSolution;
            if (solution.Status != ArtillerySolver.Status.Ready) return false;

            StationAimPoint = muzzle + solution.Direction.normalized * 1000f;
            return true;
        }

        public static bool DeployedForMission(GroundVehicle vehicle)
        {
            return vehicle != null && (VehicleDeploy.IsDeployed(vehicle) || !VehicleDeploy.CanDeploy(vehicle) || VehicleDriver.IsStatic(vehicle));
        }

        public ArtillerySolver.Solution PreviewMission(Unit target, FireMode mode)
        {
            WeaponStation station = SelectedStation;
            // Only ballistic and air-defence missiles have their own mission check; artillery and
            // every other weapon (General) go through the gun solver below, which falls back to a
            // direct lay when the station has no artillery turret.
            FireMissionKind kind = FireMissions.KindOf(station);
            if (kind == FireMissionKind.Ballistic || kind == FireMissionKind.AirDefense) return FireMissions.Check(kind, _vehicle, station, target);
            Turret artillery = ArtilleryTurret(station);
            if (target == null) return new ArtillerySolver.Solution { Status = ArtillerySolver.Status.OutOfRange };
            if (artillery == null) return FireMissions.Check(kind, _vehicle, station, target);

            Transform barrel = StationBarrel(station);
            bool hasGun = Ballistics.TryGetGunProfile(station, barrel, out GunProfile profile);
            Vector3 muzzle = hasGun ? profile.Muzzle.position : (barrel != null ? barrel.position : artillery.transform.position);
            return SolveArtillery(artillery, station.WeaponInfo, hasGun, profile, muzzle, target, mode);
        }

        // Panel preview for the elevation slider: the shot is sampled at exactly the angle the
        // player selected, so the displayed elevation/time-of-flight match the gun's aim point.
        public ArtillerySolver.Solution PreviewElevation(Unit target, float elevationDegrees)
        {
            WeaponStation station = SelectedStation;
            Turret artillery = ArtilleryTurret(station);
            if (artillery == null || target == null) return new ArtillerySolver.Solution { Status = ArtillerySolver.Status.OutOfRange };

            Transform barrel = StationBarrel(station);
            bool hasGun = Ballistics.TryGetGunProfile(station, barrel, out GunProfile profile);
            Vector3 muzzle = hasGun ? profile.Muzzle.position : (barrel != null ? barrel.position : artillery.transform.position);
            return SolveAtFixedElevation(artillery, hasGun, profile, muzzle, target, elevationDegrees);
        }

        // The gun's own aiming routine is driven by the game's ballistic solver, which only ever
        // returns a Low or High branch - that is why a slider angle appeared to be ignored. With a
        // manual override armed we bypass AimTurret entirely and rotate the turret yaw plus its
        // elevation transform ourselves, so the barrel sits at exactly the chosen angle.
        //
        // Vanilla's Turret.AimTurret feeds the elevation pivot a NEGATIVE pitch for an "up" shot
        // (its own elevation convention stores up as negative X), so the manual path uses the same
        // handedness: subtract the requested degrees from the rest pose. A positive slider angle
        // then tips the barrel UP. Flip to +1 only if a custom rig mirrors this.
        private const float ManualElevationSign = -1f;
        private struct TurretRig
        {
            public Quaternion BaseRotation;     // turret root rotation with the pitch removed
            public Quaternion ElevationRest;    // elevation pivot local rotation at rest
        }
        private static readonly Dictionary<Turret, TurretRig> _rigs = new Dictionary<Turret, TurretRig>();

        private static void ManualAim(Turret turret, Vector3 heading, float elevationDegrees)
        {
            if (turret == null || heading.sqrMagnitude < 1e-6f) return;
            Transform root = turret.transform;
            Vector3 flat = new Vector3(heading.x, 0f, heading.z);
            if (flat.sqrMagnitude < 1e-6f) return;
            Quaternion yaw = Quaternion.LookRotation(flat, Vector3.up);
            Transform elevation = GameAccess.TurretElevationTransform != null ? GameAccess.TurretElevationTransform(turret) : null;
            if (!_rigs.TryGetValue(turret, out TurretRig rig))
            {
                // Sampled once, before this code ever moves the rig: whatever pitch the prefab
                // carries (or the AI/vanilla aim left behind) becomes the zero of the slider.
                rig.ElevationRest = elevation != null ? elevation.localRotation : Quaternion.identity;
                rig.BaseRotation = elevation != null
                    ? Quaternion.identity
                    : Quaternion.Inverse(yaw) * root.rotation;
                _rigs[turret] = rig;
            }
            if (elevation == null)
            {
                root.rotation = yaw * rig.BaseRotation * Quaternion.Euler(ManualElevationSign * elevationDegrees, 0f, 0f);
                return;
            }
            // Keep the turret ring level: any built-in pitch lives on the elevation pivot, which
            // we offset from its recorded rest pose. The offset is POST-multiplied so it rotates
            // about the pivot's OWN local X axis - the same axis vanilla AimTurret drives when it
            // sets this transform's local pitch, so our sign convention matches the game's exactly
            // (pre-multiplying instead rotated about the world axis, which mirrors for pivots whose
            // local frame is swapped relative to the turret forward - the nose-down symptom).
            root.rotation = yaw;
            elevation.localRotation = rig.ElevationRest * Quaternion.Euler(ManualElevationSign * elevationDegrees, 0f, 0f);
        }

        private ArtillerySolver.Solution SolveAtFixedElevation(Turret artillery, bool hasGun, in GunProfile profile,
            Vector3 muzzle, Unit target, float elevationDegrees)
        {
            Vector3 point = TargetLock.AimPoint(target);
            if (!hasGun)
            {
                // No gun profile to integrate: fall back to the analytic solver and honour the
                // requested branch (low/high) closest to the chosen angle.
                WeaponStation station = SelectedStation;
                ArtillerySolver.Solution analytic = ArtillerySolver.Solve(artillery, station != null ? station.WeaponInfo : null,
                    muzzle, point, elevationDegrees <= 45f ? FireMode.Low : FireMode.High);
                analytic.Elevation = elevationDegrees;
                return analytic;
            }

            ArtillerySolver.Solution solution = ArtillerySolver.SolveAtElevation(profile, artillery, point, elevationDegrees);
            Vector3 velocity = TargetLock.Velocity(target);
            if (solution.Status == ArtillerySolver.Status.Ready && velocity.sqrMagnitude > 1f)
            {
                solution = ArtillerySolver.SolveAtElevation(profile, artillery, point + velocity * solution.TimeOfFlight, elevationDegrees);
            }
            return solution;
        }

        public static ArtillerySolver.Solution SolveArtillery(Turret artillery, WeaponInfo info, bool hasGun, in GunProfile profile,
            Vector3 muzzle, Unit target, FireMode mode)
        {
            Vector3 point = TargetLock.AimPoint(target);
            if (!hasGun) return ArtillerySolver.Solve(artillery, info, muzzle, point, mode);

            ArtillerySolver.Solution solution = ArtillerySolver.SolveSimulated(profile, artillery, point, mode);
            Vector3 velocity = TargetLock.Velocity(target);
            if (solution.Status == ArtillerySolver.Status.Ready && velocity.sqrMagnitude > 1f)
            {
                solution = ArtillerySolver.SolveSimulated(profile, artillery, point + velocity * solution.TimeOfFlight, mode);
            }
            return solution;
        }

        private void UpdateSolutions(bool hasGun, in GunProfile profile)
        {
            Unit target = _lock.Target;
            bool due = Time.time >= _nextSolveTime || _solvedStationIndex != SelectedStationIndex || _solvedTarget != target;
            if (!due) return;

            _nextSolveTime = Time.time + SolveInterval;
            _solvedStationIndex = SelectedStationIndex;
            _solvedTarget = target;
            _hasGunSolution = hasGun;

            _crosshairOffset = Vector3.zero;
            _crosshairReachable = true;
            _leadVector = Vector3.zero;
            _interceptOffset = Vector3.zero;
            _interceptReachable = true;
            if (!hasGun) return;

            if (HasAimHit)
            {
                _crosshairReachable = Ballistics.Solve(profile, CurrentAimPoint, out _crosshairOffset, out _);
            }
            if (target != null)
            {
                Vector3 targetPoint = TargetLock.AimPoint(target);
                _interceptReachable = Ballistics.SolveIntercept(profile, targetPoint, TargetLock.Velocity(target),
                    out Vector3 intercept, out _interceptOffset);
                _leadVector = intercept - targetPoint;
            }
        }

        private void UpdateAimTarget(bool hasGun)
        {
            Unit target = _lock.Target;
            LeadPoint = target != null ? TargetLock.AimPoint(target) + (hasGun ? _leadVector : Vector3.zero) : CurrentAimPoint;
            AimTargetPoint = IsTracking ? LeadPoint : CurrentAimPoint;
        }

        private void UpdateRange(WeaponStation station, bool hasGun)
        {
            WeaponInfo info = station != null ? station.WeaponInfo : null;
            MinRange = info != null ? info.targetRequirements.minRange : 0f;
            MaxRange = info != null ? info.targetRequirements.maxRange : 0f;

            Transform barrel = StationBarrel(station);
            Vector3 origin = barrel != null ? barrel.position : (_vehicle != null ? _vehicle.transform.position : AimTargetPoint);
            AimDistance = Vector3.Distance(origin, AimTargetPoint);

            bool reachable = !hasGun || !_hasGunSolution || (IsTracking ? _interceptReachable : _crosshairReachable);
            if (!AimTargetKnown) Range = RangeState.Unknown;
            else if (!reachable) Range = RangeState.OutOfReach;
            else if (MaxRange > 0f && AimDistance > MaxRange) Range = RangeState.BeyondMaxRange;
            else if (MinRange > 0f && AimDistance < MinRange) Range = RangeState.TooClose;
            else Range = RangeState.InRange;
        }

        private Transform StationBarrel(WeaponStation station)
        {
            Turret turret = station != null && station.Turrets != null && station.Turrets.Count > 0 && station.Turrets[0] != null
                ? station.Turrets[0]
                : PrimaryTurret;
            if (turret == null) return null;
            Transform barrel = GameAccess.TurretElevationTransform(turret);
            return barrel != null ? barrel : turret.transform;
        }

        public void ReadLocalInput()
        {
            if (InputGate.ActionClicked(GameActions.NextWeapon)) SwitchWeapon(1);
            else if (InputGate.ActionClicked(GameActions.PreviousWeapon)) SwitchWeapon(-1);
            else if (InputGate.KeyDown(KeyCode.Alpha1)) SetWeaponSlot(0);
            else if (InputGate.KeyDown(KeyCode.Alpha2)) SetWeaponSlot(1);
            else if (InputGate.KeyDown(KeyCode.Alpha3)) SetWeaponSlot(2);

            UpdateAimPoint();

            if (InputGate.ActionClicked(GameActions.TargetSelect)) _lock.Select(_vehicle, Camera.main);
            if (InputGate.ActionClicked(GameActions.TargetCancel))
            {
                _lock.Release();
                ClearMission("Fire mission cancelled");
            }

            SetLocalFire(InputGate.ActionHeld(GameActions.Fire));
        }

        private void SetLocalFire(bool held)
        {
            if (held && !_fireHeld) FirePresses++;
            _fireHeld = held;
        }

        public void ApplyRemoteInput(Vector3 aimPoint, bool hasAimHit, int stationIndex, Unit lockTarget, bool fire, byte firePresses,
            Unit missionTarget, FireMode missionMode)
        {
            CurrentAimPoint = aimPoint;
            HasAimHit = hasAimHit;
            SetWeaponSlot(stationIndex);
            _lock.Set(_vehicle, lockTarget);
            ApplyRemoteFire(fire, firePresses);
            bool sameTarget = missionTarget != null && missionTarget == MissionTarget;
            if (missionTarget != null && !(sameTarget
                ? !missionTarget.disabled && missionTarget.NetworkHQ != _vehicle.NetworkHQ
                : TargetLock.IsKnownForeignTarget(missionTarget, _vehicle)))
            {
                missionTarget = null;
            }
            SetMission(missionTarget, missionMode);
        }

        private void ApplyRemoteFire(bool fire, byte firePresses)
        {
            _remoteFire = true;
            _remoteInputTime = Time.unscaledTime;
            _fireHeld = fire;

            int presses = _remotePressesKnown ? (byte)(firePresses - _remotePresses) : 0;
            _remotePressesKnown = true;
            _remotePresses = firePresses;
            if (presses > MaxPressesPerPacket) presses = 0;
            if (fire && presses > 0) presses--;
            if (presses <= 0) return;
            _pendingShots = Mathf.Min(_pendingShots + presses, MaxPendingShots);
            _pendingShotsUntil = Time.unscaledTime + PendingShotLifetime;
        }

        public void ClearFire()
        {
            _fireHeld = false;
            _pendingShots = 0;
        }

        public void TryFire()
        {
            if (_remoteFire && Time.unscaledTime - _remoteInputTime > RemoteFireTimeout) ClearFire();
            if (_pendingShots > 0 && Time.unscaledTime > _pendingShotsUntil) _pendingShots = 0;
            if (!_fireHeld && _pendingShots == 0) return;
            if (FireSelectedWeapon() && _pendingShots > 0) _pendingShots--;
        }

        private void UpdateAimPoint()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Ray ray = VehicleCamera.ViewRay(mainCam);
            if (AimRaycast.Cast(ray.origin, ray.direction, MaxAimDistance, _vehicle.transform, out RaycastHit hit))
            {
                CurrentAimPoint = hit.point;
                HasAimHit = true;
            }
            else
            {
                CurrentAimPoint = ray.origin + ray.direction * NoHitAimDistance;
                HasAimHit = false;
            }
        }

        private const float MarkerInterval = 0.05f;
        private const float MarkerIdleInterval = 0.25f;
        private const float MarkerProfileLifetime = 1f;
        private const float MarkerStillMoveSqr = 1e-4f;
        private const float MarkerStillAngle = 0.02f;
        private WeaponStation _markerStation;
        private bool _markerHasGun;
        private GunProfile _markerProfile;
        private float _markerProfileAt = float.NegativeInfinity;
        private Transform _markerMuzzle;
        private bool _markerValid;
        private float _markerAt;
        private Vector3 _markerOrigin;
        private Quaternion _markerRotation;
        private Vector3 _markerPoint;

        private bool MarkerProfile(WeaponStation station, Transform barrel, float now, out GunProfile profile)
        {
            bool stale = station != _markerStation || now - _markerProfileAt >= MarkerProfileLifetime
                || (_markerHasGun && _markerProfile.Muzzle == null);
            if (stale)
            {
                _markerStation = station;
                _markerProfileAt = now;
                _markerHasGun = Ballistics.TryGetGunProfile(station, barrel, out _markerProfile);
            }
            profile = _markerProfile;
            return _markerHasGun;
        }

        public bool TryGetGunMarker(float toleranceDegrees, out Vector3 point, out bool onTarget)
        {
            WeaponStation station = SelectedStation;
            Transform barrel = StationBarrel(station);
            Transform ignore = _vehicle != null ? _vehicle.transform : null;
            float now = Time.unscaledTime;
            onTarget = false;

            bool hasGun = MarkerProfile(station, barrel, now, out GunProfile profile);
            Transform muzzle = hasGun ? profile.Muzzle : barrel;
            if (muzzle == null)
            {
                _markerValid = false;
                point = Vector3.zero;
                return false;
            }

            Vector3 origin = muzzle.position;
            Quaternion rotation = muzzle.rotation;
            bool reuse = false;
            point = default;
            if (_markerValid && muzzle == _markerMuzzle && station == _markerStation)
            {
                bool still = (origin - _markerOrigin).sqrMagnitude < MarkerStillMoveSqr
                    && Quaternion.Angle(rotation, _markerRotation) < MarkerStillAngle;
                if (now - _markerAt < (still ? MarkerIdleInterval : MarkerInterval))
                {
                    point = origin + rotation * Quaternion.Inverse(_markerRotation) * (_markerPoint - _markerOrigin);
                    reuse = true;
                }
            }

            if (!reuse)
            {
                if (hasGun)
                {
                    Ballistics.PredictImpact(profile, ignore, out point);
                }
                else
                {
                    point = AimRaycast.Cast(origin, barrel.forward, MaxAimDistance, ignore, out RaycastHit hit)
                        ? hit.point
                        : origin + barrel.forward * NoHitAimDistance;
                }
                _markerValid = true;
                _markerMuzzle = muzzle;
                _markerAt = now;
                _markerOrigin = origin;
                _markerRotation = rotation;
                _markerPoint = point;
            }

            Vector3 toAim = AimTargetPoint - origin;
            Vector3 toImpact = point - origin;
            onTarget = AimTargetKnown && toAim.sqrMagnitude > 1f && toImpact.sqrMagnitude > 1f
                && Vector3.Angle(toImpact, toAim) <= toleranceDegrees;
            return true;
        }

        private void SwitchWeapon(int delta)
        {
            int count = _vehicle.weaponStations?.Count ?? 0;
            if (count <= 1) return;
            SelectedStationIndex = (SelectedStationIndex + delta + count) % count;
        }

        private void SetWeaponSlot(int index)
        {
            if (_vehicle.weaponStations == null || index < 0 || index >= _vehicle.weaponStations.Count) return;
            SelectedStationIndex = index;
        }

        private bool FireSelectedWeapon()
        {
            if (_vehicle.weaponStations == null || _vehicle.weaponStations.Count == 0) return false;

            if (SelectedStationIndex >= _vehicle.weaponStations.Count)
            {
                SelectedStationIndex = 0;
            }

            WeaponStation station = _vehicle.weaponStations[SelectedStationIndex];
            if (station == null || !station.Ready()) return false;
            if (FireMissions.NeedsRadar(station) && !FireMissions.HasRadar(_vehicle))
            {
                if (Time.time >= _nextNoRadarNotice)
                {
                    _nextNoRadarNotice = Time.time + NoRadarNoticeInterval;
                    if (!Silent) ModLog.Notify(NoRadarText);
                    else RemoteNotice?.Invoke(PvNoticeKind.NoRadar);
                }
                return false;
            }

            if (station.Weapons != null)
            {
                for (int w = 0; w < station.Weapons.Count; w++)
                {
                    if (station.Weapons[w] != null) station.Weapons[w].Safety = false;
                }
            }

            Unit target = MissionActive ? MissionTarget : _lock.Target;
            if (target == null && HasProximityFuse(station)) target = FuseTarget(station);
            SetWeaponTarget(station, target);
            station.Fire(_vehicle, target);
            return true;
        }

        private const float FuseConeDegrees = 15f;
        private const float FuseRetargetInterval = 0.2f;
        private WeaponStation _fuseStation;
        private Unit _fuseTarget;
        private float _nextFuseSearch;

        internal static bool HasProximityFuse(WeaponStation station)
        {
            if (station == null || station.Weapons == null || GameAccess.GunProximityTimer == null) return false;
            foreach (Weapon weapon in station.Weapons)
            {
                if (weapon is Gun gun && GameAccess.GunProximityTimer(gun)) return true;
            }
            return false;
        }

        private Unit FuseTarget(WeaponStation station)
        {
            if (station == _fuseStation && Time.time < _nextFuseSearch && _fuseTarget != null && !_fuseTarget.disabled) return _fuseTarget;
            _fuseStation = station;
            _nextFuseSearch = Time.time + FuseRetargetInterval;
            _fuseTarget = null;

            FactionHQ hq = _vehicle.NetworkHQ;
            Transform barrel = StationBarrel(station);
            if (hq == null || barrel == null) return null;
            float maxRange = station.WeaponInfo != null && station.WeaponInfo.targetRequirements.maxRange > 0f
                ? station.WeaponInfo.targetRequirements.maxRange
                : 5000f;
            Vector3 origin = barrel.position;
            Vector3 forward = barrel.forward;
            float best = FuseConeDegrees;
            foreach (KeyValuePair<PersistentID, TrackingInfo> pair in hq.trackingDatabase)
            {
                if (pair.Value == null || !pair.Value.TryGetUnit(out Unit unit)) continue;
                if (unit == null || unit.disabled || unit.NetworkHQ == hq || !(unit is Aircraft || unit is Missile)) continue;
                Vector3 toTarget = unit.transform.position - origin;
                if (toTarget.sqrMagnitude > maxRange * maxRange) continue;
                float angle = Vector3.Angle(forward, toTarget);
                if (angle < best)
                {
                    best = angle;
                    _fuseTarget = unit;
                }
            }
            return _fuseTarget;
        }

        private void SetWeaponTarget(WeaponStation station, Unit target)
        {
            if (_weaponTargetStation != null && _weaponTargetStation != station) ApplyWeaponTarget(_weaponTargetStation, TurretTarget(_weaponTargetStation));
            if (station != null) ApplyWeaponTarget(station, target);
            _weaponTargetStation = station;
        }

        private static Unit TurretTarget(WeaponStation station)
        {
            if (station.Turrets == null) return null;
            foreach (Turret turret in station.Turrets)
            {
                Unit target = turret != null ? turret.GetTarget() : null;
                if (target != null) return target;
            }
            return null;
        }

        private static void ApplyWeaponTarget(WeaponStation station, Unit target)
        {
            if (station.Weapons == null) return;
            for (int w = 0; w < station.Weapons.Count; w++)
            {
                if (station.Weapons[w] != null) station.Weapons[w].SetTarget(target);
            }
        }
    }
}
