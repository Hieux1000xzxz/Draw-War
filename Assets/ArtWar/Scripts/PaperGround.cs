using System.Collections.Generic;
using UnityEngine;

namespace ArtWar
{
    /// <summary>
    /// Paints the battlefield: a page of an exercise book lying in the grass. Paper fibres and yellowed edges, ruled
    /// lines and red margins, a faint blue crayon wash on our half and red on theirs, a pencilled lane with a dashed
    /// centre circle, and the margins full of a kid's doodles (stars, spirals, hearts, smileys, arrows...), a couple
    /// of ink blots, a coffee ring, and a pencil and eraser left lying on the page.
    /// </summary>
    public static class PaperGround
    {
        const int PxPerUnit = 72;

        static Color32[] px;
        static int w, h;
        static float worldW, worldD;
        static System.Random rng;

        /// <param name="width">world size of the paper along X</param>
        /// <param name="depth">world size along Z</param>
        public static Texture2D Paint(float width, float depth, float fieldHalfX, float fieldHalfZ)
        {
            worldW = width;
            worldD = depth;
            w = Mathf.Min(2048, Mathf.CeilToInt(width * PxPerUnit));
            h = Mathf.Min(1024, Mathf.CeilToInt(depth * PxPerUnit));
            px = new Color32[w * h];
            rng = new System.Random(11);

            Paper();
            float laneZ = fieldHalfZ + 0.55f, laneX = fieldHalfX + 0.4f;
            TeamWashes(laneX, laneZ);
            RuledLines();
            Margins(fieldHalfX + 1f);
            Lane(laneX, laneZ);
            Doodles(laneX, laneZ);
            Stains();
            PencilAndEraser(laneZ);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            tex.SetPixels32(px);
            tex.Apply(true);
            px = null;
            return tex;
        }

        // ---------------------------------------------------------------- coordinates

        /// <summary>World X/Z on the page to a pixel (the plane's U runs along -X, V along -Z).</summary>
        static Vector2 P(float x, float z) => new Vector2((0.5f - x / worldW) * w, (0.5f - z / worldD) * h);
        static float Px(float units) => units * PxPerUnit;
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ---------------------------------------------------------------- the paper

