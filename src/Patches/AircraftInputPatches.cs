using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [HarmonyPatch(typeof(PilotPlayerState), "PlayerControls")]
    internal static class PilotPlayerState_PlayerControls_Patch
    {
        private static bool Prefix()
        {
            return !VehicleSession.AircraftHandsOff;
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), nameof(PilotPlayerState.FixedUpdateState))]
    internal static class PilotPlayerState_FixedUpdateState_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("PilotPlayerState.FixedUpdateState patch");

        private static bool Prefix(Pilot __0)
        {
            try
            {
                if (!VehicleSession.AircraftHandsOff || __0 == null) return true;
                return !AircraftAutopilot.TryFly(__0.aircraft);
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    internal static class PilotPlayerState_PlayerAxisControls_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("PilotPlayerState.PlayerAxisControls patch");

        private static bool Prefix(PilotPlayerState __instance)
        {
            if (!VehicleSession.AircraftHandsOff) return true;

            try
            {
                ControlInputs inputs = GameAccess.PilotControlInputs(__instance);
                if (inputs != null)
                {
                    inputs.pitch = 0f;
                    inputs.roll = 0f;
                    inputs.yaw = 0f;
                }
                return false;
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }
}
