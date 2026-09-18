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

        public static void UpdateWaveform(VisualElement waveformContainer, AudioClip clip)
        {
            if (clip != null)
            {
                waveformContainer.style.backgroundImage = new StyleBackground(WaveformMaker.GenerateWaveformTexture(clip, 3));
            }
        }

        public static void UpdateMovementVisuals(VisualElement movementLabelsContainer, float currentMovementSpeed)
        {
            int index = 0;
            int currentMovementIndex = (int)Mathf.Clamp(((currentMovementSpeed / 2f) * 3), 0, 2);
            movementLabelsContainer.Query<Label>().ForEach((element =>
            {
                //if (element != _sizeIconsContainer)
                {
                    element.parent.style.backgroundColor =
                        (index == currentMovementIndex
                            ? AnysoundFootstepsEditorExtensions.HexToColor("60C75C")
                            : Color.clear);
                    element.style.color = (index == currentMovementIndex
                        ? Color.black
                        : AnysoundFootstepsEditorExtensions.HexToColor("60C75C"));
                    index++;
                }
            }));
        }

        public static void UpdateSurfaceIcons(VisualElement surfaceIconsContainer, float currentSurfaceType, int maxSurfaceType)
        {
            int index = 0;
            int surfaceIndex = (int)Mathf.Clamp(((currentSurfaceType / (maxSurfaceType - 1)) * maxSurfaceType), 0, maxSurfaceType - 1);
            surfaceIconsContainer.Query<AnysoundSurfaceToggleControl>().ForEach((element =>
            {
                string fileName = SurfaceFilenames[index];
                fileName += index == surfaceIndex ? "_fill" : "";

                element.IconImage = Resources.Load<Texture2D>(fileName);
                element.SetToggleState(index == surfaceIndex);
                index++;
            }));
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