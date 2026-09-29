using System;
using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Browser;
using UnityEngine;

namespace Anysound.Shared.Generators.UI
{
    [CreateAssetMenu(fileName = "New AnysoundUIObject", menuName = "Anysound/AnysoundUIObject")]
    public class AnysoundUIObject : AnysoundGeneratorBase
    {
        public const int MaxActionSteps = 4;

        /// <summary>
        /// A material is the main aesthetic choice (wood, plastic, paper, bubbles, 8-bit ...).
        /// It decides which set of samples the action sequencer triggers.
        /// </summary>
        [Serializable]
        public class UIMaterialSettings
        {
            public string name;
            public AnysoundSoundCollectionObject clipCollection;
            [Range(0f, 2f)] public float volume = 1f;

            [Header("Icon")] [Tooltip("Auto picks a built-in icon from the name")]
            public AnysoundIcon icon = AnysoundIcon.Auto;

            [Tooltip("Optional textures replacing the built-in icon (like the footstep surface_x / surface_x_fill pngs)")]
            public Texture2D customIcon;

            public Texture2D customIconSelected;
        }

        /// <summary>
        /// One trigger in the action mini sequencer.
        /// </summary>
        [Serializable]
        public struct UIActionStep
        {
            [Tooltip("Seconds to wait after the previous step before this step is triggered")] [Range(0f, 1f)]
            public float delay;

            [Range(-24f, 24f)] public float pitchSemitones;
            [Range(0f, 1f)] public float volume;

            public UIActionStep(float delay, float pitchSemitones, float volume)
            {
                this.delay = delay;
                this.pitchSemitones = pitchSemitones;
                this.volume = volume;
            }
        }

        /// <summary>
        /// An action (up, down, click, confirm ...) is a mini sequencer triggering the chosen material 1-4 times,
        /// each step with its own delay and pitch.
        /// </summary>
        [Serializable]
        public class UIActionSettings
        {
            public string name;
            public List<UIActionStep> steps = new();

            public UIActionSettings()
            {
            }

            public UIActionSettings(string name, params UIActionStep[] steps)
            {
                this.name = name;
                this.steps = new List<UIActionStep>(steps);
            }
        }

        [Serializable]
        public struct UIExtraActionClips
        {
            [Tooltip("Must match the name of an action, e.g. \"Down\"")]
            public string actionName;

            public AnysoundSoundCollectionObject clipCollection;
        }

        /// <summary>
        /// A little extra sound on top (guinea pig squeak, whistle, water bubble ...).
        /// Each extra has a version per action, falling back to the default clips.
        /// </summary>
        [Serializable]
        public class UIExtraSettings
        {
            public string name;
            [Range(0f, 2f)] public float volume = 1f;

            [Tooltip("When enabled the extra follows the action sequence (delays and pitches). When disabled it is triggered once, unpitched")]
            public bool followActionSequence = true;

            public AnysoundSoundCollectionObject defaultClipCollection;
            public List<UIExtraActionClips> actionClips = new();

            public AnysoundSoundCollectionObject GetClipCollection(string actionName)
            {
                foreach (var actionClip in actionClips)
                {
                    if (actionClip.clipCollection && string.Equals(actionClip.actionName, actionName, StringComparison.OrdinalIgnoreCase))
                        return actionClip.clipCollection;
                }

                return defaultClipCollection;
            }
        }

        [Header("Content")] [SerializeField] public List<UIMaterialSettings> materials = new();
        [SerializeField] public List<UIActionSettings> actions = new();
        [SerializeField] public List<UIExtraSettings> extras = new();

        [Header("Mix")] [Range(0f, 1f)] public float extraMaterialVolume = 0.7f;
        [Tooltip("Normalize the generated sound so all materials/actions have the same peak level")]
        public bool normalizeOutput = true;
        [Range(0.1f, 1f)] public float normalizePeak = 0.9f;

