using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Anysound.Shared
{
    [CreateAssetMenu(menuName = "Anysound/SoundEffects/AnysoundPresetObject")]
    public class AnysoundPresetObject : ScriptableObject
    {
        public string soundName;
        public List<string> tags = new();
        public Texture2D waveformTexture;

        [FormerlySerializedAs("generatorSettingsObject")]
        public AnysoundGeneratorBase generatorObject;

        [SerializeField] private bool isLoadable = true;
        public bool IsLoadable => isLoadable;

        [SerializeField] private Dictionary<string, float> _presetValues;


        public void SetPresetValues(Dictionary<string, float> presetValues)
        {
            _presetValues = presetValues;
        }

        public void SetPresetValue(string key, float value)
        {
            if (_presetValues.ContainsKey(key))
            {
                _presetValues[key] = value;
            }
        }

        public float GetPresetValue(string key)
        {
            return _presetValues.ContainsKey(key) ? _presetValues[key] : 0f;
        }

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

            CreateAndAddWaveformTexture(clip);
        }

        public AudioClip GetAudioClip()
        {
            if (previewSamples == null || previewSamples.Length == 0)
            {
                Debug.LogError("No preview samples available. Cannot generate AudioClip.");
                return null;
            }

            // Create a new AudioClip with appropriate parameters
            // Using 44100 as a default sample rate if no other information is available
            int channels = 2; // Assume mono by default
            int sampleRate = 44100;


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