using System.Collections.Generic;
using UnityEngine;
using T = ArtWar.DoodleTemplates;

namespace ArtWar
{
    /// <summary>
    /// Water and sky life around the battlefield, in the same crayon style: a river winding across the front lawn
    /// (pencilled banks, ripples, lily pads, a little wooden bridge), the sea behind the hills with sailboats bobbing
    /// on it, and flocks of birds and seagulls flapping across the sky.
    /// </summary>
    public static class Waterscape
    {
        static readonly Color RiverBlue = new Color(0.55f, 0.78f, 0.95f);
        static readonly Color SeaBlue = new Color(0.42f, 0.68f, 0.92f);
        static readonly Color Bank = new Color(0.25f, 0.42f, 0.55f);
        static readonly Color Foam = new Color(0.94f, 0.98f, 1f);
        static readonly Color Pad = new Color(0.35f, 0.68f, 0.35f);
        static readonly Color Wood = new Color(0.8f, 0.6f, 0.4f);
        static readonly Color Sand = new Color(0.96f, 0.88f, 0.66f);

        public const float RiverZ = -11.4f;

        public static void Build(Transform root, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            River(root, rng);
            Sea(root);

            // sailboats out at sea, behind the hills: smaller the farther out they are, never right under the sun
            for (int i = 0; i < 5; i++)
            {
                float z = R(38f, 62f);
                float x = R(-40f, 40f);
                if (Mathf.Abs(x - Scenery.SunX) < 9f) x = Scenery.SunX + Mathf.Sign(x - Scenery.SunX + 0.01f) * R(9f, 14f);
                var boat = DoodleBuilder.BuildScenery(BoatShape(rng), i % 2 == 0 ? new Color(1f, 0.55f, 0.45f) : new Color(1f, 0.95f, 0.8f),
                    R(0.9f, 1.15f) * 42f / z, null, 0.95f);
                boat.root.name = "Sailboat";
                boat.root.transform.SetParent(root, false);
                boat.root.transform.position = new Vector3(x, -0.05f, z);
                boat.root.AddComponent<Bob>().phase = R(0f, 6f);
            }

            // birds far away only: a flock over the hills and seagulls over the sea
            for (int flock = 0; flock < 2; flock++)
            {
                float x0 = R(-8f, 20f), y0 = R(4.6f, 5.6f), z0 = R(24f, 30f);
                for (int i = 0; i < 5; i++)
                    Bird(root, new Vector3(x0 - i * R(0.9f, 1.5f), y0 + (i % 2 == 0 ? 0.4f : -0.3f) * (i / 2), z0 + i * 0.3f),
                        new Color(0.42f, 0.4f, 0.5f), R(1.2f, 1.8f), R(0.7f, 0.9f), rng, -16f, 40f);
            }
            for (int i = 0; i < 5; i++)
                Bird(root, new Vector3(R(-10f, 30f), R(5f, 6.5f), R(38f, 48f)), Color.white, R(0.6f, 1f), R(0.8f, 1f), rng, -20f, 46f);
        }

        // ---------------------------------------------------------------- river

        /// <summary>Centre line of the river: a lazy S across the front lawn.</summary>
        static float RiverCentre(float x) => RiverZ + Mathf.Sin(x * 0.12f) * 0.7f + Mathf.Sin(x * 0.31f + 1f) * 0.25f;
        static float RiverHalfWidth(float x) => 0.95f + Mathf.Sin(x * 0.21f) * 0.2f;

