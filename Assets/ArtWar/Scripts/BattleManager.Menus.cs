using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArtWar
{
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>
    /// The game's menus around the battle: title screen (with the unit handbook, how-to-play and settings), the battle
    /// HUD (castle health bars, army counts, pause), the pause menu and the results screen. Every menu page freezes the
    /// battle while it is open, except the results.
    /// </summary>
    public partial class BattleManager
    {
        enum Page { None, Title, Pause, Settings, Help, Codex, Results }

        const float BannerWidth = 900f;
        static readonly Color Paper = new Color(0.98f, 0.95f, 0.87f);
        static readonly Color PaperEdge = new Color(0.24f, 0.17f, 0.12f);
        static readonly Color InkText = new Color(0.16f, 0.13f, 0.22f);
        static readonly Color Primary = new Color(0.95f, 0.6f, 0.15f);
        static readonly Color Secondary = new Color(0.3f, 0.45f, 0.75f);
        static readonly Color Muted = new Color(0.5f, 0.46f, 0.42f);

        /// <summary>Loc.T("Chơi lại", "Play Again") reloads straight into a new battle; Loc.T("Về màn hình chính", "Main Menu") shows the title again.</summary>
        static bool skipTitle;
        static Difficulty difficulty = Difficulty.Normal;

        Page page = Page.None, returnPage = Page.None;
        readonly Dictionary<Page, GameObject> pages = new Dictionary<Page, GameObject>();
        GameObject menuLayer;
        Transform canvasRoot;

        // battle record for the results screen
        int statDrawn, statKilled, statLost;
        float statInkEarned, warStartTime;

        // HUD
        RectTransform playerBarFill, enemyBarFill;
        Text playerBarLabel, enemyBarLabel, playerBarTitle, enemyBarTitle;

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
            canvasRoot = pad.transform.root;
            pad.Assist = PlayerPrefs.GetInt("artwar.assist", 1) == 1;
            assistLabel.text = pad.Assist ? Loc.T("Hỗ trợ\nBẬT", "Assist\nON") : Loc.T("Hỗ trợ\nTẮT", "Assist\nOFF");

            // pause button, top right of the battlefield
            var pause = MakeButton(canvasRoot, Vector2.zero, new Vector2(84f, 84f), new Color(0f, 0f, 0f, 0.45f), "II",
                () => ShowPage(Page.Pause));
            var prt = (RectTransform)pause.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-16f, -14f);
            pause.GetComponentInChildren<Text>().fontSize = 36;

            // everything below sits on a dimmed layer that blocks the battle
            var layer = MakeRect(canvasRoot, "Menus", Vector2.zero, Vector2.zero);
            Stretch(layer);
            // menus cover the whole screen: a sheet of squared exercise-book paper
            var backdrop = layer.gameObject.AddComponent<RawImage>();
            backdrop.texture = GridPaperTile();
            backdrop.uvRect = new Rect(0f, 0f, 1920f / 64f, 1080f / 64f);
            menuLayer = layer.gameObject;

            pages[Page.Title] = BuildTitle(layer);
            pages[Page.Pause] = BuildPause(layer);
            pages[Page.Settings] = BuildSettings(layer);
            pages[Page.Help] = BuildHelp(layer);
            pages[Page.Codex] = BuildCodex(layer);
            pages[Page.Results] = new GameObject("Results", typeof(RectTransform));
            pages[Page.Results].transform.SetParent(layer, false);

            ShowPage(skipTitle ? Page.None : Page.Title);
            if (reopenSettings && page == Page.Title) OpenSub(Page.Settings);
            reopenSettings = false;
            skipTitle = false;
        }

        /// <summary>One 64×64 square of exercise-book paper (faint blue grid, a slightly darker line every square).</summary>
        static Texture2D GridPaperTile()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            var rng = new System.Random(5);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int grain = rng.Next(-5, 4); // paper fibres
                    var c = new Color32((byte)(236 + grain), (byte)(230 + grain), (byte)(214 + grain), 255);
                    if (x == 0 || y == 0) c = new Color32(176, 196, 222, 255);
                    else if (x == n / 2 || y == n / 2) c = new Color32(212, 220, 228, 255);
                    px[y * n + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>A sheet of paper in the middle of the screen with a dark frame, like the drawing page.</summary>
        RectTransform MakeSheet(Transform parent, string name, Vector2 size)
        {
            var frame = MakePanel(parent, Vector2.zero, size + new Vector2(24f, 24f), PaperEdge);
            frame.name = name;
            Center(frame, Vector2.zero);
            var sheet = MakePanel(frame, new Vector2(12f, 12f), size, Paper);
            sheet.name = "Sheet";
            return sheet;
        }

        Text MakeHeading(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var t = MakeLabel(parent, pos, size, fontSize, TextAnchor.MiddleCenter, color);
            t.text = text;
            t.fontStyle = FontStyle.Bold;
            return t;
        }

        Button MakeMenuButton(Transform parent, Vector2 pos, Vector2 size, Color color, string label,
            UnityEngine.Events.UnityAction onClick, int fontSize = 30)
        {
            var b = MakeButton(parent, pos, size, color, label, onClick);
            b.GetComponentInChildren<Text>().fontSize = fontSize;
            b.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4f, -4f);
            return b;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- title

        readonly List<(Difficulty d, Image image)> difficultyButtons = new List<(Difficulty, Image)>();

        GameObject BuildTitle(Transform layer)
        {
            var sheet = MakeSheet(layer, "Title", new Vector2(1240f, 900f));

            var logo = MakeHeading(sheet, "ART WAR", new Vector2(0f, 700f), new Vector2(1240f, 170f), 150, Primary);
            var shadow = logo.gameObject.AddComponent<Outline>();
            shadow.effectColor = PaperEdge;
            shadow.effectDistance = new Vector2(5f, -5f);
            MakeHeading(sheet, Loc.T("Vẽ quân  ·  Hóa thật  ·  Phá thành!", "Draw  ·  Summon  ·  Siege!"), new Vector2(0f, 645f), new Vector2(1240f, 50f), 34, InkText);

            // a little parade of drawings across the page
            string[] parade = { "Lính khiên", "Robot kiếm", "Rồng", "Xe tăng", "Hồn ma", "Cung thủ" };
            for (int i = 0; i < parade.Length; i++)
            {
                var make = SampleByLabel(parade[i]);
                if (make == null) continue;
                var img = MakeRect(sheet, "Doodle", new Vector2(70f + i * 185f, 440f), new Vector2(175f, 175f));
                img.gameObject.AddComponent<RawImage>().texture = DoodleThumbnail.Render(make(InkColor.Black), 192,
                    i % 2 == 0 ? new Color(0.45f, 0.8f, 0.95f) : new Color(0.98f, 0.6f, 0.75f));
            }

            // difficulty
            MakeHeading(sheet, Loc.T("Độ khó", "Difficulty"), new Vector2(0f, 375f), new Vector2(1240f, 44f), 28, InkText);
            var levels = new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard };
            for (int i = 0; i < levels.Length; i++)
            {
                var d = levels[i];
                var b = MakeMenuButton(sheet, new Vector2(320f + i * 205f, 305f), new Vector2(190f, 64f), Muted,
                    DifficultyName(d), () => SetDifficulty(d), 28);
                difficultyButtons.Add((d, b.GetComponent<Image>()));
            }
            RefreshDifficultyButtons();


            MakeMenuButton(sheet, new Vector2(420f, 180f), new Vector2(400f, 100f), Primary, Loc.T("CHƠI", "PLAY"), StartFromTitle, 46);
            MakeMenuButton(sheet, new Vector2(110f, 50f), new Vector2(320f, 84f), Secondary, Loc.T("Sổ tay quân", "Unit Handbook"),
                () => OpenSub(Page.Codex));
            MakeMenuButton(sheet, new Vector2(460f, 50f), new Vector2(320f, 84f), Secondary, Loc.T("Hướng dẫn", "How to Play"),
                () => OpenSub(Page.Help));
            MakeMenuButton(sheet, new Vector2(810f, 50f), new Vector2(320f, 84f), Secondary, Loc.T("Cài đặt", "Settings"),
                () => OpenSub(Page.Settings));
            return sheet.parent.gameObject;
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
            foreach (var (d, image) in difficultyButtons) image.color = d == difficulty ? Primary : Muted;
        }

        void StartFromTitle()
        {
            phaseTimer = drawPhaseSeconds;
            ShowPage(Page.None);
            Sfx.Play(Sound.WarCry, 0.7f);
        }

        static System.Func<InkColor, Doodle> SampleByLabel(string label)
        {
            foreach (var (l, make) in DoodleTemplates.Samples)
                if (l == label) return make;
            return null;
        }

        // ---------------------------------------------------------------- pause

        GameObject BuildPause(Transform layer)
        {
            var sheet = MakeSheet(layer, "Pause", new Vector2(560f, 700f));
            MakeHeading(sheet, Loc.T("TẠM DỪNG", "PAUSED"), new Vector2(0f, 590f), new Vector2(560f, 90f), 60, InkText);
            float y = 470f;
            void Item(string label, Color color, UnityEngine.Events.UnityAction action)
            {
                MakeMenuButton(sheet, new Vector2(80f, y), new Vector2(400f, 82f), color, label, action);
                y -= 105f;
            }
            Item(Loc.T("Tiếp tục", "Resume"), Primary, () => ShowPage(Page.None));
            Item(Loc.T("Hướng dẫn", "How to Play"), Secondary, () => OpenSub(Page.Help));
            Item(Loc.T("Cài đặt", "Settings"), Secondary, () => OpenSub(Page.Settings));
            Item(Loc.T("Chơi lại", "Play Again"), Muted, Restart);
            Item(Loc.T("Về màn hình chính", "Main Menu"), Muted, GoHome);
            return sheet.parent.gameObject;
        }

        void Restart()
        {
            skipTitle = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void GoHome()
        {
            skipTitle = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ---------------------------------------------------------------- settings

        Text drawTimeLabel;

        GameObject BuildSettings(Transform layer)
        {
            var sheet = MakeSheet(layer, "Settings", new Vector2(760f, 700f));
            MakeHeading(sheet, Loc.T("CÀI ĐẶT", "SETTINGS"), new Vector2(0f, 600f), new Vector2(760f, 80f), 56, InkText);

            MakeLabel(sheet, new Vector2(60f, 500f), new Vector2(260f, 50f), 30, TextAnchor.MiddleLeft, InkText).text = Loc.T("Âm thanh", "Sound");
            MakeSlider(sheet, new Vector2(320f, 510f), 380f, Sfx.MasterVolume, v =>
            {
                Sfx.MasterVolume = v;
                Sfx.MusicVolume = Sfx.MusicVolume; // music sits on top of the master level
            });
            MakeLabel(sheet, new Vector2(60f, 410f), new Vector2(260f, 50f), 30, TextAnchor.MiddleLeft, InkText).text = Loc.T("Nhạc nền", "Music");
            MakeSlider(sheet, new Vector2(320f, 420f), 380f, Sfx.MusicVolume, v => Sfx.MusicVolume = v);

            MakeLabel(sheet, new Vector2(60f, 315f), new Vector2(260f, 50f), 30, TextAnchor.MiddleLeft, InkText).text = Loc.T("Hỗ trợ vẽ", "Drawing assist");
            var assist = MakeMenuButton(sheet, new Vector2(320f, 310f), new Vector2(380f, 64f), Secondary, " ", () => { }, 26);
            var assistText = assist.GetComponentInChildren<Text>();
            void ShowAssist() => assistText.text = pad.Assist ? Loc.T("BẬT: nét vẽ được làm mượt", "ON: strokes are smoothed") : Loc.T("TẮT: giữ nguyên nét tay", "OFF: raw hand strokes");
            ShowAssist();
            assist.onClick.AddListener(() =>
            {
                OnAssistClicked();
                ShowAssist();
                SaveSettings();
            });

            MakeLabel(sheet, new Vector2(60f, 215f), new Vector2(260f, 50f), 30, TextAnchor.MiddleLeft, InkText).text = Loc.T("Chuẩn bị", "Prep time");
            var time = MakeMenuButton(sheet, new Vector2(320f, 210f), new Vector2(380f, 64f), Secondary, " ", CycleDrawTime, 26);
            drawTimeLabel = time.GetComponentInChildren<Text>();
            RefreshDrawTimeLabel();

            MakeLabel(sheet, new Vector2(60f, 125f), new Vector2(260f, 50f), 30, TextAnchor.MiddleLeft, InkText).text =
                Loc.T("Ngôn ngữ", "Language");
            // language dropdown: the box shows the current language, clicking it opens the list below
            var language = MakeMenuButton(sheet, new Vector2(320f, 120f), new Vector2(380f, 64f), Secondary, " ",
                () => languageList.SetActive(!languageList.activeSelf), 26);
            languageLabel = language.GetComponentInChildren<Text>();

            MakeMenuButton(sheet, new Vector2(230f, 25f), new Vector2(300f, 76f), Primary, Loc.T("Xong", "Done"), Back);

            // the open list sits on top of everything else on the sheet
            var list = MakePanel(sheet, new Vector2(320f, 120f - 2f * 62f - 6f), new Vector2(380f, 2f * 62f + 6f), PaperEdge);
            list.name = "Language List";
            var options = new[] { (Language.English, "English"), (Language.Vietnamese, "Tiếng Việt") };
            for (int i = 0; i < options.Length; i++)
            {
                var (lang, name) = options[i];
                var item = MakeButton(list, new Vector2(3f, 3f + (options.Length - 1 - i) * 62f), new Vector2(374f, 60f),
                    lang == Loc.Current ? Primary : Paper, name, () =>
                    {
                        languageList.SetActive(false);
                        SetLanguage(lang);
                    });
                var text = item.GetComponentInChildren<Text>();
                text.fontSize = 26;
                text.color = lang == Loc.Current ? Color.white : InkText;
            }
            languageList = list.gameObject;
            languageList.SetActive(false);
            RefreshLanguageLabel();
            return sheet.parent.gameObject;
        }

        Text languageLabel;
        GameObject languageList;

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
            languageLabel.text = (atTitle ? chosen : chosen + Loc.T(" (ván sau)", " (next game)")) + "  ▾";
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
            drawTimeLabel.text = FormatTime(drawPhaseSeconds) + Loc.T(" để vẽ quân", " to draw") + when;
        }

        /// <summary>A plain horizontal slider: dark track, orange fill, round-ish knob.</summary>
        void MakeSlider(Transform parent, Vector2 pos, float width, float value, System.Action<float> onChange)
        {
            var root = MakeRect(parent, "Slider", pos, new Vector2(width, 40f));
            var slider = root.gameObject.AddComponent<Slider>();

            var track = MakePanel(root, Vector2.zero, Vector2.zero, new Color(0.3f, 0.26f, 0.22f));
            track.anchorMin = new Vector2(0f, 0.3f);
            track.anchorMax = new Vector2(1f, 0.7f);
            track.offsetMin = track.offsetMax = Vector2.zero;

            var fillArea = MakeRect(root, "Fill Area", Vector2.zero, Vector2.zero);
            fillArea.anchorMin = new Vector2(0f, 0.3f);
            fillArea.anchorMax = new Vector2(1f, 0.7f);
            fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            var fill = MakePanel(fillArea, Vector2.zero, Vector2.zero, Primary);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            var handleArea = MakeRect(root, "Handle Area", Vector2.zero, Vector2.zero);
            Stretch(handleArea);
            var handle = MakePanel(handleArea, Vector2.zero, new Vector2(30f, 0f), PaperEdge);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.offsetMin = new Vector2(-15f, 0f);
            handle.offsetMax = new Vector2(15f, 0f);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.AddListener(v =>
            {
                onChange(v);
                SaveSettings();
            });
        }

        // ---------------------------------------------------------------- how to play

        static (string title, string body)[] HelpPages => new[]
        {
            (Loc.T("Mục tiêu", "Goal"),
             Loc.T("Mỗi bên có một lâu đài. <b>Phá được lâu đài địch là thắng</b>, mất lâu đài mình là thua.\n\n", "Each side has a castle. <b>Destroy the enemy castle to win</b>; lose yours and you lose.\n\n") +
             Loc.T("Đầu trận có thời gian <b>chuẩn bị</b> để vẽ quân. Bấm <b>Bắt đầu chiến!</b> (hoặc hết giờ) là hai bên lao vào nhau.\n\n", "The game opens with <b>prep time</b> to draw your army. Press <b>Start battle!</b> (or let the clock run out) and both armies charge.\n\n") +
             Loc.T("Trong lúc giao chiến bạn <b>vẫn vẽ tiếp</b> để gửi quân tiếp viện.", "During the battle you <b>keep drawing</b> to send reinforcements.")),
            (Loc.T("Vẽ quân", "Drawing units"),
             Loc.T("Vẽ <b>MỘT</b> chiến binh lên trang giấy rồi bấm <b>Hóa thật!</b>\n\n", "Draw <b>ONE</b> warrior on the page, then press <b>Summon!</b>\n\n") +
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

        int helpIndex;
        Text helpTitle, helpBody, helpCounter;

        GameObject BuildHelp(Transform layer)
        {
            var sheet = MakeSheet(layer, "Help", new Vector2(1100f, 780f));
            MakeHeading(sheet, Loc.T("HƯỚNG DẪN", "HOW TO PLAY"), new Vector2(0f, 690f), new Vector2(1100f, 70f), 52, InkText);
            helpTitle = MakeHeading(sheet, "", new Vector2(0f, 620f), new Vector2(1100f, 56f), 38, Primary);
            helpBody = MakeLabel(sheet, new Vector2(70f, 150f), new Vector2(960f, 460f), 30, TextAnchor.UpperLeft, InkText);
            helpBody.lineSpacing = 1.15f;
            helpCounter = MakeLabel(sheet, new Vector2(450f, 60f), new Vector2(200f, 60f), 28, TextAnchor.MiddleCenter, InkText);
            MakeMenuButton(sheet, new Vector2(60f, 50f), new Vector2(240f, 76f), Secondary, Loc.T("◀ Trước", "◀ Back"), () => FlipHelp(-1));
            MakeMenuButton(sheet, new Vector2(800f, 50f), new Vector2(240f, 76f), Secondary, Loc.T("Sau ▶", "Next ▶"), () => FlipHelp(1));
            MakeMenuButton(sheet, new Vector2(1020f, 700f), new Vector2(64f, 64f), Muted, "X", Back);
            FlipHelp(0);
            return sheet.parent.gameObject;
        }

        void FlipHelp(int delta)
        {
            helpIndex = Mathf.Clamp(helpIndex + delta, 0, HelpPages.Length - 1);
            helpTitle.text = HelpPages[helpIndex].title;
            helpBody.text = HelpPages[helpIndex].body;
            helpCounter.text = $"{helpIndex + 1} / {HelpPages.Length}";
        }

        // ---------------------------------------------------------------- unit handbook

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
        };

        GameObject BuildCodex(Transform layer)
        {
            var sheet = MakeSheet(layer, "Codex", new Vector2(1720f, 960f));
            MakeHeading(sheet, Loc.T("SỔ TAY QUÂN", "UNIT HANDBOOK"), new Vector2(0f, 880f), new Vector2(1720f, 70f), 52, InkText);
            MakeLabel(sheet, new Vector2(0f, 848f), new Vector2(1720f, 34f), 22, TextAnchor.MiddleCenter, Muted).text =
                Loc.T("Cách vẽ từng loại quân. Trong trận không có hình mẫu, hãy nhớ kỹ!  (cuộn để xem thêm)", "How to draw every unit. There are no guides in battle, so remember them!  (scroll for more)");
            MakeMenuButton(sheet, new Vector2(1640f, 880f), new Vector2(64f, 64f), Muted, "X", Back);

            // scrolling grid of cards
            var view = MakeRect(sheet, "Viewport", new Vector2(20f, 20f), new Vector2(1680f, 820f));
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.04f);
            view.gameObject.AddComponent<RectMask2D>();
            var content = MakeRect(view, "Content", Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = sheet.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;

            const int columns = 3;
            const float cardW = 548f, cardH = 250f, gap = 12f;
            var samples = DoodleTemplates.Samples;
            int rows = (samples.Length + columns - 1) / columns;
            float height = rows * (cardH + gap) + gap;
            content.sizeDelta = new Vector2(0f, height);
            for (int i = 0; i < samples.Length; i++)
            {
                int col = i % columns, row = i / columns;
                var pos = new Vector2(gap + col * (cardW + gap), height - (row + 1) * (cardH + gap));
                BuildCard(content, pos, new Vector2(cardW, cardH), samples[i].label, samples[i].make);
            }
            return sheet.parent.gameObject;
        }

        void BuildCard(Transform parent, Vector2 pos, Vector2 size, string label, System.Func<InkColor, Doodle> make)
        {
            var card = MakePanel(parent, pos, size, new Color(1f, 1f, 1f, 0.75f));
            card.name = "Card " + label;
            var doodle = make(InkColor.Black);
            var s = DoodleAnalyzer.Analyze(doodle);

            var thumb = MakeRect(card, "Doodle", new Vector2(8f, 25f), new Vector2(200f, 200f));
            thumb.gameObject.AddComponent<RawImage>().texture =
                DoodleThumbnail.Render(doodle, 200, new Color(0.45f, 0.8f, 0.95f));

            float x = 215f, w = size.x - x - 10f;
            var name = MakeLabel(card, new Vector2(x, 200f), new Vector2(w, 40f), 26, TextAnchor.MiddleLeft, InkText);
            name.text = DoodleAnalyzer.DisplayName(s);
            name.fontStyle = FontStyle.Bold;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 26;

            DrawingTips.TryGetValue(label, out string tip);
            var how = MakeLabel(card, new Vector2(x, 128f), new Vector2(w, 72f), 19, TextAnchor.UpperLeft,
                new Color(0.75f, 0.35f, 0.1f));
            how.text = "✎ " + tip;

            var hint = MakeLabel(card, new Vector2(x, 62f), new Vector2(w, 64f), 17, TextAnchor.UpperLeft, InkText);
            hint.text = s.kind == UnitKind.Robot ? RobotHint(s.robotMode) : DoodleAnalyzer.KindHint(s.kind);

            string power = s.kind == UnitKind.Healer ? $"{Loc.T("Hồi", "Heals")} {s.damage:0}" : s.kind == UnitKind.Wall ? Loc.T("Không đánh", "No attack") : $"{Loc.T("Đánh", "Dmg")} {s.damage:0}";
            string tags = (s.flying ? Loc.T(" · Bay", " · Flies") : "") +
                (DoodleQuality.Multiplies(s.kind) ? (s.kind == UnitKind.Healer ? Loc.T(" · Vẽ đẹp ×2", " · Draw well ×2") : Loc.T(" · Vẽ đẹp ×3", " · Draw well ×3")) : "");
            var line = MakeLabel(card, new Vector2(x, 10f), new Vector2(w, 46f), 18, TextAnchor.MiddleLeft, Secondary);
            line.text = $"{Loc.T("Máu", "HP")} {s.maxHp:0} · {power}{(s.range > 0f ? $" · {Loc.T("Tầm", "Range")} {s.range:0.#}" : "")}{tags}";
            line.fontStyle = FontStyle.Bold;
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
            var holder = pages[Page.Results].transform;
            foreach (Transform child in holder) Destroy(child.gameObject);

            bool won = winner == Team.Player;
            var sheet = MakeSheet(holder, "Results Sheet", new Vector2(900f, 760f));
            MakeHeading(sheet, won ? Loc.T("CHIẾN THẮNG!", "VICTORY!") : winner == null ? Loc.T("HÒA!", "DRAW!") : Loc.T("THẤT BẠI...", "DEFEAT..."),
                new Vector2(0f, 630f), new Vector2(900f, 120f), 96,
                won ? new Color(0.95f, 0.6f, 0.1f) : new Color(0.8f, 0.25f, 0.2f)).gameObject.AddComponent<Outline>()
                .effectColor = PaperEdge;

            // rating: how much of our castle is still standing
            float left = playerBase != null ? Mathf.Clamp01(playerBase.Hp / playerBase.Stats.maxHp) : 0f;
            int stars = !won ? 0 : left > 0.66f ? 3 : left > 0.33f ? 2 : 1;
            var rating = MakeHeading(sheet, new string('★', stars) + new string('☆', 3 - stars), new Vector2(0f, 555f),
                new Vector2(900f, 80f), 70, new Color(1f, 0.75f, 0.1f));
            rating.gameObject.AddComponent<Outline>().effectColor = PaperEdge;

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
            var body = MakeLabel(sheet, new Vector2(120f, 170f), new Vector2(660f, 370f), 32, TextAnchor.MiddleLeft, InkText);
            body.text = string.Join("\n", lines);
            body.lineSpacing = 1.2f;

            MakeMenuButton(sheet, new Vector2(90f, 50f), new Vector2(340f, 92f), Primary, Loc.T("Chơi lại", "Play Again"), Restart, 36);
            MakeMenuButton(sheet, new Vector2(470f, 50f), new Vector2(340f, 92f), Secondary, Loc.T("Màn hình chính", "Main Menu"), GoHome, 32);

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
            page = next;
            foreach (var kv in pages) kv.Value.SetActive(kv.Key == next);
            menuLayer.SetActive(next != Page.None);
            // menus freeze the battle; the results screen lets the last pieces settle
            Time.timeScale = next == Page.None || next == Page.Results ? 1f : 0f;
            pad.Locked = next != Page.None || Phase == Phase.Over;
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

        void BuildCastleBars(RectTransform banner)
        {
            (RectTransform fill, Text label, Text title) Bar(float x, Color color, bool fromRight)
            {
                var title = MakeLabel(banner, new Vector2(x, 50f), new Vector2(330f, 32f), 22,
                    fromRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Color.white);
                title.fontStyle = FontStyle.Bold;
                var bg = MakePanel(banner, new Vector2(x, 12f), new Vector2(330f, 34f), new Color(0.12f, 0.1f, 0.1f, 0.9f));
                var fill = MakePanel(bg, Vector2.zero, Vector2.zero, color);
                fill.anchorMin = fromRight ? new Vector2(0f, 0f) : Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = new Vector2(3f, 3f);
                fill.offsetMax = new Vector2(-3f, -3f);
                var label = MakeLabel(bg, Vector2.zero, new Vector2(330f, 34f), 20, TextAnchor.MiddleCenter, Color.white);
                label.fontStyle = FontStyle.Bold;
                label.gameObject.AddComponent<Shadow>();
                return (fill, label, title);
            }
            (playerBarFill, playerBarLabel, playerBarTitle) = Bar(16f, new Color(0.3f, 0.55f, 1f), false);
            (enemyBarFill, enemyBarLabel, enemyBarTitle) = Bar(BannerWidth - 346f, new Color(0.95f, 0.35f, 0.3f), true);
        }

        void RefreshHud()
        {
            void Set(DoodleUnit castle, RectTransform fill, Text label, Text title, string name, bool fromRight)
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
            Set(playerBase, playerBarFill, playerBarLabel, playerBarTitle, Loc.T("THÀNH TA", "OUR CASTLE"), false);
            Set(enemyBase, enemyBarFill, enemyBarLabel, enemyBarTitle, Loc.T("THÀNH ĐỊCH", "ENEMY CASTLE"), true);

            string armies = $"<b>{CountUnits(Team.Player)}  vs  {CountUnits(Team.Enemy)}</b>";
            string extra = Phase == Phase.Draw ? Loc.T($"địch còn vẽ {enemyPlan.Count}", $"enemy drawing {enemyPlan.Count}")
                : Phase == Phase.War ? FormatTime(Time.time - warStartTime) : "";
            countText.text = $"{armies}\n<size=17>{extra}</size>";
        }
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
