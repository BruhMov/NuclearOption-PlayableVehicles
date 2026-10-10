using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal enum HitchCheck { None, Ready, Misaligned, Uneven }

    internal static class Towing
    {
        private const float HitchReach = 4f;
        private const float MaxHitchAngle = 60f;
        private const float SearchRadius = 30f;
        private const float MaxHitchHeightGap = 0.6f;

        public static bool IsTractor(GroundVehicle vehicle)
        {
            return vehicle != null && vehicle.definition != null
                && vehicle.definition.unitName.IndexOf("Tractor", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static TowHitch HitchOf(GroundVehicle tractor)
        {
            if (tractor == null) return null;
            foreach (TowHitch hitch in TowHitch.Active)
            {
                if (hitch != null && hitch.Tractor == tractor) return hitch;
            }
            return null;
        }

        private const float MaxHitchSpeed = VehicleDeploy.MaxDeploySpeed * 2f;

        public static void Toggle(GroundVehicle tractor, VehicleDriver driver)
        {
            if (NetBridge.IsClientOnly)
            {
                ClientLoad load = LogisticsLink.ClientLoadOf(tractor);
                bool towing = load != null && load.Active;
                if (!towing && driver.BodyVelocity.magnitude > MaxHitchSpeed)
                {
                    ModLog.Notify(LogisticsLink.Describe(PvLogisticsEvent.StopToHitch, tractor, null));
                    return;
                }
                LogisticsLink.Request(tractor, !towing);
                return;
            }
            Execute(tractor, driver.BodyVelocity.magnitude);
        }

        public static void Execute(GroundVehicle tractor, float speed)
        {
            TowHitch current = HitchOf(tractor);
            if (current != null)
            {
                current.Detach(PvLogisticsEvent.Unhitched);
                return;
            }
            if (speed > MaxHitchSpeed)
            {
                LogisticsLink.Report(tractor, PvLogisticsEvent.StopToHitch, null);
                return;
            }

            HitchCheck check = FindTrailer(tractor, out GroundVehicle best);
            if (check == HitchCheck.None)
            {
                LogisticsLink.Report(tractor, PvLogisticsEvent.NoTrailer, null);
                return;
            }
            if (check == HitchCheck.Misaligned)
            {
                LogisticsLink.Report(tractor, PvLogisticsEvent.Misaligned, best);
                return;
            }

            if (check == HitchCheck.Uneven)
            {
                LogisticsLink.Report(tractor, TowHitch.HitchHeightGap(tractor, best) > 0f ? PvLogisticsEvent.TowBarLow : PvLogisticsEvent.TowBarHigh, best);
                return;
            }

            best.gameObject.AddComponent<TowHitch>().Attach(best, tractor);
            LogisticsLink.Report(tractor, PvLogisticsEvent.Hitched, best);
        }

        public static HitchCheck FindTrailer(GroundVehicle tractor, out GroundVehicle trailer)
        {
            trailer = null;
            Vector3 rear = tractor.transform.TransformPoint(TowHitch.RearPoint(tractor));
            float bestDistance = HitchReach;
            List<Unit> units = UnitRegistry.allUnits;
            for (int i = 0; i < units.Count; i++)
            {
                GroundVehicle candidate = units[i] as GroundVehicle;
                if (candidate == null || candidate == tractor || candidate.disabled) continue;
                if ((candidate.transform.position - rear).sqrMagnitude > SearchRadius * SearchRadius) continue;
                if (!VehicleGeometry.IsTrailer(candidate) || candidate.NetworkHQ != tractor.NetworkHQ || VehicleGeometry.IsBusy(candidate)) continue;
                Vector3 front = candidate.transform.TransformPoint(TowHitch.FrontPoint(candidate));
                float distance = new Vector2(front.x - rear.x, front.z - rear.z).magnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    trailer = candidate;
                }
            }
            if (trailer == null) return HitchCheck.None;
            Vector3 tractorForward = Vector3.ProjectOnPlane(tractor.transform.forward, Vector3.up);
            Vector3 trailerForward = Vector3.ProjectOnPlane(trailer.transform.forward, Vector3.up);
            if (Vector3.Angle(tractorForward, trailerForward) > MaxHitchAngle) return HitchCheck.Misaligned;
            return Mathf.Abs(TowHitch.HitchHeightGap(tractor, trailer)) > MaxHitchHeightGap ? HitchCheck.Uneven : HitchCheck.Ready;
        }

        public static void Release(GroundVehicle tractor, bool silent = false)
        {
            TowHitch hitch = HitchOf(tractor);
            if (hitch != null)
            {
                hitch.Detach(silent ? PvLogisticsEvent.State : PvLogisticsEvent.Unhitched);
                return;
            }
            ClientLoad load = !silent && NetBridge.IsClientOnly && IsTractor(tractor) ? LogisticsLink.ClientLoadOf(tractor) : null;
            if (load != null && load.Active)
            {
                string text = LogisticsLink.Describe(PvLogisticsEvent.Unhitched, tractor, load.Unit);
                if (text != null) ModLog.Notify(text);
            }
        }
    }

    internal sealed class TowHitch : MonoBehaviour
    {
        private const float HitchHeight = 0.7f;
        private const float RearClearance = 0.15f;

        public static readonly HashSet<TowHitch> Active = new HashSet<TowHitch>();

        public GroundVehicle Trailer { get; private set; }
        public GroundVehicle Tractor { get; private set; }

        private Rigidbody _rb;
        private ConfigurableJoint _joint;
        private readonly VanillaDrive _wheels = new VanillaDrive();
        private RigidbodyInterpolation _interpolation;
        private bool _detached;
        private readonly System.Collections.Generic.List<Collider> _legs = new System.Collections.Generic.List<Collider>();
        private const float LegClearance = 0.3f;
        private const float JoinTime = 0.5f;
        private Vector3 _joinFrom;
        private Vector3 _joinTo;
        private float _joinTimer;
        private const float BreakForceG = 12f;
        private const float BreakForceMin = 60000f;
        private const float BreakTorqueArm = 3f;

        public void Attach(GroundVehicle trailer, GroundVehicle tractor)
        {
            Trailer = trailer;
            Tractor = tractor;
            Active.Add(this);
            _rb = trailer.rb;
            Rigidbody tractorBody = tractor.rb;

            VehicleDeploy.Set(trailer, false);
            trailer.enabled = false;
            _interpolation = _rb.interpolation;
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            float standing = Physics.Raycast(trailer.transform.position, Vector3.down, out RaycastHit below, 10f,
                PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore) ? below.distance : 1f;
            _wheels.InitTowed(trailer, _rb, standing);
            _rb.mass = _wheels.Mass;
            DisableLegs(trailer);

            Vector3 front = FrontPoint(trailer);
            Vector3 rear = RearPoint(tractor);
            Vector3 offset = tractor.transform.TransformPoint(rear) - trailer.transform.TransformPoint(front);
            offset.y = 0f;
            _rb.position += offset;
            trailer.transform.position += offset;
            _joinFrom = tractor.transform.InverseTransformPoint(trailer.transform.TransformPoint(front));
            _joinTo = rear;
            _joinTimer = 0f;

            _joint = _rb.gameObject.AddComponent<ConfigurableJoint>();
            _joint.connectedBody = tractorBody;
            _joint.autoConfigureConnectedAnchor = false;
            _joint.anchor = front;
            _joint.connectedAnchor = _joinFrom;
            _joint.enableCollision = false;
            _joint.axis = Vector3.up;
            _joint.secondaryAxis = Vector3.forward;
            _joint.xMotion = _joint.yMotion = _joint.zMotion = ConfigurableJointMotion.Locked;
            _joint.angularXMotion = _joint.angularYMotion = _joint.angularZMotion = ConfigurableJointMotion.Limited;
            _joint.lowAngularXLimit = new SoftJointLimit { limit = -70f };
            _joint.highAngularXLimit = new SoftJointLimit { limit = 70f };
            _joint.angularYLimit = new SoftJointLimit { limit = 15f };
            _joint.angularZLimit = new SoftJointLimit { limit = 25f };
            _joint.projectionMode = JointProjectionMode.PositionAndRotation;
            _joint.projectionDistance = 0.2f;
            _joint.projectionAngle = 5f;
            float breakForce = Mathf.Max(_rb.mass * 9.81f * BreakForceG, BreakForceMin);
            if (ControlRegistry.IsClientSimulated(tractor)) breakForce = Mathf.Infinity;
            _joint.breakForce = breakForce;
            _joint.breakTorque = float.IsInfinity(breakForce) ? Mathf.Infinity : breakForce * BreakTorqueArm;
            _rb.WakeUp();
        }

        public void Detach(PvLogisticsEvent reason)
        {
            if (_detached) return;
            GroundVehicle tractor = Tractor;
            _detached = true;
            Active.Remove(this);
            if (_joint != null) Destroy(_joint);
            foreach (Collider leg in _legs)
            {
                if (leg != null) leg.enabled = true;
            }
            _legs.Clear();
            if (Trailer != null && !Trailer.disabled)
            {
                bool slung = Trailer.IsSlung();
                Trailer.enabled = !slung;
                GameAccess.GroundVehicleResetStationary(Trailer) = true;
                if (_rb != null && !slung) _rb.interpolation = _interpolation;
                SettleStationary.Begin(Trailer);
            }
            GroundVehicle trailer = Trailer;
            Tractor = null;
            Destroy(this);
            try
            {
                LogisticsLink.Report(tractor, reason, trailer);
            }
            catch (System.Exception e)
            {
                ModLog.Warning("Towing: report failed: " + e.Message);
            }
        }

        private void DisableLegs(GroundVehicle trailer)
        {
            Vector3 origin = trailer.transform.position + Vector3.up * 5f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit ground, 20f, PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore)) return;
            var names = new System.Collections.Generic.List<string>();
            foreach (Collider collider in trailer.GetComponentsInChildren<Collider>())
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (collider.bounds.min.y > ground.point.y + LegClearance) continue;
                collider.enabled = false;
                _legs.Add(collider);
                names.Add(collider.name);
            }
            if (names.Count > 0) ModLog.Info("Towing " + trailer.unitName + ": ground colliders off while towed: " + string.Join(", ", names));
        }

        private void FixedUpdate()
        {
            if (_detached) return;
            if (Trailer == null || Trailer.disabled || Tractor == null || Tractor.disabled || _joint == null)
            {
                Detach(PvLogisticsEvent.HitchLost);
                return;
            }
            if (Trailer.IsSlung())
            {
                Detach(PvLogisticsEvent.HitchSlung);
                return;
            }
            if (_joinTimer < JoinTime)
            {
                _joinTimer += Time.fixedDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_joinTimer / JoinTime));
                _joint.connectedAnchor = Vector3.Lerp(_joinFrom, _joinTo, k);
            }
            _wheels.SetTowedBrake(TractorBrake());
            _wheels.Step(0f, 0f);
            Trailer.speed = _wheels.Speed;
            Trailer.radarAlt = _wheels.RadarAlt;
        }

        private void Update()
        {
            if (!_detached && Trailer != null && !Trailer.enabled) GameAccess.GroundVehicleUpdate(Trailer);
        }

        private void OnJointBreak(float force)
        {
            if (_detached) return;
            Detach(PvLogisticsEvent.HitchBroke);
        }

        private float TractorBrake()
        {
            if (VehicleSession.IsActive && VehicleSession.Vehicle == Tractor)
            {
                VehicleDriver driver = VehicleSession.Driver;
                return driver != null && driver.UsesVanillaPhysics ? driver.Vanilla.Brake : 0f;
            }
            RemoteDrivers.Session session = RemoteDrivers.FindByVehicle(Tractor);
            if (session != null && session.Driver != null)
            {
                return Mathf.Abs(session.Driver.Throttle) <= ModConfig.ThrottleDeadzone.Value ? 1f : 0f;
            }
            return 0f;
        }

        public static float HitchHeightGap(GroundVehicle tractor, GroundVehicle trailer)
        {
            float rear = tractor.transform.TransformPoint(RearPoint(tractor)).y;
            float front = trailer.transform.TransformPoint(FrontPoint(trailer)).y;
            return rear - front;
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            if (!_detached && _joint != null) Destroy(_joint);
        }

        public static Vector3 RearPoint(GroundVehicle tractor)
        {
            Bounds bounds = VehicleGeometry.CachedBounds(tractor);
            return new Vector3(0f, bounds.min.y + HitchHeight, bounds.min.z - RearClearance);
        }

        public static Vector3 FrontPoint(GroundVehicle trailer)
        {
            Bounds bounds = VehicleGeometry.CachedBounds(trailer);
            return new Vector3(0f, bounds.min.y + HitchHeight, bounds.max.z);
        }
    }

    internal sealed class ClientLoad
    {
        private const float MaxMass = 100000f;
        private const float RestSpeed = 0.05f;
        private const float Friction = 1f;

        private readonly List<Collider> _own = new List<Collider>();
        private readonly List<Collider> _load = new List<Collider>();

        private bool _carrying;
        private Vector3 _bedPosition;
        private RigidbodyInterpolation _carriedInterpolation;

        public bool Active { get; private set; }
        public GroundVehicle Unit { get; private set; }
        public float Mass { get; private set; }
        public bool Towed { get; private set; }

        private const float RestoreDelay = 1.5f;
        private float _restoreAt = -1f;

        public void Set(GroundVehicle vehicle, bool active, GroundVehicle load, float mass, bool towed)
        {
            if (!active)
            {
                Reset(false);
                return;
            }
            if (!Active || !ReferenceEquals(load, Unit) || towed != Towed)
            {
                EndCarry();
                RestoreCollisions();
                Unit = load;
                Towed = towed;
                IgnoreCollisions(vehicle, load);
                if (!towed) BeginCarry(vehicle, load);
            }
            Active = true;
            Mass = float.IsNaN(mass) || float.IsInfinity(mass) ? 0f : Mathf.Clamp(mass, 0f, MaxMass);
        }

        public void Clear()
        {
            Reset(true);
        }

        private void Reset(bool immediate)
        {
            EndCarry();
            if (immediate) RestoreCollisions();
            else if (_own.Count > 0) _restoreAt = Time.time + RestoreDelay;
            Active = false;
            Unit = null;
            Mass = 0f;
            Towed = false;
        }

        private void BeginCarry(GroundVehicle truck, GroundVehicle container)
        {
            if (truck == null || container == null || container.rb == null || !FlatbedBed.TryGet(truck, out FlatbedBed bed)) return;
            Bounds size = VehicleGeometry.LocalBounds(container);
            Vector3 bottomCenter = new Vector3(size.center.x, size.min.y, size.center.z);
            _bedPosition = bed.Center + Vector3.up * 0.05f - bottomCenter;
            _carriedInterpolation = container.rb.interpolation;
            _carrying = true;
        }

        private void EndCarry()
        {
            if (!_carrying) return;
            _carrying = false;
            Rigidbody body = Unit != null ? Unit.rb : null;
            if (body == null) return;
            body.isKinematic = VehicleDeploy.IsDeployed(Unit);
            body.interpolation = body.isKinematic ? RigidbodyInterpolation.None : _carriedInterpolation;
        }

        public void FixedTick(Rigidbody truckBody)
        {
            if (_restoreAt >= 0f && Time.time >= _restoreAt) RestoreCollisions();
            if (!_carrying || truckBody == null || Unit == null) return;
            Rigidbody body = Unit.rb;
            if (body == null) return;
            if (!body.isKinematic) body.isKinematic = true;
            if (body.interpolation != RigidbodyInterpolation.Interpolate) body.interpolation = RigidbodyInterpolation.Interpolate;

            float dt = Time.fixedDeltaTime;
            Vector3 spin = truckBody.angularVelocity * dt;
            Quaternion turn = spin.sqrMagnitude > 1e-10f ? Quaternion.AngleAxis(spin.magnitude * Mathf.Rad2Deg, spin.normalized) : Quaternion.identity;
            Quaternion rotation = turn * truckBody.rotation;
            Vector3 position = truckBody.position + truckBody.velocity * dt;
            body.MovePosition(position + rotation * _bedPosition);
            body.MoveRotation(rotation);
        }

        public void Step(Rigidbody rb, VanillaDrive vanilla)
        {
            if (!Active || !Towed || Mass <= 0f || rb == null || !vanilla.Supported) return;
            Vector3 up = rb.rotation * Vector3.up;
            Vector3 forward = rb.rotation * Vector3.forward;
            Vector3 velocity = Vector3.ProjectOnPlane(rb.velocity, up);
            float support = Mass * -Physics.gravity.y;

            rb.AddForce(-Physics.gravity * Mass);

            Vector3 side = Vector3.Cross(forward, up);
            Vector3 lateral = Vector3.Project(-velocity * Mass * 10f, side);
            Vector3 braking = -velocity * Mass * 10f * vanilla.Brake;
            Vector3 rolling = velocity.sqrMagnitude >= RestSpeed * RestSpeed
                ? -(velocity.normalized + velocity * 0.05f) * (0.05f * support)
                : Vector3.zero;
            rb.AddForce(Vector3.ClampMagnitude(lateral + braking + rolling, support * Friction));
        }

        private void IgnoreCollisions(GroundVehicle vehicle, GroundVehicle load)
        {
            if (vehicle == null || load == null) return;
            CollectColliders(vehicle, _own);
            CollectColliders(load, _load);
            foreach (Collider own in _own)
            {
                foreach (Collider other in _load) Physics.IgnoreCollision(own, other, true);
            }
        }

        private void RestoreCollisions()
        {
            _restoreAt = -1f;
            foreach (Collider own in _own)
            {
                if (!IsActive(own)) continue;
                foreach (Collider other in _load)
                {
                    if (IsActive(other)) Physics.IgnoreCollision(own, other, false);
                }
            }
            _own.Clear();
            _load.Clear();
        }

        private static void CollectColliders(GroundVehicle vehicle, List<Collider> into)
        {
            into.Clear();
            foreach (Collider collider in vehicle.GetComponentsInChildren<Collider>())
            {
                if (IsActive(collider) && !collider.isTrigger) into.Add(collider);
            }
        }

        private static bool IsActive(Collider collider)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
        }
    }

    internal sealed class SettleStationary : MonoBehaviour
    {
        private const float StillSpeed = 0.2f;
        private const float StillTime = 2f;
        private const float GiveUpTime = 60f;
        private const float SupportProbeHeight = 3f;
        private const float SupportProbeDepth = 8f;
        private static readonly RaycastHit[] SupportHits = new RaycastHit[16];
        private GroundVehicle _vehicle;
        private float _still;
        private float _started;

        public static void Begin(GroundVehicle vehicle)
        {
            if (vehicle == null || vehicle.GetComponent<SettleStationary>() != null) return;
            var settle = vehicle.gameObject.AddComponent<SettleStationary>();
            settle._vehicle = vehicle;
            settle._started = Time.time;
        }

        private void FixedUpdate()
        {
            Rigidbody body = _vehicle != null ? _vehicle.rb : null;
            if (body == null || _vehicle.disabled || VehicleDeploy.IsDeployed(_vehicle) || VehicleGeometry.IsBusy(_vehicle)
                || Time.time - _started > GiveUpTime)
            {
                Destroy(this);
                return;
            }
            _still = body.velocity.magnitude < StillSpeed ? _still + Time.fixedDeltaTime : 0f;
            if (_still < StillTime) return;
            if (OnMovingBody(_vehicle))
            {
                _still = 0f;
                return;
            }
            VehicleDeploy.Set(_vehicle, true);
            Destroy(this);
        }

        private static bool OnMovingBody(GroundVehicle vehicle)
        {
            Transform root = vehicle.transform;
            Vector3 origin = root.position + Vector3.up * SupportProbeHeight;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, SupportHits, SupportProbeHeight + SupportProbeDepth,
                ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Rigidbody support = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = SupportHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(root) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                support = hit.collider.attachedRigidbody;
            }
            return support != null && support != vehicle.rb
                && (!support.isKinematic || support.GetComponentInParent<Ship>() != null);
        }
    }

    internal static class VehicleGeometry
    {
        public static bool IsTrailer(GroundVehicle vehicle)
        {
            return VehicleDriver.IsStatic(vehicle) && WheelCount(vehicle) > 0;
        }

        public static bool IsContainer(GroundVehicle vehicle)
        {
            return VehicleDriver.IsStatic(vehicle) && WheelCount(vehicle) == 0;
        }

        private static int WheelCount(GroundVehicle vehicle)
        {
            GameObject[] wheels = GameAccess.GroundVehicleWheels != null ? GameAccess.GroundVehicleWheels(vehicle) : null;
            return wheels != null ? wheels.Length : 0;
        }

        public static bool IsBusy(GroundVehicle vehicle)
        {
            return vehicle.GetComponent<TowHitch>() != null || vehicle.GetComponent<CargoMount>() != null
                || ControlRegistry.TurretsOverridden(vehicle) || vehicle.GetComponentInParent<CargoMount>() != null
                || vehicle.GetComponent<Joint>() != null || vehicle.IsSlung();
        }

        private const float BoundsLifetime = 2f;
        private const int BoundsPruneSize = 64;

        private struct CachedBound
        {
            public Bounds Value;
            public float Expires;
        }

        private static readonly Dictionary<UnitDefinition, Bounds> TypeBounds = new Dictionary<UnitDefinition, Bounds>();
        private static readonly Dictionary<int, CachedBound> InstanceBounds = new Dictionary<int, CachedBound>();
        private static readonly List<int> ExpiredKeys = new List<int>();

        public static Bounds CachedBounds(GroundVehicle vehicle)
        {
            UnitDefinition definition = vehicle.definition;
            if (!VehicleDriver.IsStatic(vehicle))
            {
                if (definition == null) return LocalBounds(vehicle);
                if (!TypeBounds.TryGetValue(definition, out Bounds typed))
                {
                    typed = LocalBounds(vehicle);
                    TypeBounds[definition] = typed;
                }
                return typed;
            }

            float now = Time.time;
            int id = vehicle.GetInstanceID();
            if (InstanceBounds.TryGetValue(id, out CachedBound cached) && cached.Expires > now) return cached.Value;
            if (InstanceBounds.Count >= BoundsPruneSize) PruneInstanceBounds(now);
            cached.Value = LocalBounds(vehicle);
            cached.Expires = now + BoundsLifetime;
            InstanceBounds[id] = cached;
            return cached.Value;
        }

        private static void PruneInstanceBounds(float now)
        {
            ExpiredKeys.Clear();
            foreach (KeyValuePair<int, CachedBound> pair in InstanceBounds)
            {
                if (pair.Value.Expires <= now) ExpiredKeys.Add(pair.Key);
            }
            for (int i = 0; i < ExpiredKeys.Count; i++) InstanceBounds.Remove(ExpiredKeys[i]);
        }

        private static readonly List<MeshFilter> MeshFilters = new List<MeshFilter>();

        public static Bounds LocalBounds(GroundVehicle vehicle)
        {
            Transform root = vehicle.transform;
            float limit = vehicle.definition != null ? Mathf.Max(vehicle.definition.length, vehicle.definition.width) * 1.5f + 1f : 30f;
            bool any = false;
            var bounds = new Bounds();
            vehicle.GetComponentsInChildren(MeshFilters);
            for (int f = 0; f < MeshFilters.Count; f++)
            {
                MeshFilter filter = MeshFilters[f];
                if (filter.sharedMesh == null) continue;
                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                Vector3 size = Vector3.Scale(mesh.size, filter.transform.lossyScale);
                if (size.magnitude > limit * 2f) continue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                    else bounds.Encapsulate(local);
                }
            }
            MeshFilters.Clear();
            if (!any && vehicle.definition != null)
            {
                bounds = new Bounds(new Vector3(0f, vehicle.definition.height * 0.5f, 0f),
                    new Vector3(vehicle.definition.width, vehicle.definition.height, vehicle.definition.length));
            }
            return bounds;
        }
    }
}