        static void River(Transform root, System.Random rng)
        {
            var river = new GameObject("River").transform;
            river.SetParent(root, false);

            // the water: a strip mesh with a waxy blue texture
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (float x = -50f; x <= 50.01f; x += 0.5f)
            {
                float c = RiverCentre(x), w = RiverHalfWidth(x);
                verts.Add(new Vector3(x, 0.004f, c - w));
                verts.Add(new Vector3(x, 0.004f, c + w));
                uvs.Add(new Vector2(x * 0.2f, 0f));
                uvs.Add(new Vector2(x * 0.2f, 1f));
                int n = verts.Count;
                if (n >= 4) tris.AddRange(new[] { n - 4, n - 3, n - 2, n - 2, n - 3, n - 1 });
            }
            var water = MeshObject(river, "Water", verts, uvs, tris, DoodleBuilder.TextureMaterial(WaterTexture(RiverBlue, 7)));
            water.GetComponent<Renderer>().sharedMaterial.mainTexture.wrapMode = TextureWrapMode.Repeat;

            // pencilled banks, each drawn twice a little apart
            foreach (float side in new[] { -1f, 1f })
                for (int pass = 0; pass < 2; pass++)
                {
                    var line = new List<Vector3>();
                    for (float x = -50f; x <= 50.01f; x += 0.4f)
                        line.Add(new Vector3(x, 0.008f, RiverCentre(x) + side * (RiverHalfWidth(x) + pass * 0.05f)
                                                         + Mathf.Sin(x * 1.3f + pass) * 0.02f));
                    Ribbon(river, "Bank", line, pass == 0 ? 0.07f : 0.035f, Bank);
                }

            // ripples: little white ticks along the current
            for (int i = 0; i < 60; i++)
            {
                float x = -40f + (float)rng.NextDouble() * 80f;
                float z = RiverCentre(x) + ((float)rng.NextDouble() - 0.5f) * RiverHalfWidth(x) * 1.2f;
                var arc = new List<Vector3>();
                for (int k = 0; k <= 6; k++)
                {
                    float t = k / 6f;
                    arc.Add(new Vector3(x + t * 0.5f, 0.01f, z + Mathf.Sin(t * Mathf.PI * 2f) * 0.05f));
                }
                Ribbon(river, "Ripple", arc, 0.04f, Foam);
            }

            // lily pads, some flowering
            for (int i = 0; i < 14; i++)
            {
                float x = -35f + (float)rng.NextDouble() * 70f;
                float z = RiverCentre(x) + ((float)rng.NextDouble() - 0.5f) * RiverHalfWidth(x);
                float r = 0.16f + (float)rng.NextDouble() * 0.1f;
                Disc(river, "Lily Pad", new Vector3(x, 0.012f, z), r, Pad, (float)rng.NextDouble() * 360f, true);
                if (rng.NextDouble() < 0.45)
                    Disc(river, "Lily Flower", new Vector3(x + 0.03f, 0.016f, z + 0.02f), r * 0.35f, new Color(1f, 0.7f, 0.85f), 0f, false);
            }

            Bridge(river, 2f);
        }

        static Texture2D WaterTexture(Color baseColor, int seed)
        {
            const int n = 64;
            var px = new Color32[n * n];
            var rng = new System.Random(seed);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float shade = 0.93f + 0.07f * Mathf.PerlinNoise(x * 0.09f, y * 0.21f) + (float)rng.NextDouble() * 0.03f;
                    // waxy wave streaks running along the water
                    float wave = Mathf.Sin(x * 0.2f + Mathf.Sin(y * 0.4f) * 1.5f) > 0.93f ? 1.12f : 1f;
                    Color c = baseColor * shade * wave;
                    c.a = 1f;
                    px[y * n + x] = c;
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// A little wooden footbridge across the river at <paramref name="x"/>: an arched deck of planks from one bank
        /// to the other, with posts and a hand rail along both sides following the arch.
        /// </summary>
        static void Bridge(Transform parent, float x)
        {
            var bridge = new GameObject("Bridge").transform;
            bridge.SetParent(parent, false);
            float c = RiverCentre(x), w = RiverHalfWidth(x);
            float z0 = c - w - 0.55f, z1 = c + w + 0.55f, rise = 0.45f, halfWidth = 0.62f;
            const int planks = 11;
            var dark = new Color(0.5f, 0.34f, 0.22f);

            float Y(float t) => 0.04f + Mathf.Sin(t * Mathf.PI) * rise;
            Vector3 Point(float t, float side) => new Vector3(x + side, Y(t), Mathf.Lerp(z0, z1, t));

            for (int i = 0; i < planks; i++)
            {
                float t = (i + 0.5f) / planks;
                float slope = Mathf.Cos(t * Mathf.PI) * Mathf.PI * rise / (z1 - z0);
                var plank = Box(bridge, "Plank", Point(t, 0f), new Vector3(halfWidth * 2f, 0.08f, (z1 - z0) / planks * 0.9f),
                    i % 2 == 0 ? Wood : Wood * 0.9f);
                plank.localRotation = Quaternion.Euler(-Mathf.Atan(slope) * Mathf.Rad2Deg, 0f, 0f);
            }

            // posts at every other plank and a rail joining their tops, on both sides
            foreach (float side in new[] { -halfWidth, halfWidth })
            {
                Vector3? last = null;
                for (int i = 0; i <= planks; i += 2)
                {
                    float t = i / (float)planks;
                    var foot = Point(t, side);
                    Box(bridge, "Post", foot + Vector3.up * 0.2f, new Vector3(0.08f, 0.42f, 0.08f), dark);
                    var top = foot + Vector3.up * 0.4f;
                    if (last.HasValue)
                    {
                        var rail = Box(bridge, "Rail", (last.Value + top) * 0.5f,
                            new Vector3(0.06f, 0.06f, Vector3.Distance(last.Value, top) + 0.04f), dark);
                        rail.rotation = Quaternion.LookRotation(top - last.Value);
                    }
                    last = top;
                }
            }
        }

        static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.position = pos;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = DoodleBuilder.ColorMaterial(color);
            return box.transform;
        }

