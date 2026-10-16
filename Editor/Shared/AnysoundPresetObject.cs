using System;
using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Anysound.Shared
{
    [CreateAssetMenu(menuName = "Anysound/SoundEffects/AnysoundPresetObject")]
    public class AnysoundPresetObject : ScriptableObject, ISerializationCallbackReceiver
    {
        [Serializable]
        struct PresetValue
        {
            public string key;
            public float value;
        }
        public string soundName;
        public List<string> tags = new();
        public Texture2D waveformTexture;

        [FormerlySerializedAs("generatorSettingsObject")]
        public AnysoundGeneratorBase generatorObject;

        [SerializeField] private bool isLoadable = true;
        public bool IsLoadable => isLoadable;

        // Unity can't serialize dictionaries, so the values are stored as a list and looked up through the dictionary.
        // The list keeps the old field name and key/value layout, so existing preset assets load their values.
        [SerializeField] private List<PresetValue> _presetValues = new();
        [NonSerialized] private Dictionary<string, float> _presetValuesLookup;

        public void SetPresetValues(Dictionary<string, float> presetValues)
        {
            _presetValuesLookup = presetValues;
        }

        public void OnBeforeSerialize()
        {
            if (_presetValuesLookup == null) return;

            _presetValues.Clear();
            foreach (var pair in _presetValuesLookup)
            {
                _presetValues.Add(new PresetValue { key = pair.Key, value = pair.Value });
            }
        }

        public void OnAfterDeserialize()
        {
            _presetValuesLookup = new Dictionary<string, float>();
            if (_presetValues == null) return;

            foreach (var presetValue in _presetValues)
            {
                if (presetValue.key != null)
                {
                    _presetValuesLookup[presetValue.key] = presetValue.value;
                }
            }
        }

        public void SetPresetValue(string key, float value)
        {
            if (_presetValuesLookup != null && _presetValuesLookup.ContainsKey(key))
            {
                _presetValuesLookup[key] = value;
            }
        }

        public float GetPresetValue(string key)
        {
            return _presetValuesLookup != null && _presetValuesLookup.TryGetValue(key, out float value) ? value : 0f;
        }

        public bool HasPresetValue(string key) => _presetValuesLookup != null && _presetValuesLookup.ContainsKey(key);

        public void Create(string thisName, List<string> thisTags, AnysoundGeneratorBase newFootstepObject)
        {
            soundName = thisName;
            tags = thisTags;
            this.generatorObject = newFootstepObject;
        }

        private void CreateAndAddWaveformTexture(AudioClip previewClip)
        {
            if (previewClip == null)
            {
                Debug.LogError("Preview clip is null. Can't generate waveform.");
                return;
            }

            var newWaveform = WaveformMaker.GenerateWaveformTexture(
                previewClip,
                gain: 2,
                width: 400, // default width
                height: 100, // default height
                foregroundColor: null, // use default foreground color
                backgroundColor: null // use default background color
            );

            // Generate the waveform texture
            newWaveform.name = "Waveform";
            if (newWaveform == null)
            {
                Debug.LogError("Failed to generate waveform texture.");
                return;
            }

            // Get the path of the current scriptable object
            string assetPath = AssetDatabase.GetAssetPath(this);

            // Find existing subassets of type Texture2D
            Texture2D existingSubAsset = null;
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in subAssets)
            {
                if (asset is Texture2D texture /* && texture.name == newWaveform.name*/)
                {
                    existingSubAsset = texture;
                    break;
                }
            }

            if (existingSubAsset != null)
            {
                // Update the existing texture
                existingSubAsset.SetPixels(newWaveform.GetPixels());
                existingSubAsset.Apply();
                waveformTexture = existingSubAsset;
                existingSubAsset.name = "Waveform";
                Debug.Log("Waveform texture updated.");
            }
            else
            {
                // Add the texture as a subasset to this scriptable object
                AssetDatabase.AddObjectToAsset(newWaveform, this);
                waveformTexture = newWaveform;
                Debug.Log("Waveform texture generated and added as a subasset.");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        [FormerlySerializedAs("_previewSamples")] [SerializeField]
        private float[] previewSamples;

        // The format of previewSamples. 0 in presets saved before it was stored
        [SerializeField] private int previewChannels;
        [SerializeField] private int previewFrequency;

        public void AddAudioClip(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogError("AudioClip is null. Cannot add audio samples.");
                return;
            }

            // Get the number of samples and channels from the clip
            int sampleCount = clip.samples * clip.channels;

            // Create a float array to store the samples
            previewSamples = new float[sampleCount];

            // Fill the array with the audio data from the clip
            clip.GetData(previewSamples, 0);
            previewChannels = clip.channels;
            previewFrequency = clip.frequency;

            CreateAndAddWaveformTexture(clip);
        }

        // Set for exported sounds, which play the exported file instead of stored samples
        [NonSerialized] AudioClip _exportedClip;
        public bool IsExported => _exportedClip;

        public void SetExportedClip(AudioClip clip)
        {
            _exportedClip = clip;
            clip.LoadAudioData();
            try
            {
                waveformTexture = WaveformMaker.GenerateWaveformTexture(clip, gain: 2, width: 400, height: 100,
                    foregroundColor: null, backgroundColor: null);
                waveformTexture.hideFlags = HideFlags.DontSave;
            }
            catch (Exception e)
            {
                // Streamed clips can't be read, so they get no waveform
                Debug.LogWarning($"Could not draw the waveform of '{clip.name}': {e.Message}");
            }
        }

        public AudioClip GetAudioClip()
        {
            if (_exportedClip) return _exportedClip;

            if (previewSamples == null || previewSamples.Length == 0)
            {
                Debug.LogError("No preview samples available. Cannot generate AudioClip.");
                return null;
            }

            // Older presets didn't store the format: whoosh sounds are mono, the others stereo, all at 44100 Hz
            int channels = previewChannels > 0 ? previewChannels : generatorObject is Generators.Whoosh.AnysoundWhooshObject ? 1 : 2;
            int sampleRate = previewFrequency > 0 ? previewFrequency : 44100;


            // Create a new AudioClip with the same properties
            AudioClip generatedClip = AudioClip.Create(
                "GeneratedAudio",
                previewSamples.Length / channels,
                channels,
                sampleRate,
                false
            );

            // Set the sample data to the AudioClip
            generatedClip.SetData(previewSamples, 0);

            return generatedClip;
        }
    }
}