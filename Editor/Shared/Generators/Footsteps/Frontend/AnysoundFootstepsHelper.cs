using System.Collections.Generic;
using Anysound.Shared.Footsteps;
using Anysound.Shared.Generators.Footsteps;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Frontend
{
    public static class AnysoundFootstepsHelper
    {
        public static readonly string[] SurfaceFilenames =
        {
            "surface_grass",
            "surface_dirt",
            "surface_water",
            "surface_sand",
            "surface_wood",
            "surface_gravel",
            "surface_snow",
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
            AnysoundFootstepObject anysoundFootstepObject,
            float currentSizeValue,
            float currentMovementSpeed,
            float currentSurfaceType)
        {
            return AnysoundFootstepDSP.CreateMorphedAudioClip(anysoundFootstepObject, currentSizeValue, currentMovementSpeed, currentSurfaceType);
        }

        public static AudioClip GenerateAudioClip(
            AnysoundFootstepObject anysoundFootstepObject,
            float currentSizeValue,
            float currentMovementSpeed,
            int surfaceA,
            int surfaceB,
            float crossfade)
        {
            return AnysoundFootstepDSP.CreateCrossfadedAudioClip(anysoundFootstepObject, currentSizeValue, currentMovementSpeed, surfaceA, surfaceB,
                crossfade);
        }

        /// <summary>
        /// One item per surface, with the surface_x / surface_x_fill pngs as icons
        /// </summary>
        public static List<AnysoundDropdown.Item> GetSurfaceDropdownItems(AnysoundFootstepObject anysoundFootstepObject)
        {
            var items = new List<AnysoundDropdown.Item>();
            for (int i = 0; i < anysoundFootstepObject.surfaceSettings.Count; i++)
            {
                string fileName = i < SurfaceFilenames.Length ? SurfaceFilenames[i] : null;
                string surfaceName = anysoundFootstepObject.surfaceSettings[i].SurfaceTypeName;
                if (string.IsNullOrEmpty(surfaceName))
                    surfaceName = fileName != null ? fileName.Replace("surface_", "") : $"Surface {i + 1}";

                items.Add(fileName != null
                    ? new AnysoundDropdown.Item(surfaceName, AnysoundIcon.Auto, Resources.Load<Texture2D>(fileName),
                        Resources.Load<Texture2D>(fileName + "_fill"))
                    : new AnysoundDropdown.Item(surfaceName));
            }

            return items;
        }

        public static void UpdateWaveform(VisualElement waveformContainer, AudioClip clip)
        {
            if (clip != null)
            {
                waveformContainer.style.backgroundImage = new StyleBackground(WaveformMaker.GenerateWaveformTexture(clip, 3));
            }
        }

        public static void UpdateSizeImages(VisualElement sizeIconsContainer, float value, float maxValue, int currentMovementIndex)
        {
            int sizeIndex = Mathf.RoundToInt(Mathf.Lerp(5, 0, value / maxValue));
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

        public static void UpdateSizeLabels(VisualElement sizeLabelsContainer, float value, float maxValue)
        {
            int sizeIndex = Mathf.RoundToInt(Mathf.Lerp(5, 0, value / maxValue));
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

        static Texture2D GetSizeImage(int currentSizeIndex, bool isActive, int currentMovementIndex)
        {
            string sizeFilename = SizeFilenames[currentSizeIndex] + MovementTypeFilenames[currentMovementIndex];
            sizeFilename += isActive ? "_fill" : "";
            return Resources.Load<Texture2D>(sizeFilename);
        }

        // This class allows you to open the editor window with a specific object
        public static class AnysoundFootstepsEditorExtensions
        {
#if UNITY_EDITOR
            [MenuItem("Assets/Edit Footstep Object", true)]
            public static bool ValidateEditFootstepObject()
            {
                return Selection.activeObject is AnysoundFootstepObject;
            }
#endif

            public static Color HexToColor(string hex)
            {
                // Remove # if present
                if (hex.StartsWith("#"))
                {
                    hex = hex.Substring(1);
                }

                // Handle different hex formats
                if (hex.Length == 6) // RRGGBB format
                {
                    float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;

                    return new Color(r, g, b);
                }
                else if (hex.Length == 8) // RRGGBBAA format
                {
                    float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    float a = int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f;

                    return new Color(r, g, b, a);
                }
                else if (hex.Length == 3) // RGB format (shorthand)
                {
                    float r = int.Parse(hex[0].ToString() + hex[0].ToString(), System.Globalization.NumberStyles.HexNumber) / 255f;
                    float g = int.Parse(hex[1].ToString() + hex[1].ToString(), System.Globalization.NumberStyles.HexNumber) / 255f;
                    float b = int.Parse(hex[2].ToString() + hex[2].ToString(), System.Globalization.NumberStyles.HexNumber) / 255f;

                    return new Color(r, g, b);
                }
                else
                {
                    Debug.LogError($"Invalid hex color format: {hex}");
                    return Color.white; // Return white as fallback
                }
            }
        }
    }
}