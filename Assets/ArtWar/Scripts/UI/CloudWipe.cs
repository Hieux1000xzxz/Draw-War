using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArtWar
{
    /// <summary>
    /// The screen change: big puffy clouds roll in, from below or from above, row after row, until they cover the
    /// whole screen on their own (no backdrop behind them). The change happens behind them, then the clouds part
    /// to both sides like curtains, the left ones drifting off left and the right ones off right. While they cover
    /// the screen each one bobs gently. Lives across scene reloads so it can carry the player from scene to scene.
    /// </summary>
    public class CloudWipe : MonoBehaviour
    {
        // a cloud's size; spacing is chosen so neighbouring clouds overlap well and leave no gap
        const float CloudW = 1300f, CloudH = 950f, ColSpacing = 800f;
        const int RowCount = 3;
        const float InTime = 0.6f, RowStagger = 0.14f, OutTime = 0.7f;

        static CloudWipe instance;
        readonly List<Image> pool = new List<Image>();
        readonly List<(Image img, Vector2 home, float phase)> active = new List<(Image, Vector2, float)>();
        RectTransform root;

        public static bool Busy { get; private set; }

        static CloudWipe Instance
        {
            get
            {
                if (instance == null) instance = Create();
                return instance;
            }
        }

        /// <summary>Clouds roll in, <paramref name="change"/> happens behind them, the clouds part to the sides.</summary>
        public static void Play(System.Action change)
        {
            if (Busy) return;
            Instance.StartCoroutine(Instance.Run(change));
        }

        static readonly Color[] Tints =
        {
            Color.white, Color.white, new Color(1f, 0.92f, 0.88f), new Color(0.9f, 0.88f, 1f), new Color(0.84f, 0.91f, 1f),
        };

        IEnumerator Run(System.Action change)
        {
            Busy = true;
            Canvas.ForceUpdateCanvases();
            var size = ((RectTransform)transform).rect.size;
            float W = size.x, H = size.y;
            var rng = new System.Random(Time.frameCount);

            // lay the clouds out: three rows, the middle row shifted half a step, enough columns for this screen
            active.Clear();
            var rows = new List<int>();
            int used = 0;
            for (int row = 0; row < RowCount; row++)
            {
                float y = H * (0.15f + 0.35f * row);
                float shift = row % 2 == 1 ? ColSpacing * 0.5f : 0f;
                int half = Mathf.CeilToInt((W * 0.5f + CloudW * 0.4f) / ColSpacing);
                for (int c = -half; c <= half; c++)
                {
                    float x = W * 0.5f + c * ColSpacing + shift;
                    if (x < -CloudW * 0.45f || x > W + CloudW * 0.45f) continue;
                    var img = Take(used++);
                    img.color = Tints[rng.Next(Tints.Length)];
                    img.rectTransform.localScale = new Vector3(rng.NextDouble() < 0.5 ? -1f : 1f, 1f, 1f);
                    img.sprite = CloudSprite(rng.Next(CloudSprites));
                    img.gameObject.SetActive(true);
                    active.Add((img, new Vector2(x + (float)(rng.NextDouble() - 0.5) * 80f, y + (float)(rng.NextDouble() - 0.5) * 60f),
                        (float)rng.NextDouble() * 6.28f));
                    rows.Add(row);
                }
            }
            for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);

            // in: from below (bottom row first) or from above (top row first)
            bool fromBelow = rng.NextDouble() < 0.5;
            float travel = H + CloudH;
            Sfx.Play(Sound.Swish, 0.3f, 0.5f);
            float total = InTime + RowStagger * (RowCount - 1);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    int order = fromBelow ? rows[i] : RowCount - 1 - rows[i];
                    float k = Ease(Mathf.Clamp01((t - order * RowStagger) / InTime), true);
                    Place(i, new Vector2(0f, (fromBelow ? -travel : travel) * (1f - k)));
                }
                yield return null;
            }
            for (int i = 0; i < active.Count; i++) Place(i, Vector2.zero);

            change?.Invoke();
            // hold a moment (the clouds keep bobbing) while a reloaded scene builds its first frames
            for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < active.Count; i++) Place(i, Vector2.zero);
                yield return null;
            }

            // out: part to both sides, each cloud drifting away on its own side of the screen
            Sfx.Play(Sound.Swish, 0.25f, 0.7f);
            var delay = new float[active.Count];
            for (int i = 0; i < active.Count; i++) delay[i] = (float)rng.NextDouble() * 0.12f;
            total = OutTime + 0.12f;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    float side = active[i].home.x < W * 0.5f ? -1f : 1f;
                    float k = Ease(Mathf.Clamp01((t - delay[i]) / OutTime), false);
                    Place(i, new Vector2(side * (W * 0.5f + CloudW) * k, 0f));
                }
                yield return null;
            }
            foreach (var c in active) c.img.gameObject.SetActive(false);
            active.Clear();
            Busy = false;
        }

        /// <summary>In: decelerate softly into place. Out: start gently, then sweep away.</summary>
        static float Ease(float k, bool arriving) => arriving ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k * (3f - 2f * k);

        /// <summary>A cloud at its resting place plus the animation offset plus its own gentle bobbing.</summary>
        void Place(int i, Vector2 offset)
        {
            var (img, home, phase) = active[i];
            float t = Time.unscaledTime;
            var bob = new Vector2(Mathf.Sin(t * 0.8f + phase) * 25f, Mathf.Sin(t * 1.15f + phase * 1.7f) * 16f);
            img.rectTransform.anchoredPosition = home + offset + bob;
        }

        Image Take(int index)
        {
            while (pool.Count <= index)
            {
                var c = new GameObject("Cloud", typeof(RectTransform)).AddComponent<Image>();
                c.rectTransform.SetParent(root, false);
                c.rectTransform.anchorMin = c.rectTransform.anchorMax = Vector2.zero;
                c.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                c.rectTransform.sizeDelta = new Vector2(CloudW, CloudH);
                c.raycastTarget = true; // block clicks while the clouds are up
                pool.Add(c);
            }
            return pool[index];
        }

        // ---------------------------------------------------------------- the cloud picture

        const int CloudSprites = 4;
        static readonly Sprite[] cloudSprites = new Sprite[CloudSprites];

        /// <summary>
        /// A big puffy cloud, one of four shapes: a solid oval body ringed with round bumps all the way round (bigger
        /// on top), one silhouette with a pencil outline on the outside only, white with a soft blue-grey underside.
        /// The solid body is what lets a few of them cover the screen with no backdrop.
        /// </summary>
        static Sprite CloudSprite(int variant)
        {
            if (cloudSprites[variant] != null) return cloudSprites[variant];
            const int w = 512, h = 374;
            var rng = new System.Random(23 + variant * 37);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var centre = new Vector2(w * 0.5f, h * 0.48f);
            float rx = w * 0.34f, ry = h * 0.3f;
            var bumps = new List<Vector3>(); // x, y, radius
            int count = 10 + variant;
            for (int i = 0; i < count; i++)
            {
                float a = (i + R(-0.2f, 0.2f)) / count * Mathf.PI * 2f;
                float top = Mathf.Max(0f, Mathf.Sin(a));                     // bumps on top are the biggest
                float rad = Mathf.Lerp(48f, 92f, top) * R(0.85f, 1.1f);
                var on = centre + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                bumps.Add(new Vector3(on.x, on.y, rad));
            }

            float Dist(Vector2 p)
            {
                var q = p - centre;
                float d = (new Vector2(q.x / rx, q.y / ry).magnitude - 1f) * Mathf.Min(rx, ry);
                foreach (var b in bumps) d = Mathf.Min(d, Vector2.Distance(p, new Vector2(b.x, b.y)) - b.z);
                return d;
            }

            var px = new Color[w * h];
            float wobble = R(0f, 6f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Dist(p) + Mathf.Sin(x * 0.05f + y * 0.03f + wobble) * 1.2f; // a hand-drawn line isn't perfect
                    float inside = Mathf.Clamp01(0.5f - d);
                    if (inside <= 0f) continue;
                    float low = Mathf.Clamp01((centre.y - p.y) / (ry + 60f));     // shade the underside
                    Color c = Color.Lerp(Color.white, new Color(0.82f, 0.86f, 0.95f), low * 0.8f);
                    float rim = Mathf.Clamp01(3f - Mathf.Abs(d + 2.6f));             // pencil line along the outside
                    c = Color.Lerp(c, new Color(0.5f, 0.56f, 0.7f), rim * 0.85f);
                    c.a = inside;
                    px[y * w + x] = c;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Cloud " + variant };
            tex.SetPixels(px);
            tex.Apply();
            cloudSprites[variant] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return cloudSprites[variant];
        }

        static CloudWipe Create()
        {
            var go = UiKit.Canvas("Cloud Wipe", 100);
            DontDestroyOnLoad(go);
            var wipe = go.AddComponent<CloudWipe>();
            wipe.root = UiKit.Rect(go.transform, "Clouds", Vector2.zero, Vector2.zero);
            UiKit.Stretch(wipe.root);
            return wipe;
        }
    }
}
