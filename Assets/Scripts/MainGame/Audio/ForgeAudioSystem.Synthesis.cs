using UnityEngine;

namespace ForjaGame.Audio
{
    /// <summary>
    /// Procedural acoustic waveform synthesis for ForgeAudioSystem.
    /// Generates filtered metallic, steam, parchment, impact, creature, and ambient AudioClips at runtime.
    /// </summary>
    public partial class ForgeAudioSystem
    {
        private const int SampleRate = 22050;

        private AudioClip _procUiClick;
        private AudioClip _procUiTab;
        private AudioClip _procHammerStrike;
        private AudioClip _procChiselCut;
        private AudioClip _procPolishScrape;
        private AudioClip _procQuenchSizzle;
        private AudioClip _procPressCrush;
        private AudioClip _procFurnaceStoke;
        private AudioClip _procSwingLight;
        private AudioClip _procSwingHeavy;
        private AudioClip _procHitFlesh;
        private AudioClip _procHitStone;
        private AudioClip _procPickaxe;
        private AudioClip _procSpider;
        private AudioClip _procGolem;
        private AudioClip _procPlayerHurt;
        private AudioClip _procAltarChord;
        private AudioClip _procFireLoop;
        private AudioClip _procCaveLoop;
        private int _poolCursor;

        private void Play2D(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || _uiSource == null) return;
            _uiSource.pitch = pitch;
            _uiSource.PlayOneShot(clip, volume);
        }

        private void Play3D(AudioClip clip, Vector3 pos, float volume, float pitch)
        {
            if (clip == null || _pool.Count == 0) return;
            AudioSource src = _pool[_poolCursor];
            _poolCursor = (_poolCursor + 1) % _pool.Count;
            src.transform.position = pos;
            src.spatialBlend = 0.65f;
            src.minDistance = 2.5f;
            src.maxDistance = 28f;
            src.pitch = pitch;
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }

