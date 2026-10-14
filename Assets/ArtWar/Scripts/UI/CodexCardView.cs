using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArtWar
{
    /// <summary>One card of the unit handbook: the unit's picture, name, how to draw it, what it does and its numbers.
    /// Saved as Resources/UI/CodexCard.prefab; the handbook makes one per unit.</summary>
    public class CodexCardView : MonoBehaviour
    {
        public const float Width = 548f, Height = 250f;

        public RawImage picture;
        public TMP_Text nameText, howText, hintText, statsText;

        public static CodexCardView Build(Transform parent = null)
        {
            var card = UiKit.Panel(parent, "Handbook Card", Vector2.zero, new Vector2(Width, Height), new Color(1f, 1f, 1f, 0.9f));
            var v = card.gameObject.AddComponent<CodexCardView>();
            var t = card.transform;

            v.picture = UiKit.Rect(t, "Picture", new Vector2(12f, 25f), new Vector2(200f, 200f)).gameObject.AddComponent<RawImage>();
            const float x = 218f, w = Width - x - 16f;
            v.nameText = UiKit.Label(t, "Name", new Vector2(x, 196f), new Vector2(w, 40f), 26, TextAnchor.MiddleLeft, UiKit.InkText, bold: true);
            UiKit.AutoSize(v.nameText, 16, 26);
            v.howText = UiKit.Label(t, "How To Draw", new Vector2(x, 126f), new Vector2(w, 72f), 19, TextAnchor.UpperLeft, new Color(0.75f, 0.35f, 0.1f));
            v.hintText = UiKit.Label(t, "What It Does", new Vector2(x, 60f), new Vector2(w, 64f), 17, TextAnchor.UpperLeft, UiKit.InkText);
            v.statsText = UiKit.Label(t, "Numbers", new Vector2(x, 12f), new Vector2(w, 46f), 18, TextAnchor.MiddleLeft, UiKit.Secondary, bold: true);
            return v;
        }
    }
}
