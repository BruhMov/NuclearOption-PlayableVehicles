using UnityEngine;

namespace PlayableVehicles
{
    internal static class AircraftAutopilot
    {
        private const float MinAltitude = 300f;
        private const float MaxAltitude = 3000f;
        private const float DefaultAltitude = 600f;
        private const float MinOrbitRadius = 2500f;
        private const float OrbitLeadAngle = 60f;
        private const float BankAllowed = 60f;
        private const float AimEffort = 0.9f;

        private static Aircraft _aircraft;
        private static GlobalPosition _center;
        private static Vector3 _heading;
        private static float _altitude;
        private static float _radius;

        public static void Engage(Aircraft aircraft)
        {
            _aircraft = null;
            if (!ModConfig.AircraftAutopilot.Value || aircraft == null || aircraft.disabled || aircraft.autopilot == null) return;

            _aircraft = aircraft;
            _center = aircraft.GlobalPosition();
            _heading = aircraft.transform.forward;
            _altitude = aircraft.radarAlt > MinAltitude ? Mathf.Min(aircraft.radarAlt, MaxAltitude) : DefaultAltitude;
            AircraftParameters parameters = aircraft.GetAircraftParameters();
            _radius = Mathf.Max(MinOrbitRadius, parameters != null ? parameters.turningRadius * 3f : 0f);

            if (IsAirborne(aircraft)) ModLog.Notify("Autopilot engaged: aircraft holds over its current position");
        }

        public static void Disengage()
        {
            _aircraft = null;
        }

        public static bool TryFly(Aircraft aircraft)
        {
            if (_aircraft == null || aircraft != _aircraft || aircraft.disabled || !IsAirborne(aircraft)) return false;

            Autopilot autopilot = aircraft.autopilot;
            if (autopilot == null) return false;

            if (IsHovering(autopilot))
            {
                autopilot.Hover(_center, 0f, _heading);
                return true;
            }

            AircraftParameters parameters = aircraft.GetAircraftParameters();
            ControlInputs inputs = aircraft.GetInputs();
            if (inputs != null) inputs.throttle = CruiseThrottle(aircraft, parameters);

            autopilot.AutoAim(OrbitWaypoint(aircraft), false, false, false, AimEffort, BankAllowed, true, _altitude, Vector3.zero);
            return true;
        }

        private static bool IsHovering(Autopilot autopilot)
        {
            return autopilot is AutopilotHelo || autopilot is AutopilotTiltwing;
        }

        private static bool IsAirborne(Aircraft aircraft)
        {
            if (aircraft.IsLanded() || aircraft.radarAlt < 5f) return false;
            if (aircraft.autopilot != null && IsHovering(aircraft.autopilot)) return true;

            AircraftParameters parameters = aircraft.GetAircraftParameters();
            float landingSpeed = parameters != null ? parameters.landingSpeed : 30f;
            return aircraft.speed > landingSpeed * 0.8f;
        }

        private static GlobalPosition OrbitWaypoint(Aircraft aircraft)
        {
            Vector3 radial = aircraft.GlobalPosition() - _center;
            radial.y = 0f;
            if (radial.sqrMagnitude < 1f)
            {
                radial = aircraft.transform.forward;
                radial.y = 0f;
            }
            Vector3 onCircle = Quaternion.AngleAxis(OrbitLeadAngle, Vector3.up) * radial.normalized * _radius;
            return _center + onCircle;
        }

        private static float CruiseThrottle(Aircraft aircraft, AircraftParameters parameters)
        {
            float target = parameters != null && parameters.cornerSpeed > 0f
                ? parameters.cornerSpeed
                : (parameters != null ? parameters.landingSpeed : 30f) * 2f;
            return Mathf.Clamp01(0.5f + (target - aircraft.speed) * 0.05f);
        }
    }
}
