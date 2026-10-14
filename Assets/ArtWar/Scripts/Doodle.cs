using System.Collections.Generic;
using UnityEngine;

namespace ArtWar
{
    public enum InkColor { Black, Red, Blue }

    public enum Team { Player, Enemy }

    /// <summary>One continuous pen stroke, in normalized canvas coordinates (0..1, y up).</summary>
    public class Stroke
    {
        public readonly List<Vector2> points = new List<Vector2>();

        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < points.Count; i++)
                    length += Vector2.Distance(points[i - 1], points[i]);
                return length;
            }
        }
    }

    /// <summary>A full drawing: every stroke plus the ink color it was drawn with.</summary>
    public class Doodle
    {
        public readonly List<Stroke> strokes = new List<Stroke>();
        public InkColor color = InkColor.Black;

        /// <summary>The body the player set out to draw (null = free drawing). Once its parts are there, the weapon decides the unit.</summary>
        public Model? intent;

        /// <summary>Placed from a sample ("Vẽ hộ") rather than drawn by hand: earns no extra soldiers.</summary>
        public bool readyMade;

        public float TotalLength
        {
            get
            {
                float length = 0f;
                foreach (var s in strokes) length += s.Length;
                return length;
            }
        }

        public Rect Bounds
        {
            get
            {
                bool any = false;
                Vector2 min = Vector2.zero, max = Vector2.zero;
                foreach (var s in strokes)
                {
                    foreach (var p in s.points)
                    {
                        if (!any) { min = max = p; any = true; continue; }
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
                }
                return any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : Rect.zero;
            }
        }
    }

    public static class InkPalette
    {
        public static Color Get(InkColor c)
        {
            switch (c)
            {
                case InkColor.Red: return new Color(0.85f, 0.16f, 0.14f);
                case InkColor.Blue: return new Color(0.16f, 0.36f, 0.88f);
                default: return new Color(0.1f, 0.1f, 0.14f);
            }
        }
    }
}
