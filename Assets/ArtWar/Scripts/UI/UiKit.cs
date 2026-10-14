using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArtWar
{
    /// <summary>
    /// The building blocks of the game's UI, in the hand-drawn style: panels, buttons, bars and paper sheets cut from
    /// the doodle skin, and labels in the hand-written font. The views (HUD, menus, handbook card) are laid out with
    /// these both when the editor saves them as prefabs and when the game has to build them itself.
    /// Positions are in reference pixels (1920×1080, scaled by screen height), from the parent's bottom-left corner.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Paper = new Color(0.98f, 0.95f, 0.87f);
        public static readonly Color PaperEdge = new Color(0.24f, 0.17f, 0.12f);
        public static readonly Color InkText = new Color(0.16f, 0.13f, 0.22f);
        public static readonly Color Primary = new Color(0.95f, 0.6f, 0.15f);
        public static readonly Color Secondary = new Color(0.3f, 0.45f, 0.75f);
        public static readonly Color Muted = new Color(0.55f, 0.5f, 0.45f);
        public static readonly Color HudPanel = new Color(0.12f, 0.12f, 0.18f, 0.6f);

        public const string FontPath = "Fonts/Pangolin SDF";
        public const string SymbolsFontPath = "Fonts/Noto Symbols SDF";
        public const string ShadowMaterialPath = "Fonts/Pangolin SDF Shadow";
        public const string OutlineMaterialPath = "Fonts/Pangolin SDF Outline";

        static TMP_FontAsset font;
        static Material shadowMaterial, outlineMaterial;
        static readonly Sprite[] sprites = new Sprite[5];
        static Texture gridPaper;

        /// <summary>
        /// The TextMeshPro font: Pangolin, a round hand-written face with full Vietnamese accents, falling back to
        /// Noto Sans Symbols 2 (★ ☆ ▾ ◀ ▶ ✎) and Liberation Sans (→). Both Pangolin and Noto are under the SIL Open
        /// Font License. Saved as an asset by the editor (Draw War ▸ UI); built on the fly when missing.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (font == null) font = Resources.Load<TMP_FontAsset>(FontPath);
                if (font == null) font = CreateFontAssets();
                return font;
            }
        }

        /// <summary>Creates the dynamic font assets (Pangolin with its symbol fallbacks) from the font files.</summary>
        public static TMP_FontAsset CreateFontAssets()
        {
            var pangolin = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/Pangolin-Regular"), 90, 9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            pangolin.name = "Pangolin SDF";
            var symbols = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/NotoSansSymbols2-Regular"), 90, 9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, true);
            symbols.name = "Noto Symbols SDF";
            pangolin.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { symbols };
            var liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (liberation != null) pangolin.fallbackFontAssetTable.Add(liberation);
            return pangolin;
        }

        /// <summary>The font material with a soft drop shadow under the letters (button labels, numbers on bars).</summary>
        public static Material ShadowMaterial
        {
            get
            {
                if (shadowMaterial == null) shadowMaterial = Resources.Load<Material>(ShadowMaterialPath);
                if (shadowMaterial == null) shadowMaterial = MakeShadowMaterial(Font.material);
                return shadowMaterial;
            }
        }

        /// <summary>The font material with a thick dark outline (logo, big result titles).</summary>
        public static Material OutlineMaterial
        {
            get
            {
                if (outlineMaterial == null) outlineMaterial = Resources.Load<Material>(OutlineMaterialPath);
                if (outlineMaterial == null) outlineMaterial = MakeOutlineMaterial(Font.material);
                return outlineMaterial;
            }
        }

        public static Material MakeShadowMaterial(Material baseMaterial)
        {
            var m = new Material(baseMaterial) { name = "Pangolin SDF Shadow" };
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.45f));
            m.SetFloat("_UnderlayOffsetX", 0.55f);
            m.SetFloat("_UnderlayOffsetY", -0.55f);
            m.SetFloat("_UnderlaySoftness", 0.25f);
            return m;
        }

        public static Material MakeOutlineMaterial(Material baseMaterial)
        {
            var m = new Material(baseMaterial) { name = "Pangolin SDF Outline" };
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.22f);
            m.SetColor("_OutlineColor", PaperEdge);
            m.SetFloat("_FaceDilate", 0.12f);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0.24f, 0.17f, 0.12f, 0.9f));
            m.SetFloat("_UnderlayOffsetX", 0.7f);
            m.SetFloat("_UnderlayOffsetY", -0.7f);
            m.SetFloat("_UnderlayDilate", 0.3f);
            return m;
        }

        public static string SkinName(SkinPart part) => "doodle_" + part.ToString().ToLowerInvariant();

        /// <summary>Forget loaded skin pieces and fonts (after the editor has saved new ones).</summary>
        public static void ClearCache()
        {
            System.Array.Clear(sprites, 0, sprites.Length);
            gridPaper = null;
            font = null;
            shadowMaterial = outlineMaterial = null;
        }

        public static Sprite Sprite(SkinPart part)
        {
            int i = (int)part;
            if (sprites[i] == null) sprites[i] = Resources.Load<Sprite>("UI/Skin/" + SkinName(part));
            if (sprites[i] == null) sprites[i] = DoodleSkin.MakeSprite(part);
            return sprites[i];
        }

        public static Texture GridPaper
        {
            get
            {
                if (gridPaper == null) gridPaper = Resources.Load<Texture2D>("UI/Skin/grid_paper");
                if (gridPaper == null) gridPaper = DoodleSkin.GridTile();
                return gridPaper;
            }
        }

        // ---------------------------------------------------------------- layout

        public static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Center(RectTransform rt, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
        }

        public static void TopCenter(RectTransform rt, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = offset;
        }

        public static void TopRight(RectTransform rt, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = offset;
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>A hand-drawn box tinted with <paramref name="color"/> (the fill takes the colour, the outline stays dark).</summary>
        public static Image Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color,
            SkinPart skin = SkinPart.Panel)
        {
            var rt = Rect(parent, name, pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Sprite(skin);
            image.type = skin == SkinPart.Knob ? Image.Type.Simple : Image.Type.Sliced;
            image.color = color;
            return image;
        }

        /// <summary>A sheet of paper taped to the desk, centred on its parent. Pages of the menus are drawn on these.</summary>
        public static RectTransform Sheet(Transform parent, string name, Vector2 size)
        {
            var image = Panel(parent, name, Vector2.zero, size, Color.white, SkinPart.Sheet);
            Center(image.rectTransform, Vector2.zero);
            return image.rectTransform;
        }

        /// <summary>
        /// A TextMeshPro label in the hand-written font. With <paramref name="vi"/> given it is a fixed text in both
        /// languages (a <see cref="LocalizedText"/>, editable in the Inspector); without, the game fills it in.
        /// </summary>
        public static TextMeshProUGUI Label(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, TextAnchor align,
            Color color, string vi = null, string en = null, bool bold = false)
        {
            var rt = Rect(parent, name, pos, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            t.fontSize = fontSize;
            t.alignment = Alignment(align);
            t.color = color;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            if (bold) t.fontStyle = FontStyles.Bold;
            if (vi != null) rt.gameObject.AddComponent<LocalizedText>().Set(vi, en ?? vi);
            return t;
        }

        /// <summary>Shrinks the text (down to <paramref name="min"/>) instead of letting it spill out of its box.</summary>
        public static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        /// <summary>Soft drop shadow under the letters.</summary>
        public static void Shadowed(TMP_Text t) => t.fontSharedMaterial = ShadowMaterial;

        /// <summary>Thick dark outline round the letters, for big titles.</summary>
        public static void Outlined(TMP_Text t) => t.fontSharedMaterial = OutlineMaterial;

        static TextAlignmentOptions Alignment(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        /// <summary>A crayon button with a centred label (fixed text in both languages, or filled in by the game when null).</summary>
        public static Button Button(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int fontSize,
            string vi = null, string en = null)
        {
            var image = Panel(parent, name, pos, size, color, SkinPart.Button);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            button.colors = colors;
            // the label sits a touch up-left of centre so it lands on the face, not the shadow
            var label = Label(image.transform, "Label", Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white,
                vi, en, true);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(8f, 8f);
            label.rectTransform.offsetMax = new Vector2(-12f, -4f);
            Shadowed(label);
            // long words shrink to fit rather than breaking onto a second line
            AutoSize(label, Mathf.Min(12, fontSize), fontSize);
            return button;
        }

        public static TMP_Text ButtonLabel(Button b) => b.GetComponentInChildren<TMP_Text>(true);

        /// <summary>A bar (health, ink) of the given colour inside a darker track; returns the fill to resize.</summary>
        public static RectTransform Bar(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor,
            out Image track)
        {
            track = Panel(parent, name, pos, size, new Color(0.12f, 0.1f, 0.1f, 0.85f), SkinPart.Bar);
            var fill = Panel(track.transform, "Fill", Vector2.zero, Vector2.zero, fillColor, SkinPart.Bar);
            Stretch(fill.rectTransform, 3f);
            return fill.rectTransform;
        }

        /// <summary>A horizontal slider: a dark crayon track, an orange fill and a round knob.</summary>
        public static Slider Slider(Transform parent, string name, Vector2 pos, float width)
        {
            var root = Rect(parent, name, pos, new Vector2(width, 44f));
            var slider = root.gameObject.AddComponent<Slider>();

            var track = Panel(root, "Track", Vector2.zero, Vector2.zero, new Color(0.35f, 0.3f, 0.26f), SkinPart.Bar);
            track.rectTransform.anchorMin = new Vector2(0f, 0.25f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.75f);
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;

            var fillArea = Rect(root, "Fill Area", Vector2.zero, Vector2.zero);
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            var fill = Panel(fillArea, "Fill", Vector2.zero, Vector2.zero, Primary, SkinPart.Bar);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = Rect(root, "Handle Area", Vector2.zero, Vector2.zero);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(20f, 0f);
            handleArea.offsetMax = new Vector2(-20f, 0f);
            var handle = Panel(handleArea, "Knob", Vector2.zero, Vector2.zero, Color.white, SkinPart.Knob);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handle.rectTransform.offsetMin = new Vector2(-24f, -6f);
            handle.rectTransform.offsetMax = new Vector2(24f, 6f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        /// <summary>A canvas filling the screen, scaled by screen height from 1920×1080.</summary>
        public static GameObject Canvas(string name, int order)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }
    }
}