        // ---------------------------------------------------------------- sea

        static void Sea(Transform root)
        {
            // a sandy shore right behind the hills, then open sea to the horizon
            var sand = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sand.name = "Beach";
            Object.Destroy(sand.GetComponent<Collider>());
            sand.transform.SetParent(root, false);
            sand.transform.position = new Vector3(0f, -0.03f, 32.6f); // from where the grass ends behind the hills
            sand.transform.localScale = new Vector3(80f, 1f, 0.32f);
            sand.GetComponent<Renderer>().sharedMaterial = DoodleBuilder.ColorMaterial(Sand);

            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";
            Object.Destroy(sea.GetComponent<Collider>());
            sea.transform.SetParent(root, false);
            sea.transform.position = new Vector3(0f, -0.05f, 234.2f);
            sea.transform.localScale = new Vector3(100f, 1f, 40f);
            var mat = DoodleBuilder.TextureMaterial(WaterTexture(SeaBlue, 3));
            mat.mainTexture.wrapMode = TextureWrapMode.Repeat;
            mat.mainTextureScale = new Vector2(160f, 64f);
            sea.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Doodle BoatShape(System.Random rng)
        {
            var d = new Doodle();
            d.strokes.Add(T.Polyline(true, new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.3f), new Vector2(0.72f, 0.15f),
                new Vector2(0.28f, 0.15f)));                                                       // hull
            d.strokes.Add(T.Line(new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.85f)));               // mast
            d.strokes.Add(T.Polyline(true, new Vector2(0.52f, 0.82f), new Vector2(0.52f, 0.36f),
                new Vector2(0.78f, 0.36f)));                                                       // main sail
            if (rng.NextDouble() < 0.6)
                d.strokes.Add(T.Polyline(true, new Vector2(0.48f, 0.72f), new Vector2(0.48f, 0.38f),
                    new Vector2(0.3f, 0.38f)));                                                    // jib
            d.strokes.Add(T.Polyline(true, new Vector2(0.5f, 0.85f), new Vector2(0.6f, 0.82f), new Vector2(0.5f, 0.79f))); // pennant
            return d;
        }

        // ---------------------------------------------------------------- birds

