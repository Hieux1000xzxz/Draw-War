using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArtWar
{
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>
    /// The game's menus around the battle: title screen (with the unit handbook, how-to-play and settings), the pause
    /// menu and the results screen, plus the battle HUD's castle bars and army counts. The pages themselves are the
    /// Menus prefab (<see cref="MenuView"/>); this wires them up. Every menu page freezes the battle while it is open,
    /// except the results.
    /// </summary>
    public partial class BattleManager
    {
        enum Page { None, Title, Pause, Settings, Help, Codex, Results }

        /// <summary>"Play Again" reloads straight into a new battle; "Main Menu" shows the title again.</summary>
        static bool skipTitle;
        static Difficulty difficulty = Difficulty.Normal;

        Page page = Page.None, returnPage = Page.None;
        readonly Dictionary<Page, GameObject> pages = new Dictionary<Page, GameObject>();
        MenuView menu;

        // battle record for the results screen
        int statDrawn, statKilled, statLost;
        float statInkEarned, warStartTime;

        // ---------------------------------------------------------------- difficulty and settings

        float EnemyBudgetFactor => difficulty == Difficulty.Easy ? 0.7f : difficulty == Difficulty.Hard ? 1.35f : 1f;
        float EnemyRegenFactor => difficulty == Difficulty.Easy ? 0.6f : difficulty == Difficulty.Hard ? 1.4f : 1f;

        /// <summary>How well the enemy "draws": sloppier on Easy (fewer copies), neater on Hard.</summary>
        float EnemyDrawingQuality() => difficulty == Difficulty.Easy ? Random.Range(0.15f, 0.6f)
            : difficulty == Difficulty.Hard ? Random.Range(0.5f, 1f) : Random.Range(0.25f, 0.85f);

        static string DifficultyName(Difficulty d) => d == Difficulty.Easy ? Loc.T("Dễ", "Easy") : d == Difficulty.Hard ? Loc.T("Khó", "Hard") : Loc.T("Thường", "Normal");

        static readonly float[] DrawTimes = { 60f, 90f, 120f, 180f };

        void LoadSettings()
        {
            Sfx.MasterVolume = PlayerPrefs.GetFloat("artwar.sfx", 0.8f);
            Sfx.MusicVolume = PlayerPrefs.GetFloat("artwar.music", 1f);
            drawPhaseSeconds = PlayerPrefs.GetFloat("artwar.drawTime", drawPhaseSeconds);
            difficulty = (Difficulty)PlayerPrefs.GetInt("artwar.difficulty", (int)Difficulty.Normal);
        }

        void SaveSettings()
        {
            PlayerPrefs.SetFloat("artwar.sfx", Sfx.MasterVolume);
            PlayerPrefs.SetFloat("artwar.music", Sfx.MusicVolume);
            PlayerPrefs.SetFloat("artwar.drawTime", drawPhaseSeconds);
            PlayerPrefs.SetInt("artwar.difficulty", (int)difficulty);
            PlayerPrefs.SetInt("artwar.assist", pad.Assist ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- building

        void BuildMenus()
        {
            pad.Assist = PlayerPrefs.GetInt("artwar.assist", 1) == 1;
            assistLabel.text = pad.Assist ? Loc.T("Hỗ trợ\nBẬT", "Assist\nON") : Loc.T("Hỗ trợ\nTẮT", "Assist\nOFF");

            // The pages come from Resources/UI/Menus.prefab (edit them there); without the prefab they are laid out in code.
            var prefab = Resources.Load<MenuView>("UI/Menus");
            menu = prefab != null ? Instantiate(prefab) : MenuView.Build();
            menu.name = "Menus";

            pages[Page.Title] = menu.titlePage;
            pages[Page.Pause] = menu.pausePage;
            pages[Page.Settings] = menu.settingsPage;
            pages[Page.Help] = menu.helpPage;
            pages[Page.Codex] = menu.codexPage;
            pages[Page.Results] = menu.resultsPage;

            WireTitle();
            WirePause();
            WireSettings();
            WireHelp();
            FillCodex();
            OnClick(menu.resultAgainButton, Restart);
            OnClick(menu.resultHomeButton, GoHome);

            ShowPage(skipTitle ? Page.None : Page.Title);
            if (reopenSettings && page == Page.Title) OpenSub(Page.Settings);
            reopenSettings = false;
            skipTitle = false;
        }

        // ---------------------------------------------------------------- title

        void WireTitle()
        {
            // a little parade of drawings across the page
            string[] parade = { "Lính khiên", "Robot kiếm", "Rồng", "Xe tăng", "Hồn ma", "Cung thủ" };
            for (int i = 0; i < menu.parade.Length && i < parade.Length; i++)
            {
                var make = SampleByLabel(parade[i]);
                if (make == null || menu.parade[i] == null) continue;
                menu.parade[i].texture = Thumbnail(parade[i], make, 192,
                    i % 2 == 0 ? new Color(0.45f, 0.8f, 0.95f) : new Color(0.98f, 0.6f, 0.75f));
            }

            for (int i = 0; i < menu.difficultyButtons.Length; i++)
            {
                var d = (Difficulty)i;
                OnClick(menu.difficultyButtons[i], () => SetDifficulty(d));
            }
            RefreshDifficultyButtons();

            OnClick(menu.playButton, StartFromTitle);
            OnClick(menu.codexButton, () => OpenSub(Page.Codex));
            OnClick(menu.helpButton, () => OpenSub(Page.Help));
            OnClick(menu.settingsButton, () => OpenSub(Page.Settings));
        }

        void SetDifficulty(Difficulty d)
        {
            difficulty = d;
            RefreshDifficultyButtons();
            SaveSettings();
            // the battle hasn't started yet: the enemy plans its army again for the new difficulty
            if (Phase == Phase.Draw && CountUnits(Team.Enemy) == 0)
            {
                enemyPlan.Clear();
                PlanEnemyArmy();
            }
        }

        void RefreshDifficultyButtons()
        {
            for (int i = 0; i < menu.difficultyButtons.Length; i++)
                if (menu.difficultyButtons[i] != null)
                    menu.difficultyButtons[i].image.color = (Difficulty)i == difficulty ? UiKit.Primary : UiKit.Muted;
        }

        void StartFromTitle()
        {
            // the title rolls away under the clouds and the battlefield appears behind them
            CloudWipe.Play(() =>
            {
                if (this == null) return;
                phaseTimer = drawPhaseSeconds;
                ShowPage(Page.None);
                Sfx.Play(Sound.WarCry, 0.7f);
            });
        }

        /// <summary>Unit pictures for the menus, painted once and kept across scene reloads.</summary>
        static readonly Dictionary<string, Texture2D> thumbnails = new Dictionary<string, Texture2D>();

        static Texture2D Thumbnail(string label, System.Func<InkColor, Doodle> make, int size, Color fill)
        {
            string key = $"{label}|{size}|{fill}";
            if (!thumbnails.TryGetValue(key, out var tex) || tex == null)
                thumbnails[key] = tex = DoodleThumbnail.Render(make(InkColor.Black), size, fill);
            return tex;
        }

        static System.Func<InkColor, Doodle> SampleByLabel(string label)
        {
            foreach (var (l, make) in DoodleTemplates.Samples)
                if (l == label) return make;
            return null;
        }

        // ---------------------------------------------------------------- pause

        void WirePause()
        {
            OnClick(menu.resumeButton, () => ShowPage(Page.None));
            OnClick(menu.pauseHelpButton, () => OpenSub(Page.Help));
            OnClick(menu.pauseSettingsButton, () => OpenSub(Page.Settings));
            OnClick(menu.restartButton, Restart);
            OnClick(menu.homeButton, GoHome);
        }

        void Restart() => ReloadBehindClouds(true);

        void GoHome() => ReloadBehindClouds(false);

        /// <summary>Clouds roll over the screen, the scene reloads behind them (into a new battle or the title).</summary>
        static void ReloadBehindClouds(bool straightIntoBattle)
        {
            CloudWipe.Play(() =>
            {
                skipTitle = straightIntoBattle;
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });
        }

        // ---------------------------------------------------------------- settings

        void WireSettings()
        {
            menu.soundSlider.value = Sfx.MasterVolume;
            menu.soundSlider.onValueChanged.AddListener(v =>
            {
                Sfx.MasterVolume = v;
                Sfx.MusicVolume = Sfx.MusicVolume; // music sits on top of the master level
                SaveSettings();
            });
            menu.musicSlider.value = Sfx.MusicVolume;
            menu.musicSlider.onValueChanged.AddListener(v =>
            {
                Sfx.MusicVolume = v;
                SaveSettings();
            });

            ShowAssistSetting();
            OnClick(menu.assistButton, () =>
            {
                OnAssistClicked();
                ShowAssistSetting();
                SaveSettings();
            });
            OnClick(menu.drawTimeButton, CycleDrawTime);
            RefreshDrawTimeLabel();

            // language dropdown: the box shows the current language, clicking it opens the list below
            OnClick(menu.languageButton, () => menu.languageList.SetActive(!menu.languageList.activeSelf));
            var options = new[] { Language.English, Language.Vietnamese };
            for (int i = 0; i < menu.languageOptions.Length && i < options.Length; i++)
            {
                var lang = options[i];
                var option = menu.languageOptions[i];
                bool chosen = lang == Loc.Current;
                option.image.color = chosen ? UiKit.Primary : UiKit.Paper;
                UiKit.ButtonLabel(option).color = chosen ? Color.white : UiKit.InkText;
                OnClick(option, () =>
                {
                    menu.languageList.SetActive(false);
                    SetLanguage(lang);
                });
            }
            menu.languageList.SetActive(false);
            RefreshLanguageLabel();

            OnClick(menu.settingsDoneButton, Back);
        }

        void ShowAssistSetting() => menu.assistText.text = pad.Assist
            ? Loc.T("BẬT: nét vẽ được làm mượt", "ON: strokes are smoothed")
            : Loc.T("TẮT: giữ nguyên nét tay", "OFF: raw hand strokes");

        /// <summary>After switching language on the title screen the scene reloads; this brings Settings back up.</summary>
        static bool reopenSettings;

        /// <summary>
        /// Switches the language. On the title screen everything is rebuilt in it straight away; during a battle it
        /// is saved and used from the next game, so the fight isn't interrupted.
        /// </summary>
        void SetLanguage(Language language)
        {
            if (Loc.Current == language) return;
            Loc.Current = language;
            bool atTitle = page == Page.Title || returnPage == Page.Title;
            if (atTitle)
            {
                reopenSettings = true;
                GoHome();
            }
            else RefreshLanguageLabel();
        }

        void RefreshLanguageLabel()
        {
            bool atTitle = page == Page.Title || returnPage == Page.Title;
            // the label is in the language the screen was built in; a mid-battle switch shows it will apply later
            string chosen = Loc.Current == Language.English ? "English" : "Tiếng Việt";
            menu.languageText.text = (atTitle ? chosen : chosen + Loc.T(" (ván sau)", " (next game)")) + "  ▾";
        }

        void CycleDrawTime()
        {
            int i = System.Array.IndexOf(DrawTimes, drawPhaseSeconds);
            drawPhaseSeconds = DrawTimes[(i + 1) % DrawTimes.Length];
            if (returnPage == Page.Title) phaseTimer = drawPhaseSeconds; // not started yet: applies right away
            RefreshDrawTimeLabel();
            SaveSettings();
        }

        void RefreshDrawTimeLabel()
        {
            string when = returnPage == Page.Title || page == Page.Title ? "" : Loc.T(" (ván sau)", " (next game)");
            menu.drawTimeText.text = FormatTime(drawPhaseSeconds) + Loc.T(" để vẽ quân", " to draw") + when;
        }

        // ---------------------------------------------------------------- how to play

        int helpIndex;

        void WireHelp()
        {
            OnClick(menu.helpPrevButton, () => FlipHelp(-1));
            OnClick(menu.helpNextButton, () => FlipHelp(1));
            OnClick(menu.helpCloseButton, Back);
            FlipHelp(0);
        }

        void FlipHelp(int delta)
        {
            var help = HelpPages;
            helpIndex = Mathf.Clamp(helpIndex + delta, 0, help.Length - 1);
            menu.helpTitle.text = help[helpIndex].title;
            menu.helpBody.text = help[helpIndex].body;
            menu.helpCounter.text = $"{helpIndex + 1} / {help.Length}";
        }

        // ---------------------------------------------------------------- unit handbook

        /// <summary>One card per unit (from Resources/UI/CodexCard.prefab), three to a row.</summary>
        void FillCodex()
        {
            OnClick(menu.codexCloseButton, Back);
            var cardPrefab = Resources.Load<CodexCardView>("UI/CodexCard");
            const int columns = 3;
            const float gap = 12f;
            float cardW = CodexCardView.Width, cardH = CodexCardView.Height;
            if (cardPrefab != null)
            {
                var size = ((RectTransform)cardPrefab.transform).sizeDelta;
                cardW = size.x;
                cardH = size.y;
            }
            var samples = DoodleTemplates.Samples;
            int rows = (samples.Length + columns - 1) / columns;
            float height = rows * (cardH + gap) + gap;
            menu.codexContent.sizeDelta = new Vector2(0f, height);
            var tips = DrawingTips;
            for (int i = 0; i < samples.Length; i++)
            {
                var card = cardPrefab != null ? Instantiate(cardPrefab, menu.codexContent, false) : CodexCardView.Build(menu.codexContent);
                var rt = (RectTransform)card.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                int col = i % columns, row = i / columns;
                rt.anchoredPosition = new Vector2(gap + col * (cardW + gap), height - (row + 1) * (cardH + gap));
                card.name = "Card " + samples[i].label;
                tips.TryGetValue(samples[i].label, out string tip);
                FillCard(card, samples[i].label, samples[i].make(InkColor.Black), tip);
            }
        }

        static void FillCard(CodexCardView card, string name, Doodle doodle, string tip)
        {
            var s = DoodleAnalyzer.Analyze(doodle);
            card.picture.texture = Thumbnail(name, _ => doodle, 200, new Color(0.45f, 0.8f, 0.95f));
            card.nameText.text = DoodleAnalyzer.DisplayName(s);
            card.howText.text = "✎ " + tip;
            card.hintText.text = s.kind == UnitKind.Robot ? RobotHint(s.robotMode) : DoodleAnalyzer.KindHint(s.kind);
            string power = s.kind == UnitKind.Healer ? $"{Loc.T("Hồi", "Heals")} {s.damage:0}"
                : s.kind == UnitKind.Wall ? Loc.T("Không đánh", "No attack") : $"{Loc.T("Đánh", "Dmg")} {s.damage:0}";
            string tags = (s.flying ? Loc.T(" · Bay", " · Flies") : "") +
                (DoodleQuality.Multiplies(s.kind) ? (s.kind == UnitKind.Healer ? Loc.T(" · Vẽ đẹp ×2", " · Draw well ×2") : Loc.T(" · Vẽ đẹp ×3", " · Draw well ×3")) : "");
            card.statsText.text = $"{Loc.T("Máu", "HP")} {s.maxHp:0} · {power}{(s.range > 0f ? $" · {Loc.T("Tầm", "Range")} {s.range:0.#}" : "")}{tags}";
        }

        static string RobotHint(RobotMode mode)
        {
            switch (mode)
            {
                case RobotMode.Sword: return Loc.T("Chém kiếm lưỡi rộng, trúng cả lính đứng cạnh. Rất trâu", "Broadsword sweeps that also hit whoever stands beside. Very tough");
                case RobotMode.Cannon: return Loc.T("Bắn đại bác nổ lan từ xa. Chậm nhưng cực trâu", "Long-range exploding cannon shots. Slow but super tough");
                case RobotMode.Flyer: return Loc.T("Bay, bắn tên lửa cả mặt đất lẫn lính bay", "Flies, fires rockets at ground and air");
                default: return Loc.T("Cực trâu, đập tay xuống đất gây sát thương lan", "Super tough, ground-pounds nearby enemies");
            }
        }

        // ---------------------------------------------------------------- results

        void ShowResults(Team? winner)
        {
            bool won = winner == Team.Player;
            menu.resultTitle.text = won ? Loc.T("CHIẾN THẮNG!", "VICTORY!") : winner == null ? Loc.T("HÒA!", "DRAW!") : Loc.T("THẤT BẠI...", "DEFEAT...");
            menu.resultTitle.color = won ? new Color(0.95f, 0.6f, 0.1f) : new Color(0.8f, 0.25f, 0.2f);

            // rating: how much of our castle is still standing
            float left = playerBase != null ? Mathf.Clamp01(playerBase.Hp / playerBase.Stats.maxHp) : 0f;
            int stars = !won ? 0 : left > 0.66f ? 3 : left > 0.33f ? 2 : 1;
            menu.resultRating.text = new string('★', stars) + new string('☆', 3 - stars);

            float battle = warStartTime > 0f ? Time.time - warStartTime : 0f;
            int level = playerBase != null ? playerBase.CastleLevel : 1;
            string[] lines =
            {
                $"{Loc.T("Độ khó", "Difficulty")}:  <b>{DifficultyName(difficulty)}</b>",
                $"{Loc.T("Thời gian giao chiến", "Battle time")}:  <b>{FormatTime(battle)}</b>",
                $"{Loc.T("Lính đã vẽ", "Units drawn")}:  <b>{statDrawn}</b>",
                $"{Loc.T("Địch bị hạ", "Enemies defeated")}:  <b>{statKilled}</b>      {Loc.T("Quân ta mất", "Units lost")}:  <b>{statLost}</b>",
                $"{Loc.T("Mực thu được", "Ink earned")}:  <b>{statInkEarned:0}</b>",
                Loc.T($"Lâu đài ta:  <b>cấp {level}</b>,  còn <b>{left * 100f:0}%</b> máu",
                      $"Our castle:  <b>level {level}</b>,  <b>{left * 100f:0}%</b> HP left"),
            };
            menu.resultBody.text = string.Join("\n", lines);
            StartCoroutine(ShowResultsLater());
        }

        /// <summary>Let the castle crumble for a moment before the results cover the battlefield.</summary>
        System.Collections.IEnumerator ShowResultsLater()
        {
            yield return new WaitForSeconds(1.6f);
            ShowPage(Page.Results);
        }

        // ---------------------------------------------------------------- page flow

        /// <summary>Opens a sub-page (handbook, help, settings) that "Back" returns from.</summary>
        void OpenSub(Page sub)
        {
            returnPage = page;
            ShowPage(sub);
            if (sub == Page.Settings)
            {
                RefreshDrawTimeLabel();
                RefreshLanguageLabel();
            }
        }

        void Back() => ShowPage(returnPage == Page.None ? Page.Pause : returnPage);

        void ShowPage(Page next)
        {
            if (next == Page.Pause && Phase == Phase.Over) return;
            if (next != page && next != Page.None && pages.TryGetValue(next, out var shown) && shown != null)
                StartCoroutine(SlideUp((RectTransform)shown.transform));
            page = next;
            foreach (var kv in pages) if (kv.Value != null) kv.Value.SetActive(kv.Key == next);
            menu.gameObject.SetActive(next != Page.None);
            // menus freeze the battle; the results screen lets the last pieces settle
            Time.timeScale = next == Page.None || next == Page.Results ? 1f : 0f;
            pad.Locked = next != Page.None || Phase == Phase.Over;
        }

        /// <summary>A menu sheet pops up from below the screen with a little overshoot (menus run while time is frozen).</summary>
        static System.Collections.IEnumerator SlideUp(RectTransform sheet)
        {
            const float duration = 0.32f, start = -1150f;
            Vector2 home = new Vector2(sheet.anchoredPosition.x, 0f);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (sheet == null) yield break;
                float k = t / duration;
                // ease out with a small bounce past the top
                float e = 1f + 2.4f * Mathf.Pow(k - 1f, 3f) + 1.4f * Mathf.Pow(k - 1f, 2f);
                sheet.anchoredPosition = home + new Vector2(0f, Mathf.LerpUnclamped(start, 0f, e));
                yield return null;
            }
            if (sheet != null) sheet.anchoredPosition = home;
        }

        void UpdateMenus()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            switch (page)
            {
                case Page.None: ShowPage(Page.Pause); break;
                case Page.Pause: ShowPage(Page.None); break;
                case Page.Settings:
                case Page.Help:
                case Page.Codex: Back(); break;
            }
        }

        // ---------------------------------------------------------------- HUD

        void RefreshHud()
        {
            void Set(DoodleUnit castle, RectTransform fill, TMP_Text label, TMP_Text title, string name, bool fromRight)
            {
                // a fallen castle is destroyed: its bar stays at zero
                bool standing = castle != null && castle.IsAlive;
                float max = castle != null ? castle.Stats.maxHp : 1f;
                float hp = standing ? Mathf.Max(0f, castle.Hp) : 0f;
                float ratio = Mathf.Clamp01(hp / max);
                if (fromRight) fill.anchorMin = new Vector2(1f - ratio, 0f);
                else fill.anchorMax = new Vector2(ratio, 1f);
                label.text = standing ? $"{Mathf.CeilToInt(hp)} / {max:0}" : Loc.T("ĐÃ SỤP!", "DESTROYED!");
                if (castle != null) title.text = $"{name}  ·  {Loc.T("cấp", "lv")} {castle.CastleLevel}";
            }
            Set(playerBase, hud.playerBarFill, hud.playerBarLabel, hud.playerBarTitle, Loc.T("THÀNH TA", "OUR CASTLE"), false);
            Set(enemyBase, hud.enemyBarFill, hud.enemyBarLabel, hud.enemyBarTitle, Loc.T("THÀNH ĐỊCH", "ENEMY CASTLE"), true);

            string armies = $"<b>{CountUnits(Team.Player)}  vs  {CountUnits(Team.Enemy)}</b>";
            string extra = Phase == Phase.Draw ? Loc.T($"địch còn vẽ {enemyPlan.Count}", $"enemy drawing {enemyPlan.Count}")
                : Phase == Phase.War ? FormatTime(Time.time - warStartTime) : "";
            countText.text = $"{armies}\n<size=75%>{extra}</size>";
        }

        // ---------------------------------------------------------------- text data

        static (string title, string body)[] HelpPages => new[]
        {
            (Loc.T("Mục tiêu", "Goal"),
             Loc.T("Mỗi bên có một lâu đài. <b>Phá được lâu đài địch là thắng</b>, mất lâu đài mình là thua.\n\n", "Each side has a castle. <b>Destroy the enemy castle to win</b>; lose yours and you lose.\n\n") +
             Loc.T("Đầu trận có thời gian <b>chuẩn bị</b> để vẽ quân. Bấm <b>Bắt đầu chiến!</b> (hoặc hết giờ) là hai bên lao vào nhau.\n\n", "The game opens with <b>prep time</b> to draw your army. Press <b>Start battle!</b> (or let the clock run out) and both armies charge.\n\n") +
             Loc.T("Trong lúc giao chiến bạn <b>vẫn vẽ tiếp</b> để gửi quân tiếp viện.", "During the battle you <b>keep drawing</b> to send reinforcements.")),
            (Loc.T("Vẽ quân", "Drawing units"),
             Loc.T("Vẽ <b>MỘT</b> chiến binh lên trang giấy rồi bấm <b>Triệu hồi!</b>\n\n", "Draw <b>ONE</b> warrior on the page, then press <b>Summon!</b>\n\n") +
             Loc.T("Game nhìn hình để biết đó là lính gì: chân que, vũ khí chéo, súng ngang, khiên tròn, cánh, bánh xe, hình hộp...\n", "The game reads the shape to tell what it is: stick legs, slanted weapons, level guns, round shields, wings, wheels, boxes...\n") +
             Loc.T("Mở <b>Sổ tay quân</b> ở màn hình chính để học cách vẽ từng loại.\n\n", "Open the <b>Unit Handbook</b> on the title screen to learn how to draw each one.\n\n") +
             Loc.T("Màu mực: <b>Đen</b> cân bằng · <b>Đỏ</b> đánh mạnh, ít máu · <b>Xanh</b> trâu, chậm hơn.", "Ink colors: <b>Black</b> balanced · <b>Red</b> hits harder, less HP · <b>Blue</b> tougher, slower.")),
            (Loc.T("Vẽ đẹp, nhiều quân", "Draw well, get more"),
             Loc.T("Lính thường (bộ binh, khiên, ném bom, xạ thủ, cung thủ, thầy thuốc, dơi) <b>vẽ càng giống mẫu càng ra nhiều</b>:\n\n", "Regular troops (soldiers, Spartans, bombers, riflemen, archers, medics, bats) <b>come in bigger numbers the closer you draw them to the handbook</b>:\n\n") +
             Loc.T("★  ra 1 lính      ★★  ra 2 lính      ★★★  ra 3 lính\n\n", "★  1 soldier      ★★  2 soldiers      ★★★  3 soldiers\n\n") +
             Loc.T("Quân mạnh (robot, rồng, xe tăng, máy bay, kỵ binh, máy bắn đá, hồn ma, công trình) luôn ra 1 con.\n\n", "Strong units (robots, dragons, tanks, planes, knights, catapults, ghosts, buildings) are always just one.\n\n") +
             Loc.T("Rồng rất khó vẽ: cần 2 cánh, 2 chân, nhiều khối khép kín và vẽ thật TO, thiếu thì chỉ ra Dơi.", "Dragons are hard: 2 wings, 2 legs, several closed shapes and drawn BIG, or you only get a Bat.")),
            (Loc.T("Mực và lâu đài", "Ink and castle"),
             Loc.T("Mỗi nét vẽ tốn <b>mực</b> theo độ dài. Bắt đầu với 1000 mực.\n\n", "Every stroke costs <b>ink</b> by its length. You start with 1000 ink.\n\n") +
             Loc.T("Hạ được lính địch thì <b>được thưởng mực</b>; trong trận mực cũng hồi chậm.\n\n", "<b>Defeating enemies earns ink</b>; ink also trickles back slowly during the battle.\n\n") +
             Loc.T("Dùng mực để <b>nâng cấp lâu đài</b> (nút dưới chiến trường): thêm máu, tường giáp, cung thủ bắn 2 tên, đại bác.", "Spend ink to <b>upgrade your castle</b> (button under the battlefield): more HP, armored walls, double arrows, cannons.")),
            (Loc.T("Khắc chế", "Counters"),
             Loc.T("· Cung thủ, xạ thủ, tháp canh bắn được <b>lính bay</b>.\n", "· Archers, riflemen and towers can hit <b>fliers</b>.\n") +
             Loc.T("· Giáo <b>Spartan</b> khắc kỵ binh. <b>Ném bom</b> khắc đám đông và lính khiên.\n", "· <b>Spartan</b> spears beat knights. <b>Bombers</b> beat crowds and shields.\n") +
             Loc.T("· <b>Máy bay cánh quạt</b> ném bom khắc xe tăng. <b>Phản lực</b> săn lính bay.\n", "· <b>Prop planes</b> bomb tanks. <b>Jets</b> hunt fliers.\n") +
             Loc.T("· <b>Hồn ma</b>: lính đánh gần không chạm được, hãy dùng cung.\n", "· <b>Ghosts</b> can't be touched by melee: use bows.\n") +
             Loc.T("· <b>Máy bắn đá</b> bắn xa nhất và phá thành mạnh, nhưng yếu khi bị áp sát.\n", "· <b>Catapults</b> outrange everything and crack castles, but are weak up close.\n") +
             Loc.T("· <b>Thầy thuốc</b> hồi máu cho quân xung quanh.", "· <b>Medics</b> heal the troops around them.")),
        };

        static Dictionary<string, string> DrawingTips => new Dictionary<string, string>
        {
            { "Bộ binh", Loc.T("Đầu tròn, bụng tròn, 2 chân que + một nét chéo (kiếm, giáo)", "Round head, round belly, 2 stick legs + a slanted line (sword, spear)") },
            { "Lính khiên", Loc.T("Người + một vòng tròn TO trước bụng (khiên) + giáo chéo", "Human + a BIG circle in front of the belly (shield) + slanted spear") },
            { "Ném bom", Loc.T("Người + một vòng tròn NHỎ ở tay phía trước (quả bom)", "Human + a SMALL circle held out front (the bomb)") },
            { "Xạ thủ", Loc.T("Người + một nét NGANG chĩa ra trước (súng)", "Human + a LEVEL line pointing forward (gun)") },
            { "Kỵ binh", Loc.T("Thân ngựa + 4 chân que, người cưỡi và cây thương", "Horse body + 4 stick legs, a rider and a lance") },
            { "Cung thủ", Loc.T("Người + một nét cong dọc (cung) và mũi tên", "Human + an upright curve (bow) and an arrow") },
            { "Thầy thuốc", Loc.T("Người + dấu + vẽ trên bụng", "Human + a + sign on the belly") },
            { "Tường", Loc.T("Khối khép kín, không chân, không vũ khí", "A closed shape, no legs, no weapon") },
            { "Tháp canh", Loc.T("Khối khép kín không chân + vũ khí hoặc cung", "A closed shape, no legs + a weapon or bow") },
            { "Máy bắn đá", Loc.T("Thùng xe + 2 bánh chạm đất + cần ném dựng chéo lên", "Cart + 2 wheels on the ground + a raised throwing arm") },
            { "Xe tăng", Loc.T("Thân xe + 2 bánh trở lên chạm đất + nòng pháo NGANG", "Hull + 2+ wheels on the ground + a LEVEL barrel") },
            { "Cánh quạt", Loc.T("Thân máy bay + cánh + nét dọc ở mũi (cánh quạt), không chân", "Plane body + wings + an upright line at the nose (propeller), no legs") },
            { "Phản lực", Loc.T("Thân máy bay + cánh, không chân, không cánh quạt", "Plane body + wings, no legs, no propeller") },
            { "Trực thăng", Loc.T("Thân + một nét ngang dài trên nóc (cánh quạt)", "Body + a long line across the roof (rotor)") },
            { "Rồng", Loc.T("2 cánh, 2 chân, từ 3 khối khép kín (đầu, thân, đuôi) và vẽ thật TO", "2 wings, 2 legs, 3+ closed shapes (head, body, tail) and drawn really BIG") },
            { "Robot", Loc.T("Ghép các hình HỘP vuông: đầu, thân, tay, chân", "Put together BOXES: head, body, arms, legs") },
            { "Robot kiếm", Loc.T("Robot + một nét chéo ở tay (kiếm)", "Robot + a slanted line in its hand (sword)") },
            { "Robot pháo", Loc.T("Robot + một ống ngang ở tay (đại bác)", "Robot + a level tube in its hand (cannon)") },
            { "Robot bay", Loc.T("Robot + đôi cánh", "Robot + a pair of wings") },
            { "Hồn ma", Loc.T("Một tấm khép kín, ĐÁY LƯỢN SÓNG, không chân", "A closed sheet with a WAVY HEM, no legs") },
            { "Dơi", Loc.T("Thân nhỏ + đầu + 2 cánh + 2 chân, vẽ đơn giản", "Small body + head + 2 wings + 2 legs, kept simple") },
            { "Nhện", Loc.T("Thân tròn + đầu nhỏ + từ 6 chân trở lên chĩa xuống đất", "Round body + small head + 6 or more legs reaching down to the ground") },
            { "Người tuyết", Loc.T("2–3 vòng tròn chồng lên nhau, to ở dưới, nhỏ ở trên, không chân", "2–3 circles stacked up, big at the bottom, small on top, no legs") },
            { "Khinh khí cầu", Loc.T("Vòng tròn TO ở trên + 2 sợi dây + giỏ nhỏ ở dưới", "A BIG circle on top + 2 ropes + a small basket below") },
        };

    }

    /// <summary>Paints a drawing into a small texture for menus: crayon-filled closed shapes under a dark pen line.</summary>
    public static class DoodleThumbnail
    {
        public static Texture2D Render(Doodle doodle, int size, Color fill)
        {
            var px = new Color32[size * size];
            Rect b = doodle.Bounds;
            float pad = size * 0.08f;
            float scale = (size - pad * 2f) / Mathf.Max(b.width, b.height, 0.01f);
            Vector2 Map(Vector2 p) => new Vector2((p.x - b.center.x) * scale + size * 0.5f, (p.y - b.center.y) * scale + size * 0.5f);

            Color ink = new Color(fill.r * 0.3f, fill.g * 0.3f, fill.b * 0.36f);
            Color crayon = new Color(fill.r, fill.g, fill.b, 0.75f);

            // crayon fill inside closed outlines (even-odd scanlines)
            foreach (var s in doodle.strokes)
            {
                if (!DoodleAnalyzer.IsClosed(s)) continue;
                var poly = s.points.ConvertAll(Map);
                var xs = new List<float>();
                for (int y = 0; y < size; y++)
                {
                    float yc = y + 0.5f;
                    xs.Clear();
                    for (int i = 0; i < poly.Count; i++)
                    {
                        Vector2 a = poly[i], c = poly[(i + 1) % poly.Count];
                        if ((a.y <= yc) == (c.y <= yc)) continue;
                        xs.Add(a.x + (yc - a.y) / (c.y - a.y) * (c.x - a.x));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                        for (int x = Mathf.Max(0, Mathf.CeilToInt(xs[k])); x <= Mathf.Min(size - 1, (int)xs[k + 1]); x++)
                            Blend(px, x + y * size, crayon);
                }
            }

            // pen lines
            float r = Mathf.Max(1.6f, size / 70f);
            foreach (var s in doodle.strokes)
            {
                for (int i = 0; i < s.points.Count; i++)
                {
                    Vector2 a = Map(s.points[i]), c = Map(s.points[Mathf.Min(i + 1, s.points.Count - 1)]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, c)));
                    for (int k = 0; k <= steps; k++) Dot(px, size, Vector2.Lerp(a, c, k / (float)steps), r, ink);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static void Dot(Color32[] px, int size, Vector2 c, float r, Color color)
        {
            int x0 = Mathf.Max(0, (int)(c.x - r - 1)), x1 = Mathf.Min(size - 1, (int)(c.x + r + 1));
            int y0 = Mathf.Max(0, (int)(c.y - r - 1)), y1 = Mathf.Min(size - 1, (int)(c.y + r + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01(r + 0.5f - d); // soft edge
                    if (a > 0f) Blend(px, x + y * size, new Color(color.r, color.g, color.b, a));
                }
        }

        static void Blend(Color32[] px, int i, Color c)
        {
            Color dst = px[i];
            float a = c.a + dst.a * (1f - c.a);
            if (a <= 0f) return;
            Color o = (c * c.a + dst * dst.a * (1f - c.a)) / a;
            o.a = a;
            px[i] = o;
        }
    }
}