        static void Paper()
        {
            float edge = Px(0.7f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fibre = Mathf.PerlinNoise(x * 0.35f, y * 0.05f) * 6f + Mathf.PerlinNoise(x * 0.02f, y * 0.02f) * 8f;
                    float speck = rng.NextDouble() < 0.002 ? -14f : 0f;
                    // yellowed, slightly darker towards the edges of the page
                    float d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    float age = Mathf.Clamp01(1f - d / edge);
                    age = age * age;
                    float r = 246f + fibre * 0.5f - age * 22f + speck;
                    float g = 241f + fibre * 0.5f - age * 30f + speck;
                    float b = 226f + fibre * 0.4f - age * 48f + speck;
                    px[y * w + x] = new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(b, 0, 255), 255);
                }
            }
        }

        static void RuledLines()
        {
            var rule = new Color(0.62f, 0.75f, 0.9f, 0.7f);
            for (float z = -worldD * 0.5f + 0.5f; z < worldD * 0.5f; z += 0.5f)
            {
                int y = Mathf.RoundToInt(P(0f, z).y);
                for (int x = 0; x < w; x++)
                {
                    Blend(x, y, rule);
                    Blend(x, y + 1, new Color(rule.r, rule.g, rule.b, rule.a * 0.4f));
                }
            }
        }

        static void Margins(float x)
        {
            var red = new Color(0.88f, 0.45f, 0.45f, 0.8f);
            foreach (float side in new[] { -1f, 1f })
                foreach (float off in new[] { 0f, 0.08f })
                {
                    int col = Mathf.RoundToInt(P(side * (x + off), 0f).x);
                    for (int y = 0; y < h; y++) { Blend(col, y, red); Blend(col + 1, y, red); }
                }
        }

        /// <summary>Crayon wash on each half of the lane: blue for us (left), red for them, strongest by each castle.</summary>
        static void TeamWashes(float laneX, float laneZ)
        {
            Vector2 a = P(-laneX, laneZ), b = P(laneX, -laneZ);
            int x0 = (int)Mathf.Min(a.x, b.x), x1 = (int)Mathf.Max(a.x, b.x);
            int y0 = (int)Mathf.Min(a.y, b.y), y1 = (int)Mathf.Max(a.y, b.y);
            var blue = new Color(0.35f, 0.6f, 1f);
            var red = new Color(1f, 0.4f, 0.4f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float t = (x - x0) / (float)(x1 - x0);              // 0 = enemy side (+X), 1 = our side (-X)
                    float side = Mathf.Abs(t - 0.5f) * 2f;              // 0 in the middle, 1 at the castles
                    // crayon streaks: diagonal waxy strokes with gaps
                    float streak = 0.55f + 0.45f * Mathf.PerlinNoise((x + y) * 0.08f, (x - y) * 0.012f);
                    float a2 = (0.05f + Mathf.SmoothStep(0f, 1f, side) * 0.17f) * streak;
                    Blend(x, y, t > 0.5f ? new Color(blue.r, blue.g, blue.b, a2) : new Color(red.r, red.g, red.b, a2));
                }
        }

        /// <summary>A pencilled lane outline (gone over twice, like a kid would), a dashed centre line and circle.</summary>
        static void Lane(float laneX, float laneZ)
        {
            var pencil = new Color(0.32f, 0.32f, 0.38f, 0.55f);
            for (int pass = 0; pass < 2; pass++)
            {
                float jitter = pass * 0.04f;
                foreach (float z in new[] { -laneZ, laneZ })
                    WobblyLine(new Vector2(-laneX, z + jitter), new Vector2(laneX, z - jitter), pencil, 1.6f, 0.03f);
            }

            var dash = new Color(0.3f, 0.3f, 0.36f, 0.45f);
            for (float z = -laneZ; z < laneZ; z += 0.42f)
                WobblyLine(new Vector2(0f, z), new Vector2(0.02f, Mathf.Min(z + 0.24f, laneZ)), dash, 1.6f, 0.01f);
            Circle(Vector2.zero, 0.95f, dash, 1.6f, 0.03f);
            Star(Vector2.zero, 0.32f, new Color(1f, 0.75f, 0.2f, 0.75f), 2f);
        }

        // ---------------------------------------------------------------- doodles in the margins

        static readonly Color[] Crayons =
        {
            new Color(0.25f, 0.45f, 0.9f, 0.7f), new Color(0.9f, 0.3f, 0.3f, 0.7f), new Color(0.25f, 0.65f, 0.3f, 0.7f),
            new Color(0.95f, 0.6f, 0.15f, 0.75f), new Color(0.6f, 0.35f, 0.8f, 0.7f), new Color(0.3f, 0.3f, 0.36f, 0.6f),
        };

        static void Doodles(float laneX, float laneZ)
        {
            var placed = new List<Vector2>();
            float zNear0 = -worldD * 0.5f + 0.9f, zNear1 = -laneZ - 0.6f;
            float zFar0 = laneZ + 0.6f, zFar1 = worldD * 0.5f - 0.9f;
            int tries = 0;
            while (placed.Count < 30 && tries++ < 600)
            {
                bool near = placed.Count % 3 != 0; // the near margin is the one the camera sees best
                var c = new Vector2(R(-laneX + 0.5f, laneX - 0.5f), near ? R(zNear0, zNear1) : R(zFar0, zFar1));
                if (near && c.x < 1.5f && c.x > -7.5f && c.y < -3.6f) continue; // leave room for the pencil
                bool clear = true;
                foreach (var p in placed) if (Vector2.Distance(p, c) < 1.7f) { clear = false; break; }
                if (!clear) continue;
                placed.Add(c);

                var col = Crayons[rng.Next(Crayons.Length)];
                float s = R(0.42f, 0.62f);
                switch (rng.Next(9))
                {
                    case 0: Star(c, s, col, 2.2f); break;
                    case 1: Spiral(c, s, col); break;
                    case 2: Heart(c, s, col); break;
                    case 3: Smiley(c, s, col); break;
                    case 4: Arrow(c, s, col); break;
                    case 5: Sun(c, s * 0.7f, col); break;
                    case 6: Flower(c, s, col); break;
                    case 7: Bolt(c, s, col); break;
                    default: Swirl(c, s, col); break;
                }
            }
        }

        static void Star(Vector2 c, float r, Color col, float width)
        {
            var pts = new List<Vector2>();
            float rot = R(0f, 1f);
            for (int i = 0; i <= 10; i++)
            {
                float a = rot + i * Mathf.PI / 5f + Mathf.PI / 2f;
                float rr = i % 2 == 0 ? r : r * 0.45f;
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
            }
            Polyline(pts, col, width);
        }

        static void Spiral(Vector2 c, float r, Color col)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                float a = t * Mathf.PI * 6f;
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * t);
            }
            Polyline(pts, col, 1.8f);
        }

        static void Heart(Vector2 c, float r, Color col)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f * Mathf.PI * 2f;
                float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                // the page is seen from the camera's side (-Z), so "up" on the drawing is +Z
                pts.Add(c + new Vector2(-x, y) * (r / 17f));
            }
            Polyline(pts, col, 2.2f);
        }

        static void Smiley(Vector2 c, float r, Color col)
        {
            Circle(c, r, col, 2f, 0.02f);
            Dot(P(c.x - r * 0.35f, c.y + r * 0.3f), Px(0.05f), col);
            Dot(P(c.x + r * 0.35f, c.y + r * 0.3f), Px(0.05f), col);
            var mouth = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.Lerp(200f, 340f, i / 12f) * Mathf.Deg2Rad;
                mouth.Add(c + new Vector2(-Mathf.Cos(a), Mathf.Sin(a)) * r * 0.55f);
            }
            Polyline(mouth, col, 2f);
        }

        static void Arrow(Vector2 c, float r, Color col)
        {
            // pointing at the enemy castle, of course
            Vector2 a = c + new Vector2(-r, 0f), b = c + new Vector2(r, 0f);
            WobblyLine(a, b, col, 2.2f, 0.02f);
            WobblyLine(b, b + new Vector2(-r * 0.4f, r * 0.3f), col, 2.2f, 0f);
            WobblyLine(b, b + new Vector2(-r * 0.4f, -r * 0.3f), col, 2.2f, 0f);
        }

        static void Sun(Vector2 c, float r, Color col)
        {
            Circle(c, r, col, 2f, 0.02f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                WobblyLine(c + d * r * 1.3f, c + d * r * 1.8f, col, 2f, 0f);
            }
        }

        static void Flower(Vector2 c, float r, Color col)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Circle(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.45f, r * 0.3f, col, 1.8f, 0.01f);
            }
            Dot(P(c.x, c.y), Px(r * 0.18f), new Color(0.95f, 0.75f, 0.2f, 0.8f));
        }

        static void Bolt(Vector2 c, float r, Color col)
        {
            Polyline(new List<Vector2>
            {
                c + new Vector2(0.15f, 1f) * r, c + new Vector2(-0.25f, 0.05f) * r, c + new Vector2(0.15f, 0.05f) * r,
                c + new Vector2(-0.2f, -1f) * r,
            }, col, 2.4f);
        }

        static void Swirl(Vector2 c, float r, Color col)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 50; i++)
            {
                float t = i / 50f;
                pts.Add(c + new Vector2(Mathf.Lerp(-r, r, t), Mathf.Sin(t * Mathf.PI * 3f) * r * 0.35f));
            }
            Polyline(pts, col, 2f);
        }

        // ---------------------------------------------------------------- stains and props

        static void Stains()
        {
            // ink blots with splatter
            var ink = new Color(0.12f, 0.14f, 0.3f, 0.75f);
            foreach (var c in new[] { new Vector2(9.4f, -4.3f), new Vector2(-10.2f, 4.2f), new Vector2(4.6f, 3.9f) })
            {
                float r = R(0.18f, 0.28f);
                for (int i = 0; i < 7; i++)
                {
                    var off = new Vector2(R(-1f, 1f), R(-1f, 1f)) * r * 0.5f;
                    Dot(P(c.x + off.x, c.y + off.y), Px(r * R(0.5f, 0.9f)), ink);
                }
                for (int i = 0; i < 9; i++)
                {
                    float a = R(0f, Mathf.PI * 2f), d = R(r * 1.4f, r * 3f);
                    Dot(P(c.x + Mathf.Cos(a) * d, c.y + Mathf.Sin(a) * d), Px(R(0.02f, 0.06f)), ink);
                }
            }

            // a coffee ring from someone's mug
            var coffee = new Color(0.55f, 0.35f, 0.15f, 0.16f);
            Circle(new Vector2(-9.3f, -4.1f), 0.55f, coffee, 7f, 0.01f);
            Circle(new Vector2(-9.25f, -4.12f), 0.5f, new Color(coffee.r, coffee.g, coffee.b, 0.1f), 3f, 0.01f);
        }

        /// <summary>A yellow pencil and a pink eraser left lying in the front margin.</summary>
        static void PencilAndEraser(float laneZ)
        {
            float z = -laneZ - 1.25f;
            Vector2 a = new Vector2(-7f, z - 0.25f), b = new Vector2(1f, z + 0.15f); // eraser end → tip
            Vector2 dir = (b - a).normalized, n = new Vector2(-dir.y, dir.x);
            float r = 0.17f;
            var outline = new Color(0.25f, 0.2f, 0.15f, 0.9f);

            Vector2 Q(float t, float s) => a + dir * t + n * s;
            float len = Vector2.Distance(a, b);

            // soft shadow under the pencil
            FillPolygon(new[] { Q(0.2f, -r - 0.12f), Q(len - 0.4f, -r - 0.12f), Q(len - 0.4f, r - 0.1f), Q(0.2f, r - 0.1f) },
                new Color(0.3f, 0.25f, 0.2f, 0.12f));

            FillPolygon(new[] { Q(0f, -r), Q(0.45f, -r), Q(0.45f, r), Q(0f, r) }, new Color(0.96f, 0.6f, 0.68f, 1f));       // eraser
            FillPolygon(new[] { Q(0.45f, -r), Q(0.8f, -r), Q(0.8f, r), Q(0.45f, r) }, new Color(0.75f, 0.75f, 0.78f, 1f));   // ferrule
            FillPolygon(new[] { Q(0.8f, -r), Q(len - 1.1f, -r), Q(len - 1.1f, r), Q(0.8f, r) }, new Color(1f, 0.82f, 0.2f, 1f)); // body
            FillPolygon(new[] { Q(len - 1.1f, -r), Q(len, 0f), Q(len - 1.1f, r) }, new Color(0.93f, 0.78f, 0.58f, 1f));     // wood
            FillPolygon(new[] { Q(len - 0.33f, -r * 0.3f), Q(len, 0f), Q(len - 0.33f, r * 0.3f) }, new Color(0.25f, 0.25f, 0.28f, 1f)); // lead

            // facets, ferrule rings and outlines
            WobblyLine(Q(0.8f, r * 0.33f), Q(len - 1.1f, r * 0.33f), new Color(0.9f, 0.6f, 0.1f, 0.8f), 1.5f, 0f);
            WobblyLine(Q(0.8f, -r * 0.33f), Q(len - 1.1f, -r * 0.33f), new Color(1f, 0.95f, 0.6f, 0.9f), 1.5f, 0f);
            foreach (float t in new[] { 0.55f, 0.62f, 0.69f })
                WobblyLine(Q(t, -r), Q(t, r), new Color(0.5f, 0.5f, 0.55f, 0.8f), 1.2f, 0f);
            Polyline(new List<Vector2> { Q(0f, -r), Q(len - 1.1f, -r), Q(len, 0f), Q(len - 1.1f, r), Q(0f, r), Q(0f, -r) }, outline, 2f);
            WobblyLine(Q(len - 1.1f, -r), Q(len - 1.1f, r), outline, 1.5f, 0f);
            WobblyLine(Q(0.45f, -r), Q(0.45f, r), outline, 1.5f, 0f);
            WobblyLine(Q(0.8f, -r), Q(0.8f, r), outline, 1.5f, 0f);

            // the eraser that has rubbed out a few mistakes (crumbs beside it)
            Vector2 e = new Vector2(3.2f, z + 0.1f);
            var corners = new[] { e + new Vector2(-0.45f, -0.22f), e + new Vector2(0.45f, -0.22f), e + new Vector2(0.45f, 0.22f), e + new Vector2(-0.45f, 0.22f) };
            FillPolygon(new[] { corners[0] + new Vector2(0.08f, -0.1f), corners[1] + new Vector2(0.08f, -0.1f), corners[2] + new Vector2(0.08f, -0.1f), corners[3] + new Vector2(0.08f, -0.1f) },
                new Color(0.3f, 0.25f, 0.2f, 0.12f));
            FillPolygon(corners, new Color(0.98f, 0.66f, 0.74f, 1f));
            FillPolygon(new[] { corners[0], e + new Vector2(-0.05f, -0.22f), e + new Vector2(-0.05f, 0.22f), corners[3] },
                new Color(0.4f, 0.55f, 0.85f, 1f)); // blue half of the eraser
            Polyline(new List<Vector2>(corners) { corners[0] }, outline, 2f);
            for (int i = 0; i < 6; i++)
                Dot(P(e.x + R(0.6f, 1.1f), e.y + R(-0.3f, 0.3f)), Px(R(0.02f, 0.04f)), new Color(0.95f, 0.62f, 0.7f, 0.9f));
        }

        // ---------------------------------------------------------------- raster helpers (world coordinates in)

        static void Polyline(List<Vector2> pts, Color col, float width)
        {
            for (int i = 1; i < pts.Count; i++) Segment(P(pts[i - 1].x, pts[i - 1].y), P(pts[i].x, pts[i].y), col, width);
        }

        /// <summary>A hand-drawn line: it bows slightly and wobbles along its length.</summary>
        static void WobblyLine(Vector2 a, Vector2 b, Color col, float width, float wobble)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.15f));
            Vector2 n = Vector2.Perpendicular((b - a).normalized);
            float phase = R(0f, 6f), bow = R(-1f, 1f) * wobble;
            var pts = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float off = Mathf.Sin(t * Mathf.PI) * bow + Mathf.Sin(t * 9f + phase) * wobble * 0.4f;
                pts.Add(Vector2.Lerp(a, b, t) + n * off);
            }
            Polyline(pts, col, width);
        }

        static void Circle(Vector2 c, float r, Color col, float width, float wobble)
        {
            var pts = new List<Vector2>();
            float phase = R(0f, 6f);
            for (int i = 0; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2.08f; // overshoots a little, like a real pen circle
                float rr = r * (1f + Mathf.Sin(a * 3f + phase) * wobble);
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
            }
            Polyline(pts, col, width);
        }

        static void Segment(Vector2 a, Vector2 b, Color col, float width)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 1.5f));
            for (int i = 0; i <= steps; i++) Dot(Vector2.Lerp(a, b, i / (float)steps), width, col, 0.35f);
        }

        /// <summary>Soft round stamp of the given pixel radius. <paramref name="strength"/> scales each stamp so overlapping
        /// stamps along a line build up to roughly the colour's alpha.</summary>
        static void Dot(Vector2 c, float radius, Color col, float strength = 1f)
        {
            int x0 = Mathf.Max(0, (int)(c.x - radius - 1)), x1 = Mathf.Min(w - 1, (int)(c.x + radius + 1));
            int y0 = Mathf.Max(0, (int)(c.y - radius - 1)), y1 = Mathf.Min(h - 1, (int)(c.y + radius + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01(radius + 0.5f - d) * col.a * strength;
                    if (a > 0f) Blend(x, y, new Color(col.r, col.g, col.b, a));
                }
        }

        static void FillPolygon(Vector2[] world, Color col)
        {
            var poly = new Vector2[world.Length];
            for (int i = 0; i < world.Length; i++) poly[i] = P(world[i].x, world[i].y);
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in poly) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            var xs = new List<float>();
            for (int y = Mathf.Max(0, (int)minY); y <= Mathf.Min(h - 1, (int)maxY + 1); y++)
            {
                float yc = y + 0.5f;
                xs.Clear();
                for (int i = 0; i < poly.Length; i++)
                {
                    Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                    if ((a.y <= yc) == (b.y <= yc)) continue;
                    xs.Add(a.x + (yc - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(xs[k])); x <= Mathf.Min(w - 1, (int)xs[k + 1]); x++)
                        Blend(x, y, col);
            }
        }

        static void Blend(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || c.a <= 0f) return;
            int i = y * w + x;
            Color dst = px[i];
            px[i] = new Color(Mathf.Lerp(dst.r, c.r, c.a), Mathf.Lerp(dst.g, c.g, c.a), Mathf.Lerp(dst.b, c.b, c.a), 1f);
        }
    }
}
