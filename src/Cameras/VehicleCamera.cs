using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class VehicleCamera
    {
        private const float MinPivotHeight = 2.8f;
        private const float RoofClearance = 1.2f;
        private const float DefaultPitch = 6f;
        private float _pivotHeight = MinPivotHeight;
        private const float MinDistance = 3.5f;
        private const float MaxPitch = 85f;
        private const float MinCollisionDistance = 0.8f;
        private const float GroundClearance = 0.6f;
        private const float HullMargin = 0.4f;
        private const float LoweredPivotClearance = 0.6f;
        private const float PivotDropSpeed = 14f;
        private const float ZoomSensitivityMultiplier = 0.12f;

        private GroundVehicle _vehicle;
        private Rigidbody _rb;
        private Camera _cam;

        private float _yaw;
        private float _pitch = DefaultPitch;
        private float _targetDistance;
        private float _currentDistance;
        private float _defaultFov = 60f;
        private float _zoomSpeed;
        private float _bodyRadius = 3f;
        private Bounds _hullBounds;
        private float _pivotDrop;

        public bool IsZoomed { get; private set; }

        public Ray AimRay { get; private set; }
        public bool HasAimRay { get; private set; }

        private const float LowShakeDegrees = 1.6f;
        private const float HighShakeDegrees = 0.5f;
        private float _lowShake;
        private float _highShake;
        private int[] _ammo = new int[0];

        private const float VerticalLagTime = 0.1f;
        private const float MaxVerticalLag = 0.6f;
        private float _verticalLag;
        private float _verticalLagVelocity;
        private float _lastPivotY;
        private bool _hasLastPivot;

        public static Ray ViewRay(Camera camera)
        {
            VehicleCamera view = VehicleSession.ViewCamera;
            if (VehicleSession.IsActive && view != null && view.HasAimRay) return view.AimRay;
            return camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        public void AddShake(float lowFreqShake, float highFreqShake)
        {
            float scale = ModConfig.ShakeScale.Value;
            _lowShake = Mathf.Min(_lowShake + lowFreqShake * scale, 1f);
            _highShake = Mathf.Min(_highShake + highFreqShake * scale, 1f);
        }

        public void DebugLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -ModConfig.CameraMaxLookUp.Value, MaxPitch);
        }

        private float MinThirdPersonDistance => Mathf.Max(MinDistance, _bodyRadius + 2f);
        public Camera CurrentCamera => _cam;

        public void Attach(GroundVehicle vehicle)
        {
            _vehicle = vehicle;
            _rb = vehicle.GetComponent<Rigidbody>();
            _cam = ResolveCamera();
            if (_cam != null) _defaultFov = _cam.fieldOfView;

            UnitDefinition definition = vehicle.definition;
            if (definition != null)
            {
                _bodyRadius = Mathf.Max(0.5f * Mathf.Sqrt(definition.length * definition.length + definition.width * definition.width
                    + definition.height * definition.height), 2f);
            }
            float centerOfMass = _rb != null ? _rb.centerOfMass.y : 0f;
            Bounds hull = VehicleGeometry.LocalBounds(vehicle);
            _pivotHeight = Mathf.Max(MinPivotHeight, hull.max.y - centerOfMass + RoofClearance);
            hull.Expand(HullMargin * 2f);
            _hullBounds = hull;
            _pivotDrop = 0f;
            _targetDistance = Mathf.Max(ModConfig.CameraDistance.Value, MinThirdPersonDistance);
            _currentDistance = _targetDistance;

            _yaw = vehicle.transform.eulerAngles.y;
            _pitch = DefaultPitch;
            IsZoomed = false;
            DebugInput.Zoom = false;
            _zoomSpeed = 0f;
            _lowShake = _highShake = 0f;
            _verticalLag = _verticalLagVelocity = 0f;
            _pivotDrop = 0f;
            _hasLastPivot = false;
            HasAimRay = false;
            RememberAmmo();
        }

        public void Detach()
        {
            if (_cam != null) _cam.fieldOfView = _defaultFov;

            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            if (csm != null) csm.desiredFOV = _defaultFov;

            _vehicle = null;
            _rb = null;
            _cam = null;
            IsZoomed = false;
            DebugInput.Zoom = false;
            HasAimRay = false;
        }

        private static Camera ResolveCamera()
        {
            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            if (csm != null && csm.mainCamera != null) return csm.mainCamera;
            return Camera.main;
        }

        public void UpdateCamera()
        {
            if (_vehicle == null) return;
            if (_cam == null)
            {
                _cam = ResolveCamera();
                if (_cam == null) return;
            }

            if (InputGate.MouseLookAllowed)
            {
                ReadMouse();
            }

            float targetFov = IsZoomed ? ModConfig.ZoomFov.Value : _defaultFov;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * 16f);

            Transform vt = _vehicle.transform;
            Vector3 pivot = (_rb != null ? vt.TransformPoint(_rb.centerOfMass) : vt.position) + Vector3.up * _pivotHeight;
            if (!IsZoomed) pivot.y += VerticalLag(pivot.y);
            WatchFire();

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredDir = rotation * -Vector3.forward;

            float actualDistance = IsZoomed ? -(_bodyRadius + 0.5f) : _targetDistance;

            float targetDrop = 0f;
            if (!IsZoomed && TryFindObstacle(pivot, desiredDir, actualDistance, out float obstacle))
            {
                float limited = Mathf.Clamp(obstacle - 0.2f, MinCollisionDistance, actualDistance);
                if (!InsideHull(vt, pivot + desiredDir * limited))
                {
                    _currentDistance = limited;
                }
                else
                {
                    Vector3 lowered = pivot;
                    lowered.y = Mathf.Min(pivot.y, vt.TransformPoint(new Vector3(0f, _hullBounds.max.y - HullMargin, 0f)).y + LoweredPivotClearance);
                    bool resolved = false;
                    if (lowered.y < pivot.y - 0.05f)
                    {
                        float loweredDistance = TryFindObstacle(lowered, desiredDir, actualDistance, out float loweredObstacle)
                            ? Mathf.Clamp(loweredObstacle - 0.2f, MinCollisionDistance, actualDistance)
                            : actualDistance;
                        if (!InsideHull(vt, lowered + desiredDir * loweredDistance))
                        {
                            targetDrop = pivot.y - lowered.y;
                            _currentDistance = loweredDistance;
                            resolved = true;
                        }
                    }
                    if (!resolved) _currentDistance = Mathf.Lerp(_currentDistance, actualDistance, Time.deltaTime * 14f);
                }
            }
            else
            {
                _currentDistance = Mathf.Lerp(_currentDistance, actualDistance, Time.deltaTime * 14f);
            }

            _pivotDrop = Mathf.Lerp(_pivotDrop, targetDrop, 1f - Mathf.Exp(-PivotDropSpeed * Time.deltaTime));
            if (_pivotDrop < 0.01f) _pivotDrop = 0f;
            pivot.y -= _pivotDrop;

            Vector3 position = KeepAboveGround(pivot + desiredDir * _currentDistance);
            AimRay = new Ray(position, rotation * Vector3.forward);
            HasAimRay = true;
            ApplyPose(position, rotation * Shake());
        }

        private float VerticalLag(float pivotY)
        {
            float delta = _hasLastPivot ? pivotY - _lastPivotY : 0f;
            _lastPivotY = pivotY;
            _hasLastPivot = true;
            if (Mathf.Abs(delta) > 5f) delta = 0f;
            _verticalLag = Mathf.Clamp(_verticalLag - delta, -MaxVerticalLag, MaxVerticalLag);
            _verticalLag = Mathf.SmoothDamp(_verticalLag, 0f, ref _verticalLagVelocity, VerticalLagTime, Mathf.Infinity, Time.deltaTime);
            return _verticalLag;
        }

        private Quaternion Shake()
        {
            float dt = Time.deltaTime;
            _lowShake = Mathf.Lerp(_lowShake, 0f, 5f * dt);
            _highShake = Mathf.Lerp(_highShake, 0f, 4f * dt);
            float low = Mathf.Max(_lowShake - 0.01f, 0f) * LowShakeDegrees;
            float high = Mathf.Max(_highShake - 0.05f, 0f) * HighShakeDegrees;
            if (low <= 0f && high <= 0f) return Quaternion.identity;
            float t = Time.timeSinceLevelLoad;
            Vector3 lowNoise = new Vector3(Mathf.PerlinNoise1D(t * 16f) - 0.5f, Mathf.PerlinNoise1D(t * 13.333f) - 0.5f, Mathf.PerlinNoise1D(t * 9.686f) - 0.5f);
            Vector3 highNoise = new Vector3(Mathf.PerlinNoise1D(t * 32f) - 0.5f, Mathf.PerlinNoise1D(t * 26.667f) - 0.5f, Mathf.PerlinNoise1D(t * 19.371f) - 0.5f);
            Vector3 angles = 2f * (lowNoise * low + highNoise * high);
            if (IsZoomed && _defaultFov > 0f) angles *= _cam.fieldOfView / _defaultFov;
            return Quaternion.Euler(angles.x, angles.y, angles.z * 0.5f);
        }

        private void WatchFire()
        {
            var stations = _vehicle.weaponStations;
            if (stations == null) return;
            if (_ammo.Length != stations.Count) RememberAmmo();
            for (int i = 0; i < stations.Count; i++)
            {
                WeaponStation station = stations[i];
                int ammo = station != null ? station.Ammo : 0;
                if (ammo < _ammo[i] && station != null) ShakeForShot(station.WeaponInfo, _ammo[i] - ammo);
                _ammo[i] = ammo;
            }
        }

        private void RememberAmmo()
        {
            var stations = _vehicle != null ? _vehicle.weaponStations : null;
            _ammo = new int[stations != null ? stations.Count : 0];
            for (int i = 0; i < _ammo.Length; i++) _ammo[i] = stations[i] != null ? stations[i].Ammo : 0;
        }

        private void ShakeForShot(WeaponInfo info, int shots)
        {
            if (info == null) return;
            float strength;
            if (info.missile || info.bomb) strength = 0.12f;
            else
            {
                float energy = 0.5f * Mathf.Max(info.massPerRound, 0f) * info.muzzleVelocity * info.muzzleVelocity;
                strength = Mathf.Clamp(energy / 6e6f, 0.02f, 0.8f) * Mathf.Clamp01(info.fireInterval / 0.25f);
            }
            strength *= Mathf.Min(shots, 4);
            AddShake(strength, strength * 0.6f);
        }

        private bool InsideHull(Transform vehicleTransform, Vector3 worldPoint)
        {
            return _hullBounds.Contains(vehicleTransform.InverseTransformPoint(worldPoint));
        }

        private static readonly RaycastHit[] _obstacleHits = new RaycastHit[24];

        private bool TryFindObstacle(Vector3 pivot, Vector3 direction, float distance, out float nearest)
        {
            nearest = NearestHit(pivot, direction, distance, out bool startsInside);
            if (startsInside)
            {
                float back = NearestHit(pivot + direction * distance, -direction, distance, out _);
                if (back < float.MaxValue) nearest = Mathf.Min(nearest, distance - back - 0.3f);
            }
            return nearest < float.MaxValue;
        }

        private float NearestHit(Vector3 origin, Vector3 direction, float distance, out bool startsInside)
        {
            startsInside = false;
            float nearest = float.MaxValue;
            int count = Physics.SphereCastNonAlloc(origin, 0.35f, direction, _obstacleHits, distance,
                ~PhysicsLayers.ExclusionZonesMask, QueryTriggerInteraction.Ignore);
            Transform own = _vehicle.transform;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _obstacleHits[i];
                Collider collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(own)) continue;
                Rigidbody body = collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                if (hit.distance <= 0f)
                {
                    startsInside = true;
                    continue;
                }
                if (hit.distance < nearest) nearest = hit.distance;
            }
            return nearest;
        }

        private void ReadMouse()
        {
            if (ModConfig.GameViewControls.Value)
            {
                ReadViewActions();
                return;
            }

            IsZoomed = Input.GetMouseButton(1) || DebugInput.Zoom;

            float sens = IsZoomed ? ZoomSensitivityMultiplier : 1f;
            _yaw += Input.GetAxis("Mouse X") * ModConfig.MouseSensitivityX.Value * sens;
            _pitch -= Input.GetAxis("Mouse Y") * ModConfig.MouseSensitivityY.Value * sens;
            _pitch = Mathf.Clamp(_pitch, -ModConfig.CameraMaxLookUp.Value, MaxPitch);

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f && !IsZoomed)
            {
                _targetDistance = Mathf.Clamp(_targetDistance - scroll * 10f, MinThirdPersonDistance,
                    Mathf.Max(ModConfig.CameraMaxDistance.Value, MinThirdPersonDistance));
            }
        }

        private void ReadViewActions()
        {
            IsZoomed = InputGate.ActionHeld(GameActions.FreeLook) || DebugInput.Zoom;

            float dt = Time.unscaledDeltaTime;
            float rate = Mathf.Min(_cam.fieldOfView / 20f, 1f) * 90f * PlayerSettings.viewSensitivity * dt;
            _yaw += InputGate.ActionAxis(GameActions.PanView) * rate;
            _pitch += InputGate.ActionAxis(GameActions.TiltView) * rate * (PlayerSettings.viewInvertPitch ? -1f : 1f);
            _pitch = Mathf.Clamp(_pitch, -ModConfig.CameraMaxLookUp.Value, MaxPitch);

            _zoomSpeed -= InputGate.ActionAxis(GameActions.ZoomView) * 60f * dt;
            _zoomSpeed = Mathf.Clamp(Mathf.Lerp(_zoomSpeed, 0f, 4f * dt), -10f, 10f);
            if (!IsZoomed)
            {
                _targetDistance = Mathf.Clamp(_targetDistance + _zoomSpeed * dt * _bodyRadius, MinThirdPersonDistance,
                    Mathf.Max(ModConfig.CameraMaxDistance.Value, MinThirdPersonDistance));
            }
            else _zoomSpeed = 0f;
        }

        private static Vector3 KeepAboveGround(Vector3 position)
        {
            const float probeHeight = 50f;
            Vector3 origin = position + Vector3.up * probeHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground, probeHeight + 20f, PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore))
            {
                float minY = ground.point.y + GroundClearance;
                if (position.y < minY) position.y = minY;
            }
            return position;
        }

        private void ApplyPose(Vector3 position, Quaternion rotation)
        {
            Transform camT = _cam.transform;
            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            Transform rigT = csm != null ? csm.transform : null;
            bool hasSeparateRig = rigT != null && rigT != camT;

            if (hasSeparateRig && !rigT.IsChildOf(camT)) rigT.SetPositionAndRotation(position, rotation);
            camT.SetPositionAndRotation(position, rotation);
            if (hasSeparateRig && rigT.IsChildOf(camT)) rigT.SetPositionAndRotation(position, rotation);
        }
    }
}
