using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArtWar
{
    /// <summary>
    /// Two players on one device: player 1 (blue) draws in the left column, player 2 (red) in a mirrored column on
    /// the right, and the battlefield sits between them. The red side's AI is off; player 2 has their own page,
    /// ink, pen buttons and castle upgrade. On a touch screen both can draw at the same time.
    /// </summary>
    public partial class BattleManager
    {
        static bool versus;

        /// <summary>True in a two-player game.</summary>
        public bool Versus => versus;

        /// <summary>The drawing columns shrink in a two-player game so the battlefield still fits between them.</summary>
        float ColumnScale => versus && !online ? 0.72f : 1f; // online each screen has only its own column

        /// <summary>The drawing time before a two-player battle starts by itself.</summary>
        const float VersusDrawSeconds = 120f;

        HudView hud2;
        DrawingPad pad2;
        float toast2Until;

        /// <summary>From the title: start a two-player game (the scene reloads behind the clouds into the battle).</summary>
        void StartVersus()
        {
            versus = true;
            ReloadBehindClouds(true);
        }

        void BuildSecondPlayer()
        {
            float k = ColumnScale;

            // player 1's column shrinks toward the bottom-left corner
            var column1 = ColumnPieces(hud);
            foreach (var rt in column1)
            {
                rt.anchoredPosition *= k;
                rt.localScale = Vector3.one * k;
            }
            PlaceUpgrade((RectTransform)hud.upgradeButton.transform, false);
            Tint(hud, new Color(0.15f, 0.25f, 0.5f, 0.75f));

            // no "start" button: both players get the same two minutes, then the battle starts by itself
            phaseTimer = VersusDrawSeconds;
            hud.startButton.gameObject.SetActive(false);

            // the banner moves to the middle of the screen
            CentreTop(hud.phaseText);

            // player 2: a second copy of the HUD, keeping only the drawing column and the upgrade button, mirrored right
            var prefab = Resources.Load<HudView>("UI/HUD");
            hud2 = prefab != null ? Instantiate(prefab) : HudView.Build();
            hud2.name = "HUD Player 2";
            Destroy(TopPiece(hud2, hud2.phaseText.transform).gameObject);
            Destroy(hud2.startButton.gameObject);
            Destroy(hud2.pauseButton.gameObject);
            var column2 = ColumnPieces(hud2);
            foreach (var rt in column2)
            {
                float x = rt.anchoredPosition.x, w = rt.sizeDelta.x;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-(x + w) * k, rt.anchoredPosition.y * k);
                rt.localScale = Vector3.one * k;
            }
            PlaceUpgrade((RectTransform)hud2.upgradeButton.transform, true);
            Tint(hud2, new Color(0.5f, 0.15f, 0.15f, 0.75f));

            // online nobody sees the other player's page: the host keeps the left column, the guest the right
            if (online)
            {
                bool host = NetSession.IsHost;
                foreach (var rt in host ? column2 : column1) rt.gameObject.SetActive(false);
                (host ? hud2.upgradeButton : hud.upgradeButton).gameObject.SetActive(false);
            }

            pad2 = hud2.pad.gameObject.AddComponent<DrawingPad>();
            bool guest = online && NetSession.IsGuest;
            pad2.TryConsumeInk = amount =>
            {
                if (Phase == Phase.Over) return false;
                // online the host keeps player 2's ink: the guest only checks it can afford the stroke
                if (guest) return enemyInk - pad2.InkUsed >= amount;
                if (enemyInk < amount) return false;
                enemyInk -= amount;
                return true;
            };
            pad2.RefundInk = amount => { if (!guest) enemyInk = Mathf.Min(armyInk, enemyInk + amount); };
            pad2.Changed += () => RefreshStatsFor(pad2, hud2.kindText, hud2.hintText, hud2.statsText);
            pad2.Assist = pad.Assist;
            pad2.Locked = pad.Locked;

            for (int i = 0; i < hud2.colorButtons.Length; i++)
            {
                var color = (InkColor)i;
                OnClick(hud2.colorButtons[i], () => pad2.InkColor = color);
            }
            OnClick(hud2.undoButton, pad2.Undo);
            OnClick(hud2.clearButton, () => pad2.Clear(true));
            OnClick(hud2.assistButton, () =>
            {
                pad2.Assist = !pad2.Assist;
                hud2.assistLabel.text = pad2.Assist ? Loc.T("Hỗ trợ\nBẬT", "Assist\nON") : Loc.T("Hỗ trợ\nTẮT", "Assist\nOFF");
            });
            hud2.assistLabel.text = pad2.Assist ? Loc.T("Hỗ trợ\nBẬT", "Assist\nON") : Loc.T("Hỗ trợ\nTẮT", "Assist\nOFF");
            OnClick(hud2.summonButton, OnSummon2Clicked);
            OnClick(hud2.upgradeButton, OnUpgrade2Clicked);

            RefreshStats();
            RefreshStatsFor(pad2, hud2.kindText, hud2.hintText, hud2.statsText);
        }

        void UpdateSecondPlayer()
        {
            if (hud2 == null) return;
            if (Time.time > toast2Until) hud2.toastText.text = "";
            float ink2 = online && NetSession.IsGuest ? Mathf.Max(0f, enemyInk - pad2.InkUsed) : enemyInk;
            hud2.inkFill.anchorMax = new Vector2(ink2 / armyInk, 1f);
            hud2.inkText.text = $"{Loc.T("Mực", "Ink")}  {Mathf.FloorToInt(ink2)} / {armyInk:0}";
            RefreshUpgradeFor(enemyBase, enemyInk, hud2.upgradeLabel, hud2.upgradeButton.image);
            // menus and the end of the game lock both pages; online each device only draws on its own player's page
            bool closed = page != Page.None || Phase == Phase.Over;
            if (online && NetSession.IsHost) pad2.Locked = true;
            else if (online) { pad2.Locked = closed; pad.Locked = true; }
            else pad2.Locked = pad.Locked;
        }

        void OnSummon2Clicked()
        {
            if (Phase == Phase.Over) return;
            if (pad2.CurrentDoodle.TotalLength < 0.08f)
            {
                Toast2(Loc.T("Vẽ thêm chút nữa đã!", "Draw a bit more first!"));
                return;
            }
            if (CountUnits(Team.Enemy) >= maxUnitsPerSide)
            {
                Toast2(Loc.T("Quân đã đầy!", "Your army is full!"));
                return;
            }
            var doodle = pad2.TakeDoodle();
            var stats = DoodleAnalyzer.Analyze(doodle);
            float quality = DoodleQuality.Score(doodle, stats);
            int made;
            if (online && NetSession.IsGuest)
            {
                // the host brings it to life; it arrives here a moment later
                NetSession.Send(NetSession.Msg.Summon, w => NetSession.WriteDoodle(w, doodle));
                enemyInk = Mathf.Max(0f, enemyInk - doodle.TotalLength * DoodleAnalyzer.InkPerUnit); // until the next snapshot
                made = DoodleQuality.Copies(doodle, stats, quality);
            }
            else made = SpawnArmyUnit(doodle, Team.Enemy, quality);
            if (DoodleQuality.Multiplies(stats.kind))
            {
                Sfx.Play(Sound.Twinkle, 0.6f);
                hud2.toastText.text = $"{DoodleQuality.StarText(DoodleQuality.Stars(quality))}  ×{made} {DoodleAnalyzer.DisplayName(stats)}!";
                toast2Until = Time.time + 1.8f;
            }
        }

        void OnUpgrade2Clicked()
        {
            if (Phase == Phase.Over || enemyBase == null) return;
            if (online && NetSession.IsGuest)
            {
                NetSession.Send(NetSession.Msg.Upgrade, null);
                return;
            }
            int next = enemyBase.CastleLevel + 1;
            if (next > DoodleUnit.CastleMaxLevel)
            {
                Toast2(Loc.T("Thành đã cấp tối đa!", "The castle is at max level!"));
                return;
            }
            float cost = DoodleUnit.UpgradeCost(next);
            if (enemyInk < cost)
            {
                Toast2(Loc.T($"Cần {cost:0} mực để nâng cấp!", $"Need {cost:0} ink to upgrade!"));
                return;
            }
            enemyInk -= cost;
            enemyBase.UpgradeCastle();
            hud2.toastText.text = Loc.T($"Thành lên cấp {next}!", $"Castle up to level {next}!");
            toast2Until = Time.time + 1.5f;
        }

        void Toast2(string message)
        {
            Sfx.Play(Sound.Error, 0.5f);
            hud2.toastText.text = message;
            toast2Until = Time.time + 1.5f;
        }

        // ---------------------------------------------------------------- layout helpers

        /// <summary>The pieces of a HUD that make up its drawing column (everything anchored at the bottom-left
        /// except the castle upgrade button, the banner, the start and pause buttons).</summary>
        static System.Collections.Generic.List<RectTransform> ColumnPieces(HudView view)
        {
            var skip = new System.Collections.Generic.HashSet<Transform>();
            if (view.phaseText != null) skip.Add(TopPiece(view, view.phaseText.transform));
            if (view.startButton != null) skip.Add(view.startButton.transform);
            if (view.pauseButton != null) skip.Add(view.pauseButton.transform);
            if (view.upgradeButton != null) skip.Add(view.upgradeButton.transform);
            var pieces = new System.Collections.Generic.List<RectTransform>();
            foreach (Transform child in view.transform)
            {
                if (skip.Contains(child) || !(child is RectTransform rt)) continue;
                if (rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.zero) continue;
                pieces.Add(rt);
            }
            return pieces;
        }

        /// <summary>The direct child of the HUD's root that holds <paramref name="t"/>.</summary>
        static Transform TopPiece(HudView view, Transform t)
        {
            while (t.parent != null && t.parent != view.transform) t = t.parent;
            return t;
        }

        /// <summary>The castle upgrade button sits just inside the battlefield, beside its player's column.</summary>
        void PlaceUpgrade(RectTransform rt, bool right)
        {
            float k = ColumnScale;
            float x = UiColumnWidth * k + 10f;
            rt.localScale = Vector3.one * k;
            if (!right)
            {
                rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y * k);
                return;
            }
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-(x + rt.sizeDelta.x * k), rt.anchoredPosition.y * k);
        }

        /// <summary>Colours a player's recognition box in their team colour so it's clear whose column is whose.</summary>
        static void Tint(HudView view, Color color)
        {
            var box = view.kindText != null ? view.kindText.transform.parent.GetComponent<Image>() : null;
            if (box != null) box.color = color;
        }

        /// <summary>
        /// Puts the banner over the battlefield: centred between two columns; online, where each screen has one
        /// column, it keeps its one-player place beside the host's column and is mirrored for the guest.
        /// </summary>
        void CentreTop(TMP_Text inside)
        {
            if (inside == null) return;
            var piece = (RectTransform)TopPiece(hud, inside.transform);
            float x = piece.anchoredPosition.x;
            if (!online) x = 0f;
            else if (NetSession.IsGuest)
            {
                x = -x;
                // the pause button moves to the free top-left corner
                var pause = (RectTransform)hud.pauseButton.transform;
                pause.anchorMin = pause.anchorMax = pause.pivot = new Vector2(0f, 1f);
                pause.anchoredPosition = new Vector2(14f, pause.anchoredPosition.y);
            }
            piece.anchoredPosition = new Vector2(x, piece.anchoredPosition.y);
        }
    }
}
