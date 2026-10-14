using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ArtWar
{
    /// <summary>
    /// The online game on top of the two-player game. The host (player 1) runs the battle exactly as offline and
    /// tells the guest (player 2) what happens: each new unit with its drawing, ten snapshots a second of where
    /// everyone is and how they are, every shot fired, every death. The guest's units are puppets; the guest only
    /// sends its drawings, castle upgrades and "start the battle" to the host.
    /// </summary>
    public partial class BattleManager
    {
        static bool online;
        bool waitingForPeer;
        int nextNetId = 10;
        int incomingNetId;
        float snapshotTimer;
        readonly Dictionary<int, DoodleUnit> netUnits = new Dictionary<int, DoodleUnit>();

        /// <summary>Starts the online battle on this device (both devices do this when the host says "start").</summary>
        static int matchNumber;
        float startResendTimer;

        /// <summary>On the host: begins a new match on both devices.</summary>
        static void HostStartMatch()
        {
            matchNumber++;
            int match = matchNumber;
            NetSession.Send(NetSession.Msg.Start, w => w.WriteValueSafe(match));
            EnterOnlineBattle();
        }

        static void EnterOnlineBattle()
        {
            online = true;
            versus = true;
            ReloadBehindClouds(true);
        }

        void SetupOnline()
        {
            if (!online) return;
            if (!NetSession.Active)
            {
                // the connection went away while the scene reloaded
                online = false;
                return;
            }
            if (NetSession.IsHost) waitingForPeer = true; // until the guest's scene is ready too
            else NetSession.Send(NetSession.Msg.Ready, null);
        }

        void TeardownOnline()
        {
            NetSession.Received -= OnNetMessage;
            NetSession.Connected -= OnNetConnected;
            NetSession.Disconnected -= OnNetDisconnected;
        }

        void LeaveOnline()
        {
            online = false;
            NetSession.Leave();
        }

        // ---------------------------------------------------------------- lobby

        void WireOnline()
        {
            NetSession.Received += OnNetMessage;
            NetSession.Connected += OnNetConnected;
            NetSession.Disconnected += OnNetDisconnected;
            if (menu.onlineButton == null) return;
            // room codes are letters and digits, shown in capitals
            menu.codeInput.onValidateInput = (text, index, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
            OnClick(menu.onlineButton, () =>
            {
                SetOnlineStatus("");
                SetLobbyBusy(false);
                OpenSub(Page.Online);
            });
            OnClick(menu.onlineCloseButton, CloseOnline);
            OnClick(menu.quickButton, QuickMatch);
            OnClick(menu.hostButton, CreateRoom);
            OnClick(menu.joinButton, JoinRoom);

            // testing two copies on one computer: "-host" / "-join <code or IP>" / "-quick" skip the clicking
            if (commandLineRead) return;
            commandLineRead = true;
            StartCoroutine(RunCommandLine());
        }

        System.Collections.IEnumerator RunCommandLine()
        {
            yield return null; // after the title page has opened
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host") { OpenSub(Page.Online); CreateRoom(); }
                else if (args[i] == "-quick") { OpenSub(Page.Online); QuickMatch(); }
                else if (args[i] == "-join" && i + 1 < args.Length)
                {
                    OpenSub(Page.Online);
                    menu.codeInput.text = args[i + 1];
                    JoinRoom();
                }
            }
        }

        static bool commandLineRead;

        void CloseOnline()
        {
            if (!online) NetSession.Leave();
            Back();
        }

        void SetOnlineStatus(string text)
        {
            if (menu.onlineStatus != null) menu.onlineStatus.text = text;
        }

        void SetLobbyBusy(bool busy)
        {
            menu.quickButton.interactable = menu.hostButton.interactable = menu.joinButton.interactable = !busy;
        }

        /// <summary>Why going online failed, in words a player can act on.</summary>
        static string OnlineError(System.Exception e)
        {
            string text = e.Message ?? "";
            if (text.Contains("not found") || text.Contains("NotFound") || text.Contains("404"))
                return Loc.T("<color=#C8503C>Không tìm thấy phòng.</color> Kiểm tra lại mã nhé.", "<color=#C8503C>No room with that code.</color> Check it again.");
            if (text.Contains("full") || text.Contains("Full"))
                return Loc.T("<color=#C8503C>Phòng đã đủ người.</color>", "<color=#C8503C>That room is full.</color>");
            Debug.LogWarning("Online: " + e);
            return Loc.T("<color=#C8503C>Không kết nối được.</color> Kiểm tra mạng Internet rồi thử lại.",
                "<color=#C8503C>Couldn't connect.</color> Check the internet connection and try again.");
        }

        async void CreateRoom()
        {
            NetSession.Leave();
            SetLobbyBusy(true);
            SetOnlineStatus(Loc.T("Đang tạo phòng...", "Opening a room..."));
            try
            {
                string code = await NetSession.CreateRoom();
                if (this == null) return;
                SetOnlineStatus(Loc.T($"Mã phòng của bạn:\n<size=170%><b>{code}</b></size>\nGửi mã cho bạn bè. Đang chờ người chơi 2...",
                    $"Your room code:\n<size=170%><b>{code}</b></size>\nSend it to a friend. Waiting for player 2..."));
                GUIUtility.systemCopyBuffer = code; // ready to paste into a chat
            }
            catch (System.Exception e)
            {
                if (this == null) return;
                NetSession.Leave();
                SetLobbyBusy(false);
                SetOnlineStatus(OnlineError(e));
            }
        }

        async void JoinRoom()
        {
            string code = menu.codeInput.text.Trim();
            if (code.Length < 4)
            {
                SetOnlineStatus(Loc.T("<color=#C8503C>Nhập mã phòng trước đã.</color>", "<color=#C8503C>Type a room code first.</color>"));
                return;
            }
            NetSession.Leave();
            SetLobbyBusy(true);
            SetOnlineStatus(Loc.T($"Đang vào phòng {code}...", $"Joining room {code}..."));
            try
            {
                // an IP address joins directly on the local network (testing); anything else is a room code
                if (System.Net.IPAddress.TryParse(code, out _))
                {
                    if (!NetSession.Join(code)) throw new System.Exception("join failed");
                }
                else await NetSession.JoinRoom(code);
            }
            catch (System.Exception e)
            {
                if (this == null) return;
                NetSession.Leave();
                SetLobbyBusy(false);
                SetOnlineStatus(OnlineError(e));
            }
        }

        async void QuickMatch()
        {
            NetSession.Leave();
            SetLobbyBusy(true);
            SetOnlineStatus(Loc.T("Đang tìm đối thủ...", "Looking for an opponent..."));
            try
            {
                bool hosting = await NetSession.QuickMatch();
                if (this == null || online) return;
                if (hosting)
                    SetOnlineStatus(Loc.T("Chưa có ai đang chờ, đã mở phòng mới.\nĐang chờ đối thủ vào...",
                        "Nobody was waiting, so a room is open.\nWaiting for an opponent..."));
            }
            catch (System.Exception e)
            {
                if (this == null) return;
                NetSession.Leave();
                SetLobbyBusy(false);
                SetOnlineStatus(OnlineError(e));
            }
        }

        void OnNetConnected()
        {
            if (this == null || online) return;
            if (NetSession.IsHost)
            {
                // both are here: off to battle (the guest follows when it gets "start")
                HostStartMatch();
            }
            else SetOnlineStatus(Loc.T("Đã vào phòng! Sắp bắt đầu...", "Joined! Starting..."));
        }

        void OnNetDisconnected()
        {
            if (this == null) return;
            if (!online)
            {
                // still in the lobby: joining failed, or the guest left before the game began
                if (NetSession.IsHost) return; // the room stays open for someone else
                NetSession.Leave();
                SetLobbyBusy(false);
                SetOnlineStatus(Loc.T("<color=#C8503C>Mất kết nối.</color> Thử lại nhé.", "<color=#C8503C>Connection lost.</color> Try again."));
                return;
            }
            string left = Loc.T("Người chơi kia đã rời trận!", "The other player left!");
            if (NetSession.IsGuest && hud2 != null) Toast2(left); else Toast(left); // the guest only shows the right column
            LeaveOnline();
            Invoke(nameof(GoHome), 2.5f);
        }

        void OnlineRematch()
        {
            if (NetSession.IsHost)
            {
                HostStartMatch();
            }
            else NetSession.Send(NetSession.Msg.Rematch, null);
        }

        // ---------------------------------------------------------------- every frame

        /// <summary>Online bookkeeping each frame. False means "skip the normal battle logic this frame".</summary>
        bool OnlineUpdate()
        {
            if (NetSession.IsGuest)
            {
                // the guest only shows what the host says
                phaseText.text = Phase == Phase.War ? Loc.T("GIAO CHIẾN!  Phá lâu đài địch!", "BATTLE!  Destroy the enemy castle!")
                    : Phase == Phase.Draw ? Loc.T("CHUẨN BỊ", "GET READY") + "  " + FormatTime(phaseTimer) : phaseText.text;
                if (Phase == Phase.Draw) phaseTimer = Mathf.Max(0f, phaseTimer - Time.deltaTime);
                return false;
            }

            if (waitingForPeer)
            {
                phaseText.text = Loc.T("ĐANG CHỜ NGƯỜI CHƠI 2...", "WAITING FOR PLAYER 2...");
                startResendTimer -= Time.deltaTime;
                if (startResendTimer <= 0f)
                {
                    startResendTimer = 1.5f;
                    int match = matchNumber;
                    NetSession.Send(NetSession.Msg.Start, w => w.WriteValueSafe(match));
                }
                return false;
            }

            snapshotTimer -= Time.deltaTime;
            if (snapshotTimer <= 0f)
            {
                snapshotTimer = 0.1f;
                SendSnapshot();
            }
            return true;
        }

        // ---------------------------------------------------------------- host → guest

        void RegisterNet(DoodleUnit unit, Doodle doodle, bool isBase, float size)
        {
            if (isBase) unit.NetId = unit.Team == Team.Player ? 1 : 2; // both devices build the castles themselves
            else if (NetSession.IsHost) unit.NetId = nextNetId++;
            else unit.NetId = incomingNetId;
            netUnits[unit.NetId] = unit;

            if (NetSession.IsGuest)
            {
                unit.Remote = true;
                unit.SnapRemote(unit.transform.position); // stay put until the host says otherwise (castles never move)
                return;
            }
            if (isBase) return;
            int id = unit.NetId;
            var team = unit.Team;
            var pos = unit.transform.position;
            float maxHp = unit.Stats.maxHp;
            NetSession.Send(NetSession.Msg.Spawn, w =>
            {
                w.WriteValueSafe(id);
                w.WriteValueSafe((byte)team);
                w.WriteValueSafe(pos);
                w.WriteValueSafe(size);
                w.WriteValueSafe(maxHp);
                NetSession.WriteDoodle(w, doodle);
            });
        }


        void SendSnapshot()
        {
            var alive = new List<DoodleUnit>();
            foreach (var u in units)
                if (u != null && u.IsAlive && !u.IsBase) alive.Add(u);
            float elapsed = Phase == Phase.War ? Time.time - warStartTime : 0f;
            NetSession.Send(NetSession.Msg.Snapshot, w =>
            {
                w.WriteValueSafe((byte)Phase);
                w.WriteValueSafe(phaseTimer);
                w.WriteValueSafe(elapsed);
                w.WriteValueSafe(ink);
                w.WriteValueSafe(enemyInk);
                foreach (var castle in new[] { playerBase, enemyBase })
                {
                    w.WriteValueSafe(castle != null ? Mathf.Max(0f, castle.Hp) : 0f);
                    w.WriteValueSafe((byte)(castle != null ? castle.CastleLevel : 1));
                    w.WriteValueSafe((ushort)(castle != null ? castle.AttackCount : 0));
                }
                w.WriteValueSafe((ushort)alive.Count);
                foreach (var u in alive)
                {
                    w.WriteValueSafe(u.NetId);
                    w.WriteValueSafe(u.transform.position);
                    w.WriteValueSafe(u.Hp);
                    w.WriteValueSafe((sbyte)Mathf.RoundToInt(u.Facing));
                    w.WriteValueSafe((ushort)u.AttackCount);
                }
            });
        }

        // ---------------------------------------------------------------- incoming

        void OnNetMessage(string name, FastBufferReader r)
        {
            if (this == null) return;
            switch (name)
            {
                case NetSession.Msg.Start:
                    // the host repeats "start" until the guest answers; each match number reloads only once
                    r.ReadValueSafe(out int match);
                    if (NetSession.IsGuest && match != matchNumber) { matchNumber = match; EnterOnlineBattle(); }
                    break;
                case NetSession.Msg.Ready: waitingForPeer = false; break;
                case NetSession.Msg.Rematch: if (NetSession.IsHost) OnlineRematch(); break;
                case NetSession.Msg.War: break; // two-player games start on the clock only
                case NetSession.Msg.Upgrade: if (NetSession.IsHost) OnUpgrade2Clicked(); break;
                case NetSession.Msg.Summon: if (NetSession.IsHost) HostSummonForGuest(NetSession.ReadDoodle(r)); break;
                case NetSession.Msg.Spawn: GuestSpawn(r); break;
                case NetSession.Msg.Snapshot: GuestSnapshot(r); break;
                case NetSession.Msg.Die:
                    r.ReadValueSafe(out int dead);
                    if (netUnits.TryGetValue(dead, out var unit) && unit != null) unit.RemoteDie();
                    netUnits.Remove(dead);
                    break;
                case NetSession.Msg.Shot: GuestShot(r); break;
            }
        }

        /// <summary>On the host: the guest drew a unit. It pays from player 2's ink like any other drawing.</summary>
        void HostSummonForGuest(Doodle doodle)
        {
            if (Phase == Phase.Over || CountUnits(Team.Enemy) >= maxUnitsPerSide) return;
            float cost = doodle.TotalLength * DoodleAnalyzer.InkPerUnit;
            if (cost > enemyInk + 1f) return;
            enemyInk = Mathf.Max(0f, enemyInk - cost);
            var stats = DoodleAnalyzer.Analyze(doodle);
            SpawnArmyUnit(doodle, Team.Enemy, DoodleQuality.Score(doodle, stats));
        }

        void GuestSpawn(FastBufferReader r)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out byte team);
            r.ReadValueSafe(out Vector3 pos);
            r.ReadValueSafe(out float scale);
            r.ReadValueSafe(out float maxHp);
            var doodle = NetSession.ReadDoodle(r);
            var stats = DoodleAnalyzer.Analyze(doodle);
            stats.maxHp = maxHp;
            incomingNetId = id;
            var unit = SpawnUnit(doodle, (Team)team, pos, stats, false, scale);
            unit.SnapRemote(pos);
        }

        void GuestSnapshot(FastBufferReader r)
        {
            r.ReadValueSafe(out byte phase);
            r.ReadValueSafe(out float timer);
            r.ReadValueSafe(out float elapsed);
            r.ReadValueSafe(out float ink1);
            r.ReadValueSafe(out float ink2);
            ink = ink1;
            phaseTimer = timer;
            enemyInk = ink2;
            if ((Phase)phase == Phase.War && Phase == Phase.Draw) GuestEnterWar(elapsed);

            foreach (var castle in new[] { playerBase, enemyBase })
            {
                r.ReadValueSafe(out float hp);
                r.ReadValueSafe(out byte level);
                r.ReadValueSafe(out ushort attacks);
                if (castle != null && castle.IsAlive) castle.ApplyRemoteState(castle.transform.position, hp, 0f, attacks, level);
            }

            r.ReadValueSafe(out ushort count);
            for (int i = 0; i < count; i++)
            {
                r.ReadValueSafe(out int id);
                r.ReadValueSafe(out Vector3 pos);
                r.ReadValueSafe(out float hp);
                r.ReadValueSafe(out sbyte facing);
                r.ReadValueSafe(out ushort attacks);
                if (netUnits.TryGetValue(id, out var unit) && unit != null) unit.ApplyRemoteState(pos, hp, facing, attacks, 1);
            }
        }

        void GuestEnterWar(float elapsed)
        {
            Phase = Phase.War;
            warStartTime = Time.time - elapsed;
            Sfx.Play(Sound.Fanfare, 0.7f);
            StartCoroutine(BattleCry());
            Sfx.SetBattleMood(true);
            startButton.SetActive(false);
        }

        void GuestShot(FastBufferReader r)
        {
            var look = NetSession.ReadDoodle(r);
            r.ReadValueSafe(out float scale);
            r.ReadValueSafe(out Vector3 from);
            r.ReadValueSafe(out Vector3 to);
            r.ReadValueSafe(out float speed);
            r.ReadValueSafe(out float arc);
            r.ReadValueSafe(out bool align);
            r.ReadValueSafe(out bool hasColor);
            r.ReadValueSafe(out Color color);
            Projectile.Launch(look, scale, from, to, speed, arc, align, null, hasColor ? color : (Color?)null);
        }
    }
}
