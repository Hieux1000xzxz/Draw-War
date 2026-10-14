using UnityEngine;

namespace ArtWar
{
    /// <summary>The hand-drawn pieces every button, panel and bar is cut from.</summary>
    public enum SkinPart { Button, Panel, Sheet, Bar, Knob }

    /// <summary>
    /// Paints the hand-drawn UI skin: rounded shapes with a wobbly pencil outline gone over twice, a waxy crayon fill
    /// and, for buttons and sheets, a little drop shadow. Fills are white so an Image's colour tints them while the
    /// dark outline stays dark. Every piece except the knob is a 9-slice sprite, so it stretches to any size.
    /// The editor saves these as PNGs (Draw War ▸ UI); at runtime they're loaded from Resources, or painted on the
    /// spot when missing.
    /// </summary>
    public static class DoodleSkin
    {
        static readonly Color Outline = new Color(0.16f, 0.13f, 0.11f);
        static readonly Color SheetPaper = new Color(0.99f, 0.96f, 0.88f);
        static readonly Color SheetEdge = new Color(0.33f, 0.24f, 0.16f);

        struct Spec
        {
            public int w, h;
            public float margin, radius, outline;
            public Vector2 shadow;
            public float shadowAlpha;
            public int border;
            public bool sheet;
        }

        static Spec SpecFor(SkinPart part)
        {
            switch (part)
            {
                case SkinPart.Button: return new Spec { w = 192, h = 112, margin = 9, radius = 26, outline = 4.5f, shadow = new Vector2(4, -5), shadowAlpha = 0.28f, border = 40 };
                case SkinPart.Sheet: return new Spec { w = 256, h = 256, margin = 12, radius = 14, outline = 5.5f, shadow = new Vector2(6, -8), shadowAlpha = 0.25f, border = 76, sheet = true };
                case SkinPart.Bar: return new Spec { w = 96, h = 48, margin = 4, radius = 15, outline = 3.5f, border = 20 };
                case SkinPart.Knob: return new Spec { w = 64, h = 64, margin = 6, radius = 26, outline = 4f, shadow = new Vector2(2, -3), shadowAlpha = 0.25f, border = 0 };
                default: return new Spec { w = 192, h = 192, margin = 6, radius = 22, outline = 4f, border = 36 };
            }
        }

        /// <summary>The 9-slice border (left, bottom, right, top) in pixels.</summary>
        public static Vector4 Border(SkinPart part)
        {
            int b = SpecFor(part).border;
            return new Vector4(b, b, b, b);
        }

        public static Sprite MakeSprite(SkinPart part)
        {
            var tex = Paint(part);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, Border(part));
        }

        public static Texture2D Paint(SkinPart part)
        {
            var s = SpecFor(part);
            var px = new Color[s.w * s.h];
            var rng = new System.Random((int)part * 31 + 7);
            float ph1 = (float)rng.NextDouble() * 6f, ph2 = (float)rng.NextDouble() * 6f, ph3 = (float)rng.NextDouble() * 6f;

            Vector2 centre = new Vector2(s.w * 0.5f, s.h * 0.5f);
            Vector2 half = new Vector2(s.w * 0.5f - s.margin, s.h * 0.5f - s.margin);

            // signed distance to a rounded box, roughened like a hand-drawn line
            float Shape(Vector2 p, float phase, float amount)
            {
                Vector2 q = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half + new Vector2(s.radius, s.radius);
                float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - s.radius;
                float wobble = Mathf.Sin(p.x * 0.07f + phase) * 0.9f + Mathf.Sin(p.y * 0.11f + phase * 1.7f) * 0.7f;
                return d + wobble * amount;
            }

            for (int y = 0; y < s.h; y++)
            {
                for (int x = 0; x < s.w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    Color c = new Color(0f, 0f, 0f, 0f);

                    // drop shadow
                    if (s.shadowAlpha > 0f)
                    {
                        float ds = Shape(p - s.shadow, ph1, 1f);
                        c = Over(c, new Color(0f, 0f, 0f, Mathf.Clamp01(0.5f - ds) * s.shadowAlpha));
                    }

                    // crayon fill: white with faint diagonal wax strokes (they show through whatever tint is applied)
                    float d = Shape(p, ph1, 1f);
                    float inside = Mathf.Clamp01(0.5f - d);
                    if (inside > 0f)
                    {
                        float streak = Mathf.PerlinNoise((x + y) * 0.09f, (x - y) * 0.015f);
                        float shade = 1f - streak * 0.09f - (rng.NextDouble() < 0.02 ? 0.04f : 0f);
                        Color fill = s.sheet ? SheetPaper * shade : new Color(shade, shade, shade);
                        fill.a = inside;
                        c = Over(c, fill);
                    }

                    // the pencil outline, gone over twice a little apart
                    Color edge = s.sheet ? SheetEdge : Outline;
                    float o1 = Mathf.Clamp01(s.outline * 0.5f + 0.5f - Mathf.Abs(d + s.outline * 0.35f));
                    float d2 = Shape(p + new Vector2(0.8f, -0.6f), ph2, 1.4f);
                    float o2 = Mathf.Clamp01(s.outline * 0.3f + 0.5f - Mathf.Abs(d2 + s.outline * 0.3f)) * 0.55f;
                    float o = Mathf.Max(o1, o2) * (0.88f + 0.12f * Mathf.PerlinNoise(x * 0.3f + ph3, y * 0.3f));
                    if (o > 0f) c = Over(c, new Color(edge.r, edge.g, edge.b, o));

                    px[y * s.w + x] = c;
                }
            }

            if (s.sheet) Tape(px, s.w, s.h);

            var tex = new Texture2D(s.w, s.h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Doodle " + part,
            };
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Two strips of masking tape holding the sheet to the desk, across the top corners.</summary>
        static void Tape(Color[] px, int w, int h)
        {
            var tape = new Color(0.96f, 0.9f, 0.62f, 0.78f);
            void Strip(Vector2 c, float angle)
            {
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), n = new Vector2(-dir.y, dir.x);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - c;
                        float along = Vector2.Dot(d, dir), across = Vector2.Dot(d, n);
                        // torn, zig-zag ends
                        float end = 30f + Mathf.Abs(Mathf.Repeat(across * 0.6f, 4f) - 2f);
                        if (Mathf.Abs(along) > end || Mathf.Abs(across) > 11f) continue;
                        float a = tape.a * (0.85f + 0.15f * Mathf.PerlinNoise(x * 0.2f, y * 0.2f));
                        px[y * w + x] = Over(px[y * w + x], new Color(tape.r, tape.g, tape.b, a));
                    }
            }
            Strip(new Vector2(30f, h - 28f), Mathf.PI * 0.25f);
            Strip(new Vector2(w - 30f, h - 28f), -Mathf.PI * 0.25f);
        }

        static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return new Color(0f, 0f, 0f, 0f);
            Color o = (src * src.a + dst * dst.a * (1f - src.a)) / a;
            o.a = a;
            return o;
        }

        /// <summary>One 64×64 square of exercise-book paper (faint blue grid, a lighter line through the middle).</summary>
        public static Texture2D GridTile()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "Grid Paper",
            };
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
    }
}
