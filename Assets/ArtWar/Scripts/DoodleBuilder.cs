using System.Collections.Generic;
using UnityEngine;

namespace ArtWar
{
    /// <summary>One stroke of a built drawing: its own object, pivoted where that body part would bend or spin.</summary>
    public class BuiltPart
    {
        public GameObject go;
        public PartType type;
        public bool closed;
        public bool isHead;
        public float radius;   // half of the stroke's larger extent (wheels, rotors)
        public Vector3 restPosition;
        public Rect bounds;    // ink bounds in the drawing's space
        public Vector2 tip;    // far end of the stroke, relative to its pivot (muzzle of a gun)
        public bool attached;  // rides on another part (wing, leg, arm, barrel) instead of hanging off the root
        public bool held;      // attached to a hand but still animated (a sword chops, a cannon aims and recoils)
    }

    /// <summary>The result of building a drawing: the visual root, its size, its parts and its eyes.</summary>
    public class BuiltDoodle
    {
        public GameObject root;
        public Vector2 size;
        public bool mirrored;
        public readonly List<BuiltPart> parts = new List<BuiltPart>();
        public readonly List<Transform> eyes = new List<Transform>();
        public readonly List<Transform> pupils = new List<Transform>();
        public float eyeRadius;
    }

    /// <summary>
    /// Builds the "pen drawing come alive" look of the magic-pen episode: every stroke becomes a thin, slightly
    /// wobbly colored pen line traced twice like a quick sketch, and closed shapes get a see-through crayon fill
    /// with hatching. Each stroke is pivoted at its natural joint (top of a leg, root of a wing, hub of a wheel...)
    /// so it can be animated, and the drawing gets a pair of little pen eyes.
    /// </summary>
    public static class DoodleBuilder
    {
        public const float WorldPerCanvas = 3f;
        const float LineHalfWidth = 0.026f;
        const float SketchHalfWidth = 0.011f;
        const float Lift = 0.03f;            // keeps the lowest line just above the ground
        const float Wobble = 0.014f;         // hand-drawn waviness of the pen line
        const float FillAlpha = 0.7f;
        const float ResampleSpacing = 0.02f; // canvas units

        static readonly Dictionary<Color, Material> colorMats = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> crayonMats = new Dictionary<Color, Material>();
        static Texture2D crayonTexture;

        public static Material ColorMaterial(Color c)
        {
            if (colorMats.TryGetValue(c, out var m) && m != null) return m;
            m = NewUnlit();
            m.color = c;
            colorMats[c] = m;
            return m;
        }

        public static Material TextureMaterial(Texture tex)
        {
            var m = NewUnlit();
            m.color = Color.white;
            m.mainTexture = tex;
            return m;
        }

        /// <summary>See-through crayon wash with diagonal hatching, tinted with the given color.</summary>
        public static Material CrayonMaterial(Color c, float alpha = FillAlpha)
        {
            var key = new Color(c.r, c.g, c.b, alpha);
            if (crayonMats.TryGetValue(key, out var m) && m != null) return m;
            m = NewUnlit();
            m.mainTexture = CrayonTexture();
            m.color = key;
            MakeTransparent(m);
            crayonMats[key] = m;
            return m;
        }

        static Material NewUnlit()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // double sided
            return m;
        }

        static void MakeTransparent(Material m)
        {
            if (!m.HasProperty("_Surface")) return; // Sprites/Default already blends
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>White crayon grain: uneven coverage plus diagonal strokes, tiled over the fills.</summary>
        static Texture2D CrayonTexture()
        {
            if (crayonTexture != null) return crayonTexture;
            const int n = 128;
            var px = new Color32[n * n];
            var rng = new System.Random(7);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float grain = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.11f, y * 0.11f) + (float)rng.NextDouble() * 0.12f;
                    float stripe = Mathf.Sin((x + y) * 0.9f) * 0.5f + 0.5f; // diagonal hatching
                    float a = Mathf.Clamp01(grain * (0.62f + 0.38f * stripe));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            crayonTexture = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            crayonTexture.SetPixels32(px);
            crayonTexture.Apply(true);
            return crayonTexture;
        }

