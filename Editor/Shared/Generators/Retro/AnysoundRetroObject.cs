using System;
using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Browser;
using UnityEditor;
using UnityEngine;

namespace Anysound.Shared.Generators.Retro
{
    // The order is used as index into AnysoundRetroObject.Categories and is stored in presets, so only append
    public enum RetroCategory
    {
        Pickup,
        Laser,
        Explosion,
        Powerup,
        Hit,
        Jump,
        Click,
        Select,
    }

    public enum RetroWaveform
    {
        Square,
        Sawtooth,
        Sine,
        Triangle,
        Noise,
    }

    [Flags]
    public enum RetroWaveforms
    {
        Square = 1 << RetroWaveform.Square,
        Sawtooth = 1 << RetroWaveform.Sawtooth,
        Sine = 1 << RetroWaveform.Sine,
        Triangle = 1 << RetroWaveform.Triangle,
        Noise = 1 << RetroWaveform.Noise,
    }

    /// <summary>
    /// The synth parameters, in the normalized units of sfxr (https://sfxr.me). The order is used as index into
    /// RetroCategorySettings.ranges, so only append
    /// </summary>
    public enum RetroParameter
    {
        BaseFrequency,
        FrequencyLimit,
        FrequencySlide,
        FrequencyDeltaSlide,
        VibratoDepth,
        VibratoSpeed,
        ArpeggioAmount,
        ArpeggioSpeed,
        DutyCycle,
        DutySweep,
        RepeatSpeed,
        PhaserOffset,
        PhaserSweep,
        LowPassCutoff,
        LowPassSweep,
        LowPassResonance,
        HighPassCutoff,
        HighPassSweep,
        // Mixes noise into the waveform (0 = only the waveform, 1 = only noise)
        NoiseMix,
        // Extra volume at the start of the sustain
        Punch,
        // The part of the sound after the attack that is held before it decays
        Sustain,
    }

    [Serializable]
    public struct RetroRange
    {
        public float min;
        public float max;

        public RetroRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float Lerp(float t) => Mathf.Lerp(min, max, t);
    }

    /// <summary>
    /// Moves a parameter with the category's character slider: atLow is added at 0, atHigh at 1
    /// </summary>
    [Serializable]
    public struct RetroModulation
    {
        public RetroParameter parameter;
        public float atLow;
        public float atHigh;

        public RetroModulation(RetroParameter parameter, float atLow, float atHigh)
        {
            this.parameter = parameter;
            this.atLow = atLow;
            this.atHigh = atHigh;
        }
    }

    [Serializable]
    public class RetroCategorySettings
    {
        [Tooltip("The waveforms the random generation picks among. This is the hard end of the hardness slider, the soft end is sine/FM")]
        public RetroWaveforms waveforms = RetroWaveforms.Square;

        [Tooltip("Every parameter has a min/max range, the seed picks a value within it")]
        public RetroRange[] ranges = NeutralRanges();

        [Header("Character slider")] public string characterName = "CHARACTER";
        public string characterLowLabel = "LOW";
        public string characterHighLabel = "HIGH";
        public List<RetroModulation> characterModulations = new();

        public static readonly int ParameterCount = Enum.GetValues(typeof(RetroParameter)).Length;

        public RetroRange GetRange(RetroParameter parameter) => ranges[(int)parameter];

        public RetroCategorySettings Set(RetroParameter parameter, float min, float max)
        {
            ranges[(int)parameter] = new RetroRange(min, max);
            return this;
        }

        public RetroCategorySettings Character(string name, string low, string high, params RetroModulation[] modulations)
        {
            characterName = name;
            characterLowLabel = low;
            characterHighLabel = high;
            characterModulations = new List<RetroModulation>(modulations);
            return this;
        }

