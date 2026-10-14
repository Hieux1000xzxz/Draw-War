using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArtWar
{
    /// <summary>
    /// Every menu page, each a taped sheet of paper on a full-screen exercise-book backdrop: title, pause, settings,
    /// how to play, unit handbook and results. Saved as Resources/UI/Menus.prefab; the game shows one page at a
    /// time and fills in the moving parts (texts, thumbnails, handbook cards) through these fields.
    /// </summary>
    public class MenuView : MonoBehaviour
    {
        [Header("Pages")]
        public GameObject backdrop;
        public GameObject titlePage, pausePage, settingsPage, helpPage, codexPage, resultsPage;

        [Header("Title")]
        public TMP_Text logo;
        public RawImage[] parade = new RawImage[6];
        public Button[] difficultyButtons = new Button[3];
        public Button playButton, versusButton, onlineButton, practiceButton, codexButton, helpButton, settingsButton;

        [Header("Pause")]
        public Button resumeButton, pauseHelpButton, pauseSettingsButton, restartButton, homeButton;

        [Header("Settings")]
        public Slider soundSlider, musicSlider;
        public Button assistButton, drawTimeButton, languageButton, settingsDoneButton;
        public TMP_Text assistText, drawTimeText, languageText;
        public GameObject languageList;
        public Button[] languageOptions = new Button[2];

        [Header("How to play")]
        public TMP_Text helpTitle, helpBody, helpCounter;
        public Button helpPrevButton, helpNextButton, helpCloseButton;

        [Header("Unit handbook")]
        public RectTransform codexContent;
        public Button codexCloseButton;

        [Header("Results")]
        public TMP_Text resultTitle, resultRating, resultBody;
        public Button resultAgainButton, resultHomeButton;

        public static MenuView Build()
        {
            var go = UiKit.Canvas("Menus", 10);
            var v = go.AddComponent<MenuView>();

        public static readonly Color PracticeGreen = new Color(0.33f, 0.66f, 0.45f);
        public static readonly Color VersusRed = new Color(0.86f, 0.32f, 0.3f);
        public static readonly Color OnlinePurple = new Color(0.55f, 0.4f, 0.8f);

            var layer = UiKit.Rect(go.transform, "Backdrop", Vector2.zero, Vector2.zero);
            UiKit.Stretch(layer);
            var paper = layer.gameObject.AddComponent<RawImage>();
            paper.texture = UiKit.GridPaper;
            paper.uvRect = new Rect(0f, 0f, 1920f / 64f, 1080f / 64f);
            v.backdrop = layer.gameObject;

            BuildTitle(v, layer);
            BuildPause(v, layer);
            BuildSettings(v, layer);
            BuildHelp(v, layer);
            BuildCodex(v, layer);
            BuildResults(v, layer);
            return v;
        }

        static TMP_Text Heading(Transform sheet, string name, float y, float width, int size, string vi, string en, Color? color = null) =>
            UiKit.Label(sheet, name, new Vector2(0f, y), new Vector2(width, size * 1.5f), size, TextAnchor.MiddleCenter,
                color ?? UiKit.InkText, vi, en, true);

        static void BuildTitle(MenuView v, Transform layer)
        {
            var sheet = UiKit.Sheet(layer, "Title Page", new Vector2(1260f, 920f));
            v.titlePage = sheet.gameObject;
            v.logo = UiKit.Label(sheet, "Logo", new Vector2(0f, 705f), new Vector2(1260f, 175f), 150, TextAnchor.MiddleCenter,
                UiKit.Primary, "DRAW WAR", "DRAW WAR", true);
            UiKit.Outlined(v.logo);
            Heading(sheet, "Tagline", 640f, 1260f, 34, "Vẽ quân  ·  Triệu hồi  ·  Phá thành!", "Draw  ·  Fight  ·  Win!");

            for (int i = 0; i < v.parade.Length; i++)
            {
                var img = UiKit.Rect(sheet, "Parade " + (i + 1), new Vector2(80f + i * 185f, 440f), new Vector2(175f, 175f));
                v.parade[i] = img.gameObject.AddComponent<RawImage>();
            }

            Heading(sheet, "Difficulty Title", 372f, 1260f, 28, "Độ khó", "Difficulty");
            string[] vi = { "Dễ", "Thường", "Khó" }, en = { "Easy", "Normal", "Hard" };
            for (int i = 0; i < 3; i++)
                v.difficultyButtons[i] = UiKit.Button(sheet, "Difficulty " + en[i], new Vector2(320f + i * 210f, 300f), new Vector2(200f, 70f),
                    UiKit.Muted, 28, vi[i], en[i]);

            v.playButton = UiKit.Button(sheet, "Play", new Vector2(90f, 170f), new Vector2(340f, 110f), UiKit.Primary, 44, "CHƠI", "PLAY");
            v.versusButton = UiKit.Button(sheet, "Versus", new Vector2(460f, 170f), new Vector2(340f, 110f), VersusRed, 34, "2 NGƯỜI CHƠI", "2 PLAYERS");
            v.onlineButton = UiKit.Button(sheet, "Online", new Vector2(830f, 170f), new Vector2(340f, 110f), OnlinePurple, 34, "CHƠI ONLINE", "ONLINE");
            // bottom row: practice room, unit handbook, how to play, settings
            v.practiceButton = UiKit.Button(sheet, "Practice", new Vector2(60f, 45f), new Vector2(270f, 92f), PracticeGreen, 28, "Luyện vẽ", "Practice");
            v.codexButton = UiKit.Button(sheet, "Handbook", new Vector2(350f, 45f), new Vector2(270f, 92f), UiKit.Secondary, 28, "Sổ tay quân", "Unit Handbook");
            v.helpButton = UiKit.Button(sheet, "How To Play", new Vector2(640f, 45f), new Vector2(270f, 92f), UiKit.Secondary, 28, "Hướng dẫn", "How to Play");
            v.settingsButton = UiKit.Button(sheet, "Settings", new Vector2(930f, 45f), new Vector2(270f, 92f), UiKit.Secondary, 28, "Cài đặt", "Settings");
        }

        static void BuildPause(MenuView v, Transform layer)
        {
            var sheet = UiKit.Sheet(layer, "Pause Page", new Vector2(580f, 740f));
            v.pausePage = sheet.gameObject;
            Heading(sheet, "Title", 600f, 580f, 60, "TẠM DỪNG", "PAUSED");
            float y = 480f;
            Button Item(string name, Color color, string vi, string en)
            {
                var b = UiKit.Button(sheet, name, new Vector2(80f, y), new Vector2(420f, 90f), color, 30, vi, en);
                y -= 108f;
                return b;
            }
            v.resumeButton = Item("Resume", UiKit.Primary, "Tiếp tục", "Resume");
            v.pauseHelpButton = Item("How To Play", UiKit.Secondary, "Hướng dẫn", "How to Play");
            v.pauseSettingsButton = Item("Settings", UiKit.Secondary, "Cài đặt", "Settings");
            v.restartButton = Item("Play Again", UiKit.Muted, "Chơi lại", "Play Again");
            v.homeButton = Item("Main Menu", UiKit.Muted, "Về màn hình chính", "Main Menu");
        }

        static void BuildSettings(MenuView v, Transform layer)
        {
            var sheet = UiKit.Sheet(layer, "Settings Page", new Vector2(800f, 740f));
            v.settingsPage = sheet.gameObject;
            Heading(sheet, "Title", 615f, 800f, 56, "CÀI ĐẶT", "SETTINGS");

            TMP_Text Row(string name, float y, string vi, string en) =>
                UiKit.Label(sheet, name, new Vector2(70f, y), new Vector2(270f, 50f), 30, TextAnchor.MiddleLeft, UiKit.InkText, vi, en);
            Row("Sound Label", 515f, "Âm thanh", "Sound");
            v.soundSlider = UiKit.Slider(sheet, "Sound", new Vector2(340f, 518f), 390f);
            Row("Music Label", 425f, "Nhạc nền", "Music");
            v.musicSlider = UiKit.Slider(sheet, "Music", new Vector2(340f, 428f), 390f);

            Row("Assist Label", 330f, "Hỗ trợ vẽ", "Drawing assist");
            v.assistButton = UiKit.Button(sheet, "Assist", new Vector2(340f, 320f), new Vector2(390f, 70f), UiKit.Secondary, 24);
            v.assistText = UiKit.ButtonLabel(v.assistButton);
            Row("Prep Label", 235f, "Chuẩn bị", "Prep time");
            v.drawTimeButton = UiKit.Button(sheet, "Prep Time", new Vector2(340f, 225f), new Vector2(390f, 70f), UiKit.Secondary, 24);
            v.drawTimeText = UiKit.ButtonLabel(v.drawTimeButton);
            Row("Language Label", 140f, "Ngôn ngữ", "Language");
            v.languageButton = UiKit.Button(sheet, "Language", new Vector2(340f, 130f), new Vector2(390f, 70f), UiKit.Secondary, 24);
            v.languageText = UiKit.ButtonLabel(v.languageButton);

            v.settingsDoneButton = UiKit.Button(sheet, "Done", new Vector2(250f, 28f), new Vector2(300f, 82f), UiKit.Primary, 30, "Xong", "Done");

            // the dropdown's list, opening below the language box and over everything else
            var list = UiKit.Panel(sheet, "Language List", new Vector2(340f, 130f - 2f * 66f - 8f), new Vector2(390f, 2f * 66f + 10f), UiKit.Paper);
            v.languageList = list.gameObject;
            string[] names = { "English", "Tiếng Việt" };
            for (int i = 0; i < 2; i++)
                v.languageOptions[i] = UiKit.Button(list.transform, names[i], new Vector2(6f, 6f + (1 - i) * 66f), new Vector2(378f, 64f),
                    UiKit.Paper, 26, names[i]);
            v.languageList.SetActive(false);
        }

        static void BuildHelp(MenuView v, Transform layer)
        {
            var sheet = UiKit.Sheet(layer, "How To Play Page", new Vector2(1140f, 820f));
            v.helpPage = sheet.gameObject;
            Heading(sheet, "Title", 712f, 1140f, 52, "HƯỚNG DẪN", "HOW TO PLAY");
            v.helpTitle = UiKit.Label(sheet, "Page Title", new Vector2(0f, 640f), new Vector2(1140f, 56f), 38, TextAnchor.MiddleCenter,
                UiKit.Primary, bold: true);
            v.helpBody = UiKit.Label(sheet, "Page Text", new Vector2(80f, 160f), new Vector2(980f, 470f), 30, TextAnchor.UpperLeft, UiKit.InkText);
            v.helpBody.lineSpacing = 15f;
            v.helpCounter = UiKit.Label(sheet, "Page Number", new Vector2(470f, 62f), new Vector2(200f, 60f), 28, TextAnchor.MiddleCenter, UiKit.InkText);
            v.helpPrevButton = UiKit.Button(sheet, "Previous", new Vector2(60f, 45f), new Vector2(250f, 84f), UiKit.Secondary, 30, "◀ Trước", "◀ Back");
            v.helpNextButton = UiKit.Button(sheet, "Next", new Vector2(830f, 45f), new Vector2(250f, 84f), UiKit.Secondary, 30, "Sau ▶", "Next ▶");
            v.helpCloseButton = UiKit.Button(sheet, "Close", new Vector2(1050f, 728f), new Vector2(74f, 74f), UiKit.Muted, 30, "X");
        }

        static void BuildCodex(MenuView v, Transform layer)
        {
            var sheet = UiKit.Sheet(layer, "Handbook Page", new Vector2(1740f, 990f));
            v.codexPage = sheet.gameObject;
            Heading(sheet, "Title", 890f, 1740f, 52, "SỔ TAY QUÂN", "UNIT HANDBOOK");
            UiKit.Label(sheet, "Subtitle", new Vector2(0f, 860f), new Vector2(1740f, 34f), 22, TextAnchor.MiddleCenter, UiKit.Muted,
                "Cách vẽ từng loại quân. Trong trận không có hình mẫu, hãy nhớ kỹ!  (cuộn để xem thêm)",
                "How to draw every unit. There are no guides in battle, so remember them!  (scroll for more)");
            v.codexCloseButton = UiKit.Button(sheet, "Close", new Vector2(1652f, 898f), new Vector2(74f, 74f), UiKit.Muted, 30, "X");

            var view = UiKit.Rect(sheet, "Viewport", new Vector2(30f, 30f), new Vector2(1680f, 820f));
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.Rect(view, "Cards", Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = sheet.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;
            v.codexContent = content;
        }

        static void BuildResults(MenuView v, Transform layer)
        {
            var sheet = UiKit.Sheet(layer, "Results Page", new Vector2(940f, 800f));
            v.resultsPage = sheet.gameObject;
            v.resultTitle = UiKit.Label(sheet, "Result", new Vector2(0f, 650f), new Vector2(940f, 130f), 96, TextAnchor.MiddleCenter,
                UiKit.Primary, bold: true);
            UiKit.Outlined(v.resultTitle);
            v.resultRating = UiKit.Label(sheet, "Stars", new Vector2(0f, 572f), new Vector2(940f, 84f), 70, TextAnchor.MiddleCenter,
                new Color(1f, 0.75f, 0.1f), bold: true);
            UiKit.Outlined(v.resultRating);
            v.resultBody = UiKit.Label(sheet, "Record", new Vector2(130f, 175f), new Vector2(680f, 380f), 32, TextAnchor.MiddleLeft, UiKit.InkText);
            v.resultBody.lineSpacing = 20f;
            v.resultAgainButton = UiKit.Button(sheet, "Play Again", new Vector2(100f, 45f), new Vector2(350f, 100f), UiKit.Primary, 36, "Chơi lại", "Play Again");
            v.resultHomeButton = UiKit.Button(sheet, "Main Menu", new Vector2(490f, 45f), new Vector2(350f, 100f), UiKit.Secondary, 32, "Màn hình chính", "Main Menu");
        }
    }
}