        /// <summary>Pen color: a deep shade of the fill, like a colored pen going over its own crayon.</summary>
        static Color LineColor(Color fill) => new Color(fill.r * 0.3f, fill.g * 0.3f, fill.b * 0.36f);

        /// <summary>Crayon fill: pastel of the ink; black ink uses the team's pastel (sky blue for us, pink for the enemy).</summary>
        static Color FillColor(InkColor c, Team? team)
        {
            switch (c)
            {
                case InkColor.Red: return new Color(1f, 0.5f, 0.42f);
                case InkColor.Blue: return new Color(0.45f, 0.62f, 1f);
                default:
                    return team == Team.Enemy ? new Color(0.98f, 0.55f, 0.75f)
                        : team == Team.Player ? new Color(0.45f, 0.8f, 0.95f)
                        : new Color(0.98f, 0.85f, 0.4f);
            }
        }

        /// <summary>Builds a drawing with its pivot at bottom-center. Team null = neutral (shots, effects).</summary>
        public static BuiltDoodle Build(Doodle doodle, bool mirror, Team? team, float scale, bool withEyes) =>
            Build(doodle, mirror, FillColor(doodle.color, team), FillAlpha, scale, withEyes, true, null);

        /// <summary>
        /// Scenery drawing in a chosen crayon color, without body parts. With <paramref name="origin"/> several
        /// drawings in the same canvas space line up (e.g. a tree's trunk and crown built in two colors).
        /// </summary>
        public static BuiltDoodle BuildScenery(Doodle doodle, Color fill, float scale, Vector2? origin = null,
            float alpha = 0.85f, bool withEyes = false) =>
            Build(doodle, false, fill, alpha, scale, withEyes, false, origin);

        static BuiltDoodle Build(Doodle doodle, bool mirror, Color fillColor, float fillAlpha, float scale, bool withEyes,
            bool classify, Vector2? originOverride)
        {
            var result = new BuiltDoodle { root = new GameObject("Doodle"), mirrored = mirror };

            Rect b = doodle.Bounds;
            Vector2 origin = originOverride ?? new Vector2(b.center.x, b.yMin);
            float k = WorldPerCanvas * scale;
            result.size = new Vector2(b.width * k + Lift * 2f, b.height * k + Lift * 2f);
            float facing = mirror ? -1f : 1f;

            var types = classify ? DoodleAnalyzer.ClassifyParts(doodle) : new PartType[doodle.strokes.Count];

            // Dragons (wings + legs) are rendered like a pencil study: dark sketchy outlines over
            // a clean pale wash. Ghosts are a see-through white sheet with a faint team-tinted outline.
            var kind = classify ? DoodleAnalyzer.Analyze(doodle).kind : UnitKind.Infantry;
            bool sketch = classify && kind == UnitKind.Dragon;
            bool ghost = classify && kind == UnitKind.Ghost;
            if (sketch) fillColor = SketchFill(doodle.color);
            if (ghost)
            {
                fillColor = Color.Lerp(fillColor, new Color(0.97f, 0.98f, 1f), 0.8f);
                fillAlpha = 0.45f;
            }

            Color line = sketch ? new Color(0.13f, 0.1f, 0.18f)
                : ghost ? Color.Lerp(LineColor(fillColor), fillColor, 0.35f)
                : LineColor(fillColor);
            var mats = new[]
            {
                sketch ? GraphiteMaterial(fillColor)
                    : fillAlpha >= 1f ? ColorMaterial(fillColor) // fully opaque (castle stone)
                    : CrayonMaterial(fillColor, fillAlpha),
                ColorMaterial(line),
                ColorMaterial(Color.Lerp(line, Color.white, sketch ? 0.15f : 0.3f)),
            };

            Vector2 bodyCenter = new Vector2(0f, b.height * k * 0.5f + Lift);

            for (int s = 0; s < doodle.strokes.Count; s++)
            {
                var stroke = doodle.strokes[s];
                if (stroke.points.Count == 0) continue;
                bool closed = DoodleAnalyzer.IsClosed(stroke);

                var pts = Resample(stroke.points, ResampleSpacing);
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 p = (pts[i] - origin) * k;
                    if (mirror) p.x = -p.x;
                    p.y += Lift;
                    pts[i] = p;
                }

                Rect sb = Bounds(pts);
                Vector2 pivot = Pivot(types[s], closed, pts, sb, bodyCenter);
                for (int i = 0; i < pts.Count; i++) pts[i] -= pivot;

                // Stick legs are thin and end in a little foot (not box legs, not a dragon's clawed legs);
                // weapons are drawn a bit finer than bodies.
                PartType type = types[s];
                bool stickLeg = type == PartType.Leg && !closed;
                float thickness = stickLeg ? (sketch ? 0.85f : 0.55f)
                    : type == PartType.Weapon || type == PartType.Gun ? 0.75f : 1f;
                List<Vector2> foot = stickLeg && !sketch ? Foot(pts, facing) : null;

                var go = new GameObject("Stroke " + s + " " + type);
                go.transform.SetParent(result.root.transform, false);
                // scenery and castles: each later stroke sits a hair in front, so overlapping solid shapes never flicker
                var rest = new Vector3(pivot.x, pivot.y, classify ? 0f : -0.003f * s);
                go.transform.localPosition = rest;
                go.AddComponent<MeshFilter>().sharedMesh = BuildStrokeMesh(pts, closed, pivot, s * 13.7f, thickness, foot, sketch);
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;

                // the end farthest from the joint: where shots leave a gun
                Vector2 tip = pts[0].sqrMagnitude > pts[pts.Count - 1].sqrMagnitude ? pts[0] : pts[pts.Count - 1];
                if (closed && (type == PartType.Gun || type == PartType.Weapon))
                {
                    // a barrel tube: the muzzle is the middle of its far end
                    float farX = Mathf.Abs(sb.xMin - pivot.x) > Mathf.Abs(sb.xMax - pivot.x) ? sb.xMin : sb.xMax;
                    tip = new Vector2(farX, sb.center.y) - pivot;
                }
                result.parts.Add(new BuiltPart
                {
                    go = go,
                    type = type,
                    closed = closed,
                    radius = Mathf.Max(sb.width, sb.height) * 0.5f,
                    restPosition = rest,
                    bounds = sb,
                    tip = tip,
                });
            }

