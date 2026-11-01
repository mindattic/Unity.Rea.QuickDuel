using System;
using Unity.Multiplayer;      // NetSession
using Unity.Multiplayer.Core; // Disconnection, DisconnectText
using UnityEngine;

namespace Unity.Rea.QuickDuel.Online
{
    // The session flow of online PvP in the Duel scene (UQD-T21), apart from any menu so a test can drive it with the
    // Unity.Multiplayer test kit's own sessions. On the host it spawns the one OnlineDuel once the session is up, seats
    // the host's own connection on the left and the first player to join on the right, and frees that seat when the
    // player leaves. On every peer it hands the OnlineDuel to the scene's DuelController as its Online source once it
    // arrives, and takes it back when the session ends, so DuelSession and DuelView play the online duel and then fall
    // back to the local game untouched.
    public sealed class OnlineDuelLobby : IDisposable
    {
        // The OnlineDuel's network key: one per session.
        public const string DuelKey = "quickduel.duel";

        private readonly NetSession session;
        private readonly GameObject duelPrefab;
        private readonly DuelController controller;

        public OnlineDuelLobby(NetSession session, GameObject duelPrefab, DuelController controller)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.duelPrefab = duelPrefab;
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            session.Started += OnStarted;
            session.Ended += OnEnded;
            session.ClientConnected += OnClientConnected;
            session.ClientDisconnected += OnClientDisconnected;
            session.Objects.Spawned += OnObjectSpawned;
            session.Objects.Despawned += OnObjectDespawned;
            if (session.IsLive)
                OnStarted();
        }

        // This peer's OnlineDuel once it has arrived (spawned, on the host), else null.
        public OnlineDuel Duel { get; private set; }

        // A sentence for the player: what the session is doing, or why it ended. Empty when there is nothing to say.
        public string Status { get; private set; } = "";

        public bool IsActive => session.IsActive;
        public bool IsHost => session.IsHost;

        // The status or the duel changed.
        public event Action Changed;

        public void Dispose()
        {
            session.Started -= OnStarted;
            session.Ended -= OnEnded;
            session.ClientConnected -= OnClientConnected;
            session.ClientDisconnected -= OnClientDisconnected;
            session.Objects.Spawned -= OnObjectSpawned;
            session.Objects.Despawned -= OnObjectDespawned;
            Detach();
        }

        // Called by the menu right after it asks the session to host or join, so the player sees it is under way.
        public void Starting(string status) => SetStatus(status);

        // Called by the menu when a start or join call was refused or threw.
        public void Failed(string status) => SetStatus(status);

        private void OnStarted()
        {
            if (session.IsServer)
            {
                if (duelPrefab == null)
                {
                    SetStatus("Can't host: the scene has no online duel prefab.");
                    return;
                }
                if (!session.Objects.TryGet(DuelKey, out OnlineDuel duel))
                    duel = session.Objects.Spawn(duelPrefab, DuelKey).GetComponent<OnlineDuel>();
                Attach(duel);
                // The host's own player duels on the left.
                foreach (var client in session.ConnectedClientIds)
                    OnClientConnected(client);
                SetStatus(session.IsHost ? HostWaitingStatus() : "Serving");
            }
            else
            {
                if (session.Objects.TryGet(DuelKey, out OnlineDuel duel))
                    Attach(duel);
                SetStatus("Connected");
            }
        }

        private string HostWaitingStatus()
        {
            var config = session.Config;
            return config != null ? $"Hosting on port {config.Port}" : "Hosting";
        }

        private void OnEnded(Disconnection disconnection)
        {
            Detach();
            SetStatus(DisconnectText.ShouldShow(disconnection) ? DisconnectText.For(disconnection).ToString() : "");
        }

        private void OnClientConnected(ulong client)
        {
            if (Duel == null || !session.IsServer)
                return;
            if (session.IsHost && client == session.LocalClientId)
            {
                if (Duel.LeftClient != client)
                    Duel.Seat(client, Duel.RightClient);
                return;
            }
            if (Duel.RightClient == OnlineDuel.NoClient && Duel.LeftClient != client)
            {
                Duel.Seat(Duel.LeftClient, client);
                SetStatus("A player joined");
            }
        }

        private void OnClientDisconnected(ulong client)
        {
            if (Duel == null || !session.IsServer)
                return;
            if (Duel.LeftClient == client || Duel.RightClient == client)
            {
                Duel.Unseat(client);
                SetStatus("The other player left");
            }
        }

        private void OnObjectSpawned(NetKey key)
        {
            if (key != null && key.Key == DuelKey && Duel == null && key.TryGetComponent(out OnlineDuel duel))
                Attach(duel);
        }

        private void OnObjectDespawned(NetKey key)
        {
            if (key != null && key.Key == DuelKey)
                Detach();
        }

        private void Attach(OnlineDuel duel)
        {
            if (duel == null || Duel == duel)
                return;
            Duel = duel;
            if (controller != null)
                controller.Online = duel;
            Changed?.Invoke();
        }

        private void Detach()
        {
            if (Duel == null)
                return;
            if (controller != null && ReferenceEquals(controller.Online, Duel))
                controller.Online = null;
            Duel = null;
            Changed?.Invoke();
        }

        private void SetStatus(string status)
        {
            Status = status ?? "";
            Changed?.Invoke();
        }
    }
}
