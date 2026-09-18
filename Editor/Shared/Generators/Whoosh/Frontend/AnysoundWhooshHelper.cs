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


        public static void UpdateSurfaceIcons(VisualElement surfaceIconsContainer, float currentSurfaceType)
        {
            int index = 0;
            int surfaceIndex = (int)Mathf.Clamp(((currentSurfaceType / 2f) * 3), 0, 2);
            surfaceIconsContainer.Query<AnysoundSurfaceToggleControl>().ForEach((element =>
            {
                string fileName = SurfaceFilenames[index];
                fileName += index == surfaceIndex ? "_fill" : "";

                element.IconImage = Resources.Load<Texture2D>(fileName);
                element.SetToggleState(index == surfaceIndex);
                index++;
            }));
        }

        public static void UpdateSizeImages(VisualElement sizeIconsContainer, float value, int currentMovementIndex)
        {
            int sizeIndex = 5 - (int)((value / 3f) * 6);
            if (sizeIndex < 0) sizeIndex = 0;
            int index2 = 5;
            sizeIconsContainer.Query<VisualElement>().ForEach(element =>
            {
                if (element != sizeIconsContainer)
                {
                    element.style.backgroundImage = new StyleBackground(GetSizeImage(index2, index2 == sizeIndex, currentMovementIndex));
                    index2--;
                }
            });
        }

        public static void UpdateSizeLabels(VisualElement sizeLabelsContainer, float value)
        {
            int sizeIndex = 5 - (int)((value / 3f) * 6);
            if (sizeIndex < 0) sizeIndex = 0;
            int index = 0;
            sizeLabelsContainer.Query<Label>().ForEach(label =>
            {
                label.style.color = index == sizeIndex
                    ? new Color(0.2352941176f, 0.6509803922f, 1)
                    : new Color(0.1254901961f, 0.368627451f, 0.5764705882f);
                index++;
            });
        }

        static VectorImage GetSizeImage(int currentSizeIndex, bool isActive, int currentMovementIndex)
        {
            string sizeFilename = SizeFilenames[currentSizeIndex] + MovementTypeFilenames[currentMovementIndex];
            sizeFilename += isActive ? "_fill" : "";
            return Resources.Load<VectorImage>(sizeFilename);
        }
    }
}