            AttachToWings(result);
            AttachToFeet(result);
            AttachToHands(result);
            // robots have a mouth drawn on their box head already
            if (withEyes) AddEyes(result, facing, line, sketch, mouth: classify && !sketch && kind != UnitKind.Robot);
            return result;
        }

        /// <summary>
        /// A robot's weapon goes into the hand that holds it: a sword or cannon whose grip sits on an arm box
        /// becomes part of that arm, so it rises and swings with the arm (and still chops or recoils on its own).
        /// Bits drawn on a barrel (muzzle ring, breech, bands) ride on the barrel.
        /// </summary>
        static void AttachToHands(BuiltDoodle d)
        {
            BuiltPart body = null;
            foreach (var p in d.parts)
                if (p.type == PartType.Box && (body == null || Area(p.bounds) > Area(body.bounds))) body = p;

            foreach (var weapon in d.parts)
            {
                if ((weapon.type != PartType.Weapon && weapon.type != PartType.Gun) || weapon.attached) continue;
                Vector2 grip = weapon.restPosition;
                Vector2 point = grip + weapon.tip; // the far end, in the drawing's space

                if (body != null)
                {
                    BuiltPart hand = null;
                    float best = float.MaxValue;
                    foreach (var arm in d.parts)
                    {
                        if (arm.type != PartType.Box || arm == body || arm.attached) continue;
                        Rect r = arm.bounds;
                        float pad = Mathf.Max(r.width, r.height) * 0.35f;
                        var reach = Rect.MinMaxRect(r.xMin - pad, r.yMin - pad, r.xMax + pad, r.yMax + pad);
                        if (!reach.Contains(grip)) continue;
                        float dist = Vector2.Distance(grip, r.center);
                        if (dist < best) { best = dist; hand = arm; }
                    }
                    if (hand != null)
                    {
                        weapon.go.transform.SetParent(hand.go.transform, true);
                        weapon.restPosition = weapon.go.transform.localPosition;
                        weapon.attached = weapon.held = true;
                    }
                }

                // Bits drawn on the weapon ride on it: a barrel's breech, band and muzzle ring; a sword's blade,
                // crossguard and pommel. (Only barrels and weapons in a robot's hand, so a soldier's spear never
                // grabs the soldier's own belly.)
                if (!weapon.closed && !weapon.held) continue;
                Rect wb = weapon.bounds;
                float length = Vector2.Distance(grip, point);
                float margin = Mathf.Max(wb.width, wb.height) * 0.2f;
                var around = Rect.MinMaxRect(wb.xMin - margin, wb.yMin - margin, wb.xMax + margin, wb.yMax + margin);
                foreach (var bit in d.parts)
                {
                    if (bit == weapon || bit.attached || (bit.type != PartType.None && bit.type != PartType.Shield)) continue;
                    if (!around.Contains(bit.bounds.center) || Area(bit.bounds) > Area(wb) * 1.6f) continue;
                    if (!weapon.closed && DistanceToSegment(bit.bounds.center, grip, point) > length * 0.18f) continue;
                    bit.go.transform.SetParent(weapon.go.transform, true);
                    bit.restPosition = bit.go.transform.localPosition;
                    bit.attached = true;
                }
            }
        }

        static float Area(Rect r) => r.width * r.height;

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Little strokes at the bottom of a leg (claws, toes, a boot) move with that leg.</summary>
        static void AttachToFeet(BuiltDoodle d)
        {
            float reach = Mathf.Max(d.size.x, d.size.y) * 0.06f;
            foreach (var leg in d.parts)
            {
                if (leg.type != PartType.Leg || leg.closed) continue;
                Vector2 foot = (Vector2)leg.restPosition + leg.tip;
                foreach (var p in d.parts)
                {
                    if (p == leg || p.attached || p.type != PartType.None) continue;
                    if (Mathf.Max(p.bounds.width, p.bounds.height) > reach * 2.5f) continue;
                    if (Vector2.Distance(p.bounds.center, foot) > reach) continue;
                    p.go.transform.SetParent(leg.go.transform, true);
                    p.restPosition = p.go.transform.localPosition;
                    p.attached = true;
                }
            }
        }

        /// <summary>
        /// Shapes drawn inside a wing (membranes, finger bones) ride along with it, so the whole wing flaps as one.
        /// </summary>
        static void AttachToWings(BuiltDoodle d)
        {
            foreach (var wing in d.parts)
            {
                if (wing.type != PartType.Wing) continue;
                Rect wb = wing.bounds;
                float floor = wb.yMin + wb.height * 0.15f;
                foreach (var p in d.parts)
                {
                    if (p == wing || p.attached || (p.type != PartType.None && p.type != PartType.Shield)) continue;
                    if (!wb.Contains(p.bounds.center) || p.bounds.yMin < floor) continue;
                    p.go.transform.SetParent(wing.go.transform, true);
                    p.restPosition = p.go.transform.localPosition;
                    p.attached = true;
                }
            }
        }

        static Vector2 Pivot(PartType type, bool closed, List<Vector2> pts, Rect sb, Vector2 bodyCenter)
        {
            Vector2 first = pts[0], last = pts[pts.Count - 1];
            switch (type)
            {
                case PartType.Leg:
                    // stick leg: the hip end; box leg: the middle of its top edge
                    return closed ? new Vector2(sb.center.x, sb.yMax) : first.y > last.y ? first : last;
                case PartType.Box: return new Vector2(sb.center.x, sb.yMax); // robot arms swing from the shoulder
                case PartType.Weapon:
                case PartType.Gun:
                    if (closed)
                    {
                        // a barrel tube: its breech, the end nearer the body
                        float nearX = Mathf.Abs(sb.xMin - bodyCenter.x) < Mathf.Abs(sb.xMax - bodyCenter.x) ? sb.xMin : sb.xMax;
                        return new Vector2(nearX, sb.center.y);
                    }
                    // the end held by the body
                    return Vector2.Distance(first, bodyCenter) < Vector2.Distance(last, bodyCenter) ? first : last;
                case PartType.Wing:
                    // the shoulder: the end of the wing stroke that sits on the body, so a flapping wing stays attached
                    return Vector2.Distance(first, bodyCenter) < Vector2.Distance(last, bodyCenter) ? first : last;
                default: return sb.center;
            }
        }

        // ---------------------------------------------------------------- eyes

        static void AddEyes(BuiltDoodle d, float facing, Color ink, bool fierce = false, bool mouth = false)
        {
            float maxDim = Mathf.Max(d.size.x, d.size.y);
            BuiltPart head = null, body = null;
            float bestHeadY = float.MinValue, bestBodyArea = 0f;
            foreach (var p in d.parts)
            {
                if (!p.closed || p.attached || p.type == PartType.Wheel || p.type == PartType.Leg) continue;
                var r = p.bounds;
                if (Mathf.Max(r.width, r.height) < maxDim * 0.45f && r.center.y > d.size.y * 0.55f && r.center.y > bestHeadY)
                {
                    bestHeadY = r.center.y;
                    head = p;
                }
                float area = r.width * r.height;
                if (area > bestBodyArea)
                {
                    bestBodyArea = area;
                    body = p;
                }
            }

            Transform anchor;
            Vector2 a, c; // eye positions in the anchor's local space
            float er;
            if (head != null)
            {
                head.isHead = true;
                var r = head.bounds;
                float rad = Mathf.Max(r.width, r.height) * 0.5f;
                er = Mathf.Clamp(rad * 0.22f, 0.035f, 0.13f);
                Vector2 hc = r.center - (Vector2)head.restPosition;
                a = hc + new Vector2(facing * rad * 0.05f, rad * 0.15f);
                c = hc + new Vector2(facing * rad * 0.6f, rad * 0.15f);
                anchor = head.go.transform;
            }
            else if (body != null)
            {
                var r = body.bounds;
                float rx = r.width * 0.5f, ry = r.height * 0.5f;
                er = Mathf.Clamp(Mathf.Min(rx, ry) * 0.25f, 0.035f, 0.13f);
                Vector2 bc = r.center - (Vector2)body.restPosition;
                float ex = facing * Mathf.Max(rx * 0.55f - er, 0f);
                a = bc + new Vector2(ex, ry * 0.3f);
                c = bc + new Vector2(ex + facing * er * 2.1f, ry * 0.3f);
                anchor = body.go.transform;
            }
            else
            {
                er = 0.07f;
                Vector2 top = new Vector2(facing * d.size.x * 0.1f, d.size.y * 0.85f);
                a = top;
                c = top + new Vector2(facing * er * 2.1f, 0f);
                anchor = d.root.transform;
            }

            if (fierce) er *= 0.6f; // a predator's small, narrow eyes
            d.eyeRadius = er;
            foreach (var pos in new[] { a, c })
            {
                var eye = new GameObject("Eye").transform;
                eye.SetParent(anchor, false);
                eye.localPosition = new Vector3(pos.x, pos.y, -0.015f);
                AddDiscObject(eye, "Eye Rim", er * 1.18f, 0f, ColorMaterial(ink));
                AddDiscObject(eye, "Eye White", er, -0.004f, ColorMaterial(fierce ? new Color(1f, 0.82f, 0.25f) : Color.white));
                var pupil = AddDiscObject(eye, "Pupil", er * 0.55f, -0.008f, ColorMaterial(ink));
                if (fierce) pupil.localScale = new Vector3(0.35f, 1.5f, 1f); // slit pupil
                d.eyes.Add(eye);
                d.pupils.Add(pupil);
            }

            if (!mouth) return;
            // a little mouth under the eyes, a touch forward: a smiling "v", a grumpy "^" or a big open laughing V
            Vector2 m = (a + c) * 0.5f + new Vector2(facing * er * 0.4f, -er * 2.1f);
            float half = er * 0.8f, width = Mathf.Max(er * 0.3f, 0.012f);
            int style = Random.Range(0, 3);
            if (style == 2)
            {
                // laughing: a wide open V filled dark, with a pink tongue at the bottom
                float w = er * 1.05f, depth = er * 1.15f, top = m.y + er * 0.35f;
                AddPolygonObject(anchor, "Mouth", new[]
                {
                    new Vector2(m.x - w, top), new Vector2(m.x + w, top), new Vector2(m.x, top - depth),
                }, -0.016f, ColorMaterial(ink));
                AddPolygonObject(anchor, "Tongue", new[]
                {
                    new Vector2(m.x - w * 0.42f, top - depth * 0.58f), new Vector2(m.x + w * 0.42f, top - depth * 0.58f),
                    new Vector2(m.x, top - depth * 0.92f),
                }, -0.018f, ColorMaterial(new Color(1f, 0.45f, 0.5f)));
                return;
            }
            float dip = er * 0.55f * (style == 0 ? 1f : -1f);
            AddLineObject(anchor, "Mouth", new[]
            {
                new Vector2(m.x - half, m.y + dip * 0.5f), new Vector2(m.x, m.y - dip * 0.5f), new Vector2(m.x + half, m.y + dip * 0.5f),
            }, width, -0.016f, ColorMaterial(ink));
        }

        /// <summary>A small filled shape (fan triangles from its first corner, drawn from both sides).</summary>
        static void AddPolygonObject(Transform parent, string name, Vector2[] corners, float z, Material mat)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            foreach (var p in corners) v.Add(p);
            for (int i = 1; i + 1 < corners.Length; i++) t.AddRange(new[] { 0, i, i + 1, 0, i + 1, i });
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>A short pen line through the given points (each joint gets a round dot so corners don't gap).</summary>
        static void AddLineObject(Transform parent, string name, Vector2[] points, float width, float z, Material mat)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 p0 = points[i - 1], p1 = points[i];
                Vector2 n = Vector2.Perpendicular((p1 - p0).normalized) * (width * 0.5f);
                int k = v.Count;
                v.Add(p0 - n); v.Add(p0 + n); v.Add(p1 - n); v.Add(p1 + n);
                t.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3, k, k + 2, k + 1, k + 2, k + 3, k + 1 });
            }
            foreach (var p in points) AddDisc(v, t, p, width * 0.5f, 0f, 8);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Transform AddDiscObject(Transform parent, string name, float r, float z, Material mat)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            AddDisc(v, t, Vector2.zero, r, 0f, 16);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        // ---------------------------------------------------------------- meshes

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
            if (src.Count > 1 && Vector2.Distance(result[result.Count - 1], src[src.Count - 1]) > spacing * 0.3f)
                result.Add(src[src.Count - 1]);
            return result;
        }

        /// <summary>
        /// Submesh 0: crayon fill (closed shapes only), 1: the main pen line, 2: a lighter second pass,
        /// a little off the first one, like a quick sketch gone over twice.
        /// </summary>
        static Mesh BuildStrokeMesh(List<Vector2> pts, bool closed, Vector2 pivot, float seed, float thickness,
            List<Vector2> foot, bool pencil = false)
        {
            var verts = new List<Vector3>();
            var fill = new List<int>();
            var line = new List<int>();
            var sketch = new List<int>();

            var main = Wobbled(pts, seed, Wobble);
            var second = Wobbled(pts, seed + 5.3f, Wobble * 1.6f);

            // Camera looks down +Z, so larger z = further back.
            if (closed) AddFill(verts, fill, main, 0.012f);
            AddShape(verts, sketch, second, SketchHalfWidth * thickness, 0.006f, new Vector2(0.012f, -0.01f), !closed);
            AddShape(verts, line, main, LineHalfWidth * thickness, 0f, Vector2.zero, !closed);
            if (pencil)
            {
                // a pencil goes over its lines several times, never quite in the same place
                AddShape(verts, sketch, Wobbled(pts, seed + 11.1f, Wobble * 2.6f), SketchHalfWidth * 0.8f, 0.004f,
                    new Vector2(-0.014f, 0.012f), !closed);
            }
            if (foot != null)
            {
                AddFill(verts, fill, foot, 0.012f);
                AddShape(verts, line, foot, LineHalfWidth * 0.6f, 0f, Vector2.zero, false);
            }

            // World-anchored UVs so the crayon hatching lines up across the whole drawing.
            var uvs = new List<Vector2>(verts.Count);
            foreach (var v in verts) uvs.Add(new Vector2(v.x + pivot.x, v.y + pivot.y) * 1.7f);

            var mesh = new Mesh { name = "Stroke" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(fill, 0);
            mesh.SetTriangles(line, 1);
            mesh.SetTriangles(sketch, 2);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Pale wash under a pencil-drawn creature: lavender by default, tinted by the ink.</summary>
        static Color SketchFill(InkColor c)
        {
            switch (c)
            {
                case InkColor.Red: return new Color(0.9f, 0.62f, 0.6f);
                case InkColor.Blue: return new Color(0.62f, 0.68f, 0.95f);
                default: return new Color(0.74f, 0.68f, 0.88f);
            }
        }

        static Texture2D graphiteTexture;

        /// <summary>Paper-grain wash for pencil drawings: solid enough to read, with soft tooth.</summary>
        static Material GraphiteMaterial(Color c)
        {
            if (graphiteTexture == null)
            {
                const int n = 128;
                var px = new Color32[n * n];
                var rng = new System.Random(5);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float grain = 0.86f + 0.14f * Mathf.PerlinNoise(x * 0.2f, y * 0.2f) - (float)rng.NextDouble() * 0.08f;
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(grain) * 255));
                    }
                graphiteTexture = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
                graphiteTexture.SetPixels32(px);
                graphiteTexture.Apply(true);
            }
            var key = new Color(c.r, c.g, c.b, 0.93f);
            if (crayonMats.TryGetValue(key, out var m) && m != null && m.mainTexture == graphiteTexture) return m;
            m = NewUnlit();
            m.mainTexture = graphiteTexture;
            m.color = key;
            MakeTransparent(m);
            crayonMats[key] = m;
            return m;
        }

        /// <summary>A little oval shoe at the lower end of a leg, pointing the way the unit faces.</summary>
        static List<Vector2> Foot(List<Vector2> leg, float facing)
        {
            Vector2 a = leg[0], b = leg[leg.Count - 1];
            Vector2 ankle = a.y < b.y ? a : b;
            float rx = Mathf.Clamp(Vector2.Distance(a, b) * 0.2f, 0.045f, 0.13f), ry = rx * 0.45f;
            Vector2 c = ankle + new Vector2(facing * rx * 0.55f, ry * 0.5f);
            var shoe = new List<Vector2>();
            const int segments = 14;
            for (int i = 0; i <= segments; i++)
            {
                float t = i * Mathf.PI * 2f / segments;
                shoe.Add(c + new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * ry));
            }
            return shoe;
        }

        /// <summary>Hand-drawn waviness: pushes each point sideways by a smooth, low-frequency noise.</summary>
        static List<Vector2> Wobbled(List<Vector2> pts, float seed, float amount)
        {
            int n = pts.Count;
            var result = new List<Vector2>(n);
            float run = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) run += Vector2.Distance(pts[i - 1], pts[i]);
                Vector2 dir = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                Vector2 normal = dir.sqrMagnitude > 1e-8f ? new Vector2(-dir.y, dir.x).normalized : Vector2.zero;
                float w = Mathf.Sin(run * 6.1f + seed) * 0.6f + Mathf.Sin(run * 15.3f + seed * 1.7f) * 0.4f;
                result.Add(pts[i] + normal * (w * amount));
            }
            return result;
        }

        /// <summary>Ribbon along the points plus a disc at every point for round joins and caps. Tapered = thinner at the ends like a real pen line.</summary>
        static void AddShape(List<Vector3> v, List<int> t, List<Vector2> pts, float hw, float z, Vector2 offset, bool tapered)
        {
            int n = pts.Count;
            var widths = new float[n];
            float total = 0f;
            for (int i = 1; i < n; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
            float run = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) run += Vector2.Distance(pts[i - 1], pts[i]);
                float f = total > 1e-5f ? run / total : 0.5f;
                widths[i] = tapered ? hw * (0.45f + 0.55f * Mathf.Sin(f * Mathf.PI)) : hw;
            }

            for (int i = 0; i < n; i++) AddDisc(v, t, pts[i] + offset, widths[i], z, 10);
            if (n < 2) return;

            int start = v.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                if (dir.sqrMagnitude < 1e-8f) dir = Vector2.right;
                dir.Normalize();
                Vector2 normal = new Vector2(-dir.y, dir.x) * widths[i];
                Vector2 p = pts[i] + offset;
                v.Add(new Vector3(p.x + normal.x, p.y + normal.y, z));
                v.Add(new Vector3(p.x - normal.x, p.y - normal.y, z));
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = start + i * 2;
                t.Add(a); t.Add(a + 2); t.Add(a + 1);
                t.Add(a + 1); t.Add(a + 2); t.Add(a + 3);
            }
        }

        /// <summary>Solid wash inside a closed shape (ear-clipping, falls back to a fan).</summary>
        static void AddFill(List<Vector3> v, List<int> t, List<Vector2> pts, float z)
        {
            var poly = new List<Vector2>();
            int step = Mathf.Max(1, pts.Count / 64);
            for (int i = 0; i < pts.Count - 1; i += step) poly.Add(pts[i]);
            if (poly.Count < 3) return;

            int start = v.Count;
            foreach (var p in poly) v.Add(new Vector3(p.x, p.y, z));
            foreach (int i in Triangulate(poly)) t.Add(start + i);
        }

        static List<int> Triangulate(List<Vector2> poly)
        {
            int n = poly.Count;
            var idx = new List<int>();
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % n];
                area += p.x * q.y - q.x * p.y;
            }
            for (int i = 0; i < n; i++) idx.Add(area > 0f ? i : n - 1 - i); // counter-clockwise

            var tris = new List<int>();
            int guard = n * n;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if (Cross(b - a, c - b) <= 1e-7f) continue; // reflex corner
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (InTriangle(poly[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    tris.Add(ia); tris.Add(ib); tris.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }

            if (idx.Count == 3)
            {
                tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]);
            }
            else if (idx.Count > 3)
            {
                // self-intersecting scribble: fan out what is left
                for (int i = 1; i < idx.Count - 1; i++)
                {
                    tris.Add(idx[0]); tris.Add(idx[i]); tris.Add(idx[i + 1]);
                }
            }
            return tris;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f;

        static void AddDisc(List<Vector3> v, List<int> t, Vector2 c, float r, float z, int segments)
        {
            int start = v.Count;
            v.Add(new Vector3(c.x, c.y, z));
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                v.Add(new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, z));
            }
            for (int i = 0; i < segments; i++)
            {
                t.Add(start);
                t.Add(start + 1 + (i + 1) % segments);
                t.Add(start + 1 + i);
            }
        }

        static Rect Bounds(List<Vector2> pts)
        {
            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>Frees the generated meshes under an object that is being destroyed.</summary>
        public static void DestroyMeshes(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.sharedMesh.name != "Quad") Object.Destroy(mf.sharedMesh);
        }

        /// <summary>Flat ellipse lying on the ground, used as a fake shadow.</summary>
        static Material shadowMaterial;

        /// <summary>
        /// A soft, faint ground shadow: a flat quad with a radial fade, darkest (but still see-through) in the middle
        /// and melting into the paper at the edge.
        /// </summary>
        public static GameObject BuildShadow(float rx, float rz)
        {
            var mesh = new Mesh { name = "Shadow" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-rx, 0.01f, -rz), new Vector3(rx, 0.01f, -rz), new Vector3(-rx, 0.01f, rz), new Vector3(rx, 0.01f, rz),
            });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            mesh.RecalculateBounds();

            if (shadowMaterial == null)
            {
                const int n = 64;
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                        float a = Mathf.SmoothStep(0f, 1f, 1f - d) * 0.22f;
                        px[y * n + x] = new Color32(70, 55, 45, (byte)(a * 255));
                    }
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(px);
                tex.Apply();
                shadowMaterial = NewUnlit();
                shadowMaterial.mainTexture = tex;
                shadowMaterial.color = Color.white;
                MakeTransparent(shadowMaterial);
                shadowMaterial.renderQueue = 2950; // under every see-through crayon drawing
            }

            var go = new GameObject("Shadow");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = shadowMaterial;
            return go;
        }
    }
}
