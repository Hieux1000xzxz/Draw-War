using System.Collections.Generic;
using UnityEngine;

namespace ArtWar
{
    /// <summary>
    /// Cleans up a hand-drawn stroke so it looks the way the player meant it:
    /// wobbly circles become clean circles/ellipses, boxy loops become crisp polygons (or exact rectangles),
    /// near-straight lines snap straight (and to horizontal/vertical), and the rest is split at sharp
    /// corners into straight pieces and smoothed curves.
    /// </summary>
    public static class StrokeBeautifier
    {
        const float Spacing = 0.01f;
        const float CornerAngle = 50f;
        const int CornerWindow = 3;
        const float AxisSnapDegrees = 8f;

        public static List<Vector2> Beautify(List<Vector2> raw)
        {
            if (raw.Count < 3) return raw;

            var pts = Resample(raw, Spacing);
            float len = Length(pts);
            if (pts.Count < 4 || len < 0.03f) return raw;

            Vector2 a = pts[0], b = pts[pts.Count - 1];
            float chord = Vector2.Distance(a, b);
            bool closed = len > 0.12f && chord < Mathf.Max(0.05f, len * 0.15f);

            if (!closed)
            {
                if (chord / len > 0.92f) return StraightLine(a, b);
                return CleanOpen(pts);
            }

            pts[pts.Count - 1] = pts[0];
            // A good round fit wins outright (hand jitter can fake corners on a circle).
            if (IsEllipse(pts, 0.08f, out Vector2 c, out Vector2 r))
                return Ellipse(c, r, pts[0]);
            var corners = FindCorners(pts, true);
            if (corners.Count >= 3 && corners.Count <= 6)
                return Polygon(pts, corners);
            if (corners.Count < 3 && IsEllipse(pts, 0.13f, out c, out r))
                return Ellipse(c, r, pts[0]);
            return Chaikin(pts, 2, true);
        }

        // ------------------------------------------------------------ open strokes

        static List<Vector2> StraightLine(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(angle / 90f) * 90f;
            if (Mathf.Abs(Mathf.DeltaAngle(angle, snapped)) < AxisSnapDegrees)
            {
                Vector2 mid = (a + b) * 0.5f;
                float half = d.magnitude * 0.5f;
                Vector2 dir = new Vector2(Mathf.Cos(snapped * Mathf.Deg2Rad), Mathf.Sin(snapped * Mathf.Deg2Rad));
                a = mid - dir * half;
                b = mid + dir * half;
            }
            return Segment(a, b);
        }

        static List<Vector2> CleanOpen(List<Vector2> pts)
        {
            var corners = FindCorners(pts, false);
            var cuts = new List<int> { 0 };
            cuts.AddRange(corners);
            cuts.Add(pts.Count - 1);

            var result = new List<Vector2> { pts[0] };
            for (int i = 1; i < cuts.Count; i++)
            {
                var piece = pts.GetRange(cuts[i - 1], cuts[i] - cuts[i - 1] + 1);
                List<Vector2> clean;
                float pl = Length(piece);
                if (piece.Count < 3 || pl < 1e-4f || Vector2.Distance(piece[0], piece[piece.Count - 1]) / pl > 0.95f)
                    clean = Segment(piece[0], piece[piece.Count - 1]);
                else
                    clean = Chaikin(piece, 2, false);
                for (int k = 1; k < clean.Count; k++) result.Add(clean[k]);
            }
            return result;
        }

        // ------------------------------------------------------------ closed strokes

        static List<Vector2> Polygon(List<Vector2> pts, List<int> corners)
        {
            var verts = new List<Vector2>();
            foreach (int i in corners) verts.Add(pts[i]);

            // Four corners with near axis-aligned edges: make it an exact rectangle.
            if (verts.Count == 4 && AllEdgesNearAxis(verts))
            {
                Vector2 min = verts[0], max = verts[0];
                foreach (var v in verts)
                {
                    min = Vector2.Min(min, v);
                    max = Vector2.Max(max, v);
                }
                verts = new List<Vector2>
                {
                    new Vector2(min.x, min.y), new Vector2(max.x, min.y),
                    new Vector2(max.x, max.y), new Vector2(min.x, max.y),
                };
            }

            var result = new List<Vector2>();
            for (int i = 0; i < verts.Count; i++)
            {
                var seg = Segment(verts[i], verts[(i + 1) % verts.Count]);
                if (result.Count > 0) seg.RemoveAt(0);
                result.AddRange(seg);
            }
            result.Add(result[0]);
            return result;
        }

