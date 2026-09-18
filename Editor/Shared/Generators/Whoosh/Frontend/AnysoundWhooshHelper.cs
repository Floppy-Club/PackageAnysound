using System.Collections.Generic;
using Anysound.Shared.Footsteps;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Generators.Whoosh.Frontend
{
    public static class AnysoundWhooshHelper
    {
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