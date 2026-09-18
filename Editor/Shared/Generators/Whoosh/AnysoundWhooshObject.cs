using System;
using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Browser;
using Anysound.Shared.Generators.Whoosh.Frontend;
using UnityEngine;

namespace Anysound.Shared.Generators.Whoosh
{
    [CreateAssetMenu(fileName = "New AnysoundWhooshObject", menuName = "Anysound/AnysoundWhooshObject")]
    public class AnysoundWhooshObject : AnysoundGeneratorBase
    {
        [SerializeField] WhooshMovementSettings[] movementSettings;
        public WhooshMovementSettings[] MovementSettings => movementSettings;

        public WhooshMovementSettings GetWhooshMovementSettings(float value)
        {
            List<WhooshMovementSettings> whooshMovementSettingsList = new List<WhooshMovementSettings>();
            whooshMovementSettingsList.AddRange(movementSettings);

            float[] weightValues = new float[whooshMovementSettingsList.Count];

            if (whooshMovementSettingsList.Count > 0)
            {
                if (whooshMovementSettingsList.Count == 1)
                {
                    // If there's only one setting, it gets full weight
                    weightValues[0] = 1.0f;
                }
                else // whooshMovementSettingsList.Count >= 2
                {
                    // Interpolate between the first two settings based on 'value'
                    // If value is 0, weights will be [1.0, 0.0, ...]
                    // If value is 1, weights will be [0.0, 1.0, ...]
                    weightValues[0] = 1.0f - value;
                    weightValues[1] = value;

                    // Set weights for any subsequent settings to 0, as not specified otherwise
                    for (int i = 2; i < whooshMovementSettingsList.Count; i++)
                    {
                        weightValues[i] = 0.0f;
                    }
                }
            }

            return WhooshMovementSettings.WeightedMix(whooshMovementSettingsList, new List<float>(weightValues));
        }

        [Serializable]
        public struct WhooshMovementSettings
        {
            [Header("Sound generation settings")] [SerializeField]
            private NoiseGeneratorSettings noiseGenerator1;

            public NoiseGeneratorSettings NoiseGenerator1 => noiseGenerator1;

            [SerializeField] private NoiseGeneratorSettings noiseGenerator2;
            public NoiseGeneratorSettings NoiseGenerator2 => noiseGenerator2;
            [SerializeField] ToneGeneratorSettings toneGenerator;
            public ToneGeneratorSettings ToneGenerator => toneGenerator;

            [Header("Filter settings")] [SerializeField]
            AnysoundAudioDSP.BandPassFilterSettings bandPassFilter1;

            public AnysoundAudioDSP.BandPassFilterSettings BandPassFilterSettings1 => bandPassFilter1;
            [SerializeField] AnysoundAudioDSP.BandPassFilterSettings bandPassFilter2;
            public AnysoundAudioDSP.BandPassFilterSettings BandPassFilterSettings2 => bandPassFilter2;


            [Header("Envelope settings")] [SerializeField]
            private AnysoundAudioDSP.ADSREnvelopeSettings amplitudeEnvelope;

            public AnysoundAudioDSP.ADSREnvelopeSettings AmplitudeEnvelope => amplitudeEnvelope;
            [SerializeField] private AnysoundAudioDSP.ADSREnvelopeSettings filterEnvelope1;
            public AnysoundAudioDSP.ADSREnvelopeSettings FilterEnvelope1 => filterEnvelope1;
            [SerializeField] private AnysoundAudioDSP.ADSREnvelopeSettings filterEnvelope2;
            public AnysoundAudioDSP.ADSREnvelopeSettings FilterEnvelope2 => filterEnvelope2;
            [SerializeField] private AnysoundAudioDSP.ADSREnvelopeSettings sineFrequencyEnvelope;
            public AnysoundAudioDSP.ADSREnvelopeSettings SineFrequencyEnvelope => sineFrequencyEnvelope;

            [SerializeField] LFOGeneratorSettings fluctuationLFOGenerator;
            public LFOGeneratorSettings FluctuationLFOGenerator => fluctuationLFOGenerator;