        static bool AllEdgesNearAxis(List<Vector2> verts)
        {
            for (int i = 0; i < verts.Count; i++)
            {
                Vector2 d = verts[(i + 1) % verts.Count] - verts[i];
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Round(angle / 90f) * 90f)) > 15f) return false;
            }
            return true;
        }

        static bool IsEllipse(List<Vector2> pts, float tolerance, out Vector2 center, out Vector2 radii)
        {
            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            center = (min + max) * 0.5f;
            radii = Vector2.Max((max - min) * 0.5f, new Vector2(0.005f, 0.005f));

            float sum = 0f, sumSq = 0f;
            foreach (var p in pts)
            {
                Vector2 q = p - center;
                float r = Mathf.Sqrt(q.x * q.x / (radii.x * radii.x) + q.y * q.y / (radii.y * radii.y));
                sum += r;
                sumSq += r * r;
            }
            float mean = sum / pts.Count;
            float std = Mathf.Sqrt(Mathf.Max(0f, sumSq / pts.Count - mean * mean));
            if (std / mean > tolerance) return false;

            float ratio = radii.x / radii.y;
            if (ratio > 0.85f && ratio < 1.18f) radii = Vector2.one * ((radii.x + radii.y) * 0.5f);
            return true;
        }

        static List<Vector2> Ellipse(Vector2 c, Vector2 r, Vector2 startNear)
        {
            Vector2 s = startNear - c;
            float start = Mathf.Atan2(s.y / r.y, s.x / r.x);
            int segments = Mathf.Clamp(Mathf.CeilToInt((r.x + r.y) * Mathf.PI / Spacing), 16, 80);
            var result = new List<Vector2>();
            for (int i = 0; i <= segments; i++)
            {
                float a = start + i * Mathf.PI * 2f / segments;
                result.Add(c + new Vector2(Mathf.Cos(a) * r.x, Mathf.Sin(a) * r.y));
            }
            return result;
        }

        // ------------------------------------------------------------ helpers

        /// <summary>Indices where the stroke turns sharply. For closed loops the start point is also allowed to be a corner.</summary>
        static List<int> FindCorners(List<Vector2> raw, bool closed)
        {
            int n = raw.Count;
            // Measure turning on a lightly smoothed copy so hand jitter doesn't read as corners.
            var pts = new List<Vector2>(raw);
            for (int i = 1; i < n - 1; i++) pts[i] = (raw[i - 1] + raw[i] * 2f + raw[i + 1]) * 0.25f;
            var angles = new float[n];
            for (int i = 0; i < n; i++)
            {
                int prev = i - CornerWindow, next = i + CornerWindow;
                if (closed)
                {
                    // the last point duplicates the first, so wrap over n - 1 unique points
                    int m = n - 1;
                    prev = ((prev % m) + m) % m;
                    next = next % m;
                }
                else if (prev < 0 || next >= n) continue;
                angles[i] = Vector2.Angle(pts[i] - pts[prev], pts[next] - pts[i]);
            }

            int limit = closed ? n - 1 : n;
            var corners = new List<int>();
            for (int i = 0; i < limit; i++)
            {
                if (angles[i] < CornerAngle) continue;
                bool isPeak = true;
                for (int k = -CornerWindow; k <= CornerWindow && isPeak; k++)
                {
                    int j = closed ? (((i + k) % limit) + limit) % limit : i + k;
                    if (j < 0 || j >= limit || j == i) continue;
                    if (angles[j] > angles[i] || (angles[j] == angles[i] && j < i)) isPeak = false;
                }
                if (isPeak) corners.Add(i);
            }
            return corners;
        }

        static List<Vector2> Chaikin(List<Vector2> pts, int iterations, bool closed)
        {
            var cur = pts;
            for (int it = 0; it < iterations; it++)
            {
                var next = new List<Vector2>();
                if (!closed) next.Add(cur[0]);
                int segs = cur.Count - 1;
                for (int i = 0; i < segs; i++)
                {
                    Vector2 p = cur[i], q = cur[i + 1];
                    next.Add(Vector2.Lerp(p, q, 0.25f));
                    next.Add(Vector2.Lerp(p, q, 0.75f));
                }
                if (closed) next.Add(next[0]);
                else next.Add(cur[cur.Count - 1]);
                cur = next;
            }
            return cur;
        }

        static List<Vector2> Segment(Vector2 a, Vector2 b)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Spacing));
            var result = new List<Vector2>();
            for (int i = 0; i <= steps; i++) result.Add(Vector2.Lerp(a, b, (float)i / steps));
            return result;
        }

        static float Length(List<Vector2> pts)
        {
            float l = 0f;
            for (int i = 1; i < pts.Count; i++) l += Vector2.Distance(pts[i - 1], pts[i]);
            return l;
        }

        static List<Vector2> Resample(List<Vector2> src, float spacing)
        {
            var result = new List<Vector2> { src[0] };
            float carried = 0f;
            for (int i = 1; i < src.Count; i++)
            {
                Vector2 a = src[i - 1], b = src[i];
                float seg = Vector2.Distance(a, b);
                if (seg < 1e-6f) continue;
                float t = spacing - carried;
                while (t <= seg)
                {
                    result.Add(Vector2.Lerp(a, b, t / seg));
                    t += spacing;
                }
                carried = seg - (t - spacing);
            }
            if (Vector2.Distance(result[result.Count - 1], src[src.Count - 1]) > spacing * 0.3f)
                result.Add(src[src.Count - 1]);
            return result;
        }
    }
}
