using System;
using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Browser;
using Anysound.Shared.Frontend;
using UnityEngine;

namespace Anysound.Shared.Footsteps
{
    [CreateAssetMenu(fileName = "New FootstepObject", menuName = "Anysound/FootstepObject")]
    public class AnysoundFootstepObject : AnysoundGeneratorBase
    {

        
        
        [Serializable]
        public class FootstepSurfaceSettings
        {
            [SerializeField] string surfaceTypeName;
            public string SurfaceTypeName => surfaceTypeName;

            [Serializable]
            public class FootstepSettingsPreset
            {
                [SerializeField] string name;

                [Serializable]
                public struct AudioLayer
                {
                    [Range(0, 1)] public float value;
                    public AnysoundSoundCollectionObject clipCollection;

                    public AudioLayer(float value, AnysoundSoundCollectionObject clipCollection)
                    {
                        this.value = value;
                        this.clipCollection = clipCollection;
                    }
                }


                public AudioLayer[] audioLayers;

                [SerializeField] public AnysoundAudioDSP.ADSREnvelopeSettings ADSREnvelopeSettings;
                [SerializeField] public AnysoundAudioDSP.BandPassFilterSettings BandPassFilterSettings;
                [SerializeField] public AnysoundAudioDSP.DistortionSettings DistortionSettings;


                public FootstepSettingsPreset(string name)
                {
                    this.name = name;
                }

                public AudioClip GetAudioClip()
                {
                    List<AudioClip> clips = new List<AudioClip>();
                    List<float> weights = new List<float>();
                    foreach (AudioLayer layer in audioLayers)
                    {
                        clips.Add(layer.clipCollection.GetClip());
                        weights.Add(layer.value);
                    }

                    return AnysoundAudioDSP.Mix(clips, weights);
                }
            }


            [SerializeField] private List<FootstepSettingsPreset> footstepSizeSettings;

            public List<FootstepSettingsPreset> FootstepSizeSettings
            {
                get => footstepSizeSettings;
                set => footstepSizeSettings = value;
            }


            [SerializeField] public List<AnysoundCurve> footstepStyleEnvelopeCurves;

            public AudioClip GetAudioClip(float mixValue)
            {
                List<AudioClip> mixClips = new();
                List<float> stepSizeWeights = new List<float>();
                for (var i = 0; i < footstepSizeSettings.Count; i++)
                {
                    var preset = footstepSizeSettings[i];
                    float distance = Mathf.Abs(i - mixValue);
                    stepSizeWeights.Add(Mathf.Clamp01(1 - distance));
                    mixClips.Add(preset.GetAudioClip());
                }

                return AnysoundAudioDSP.Mix(mixClips, stepSizeWeights);
            }

            public AnysoundAudioDSP.BandPassFilterSettings GetBandpassFilterSettings(float mixValue)
            {
                List<float> stepSizeWeights = new List<float>();
                List<AnysoundAudioDSP.BandPassFilterSettings> bandPassFilterSettingsList = new();
                for (var i = 0; i < footstepSizeSettings.Count; i++)
                {
                    var preset = footstepSizeSettings[i];
                    float distance = Mathf.Abs(i - mixValue);
                    stepSizeWeights.Add(Mathf.Clamp01(1 - distance));
                    bandPassFilterSettingsList.Add(preset.BandPassFilterSettings);
                }

                return AnysoundAudioDSP.BandPassFilterSettings.WeightedMix(bandPassFilterSettingsList, stepSizeWeights);
            }

            public AnysoundAudioDSP.DistortionSettings GetDistortionSettings(float mixValue)
            {
                List<float> stepSizeWeights = new List<float>();
                List<AnysoundAudioDSP.DistortionSettings> distortionSettingsList = new();
                for (var i = 0; i < footstepSizeSettings.Count; i++)
                {
                    var preset = footstepSizeSettings[i];
                    float distance = Mathf.Abs(i - mixValue);
                    stepSizeWeights.Add(Mathf.Clamp01(1 - distance));
                    distortionSettingsList.Add(preset.DistortionSettings);
                }

                return AnysoundAudioDSP.DistortionSettings.WeightedMix(distortionSettingsList, stepSizeWeights);
            }

            public AnysoundAudioDSP.ADSREnvelopeSettings GetEnvelopeSettings(float mixValue)
            {
                List<float> stepSizeWeights = new List<float>();
                List<AnysoundAudioDSP.ADSREnvelopeSettings> envelopeSettingsList = new();
                for (var i = 0; i < footstepSizeSettings.Count; i++)
                {
                    var preset = footstepSizeSettings[i];
                    float distance = Mathf.Abs(i - mixValue);
                    stepSizeWeights.Add(Mathf.Clamp01(1 - distance));
                    envelopeSettingsList.Add(preset.ADSREnvelopeSettings);
                }

                return AnysoundAudioDSP.ADSREnvelopeSettings.WeightedMix(envelopeSettingsList, stepSizeWeights);
            }