            public static WhooshMovementSettings WeightedMix(List<WhooshMovementSettings> settings, List<float> weights)
            {
                if (settings == null || weights == null || settings.Count != weights.Count || settings.Count == 0)
                    throw new ArgumentException("Settings and weights must be non-null and of the same length > 0");

                List<NoiseGeneratorSettings> noiseGenerator1List = new List<NoiseGeneratorSettings>();
                List<NoiseGeneratorSettings> noiseGenerator2List = new List<NoiseGeneratorSettings>();
                List<ToneGeneratorSettings> toneGeneratorList = new List<ToneGeneratorSettings>();
                List<AnysoundAudioDSP.BandPassFilterSettings> bandPassFilter1List = new List<AnysoundAudioDSP.BandPassFilterSettings>();
                List<AnysoundAudioDSP.BandPassFilterSettings> bandPassFilter2List = new List<AnysoundAudioDSP.BandPassFilterSettings>();
                List<AnysoundAudioDSP.ADSREnvelopeSettings> amplitudeEnvelopeList = new List<AnysoundAudioDSP.ADSREnvelopeSettings>();
                List<AnysoundAudioDSP.ADSREnvelopeSettings> filterEnvelope1List = new List<AnysoundAudioDSP.ADSREnvelopeSettings>();
                List<AnysoundAudioDSP.ADSREnvelopeSettings> filterEnvelope2List = new List<AnysoundAudioDSP.ADSREnvelopeSettings>();
                List<AnysoundAudioDSP.ADSREnvelopeSettings> sineFrequencyEnvelopeList = new List<AnysoundAudioDSP.ADSREnvelopeSettings>();
                List<LFOGeneratorSettings> lfoGeneratorSettingsList = new List<LFOGeneratorSettings>();

                for (int i = 0; i < settings.Count; i++)
                {
                    noiseGenerator1List.Add(settings[i].NoiseGenerator1);
                    noiseGenerator2List.Add(settings[i].NoiseGenerator2);
                    toneGeneratorList.Add(settings[i].ToneGenerator);
                    bandPassFilter1List.Add(settings[i].BandPassFilterSettings1);
                    bandPassFilter2List.Add(settings[i].BandPassFilterSettings2);
                    amplitudeEnvelopeList.Add(settings[i].AmplitudeEnvelope);
                    filterEnvelope1List.Add(settings[i].FilterEnvelope1);
                    filterEnvelope2List.Add(settings[i].FilterEnvelope2);
                    sineFrequencyEnvelopeList.Add(settings[i].SineFrequencyEnvelope);
                    lfoGeneratorSettingsList.Add(settings[i].fluctuationLFOGenerator);
                }

                NoiseGeneratorSettings mixedNoiseGenerator1 = NoiseGeneratorSettings.WeightedMix(noiseGenerator1List, weights);
                NoiseGeneratorSettings mixedNoiseGenerator2 = NoiseGeneratorSettings.WeightedMix(noiseGenerator2List, weights);
                ToneGeneratorSettings mixedToneGenerator = ToneGeneratorSettings.WeightedMix(toneGeneratorList, weights);
                AnysoundAudioDSP.BandPassFilterSettings mixedBandPassFilter1 =
                    AnysoundAudioDSP.BandPassFilterSettings.WeightedMix(bandPassFilter1List, weights);
                AnysoundAudioDSP.BandPassFilterSettings mixedBandPassFilter2 =
                    AnysoundAudioDSP.BandPassFilterSettings.WeightedMix(bandPassFilter2List, weights);
                AnysoundAudioDSP.ADSREnvelopeSettings mixedAmplitudeEnvelope =
                    AnysoundAudioDSP.ADSREnvelopeSettings.WeightedMix(amplitudeEnvelopeList, weights);
                AnysoundAudioDSP.ADSREnvelopeSettings mixedFilterEnvelope1 =
                    AnysoundAudioDSP.ADSREnvelopeSettings.WeightedMix(filterEnvelope1List, weights);
                AnysoundAudioDSP.ADSREnvelopeSettings mixedFilterEnvelope2 =
                    AnysoundAudioDSP.ADSREnvelopeSettings.WeightedMix(filterEnvelope2List, weights);
                AnysoundAudioDSP.ADSREnvelopeSettings mixedSineFrequencyEnvelope =
                    AnysoundAudioDSP.ADSREnvelopeSettings.WeightedMix(sineFrequencyEnvelopeList, weights);
                LFOGeneratorSettings mixedLFOGeneratorSettings = LFOGeneratorSettings.WeightedMix(lfoGeneratorSettingsList, weights);

                return new WhooshMovementSettings
                {
                    noiseGenerator1 = mixedNoiseGenerator1,
                    noiseGenerator2 = mixedNoiseGenerator2,
                    toneGenerator = mixedToneGenerator,
                    bandPassFilter1 = mixedBandPassFilter1,
                    bandPassFilter2 = mixedBandPassFilter2,
                    amplitudeEnvelope = mixedAmplitudeEnvelope,
                    filterEnvelope1 = mixedFilterEnvelope1,
                    filterEnvelope2 = mixedFilterEnvelope2,
                    sineFrequencyEnvelope = mixedSineFrequencyEnvelope,
                    fluctuationLFOGenerator = mixedLFOGeneratorSettings,
                };
            }
        }

        [Serializable]
        public struct NoiseGeneratorSettings
        {
            [Range(0, 1f)] public float amplitude;

            public enum NoiseTypes
            {
                WhiteNoise,
                PinkNoise,
                BrownNoise,
            }

            public NoiseTypes noiseType;

