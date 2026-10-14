using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ArtWar
{
    /// <summary>
    /// Online two-player games over Netcode for GameObjects. Players meet through Unity's Multiplayer Services: a
    /// room code to share, or quick match with whoever is waiting; Relay carries the traffic over the internet, so
    /// nobody needs an IP address. (Join-by-IP on a LAN stays for testing from the command line.)
    /// The host is player 1 (blue) and runs the real battle; the guest is player 2 (red) and only sends what they do
    /// (a drawing to summon, a castle upgrade, "start the battle") and shows what the host tells it.
    /// Everything travels as named messages; no networked prefabs are needed.
    /// </summary>
    public static class NetSession
    {
        public const ushort Port = 7777;

        public static bool Active => manager != null && (manager.IsHost || manager.IsConnectedClient);
        public static bool IsHost => manager != null && manager.IsHost;
        public static bool IsGuest => manager != null && manager.IsClient && !manager.IsHost;
        public static bool PeerConnected => manager != null && manager.IsHost && manager.ConnectedClientsIds.Count > 1;

        /// <summary>Raised on the host when a guest joins, and on the guest when it reaches the host.</summary>
        public static event Action Connected;
        /// <summary>Raised when the other player leaves or the connection drops.</summary>
        public static event Action Disconnected;
        /// <summary>A named message arrived: (name, reader). Readers are only valid during the call.</summary>
        public static event Action<string, FastBufferReader> Received;

        static NetworkManager manager;
        static readonly string[] Names =
        {
            Msg.Start, Msg.Ready, Msg.Spawn, Msg.Snapshot, Msg.Die, Msg.Shot, Msg.Over,
            Msg.Summon, Msg.Upgrade, Msg.War, Msg.Rematch,
        };

        public static class Msg
        {
            public const string Start = "aw.start", Ready = "aw.ready", Spawn = "aw.spawn", Snapshot = "aw.snap",
                Die = "aw.die", Shot = "aw.shot", Over = "aw.over", Summon = "aw.summon", Upgrade = "aw.upgrade",
                War = "aw.war", Rematch = "aw.rematch";
        }

        static NetworkManager Manager()
        {
            if (manager != null) return manager;
            var go = new GameObject("Network");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
            manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = false; // each side loads its own scene; the battle is sent as messages
            manager.NetworkConfig.ConnectionApproval = false;
            // the other player must not freeze when their window loses focus
            Application.runInBackground = true;
            // listen as soon as Netcode starts: the host's first message can arrive before joining returns
            manager.OnClientStarted += Register;
            manager.OnServerStarted += Register;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            return manager;
        }

        /// <summary>Opens a room on this device. Returns false if the port is busy.</summary>
        public static bool Host()
        {
            var m = Manager();
            ((UnityTransport)m.NetworkConfig.NetworkTransport).SetConnectionData("0.0.0.0", Port, "0.0.0.0");
            if (!m.StartHost()) return false;
            Register();
            return true;
        }

        /// <summary>Joins the room at <paramref name="address"/>.</summary>
        public static bool Join(string address)
        {
            var m = Manager();
            ((UnityTransport)m.NetworkConfig.NetworkTransport).SetConnectionData(address.Trim(), Port);
            if (!m.StartClient()) return false;
            Register();
            return true;
        }

        public static void Leave()
        {
            if (session != null)
            {
                var leaving = session;
                session = null;
                _ = leaving.LeaveAsync(); // frees the room code; nobody waits for it
            }
            if (manager == null) return;
            if (manager.IsListening) manager.Shutdown();
        }

        // ---------------------------------------------------------------- rooms over the internet

        static ISession session;

        /// <summary>The room's code to share (empty when not in a room).</summary>
        public static string RoomCode => session?.Code ?? "";

        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                // two copies of the game on one computer (testing) need different players
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-profile");
                if (i >= 0 && i + 1 < args.Length) options.SetProfile(args[i + 1]);
                else if (Application.isEditor) options.SetProfile("editor");
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static SessionOptions RoomOptions(bool hidden) => new SessionOptions { MaxPlayers = 2, IsPrivate = hidden }.WithRelayNetwork();

        /// <summary>Opens a private room. Returns its code for the other player to type in.</summary>
        public static async Task<string> CreateRoom()
        {
            Manager();
            await SignIn();
            session = await MultiplayerService.Instance.CreateSessionAsync(RoomOptions(true));
            Register();
            return session.Code;
        }

        /// <summary>Joins the room with this code.</summary>
        public static async Task JoinRoom(string code)
        {
            Manager();
            await SignIn();
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
            Register();
        }

        /// <summary>
        /// Joins any open public room; if there is none, opens one and waits there. True when this device ended up
        /// hosting (still waiting for an opponent).
        /// </summary>
        public static async Task<bool> QuickMatch()
        {
            Manager();
            await SignIn();
            session = await MultiplayerService.Instance.MatchmakeSessionAsync(
                new QuickJoinOptions { Timeout = TimeSpan.FromSeconds(4), CreateSession = true }, RoomOptions(false));
            Register();
            return IsHost;
        }

        static void Register()
        {
            var messaging = manager.CustomMessagingManager;
            foreach (var name in Names)
            {
                var n = name;
                messaging.UnregisterNamedMessageHandler(n);
                messaging.RegisterNamedMessageHandler(n, (sender, reader) => Received?.Invoke(n, reader));
            }
        }

        static void OnClientConnected(ulong id)
        {
            // the host hears about itself too; only the other player matters
            if (manager.IsHost && id == NetworkManager.ServerClientId) return;
            Connected?.Invoke();
        }

        static void OnClientDisconnected(ulong id)
        {
            if (manager.IsHost && id == NetworkManager.ServerClientId) return;
            Disconnected?.Invoke();
        }

        /// <summary>Sends to the other player. <paramref name="write"/> fills the message.</summary>
        public static void Send(string name, Action<FastBufferWriter> write, bool reliable = true)
        {
            if (!Active) return;
            using var writer = new FastBufferWriter(1024, Allocator.Temp, 1 << 20);
            write?.Invoke(writer);
            var delivery = writer.Length > 1000 ? NetworkDelivery.ReliableFragmentedSequenced
                : reliable ? NetworkDelivery.ReliableSequenced : NetworkDelivery.UnreliableSequenced;
            var messaging = manager.CustomMessagingManager;
            if (manager.IsHost)
            {
                foreach (var id in manager.ConnectedClientsIds)
                    if (id != NetworkManager.ServerClientId) messaging.SendNamedMessage(name, id, writer, delivery);
            }
            else messaging.SendNamedMessage(name, NetworkManager.ServerClientId, writer, delivery);
        }

        // ---------------------------------------------------------------- drawings on the wire

        public static void WriteDoodle(FastBufferWriter w, Doodle d)
        {
            w.WriteValueSafe((byte)d.color);
            w.WriteValueSafe((ushort)d.strokes.Count);
            foreach (var s in d.strokes)
            {
                w.WriteValueSafe((ushort)s.points.Count);
                foreach (var p in s.points)
                {
                    // 1/16000 of the page is far finer than any pen
                    w.WriteValueSafe((short)Mathf.Clamp(Mathf.RoundToInt(p.x * 16000f), short.MinValue, short.MaxValue));
                    w.WriteValueSafe((short)Mathf.Clamp(Mathf.RoundToInt(p.y * 16000f), short.MinValue, short.MaxValue));
                }
            }
        }

        public static Doodle ReadDoodle(FastBufferReader r)
        {
            r.ReadValueSafe(out byte color);
            r.ReadValueSafe(out ushort strokes);
            var d = new Doodle { color = (InkColor)color };
            for (int i = 0; i < strokes; i++)
            {
                r.ReadValueSafe(out ushort count);
                var s = new Stroke();
                for (int k = 0; k < count; k++)
                {
                    r.ReadValueSafe(out short x);
                    r.ReadValueSafe(out short y);
                    s.points.Add(new Vector2(x / 16000f, y / 16000f));
                }
                d.strokes.Add(s);
            }
            return d;
        }

        /// <summary>This device's address on the local network, to tell the other player.</summary>
        public static string LocalAddress()
        {
            try
            {
                foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                        return ip.ToString();
            }
            catch (Exception) { }
            return "127.0.0.1";
        }
    }
}
