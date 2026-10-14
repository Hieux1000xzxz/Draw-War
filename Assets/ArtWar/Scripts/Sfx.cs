using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtWar
{
    public enum Sound
    {
        Click, Error, Boing, Twinkle, Swish, Bonk, Squeak, Pew, Fwip, Cannon, Boom, BombWhistle, Missile,
        Fire, Heal, Poof, Rip, Fanfare, Victory, Defeat, Roar, Beep, Clank, Boo,
        Scream, Yelp, WarCry, Giggle,
    }

    /// <summary>
    /// Cute cartoon sound effects and a bouncy background tune, all synthesized at startup
    /// (no audio files needed). Call <see cref="Play"/> from anywhere; voices are pooled and throttled.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 22050;
        const int Voices = 16;

        static Sfx instance;
        readonly Dictionary<Sound, AudioClip> clips = new Dictionary<Sound, AudioClip>();
        readonly Dictionary<Sound, float> lastPlayed = new Dictionary<Sound, float>();
        AudioSource[] voices;
        AudioSource music, scribble;
        int nextVoice;
        float scribbleTarget;

        public static float MasterVolume = 0.8f;

        static float musicVolume = 1f;

        /// <summary>Music level 0..1 (on top of <see cref="MasterVolume"/>), applied immediately.</summary>
        public static float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                if (instance != null && instance.music != null) instance.music.volume = 0.22f * MasterVolume * musicVolume;
            }
        }

        /// <summary>Restarts the music after a game over silenced it.</summary>
        public static void PlayMusic() { if (instance != null && !instance.music.isPlaying) instance.music.Play(); }

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("Sfx");
            instance = go.AddComponent<Sfx>();
        }

        void Awake()
        {
            instance = this;
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
            Build();

            music = gameObject.AddComponent<AudioSource>();
            music.clip = BuildMusic();
            music.loop = true;
            music.volume = 0.22f * MasterVolume * musicVolume;
            music.Play();

            scribble = gameObject.AddComponent<AudioSource>();
            scribble.clip = Make(Scribble(1.2f));
            scribble.loop = true;
            scribble.volume = 0f;
            scribble.Play();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            // the pen scratch fades in while the pen moves and out when it stops
            scribble.volume = Mathf.MoveTowards(scribble.volume, scribbleTarget * 0.35f * MasterVolume, Time.unscaledDeltaTime * 4f);
            scribbleTarget = Mathf.MoveTowards(scribbleTarget, 0f, Time.unscaledDeltaTime * 6f);
        }

        /// <summary>Plays a sound with a little random pitch so repeats don't sound robotic.</summary>
        public static void Play(Sound s, float volume = 1f, float pitch = 1f)
        {
            if (instance == null || !instance.clips.TryGetValue(s, out var clip)) return;
            // throttle: the same sound at most every 40 ms (rifle bursts, many hits at once)
            if (instance.lastPlayed.TryGetValue(s, out float last) && Time.unscaledTime - last < 0.04f) return;
            instance.lastPlayed[s] = Time.unscaledTime;

            var v = instance.voices[instance.nextVoice];
            instance.nextVoice = (instance.nextVoice + 1) % Voices;
            v.pitch = pitch * UnityEngine.Random.Range(0.93f, 1.08f);
            v.PlayOneShot(clip, volume * MasterVolume);
        }

        /// <summary>Keeps the pen-scratch loop audible while the player is drawing.</summary>
        public static void Scratch() { if (instance != null) instance.scribbleTarget = 1f; }

        /// <summary>Music speeds up a little for the battle.</summary>
        public static void SetBattleMood(bool battle) { if (instance != null) instance.music.pitch = battle ? 1.12f : 1f; }

        public static void StopMusic() { if (instance != null) instance.music.Stop(); }

        // ---------------------------------------------------------------- sound design

        void Build()
        {
            clips[Sound.Click] = Make(Tone(900f, 1300f, 0.05f, Wave.Sine, 0.002f));
            clips[Sound.Error] = Make(Concat(Tone(330f, 300f, 0.09f, Wave.Square, 0.005f, 0.25f), Tone(250f, 220f, 0.12f, Wave.Square, 0.005f, 0.25f)));
            clips[Sound.Boing] = Make(Mix(Tone(180f, 520f, 0.35f, Wave.Sine, 0.005f, 1f, 18f, 0.08f), Tone(360f, 1040f, 0.25f, Wave.Triangle, 0.005f, 0.3f)));
            clips[Sound.Twinkle] = Make(Concat(Tone(1568f, 1568f, 0.07f, Wave.Sine, 0.002f, 0.5f), Tone(2093f, 2093f, 0.07f, Wave.Sine, 0.002f, 0.45f),
                Tone(2637f, 2637f, 0.12f, Wave.Sine, 0.002f, 0.4f)));
            clips[Sound.Swish] = Make(Noise(0.18f, 0.12f, 0.6f, 0.4f));
            clips[Sound.Bonk] = Make(Mix(Tone(320f, 140f, 0.12f, Wave.Triangle, 0.002f), Noise(0.05f, 0.25f, 0.05f, 0.4f)));
            clips[Sound.Squeak] = Make(Tone(1400f, 2200f, 0.09f, Wave.Sine, 0.005f, 0.35f, 30f, 0.04f));
            clips[Sound.Pew] = Make(Tone(1500f, 300f, 0.11f, Wave.Square, 0.002f, 0.3f));
            clips[Sound.Fwip] = Make(Mix(Noise(0.12f, 0.5f, 0.9f, 0.35f), Tone(700f, 1100f, 0.1f, Wave.Sine, 0.005f, 0.2f)));
            clips[Sound.Cannon] = Make(Mix(Noise(0.35f, 0.25f, 0.03f, 0.9f), Tone(110f, 45f, 0.3f, Wave.Sine, 0.003f)));
            clips[Sound.Boom] = Make(Mix(Noise(0.6f, 0.3f, 0.02f, 1f), Tone(80f, 35f, 0.5f, Wave.Sine, 0.003f, 0.9f)));
            clips[Sound.BombWhistle] = Make(Tone(1800f, 500f, 0.7f, Wave.Sine, 0.05f, 0.35f, 6f, 0.01f));
            clips[Sound.Missile] = Make(Mix(Noise(0.45f, 0.2f, 0.5f, 0.5f), Tone(400f, 900f, 0.4f, Wave.Triangle, 0.02f, 0.25f)));
            clips[Sound.Fire] = Make(Noise(0.5f, 0.15f, 0.05f, 0.6f));
            clips[Sound.Heal] = Make(Concat(Tone(1047f, 1047f, 0.08f, Wave.Sine, 0.003f, 0.5f), Tone(1319f, 1319f, 0.08f, Wave.Sine, 0.003f, 0.5f),
                Tone(1568f, 1568f, 0.16f, Wave.Sine, 0.003f, 0.5f)));
            clips[Sound.Poof] = Make(Mix(Noise(0.3f, 0.6f, 0.08f, 0.6f), Tone(600f, 150f, 0.25f, Wave.Triangle, 0.003f, 0.5f)));
            clips[Sound.Rip] = Make(Noise(0.22f, 0.9f, 0.5f, 0.45f, 60f));
            clips[Sound.Fanfare] = Make(Concat(Tone(523f, 523f, 0.12f, Wave.Square, 0.005f, 0.3f), Tone(659f, 659f, 0.12f, Wave.Square, 0.005f, 0.3f),
                Tone(784f, 784f, 0.12f, Wave.Square, 0.005f, 0.3f), Tone(1047f, 1047f, 0.45f, Wave.Square, 0.005f, 0.3f, 6f, 0.01f)));
            clips[Sound.Victory] = Make(Concat(Tone(784f, 784f, 0.1f, Wave.Triangle, 0.005f), Tone(784f, 784f, 0.1f, Wave.Triangle, 0.005f),
                Tone(784f, 784f, 0.1f, Wave.Triangle, 0.005f), Tone(1047f, 1047f, 0.3f, Wave.Triangle, 0.005f),
                Tone(988f, 988f, 0.12f, Wave.Triangle, 0.005f), Tone(1047f, 1047f, 0.6f, Wave.Triangle, 0.005f, 1f, 6f, 0.01f)));
            // robot: "bee-boop" and a metal clank (inharmonic partials ring like struck metal)
            clips[Sound.Beep] = Make(Concat(Tone(880f, 880f, 0.09f, Wave.Square, 0.003f, 0.3f), Tone(587f, 587f, 0.14f, Wave.Square, 0.003f, 0.3f),
                Tone(1175f, 1175f, 0.1f, Wave.Square, 0.003f, 0.25f)));
            clips[Sound.Clank] = Make(Mix(Tone(523f, 515f, 0.35f, Wave.Sine, 0.001f, 0.45f), Tone(1397f, 1390f, 0.25f, Wave.Sine, 0.001f, 0.3f),
                Tone(2213f, 2200f, 0.18f, Wave.Sine, 0.001f, 0.2f), Noise(0.04f, 0.9f, 0.6f, 0.5f), Tone(90f, 50f, 0.25f, Wave.Sine, 0.002f, 0.7f)));
            // dragon roar: a low growling saw with a wobble, rising then falling, over a breathy rumble
            clips[Sound.Roar] = Make(Mix(
                Concat(Tone(95f, 150f, 0.25f, Wave.Saw, 0.03f, 0.55f, 11f, 0.08f), Tone(150f, 70f, 0.75f, Wave.Saw, 0.01f, 0.6f, 9f, 0.1f)),
                Noise(1f, 0.08f, 0.03f, 0.7f)));
            // ghost "hoooo": a wavering, sliding whistle over a breathy hiss
            clips[Sound.Boo] = Make(Mix(Concat(Tone(392f, 440f, 0.2f, Wave.Sine, 0.08f, 0.35f, 6f, 0.03f),
                Tone(440f, 247f, 0.8f, Wave.Triangle, 0.01f, 0.35f, 6f, 0.05f)), Noise(1f, 0.05f, 0.04f, 0.5f)));
            // Cockroach-style voices: raspy, nasal cartoon yells, a buzzing voice through vowel formants.
            // death scream "AAAAÉÉÉ!": shoots up, wobbles, falls away
            clips[Sound.Scream] = Make(Voice(0.75f, 420f, 720f, 380f, 11f, 0.07f, Vowel.A, Vowel.E, 1f));
            // hit yelp "ÁI!"
            clips[Sound.Yelp] = Make(Voice(0.2f, 480f, 680f, 520f, 0f, 0f, Vowel.A, Vowel.I, 0.95f));
            // battle cry "YAAAAA!"
            clips[Sound.WarCry] = Make(Mix(Voice(0.85f, 330f, 420f, 360f, 7f, 0.05f, Vowel.I, Vowel.A, 0.75f),
                Voice(0.85f, 410f, 500f, 440f, 6f, 0.05f, Vowel.I, Vowel.A, 0.5f)));
            // giggle "hê-hê-hê-hê"
            clips[Sound.Giggle] = Make(Concat(
                Voice(0.085f, 560f, 620f, 540f, 0f, 0f, Vowel.E, Vowel.E, 0.8f), new float[Rate / 28],
                Voice(0.085f, 530f, 590f, 510f, 0f, 0f, Vowel.E, Vowel.E, 0.8f), new float[Rate / 28],
                Voice(0.085f, 500f, 560f, 480f, 0f, 0f, Vowel.E, Vowel.E, 0.75f), new float[Rate / 28],
                Voice(0.12f, 470f, 520f, 400f, 0f, 0f, Vowel.E, Vowel.A, 0.75f)));
            // the sad trombone: wah, wah, wah, waaah
            clips[Sound.Defeat] = Make(Concat(Tone(392f, 370f, 0.35f, Wave.Saw, 0.02f, 0.3f), Tone(370f, 349f, 0.35f, Wave.Saw, 0.02f, 0.3f),
                Tone(349f, 330f, 0.35f, Wave.Saw, 0.02f, 0.3f), Tone(330f, 311f, 1f, Wave.Saw, 0.02f, 0.3f, 5f, 0.02f)));
        }

        enum Wave { Sine, Square, Triangle, Saw }

        /// <summary>Vowel formants (F1, F2), scaled up ×1.2: a small, nasal cartoon throat.</summary>
        enum Vowel { I, E, A }

        static Vector2 Formants(Vowel v)
        {
            switch (v)
            {
                case Vowel.I: return new Vector2(300f, 2300f) * 1.2f;
                case Vowel.E: return new Vector2(530f, 1850f) * 1.2f;
                default: return new Vector2(800f, 1250f) * 1.2f;
            }
        }

        /// <summary>
        /// A tiny cartoon voice: a buzzy pulse whose pitch goes start → peak (at 30%) → end, shaped into a vowel by two
        /// resonators that glide from one vowel to another, with optional vibrato for a wobbly scream.
        /// </summary>
        static float[] Voice(float dur, float fStart, float fPeak, float fEnd, float vibratoHz, float vibratoDepth,
            Vowel from, Vowel to, float gain)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            var s = new float[n];
            Vector2 fa = Formants(from), fb = Formants(to);
            float phase = 0f;
            float y1a = 0f, y2a = 0f, y1b = 0f, y2b = 0f;
            var rng = new System.Random(n + (int)fStart);
            float jitter = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f0 = t < 0.3f ? Mathf.Lerp(fStart, fPeak, t / 0.3f) : Mathf.Lerp(fPeak, fEnd, (t - 0.3f) / 0.7f);
                f0 *= 1f + vibratoDepth * Mathf.Sin(i / (float)Rate * vibratoHz * Mathf.PI * 2f);
                // rasp: the pitch stumbles a little from cycle to cycle, like a hoarse cartoon throat
                jitter += 0.02f * (((float)rng.NextDouble() * 2f - 1f) - jitter);
                f0 *= 1f + jitter * 0.6f;
                phase += f0 / Rate;
                float p = phase - Mathf.Floor(phase);
                float source = p < 0.3f ? 1f : -0.43f; // narrow pulse: rich, buzzy, voice-like

                Vector2 f = Vector2.Lerp(fa, fb, Mathf.SmoothStep(0f, 1f, t));
                float outA = Resonate(source, f.x, 160f, ref y1a, ref y2a);
                float outB = Resonate(source, f.y, 220f, ref y1b, ref y2b);

                float env = Mathf.Min(1f, i / (0.012f * Rate)) * Mathf.Min(1f, (n - i) / (0.06f * Rate));
                float growl = 0.75f + 0.25f * Mathf.Sin(i / (float)Rate * 38f * Mathf.PI * 2f); // gravelly flutter
                s[i] = (outA + outB * 0.7f) * env * growl;
            }
            // loud and clear: scale so the loudest moment hits the requested level
            float peak = 0f;
            foreach (var v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 1e-5f) for (int i = 0; i < n; i++) s[i] *= gain / peak;
            return s;
        }

        /// <summary>One step of a two-pole resonator at <paramref name="freq"/> Hz with the given bandwidth.</summary>
        static float Resonate(float x, float freq, float bandwidth, ref float y1, ref float y2)
        {
            float r = Mathf.Exp(-Mathf.PI * bandwidth / Rate);
            float a1 = 2f * r * Mathf.Cos(2f * Mathf.PI * freq / Rate);
            float a2 = -r * r;
            float y = (1f - r) * x + a1 * y1 + a2 * y2;
            y2 = y1;
            y1 = y;
            return y * 3f;
        }

        static float Osc(Wave w, float phase)
        {
            float p = phase - Mathf.Floor(phase);
            switch (w)
            {
                case Wave.Square: return p < 0.5f ? 0.6f : -0.6f;
                case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case Wave.Saw: return (2f * p - 1f) * 0.6f;
                default: return Mathf.Sin(p * Mathf.PI * 2f);
            }
        }

        /// <summary>A pitch glide with a quick attack and an exponential-ish decay; optional vibrato.</summary>
        static float[] Tone(float f0, float f1, float dur, Wave w, float attack, float gain = 0.8f,
            float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            var s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(f0, f1, t) * (1f + vibratoDepth * Mathf.Sin(i / (float)Rate * vibratoHz * Mathf.PI * 2f));
                phase += f / Rate;
                float env = Mathf.Min(1f, i / (attack * Rate + 1f)) * Mathf.Pow(1f - t, 1.6f);
                s[i] = Osc(w, phase) * env * gain;
            }
            return s;
        }

        /// <summary>Filtered noise: cutoff glides from c0 to c1 (0..1); optional amplitude crackle (Hz) for paper rips.</summary>
        static float[] Noise(float dur, float c0, float c1, float gain, float crackleHz = 0f)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            var s = new float[n];
            var rng = new System.Random(n);
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float a = Mathf.Lerp(c0, c1, t);
                y += a * ((float)rng.NextDouble() * 2f - 1f - y);
                float env = Mathf.Min(1f, i / (0.004f * Rate)) * Mathf.Pow(1f - t, 1.4f);
                if (crackleHz > 0f) env *= 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(i / (float)Rate * crackleHz * Mathf.PI));
                s[i] = y * env * gain * 2.2f;
            }
            return s;
        }

        static float[] Scribble(float dur)
        {
            // pencil on paper: bright noise with an irregular back-and-forth rhythm
            int n = Mathf.CeilToInt(dur * Rate);
            var s = new float[n];
            var rng = new System.Random(3);
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                y += 0.55f * ((float)rng.NextDouble() * 2f - 1f - y);
                float stroke = 0.55f + 0.45f * Mathf.Sin(t * 2f * Mathf.PI * 7f + Mathf.Sin(t * 13f) * 2f);
                s[i] = (((float)rng.NextDouble() * 2f - 1f) - y) * 0.35f * stroke;
            }
            return s;
        }

        static float[] Mix(params float[][] parts)
        {
            int n = 0;
            foreach (var p in parts) n = Math.Max(n, p.Length);
            var s = new float[n];
            foreach (var p in parts)
                for (int i = 0; i < p.Length; i++) s[i] += p[i];
            return s;
        }

        static float[] Concat(params float[][] parts)
        {
            var list = new List<float>();
            foreach (var p in parts) list.AddRange(p);
            return list.ToArray();
        }

        static AudioClip Make(float[] data)
        {
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("sfx", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---------------------------------------------------------------- music

        /// <summary>
        /// A bouncy 8-bar cartoon tune: oom-pah bass, a plucky pentatonic melody, a little xylophone
        /// echo and brushed hi-hats. 120 BPM, loops seamlessly.
        /// </summary>
        AudioClip BuildMusic()
        {
            const float bpm = 132f;
            float beat = 60f / bpm;
            int bars = 8, beatsPerBar = 4;
            int n = Mathf.CeilToInt(bars * beatsPerBar * beat * Rate);
            var s = new float[n];

            // chord roots per bar (C, A minor, F, G, C, A minor, D minor, G)
            float[] roots = { 130.81f, 110f, 87.31f, 98f, 130.81f, 110f, 73.42f, 98f };
            // melody in scale steps of C major pentatonic, one note per half beat, -1 = rest
            int[] melody =
            {
                0, 2, 4, 2, 5, -1, 4, -1,   4, 5, 7, 5, 4, -1, 2, -1,
                5, 4, 2, 0, 2, -1, 4, 5,    7, -1, 5, 4, 2, -1, -1, -1,
                0, 2, 4, 2, 5, -1, 7, -1,   9, 7, 5, 4, 5, -1, 4, -1,
                2, 4, 5, 4, 2, 0, 2, -1,    4, 2, 1, 2, 0, -1, -1, -1,
            };
            float[] scale = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21 };

            for (int bar = 0; bar < bars; bar++)
            {
                for (int b = 0; b < beatsPerBar; b++)
                {
                    float tBeat = (bar * beatsPerBar + b) * beat;
                    // oom (root) on 1 and 3, pah (fifth up an octave) on 2 and 4
                    float bassF = b % 2 == 0 ? roots[bar] : roots[bar] * 1.5f * 2f;
                    AddAt(s, Tone(bassF, bassF, beat * 0.8f, b % 2 == 0 ? Wave.Sine : Wave.Triangle, 0.005f, b % 2 == 0 ? 0.55f : 0.22f), tBeat);
                    // brushed hi-hat on the off-beats
                    AddAt(s, Noise(0.05f, 0.95f, 0.9f, 0.08f), tBeat + beat * 0.5f);
                }
                for (int h = 0; h < 8; h++)
                {
                    int step = melody[bar * 8 + h];
                    if (step < 0) continue;
                    float f = 523.25f * Mathf.Pow(2f, scale[step] / 12f);
                    float t = (bar * 8 + h) * beat * 0.5f;
                    AddAt(s, Tone(f, f, beat * 0.45f, Wave.Triangle, 0.003f, 0.32f), t);           // pluck
                    AddAt(s, Tone(f * 2f, f * 2f, beat * 0.3f, Wave.Sine, 0.002f, 0.08f), t + beat * 0.25f); // xylophone echo
                }
            }
            return Make(s);
        }

        static void AddAt(float[] dst, float[] src, float time)
        {
            int start = Mathf.RoundToInt(time * Rate);
            for (int i = 0; i < src.Length; i++) dst[(start + i) % dst.Length] += src[i]; // wraps so the loop seam is seamless
        }
    }
}
