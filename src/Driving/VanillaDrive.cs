using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class VanillaDrive
    {
        private const float MaxSteering = 0.5f;
        private static float FullSteering => Mathf.Min(ModConfig.TurnRate.Value * Mathf.Deg2Rad * 0.5f, MaxSteering);
        private bool _tracked;
        private const float ReverseBrakeSpeed = 0.5f;
        private const float RestSpeed = 0.05f;
        private const float HeavySupportFactor = 20f;
        private readonly RaycastHit[] _groundHits = new RaycastHit[16];
        private Rigidbody _carrier;
        private Vector3 _carrierAngularVelocity;

        private GroundVehicle _vehicle;
        private Rigidbody _rb;
        private float _mass;
        private float _acceleration;
        private float _topSpeedOnroad;
        private float _topSpeedOffroad;
        private float _suspensionTravel;
        private float _springRate;
        private float _dampingRate;
        private float _frictionCoef;
        private float _spawnOffset;

        public float Speed { get; private set; }
        public float RadarAlt { get; private set; }
        public bool Grounded { get; private set; }
        public bool Supported { get; private set; }
        public bool OnPaved { get; private set; }
        public float Brake { get; private set; }
        public float TopSpeed => (OnPaved ? _topSpeedOnroad : _topSpeedOffroad) * 0.277777f;

        public static bool Available => GameAccess.GroundVehicleAcceleration != null && GameAccess.GroundVehicleTopSpeedOnroad != null
            && GameAccess.GroundVehicleTopSpeedOffroad != null && GameAccess.GroundVehicleSuspensionTravel != null
            && GameAccess.GroundVehicleSpringRate != null && GameAccess.GroundVehicleDampingRate != null
            && GameAccess.GroundVehicleFrictionCoef != null;

        public bool Init(GroundVehicle vehicle, Rigidbody rb)
        {
            _vehicle = vehicle;
            _rb = rb;
            _towed = false;
            _towBrake = 0f;
            Brake = 0f;
            _carrier = null;
            _carrierAngularVelocity = Vector3.zero;
            if (!Available || vehicle == null || rb == null) return false;

            _acceleration = GameAccess.GroundVehicleAcceleration(vehicle);
            _topSpeedOnroad = GameAccess.GroundVehicleTopSpeedOnroad(vehicle);
            _topSpeedOffroad = GameAccess.GroundVehicleTopSpeedOffroad(vehicle);
            _suspensionTravel = GameAccess.GroundVehicleSuspensionTravel(vehicle);
            _springRate = GameAccess.GroundVehicleSpringRate(vehicle);
            _dampingRate = GameAccess.GroundVehicleDampingRate(vehicle);
            _frictionCoef = GameAccess.GroundVehicleFrictionCoef(vehicle);
            _mass = vehicle.definition != null && vehicle.definition.mass > 0f ? vehicle.definition.mass : rb.mass;
            _spawnOffset = vehicle.definition != null ? vehicle.definition.spawnOffset.y : 0f;
            _tracked = vehicle.definition is VehicleDefinition vehicleDefinition && vehicleDefinition.vehicleType == VehicleType.MBT;
            return _suspensionTravel > 0f && _springRate > 0f;
        }

        public float Mass => _mass;

        public void InitTowed(GroundVehicle vehicle, Rigidbody rb, float standingHeight)
        {
            Init(vehicle, rb);
            _vehicle = vehicle;
            _rb = rb;
            if (_mass <= 0f) _mass = vehicle.definition != null && vehicle.definition.mass > 0f ? vehicle.definition.mass : rb.mass;
            if (vehicle.definition != null) _spawnOffset = vehicle.definition.spawnOffset.y;
            _acceleration = 0f;
            if (_suspensionTravel < standingHeight + 0.1f || _springRate <= 0f)
            {
                _suspensionTravel = Mathf.Max(standingHeight, 0.3f) + 0.3f;
                _springRate = _mass * 9.81f / 0.3f;
                _dampingRate = 1.2f * Mathf.Sqrt(_springRate * _mass);
            }
            if (_dampingRate <= 0f) _dampingRate = 1.2f * Mathf.Sqrt(_springRate * _mass);
            if (_frictionCoef <= 0f) _frictionCoef = 1f;
            _towed = true;
        }

        private bool _towed;
        private float _towBrake;

        public void SetTowedBrake(float brake)
        {
            _towBrake = Mathf.Clamp01(brake);
        }

        public void Step(float throttle, float steer)
        {
            throttle = _towed ? 0f : Mathf.Clamp(throttle, -1f, 1f);

            Transform t = _vehicle.transform;
            Vector3 position = t.position;
            Vector3 up = t.up;
            Vector3 down = -up;
            Vector3 forward = t.forward;

            Grounded = false;
            Supported = false;
            Brake = 0f;
            Vector3 velocity = _rb.velocity;
            Vector3 carrierAngularVelocity = Vector3.zero;
            Rigidbody carrier = null;
            Plane surface;
            if (SampleGround(position, down, out RaycastHit hit))
            {
                surface = new Plane(hit.normal, hit.point);
                OnPaved = GameAssets.i == null || hit.collider.sharedMaterial != GameAssets.i.terrainMaterial;
                Rigidbody support = hit.collider.attachedRigidbody;
                if (IsCarrier(support, hit.collider))
                {
                    carrier = support;
                    carrierAngularVelocity = support.angularVelocity;
                    velocity -= support.GetPointVelocity(hit.point);
                }
            }
            else
            {
                surface = new Plane(Vector3.up, new Vector3(0f, position.y - 1000f, 0f));
            }
            FollowCarrier(carrier, carrierAngularVelocity);

            Speed = Vector3.Dot(velocity, forward);
            float alt = surface.Raycast(new Ray(position, down), out float distance) ? Mathf.Max(distance, 0f) : 100f;
            RadarAlt = Mathf.Max(alt - _spawnOffset, 0f);
            AntiRollover();
            if (alt > _suspensionTravel * 2f) return;
            Grounded = true;

            float engineThrottle = throttle;
            float brake = 0f;
            if (!_towed)
            {
                if (Mathf.Abs(throttle) <= ModConfig.ThrottleDeadzone.Value)
                {
                    engineThrottle = 0f;
                    brake = 1f;
                }
                else if (Mathf.Abs(Speed) > ReverseBrakeSpeed && (throttle > 0f) != (Speed > 0f))
                {
                    brake = Mathf.Abs(throttle);
                }
            }
            else
            {
                brake = _towBrake;
            }
            float engineOutput = engineThrottle * _acceleration;

            float steering = _towed ? 0f : Mathf.Clamp(steer, -1f, 1f) * FullSteering;
            if (!_tracked && !_towed)
            {
                steering *= Mathf.Clamp01(Mathf.Abs(Speed) / ModConfig.FullSteeringSpeed.Value) * (Speed < -ReverseBrakeSpeed ? -1f : 1f);
            }

            float topSpeed = Speed < 0f && throttle < 0f ? TopSpeed * ModConfig.ReverseSpeedFactor.Value : TopSpeed;
            if (!_towed && Mathf.Abs(Speed) > topSpeed)
            {
                brake = Mathf.Clamp01(brake + Mathf.Clamp01((Mathf.Abs(Speed) - topSpeed) * 0.1f));
            }

            Brake = _towed ? 0f : brake;

            Vector3 normal = surface.normal;
            float spring = _springRate * Mathf.Max(_suspensionTravel - alt, 0f);
            Supported = spring > 0f;
            float damping = spring > 0f ? -_dampingRate * Mathf.Min(Vector3.Dot(normal, velocity), 0f) : 0f;
            float support2 = spring + damping;
            Vector3 suspension = normal * support2;

            Vector3 levelTorque = 1.5f * -Vector3.Cross(normal, up) - 0.1f * (_rb.angularVelocity - carrierAngularVelocity);
            Vector3 side = Vector3.Cross(forward, normal);
            Vector3 resist = new Vector3(-suspension.x, 0f, -suspension.z) - velocity * _mass * 10f;
            Vector3 lateral = Vector3.Project(-velocity * 10f + resist, side);
            Vector3 braking = (-velocity * 10f + resist) * brake;

            Vector3 drive = Vector3.zero;
            if (Mathf.Abs(Speed) < topSpeed)
            {
                float accel = Mathf.Clamp(engineOutput * 40f / Mathf.Max(Mathf.Abs(Speed), 1f), -_acceleration * 5f, _acceleration * 5f);
                drive = forward * _mass * accel;
            }
            Vector3 rolling = Vector3.zero;
            if (_towed || engineThrottle != 0f || velocity.sqrMagnitude >= RestSpeed * RestSpeed)
            {
                rolling = new Vector3(Mathf.Sign(-velocity.x) - velocity.x * 0.05f, Mathf.Sign(-velocity.y) - velocity.y * 0.05f,
                    Mathf.Sign(-velocity.z) - velocity.z * 0.05f) * (0.05f * support2);
            }
            Vector3 grip = Vector3.ClampMagnitude(lateral + braking + rolling, support2 * _frictionCoef) + drive;

            _rb.AddForce(suspension + grip);
            _rb.AddTorque(steering * 0.2f * up + levelTorque, ForceMode.VelocityChange);
        }

        private bool SampleGround(Vector3 position, Vector3 down, out RaycastHit ground)
        {
            ground = default;
            int count = Physics.RaycastNonAlloc(position, down, _groundHits, _suspensionTravel * 3f,
                ~PhysicsLayers.ExclusionZonesMask, QueryTriggerInteraction.UseGlobal);
            Transform own = _vehicle.transform;
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = _groundHits[i].collider;
                if (collider == null || _groundHits[i].distance >= nearest) continue;
                if (collider.attachedRigidbody == _rb || collider.transform.IsChildOf(own)) continue;
                nearest = _groundHits[i].distance;
                ground = _groundHits[i];
                found = true;
            }
            return found;
        }

        private bool IsCarrier(Rigidbody body, Collider collider)
        {
            if (body == null || body == _rb) return false;
            return collider.gameObject.layer == PhysicsLayers.Ships || body.isKinematic || body.mass >= _mass * HeavySupportFactor;
        }

        private void FollowCarrier(Rigidbody carrier, Vector3 carrierAngularVelocity)
        {
            if (carrier != null && carrier == _carrier)
            {
                _rb.angularVelocity += carrierAngularVelocity - _carrierAngularVelocity;
            }
            _carrier = carrier;
            _carrierAngularVelocity = carrierAngularVelocity;
        }

        private void AntiRollover()
        {
            Transform t = _vehicle.transform;
            if (Mathf.Abs(Speed) >= 1f || _vehicle.disabled || Vector3.Dot(t.up, Vector3.up) >= 0.8f) return;
            Vector3 local = t.InverseTransformDirection(_rb.angularVelocity);
            Vector3 axis = Vector3.Dot(t.right, Vector3.up) > 0f ? -t.forward : t.forward;
            _rb.AddTorque(axis * (0.3f - local.z), ForceMode.VelocityChange);
        }
    }
}
