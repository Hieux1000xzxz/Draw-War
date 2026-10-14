using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArtWar
{
    /// <summary>
    /// The in-battle screen: the drawing column on the left (recognition box, the page, ink bar, pen buttons), the
    /// banner over the battlefield (phase, castle health, army counts), the start, upgrade and pause buttons.
    /// Saved as Resources/UI/HUD.prefab: move, resize or restyle anything there; the game only looks things up
    /// through these fields.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        public const float ColumnLeft = 20f, ColumnWidth = 620f, BannerWidth = 900f, UiColumnWidth = 660f;

        [Header("Recognition box")]
        public TMP_Text kindText;
        public TMP_Text hintText;
        public TMP_Text statsText;

        [Header("Drawing page")]
        public RectTransform pad;
        public TMP_Text toastText;

        [Header("Ink")]
        public RectTransform inkFill;
        public TMP_Text inkText;

        [Header("Pen buttons")]
        public Button[] colorButtons = new Button[3];
        public Button undoButton;
        public Button clearButton;
        public Button assistButton;
        public TMP_Text assistLabel;
        public Button summonButton;

        [Header("Battle banner")]
        public TMP_Text phaseText;
        public TMP_Text countText;
        public RectTransform playerBarFill, enemyBarFill;
        public TMP_Text playerBarLabel, enemyBarLabel, playerBarTitle, enemyBarTitle;
        public Button startButton;

        [Header("Castle upgrade and pause")]
        public Button upgradeButton;
        public TMP_Text upgradeLabel;
        public Button pauseButton;

        /// <summary>Lays the HUD out from scratch (used by the editor to save the prefab, and as a fallback).</summary>
        public static HudView Build()
        {
            var go = UiKit.Canvas("HUD", 0);
            var v = go.AddComponent<HudView>();
            var root = go.transform;
            const float left = ColumnLeft, width = ColumnWidth;

            // recognition + stats box
            var box = UiKit.Panel(root, "Recognition Box", new Vector2(left, 785f), new Vector2(width, 280f), UiKit.HudPanel).transform;
            v.kindText = UiKit.Label(box, "Kind", new Vector2(12f, 212f), new Vector2(width - 24f, 56f), 40, TextAnchor.MiddleCenter,
                Color.white, bold: true);
            UiKit.AutoSize(v.kindText, 18, 40);
            v.hintText = UiKit.Label(box, "Hint", new Vector2(12f, 140f), new Vector2(width - 24f, 70f), 22, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.95f, 1f));
            UiKit.AutoSize(v.hintText, 13, 22);
            v.statsText = UiKit.Label(box, "Stats", new Vector2(12f, 10f), new Vector2(width - 24f, 126f), 21, TextAnchor.MiddleCenter,
                Color.white);

            // the page, in a wooden clipboard-ish frame
            var frame = UiKit.Panel(root, "Page Frame", new Vector2(left, 170f), new Vector2(width, 600f), new Color(0.45f, 0.32f, 0.22f)).transform;
            v.pad = UiKit.Rect(frame, "Drawing Pad", new Vector2(12f, 12f), new Vector2(width - 24f, 576f));
            v.pad.gameObject.AddComponent<RawImage>();
            UiKit.Label(frame, "Page Hint", new Vector2(18f, 556f), new Vector2(width - 36f, 30f), 18, TextAnchor.MiddleLeft,
                new Color(0.45f, 0.45f, 0.55f), "Mỗi lần chỉ vẽ MỘT lính, rồi bấm \"Hóa thật!\"", "Draw ONE unit at a time, then press \"Summon!\"");
            v.toastText = UiKit.Label(root, "Toast", new Vector2(left, 710f), new Vector2(width, 50f), 30, TextAnchor.MiddleCenter,
                new Color(1f, 0.35f, 0.25f), bold: true);

            // ink bar
            v.inkFill = UiKit.Bar(root, "Ink Bar", new Vector2(left, 118f), new Vector2(width, 44f), new Color(0.25f, 0.42f, 0.9f), out var inkTrack);
            v.inkText = UiKit.Label(inkTrack.transform, "Ink", Vector2.zero, new Vector2(width, 44f), 22, TextAnchor.MiddleCenter, Color.white, bold: true);
            UiKit.Shadowed(v.inkText);

            // pen buttons: ink colours, undo, clear, assist, summon
            float x = left;
            for (int i = 0; i < 3; i++)
            {
                v.colorButtons[i] = UiKit.Button(root, "Ink " + (InkColor)i, new Vector2(x, 22f), new Vector2(66f, 86f), InkPalette.Get((InkColor)i), 20, "");
                x += 70f;
            }
            v.undoButton = UiKit.Button(root, "Undo", new Vector2(x, 22f), new Vector2(92f, 86f), new Color(0.6f, 0.56f, 0.52f), 20, "Hoàn tác", "Undo");
            x += 96f;
            v.clearButton = UiKit.Button(root, "Clear", new Vector2(x, 22f), new Vector2(84f, 86f), new Color(0.7f, 0.64f, 0.58f), 24, "Xóa", "Clear");
            x += 88f;
            v.assistButton = UiKit.Button(root, "Assist", new Vector2(x, 22f), new Vector2(98f, 86f), new Color(0.35f, 0.65f, 0.58f), 19);
            v.assistLabel = UiKit.ButtonLabel(v.assistButton);
            x += 102f;
            v.summonButton = UiKit.Button(root, "Summon", new Vector2(x, 22f), new Vector2(left + width - x, 86f), UiKit.Primary, 30,
                "Hóa thật!", "Summon!");

            // banner over the battlefield
            var banner = UiKit.Panel(root, "Banner", Vector2.zero, new Vector2(BannerWidth, 150f), UiKit.HudPanel);
            UiKit.TopCenter(banner.rectTransform, new Vector2(UiColumnWidth * 0.5f, -12f));
            v.phaseText = UiKit.Label(banner.transform, "Phase", new Vector2(0f, 86f), new Vector2(BannerWidth, 58f), 44, TextAnchor.MiddleCenter,
                Color.white, bold: true);
            v.countText = UiKit.Label(banner.transform, "Armies", new Vector2(BannerWidth * 0.5f - 95f, 8f), new Vector2(190f, 76f), 22,
                TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.75f));
            (v.playerBarFill, v.playerBarLabel, v.playerBarTitle) = CastleBar(banner.transform, 18f, new Color(0.3f, 0.55f, 1f), false);
            (v.enemyBarFill, v.enemyBarLabel, v.enemyBarTitle) = CastleBar(banner.transform, BannerWidth - 348f, new Color(0.95f, 0.35f, 0.3f), true);

            v.startButton = UiKit.Button(root, "Start Battle", Vector2.zero, new Vector2(330f, 76f), new Color(0.88f, 0.28f, 0.22f), 30,
                "Bắt đầu chiến!", "Start battle!");
            UiKit.TopCenter((RectTransform)v.startButton.transform, new Vector2(UiColumnWidth * 0.5f, -170f));

            // castle upgrade, on the battlefield next to our castle
            v.upgradeButton = UiKit.Button(root, "Upgrade Castle", new Vector2(UiColumnWidth + 10f, 18f), new Vector2(440f, 92f), UiKit.Secondary, 21);
            v.upgradeLabel = UiKit.ButtonLabel(v.upgradeButton);

            v.pauseButton = UiKit.Button(root, "Pause", Vector2.zero, new Vector2(88f, 88f), new Color(0.3f, 0.3f, 0.38f), 34, "II");
            UiKit.TopRight((RectTransform)v.pauseButton.transform, new Vector2(-14f, -12f));
            return v;
        }

        static (RectTransform fill, TMP_Text label, TMP_Text title) CastleBar(Transform banner, float x, Color color, bool fromRight)
        {
            var title = UiKit.Label(banner, fromRight ? "Enemy Castle" : "Our Castle", new Vector2(x, 50f), new Vector2(330f, 32f), 22,
                fromRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Color.white, bold: true);
            var fill = UiKit.Bar(banner, (fromRight ? "Enemy" : "Our") + " Castle Bar", new Vector2(x, 12f), new Vector2(330f, 36f), color, out var track);
            var label = UiKit.Label(track.transform, "HP", Vector2.zero, new Vector2(330f, 36f), 20, TextAnchor.MiddleCenter, Color.white, bold: true);
            UiKit.Shadowed(label);
            return (fill, label, title);
        }
    }
}
