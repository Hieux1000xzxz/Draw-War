using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArtWar
{
    /// <summary>
    /// The sheet of paper the player draws on. Works with mouse, touch and pen through Pointer.current.
    /// Every bit of line costs ink. Finished strokes are cleaned up (assist) and highlighted by what
    /// the game recognized them as.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class DrawingPad : MonoBehaviour
    {
        const int TexSize = 512;
        const int BrushRadius = 6;
        const int HaloRadius = 13;
        const float MinPointSpacing = 0.008f;
        const float DotCost = 0.5f;

        public Func<float, bool> TryConsumeInk;
        public Action<float> RefundInk;
        public event Action Changed;

        public Doodle CurrentDoodle { get; private set; } = new Doodle();
        public float InkUsed { get; private set; }

        /// <summary>Clean up strokes when they are finished (perfect circles, straight lines, crisp corners).</summary>
        public bool Assist { get; set; } = true;

        /// <summary>When locked the pen does nothing (e.g. during the battle).</summary>
        public bool Locked { get; set; }

        public InkColor InkColor
        {
            get => CurrentDoodle.color;
            set
            {
                CurrentDoodle.color = value;
                Redraw();
                Changed?.Invoke();
            }
        }

        /// <summary>The body (model) the player chose to draw (null = free drawing); shown faintly to trace over.</summary>
        public Model? Target { get; private set; }

        RectTransform rect;
        Texture2D tex;
        Color32[] paper;
        Color32[] background; // paper plus the faint guide drawing
        Color32[] pixels;
        Stroke current;
        Vector2 lastPoint, smoothPoint, rawPoint;
        bool dirty;
        readonly List<float> strokeCosts = new List<float>();

        static Color32 PartColor(PartType p)
        {
            switch (p)
            {
                case PartType.Leg: return new Color32(150, 225, 150, 255);
                case PartType.Weapon: return new Color32(250, 160, 150, 255);
                case PartType.Shield: return new Color32(165, 200, 250, 255);
                case PartType.Wheel: return new Color32(250, 215, 95, 255);
                case PartType.Wing: return new Color32(140, 230, 235, 255);
                case PartType.Bow: return new Color32(250, 185, 100, 255);
                case PartType.Rotor: return new Color32(205, 160, 245, 255);
                case PartType.Propeller: return new Color32(245, 160, 220, 255);
                case PartType.Cross: return new Color32(120, 235, 190, 255);
                case PartType.Gun: return new Color32(255, 200, 120, 255);
                case PartType.Box: return new Color32(185, 205, 235, 255);
                case PartType.Guard: return new Color32(140, 185, 250, 255);
                case PartType.Bomb: return new Color32(250, 140, 110, 255);
                case PartType.Hat: return new Color32(200, 150, 245, 255);
                case PartType.Coil: return new Color32(190, 225, 110, 255);
                default: return new Color32(0, 0, 0, 0);
            }
        }

        void Awake()
        {
            rect = (RectTransform)transform;
            tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            paper = new Color32[TexSize * TexSize];
            var bg = new Color32(250, 247, 236, 255);
            var grid = new Color32(206, 222, 240, 255);
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                    paper[y * TexSize + x] = (x % 32 == 0 || y % 32 == 0) ? grid : bg;
            background = (Color32[])paper.Clone();
            pixels = (Color32[])paper.Clone();
            dirty = true;
            GetComponent<RawImage>().texture = tex;
        }

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null && !Locked)
            {
                Vector2 screen = pointer.position.ReadValue();
                bool pressed = pointer.press.isPressed;

                if (pointer.press.wasPressedThisFrame && TryGetCanvasPoint(screen, out var c))
                    BeginStroke(c);
                else if (current != null && pressed)
                {
                    TryGetCanvasPoint(screen, out c);
                    rawPoint = new Vector2(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y)); // keep drawing along the edge
                    // Light low-pass filter takes the shake out of mouse and finger input.
                    smoothPoint = Vector2.Lerp(smoothPoint, rawPoint, Assist ? 0.5f : 1f);
                    ExtendStroke(smoothPoint);
                }

                if (!pressed && current != null) EndStroke();
            }

            if (dirty)
            {
                tex.SetPixels32(pixels);
                tex.Apply(false);
                dirty = false;
            }
        }

        bool TryGetCanvasPoint(Vector2 screen, out Vector2 c)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out var local);
            Rect r = rect.rect;
            c = new Vector2((local.x - r.x) / r.width, (local.y - r.y) / r.height);
            return c.x >= 0f && c.x <= 1f && c.y >= 0f && c.y <= 1f;
        }

        void BeginStroke(Vector2 c)
        {
            if (TryConsumeInk == null || !TryConsumeInk(DotCost)) return;
            InkUsed += DotCost;
            current = new Stroke();
            current.points.Add(c);
            CurrentDoodle.strokes.Add(current);
            strokeCosts.Add(DotCost);
            lastPoint = smoothPoint = rawPoint = c;
            PaintDot(c, InkPalette.Get(CurrentDoodle.color), BrushRadius);
            Changed?.Invoke();
        }

        void ExtendStroke(Vector2 c)
        {
            float d = Vector2.Distance(lastPoint, c);
            if (d < MinPointSpacing) return;
            float cost = d * DoodleAnalyzer.InkPerUnit;
            if (!TryConsumeInk(cost)) return; // out of ink: the pen runs dry
            InkUsed += cost;
            strokeCosts[strokeCosts.Count - 1] += cost;
            current.points.Add(c);
            PaintLine(lastPoint, c, InkPalette.Get(CurrentDoodle.color), BrushRadius);
            lastPoint = c;
            Sfx.Scratch();
            Changed?.Invoke();
        }

        void EndStroke()
        {
            var stroke = current;
            current = null;
            // The smoothed pen trails the finger a little; finish exactly where it was lifted.
            if (Vector2.Distance(lastPoint, rawPoint) > MinPointSpacing) stroke.points.Add(rawPoint);

            if (Assist && stroke.points.Count >= 3)
            {
                var clean = StrokeBeautifier.Beautify(stroke.points);
                stroke.points.Clear();
                stroke.points.AddRange(clean);
            }
            Redraw();
            Changed?.Invoke();
        }

        public void Undo()
        {
            if (CurrentDoodle.strokes.Count == 0) return;
            int last = CurrentDoodle.strokes.Count - 1;
            if (current == CurrentDoodle.strokes[last]) current = null;
            RefundInk?.Invoke(strokeCosts[last]);
            InkUsed -= strokeCosts[last];
            CurrentDoodle.strokes.RemoveAt(last);
            strokeCosts.RemoveAt(last);
            Redraw();
            Changed?.Invoke();
        }

        /// <summary>Wipes the page. With refund, the ink spent on it goes back to the pot.</summary>
        public void Clear(bool refund)
        {
            if (refund && InkUsed > 0f) RefundInk?.Invoke(InkUsed);
            InkUsed = 0f;
            CurrentDoodle = new Doodle { color = CurrentDoodle.color, intent = Target };
            strokeCosts.Clear();
            current = null;
            Array.Copy(background, pixels, background.Length);
            dirty = true;
            Changed?.Invoke();
        }

        /// <summary>Replaces the page with a ready-made drawing, paying its ink. False if there isn't enough ink.</summary>
        public bool Load(Doodle sample)
        {
            Clear(true);
            sample = FitToPad(sample);
            float cost = sample.TotalLength * DoodleAnalyzer.InkPerUnit;
            if (!TryConsumeInk(cost)) return false;
            sample.color = CurrentDoodle.color;
            sample.intent = Target ?? sample.intent;
            sample.readyMade = true;
            CurrentDoodle = sample;
            foreach (var s in sample.strokes) strokeCosts.Add(s.Length * DoodleAnalyzer.InkPerUnit);
            InkUsed = cost;
            Redraw();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Chooses what the player wants to draw: the kind's sample appears as a faint dashed guide to trace over,
        /// and the drawing is tagged with that intent. Null goes back to free drawing.
        /// </summary>
        public void SetTarget(Model? kind, Doodle guide)
        {
            Target = kind;
            CurrentDoodle.intent = kind;
            Array.Copy(paper, background, paper.Length);
            if (guide != null)
            {
                var ghost = new Color32(170, 182, 210, 255);
                foreach (var s in FitToPad(guide).strokes)
                {
                    // dashes: paint a short run, skip a short run, all along the stroke
                    float run = 0f;
                    for (int i = 1; i < s.points.Count; i++)
                    {
                        Vector2 a = s.points[i - 1], b = s.points[i];
                        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * TexSize / 1.5f));
                        for (int k = 1; k <= steps; k++)
                        {
                            Vector2 p = Vector2.Lerp(a, b, (float)k / steps);
                            run += Vector2.Distance(a, b) / steps;
                            if (run % 0.03f < 0.018f) PaintDotOn(background, p, ghost, 3);
                        }
                    }
                }
            }
            Redraw();
            Changed?.Invoke();
        }

        /// <summary>Scales and moves a drawing so it sits nicely on the page (centered, feet near the bottom).</summary>
        public static Doodle FitToPad(Doodle d)
        {
            Rect b = d.Bounds;
            // shrink only what would spill off the page; never enlarge (size changes a unit's stats and ink cost)
            float scale = Mathf.Min(0.92f / Mathf.Max(b.width, 0.01f), 0.92f / Mathf.Max(b.height, 0.01f), 1f);
            Vector2 offset = new Vector2(0.5f, 0.07f) - new Vector2(b.center.x, b.yMin) * scale;
            var fitted = new Doodle { color = d.color, intent = d.intent };
            foreach (var s in d.strokes)
            {
                var copy = new Stroke();
                foreach (var p in s.points) copy.points.Add(p * scale + offset);
                fitted.strokes.Add(copy);
            }
            return fitted;
        }

        void PaintDotOn(Color32[] target, Vector2 c, Color32 color, int radius)
        {
            int cx = Mathf.RoundToInt(c.x * (TexSize - 1)), cy = Mathf.RoundToInt(c.y * (TexSize - 1));
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    int px = cx + x, py = cy + y;
                    if (px < 0 || py < 0 || px >= TexSize || py >= TexSize || x * x + y * y > radius * radius) continue;
                    target[py * TexSize + px] = color;
                }
        }

        /// <summary>Hands over the finished drawing and gives the player a fresh page.</summary>
        public Doodle TakeDoodle()
        {
            var d = CurrentDoodle;
            Clear(false);
            return d;
        }

        void Redraw()
        {
            Array.Copy(background, pixels, background.Length);
            var parts = DoodleAnalyzer.ClassifyParts(CurrentDoodle);
            for (int i = 0; i < CurrentDoodle.strokes.Count; i++)
            {
                if (parts[i] != PartType.None && CurrentDoodle.strokes[i] != current)
                    PaintStroke(CurrentDoodle.strokes[i], PartColor(parts[i]), HaloRadius);
            }
            Color32 ink = InkPalette.Get(CurrentDoodle.color);
            foreach (var s in CurrentDoodle.strokes) PaintStroke(s, ink, BrushRadius);
            dirty = true;
        }

        void PaintStroke(Stroke s, Color32 color, int radius)
        {
            if (s.points.Count > 0) PaintDot(s.points[0], color, radius);
            for (int i = 1; i < s.points.Count; i++) PaintLine(s.points[i - 1], s.points[i], color, radius);
        }

        void PaintLine(Vector2 a, Vector2 b, Color32 color, int radius)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * TexSize / 1.5f);
            for (int i = 1; i <= steps; i++) PaintDot(Vector2.Lerp(a, b, (float)i / steps), color, radius);
        }

        /// <summary>Round brush with a soft, anti-aliased rim.</summary>
        void PaintDot(Vector2 c, Color32 color, int radius)
        {
            float fx = c.x * (TexSize - 1), fy = c.y * (TexSize - 1);
            int cx = Mathf.RoundToInt(fx), cy = Mathf.RoundToInt(fy);
            int reach = radius + 1;
            for (int y = -reach; y <= reach; y++)
            {
                int py = cy + y;
                if (py < 0 || py >= TexSize) continue;
                for (int x = -reach; x <= reach; x++)
                {
                    int px = cx + x;
                    if (px < 0 || px >= TexSize) continue;
                    float dx = px - fx, dy = py - fy;
                    float coverage = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    if (coverage <= 0f) continue;
                    int i = py * TexSize + px;
                    pixels[i] = coverage >= 1f ? color : Color32.Lerp(pixels[i], color, coverage * 0.5f);
                }
            }
            dirty = true;
        }
    }
}