        // The range limits of every parameter, as in sfxr. Slides and sweeps go both ways
        public static Vector2 Limits(RetroParameter parameter)
        {
            switch (parameter)
            {
                case RetroParameter.FrequencySlide:
                case RetroParameter.FrequencyDeltaSlide:
                case RetroParameter.ArpeggioAmount:
                case RetroParameter.DutySweep:
                case RetroParameter.PhaserOffset:
                case RetroParameter.PhaserSweep:
                case RetroParameter.LowPassSweep:
                case RetroParameter.HighPassSweep:
                    return new Vector2(-1f, 1f);
                default:
                    return new Vector2(0f, 1f);
            }
        }

        // The value that leaves a parameter's effect out
        static float NeutralValue(RetroParameter parameter)
        {
            switch (parameter)
            {
                case RetroParameter.BaseFrequency: return 0.3f;
                case RetroParameter.LowPassCutoff: return 1f;
                case RetroParameter.Sustain: return 0.3f;
                default: return 0f;
            }
        }

        static RetroRange[] NeutralRanges()
        {
            var ranges = new RetroRange[ParameterCount];
            for (int i = 0; i < ranges.Length; i++)
            {
                float value = NeutralValue((RetroParameter)i);
                ranges[i] = new RetroRange(value, value);
            }

            return ranges;
        }

        // Parameters appended after the settings were saved get their neutral range
        public void EnsureRanges()
        {
            if (ranges != null && ranges.Length == ParameterCount) return;

            var newRanges = NeutralRanges();
            if (ranges != null)
                Array.Copy(ranges, newRanges, Mathf.Min(ranges.Length, newRanges.Length));
            ranges = newRanges;
        }
    }

    /// <summary>
    /// The synth values of one sound: the category's ranges resolved with the seed and moved by the character slider
    /// </summary>
    public struct RetroSynthSettings
    {
        public RetroWaveform waveform;
        public float[] values;

        public float this[RetroParameter parameter] => values[(int)parameter];
    }

    [CreateAssetMenu(fileName = "New AnysoundRetroObject", menuName = "Anysound/AnysoundRetroObject")]
    public class AnysoundRetroObject : AnysoundGeneratorBase
    {
        public override string ProductId => Licensing.AnysoundProducts.Retro;

        public static readonly string[] CategoryNames = Enum.GetNames(typeof(RetroCategory));

        [SerializeField] RetroCategorySettings[] categories;

        public RetroCategorySettings GetCategory(RetroCategory category)
        {
            EnsureCategories();
            return categories[(int)category];
        }

        void OnEnable() => EnsureCategories();

        void OnValidate() => EnsureCategories();

