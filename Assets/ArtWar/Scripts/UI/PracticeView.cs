using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArtWar
{
    /// <summary>
    /// The drawing practice room: a list of every unit on the left, a big page in the middle with the chosen unit
    /// shown faintly to trace over, and live marks on the right (what the game reads it as, how well it is drawn).
    /// No ink, no battle. Saved as Resources/UI/Practice.prefab; the game fills in the list and the marks.
    /// </summary>
    public class PracticeView : MonoBehaviour
    {
        [Header("Layout")]
        public RectTransform page;

        [Header("Unit list")]
        public RectTransform listContent;

        [Header("Drawing page")]
        public TMP_Text targetTitle;
        public RectTransform pad;

        [Header("Marks")]
        public RawImage targetPicture;
        public TMP_Text howText, checklistText, readText, verdictText, starsText, tipText;
        public Button guideButton, undoButton, clearButton, prevButton, nextButton, closeButton;
        public TMP_Text guideLabel;

        public const float EntryHeight = 92f;

        public static PracticeView Build()
        {
            var go = UiKit.Canvas("Practice", 12);
            var v = go.AddComponent<PracticeView>();

            var backdrop = UiKit.Rect(go.transform, "Backdrop", Vector2.zero, Vector2.zero);
            UiKit.Stretch(backdrop);
            var paper = backdrop.gameObject.AddComponent<RawImage>();
            paper.texture = UiKit.GridPaper;
            paper.uvRect = new Rect(0f, 0f, 1920f / 64f, 1080f / 64f);

            // everything sits in a 1920×1080 frame in the middle of the screen
            v.page = UiKit.Rect(go.transform, "Practice Page", Vector2.zero, new Vector2(1920f, 1080f));
            UiKit.Center(v.page, Vector2.zero);
            var page = v.page;

            // left: the units to practise
            var list = UiKit.Sheet(page, "Unit List", new Vector2(400f, 1000f));
            list.anchorMin = list.anchorMax = list.pivot = Vector2.zero;
            list.anchoredPosition = new Vector2(30f, 40f);
            UiKit.Label(list, "Title", new Vector2(0f, 930f), new Vector2(400f, 56f), 34, TextAnchor.MiddleCenter, UiKit.InkText,
                "CHỌN QUÂN", "PICK A UNIT", true);
            var view = UiKit.Rect(list, "Viewport", new Vector2(16f, 20f), new Vector2(368f, 900f));
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);
            view.gameObject.AddComponent<RectMask2D>();
            v.listContent = UiKit.Rect(view, "Units", Vector2.zero, Vector2.zero);
            v.listContent.anchorMin = new Vector2(0f, 1f);
            v.listContent.anchorMax = new Vector2(1f, 1f);
            v.listContent.pivot = new Vector2(0.5f, 1f);
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = v.listContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;

            // middle: the page to draw on
            var frame = UiKit.Panel(page, "Page Frame", new Vector2(460f, 40f), new Vector2(860f, 1000f), new Color(0.45f, 0.32f, 0.22f));
            v.targetTitle = UiKit.Label(frame.transform, "Target", new Vector2(20f, 900f), new Vector2(820f, 80f), 40,
                TextAnchor.MiddleCenter, Color.white, bold: true);
            UiKit.AutoSize(v.targetTitle, 20, 40);
            UiKit.Shadowed(v.targetTitle);
            v.pad = UiKit.Rect(frame.transform, "Drawing Pad", new Vector2(20f, 60f), new Vector2(820f, 820f));
            v.pad.gameObject.AddComponent<RawImage>();
            UiKit.Label(frame.transform, "Hint", new Vector2(20f, 14f), new Vector2(820f, 40f), 20, TextAnchor.MiddleCenter,
                new Color(1f, 0.95f, 0.85f), "Đồ theo nét mờ. Không tốn mực, vẽ bao nhiêu cũng được!",
                "Trace the faint lines. No ink here, draw as much as you like!");

            // right: the marks and tools
            var marks = UiKit.Sheet(page, "Marks", new Vector2(540f, 1000f));
            marks.anchorMin = marks.anchorMax = marks.pivot = Vector2.zero;
            marks.anchoredPosition = new Vector2(1350f, 40f);
            v.closeButton = UiKit.Button(marks, "Close", new Vector2(456f, 920f), new Vector2(70f, 70f), UiKit.Muted, 30, "X");
            v.targetPicture = UiKit.Rect(marks, "Target Picture", new Vector2(170f, 740f), new Vector2(200f, 200f)).gameObject.AddComponent<RawImage>();
            v.howText = UiKit.Label(marks, "How To Draw", new Vector2(30f, 640f), new Vector2(480f, 96f), 22, TextAnchor.UpperCenter,
                new Color(0.75f, 0.35f, 0.1f));
            v.checklistText = UiKit.Label(marks, "Checklist", new Vector2(24f, 566f), new Vector2(492f, 76f), 22, TextAnchor.MiddleCenter, UiKit.InkText);
            UiKit.AutoSize(v.checklistText, 14, 22);
            v.readText = UiKit.Label(marks, "Read As", new Vector2(30f, 512f), new Vector2(480f, 52f), 22, TextAnchor.MiddleCenter, UiKit.InkText);
            v.verdictText = UiKit.Label(marks, "Verdict", new Vector2(30f, 440f), new Vector2(480f, 70f), 42, TextAnchor.MiddleCenter,
                UiKit.InkText, bold: true);
            UiKit.AutoSize(v.verdictText, 22, 44);
            v.starsText = UiKit.Label(marks, "Stars", new Vector2(30f, 360f), new Vector2(480f, 80f), 60, TextAnchor.MiddleCenter,
                new Color(1f, 0.75f, 0.1f), bold: true);
            UiKit.Outlined(v.starsText);
            v.tipText = UiKit.Label(marks, "Tip", new Vector2(30f, 276f), new Vector2(480f, 80f), 20, TextAnchor.UpperCenter, UiKit.InkText);

            v.guideButton = UiKit.Button(marks, "Guide", new Vector2(40f, 196f), new Vector2(460f, 72f), UiKit.Secondary, 24);
            v.guideLabel = UiKit.ButtonLabel(v.guideButton);
            v.undoButton = UiKit.Button(marks, "Undo", new Vector2(40f, 112f), new Vector2(222f, 72f), new Color(0.6f, 0.56f, 0.52f), 26,
                "Hoàn tác", "Undo");
            v.clearButton = UiKit.Button(marks, "Clear", new Vector2(278f, 112f), new Vector2(222f, 72f), new Color(0.7f, 0.64f, 0.58f), 26,
                "Xóa", "Clear");
            v.prevButton = UiKit.Button(marks, "Previous", new Vector2(40f, 28f), new Vector2(222f, 72f), UiKit.Primary, 26,
                "◀ Quân trước", "◀ Previous");
            v.nextButton = UiKit.Button(marks, "Next", new Vector2(278f, 28f), new Vector2(222f, 72f), UiKit.Primary, 26,
                "Quân sau ▶", "Next ▶");
            return v;
        }
    }
}
