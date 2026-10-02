using System;
using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Browser;
using UnityEngine;
using UnityEngine.Serialization;

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
        /// One trigger in the action mini sequencer. Every parameter has a min/max range, the frontend's random action button
        /// picks a value within it (see AnysoundUIParameters.actionSeed)
        /// </summary>
        [Serializable]
        public struct UIActionStep
        {
            public const float MaxDelay = 1f;
            public const float MaxPitch = 24f;
            public const float MaxDuration = 1f;

            // The min fields keep the old single value names, so existing actions load their values as min
            [Tooltip("Seconds to wait after the previous step before this step is triggered")]
            [FormerlySerializedAs("delay")] public float delayMin;
            public float delayMax;

            [FormerlySerializedAs("pitchSemitones")] public float pitchMin;
            public float pitchMax;

            [FormerlySerializedAs("volume")] public float volumeMin;
            public float volumeMax;

            [Tooltip("Seconds this step plays before it is faded out. 0 = the whole sample")]
            [FormerlySerializedAs("duration")] public float durationMin;
            public float durationMax;

            // False for steps saved before the ranges existed. Their max is then set to their min (see MigrateStepRanges)
            [SerializeField, HideInInspector] internal bool hasRanges;

            public UIActionStep(float delay, float pitchSemitones, float volume, float duration = 0f)
            {
                delayMin = delayMax = delay;
                pitchMin = pitchMax = pitchSemitones;
                volumeMin = volumeMax = volume;
                durationMin = durationMax = duration;
                hasRanges = true;
            }

            /// <summary>
            /// The step values at the given positions in the ranges (0 = min, 1 = max)
            /// </summary>
            public ResolvedStep Resolve(float delayT, float pitchT, float volumeT, float durationT) => new()
            {
                delay = Mathf.Max(0f, Mathf.Lerp(delayMin, delayMax, delayT)),
                pitchSemitones = Mathf.Lerp(pitchMin, pitchMax, pitchT),
                volume = Mathf.Max(0f, Mathf.Lerp(volumeMin, volumeMax, volumeT)),
                duration = Mathf.Max(0f, Mathf.Lerp(durationMin, durationMax, durationT)),
            };
        }

        public struct ResolvedStep
        {
            public float delay;
            public float pitchSemitones;
            public float volume;
            public float duration;
        }

        /// <summary>
        /// An action (up, down, click, confirm ...) is a mini sequencer triggering the chosen material 1-4 times,
        /// each step with its own delay and pitch.
        /// </summary>
        [Serializable]
        public class UIActionSettings
        {
            public string name;

            [Tooltip("Auto picks a built-in icon from the name")]
            public AnysoundIcon icon = AnysoundIcon.Auto;

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
            public bool followActionSequence;

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

        public List<AnysoundDropdown.Item> GetActionDropdownItems()
        {
            var names = ActionNames;
            var items = new List<AnysoundDropdown.Item>();
            for (int i = 0; i < names.Length; i++)
            {
                var icon = actions[i].icon == AnysoundIcon.Auto ? AnysoundIconElement.FromActionName(names[i]) : actions[i].icon;
                items.Add(new AnysoundDropdown.Item(names[i], icon));
            }

            return items;
        }

        /// <summary>
        /// Extras are shown without icons, with "None" first (0 means no extra)
        /// </summary>
        public List<AnysoundDropdown.Item> GetExtraDropdownItems()
        {
            var items = new List<AnysoundDropdown.Item> { new("None", AnysoundIcon.None) };
            foreach (var extraName in ExtraNames)
                items.Add(new AnysoundDropdown.Item(extraName, AnysoundIcon.Generic));
            return items;
        }

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
        /// <summary>
        /// One text item per clip in the collection, so the exact sample can be picked
        /// </summary>
        public static List<AnysoundDropdown.Item> GetClipDropdownItems(AnysoundSoundCollectionObject collection)
        {
            var items = new List<AnysoundDropdown.Item>();
            int count = collection ? collection.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var clip = collection.GetClip(i);
                items.Add(new AnysoundDropdown.Item(clip ? TrimClipName(clip.name) : $"Clip {i + 1}", AnysoundIcon.None));
            }

            return items;
        }

        static readonly string[] ClipNamePrefixes = { "AnySound_UI_Extra_Layer_", "Transient_" };

        /// <summary>
        /// "Transient_DirtCrumble_3" -> "DirtCrumble 3"
        /// </summary>
        static string TrimClipName(string clipName)
        {
            foreach (var prefix in ClipNamePrefixes)
            {
                if (clipName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    clipName = clipName.Substring(prefix.Length);
                    break;
                }
            }

            return clipName.Replace('_', ' ').Trim();
        }

        public AnysoundSoundCollectionObject GetMaterialCollection(int materialIndex) =>
            materials != null && materialIndex >= 0 && materialIndex < materials.Count ? materials[materialIndex].clipCollection : null;

        public AnysoundSoundCollectionObject GetExtraCollection(int extraIndex, int actionIndex)
        {
            if (extras == null || extraIndex < 0 || extraIndex >= extras.Count) return null;
            string actionName = actions != null && actionIndex >= 0 && actionIndex < actions.Count ? actions[actionIndex].name : "";
            return extras[extraIndex].GetClipCollection(actionName);
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

        void OnEnable()
        {
            MigrateStepRanges();
        }

        void OnValidate()
        {
            if (actions == null) return;
            MigrateStepRanges();
            foreach (var action in actions)
            {
                if (action.steps != null && action.steps.Count > MaxActionSteps)
                {
                    Debug.LogWarning($"Action '{action.name}' can have at most {MaxActionSteps} steps");
                    action.steps.RemoveRange(MaxActionSteps, action.steps.Count - MaxActionSteps);
                }
            }
        }

        /// <summary>
        /// Steps saved before the min/max ranges only have their value loaded into min. Max is set to the same value,
        /// so they sound exactly like before
        /// </summary>
        void MigrateStepRanges()
        {
            if (actions == null) return;
            bool changed = false;
            foreach (var action in actions)
            {
                if (action.steps == null) continue;
                for (int i = 0; i < action.steps.Count; i++)
                {
                    var step = action.steps[i];
                    if (step.hasRanges) continue;
                    step.delayMax = step.delayMin;
                    step.pitchMax = step.pitchMin;
                    step.volumeMax = step.volumeMin;
                    step.durationMax = step.durationMin;
                    step.hasRanges = true;
                    action.steps[i] = step;
                    changed = true;
                }
            }

#if UNITY_EDITOR
            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
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
        public const string MaterialClipKey = "MaterialClip";
        public const string ExtraMaterialClipKey = "ExtraMaterialClip";
        public const string ExtraSampleClipKey = "ExtraSampleClip";
        public const string ActionSeedKey = "ActionSeed";

        // Seeds are stored as floats in presets, so they are kept below 2^24 where floats are still exact
        public const int MaxActionSeed = 1 << 24;

        public int material;
        public int extraMaterial;
        public int action;
        public int extraSample;
        [Range(0f, 1f)] public float size;

        // Index of the exact clip within the chosen material / extra material / extra sample collection
        public int materialClip;
        public int extraMaterialClip;
        public int extraSampleClip;

        // Picks the action step values within their min/max ranges. 0 = the middle of every range
        public int actionSeed;

        public bool HasExtraMaterial => extraMaterial > 0;
        public int ExtraMaterialIndex => extraMaterial - 1;
        public bool HasExtraSample => extraSample > 0;
        public int ExtraSampleIndex => extraSample - 1;

        public static AnysoundUIParameters Default => new() { size = 1f };

        public static int NewActionSeed() => UnityEngine.Random.Range(1, MaxActionSeed);

        /// <summary>
        /// The step values of the action for this seed. The random positions are drawn in a fixed order,
        /// so the same seed always gives the same sound, no matter how the ranges are set
        /// </summary>
        public List<AnysoundUIObject.ResolvedStep> ResolveSteps(IReadOnlyList<AnysoundUIObject.UIActionStep> steps)
        {
            var rng = actionSeed != 0 ? new System.Random(actionSeed) : null;
            float Next() => rng != null ? (float)rng.NextDouble() : 0.5f;

            var resolved = new List<AnysoundUIObject.ResolvedStep>();
            foreach (var step in steps)
            {
                float delayT = Next(), pitchT = Next(), volumeT = Next(), durationT = Next();
                resolved.Add(step.Resolve(delayT, pitchT, volumeT, durationT));
            }

            return resolved;
        }

        public Dictionary<string, float> ToPresetValues()
        {
            return new Dictionary<string, float>
            {
                { MaterialKey, material },
                { ExtraMaterialKey, extraMaterial },
                { ActionKey, action },
                { ExtraSampleKey, extraSample },
                { SizeKey, size },
                { MaterialClipKey, materialClip },
                { ExtraMaterialClipKey, extraMaterialClip },
                { ExtraSampleClipKey, extraSampleClip },
                { ActionSeedKey, actionSeed },
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
                materialClip = Mathf.RoundToInt(Get(MaterialClipKey, 0)),
                extraMaterialClip = Mathf.RoundToInt(Get(ExtraMaterialClipKey, 0)),
                extraSampleClip = Mathf.RoundToInt(Get(ExtraSampleClipKey, 0)),
                actionSeed = Mathf.RoundToInt(Get(ActionSeedKey, 0)),
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
                materialClip = Mathf.RoundToInt(preset.GetPresetValue(MaterialClipKey)),
                extraMaterialClip = Mathf.RoundToInt(preset.GetPresetValue(ExtraMaterialClipKey)),
                extraSampleClip = Mathf.RoundToInt(preset.GetPresetValue(ExtraSampleClipKey)),
                actionSeed = Mathf.RoundToInt(preset.GetPresetValue(ActionSeedKey)),
            };
        }
    }
}