        // Categories appended after the asset was saved (or a new asset) get their default settings
        void EnsureCategories()
        {
            int count = CategoryNames.Length;
            if (categories != null && categories.Length == count && Array.TrueForAll(categories, c => c != null && c.ranges?.Length == RetroCategorySettings.ParameterCount))
                return;

            var newCategories = new RetroCategorySettings[count];
            for (int i = 0; i < count; i++)
            {
                newCategories[i] = categories != null && i < categories.Length && categories[i] != null
                    ? categories[i]
                    : DefaultSettings((RetroCategory)i);
                newCategories[i].EnsureRanges();
            }

            categories = newCategories;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        public void ResetCategory(RetroCategory category)
        {
            EnsureCategories();
            categories[(int)category] = DefaultSettings(category);
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// The synth values for these parameters. The random positions are drawn in a fixed order,
        /// so the same seed always gives the same sound, no matter how the ranges are set
        /// </summary>
        public RetroSynthSettings Resolve(AnysoundRetroParameters parameters) => Resolve(GetCategory(parameters.category), parameters);

        public static RetroSynthSettings Resolve(RetroCategorySettings settings, AnysoundRetroParameters parameters)
        {
            var rng = parameters.seed != 0 ? new System.Random(parameters.seed) : null;
            float Next() => rng != null ? (float)rng.NextDouble() : 0.5f;

            var values = new float[RetroCategorySettings.ParameterCount];
            for (int i = 0; i < values.Length; i++)
                values[i] = settings.ranges[i].Lerp(Next());

            foreach (var modulation in settings.characterModulations)
                values[(int)modulation.parameter] += Mathf.Lerp(modulation.atLow, modulation.atHigh, parameters.character);

            for (int i = 0; i < values.Length; i++)
            {
                var limits = RetroCategorySettings.Limits((RetroParameter)i);
                values[i] = Mathf.Clamp(values[i], limits.x, limits.y);
            }

            return new RetroSynthSettings { waveform = PickWaveform(settings.waveforms, Next()), values = values };
        }

        // How far an exported variation may move a parameter, as part of the parameter's range, and the duration
        const float VariationAmount = 0.02f;
        const float DurationVariation = 0.05f;

        /// <summary>
        /// A slightly different version of the same sound, e.g. for the clips of an export. Variation 0 is the original.
        /// Only the parameters the sound uses are moved, so a variation never adds an effect the original doesn't have
        /// </summary>
        public static RetroSynthSettings Vary(RetroSynthSettings settings, AnysoundRetroParameters parameters, int variation)
        {
            if (variation == 0) return settings;

            var rng = VariationRandom(parameters, variation);
            var values = (float[])settings.values.Clone();
            for (int i = 0; i < values.Length; i++)
            {
                var parameter = (RetroParameter)i;
                float offset = ((float)rng.NextDouble() * 2f - 1f) * VariationAmount;
                if (!IsUsed(parameter, values[i])) continue;

                var limits = RetroCategorySettings.Limits(parameter);
                values[i] = Mathf.Clamp(values[i] + offset * (limits.y - limits.x), limits.x, limits.y);
            }

            return new RetroSynthSettings { waveform = settings.waveform, values = values };
        }

        public static AnysoundRetroParameters Vary(AnysoundRetroParameters parameters, int variation)
        {
            if (variation == 0) return parameters;

            // A different stream than the synth values, so changing one doesn't change the other
            var rng = VariationRandom(parameters, -variation);
            parameters.duration = Mathf.Clamp(parameters.duration * (1f + ((float)rng.NextDouble() * 2f - 1f) * DurationVariation),
                AnysoundRetroParameters.DurationMin, AnysoundRetroParameters.DurationMax);
            return parameters;
        }

        // System.Random gives similar first values for nearby seeds, so the seed and variation are hashed first
        static System.Random VariationRandom(AnysoundRetroParameters parameters, int variation)
        {
            unchecked
            {
                uint h = (uint)parameters.seed * 0x9E3779B1u ^ (uint)variation * 0x85EBCA77u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return new System.Random((int)(h & 0x7FFFFFFF));
            }
        }

        // At their neutral value these parameters have no effect (the low pass is open at 1, the others are off at 0)
        static bool IsUsed(RetroParameter parameter, float value) =>
            parameter == RetroParameter.LowPassCutoff ? value < 1f : Mathf.Abs(value) > 0.0001f;

        static RetroWaveform PickWaveform(RetroWaveforms waveforms, float t)
        {
            var allowed = new List<RetroWaveform>();
            foreach (RetroWaveform waveform in Enum.GetValues(typeof(RetroWaveform)))
            {
                if ((waveforms & (RetroWaveforms)(1 << (int)waveform)) != 0)
                    allowed.Add(waveform);
            }

            if (allowed.Count == 0) return RetroWaveform.Square;
            return allowed[Mathf.Min((int)(t * allowed.Count), allowed.Count - 1)];
        }

        /// <summary>
        /// The starting point of every category, based on the generators of sfxr
        /// </summary>
        public static RetroCategorySettings DefaultSettings(RetroCategory category)
        {
            var s = new RetroCategorySettings();
            switch (category)
            {
                case RetroCategory.Pickup:
                    s.waveforms = RetroWaveforms.Square;
                    s.Set(RetroParameter.BaseFrequency, 0.4f, 0.9f)
                        .Set(RetroParameter.ArpeggioSpeed, 0.5f, 0.7f)
                        .Set(RetroParameter.DutyCycle, 0f, 0.4f)
                        .Set(RetroParameter.Punch, 0.3f, 0.6f)
                        .Set(RetroParameter.Sustain, 0f, 0.15f)
                        .Character("SPARKLE", "PLAIN", "SPARKLY",
                            new RetroModulation(RetroParameter.ArpeggioAmount, 0f, 0.6f));
                    break;
                case RetroCategory.Laser:
                    s.waveforms = RetroWaveforms.Square | RetroWaveforms.Sawtooth;
                    s.Set(RetroParameter.BaseFrequency, 0.5f, 1f)
                        .Set(RetroParameter.FrequencyLimit, 0.1f, 0.3f)
                        .Set(RetroParameter.FrequencySlide, -0.25f, -0.15f)
                        .Set(RetroParameter.DutyCycle, 0f, 0.9f)
                        .Set(RetroParameter.DutySweep, -0.3f, 0.2f)
                        .Set(RetroParameter.PhaserOffset, 0f, 0.2f)
                        .Set(RetroParameter.PhaserSweep, -0.2f, 0f)
                        .Set(RetroParameter.HighPassCutoff, 0f, 0.3f)
                        .Set(RetroParameter.Punch, 0f, 0.3f)
                        .Set(RetroParameter.Sustain, 0.2f, 0.6f)
                        .Character("SWEEP", "FLAT", "STEEP",
                            new RetroModulation(RetroParameter.FrequencySlide, 0.12f, -0.12f));
                    break;
                case RetroCategory.Explosion:
                    s.waveforms = RetroWaveforms.Noise;
                    s.Set(RetroParameter.BaseFrequency, 0.15f, 0.45f)
                        .Set(RetroParameter.FrequencySlide, -0.2f, 0.1f)
                        .Set(RetroParameter.VibratoDepth, 0f, 0.4f)
                        .Set(RetroParameter.VibratoSpeed, 0f, 0.6f)
                        .Set(RetroParameter.RepeatSpeed, 0f, 0.6f)
                        .Set(RetroParameter.PhaserOffset, -0.3f, 0.6f)
                        .Set(RetroParameter.PhaserSweep, -0.3f, 0f)
                        .Set(RetroParameter.Punch, 0.2f, 0.8f)
                        .Set(RetroParameter.Sustain, 0.1f, 0.4f)
                        .Character("SIZE", "SMALL", "BIG",
                            new RetroModulation(RetroParameter.BaseFrequency, 0.25f, -0.12f),
                            new RetroModulation(RetroParameter.LowPassCutoff, 0f, -0.6f),
                            new RetroModulation(RetroParameter.Punch, -0.2f, 0.2f));
                    break;
                case RetroCategory.Powerup:
                    s.waveforms = RetroWaveforms.Square | RetroWaveforms.Sawtooth;
                    s.Set(RetroParameter.BaseFrequency, 0.2f, 0.5f)
                        .Set(RetroParameter.FrequencySlide, 0.05f, 0.15f)
                        .Set(RetroParameter.DutyCycle, 0f, 0.6f)
                        .Set(RetroParameter.VibratoDepth, 0f, 0.4f)
                        .Set(RetroParameter.VibratoSpeed, 0.2f, 0.6f)
                        .Set(RetroParameter.RepeatSpeed, 0f, 0.8f)
                        .Set(RetroParameter.Sustain, 0.2f, 0.6f)
                        .Character("RISE", "GENTLE", "STEEP",
                            new RetroModulation(RetroParameter.FrequencySlide, 0f, 0.3f),
                            new RetroModulation(RetroParameter.RepeatSpeed, 0f, 0.2f));
                    break;
                case RetroCategory.Hit:
                    s.waveforms = RetroWaveforms.Square | RetroWaveforms.Sawtooth;
                    s.Set(RetroParameter.BaseFrequency, 0.2f, 0.8f)
                        .Set(RetroParameter.FrequencySlide, -0.7f, -0.3f)
                        .Set(RetroParameter.DutyCycle, 0f, 0.6f)
                        .Set(RetroParameter.HighPassCutoff, 0f, 0.3f)
                        .Set(RetroParameter.Punch, 0.2f, 0.5f)
                        .Set(RetroParameter.Sustain, 0f, 0.15f)
                        .Character("CRUNCH", "TONE", "NOISE",
                            new RetroModulation(RetroParameter.NoiseMix, 0f, 1f));
                    break;
                case RetroCategory.Jump:
                    s.waveforms = RetroWaveforms.Square;
                    s.Set(RetroParameter.BaseFrequency, 0.3f, 0.6f)
                        .Set(RetroParameter.DutyCycle, 0f, 0.6f)
                        .Set(RetroParameter.LowPassCutoff, 0.6f, 1f)
                        .Set(RetroParameter.HighPassCutoff, 0f, 0.3f)
                        .Set(RetroParameter.Sustain, 0.3f, 0.6f)
                        .Character("HEIGHT", "LOW", "HIGH",
                            new RetroModulation(RetroParameter.FrequencySlide, 0.05f, 0.3f));
                    break;
                case RetroCategory.Click:
                    s.waveforms = RetroWaveforms.Square | RetroWaveforms.Noise;
                    s.Set(RetroParameter.BaseFrequency, 0.5f, 0.7f)
                        .Set(RetroParameter.FrequencySlide, -0.3f, 0f)
                        .Set(RetroParameter.HighPassCutoff, 0.1f, 0.3f)
                        .Set(RetroParameter.Punch, 0.2f, 0.5f)
                        .Set(RetroParameter.Sustain, 0f, 0.1f)
                        .Character("PITCH", "LOW", "HIGH",
                            new RetroModulation(RetroParameter.BaseFrequency, -0.3f, 0.25f));
                    break;
                case RetroCategory.Select:
                    s.waveforms = RetroWaveforms.Square | RetroWaveforms.Sawtooth;
                    s.Set(RetroParameter.BaseFrequency, 0.2f, 0.6f)
                        .Set(RetroParameter.DutyCycle, 0f, 0.6f)
                        .Set(RetroParameter.HighPassCutoff, 0.1f, 0.1f)
                        .Set(RetroParameter.Sustain, 0.3f, 0.6f)
                        .Character("BRIGHTNESS", "DULL", "BRIGHT",
                            new RetroModulation(RetroParameter.LowPassCutoff, -0.75f, 0f),
                            new RetroModulation(RetroParameter.HighPassCutoff, -0.1f, 0.1f));
                    break;
            }

            return s;
        }

        // Words describing a sound, e.g. { "Pickup", "Hard", "Short", "Sparkly" }
        public string[] GetDescription(AnysoundRetroParameters parameters)
        {
            var settings = GetCategory(parameters.category);
            return new[]
            {
                parameters.category.ToString(),
                Describe(parameters.hardness, "Soft", "Mellow", "Hard"),
                Describe(Mathf.InverseLerp(AnysoundRetroParameters.DurationMin, AnysoundRetroParameters.DurationMax, parameters.duration),
                    "Short", "Medium", "Long"),
                Describe(parameters.character, Capitalize(settings.characterLowLabel), Capitalize(settings.characterName),
                    Capitalize(settings.characterHighLabel)),
            };
        }

        static string Describe(float t, string low, string mid, string high) => t < 1f / 3f ? low : t < 2f / 3f ? mid : high;

        static string Capitalize(string word) =>
            string.IsNullOrEmpty(word) ? word : char.ToUpper(word[0]) + word.Substring(1).ToLower();

        public override void DescribePreset(Dictionary<string, float> presetValues, out string presetName, out List<string> tags)
        {
            var parameters = AnysoundRetroParameters.FromPresetValues(presetValues);
            string[] description = GetDescription(parameters);
            presetName = $"8-Bit {string.Join(" ", description)}";
            tags = new List<string>
            {
                "8-bit",
                description[0].ToLower(),
                description[1].ToLower(),
                $"{description[2]} length".ToLower(),
            };
        }

        public override void CreatePreset(Dictionary<string, float> presetValues)
        {
            DescribePreset(presetValues, out string presetName, out List<string> tags);
            AudioClip generatedClip = AnysoundRetroDSP.CreateAudioClip(this, AnysoundRetroParameters.FromPresetValues(presetValues));

            AnysoundBrowser.CreatePreset(presetName, presetValues, tags, this, generatedClip);
        }
    }

    /// <summary>
    /// The user facing values of the 8-bit generator
    /// </summary>
    [Serializable]
    public struct AnysoundRetroParameters
    {
        public const string CategoryKey = "Category";
        public const string DurationKey = "Duration";
        public const string HardnessKey = "Hardness";
        public const string CharacterKey = "Character";
        public const string SeedKey = "Seed";

        public const float DurationMin = 0.05f, DurationMax = 2f, DurationDefault = 0.3f;
        public const float HardnessDefault = 1f;
        public const float CharacterDefault = 0.5f;

        // Seeds are stored as floats in presets, so they are kept below 2^24 where floats are still exact
        public const int MaxSeed = 1 << 24;

        public RetroCategory category;
        [Range(DurationMin, DurationMax)] public float duration;

        // The waveform: 0 = soft (sine, then FM towards the middle), 1 = hard (the category's square/sawtooth)
        [Range(0f, 1f)] public float hardness;

        // The category's own parameter, see RetroCategorySettings.characterModulations
        [Range(0f, 1f)] public float character;

        // Picks the synth values within the category's ranges. 0 = the middle of every range
        public int seed;

        public static AnysoundRetroParameters Default => new()
        {
            duration = DurationDefault,
            hardness = HardnessDefault,
            character = CharacterDefault,
            seed = NewSeed(),
        };

        public static int NewSeed() => UnityEngine.Random.Range(1, MaxSeed);

        public Dictionary<string, float> ToPresetValues()
        {
            return new Dictionary<string, float>
            {
                { CategoryKey, (int)category },
                { DurationKey, duration },
                { HardnessKey, hardness },
                { CharacterKey, character },
                { SeedKey, seed },
            };
        }

        public static AnysoundRetroParameters FromPresetValues(Dictionary<string, float> presetValues)
        {
            float Get(string key, float fallback) => presetValues != null && presetValues.TryGetValue(key, out var v) ? v : fallback;
            return Create(Get(CategoryKey, 0), Get(DurationKey, DurationDefault), Get(HardnessKey, HardnessDefault),
                Get(CharacterKey, CharacterDefault), Get(SeedKey, 0));
        }

        public static AnysoundRetroParameters FromPreset(AnysoundPresetObject preset)
        {
            float Get(string key, float fallback) => preset.HasPresetValue(key) ? preset.GetPresetValue(key) : fallback;
            return Create(Get(CategoryKey, 0), Get(DurationKey, DurationDefault), Get(HardnessKey, HardnessDefault),
                Get(CharacterKey, CharacterDefault), Get(SeedKey, 0));
        }

        // Stored values are clamped, so a value a rounding error outside its range still loads
        static AnysoundRetroParameters Create(float category, float duration, float hardness, float character, float seed) => new()
        {
            category = (RetroCategory)Mathf.Clamp(Mathf.RoundToInt(category), 0, AnysoundRetroObject.CategoryNames.Length - 1),
            duration = Mathf.Clamp(duration, DurationMin, DurationMax),
            hardness = Mathf.Clamp01(hardness),
            character = Mathf.Clamp01(character),
            seed = Mathf.RoundToInt(seed),
        };
    }
}
