using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Generators.Whoosh.Frontend
{
    public static class AnysoundWhooshHelper
    {
        // Slider ranges, shared by the editor windows and the preset/file names
        public const float MovementMin = 0f, MovementMax = 1f;
        public const float SizeMin = 0.5f, SizeMax = 2f;
        public const float DurationMin = 0.05f, DurationMax = 0.5f;

        // Words describing a whoosh, e.g. { "Large", "Fast", "Short" }
        public static string[] GetDescription(float size, float movement, float duration) => new[]
        {
            Describe(size, SizeMin, SizeMax, "Small", "Medium", "Large"),
            Describe(movement, MovementMin, MovementMax, "Slow", "Medium", "Fast"),
            Describe(duration, DurationMin, DurationMax, "Short", "Medium", "Long"),
        };

        static string Describe(float value, float min, float max, string low, string mid, string high)
        {
            float t = Mathf.InverseLerp(min, max, value);
            return t < 1f / 3f ? low : t < 2f / 3f ? mid : high;
        }

        public static readonly string[] SurfaceFilenames =
        {
            "surface_grass",
            "surface_sand",
            "surface_mud",
            "surface_wood",
            "surface_concrete",
        };

        public static readonly string[] SizeFilenames =
        {
            "size_xxl",
            "size_xl",
            "size_l",
            "size_m",
            "size_s",
            "size_xs",
        };

        public static readonly string[] MovementTypeFilenames =
        {
            "_sneak",
            "_walk",
            "_run",
        };


        public static AudioClip GenerateAudioClip(
            AnysoundWhooshObject anysoundFootstepObject,
            Dictionary<string, float> presetValues)
        {
            Debug.Log(presetValues);
            return AnysoundWhoosh.CreateMorphedAudioClip(anysoundFootstepObject,
                presetValues["Duration"],
                presetValues["Movement"],
                presetValues["Size"],
                presetValues["Fluctuation"]);
        }

        public static void UpdateWaveform(VisualElement waveformContainer, AudioClip clip)
        {
            if (clip != null)
            {
                waveformContainer.style.backgroundImage = new StyleBackground(WaveformMaker.GenerateWaveformTexture(clip));
            }
        }

    }
}