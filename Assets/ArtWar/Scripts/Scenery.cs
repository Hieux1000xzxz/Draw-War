using UnityEngine;
using T = ArtWar.DoodleTemplates;

namespace ArtWar
{
    /// <summary>
    /// The landscape around the paper battlefield, drawn in the same pen-and-crayon style as the units:
    /// grass, rolling hills, a row of trees and bushes, a picket fence, flowers and grass tufts,
    /// a smiling sun and drifting clouds. Built once from a fixed seed so the scene always looks the same.
    /// </summary>
    public static class Scenery
    {
        static readonly Color Leaf = new Color(0.45f, 0.78f, 0.4f);
        static readonly Color DarkLeaf = new Color(0.3f, 0.62f, 0.35f);
        static readonly Color Trunk = new Color(0.7f, 0.5f, 0.32f);
        static readonly Color Hill = new Color(0.42f, 0.74f, 0.34f);
        static readonly Color FarHill = new Color(0.38f, 0.64f, 0.5f);
        static readonly Color Wood = new Color(0.92f, 0.82f, 0.62f);
        static readonly Color Sun = new Color(1f, 0.85f, 0.3f);
        static readonly Color Cloud = new Color(1f, 1f, 1f);
        static readonly Color[] Petals =
        {
            new Color(1f, 0.6f, 0.75f), new Color(0.75f, 0.6f, 1f), new Color(1f, 0.95f, 0.5f), new Color(1f, 0.55f, 0.45f),
            new Color(1f, 0.4f, 0.4f), new Color(0.5f, 0.75f, 1f), new Color(1f, 0.72f, 0.3f), new Color(1f, 1f, 1f),
            new Color(0.95f, 0.45f, 0.85f),
        };
        static readonly Color Blossom = new Color(1f, 0.74f, 0.84f);
        static readonly Color Autumn = new Color(0.98f, 0.66f, 0.3f);
        static readonly Color Apple = new Color(0.95f, 0.25f, 0.25f);
        static readonly Color Mushroom = new Color(0.92f, 0.25f, 0.22f);
        static readonly Color Cream = new Color(1f, 0.97f, 0.88f);
        static readonly Color SeedBrown = new Color(0.55f, 0.35f, 0.18f);

        public static readonly Color SkyColor = new Color(0.74f, 0.88f, 0.98f);

        public static void Build(Transform parent, float fieldHalfX, float fieldHalfZ)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(2024);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            BuildGrass(root);

            // far hills along the horizon
            for (int i = 0; i < 7; i++)
            {
                float x = -42f + i * 14f + R(-3f, 3f);
                Place(root, HillShape(rng), i % 2 == 0 ? FarHill : Hill, R(3.5f, 5f), new Vector3(x, -0.3f, R(29f, 32f)), 0.95f);
            }

            // sun and clouds above the hills
            var sun = DoodleBuilder.BuildScenery(SunShape(), Sun, 2.2f, null, 0.95f, true);
            Put(root, sun.root, new Vector3(20f, 3.4f, 33f));
            for (int i = 0; i < 5; i++)
            {
                var cloud = DoodleBuilder.BuildScenery(CloudShape(rng), Cloud, R(2f, 3f), null, 0.9f);
                Put(root, cloud.root, new Vector3(-36f + i * 17f + R(-4f, 4f), R(3.2f, 4.6f), R(32f, 34f)));
                cloud.root.AddComponent<Drift>().speed = R(0.15f, 0.35f);
            }

            // a picket fence behind the paper, then trees and bushes
            float back = fieldHalfZ + 3.6f;
            for (float x = -24f; x < 24f; x += 3.1f)
                Place(root, FenceShape(), Wood, 0.6f, new Vector3(x, 0f, back), 0.9f);
            for (int i = 0; i < 16; i++)
            {
                float x = -26f + i * 3.5f + R(-1f, 1f);
                float z = back + R(1.5f, 9f);
                float scale = R(0.9f, 1.6f);
                if (rng.NextDouble() < 0.6) RoundTree(root, new Vector3(x, 0f, z), scale, rng);
                else PineTree(root, new Vector3(x, 0f, z), scale);
            }
            for (int i = 0; i < 12; i++)
                Place(root, BushShape(rng), i % 2 == 0 ? Leaf : DarkLeaf, R(0.35f, 0.55f),
                    new Vector3(R(-24f, 24f), 0f, back + R(0.4f, 1.2f)), 0.9f);

