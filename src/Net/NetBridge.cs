using System.Collections.Generic;
using Mirage;
using NuclearOption.Networking;

namespace PlayableVehicles
{
    internal static class NetBridge
    {
        private static NetworkManagerNuclearOption _hooked;

        private static readonly HashSet<INetworkPlayer> _modClients = new HashSet<INetworkPlayer>();

        private static NetworkManagerNuclearOption Manager => NetworkManagerNuclearOption.i;

        public static bool IsServer
        {
            get
            {
                NetworkManagerNuclearOption manager = Manager;
                return manager != null && manager.Server != null && manager.Server.Active;
            }
        }

        public static bool IsClientOnly
        {
            get
            {
                NetworkManagerNuclearOption manager = Manager;
                return manager != null && manager.Client != null && manager.Client.Active && !IsServer;
            }
        }

        public static bool HasModClients => _modClients.Count > 0;

        public static void Tick()
        {
            NetworkManagerNuclearOption manager = Manager;
            if (manager == null || manager == _hooked) return;
            _hooked = manager;

            NetSerializers.Register();
            manager.Server.Started.AddListener(RegisterServerHandlers);
            manager.Server.Stopped.AddListener(OnServerStopped);
            manager.Server.Disconnected.AddListener(OnServerDisconnected);
            manager.Client.Started.AddListener(RegisterClientHandlers);
            manager.Client.Authenticated.AddListener(_ => ClientLink.SendHello());
            manager.Client.Disconnected.AddListener(_ => OnClientDisconnected());

            if (manager.Server.Active) RegisterServerHandlers();
            if (manager.Client.Active) RegisterClientHandlers();
        }

        public static void SendToServer<T>(T message, Channel channel)
        {
            INetworkPlayer server = Manager != null && Manager.Client != null ? Manager.Client.Player : null;
            server?.Send(message, channel);
        }

        public static int SendToModClients<T>(T message, Channel channel, INetworkPlayer exclude = null)
        {
            int sent = 0;
            foreach (INetworkPlayer connection in _modClients)
            {
                if (connection == exclude) continue;
                connection.Send(message, channel);
                sent++;
            }
            return sent;
        }

        public static T FindClientObject<T>(uint netId) where T : NetworkBehaviour
        {
            NetworkManagerNuclearOption manager = Manager;
            if (netId == 0 || manager == null || manager.Client == null || manager.Client.World == null) return null;
            return manager.Client.World.TryGetIdentity(netId, out NetworkIdentity identity) && identity != null
                ? identity.GetComponent<T>()
                : null;
        }

        private static void OnHello(INetworkPlayer connection, PvHello message)
        {
            if (_modClients.Contains(connection)) return;
            if (message.Version >= PvProtocol.FirstVersioned)
            {
                connection.Send(new PvVersion { Version = PvProtocol.Version }, Channel.Reliable);
            }
            if (message.Version != PvProtocol.Version)
            {
                string text = "Multiplayer: " + connection + " has PlayableVehicles protocol " + message.Version + ", host has "
                    + PvProtocol.Version + " - vehicle control disabled for this player";
                ModLog.Warning(text);
                ModLog.Notify("A player has another PlayableVehicles version (protocol " + message.Version + ", host "
                    + PvProtocol.Version + "): vehicle control disabled for them");
                return;
            }

            _modClients.Add(connection);
            ModLog.Info("Multiplayer: " + connection + " has PlayableVehicles protocol " + message.Version);
            connection.Send(new PvServerRules { Access = VehicleAccess.Mode }, Channel.Reliable);
        }

        private static void OnStateRequest(INetworkPlayer connection, PvStateRequest _)
        {
            KillCredit.SendStateTo(connection);
        }

