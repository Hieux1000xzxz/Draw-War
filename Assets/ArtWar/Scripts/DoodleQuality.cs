using System.Collections.Generic;
using UnityEngine;

namespace ArtWar
{
    /// <summary>
    /// Judges how well a soldier is drawn by laying it over the neat drawing of its kind: how much of the
    /// reference the strokes cover, and how many strokes stay on it. A better drawing fields more soldiers;
    /// big units (robots, dragons, tanks, planes...) are always one.
    /// </summary>
    public static class DoodleQuality
    {
        public const int MaxStars = 3;
        const float Spacing = 0.015f;   // sample spacing, in drawings scaled to unit size
        const float Tolerance = 0.035f; // how close a stroke must pass to count as "on" the reference

        /// <summary>Kinds that come in numbers: the better the drawing, the more of them.</summary>
        public static bool Multiplies(UnitKind k) =>
            k == UnitKind.Infantry || k == UnitKind.Rifleman || k == UnitKind.Archer ||
            k == UnitKind.Shieldman || k == UnitKind.Healer || k == UnitKind.Grenadier || k == UnitKind.Bat;

        /// <summary>0..1: 1 = traced the neat drawing exactly.</summary>
        public static float Score(Doodle d, UnitStats s)
        {
            if (d.strokes.Count == 0) return 0f;
            var drawn = Samples(d);
            var reference = Samples(DoodleTemplates.Reference(s.kind));
            if (drawn.Count == 0 || reference.Count == 0) return 0f;

            float coverage = Fraction(reference, drawn);
            float precision = Fraction(drawn, reference);
            // both must be good: tracing only half the soldier, or scribbling all over it, both score low
            return coverage + precision < 1e-4f ? 0f : Mathf.Clamp01(2f * coverage * precision / (coverage + precision));
        }

        public static int Stars(float score)
        {
            if (score >= 0.8f) return 3;
            if (score >= 0.5f) return 2;
            return 1;
        }

        /// <summary>How many soldiers one drawing becomes. Ready-made drawings ("Vẽ hộ") earn no bonus.</summary>
        public static int Copies(Doodle d, UnitStats s, float score)
        {
            if (!Multiplies(s.kind) || d.readyMade) return 1;
            int stars = Stars(score);
            return s.kind == UnitKind.Healer ? Mathf.Min(stars, 2) : stars;
        }

        public static string StarText(int stars) =>
            new string('★', stars) + new string('☆', MaxStars - stars);

        /// <summary>Points along every stroke, with the drawing centered and scaled so its longer side is 1.</summary>
        static List<Vector2> Samples(Doodle d)
        {
            var list = new List<Vector2>();
            Rect b = d.Bounds;
            float size = Mathf.Max(b.width, b.height, 0.01f);
            foreach (var stroke in d.strokes)
            {
                var pts = stroke.points;
                if (pts.Count == 0) continue;
                Vector2 prev = (pts[0] - b.center) / size;
                list.Add(prev);
                float carry = 0f;
                for (int i = 1; i < pts.Count; i++)
                {
                    Vector2 next = (pts[i] - b.center) / size;
                    float seg = Vector2.Distance(prev, next);
                    float t = Spacing - carry;
                    while (t <= seg)
                    {
                        list.Add(Vector2.Lerp(prev, next, t / seg));
                        t += Spacing;
                    }
                    carry = seg - (t - Spacing);
                    prev = next;
                }
            }
            return list;
        }

        /// <summary>Share of the points in <paramref name="from"/> that have a point of <paramref name="to"/> nearby.</summary>
        static float Fraction(List<Vector2> from, List<Vector2> to)
        {
            float tol2 = Tolerance * Tolerance;
            int hit = 0;
            foreach (var p in from)
            {
                foreach (var q in to)
                {
                    if ((p - q).sqrMagnitude <= tol2) { hit++; break; }
                }
            }
            return (float)hit / from.Count;
        }
    }
}