            public AnysoundAudioDSP.CurveEnvelopeSettings GetFootstepStyleEnvelopeCurve(float mixValue)
            {
                List<AnysoundAudioDSP.CurveEnvelopeSettings> footstepStyleEnvelopeCurvesList = new();
                List<float> stepSizeWeights = new List<float>();
                for (var i = 0; i < footstepStyleEnvelopeCurves.Count; i++)
                {
                    var curve = footstepStyleEnvelopeCurves[i].curve;
                    float distance = Mathf.Abs(i - mixValue);
                    stepSizeWeights.Add(Mathf.Clamp01(1 - distance));
                    footstepStyleEnvelopeCurvesList.Add(new AnysoundAudioDSP.CurveEnvelopeSettings(curve));
                }

                return AnysoundAudioDSP.CurveEnvelopeSettings.WeightedMix(footstepStyleEnvelopeCurvesList, stepSizeWeights);
            }
        }

        [SerializeField] public List<FootstepSurfaceSettings> surfaceSettings;

        // Old presets store one surface value scrubbing through all surfaces. New presets crossfade between two surfaces
        public const string SurfaceTypeKey = "SurfaceType";
        public const string SurfaceAKey = "SurfaceA";
        public const string SurfaceBKey = "SurfaceB";
        public const string SurfaceCrossfadeKey = "SurfaceCrossfade";

        /// <summary>
        /// Converts an old surface value (e.g. 2.4 = between surface 2 and 3) to the same mix as a crossfade
        /// </summary>
        public void SurfaceTypeToCrossfade(float surfaceType, out int surfaceA, out int surfaceB, out float crossfade)
        {
            int last = Mathf.Max(0, surfaceSettings.Count - 1);
            surfaceType = Mathf.Clamp(surfaceType, 0, last);
            surfaceA = Mathf.FloorToInt(surfaceType);
            surfaceB = Mathf.Min(surfaceA + 1, last);
            crossfade = surfaceB == surfaceA ? 0f : surfaceType - surfaceA;
        }

        public void GetSurfaceCrossfade(Func<string, bool> hasValue, Func<string, float> getValue, out int surfaceA, out int surfaceB,
            out float crossfade)
        {
            if (hasValue(SurfaceAKey))
            {
                surfaceA = Mathf.RoundToInt(getValue(SurfaceAKey));
                surfaceB = Mathf.RoundToInt(getValue(SurfaceBKey));
                crossfade = getValue(SurfaceCrossfadeKey);
            }
            else
            {
                SurfaceTypeToCrossfade(getValue(SurfaceTypeKey), out surfaceA, out surfaceB, out crossfade);
            }
        }

        static string GetSurfaceTag(int surfaceIndex) =>
            surfaceIndex >= 0 && surfaceIndex < AnysoundFootstepsHelper.SurfaceFilenames.Length
                ? AnysoundFootstepsHelper.SurfaceFilenames[surfaceIndex].Replace("surface_", "")
                : $"surface{surfaceIndex}";

        public override void CreatePreset(Dictionary<string, float> presetValues)
        {
            float currentSizeValue = presetValues["Size"];
            float currentMovementSpeedValue = presetValues["MovementSpeed"];
            GetSurfaceCrossfade(presetValues.ContainsKey, key => presetValues.TryGetValue(key, out var v) ? v : 0f,
                out int surfaceA, out int surfaceB, out float crossfade);

            int maxSize = surfaceSettings[0].FootstepSizeSettings.Count - 1;
            // Extract tags from the current slider values. A real crossfade gets both surfaces
            string surfaceTagA = GetSurfaceTag(surfaceA);
            string surfaceTagB = GetSurfaceTag(surfaceB);
            bool isMixed = surfaceA != surfaceB && crossfade > 0f && crossfade < 1f;
            string surfaceTag = isMixed ? $"{surfaceTagA}-{surfaceTagB}" : crossfade >= 1f ? surfaceTagB : surfaceTagA;
            string sizeTag = AnysoundFootstepsHelper.SizeFilenames[Mathf.RoundToInt(Mathf.Lerp(5, 0, currentSizeValue / maxSize))]
                .Replace("size_", "");
            string movementTag = AnysoundFootstepsHelper.MovementTypeFilenames[(int)((currentMovementSpeedValue / 2f) * 3)].Replace("_", "");

            // Generate an initial name for the preset
            string presetName = $"Footstep {surfaceTag} size-{sizeTag} {movementTag}";

            List<string> tags = new List<string>()
            {
                "footstep",
                sizeTag, // e.g., "xxl", "xl", "l", etc.
                movementTag, // e.g., "sneak", "walk", "run"
            };
            // e.g., "grass", "sand", "mud", etc.
            if (isMixed)
            {
                tags.Add(surfaceTagA);
                tags.Add(surfaceTagB);
            }
            else
            {
                tags.Add(surfaceTag);
            }


            // Generate audio clip
            AudioClip generatedClip = AnysoundFootstepsHelper.GenerateAudioClip(
                this,
                currentSizeValue,
                currentMovementSpeedValue,
                surfaceA,
                surfaceB,
                crossfade
            );

            AnysoundBrowser.CreatePreset(presetName, presetValues, tags, this, generatedClip);
        }
    }
}