        private void BuildProceduralBank()
        {
            var rng = new System.Random(1337);
            _procUiClick = MakeClip("Proc_UIClick", 0.055f, (t, d) =>
                Mathf.Exp(-t * 68f) * (Mathf.Sin(t * 2f * Mathf.PI * 620f) * 0.55f + NextNoise(rng) * 0.35f));

            _procUiTab = MakeClip("Proc_UITab", 0.11f, (t, d) =>
                Mathf.Sin(Mathf.PI * (t / d)) * LowPassNoise(rng, 0.35f) * 0.55f);

            _procHammerStrike = MakeClip("Proc_Hammer", 0.42f, (t, d) =>
            {
                float ring = Mathf.Sin(t * 2f * Mathf.PI * 1040f) * 0.42f
                           + Mathf.Sin(t * 2f * Mathf.PI * 1580f) * 0.28f
                           + Mathf.Sin(t * 2f * Mathf.PI * 2490f) * 0.15f;
                float thud = Mathf.Sin(t * 2f * Mathf.PI * (150f - t * 180f)) * Mathf.Exp(-t * 32f) * 0.65f;
                float click = NextNoise(rng) * Mathf.Exp(-t * 90f) * 0.4f;
                return ring * Mathf.Exp(-t * 8.5f) + thud + click;
            });

            _procChiselCut = MakeClip("Proc_Chisel", 0.24f, (t, d) =>
                Mathf.Exp(-t * 16f) * (Mathf.Sin(t * 2f * Mathf.PI * 1820f) * 0.45f + NextNoise(rng) * 0.45f));

            _procPolishScrape = MakeClip("Proc_Polish", 0.20f, (t, d) =>
                Mathf.Sin(Mathf.PI * (t / d)) * (LowPassNoise(rng, 0.45f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 2900f) * 0.08f));

            _procQuenchSizzle = MakeClip("Proc_Quench", 0.85f, (t, d) =>
            {
                float env = Mathf.Sin(Mathf.Min(1f, t / 0.08f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 2.8f);
                float bubble = Mathf.Sin(t * 2f * Mathf.PI * (220f + Mathf.Sin(t * 38f) * 80f)) * 0.18f;
                return env * (LowPassNoise(rng, 0.65f) * 0.72f + bubble);
            });

            _procPressCrush = MakeClip("Proc_Press", 0.55f, (t, d) =>
                Mathf.Exp(-t * 5.5f) * (Mathf.Sin(t * 2f * Mathf.PI * 78f) * 0.7f + LowPassNoise(rng, 0.2f) * 0.4f));

            _procFurnaceStoke = MakeClip("Proc_Furnace", 0.45f, (t, d) =>
                Mathf.Sin(Mathf.PI * (t / d)) * LowPassNoise(rng, 0.22f) * 0.85f);

            _procSwingLight = MakeClip("Proc_Swing1H", 0.20f, (t, d) =>
                Mathf.Sin(Mathf.PI * (t / d)) * (LowPassNoise(rng, 0.35f) * 0.65f + Mathf.Sin(t * 2f * Mathf.PI * (520f - t * 1100f)) * 0.2f));

            _procSwingHeavy = MakeClip("Proc_Swing2H", 0.32f, (t, d) =>
                Mathf.Sin(Mathf.PI * (t / d)) * (LowPassNoise(rng, 0.18f) * 0.85f + Mathf.Sin(t * 2f * Mathf.PI * (240f - t * 380f)) * 0.35f));

            _procHitFlesh = MakeClip("Proc_HitFlesh", 0.18f, (t, d) =>
                Mathf.Exp(-t * 22f) * (Mathf.Sin(t * 2f * Mathf.PI * (135f - t * 260f)) * 0.75f + LowPassNoise(rng, 0.3f) * 0.45f));

            _procHitStone = MakeClip("Proc_HitStone", 0.25f, (t, d) =>
                Mathf.Exp(-t * 15f) * (Mathf.Sin(t * 2f * Mathf.PI * 195f) * 0.55f + Mathf.Sin(t * 2f * Mathf.PI * 880f) * 0.28f + NextNoise(rng) * 0.35f));

            _procPickaxe = MakeClip("Proc_Pickaxe", 0.30f, (t, d) =>
                Mathf.Exp(-t * 12f) * (Mathf.Sin(t * 2f * Mathf.PI * 1320f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 2120f) * 0.25f + NextNoise(rng) * 0.35f));

            _procSpider = MakeClip("Proc_Spider", 0.28f, (t, d) =>
            {
                float mod = Mathf.Sin(t * 2f * Mathf.PI * 42f);
                return Mathf.Sin(Mathf.PI * (t / d)) * (Mathf.Sin(t * 2f * Mathf.PI * (760f + mod * 240f)) * 0.35f + LowPassNoise(rng, 0.5f) * 0.55f);
            });

            _procGolem = MakeClip("Proc_Golem", 0.55f, (t, d) =>
            {
                float rumble = Mathf.Sin(t * 2f * Mathf.PI * (68f + Mathf.Sin(t * 18f) * 14f)) * 0.7f;
                return Mathf.Sin(Mathf.PI * (t / d)) * (rumble + LowPassNoise(rng, 0.16f) * 0.45f);
            });

            _procPlayerHurt = MakeClip("Proc_PlayerHurt", 0.22f, (t, d) =>
                Mathf.Exp(-t * 14f) * (Mathf.Sin(t * 2f * Mathf.PI * (190f - t * 240f)) * 0.7f + LowPassNoise(rng, 0.25f) * 0.35f));

            _procAltarChord = MakeClip("Proc_Altar", 1.4f, (t, d) =>
            {
                float env = Mathf.Sin(Mathf.Min(1f, t / 0.12f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 1.6f);
                return env * (Mathf.Sin(t * 2f * Mathf.PI * 293.66f) * 0.35f
                            + Mathf.Sin(t * 2f * Mathf.PI * 440.00f) * 0.30f
                            + Mathf.Sin(t * 2f * Mathf.PI * 587.33f) * 0.22f);
            });

            _procFireLoop = MakeLoopClip("Proc_FireLoop", 2.5f, rng, true);
            _procCaveLoop = MakeLoopClip("Proc_CaveLoop", 3.0f, rng, false);
        }

        private float _lpState;

        private float NextNoise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

        private float LowPassNoise(System.Random rng, float alpha)
        {
            _lpState += alpha * (NextNoise(rng) - _lpState);
            return _lpState;
        }

        private AudioClip MakeClip(string clipName, float duration, System.Func<float, float, float> gen)
        {
            _lpState = 0f;
            int samples = Mathf.Max(64, Mathf.RoundToInt(SampleRate * duration));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                data[i] = Mathf.Clamp(gen(t, duration), -0.95f, 0.95f);
            }
            var clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip MakeLoopClip(string clipName, float duration, System.Random rng, bool isFire)
        {
            _lpState = 0f;
            int samples = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float s = LowPassNoise(rng, isFire ? 0.08f : 0.03f) * 0.55f;
                if (isFire && rng.NextDouble() < 0.0025) s += NextNoise(rng) * 0.65f;
                if (!isFire) s += Mathf.Sin(t * 2f * Mathf.PI * 58f) * 0.14f;
                data[i] = Mathf.Clamp(s, -0.85f, 0.85f);
            }
            // Crossfade loop seam (last 512 samples)
            int fade = Mathf.Min(512, samples / 8);
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                data[samples - fade + i] = Mathf.Lerp(data[samples - fade + i], data[i], w);
            }
            var clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