        [Header("Size (0 = narrow and short, 1 = wide and full)")]
        [SerializeField] public AnysoundAudioDSP.BandPassFilterSettings narrowFilter = new(2500f, 0.8f, 1f);
        [SerializeField] public AnysoundAudioDSP.BandPassFilterSettings wideFilter = new(1500f, 5f, 1f);
        [SerializeField] public AnysoundAudioDSP.ADSREnvelopeSettings shortEnvelope = new(0.001f, 0.04f, 0.5f, 0.05f, 0.06f);
        [SerializeField] public AnysoundAudioDSP.ADSREnvelopeSettings fullEnvelope = new(0.001f, 0.001f, 1f, 0.1f, 2f);

        public string[] MaterialNames => GetNames(materials, m => m.name);

        /// <param name="includeNone">Adds a "None" item first (used for the extra material, where 0 means none)</param>
        public List<AnysoundDropdown.Item> GetMaterialDropdownItems(bool includeNone = false)
        {
            var names = MaterialNames;
            var items = new List<AnysoundDropdown.Item>();
            if (includeNone)
                items.Add(new AnysoundDropdown.Item("None", AnysoundIcon.None));
            for (int i = 0; i < names.Length; i++)
                items.Add(new AnysoundDropdown.Item(names[i], materials[i].icon, materials[i].customIcon, materials[i].customIconSelected));
            return items;
        }
        public string[] ActionNames => GetNames(actions, a => a.name);
        public string[] ExtraNames => GetNames(extras, e => e.name);

        static string[] GetNames<T>(List<T> list, Func<T, string> getName)
        {
            if (list == null) return Array.Empty<string>();
            var names = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                names[i] = string.IsNullOrEmpty(getName(list[i])) ? $"Unnamed {i}" : getName(list[i]);
            return names;
        }

        public AnysoundAudioDSP.BandPassFilterSettings GetFilterSettings(float size) =>
            AnysoundAudioDSP.BandPassFilterSettings.Lerp(narrowFilter, wideFilter, size);

        public AnysoundAudioDSP.ADSREnvelopeSettings GetEnvelopeSettings(float size) =>
            AnysoundAudioDSP.ADSREnvelopeSettings.Lerp(shortEnvelope, fullEnvelope, size);

        // Called when the asset is created from the Create menu (or Reset from the inspector)
        void Reset()
        {
            actions = CreateDefaultActions();
        }

        void OnValidate()
        {
            if (actions == null) return;
            foreach (var action in actions)
            {
                if (action.steps != null && action.steps.Count > MaxActionSteps)
                {
                    Debug.LogWarning($"Action '{action.name}' can have at most {MaxActionSteps} steps");
                    action.steps.RemoveRange(MaxActionSteps, action.steps.Count - MaxActionSteps);
                }
            }
        }

        public static List<UIActionSettings> CreateDefaultActions()
        {
            return new List<UIActionSettings>
            {
                new("Click", new UIActionStep(0f, 0f, 1f)),
                new("Hover", new UIActionStep(0f, 3f, 0.5f)),
                new("Up", new UIActionStep(0f, 0f, 1f), new UIActionStep(0.07f, 4f, 0.8f)),
                new("Down", new UIActionStep(0f, 0f, 1f), new UIActionStep(0.07f, -4f, 0.8f)),
                new("Ok", new UIActionStep(0f, 0f, 1f), new UIActionStep(0.08f, 7f, 0.9f)),
                new("Confirm", new UIActionStep(0f, 0f, 1f), new UIActionStep(0.07f, 4f, 0.9f), new UIActionStep(0.07f, 7f, 0.9f)),
                new("Back", new UIActionStep(0f, 0f, 1f), new UIActionStep(0.08f, -5f, 0.8f)),
                new("Delete", new UIActionStep(0f, 0f, 1f), new UIActionStep(0.06f, -3f, 0.9f), new UIActionStep(0.06f, -6f, 0.8f)),
                new("In", new UIActionStep(0f, -2f, 0.7f), new UIActionStep(0.05f, 0f, 0.85f), new UIActionStep(0.05f, 2f, 1f)),
                new("Out", new UIActionStep(0f, 2f, 1f), new UIActionStep(0.05f, 0f, 0.85f), new UIActionStep(0.05f, -2f, 0.7f)),
                new("Error", new UIActionStep(0f, -1f, 1f), new UIActionStep(0.1f, -1f, 1f)),
            };
        }