            public static NoiseGeneratorSettings WeightedMix(List<NoiseGeneratorSettings> settings, List<float> weights)
            {
                if (settings == null || weights == null || settings.Count != weights.Count || settings.Count == 0)
                    throw new ArgumentException("Settings and weights must be non-null and of the same length > 0");

                float totalWeight = 0f;
                float sumAmplitude = 0f;

                for (int i = 0; i < settings.Count; i++)
                {
                    float w = weights[i];
                    totalWeight += w;
                    sumAmplitude += settings[i].amplitude * w;
                }

                if (totalWeight == 0f)
                    throw new InvalidOperationException("Total weight must be greater than zero.");

                // For NoiseTypes, we will simply take the type from the first setting as enums cannot be weighted directly.
                NoiseTypes mixedNoiseType = settings[0].noiseType;

                return new NoiseGeneratorSettings
                {
                    amplitude = sumAmplitude / totalWeight,
                    noiseType = mixedNoiseType
                };
            }
        }

        [Serializable]
        public struct ToneGeneratorSettings
        {
            [Range(0, 1f)] public float amplitude;

            public enum ToneTypes
            {
                Saw,
                Square,
                Triangle,
                Sine,
            }

            public ToneTypes toneType;

            public static ToneGeneratorSettings WeightedMix(List<ToneGeneratorSettings> settings, List<float> weights)
            {
                if (settings == null || weights == null || settings.Count != weights.Count || settings.Count == 0)
                    throw new ArgumentException("Settings and weights must be non-null and of the same length > 0");

                float totalWeight = 0f;
                float sumAmplitude = 0f;

                for (int i = 0; i < settings.Count; i++)
                {
                    float w = weights[i];
                    totalWeight += w;
                    sumAmplitude += settings[i].amplitude * w;
                }

                if (totalWeight == 0f)
                    throw new InvalidOperationException("Total weight must be greater than zero.");

                // For ToneTypes, we will simply take the type from the first setting as enums cannot be weighted directly.
                ToneTypes mixedToneType = settings[0].toneType;

                return new ToneGeneratorSettings
                {
                    amplitude = sumAmplitude / totalWeight,
                    toneType = mixedToneType
                };
            }
        }

        [Serializable]
        public struct LFOGeneratorSettings
        {
            public float speed;
            public float amplitude;

            public enum LFOTypes
            {
                Sine,
                Square,
                Triangle,
            }

            public LFOTypes lfoType;

            public static LFOGeneratorSettings WeightedMix(List<LFOGeneratorSettings> lfoGeneratorSettingsList, List<float> weights)
            {
                if (lfoGeneratorSettingsList == null || weights == null || lfoGeneratorSettingsList.Count != weights.Count ||
                    lfoGeneratorSettingsList.Count == 0)
                    throw new ArgumentException("Settings and weights must be non-null and of the same length > 0");

                float totalWeight = 0f;
                float sumSpeed = 0f;
                float sumAmplitude = 0f;

                for (int i = 0; i < lfoGeneratorSettingsList.Count; i++)
                {
                    float w = weights[i];
                    totalWeight += w;
                    sumSpeed += lfoGeneratorSettingsList[i].speed * w;
                    sumAmplitude += lfoGeneratorSettingsList[i].amplitude * w;
                }

                if (totalWeight == 0f)
                    throw new InvalidOperationException("Total weight must be greater than zero.");

                // For LFOTypes, we will simply take the type from the first setting as enums cannot be weighted directly.
                LFOTypes mixedLfoType = lfoGeneratorSettingsList[0].lfoType;

                return new LFOGeneratorSettings
                {
                    speed = sumSpeed / totalWeight,
                    amplitude = sumAmplitude / totalWeight,
                    lfoType = mixedLfoType
                };
            }
        }


        public override void CreatePreset(Dictionary<string, float> presetValues)
        {
            // Extract tags from the current slider values
            string surfaceTag = AnysoundWhooshHelper
                .SurfaceFilenames[(int)(presetValues["Size"] / 2f * AnysoundWhooshHelper.SurfaceFilenames.Length)].Replace("size_", "");
            string sizeTag = AnysoundWhooshHelper.SizeFilenames[5 - (int)((presetValues["Movement"] / 3f) * 6)].Replace("movement_", "");
            string movementTag = AnysoundWhooshHelper.MovementTypeFilenames[(int)((presetValues["Duration"] / 2f) * 3)].Replace("duration_", "");

            // Generate an initial name for the preset
            string thisPresetName = $"Whoosh {surfaceTag} size-{sizeTag} {movementTag}";


            // Generate audio clip
            AudioClip generatedClip = AnysoundWhooshHelper.GenerateAudioClip(this, presetValues);

            List<string> thisTags = new List<string>()
            {
                "Whoosh",
                surfaceTag, // e.g., "grass", "sand", "mud", etc.
                sizeTag, // e.g., "xxl", "xl", "l", etc.
                movementTag, // e.g., "sneak", "walk", "run"
            };

            AnysoundBrowser.CreatePreset(thisPresetName, presetValues, thisTags, this, generatedClip);
        }
    }
}