            // flowers and tufts around the paper (in front and on the sides)
            for (int i = 0; i < 26; i++)
            {
                bool front = i < 16;
                float x = front ? R(-20f, 20f) : (i % 2 == 0 ? -1f : 1f) * R(fieldHalfX + 4.5f, fieldHalfX + 9f);
                float z = front ? -(fieldHalfZ + R(3.6f, 6.5f)) : R(-fieldHalfZ - 2f, fieldHalfZ + 2f);
                var pos = new Vector3(x, 0f, z);
                if (rng.NextDouble() < 0.55) Flower(root, pos, R(0.4f, 0.55f), Petals[rng.Next(Petals.Length)]);
                else Place(root, TuftShape(rng), DarkLeaf, R(0.3f, 0.45f), pos, 0.9f);
            }

            // a second row of colourful trees between the first ones: apple, blossom and autumn trees
            for (int i = 0; i < 12; i++)
            {
                float x = -24f + i * 4.3f + R(-1f, 1f);
                var pos = new Vector3(x, 0f, back + R(4f, 12f));
                float scale = R(1f, 1.5f);
                switch (i % 3)
                {
                    case 0: ColorTree(root, pos, scale, rng, Leaf, Apple); break;
                    case 1: ColorTree(root, pos, scale, rng, Blossom, null); break;
                    default: ColorTree(root, pos, scale, rng, Autumn, null); break;
                }
            }

            // sunflowers and flower patches along the fence
            for (float x = -22f; x < 22f; x += R(2.5f, 4f))
            {
                if (rng.NextDouble() < 0.45) Sunflower(root, new Vector3(x, 0f, back + R(0.6f, 1.4f)), R(0.7f, 0.95f));
                else FlowerPatch(root, new Vector3(x, 0f, back + R(0.6f, 1.6f)), rng, 3);
            }