        static void Bird(Transform root, Vector3 pos, Color color, float speed, float scale, System.Random rng,
            float minX = -13f, float maxX = 32f)
        {
            // a chunky crayon "M": two filled curved wings meeting at the body, so it reads from far away
            var d = new Doodle();
            d.strokes.Add(T.Polyline(true,
                new Vector2(0.05f, 0.52f), new Vector2(0.18f, 0.66f), new Vector2(0.32f, 0.7f), new Vector2(0.44f, 0.6f),
                new Vector2(0.5f, 0.52f), new Vector2(0.56f, 0.6f), new Vector2(0.68f, 0.7f), new Vector2(0.82f, 0.66f),
                new Vector2(0.95f, 0.52f), new Vector2(0.8f, 0.58f), new Vector2(0.66f, 0.58f), new Vector2(0.55f, 0.48f),
                new Vector2(0.5f, 0.42f), new Vector2(0.45f, 0.48f), new Vector2(0.34f, 0.58f), new Vector2(0.2f, 0.58f)));
            var built = DoodleBuilder.BuildScenery(d, color, scale, new Vector2(0.5f, 0.5f), 1f);
            built.root.name = "Bird";
            built.root.transform.SetParent(root, false);
            built.root.transform.position = pos;
            var bird = built.root.AddComponent<Flap>();
            bird.speed = speed;
            bird.phase = (float)rng.NextDouble() * 6f;
            bird.rate = 7f + (float)rng.NextDouble() * 4f;
            bird.minX = minX;
            bird.maxX = maxX;
        }

        // ---------------------------------------------------------------- mesh helpers

        static GameObject MeshObject(Transform parent, string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris, Material mat)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            if (uvs != null) mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A flat pencil line on the ground following the given points.</summary>
        static void Ribbon(Transform parent, string name, List<Vector3> line, float width, Color color)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < line.Count; i++)
            {
                Vector3 dir = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 n = new Vector3(-dir.z, 0f, dir.x) * (width * 0.5f);
                verts.Add(line[i] - n);
                verts.Add(line[i] + n);
                int k = verts.Count;
                if (k >= 4) tris.AddRange(new[] { k - 4, k - 3, k - 2, k - 2, k - 3, k - 1 });
            }
            MeshObject(parent, name, verts, null, tris, DoodleBuilder.ColorMaterial(color));
        }

        /// <summary>A flat disc on the water; a lily pad gets its little notch.</summary>
        static void Disc(Transform parent, string name, Vector3 centre, float r, Color color, float rotation, bool notch)
        {
            var verts = new List<Vector3> { centre };
            var tris = new List<int>();
            const int seg = 18;
            float gap = notch ? 0.5f : 0f;
            for (int i = 0; i <= seg; i++)
            {
                float a = (rotation * Mathf.Deg2Rad) + gap * 0.5f + i * (Mathf.PI * 2f - gap) / seg;
                verts.Add(centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.8f));
                if (i > 0) tris.AddRange(new[] { 0, i + 1, i });
            }
            MeshObject(parent, name, verts, null, tris, DoodleBuilder.ColorMaterial(color));
        }
    }

    /// <summary>A bird crossing the sky, wings flapping (the drawing flips up and down), wrapping round at the edge.</summary>
    public class Flap : MonoBehaviour
    {
        public float speed = 2f, rate = 9f, phase;
        public float minX = -55f, maxX = 55f; // the stretch of sky it loops over

        void Update()
        {
            var p = transform.position;
            p.x += speed * Time.deltaTime;
            p.y += Mathf.Sin(Time.time * 0.8f + phase) * 0.15f * Time.deltaTime;
            if (p.x > maxX) p.x = minX;
            transform.position = p;
            float beat = Mathf.Sin(Time.time * rate + phase);
            transform.localScale = new Vector3(1f, 0.35f + 0.65f * beat, 1f);
        }
    }

    /// <summary>A boat riding the waves: bobs, rocks and drifts slowly.</summary>
    public class Bob : MonoBehaviour
    {
        public float drift, phase;
        float baseY;

        void Start() => baseY = transform.position.y;

        void Update()
        {
            var p = transform.position;
            p.x += drift * Time.deltaTime;
            if (p.x > 50f) p.x = -50f;
            if (p.x < -50f) p.x = 50f;
            p.y = baseY + Mathf.Sin(Time.time * 1.3f + phase) * 0.12f;
            transform.position = p;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.1f + phase) * 4f);
        }
    }
}