        private static void RegisterServerHandlers()
        {
            MessageHandler handler = _hooked.Server.MessageHandler;
            handler.RegisterHandler<PvEnterRequest>(FromModClient<PvEnterRequest>(RemoteDrivers.OnEnter), false);
            handler.RegisterHandler<PvExitRequest>(FromModClient<PvExitRequest>(RemoteDrivers.OnExit), false);
            handler.RegisterHandler<PvInput>(FromModClient<PvInput>(RemoteDrivers.OnInput), false);
            handler.RegisterHandler<PvDeploy>(FromModClient<PvDeploy>(RemoteDrivers.OnDeploy), false);
            handler.RegisterHandler<PvLogisticsRequest>(FromModClient<PvLogisticsRequest>(LogisticsLink.OnRequest), false);
            handler.RegisterHandler<PvHello>(OnHello, false);
            handler.RegisterHandler<PvPurchaseRequest>(FromModClient<PvPurchaseRequest>(PurchaseServer.OnRequest), false);
            handler.RegisterHandler<PvStateRequest>(FromModClient<PvStateRequest>(OnStateRequest), false);
            ModLog.Info("Multiplayer: server handlers registered (protocol " + PvProtocol.Version + ")");
        }

        private static void RegisterClientHandlers()
        {
            MessageHandler handler = _hooked.Client.MessageHandler;
            handler.RegisterHandler<PvVersion>(ClientLink.OnVersion, false);
            handler.RegisterHandler<PvSessionReply>(FromCompatibleHost<PvSessionReply>(ClientLink.OnReply), false);
            handler.RegisterHandler<PvTurretAim>(FromCompatibleHost<PvTurretAim>(TurretSync.OnTurretAim), false);
            handler.RegisterHandler<PvHit>(FromCompatibleHost<PvHit>(HitFeedback.OnRemoteHit), false);
            handler.RegisterHandler<PvDriver>(FromCompatibleHost<PvDriver>(KillCredit.OnDriverMessage), false);
            handler.RegisterHandler<PvPurchaseReply>(FromCompatibleHost<PvPurchaseReply>(PurchaseClient.OnReply), false);
            handler.RegisterHandler<PvNotice>(FromCompatibleHost<PvNotice>(ClientLink.OnNotice), false);
            handler.RegisterHandler<PvDamageDirection>(FromCompatibleHost<PvDamageDirection>(HitFeedback.OnDamageDirection), false);
            handler.RegisterHandler<PvLogistics>(FromCompatibleHost<PvLogistics>(LogisticsLink.OnLogistics), false);
            handler.RegisterHandler<PvInputEcho>(FromCompatibleHost<PvInputEcho>(ClientLink.OnInputEcho), false);
            handler.RegisterHandler<PvIncomingHit>(FromCompatibleHost<PvIncomingHit>(HitFeedback.OnIncomingHit), false);
            handler.RegisterHandler<PvAmmoState>(FromCompatibleHost<PvAmmoState>(AmmoSync.OnAmmoState), false);
            handler.RegisterHandler<PvServerRules>(OnServerRules, false);
        }

        private static MessageDelegateWithPlayer<T> FromModClient<T>(MessageDelegateWithPlayer<T> handler)
        {
            return (connection, message) =>
            {
                if (_modClients.Contains(connection)) handler(connection, message);
            };
        }

        private static MessageDelegateWithPlayer<T> FromCompatibleHost<T>(MessageDelegateWithPlayer<T> handler)
        {
            return (connection, message) =>
            {
                if (ClientLink.HostCompatible) handler(connection, message);
            };
        }

        private static void OnServerRules(INetworkPlayer connection, PvServerRules message)
        {
            if (ClientLink.AcceptServerRules()) VehicleAccess.OnServerRules(connection, message);
        }

        private static void OnServerStopped()
        {
            RemoteDrivers.EndAll();
            PurchaseServer.Reset();
            _modClients.Clear();
        }

        private static void OnServerDisconnected(INetworkPlayer connection)
        {
            RemoteDrivers.OnDisconnected(connection);
            _modClients.Remove(connection);
        }

        private static void OnClientDisconnected()
        {
            ClientLink.OnDisconnected();
            TurretSync.ClientReset();
            PurchaseClient.Reset();
            LogisticsLink.ClientReset();
            VehicleAccess.ClientReset();
            KillCredit.RestoreAll();
        }
    }
}