            // groves beyond each castle
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 6; i++)
                {
                    var pos = new Vector3(side * R(fieldHalfX + 5f, fieldHalfX + 13f), 0f, R(-fieldHalfZ, fieldHalfZ + 3f));
                    if (rng.NextDouble() < 0.5) ColorTree(root, pos, R(1f, 1.4f), rng, i % 2 == 0 ? Blossom : Leaf, i % 2 == 0 ? (Color?)null : Apple);
                    else PineTree(root, pos, R(1f, 1.4f));
                }
                for (int i = 0; i < 4; i++)
                    FlowerPatch(root, new Vector3(side * R(fieldHalfX + 4f, fieldHalfX + 10f), 0f, R(-fieldHalfZ - 3f, fieldHalfZ)), rng, 4);
                Sunflower(root, new Vector3(side * (fieldHalfX + 4.3f), 0f, -fieldHalfZ - 3.8f), 0.9f);
            }

            // the front lawn: low patches of flowers, tulips and toadstools that don't hide the battle
            for (int i = 0; i < 14; i++)
            {
                var pos = new Vector3(R(-22f, 22f), 0f, -(fieldHalfZ + R(3.9f, 8f)));
                if (rng.NextDouble() < 0.3) Toadstool(root, pos, R(0.45f, 0.6f));
                else FlowerPatch(root, pos, rng, 3);
            }
        }

        // ---------------------------------------------------------------- placement

        static void Place(Transform root, Doodle d, Color fill, float scale, Vector3 pos, float alpha)
        {
            var built = DoodleBuilder.BuildScenery(d, fill, scale, null, alpha);
            Put(root, built.root, pos);
        }

        static void Put(Transform root, GameObject go, Vector3 pos)
        {
            go.transform.SetParent(root, false);
            go.transform.position = pos;
        }

        /// <summary>Several drawings sharing one canvas origin, so differently colored parts line up.</summary>
        static void Layered(Transform root, Vector3 pos, float scale, params (Doodle d, Color c)[] layers)
        {
            var group = new GameObject("Layered").transform;
            group.SetParent(root, false);
            group.position = pos;
            var origin = new Vector2(0.5f, 0.05f);
            for (int i = 0; i < layers.Length; i++)
            {
                var built = DoodleBuilder.BuildScenery(layers[i].d, layers[i].c, scale, origin, 0.9f);
                built.root.transform.SetParent(group, false);
                built.root.transform.localPosition = new Vector3(0f, 0f, -0.01f * i); // later layers in front
            }
        }

        static void RoundTree(Transform root, Vector3 pos, float scale, System.Random rng)
        {
            var trunk = new Doodle();
            trunk.strokes.Add(T.Polyline(true,
                new Vector2(0.46f, 0.05f), new Vector2(0.54f, 0.05f), new Vector2(0.53f, 0.45f), new Vector2(0.47f, 0.45f)));
            trunk.strokes.Add(T.Line(new Vector2(0.5f, 0.33f), new Vector2(0.4f, 0.45f))); // branch

            var crown = new Doodle();
            crown.strokes.Add(Blob(new Vector2(0.5f, 0.62f), 0.2f, 0.17f, 7, rng));
            Layered(root, pos, scale, (trunk, Trunk), (crown, rng.NextDouble() < 0.5 ? Leaf : DarkLeaf));
        }

        /// <summary>A round tree in a chosen colour: apple tree (red fruit), pink blossom or autumn orange.</summary>
        static void ColorTree(Transform root, Vector3 pos, float scale, System.Random rng, Color crown, Color? fruit)
        {
            var trunk = new Doodle();
            trunk.strokes.Add(T.Polyline(true,
                new Vector2(0.46f, 0.05f), new Vector2(0.54f, 0.05f), new Vector2(0.53f, 0.45f), new Vector2(0.47f, 0.45f)));
            trunk.strokes.Add(T.Line(new Vector2(0.5f, 0.33f), new Vector2(0.6f, 0.44f)));
            var top = new Doodle();
            top.strokes.Add(Blob(new Vector2(0.5f, 0.63f), 0.22f, 0.18f, 8, rng));
            if (fruit == null)
            {
                Layered(root, pos, scale, (trunk, Trunk), (top, crown));
                return;
            }
            var dots = new Doodle();
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f + (float)rng.NextDouble() * 0.5f, d = 0.06f + (float)rng.NextDouble() * 0.1f;
                dots.strokes.Add(T.Circle(new Vector2(0.5f + Mathf.Cos(a) * d * 1.2f, 0.63f + Mathf.Sin(a) * d), 0.022f));
            }
            Layered(root, pos, scale, (trunk, Trunk), (top, crown), (dots, fruit.Value));
        }

        /// <summary>A tall sunflower: thin stem, two leaves, a ring of yellow petals round a big brown middle.</summary>
        static void Sunflower(Transform root, Vector3 pos, float scale)
        {
            var stem = new Doodle();
            stem.strokes.Add(T.Line(new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.68f)));
            stem.strokes.Add(T.Ellipse(new Vector2(0.42f, 0.3f), 0.07f, 0.025f));
            stem.strokes.Add(T.Ellipse(new Vector2(0.58f, 0.42f), 0.07f, 0.025f));
            var petals = new Doodle();
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                petals.strokes.Add(T.Ellipse(new Vector2(0.5f + Mathf.Cos(a) * 0.1f, 0.78f + Mathf.Sin(a) * 0.1f), 0.045f, 0.045f));
            }
            var middle = new Doodle();
            middle.strokes.Add(T.Circle(new Vector2(0.5f, 0.78f), 0.07f));
            Layered(root, pos, scale, (stem, Leaf), (petals, Sun), (middle, SeedBrown));
        }

        /// <summary>A tulip: stem, one long leaf and a cup-shaped bloom with three points.</summary>
        static void Tulip(Transform root, Vector3 pos, float scale, Color petal)
        {
            var stem = new Doodle();
            stem.strokes.Add(T.Line(new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.45f)));
            stem.strokes.Add(T.Polyline(true, new Vector2(0.5f, 0.08f), new Vector2(0.4f, 0.3f), new Vector2(0.49f, 0.2f)));
            var bloom = new Doodle();
            bloom.strokes.Add(T.Polyline(true,
                new Vector2(0.42f, 0.6f), new Vector2(0.45f, 0.48f), new Vector2(0.5f, 0.45f), new Vector2(0.55f, 0.48f),
                new Vector2(0.58f, 0.6f), new Vector2(0.55f, 0.55f), new Vector2(0.5f, 0.62f), new Vector2(0.45f, 0.55f)));
            Layered(root, pos, scale, (stem, Leaf), (bloom, petal));
        }

        /// <summary>A storybook toadstool: cream stalk, red cap with white spots.</summary>
        static void Toadstool(Transform root, Vector3 pos, float scale)
        {
            var stalk = new Doodle();
            stalk.strokes.Add(T.Polyline(true,
                new Vector2(0.46f, 0.05f), new Vector2(0.54f, 0.05f), new Vector2(0.53f, 0.25f), new Vector2(0.47f, 0.25f)));
            var cap = new Doodle();
            var dome = new Stroke();
            for (int i = 0; i <= 16; i++)
            {
                float a = Mathf.PI * i / 16f;
                dome.points.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.16f, 0.24f + Mathf.Sin(a) * 0.13f));
            }
            dome.points.Add(dome.points[0]);
            cap.strokes.Add(dome);
            var spots = new Doodle();
            spots.strokes.Add(T.Circle(new Vector2(0.44f, 0.3f), 0.025f));
            spots.strokes.Add(T.Circle(new Vector2(0.55f, 0.33f), 0.03f));
            spots.strokes.Add(T.Circle(new Vector2(0.5f, 0.27f), 0.018f));
            Layered(root, pos, scale, (stalk, Cream), (cap, Mushroom), (spots, Color.white));
        }

        /// <summary>A little bunch of flowers of mixed colours around a point.</summary>
        static void FlowerPatch(Transform root, Vector3 centre, System.Random rng, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var pos = centre + new Vector3((float)rng.NextDouble() * 1.6f - 0.8f, 0f, (float)rng.NextDouble() * 0.9f - 0.45f);
                var petal = Petals[rng.Next(Petals.Length)];
                float scale = 0.35f + (float)rng.NextDouble() * 0.2f;
                if (rng.NextDouble() < 0.35) Tulip(root, pos, scale, petal);
                else Flower(root, pos, scale, petal);
            }
        }

        static void PineTree(Transform root, Vector3 pos, float scale)
        {
            var trunk = new Doodle();
            trunk.strokes.Add(T.Polyline(true,
                new Vector2(0.47f, 0.05f), new Vector2(0.53f, 0.05f), new Vector2(0.53f, 0.2f), new Vector2(0.47f, 0.2f)));
            var needles = new Doodle();
            for (int i = 0; i < 3; i++)
            {
                float y = 0.18f + i * 0.17f, w = 0.2f - i * 0.04f;
                needles.strokes.Add(T.Polyline(true,
                    new Vector2(0.5f - w, y), new Vector2(0.5f + w, y), new Vector2(0.5f, y + 0.28f)));
            }
            Layered(root, pos, scale, (trunk, Trunk), (needles, DarkLeaf));
        }

        static void Flower(Transform root, Vector3 pos, float scale, Color petal)
        {
            var stem = new Doodle();
            stem.strokes.Add(T.Line(new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.45f)));
            stem.strokes.Add(T.Ellipse(new Vector2(0.43f, 0.22f), 0.06f, 0.025f)); // leaf
            var petals = new Doodle();
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                petals.strokes.Add(T.Circle(new Vector2(0.5f + Mathf.Cos(a) * 0.07f, 0.52f + Mathf.Sin(a) * 0.07f), 0.05f));
            }
            var heart = new Doodle();
            heart.strokes.Add(T.Circle(new Vector2(0.5f, 0.52f), 0.04f));
            Layered(root, pos, scale, (stem, Leaf), (petals, petal), (heart, Sun));
        }

        static void BuildGrass(Transform root)
        {
            var grass = GameObject.CreatePrimitive(PrimitiveType.Plane);
            grass.name = "Grass";
            Object.Destroy(grass.GetComponent<Collider>());
            grass.transform.SetParent(root, false);
            grass.transform.position = new Vector3(0f, -0.02f, 6f);
            grass.transform.localScale = new Vector3(10f, 1f, 5f); // ends at z = 31, behind the hills
            var mat = DoodleBuilder.TextureMaterial(GrassTexture());
            mat.mainTextureScale = new Vector2(14f, 9f);
            grass.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Texture2D GrassTexture()
        {
            const int n = 128;
            var px = new Color32[n * n];
            var rng = new System.Random(11);
            var baseCol = new Color(0.66f, 0.86f, 0.5f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float shade = 0.92f + 0.08f * Mathf.PerlinNoise(x * 0.07f, y * 0.07f) + (float)rng.NextDouble() * 0.04f;
                    float hatch = Mathf.Sin((x - y) * 0.7f) > 0.85f ? 0.9f : 1f; // crayon strokes
                    px[y * n + x] = baseCol * (shade * hatch);
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        // ---------------------------------------------------------------- shapes (canvas units)

        /// <summary>A lumpy closed outline: a circle with bumps, like a crayon tree crown or bush.</summary>
        static Stroke Blob(Vector2 c, float rx, float ry, int bumps, System.Random rng)
        {
            var s = new Stroke();
            const int steps = 56;
            float phase = (float)rng.NextDouble() * 6f;
            for (int i = 0; i <= steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                float bump = 1f + 0.12f * Mathf.Abs(Mathf.Sin(a * bumps * 0.5f + phase));
                s.points.Add(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry) * bump);
            }
            s.points[s.points.Count - 1] = s.points[0];
            return s;
        }

        static Doodle HillShape(System.Random rng)
        {
            var d = new Doodle();
            var s = new Stroke();
            float h = 0.25f + (float)rng.NextDouble() * 0.15f;
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                s.points.Add(new Vector2(t, 0.05f + Mathf.Sin(t * Mathf.PI) * h));
            }
            s.points.Add(new Vector2(0f, 0.05f));
            d.strokes.Add(s);
            return d;
        }

        static Doodle SunShape()
        {
            var d = new Doodle();
            d.strokes.Add(T.Circle(new Vector2(0.5f, 0.5f), 0.14f));
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                d.strokes.Add(T.Line(new Vector2(0.5f, 0.5f) + dir * 0.18f, new Vector2(0.5f, 0.5f) + dir * 0.26f));
            }
            d.strokes.Add(T.Arc(new Vector2(0.5f, 0.49f), 0.07f, 0.05f, 200f, 340f)); // smile
            return d;
        }

        static Doodle CloudShape(System.Random rng)
        {
            var d = new Doodle();
            var s = new Stroke();
            // flat bottom, three or four puffs on top
            int puffs = 3 + rng.Next(2);
            float w = 0.12f * puffs;
            float x0 = 0.5f - w * 0.5f;
            s.points.Add(new Vector2(x0, 0.3f));
            s.points.Add(new Vector2(x0 + w, 0.3f));
            for (int p = puffs - 1; p >= 0; p--)
            {
                float cx = x0 + (p + 0.5f) * (w / puffs);
                float r = w / puffs * (0.6f + 0.25f * (float)rng.NextDouble());
                for (int i = 0; i <= 10; i++)
                {
                    float a = Mathf.Lerp(0f, Mathf.PI, i / 10f);
                    s.points.Add(new Vector2(cx + Mathf.Cos(a) * r, 0.3f + Mathf.Sin(a) * r * 1.2f));
                }
            }
            s.points.Add(new Vector2(x0, 0.3f));
            d.strokes.Add(s);
            return d;
        }

        static Doodle FenceShape()
        {
            var d = new Doodle();
            foreach (float x in new[] { 0.2f, 0.5f, 0.8f })
                d.strokes.Add(T.Polyline(true,
                    new Vector2(x - 0.06f, 0.05f), new Vector2(x + 0.06f, 0.05f), new Vector2(x + 0.06f, 0.5f),
                    new Vector2(x, 0.58f), new Vector2(x - 0.06f, 0.5f)));
            d.strokes.Add(T.Polyline(true,
                new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.18f), new Vector2(0.95f, 0.24f), new Vector2(0.05f, 0.24f)));
            d.strokes.Add(T.Polyline(true,
                new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.36f), new Vector2(0.95f, 0.42f), new Vector2(0.05f, 0.42f)));
            return d;
        }

        static Doodle BushShape(System.Random rng)
        {
            var d = new Doodle();
            d.strokes.Add(Blob(new Vector2(0.5f, 0.2f), 0.22f, 0.13f, 6, rng));
            return d;
        }

        static Doodle TuftShape(System.Random rng)
        {
            var d = new Doodle();
            d.strokes.Add(T.Polyline(false,
                new Vector2(0.38f, 0.05f), new Vector2(0.3f, 0.3f), new Vector2(0.45f, 0.05f), new Vector2(0.5f, 0.38f),
                new Vector2(0.55f, 0.05f), new Vector2(0.7f, 0.28f), new Vector2(0.62f, 0.05f)));
            return d;
        }
    }

    /// <summary>Slowly drifts a cloud sideways and wraps it around.</summary>
    public class Drift : MonoBehaviour
    {
        public float speed = 0.25f;

        void Update()
        {
            var p = transform.position;
            p.x += speed * Time.deltaTime;
            if (p.x > 48f) p.x = -48f;
            transform.position = p;
        }
    }
}