        public override void CreatePreset(Dictionary<string, float> presetValues)
        {
            var parameters = AnysoundUIParameters.FromPresetValues(presetValues);

            string materialTag = SafeName(MaterialNames, parameters.material);
            string actionTag = SafeName(ActionNames, parameters.action);
            string extraMaterialTag = parameters.HasExtraMaterial ? SafeName(MaterialNames, parameters.ExtraMaterialIndex) : null;
            string extraTag = parameters.HasExtraSample ? SafeName(ExtraNames, parameters.ExtraSampleIndex) : null;

            string presetName = $"UI {materialTag}";
            if (extraMaterialTag != null) presetName += $"+{extraMaterialTag}";
            presetName += $" {actionTag}";
            if (extraTag != null) presetName += $" {extraTag}";

            List<string> tags = new List<string> { "ui", materialTag, actionTag };
            if (extraMaterialTag != null && !tags.Contains(extraMaterialTag)) tags.Add(extraMaterialTag);
            if (extraTag != null) tags.Add(extraTag);

            AudioClip generatedClip = AnysoundUIDSP.CreateAudioClip(this, parameters);

            AnysoundBrowser.CreatePreset(presetName, presetValues, tags, this, generatedClip);
        }

        static string SafeName(string[] names, int index) =>
            index >= 0 && index < names.Length ? names[index].ToLower() : "none";
    }

    /// <summary>
    /// The user facing values of the UI generator. Extra material and extra sample are stored as index + 1 so 0 means "none"
    /// </summary>
    [Serializable]
    public struct AnysoundUIParameters
    {
        public const string MaterialKey = "Material";
        public const string ExtraMaterialKey = "ExtraMaterial";
        public const string ActionKey = "Action";
        public const string ExtraSampleKey = "ExtraSample";
        public const string SizeKey = "Size";

        public int material;
        public int extraMaterial;
        public int action;
        public int extraSample;
        [Range(0f, 1f)] public float size;

        public bool HasExtraMaterial => extraMaterial > 0;
        public int ExtraMaterialIndex => extraMaterial - 1;
        public bool HasExtraSample => extraSample > 0;
        public int ExtraSampleIndex => extraSample - 1;

        public static AnysoundUIParameters Default => new() { size = 1f };

        public Dictionary<string, float> ToPresetValues()
        {
            return new Dictionary<string, float>
            {
                { MaterialKey, material },
                { ExtraMaterialKey, extraMaterial },
                { ActionKey, action },
                { ExtraSampleKey, extraSample },
                { SizeKey, size },
            };
        }

        public static AnysoundUIParameters FromPresetValues(Dictionary<string, float> presetValues)
        {
            float Get(string key, float fallback) => presetValues != null && presetValues.TryGetValue(key, out var v) ? v : fallback;
            return new AnysoundUIParameters
            {
                material = Mathf.RoundToInt(Get(MaterialKey, 0)),
                extraMaterial = Mathf.RoundToInt(Get(ExtraMaterialKey, 0)),
                action = Mathf.RoundToInt(Get(ActionKey, 0)),
                extraSample = Mathf.RoundToInt(Get(ExtraSampleKey, 0)),
                size = Get(SizeKey, 1f),
            };
        }

        public static AnysoundUIParameters FromPreset(AnysoundPresetObject preset)
        {
            return new AnysoundUIParameters
            {
                material = Mathf.RoundToInt(preset.GetPresetValue(MaterialKey)),
                extraMaterial = Mathf.RoundToInt(preset.GetPresetValue(ExtraMaterialKey)),
                action = Mathf.RoundToInt(preset.GetPresetValue(ActionKey)),
                extraSample = Mathf.RoundToInt(preset.GetPresetValue(ExtraSampleKey)),
                size = preset.GetPresetValue(SizeKey),
            };
        }
    }
}
