using System.Collections.Generic;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class KillCredit
    {
        private const float RestoreDelay = 15f;

        private sealed class Entry
        {
            public uint VehicleId;
            public Player Driver;
            public Player PreviousPlayer;
            public string PreviousName;
            public float RestoreTime = -1f;
        }

        private static readonly Dictionary<PersistentUnit, Entry> _entries = new Dictionary<PersistentUnit, Entry>();
        private static readonly List<PersistentUnit> _due = new List<PersistentUnit>();

        public static void Begin(GroundVehicle vehicle, Player driver)
        {
            if (vehicle == null || driver == null) return;
            if (!UnitRegistry.TryGetPersistentUnit(vehicle.persistentID, out PersistentUnit unit)) return;

            if (!_entries.TryGetValue(unit, out Entry entry))
            {
                entry = new Entry { VehicleId = vehicle.persistentID.Id, PreviousPlayer = unit.player, PreviousName = unit.unitName };
                _entries.Add(unit, entry);
            }
            entry.RestoreTime = -1f;
            entry.Driver = driver;
            unit.player = driver;
            unit.unitName = DriverLabel(driver, vehicle, entry.PreviousName);

            Broadcast(entry, true);
        }

        public static void End(GroundVehicle vehicle)
        {
            if ((object)vehicle == null) return;
            if (UnitRegistry.TryGetPersistentUnit(vehicle.persistentID, out PersistentUnit unit) && _entries.TryGetValue(unit, out Entry entry))
            {
                entry.RestoreTime = Time.time + RestoreDelay;
                Broadcast(entry, false);
            }
        }

        public static bool TryGetActiveDriver(GroundVehicle vehicle, out Player driver)
        {
            driver = null;
            if (vehicle == null || _entries.Count == 0) return false;
            if (!UnitRegistry.TryGetPersistentUnit(vehicle.persistentID, out PersistentUnit unit) || !_entries.TryGetValue(unit, out Entry entry)) return false;
            if (entry.RestoreTime >= 0f || entry.Driver == null) return false;
            driver = entry.Driver;
            return true;
        }

        public static string DisplayName(Player player)
        {
            try
            {
                return player.GetDisplayName(PlayerNameContext.Other);
            }
            catch
            {
                return "another player";
            }
        }

        private static void Broadcast(Entry entry, bool driving)
        {
            if (!NetBridge.IsServer || !NetBridge.HasModClients || entry.Driver == null) return;
            NetBridge.SendToModClients(new PvDriver { VehicleId = entry.VehicleId, PlayerNetId = entry.Driver.NetId, Driving = driving }, Channel.Reliable);
        }

        public static void Tick()
        {
            if (_entries.Count == 0) return;

            _due.Clear();
            foreach (KeyValuePair<PersistentUnit, Entry> pair in _entries)
            {
                if (pair.Value.RestoreTime >= 0f && Time.time >= pair.Value.RestoreTime) _due.Add(pair.Key);
            }
            foreach (PersistentUnit unit in _due)
            {
                Entry entry = _entries[unit];
                Restore(unit);
                if (NetBridge.IsServer && NetBridge.HasModClients)
                {
                    NetBridge.SendToModClients(new PvDriver { VehicleId = entry.VehicleId, PlayerNetId = 0 }, Channel.Reliable);
                }
            }
        }

        public static void RestoreAll()
        {
            foreach (KeyValuePair<PersistentUnit, Entry> pair in _entries)
            {
                pair.Key.player = pair.Value.PreviousPlayer;
                pair.Key.unitName = pair.Value.PreviousName;
            }
            _entries.Clear();
        }

        public static void Reset()
        {
            _entries.Clear();
            _due.Clear();
        }

        public static void SendStateTo(INetworkPlayer connection)
        {
            foreach (Entry entry in _entries.Values)
            {
                if (entry.Driver == null) continue;
                connection.Send(new PvDriver { VehicleId = entry.VehicleId, PlayerNetId = entry.Driver.NetId, Driving = entry.RestoreTime < 0f }, Channel.Reliable);
            }
        }

        public static void OnDriverMessage(INetworkPlayer _, PvDriver message)
        {
            if (NetBridge.IsServer) return;
            if (!UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = message.VehicleId }), out Unit unit)) return;
            GroundVehicle vehicle = unit as GroundVehicle;
            if (vehicle == null) return;

            if (message.PlayerNetId == 0)
            {
                if (UnitRegistry.TryGetPersistentUnit(vehicle.persistentID, out PersistentUnit persistent) && _entries.ContainsKey(persistent))
                {
                    Restore(persistent);
                }
                return;
            }

            Player driver = NetBridge.FindClientObject<Player>(message.PlayerNetId);
            if (driver == null) return;
            Begin(vehicle, driver);
            if (!message.Driving) End(vehicle);
        }

        private static void Restore(PersistentUnit unit)
        {
            Entry entry = _entries[unit];
            unit.player = entry.PreviousPlayer;
            unit.unitName = entry.PreviousName;
            _entries.Remove(unit);
        }

        private static string DriverLabel(Player driver, GroundVehicle vehicle, string fallbackType)
        {
            string type = vehicle.definition != null && !string.IsNullOrEmpty(vehicle.definition.unitName) ? vehicle.definition.unitName : fallbackType;
            return DisplayName(driver) + " [" + type + "]";
        }
    }
}
