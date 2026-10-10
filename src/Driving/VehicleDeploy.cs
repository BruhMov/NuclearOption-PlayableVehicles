using System;
using Mirage;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class VehicleDeploy
    {
        public const float MaxDeploySpeed = 1f;

        public static bool CanDeploy(GroundVehicle vehicle)
        {
            if (vehicle == null || GameAccess.GroundVehicleDeployableParts == null) return false;
            Array parts = GameAccess.GroundVehicleDeployableParts(vehicle);
            return parts != null && parts.Length > 0;
        }

        public static bool IsDeployed(GroundVehicle vehicle)
        {
            return vehicle != null && vehicle.NetworknetworkStationary;
        }

        public static void Toggle(GroundVehicle vehicle, VehicleDriver driver)
        {
            if (!CanDeploy(vehicle))
            {
                ModLog.Notify(vehicle.unitName + " has nothing to deploy");
                return;
            }

            bool deploy = !IsDeployed(vehicle);
            if (deploy && driver.BodyVelocity.magnitude > MaxDeploySpeed)
            {
                ModLog.Notify("Stop the vehicle to deploy");
                return;
            }

            if (NetBridge.IsClientOnly)
            {
                NetBridge.SendToServer(new PvDeploy { VehicleId = vehicle.persistentID.Id, Deploy = deploy }, Channel.Reliable);
            }
            else
            {
                Set(vehicle, deploy);
            }
            ModLog.Notify(deploy ? "Deploying " + vehicle.unitName : "Stowing " + vehicle.unitName);
        }

        public static void Set(GroundVehicle vehicle, bool deploy)
        {
            if (vehicle == null || IsDeployed(vehicle) == deploy) return;
            vehicle.NetworknetworkStationary = deploy;
            if (!vehicle.IsHost) vehicle.OnStationaryChanged(deploy);
        }
    }
}
