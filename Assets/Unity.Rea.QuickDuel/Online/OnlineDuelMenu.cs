using System;
using Unity.Multiplayer;      // NetBootstrap
using Unity.Multiplayer.Core; // HostAddress, JoinCodeProvider
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel.Online
{
    // The Duel scene's Host / Join menu (UQD-T21): a small panel at the left edge, under the left score, built in
    // code on DuelView's UI Toolkit tree (UQD-T31), shown between rounds. Host starts a listen server on the scene's
    // NetBootstrap (this player duels on the left); Join dials an address ("192.168.1.20" or "192.168.1.20:7777") and
    // plays the right; Leave ends either and drops back to the local game. The session flow itself is OnlineDuelLobby's;
    // this only draws it and turns clicks into its calls. DuelSceneBuilder adds it, wired to the scene's NetBootstrap
    // and the OnlineDuel prefab.
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class OnlineDuelMenu : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.10f, 0.08f, 0.08f);
        private static readonly Color Panel = new Color(0.10f, 0.08f, 0.08f, 0.82f);
        private static readonly Color Paper = new Color(0.93f, 0.88f, 0.78f);

        [SerializeField] private NetBootstrap bootstrap;
        [SerializeField] private GameObject duelPrefab;

        private DuelController controller;
        private DuelView view;
        private VisualElement panel;
        private Button hostButton;
        private Button joinButton;
        private Button leaveButton;
        private TextField address;
        private Label status;

        public OnlineDuelLobby Lobby { get; private set; }

        public bool IsShown => panel != null && panel.style.display != DisplayStyle.None;
        public string StatusText => status != null ? status.text : "";
        public string Address
        {
            get => address != null ? address.value : "";
            set { if (address != null) address.value = value; }
        }

        // Called by DuelSceneBuilder (and tests) in place of Inspector wiring.
        public void Wire(NetBootstrap netBootstrap, GameObject onlineDuelPrefab)
        {
            bootstrap = netBootstrap;
            duelPrefab = onlineDuelPrefab;
        }

        private void Start()
        {
            controller = GetComponent<DuelController>();
            view = GetComponent<DuelView>();
            if (bootstrap == null)
                bootstrap = NetBootstrap.Instance;
            Build();
            if (bootstrap == null || bootstrap.Session == null)
            {
                status.text = "Online play is not set up in this scene.";
                hostButton.SetEnabled(false);
                joinButton.SetEnabled(false);
                return;
            }

            // One opponent, joined by address: no join-code service is set up for QuickDuel.
            if (!bootstrap.Session.IsActive)
                bootstrap.Configure(c => { c.MaxPlayers = 2; c.JoinCodes = JoinCodeProvider.None; });

            Lobby = new OnlineDuelLobby(bootstrap.Session, duelPrefab, controller);
            Lobby.Changed += Redraw;
            Redraw();
        }

        private void OnDestroy()
        {
            if (Lobby != null)
            {
                Lobby.Changed -= Redraw;
                Lobby.Dispose();
                Lobby = null;
            }
        }

        private void Update()
        {
            // Shown between rounds only, never over a round being played.
            if (panel != null)
                SetVisible(panel, controller.CanBeginRound);
        }

        // Hosts a listen server; this machine duels on the left. False when it could not start.
        public bool Host() => Try(() => bootstrap.StartHost(), "Starting the host...");

        // Joins the host at `text` ("address" or "address:port"); this machine duels on the right.
        public bool Join(string text)
        {
            if (Lobby == null)
                return false;
            var port = bootstrap.BuildConfig().Port;
            if (!HostAddress.TryParse((text ?? "").Trim(), port, out var target, out var error))
            {
                Lobby.Failed(error);
                return false;
            }
            return Try(() => bootstrap.JoinByAddress(target.Host, target.Port), $"Joining {target}...");
        }

        public bool Leave() => bootstrap != null && bootstrap.Leave();

        private bool Try(Func<bool> start, string starting)
        {
            if (Lobby == null)
                return false;
            try
            {
                Lobby.Starting(starting);
                if (start())
                    return true;
                Lobby.Failed("Could not start: is a game already running?");
            }
            catch (Exception ex)
            {
                Lobby.Failed(ex.Message);
            }
            return false;
        }

        private void Redraw()
        {
            if (panel == null || Lobby == null)
                return;
            var active = Lobby.IsActive;
            SetVisible(hostButton, !active);
            SetVisible(joinButton, !active);
            SetVisible(address, !active);
            SetVisible(leaveButton, active);
            status.text = Lobby.Status;
        }

        private void Build()
        {
            EnsureEventSystem();

            panel = new VisualElement { name = "OnlineMenu" };
            panel.style.position = Position.Absolute;
            panel.style.left = 40;
            // Pinned to the left edge at the scoreboard's height (Root's real vertical centre, which tracks
            // the actual aspect -- see DuelView.CreateEdgeLabel), just under the left score.
            panel.style.top = Length.Percent(50);
            panel.style.marginTop = -440;
            panel.style.width = 340;
            panel.style.height = 240;
            panel.style.backgroundColor = Panel;   // also keeps clicks on it from striking (it is opaque to picking)
            view.Root.Add(panel);

            TextLabel(panel, "Heading", "ONLINE", 30, 20, 12, 300, 36, Paper);
            hostButton = MakeButton(panel, "Host", "Host", 20, 56, 300, 48, () => Host());
            address = MakeField(panel, 20, 114, 190, 48);
            joinButton = MakeButton(panel, "Join", "Join", 220, 114, 100, 48, () => Join(address.value));
            leaveButton = MakeButton(panel, "Leave", "Leave", 20, 56, 300, 48, () => Leave());
            status = TextLabel(panel, "Status", "", 22, 20, 172, 300, 60, Paper);
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.unityTextAlign = TextAnchor.UpperLeft;
            SetVisible(leaveButton, false);
        }

        private static void SetVisible(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
                return;
            var events = new GameObject("EventSystem", typeof(EventSystem));
            events.AddComponent<InputSystemUIInputModule>();
        }

        // Every child is placed top-left, relative to its own parent's top-left corner (`panel`'s own, for
        // these) -- the same top-left anchor the original's RectTransform.Place used, just expressed as
        // plain absolute left/top instead of an anchor + pivot + anchoredPosition triple.
        private static VisualElement Place(VisualElement element, VisualElement parent, float left, float top, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = left;
            element.style.top = top;
            element.style.width = width;
            element.style.height = height;
            parent.Add(element);
            return element;
        }

        private static Label TextLabel(VisualElement parent, string name, string value, int size, float left, float top, float width, float height, Color color)
        {
            var label = new Label(value) { name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.color = color;
            Place(label, parent, left, top, width, height);
            return label;
        }

        private static Button MakeButton(VisualElement parent, string name, string label, float left, float top, float width, float height, Action onClick)
        {
            Button button = null;
            button = new Button(() =>
            {
                onClick();
                // Let go of the button, so Space (also the UI's Submit) starts a round instead of clicking it again.
                button.Blur();
            }) { name = name, text = label };
            button.style.backgroundColor = Paper;
            button.style.color = Ink;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 26;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.marginLeft = button.style.marginRight = button.style.marginTop = button.style.marginBottom = 0;
            Place(button, parent, left, top, width, height);
            return button;
        }

        private static TextField MakeField(VisualElement parent, float left, float top, float width, float height)
        {
            var field = new TextField { name = "Address", value = "127.0.0.1" };
            field.style.backgroundColor = Color.white;
            field.style.color = Ink;
            field.style.fontSize = 24;
            field.style.unityTextAlign = TextAnchor.MiddleLeft;
            field.style.marginLeft = field.style.marginRight = field.style.marginTop = field.style.marginBottom = 0;
            Place(field, parent, left, top, width, height);
            return field;
        }
    }
}
