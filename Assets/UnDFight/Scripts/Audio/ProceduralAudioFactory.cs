using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Generates clean, click-free 44.1 kHz mono procedural AudioClips in memory
    /// for UI navigation, combat impacts, attacks, round gongs, and an 8s ambient music loop.
    /// </summary>
    public static class ProceduralAudioFactory
    {
        public const int SampleRate = 44100;

        public static AudioClip CreateUiMoveClip()
        {
            return BuildClip("SFX_UI_Move", 0.055f, (t, dur, rng) =>
            {
                float norm = t / dur;
                float env = FastAttackDecay(t, 0.003f, dur, 7.5f);
                float freq = Mathf.Lerp(680f, 940f, norm);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.22f;
            });
        }

        public static AudioClip CreateUiConfirmClip()
        {
            return BuildClip("SFX_UI_Confirm", 0.24f, (t, dur, rng) =>
            {
                float bassEnv = FastAttackDecay(t, 0.003f, dur, 10f);
                float bellEnv = FastAttackDecay(t, 0.002f, dur, 14f);
                float bassFreq = Mathf.Lerp(118f, 46f, Mathf.Min(1f, t / 0.14f));
                float thud = Mathf.Sin(2f * Mathf.PI * bassFreq * t) * bassEnv * 0.45f;
                float metal = (Mathf.Sin(2f * Mathf.PI * 740f * t) * 0.55f + Mathf.Sin(2f * Mathf.PI * 1110f * t) * 0.35f) * bellEnv * 0.30f;
                return thud + metal;
            });
        }

        public static AudioClip CreateUiBackClip()
        {
            return BuildClip("SFX_UI_Back", 0.14f, (t, dur, rng) =>
            {
                float norm = t / dur;
                float env = FastAttackDecay(t, 0.003f, dur, 8.5f);
                float freq = Mathf.Lerp(460f, 270f, norm);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.26f;
            });
        }

        public static AudioClip CreateWhooshLightClip()
        {
            return BuildFilteredNoiseSweepClip("SFX_Whoosh_Light", 0.13f, startCutoffHz: 950f, endCutoffHz: 320f, gain: 0.30f);
        }

        public static AudioClip CreateWhooshHeavyClip()
        {
            return BuildFilteredNoiseSweepClip("SFX_Whoosh_Heavy", 0.22f, startCutoffHz: 680f, endCutoffHz: 150f, gain: 0.42f);
        }

        public static AudioClip CreateDashClip()
        {
            return BuildClip("SFX_Dash", 0.14f, (t, dur, rng) =>
            {
                float norm = t / dur;
                float env = FastAttackDecay(t, 0.005f, dur, 7.5f);
                float lowFreq = Mathf.Lerp(175f, 70f, norm);
                float body = Mathf.Sin(2f * Mathf.PI * lowFreq * t) * 0.42f;
                float rush = ((float)rng.NextDouble() * 2f - 1f) * 0.36f * (1f - norm * 0.4f);
                return (body + rush) * env;
            });
        }

        public static AudioClip CreateThrowTechClip()
        {
            return BuildClip("SFX_Throw_Tech", 0.24f, (t, dur, rng) =>
            {
                float hit1Env = FastAttackDecay(t, 0.002f, 0.14f, 12f);
                float hit2Env = t >= 0.045f ? FastAttackDecay(t - 0.045f, 0.002f, dur - 0.045f, 10f) : 0f;
                float clang1 = (Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.55f +
                                Mathf.Sin(2f * Mathf.PI * 980f * t) * 0.45f) * hit1Env;
                float clang2 = (Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.50f +
                                Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.40f +
                                Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.35f) * hit2Env;
                float snap = ((float)rng.NextDouble() * 2f - 1f) * FastAttackDecay(t, 0.001f, 0.07f, 18f) * 0.32f;
                return (clang1 + clang2) * 0.55f + snap;
            });
        }

        public static AudioClip CreateWhooshSpecialClip()
        {
            return BuildClip("SFX_Whoosh_Special", 0.34f, (t, dur, rng) =>
            {
                float norm = t / dur;
                float env = FastAttackDecay(t, 0.008f, dur, 5.2f);
                float sweepHz = Mathf.Lerp(260f, 1240f, norm);
                float tone = (Mathf.Sin(2f * Mathf.PI * sweepHz * t) * 0.45f +
                              Mathf.Sin(2f * Mathf.PI * sweepHz * 1.5f * t) * 0.25f) * 0.42f;
                float air = ((float)rng.NextDouble() * 2f - 1f) * 0.28f;
                return (tone + air) * env;
            });
        }

        public static AudioClip CreateSuperFlashClip()
        {
            return BuildClip("SFX_Super_Flash", 0.44f, (t, dur, rng) =>
            {
                float norm = t / dur;
                float env = FastAttackDecay(t, 0.004f, dur, 4.8f);
                float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(92f, 44f, norm) * t) * 0.55f;
                float chime = (Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.38f +
                               Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.30f +
                               Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.22f) * FastAttackDecay(t, 0.003f, 0.36f, 7.5f);
                return (sub + chime) * env;
            });
        }

        public static AudioClip CreateExActivationClip()
        {
            return BuildClip("SFX_Ex_Activation", 0.20f, (t, dur, rng) =>
            {
                float norm = t / dur;
                float env = FastAttackDecay(t, 0.002f, dur, 9.5f);
                float rise = Mathf.Lerp(420f, 1180f, norm);
                return (Mathf.Sin(2f * Mathf.PI * rise * t) * 0.48f +
                        Mathf.Sin(2f * Mathf.PI * rise * 1.5f * t) * 0.28f) * env;
            });
        }

        public static AudioClip CreateProjectileFireClip()
        {
            return BuildClip("SFX_Projectile_Fire", 0.25f, (t, dur, rng) =>
            {
                float norm = t / dur;
                float env = FastAttackDecay(t, 0.004f, dur, 7.2f);
                float sweep = Mathf.Lerp(620f, 190f, norm);
                float tone = Mathf.Sin(2f * Mathf.PI * sweep * t) * 0.50f;
                float hiss = ((float)rng.NextDouble() * 2f - 1f) * 0.32f;
                return (tone + hiss) * env;
            });
        }

        public static AudioClip CreateArmorAbsorbClip()
        {
            return BuildClip("SFX_Armor_Absorb", 0.18f, (t, dur, rng) =>
            {
                float env = FastAttackDecay(t, 0.002f, dur, 11f);
                float clang = Mathf.Sin(2f * Mathf.PI * 310f * t) * 0.48f +
                              Mathf.Sin(2f * Mathf.PI * 465f * t) * 0.38f +
                              Mathf.Sin(2f * Mathf.PI * 930f * t) * 0.22f;
                float thud = Mathf.Sin(2f * Mathf.PI * 96f * t) * 0.45f;
                return (clang + thud) * env;
            });
        }

        public static AudioClip CreateHitLightClip()
        {
            return BuildClip("SFX_Hit_Light", 0.15f, (t, dur, rng) =>
            {
                float snapEnv = FastAttackDecay(t, 0.002f, dur, 14f);
                float bodyEnv = FastAttackDecay(t, 0.002f, dur, 18f);
                float freq = Mathf.Lerp(210f, 62f, Mathf.Min(1f, t / 0.08f));
                float punch = Mathf.Sin(2f * Mathf.PI * freq * t) * bodyEnv * 0.55f;
                float crack = ((float)rng.NextDouble() * 2f - 1f) * FastAttackDecay(t, 0.001f, 0.045f, 22f) * 0.38f;
                return (punch + crack) * (0.85f + 0.15f * snapEnv);
            });
        }

        public static AudioClip CreateHitHeavyClip()
        {
            return BuildClip("SFX_Hit_Heavy", 0.27f, (t, dur, rng) =>
            {
                float subEnv = FastAttackDecay(t, 0.002f, dur, 9.5f);
                float subFreq = Mathf.Lerp(145f, 36f, Mathf.Min(1f, t / 0.13f));
                float sub = Mathf.Sin(2f * Mathf.PI * subFreq * t) * subEnv * 0.68f;
                float mid = Mathf.Sin(2f * Mathf.PI * 96f * t) * FastAttackDecay(t, 0.002f, 0.12f, 16f) * 0.22f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * FastAttackDecay(t, 0.001f, 0.08f, 19f) * 0.42f;
                return sub + mid + noise;
            });
        }

        public static AudioClip CreateHitSpecialClip()
        {
            return BuildClip("SFX_Hit_Special", 0.42f, (t, dur, rng) =>
            {
                float subEnv = FastAttackDecay(t, 0.002f, dur, 6.2f);
                float subFreq = Mathf.Lerp(160f, 30f, Mathf.Min(1f, t / 0.18f));
                float sub = Mathf.Sin(2f * Mathf.PI * subFreq * t) * subEnv * 0.72f;
                float ringEnv = FastAttackDecay(t, 0.003f, 0.34f, 8.5f);
                float ring = (Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.4f +
                              Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.3f +
                              Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.2f) * ringEnv * 0.32f;
                float crack = ((float)rng.NextDouble() * 2f - 1f) * FastAttackDecay(t, 0.001f, 0.11f, 14f) * 0.45f;
                return sub + ring + crack;
            });
        }

        public static AudioClip CreateBlockImpactClip()
        {
            return BuildClip("SFX_Block_Impact", 0.16f, (t, dur, rng) =>
            {
                float thudEnv = FastAttackDecay(t, 0.002f, dur, 15f);
                float guardEnv = FastAttackDecay(t, 0.002f, 0.09f, 20f);
                float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(170f, 78f, t / dur) * t) * thudEnv * 0.42f;
                float guard = (Mathf.Sin(2f * Mathf.PI * 360f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 540f * t) * 0.4f) * guardEnv * 0.28f;
                return thud + guard;
            });
        }

        public static AudioClip CreateKnockdownClip()
        {
            return BuildClip("SFX_Knockdown", 0.36f, (t, dur, rng) =>
            {
                float subEnv = FastAttackDecay(t, 0.004f, dur, 7.2f);
                float freq = Mathf.Lerp(98f, 28f, Mathf.Min(1f, t / 0.22f));
                float drop = Mathf.Sin(2f * Mathf.PI * freq * t) * subEnv * 0.72f;
                float grit = ((float)rng.NextDouble() * 2f - 1f) * FastAttackDecay(t, 0.003f, 0.16f, 12f) * 0.28f;
                return drop + grit;
            });
        }

        public static AudioClip CreatePropImpactClip()
        {
            return BuildClip("SFX_Prop_Impact", 0.16f, (t, dur, rng) =>
            {
                float bodyEnv = FastAttackDecay(t, 0.002f, dur, 14f);
                float body = (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(240f, 115f, t / dur) * t) * 0.55f +
                              Mathf.Sin(2f * Mathf.PI * 390f * t) * 0.30f) * bodyEnv;
                float knock = ((float)rng.NextDouble() * 2f - 1f) * FastAttackDecay(t, 0.001f, 0.045f, 22f) * 0.32f;
                return body * 0.65f + knock;
            });
        }

        public static AudioClip CreatePropBreakWoodPlasticClip()
        {
            return BuildClip("SFX_Prop_Break_Crate", 0.38f, (t, dur, rng) =>
            {
                float crack1 = ((float)rng.NextDouble() * 2f - 1f) * FastAttackDecay(t, 0.001f, 0.09f, 16f) * 0.55f;
                float crack2 = t >= 0.055f
                    ? ((float)rng.NextDouble() * 2f - 1f) * FastAttackDecay(t - 0.055f, 0.002f, 0.14f, 11f) * 0.42f
                    : 0f;
                float clatter = t >= 0.11f
                    ? (Mathf.Sin(2f * Mathf.PI * 310f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.25f) *
                      FastAttackDecay(t - 0.11f, 0.003f, 0.24f, 9f) * 0.35f
                    : 0f;
                float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(155f, 52f, Mathf.Min(1f, t / 0.16f)) * t) *
                              FastAttackDecay(t, 0.002f, 0.18f, 12f) * 0.48f;
                return crack1 + crack2 + clatter + thump;
            });
        }

        public static AudioClip CreatePropBreakGlassClip()
        {
            return BuildClip("SFX_Prop_Break_Glass", 0.34f, (t, dur, rng) =>
            {
                float env = FastAttackDecay(t, 0.001f, dur, 9.5f);
                float shimmer = (Mathf.Sin(2f * Mathf.PI * 1920f * t) * 0.32f +
                                 Mathf.Sin(2f * Mathf.PI * 2840f * t) * 0.28f +
                                 Mathf.Sin(2f * Mathf.PI * 4110f * t) * 0.20f) * env;
                float burst = ((float)rng.NextDouble() * 2f - 1f) * FastAttackDecay(t, 0.001f, 0.16f, 13f) * 0.40f;
                return shimmer * 0.65f + burst;
            });
        }

        public static AudioClip CreateRoundGongClip()
        {
            return BuildClip("SFX_Round_Gong", 0.95f, (t, dur, rng) =>
            {
                float env = FastAttackDecay(t, 0.008f, dur, 3.6f);
                float swell = Mathf.Sin(2f * Mathf.PI * 2.5f * t) * 0.08f + 0.92f;
                float f1 = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.48f;
                float f2 = Mathf.Sin(2f * Mathf.PI * 165.4f * t) * 0.28f;
                float f3 = Mathf.Sin(2f * Mathf.PI * 275.8f * t) * 0.16f;
                float f4 = Mathf.Sin(2f * Mathf.PI * 419f * t) * FastAttackDecay(t, 0.005f, 0.40f, 9f) * 0.12f;
                return (f1 + f2 + f3 + f4) * env * swell * 0.65f;
            });
        }

        public static AudioClip CreateAmbientMusicLoopClip()
        {
            const float durationSec = 8.0f;
            int samples = Mathf.RoundToInt(durationSec * SampleRate);
            float[] data = new float[samples];

            // Integer cycle frequencies over 8.0s guarantee zero phase jump across loop seam
            const float bassA = 55.0f;   // 440 cycles / 8s
            const float bassB = 65.5f;   // 524 cycles / 8s
            const float padLo = 110.0f;  // 880 cycles / 8s
            const float padFifth = 165.0f; // 1320 cycles / 8s
            const float padMinor = 130.75f; // 1046 cycles / 8s

            float beatPeriod = 0.5f; // 120 BPM (16 beats in 8s)

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float localBeat = t % beatPeriod;
                float beatNorm = localBeat / beatPeriod;

                // Soft sub pulse on every beat, slightly stronger on downbeats
                int beatIdx = Mathf.FloorToInt(t / beatPeriod);
                bool useSecondBass = (beatIdx % 8) >= 6;
                float bassFreq = useSecondBass ? bassB : bassA;
                float pulseEnv = Mathf.Exp(-beatNorm * 4.8f) * Mathf.Clamp01(localBeat / 0.015f);
                float bass = Mathf.Sin(2f * Mathf.PI * bassFreq * t) * (0.14f + 0.18f * pulseEnv);

                // Warm low atmospheric pad breathing smoothly across 8s (integer period 0.25 Hz -> 2 cycles / 8s)
                float lfo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                float pad = (Mathf.Sin(2f * Mathf.PI * padLo * t) * 0.45f +
                             Mathf.Sin(2f * Mathf.PI * padMinor * t) * 0.30f +
                             Mathf.Sin(2f * Mathf.PI * padFifth * t) * 0.25f) * (0.12f + 0.06f * lfo);

                // Subtle rim percussive pulse on beats 2 & 4
                float perc = 0f;
                if (beatIdx % 2 == 1)
                {
                    perc = Mathf.Sin(2f * Mathf.PI * 330f * t) * Mathf.Exp(-beatNorm * 22f) * Mathf.Clamp01(localBeat / 0.004f) * 0.08f;
                }

                // Loop boundary cross-fade safety micro-ramp (2 ms)
                float edgeFade = Mathf.Clamp01(t / 0.002f) * Mathf.Clamp01((durationSec - t) / 0.002f);
                data[i] = Mathf.Clamp((bass + pad + perc) * 0.45f * edgeFade, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("BGM_Favela_Sunset_Loop", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateRainLoopClip()
        {
            const float durationSec = 4.0f;
            int samples = Mathf.RoundToInt(durationSec * SampleRate);
            float[] data = new float[samples];
            var rng = new System.Random("Weather_Rain_Loop".GetHashCode());

            // Pink-ish rain noise with soft high-frequency droplet patter and smooth loop crossfade
            float b0 = 0f, b1 = 0f, b2 = 0f;
            float lp = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                b0 = 0.997f * b0 + white * 0.029f;
                b1 = 0.985f * b1 + white * 0.032f;
                b2 = 0.950f * b2 + white * 0.048f;
                float pink = (b0 + b1 + b2 + white * 0.08f) * 0.55f;

                lp += 0.22f * (pink - lp);
                float droplet = (rng.NextDouble() < 0.0025) ? (((float)rng.NextDouble() * 2f - 1f) * 0.28f) : 0f;
                float swell = 0.88f + 0.12f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);

                float edgeFade = Mathf.Clamp01(t / 0.06f) * Mathf.Clamp01((durationSec - t) / 0.06f);
                data[i] = Mathf.Clamp((lp + droplet) * swell * 0.42f * edgeFade, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("AMB_Weather_Rain_Loop", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateNightWindLoopClip()
        {
            const float durationSec = 6.0f;
            int samples = Mathf.RoundToInt(durationSec * SampleRate);
            float[] data = new float[samples];
            var rng = new System.Random("Weather_NightWind_Loop".GetHashCode());

            float lp = 0f;
            float hp = 0f;
            float prev = 0f;
            float dt = 1f / SampleRate;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)rng.NextDouble() * 2f - 1f;

                // Gentle slow gusting cutoff (integer cycles over 6s for seamless loop)
                float gustLfo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (1f / 6f) * t);
                float cutoff = Mathf.Lerp(180f, 520f, gustLfo);
                float rc = 1f / (2f * Mathf.PI * cutoff);
                float alpha = dt / (rc + dt);
                lp += alpha * (white - lp);
                hp = 0.985f * (hp + lp - prev);
                prev = lp;

                float edgeFade = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((durationSec - t) / 0.08f);
                data[i] = Mathf.Clamp(hp * (0.28f + 0.22f * gustLfo) * edgeFade, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("AMB_Weather_NightWind_Loop", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private delegate float SampleFormula(float timeSec, float durationSec, System.Random rng);

        private static AudioClip BuildClip(string clipName, float durationSec, SampleFormula formula)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(durationSec * SampleRate));
            float[] buffer = new float[samples];
            var rng = new System.Random(clipName.GetHashCode());

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                buffer[i] = Mathf.Clamp(formula(t, durationSec, rng), -0.98f, 0.98f);
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        private static AudioClip BuildFilteredNoiseSweepClip(
            string clipName,
            float durationSec,
            float startCutoffHz,
            float endCutoffHz,
            float gain)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(durationSec * SampleRate));
            float[] buffer = new float[samples];
            var rng = new System.Random(clipName.GetHashCode());

            float lp = 0f;
            float hp = 0f;
            float prev = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float norm = t / durationSec;
                float white = (float)rng.NextDouble() * 2f - 1f;

                float cutoff = Mathf.Lerp(startCutoffHz, endCutoffHz, norm);
                float rc = 1f / (2f * Mathf.PI * cutoff);
                float dt = 1f / SampleRate;
                float alphaLp = dt / (rc + dt);
                lp += alphaLp * (white - lp);

                hp = 0.96f * (hp + lp - prev);
                prev = lp;

                float env = Mathf.Sin(Mathf.PI * norm) * FastAttackDecay(t, 0.006f, durationSec, 4.5f);
                buffer[i] = Mathf.Clamp(hp * env * gain * 2.2f, -0.98f, 0.98f);
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        private static float FastAttackDecay(float t, float attackSec, float totalSec, float decayRate)
        {
            if (t >= totalSec)
            {
                return 0f;
            }
            float attack = attackSec > 0f ? Mathf.Clamp01(t / attackSec) : 1f;
            float tailFade = Mathf.Clamp01((totalSec - t) / 0.008f);
            return attack * tailFade * Mathf.Exp(-decayRate * (t / totalSec));
        }
    }
}
