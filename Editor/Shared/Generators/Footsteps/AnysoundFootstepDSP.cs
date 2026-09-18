using System;
using System.Collections.Generic;
using System.IO;
using Anysound.Shared.Footsteps;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Anysound.Shared.Generators.Footsteps
{
    public static class AnysoundFootstepDSP
    {
        private static bool _isPreviewing;
        public static bool IsPreviewing => _isPreviewing;

        public static AudioClip CreateMorphedAudioClip(AnysoundFootstepObject anysoundFootstepObject, float currentSize, float currentMovementSpeed,
            float currentSurfaceType)
        {
            List<AnysoundAudioDSP.BandPassFilterSettings> surfaceBandPassList = new List<AnysoundAudioDSP.BandPassFilterSettings>();
            List<AnysoundAudioDSP.DistortionSettings> surfaceDistortionList = new List<AnysoundAudioDSP.DistortionSettings>();

            List<AnysoundAudioDSP.ADSREnvelopeSettings> surfaceEnvelopeList = new List<AnysoundAudioDSP.ADSREnvelopeSettings>();
            List<AnysoundAudioDSP.CurveEnvelopeSettings> footstepStyleEnvelopeSettingsList = new List<AnysoundAudioDSP.CurveEnvelopeSettings>();


            List<AudioClip> footstepClips = new List<AudioClip>();
            List<float> surfaceWeightList = new List<float>();
            for (var i = 0; i < anysoundFootstepObject.surfaceSettings.Count; i++)
            {
                var footstepSurface = anysoundFootstepObject.surfaceSettings[i];
                footstepClips.Add(footstepSurface.GetAudioClip(currentSize));
                surfaceBandPassList.Add(footstepSurface.GetBandpassFilterSettings(currentSize));
                surfaceEnvelopeList.Add(footstepSurface.GetEnvelopeSettings(currentSize));
                footstepStyleEnvelopeSettingsList.Add(footstepSurface.GetFootstepStyleEnvelopeCurve(currentMovementSpeed));
                surfaceDistortionList.Add(footstepSurface.GetDistortionSettings(currentSize));

                float distance = Mathf.Abs(i - currentSurfaceType);
                surfaceWeightList.Add(Mathf.Clamp01(1 - distance));
            }

            var result = AnysoundAudioDSP.Mix(footstepClips, surfaceWeightList);
            result = AnysoundAudioDSP.ApplyADSREnvelope(result,
                AnysoundAudioDSP.ADSREnvelopeSettings.WeightedMix(surfaceEnvelopeList, surfaceWeightList));


            result = AnysoundAudioDSP.ApplyBandpassFilter(result,
                AnysoundAudioDSP.BandPassFilterSettings.WeightedMix(surfaceBandPassList, surfaceWeightList));

            result = AnysoundAudioDSP.ApplyDistortion(result,
                AnysoundAudioDSP.DistortionSettings.WeightedMix(surfaceDistortionList, surfaceWeightList));


            result = AnysoundAudioDSP.ApplyCurveEnvelope(result,
                AnysoundAudioDSP.CurveEnvelopeSettings.WeightedMix(footstepStyleEnvelopeSettingsList, surfaceWeightList));

            return result;
        }

        static AudioSource _previewSource;

        public static void Setup()
        {
            // Check if reference exists AND is still valid
            if (!_previewSource || !_previewSource.gameObject)
            {
#if UNITY_EDITOR
                GameObject audioSourceGO = EditorUtility.CreateGameObjectWithHideFlags(
                    "AudioPreview",
                    HideFlags.HideAndDontSave);
#else
                GameObject audioSourceGO = new GameObject("AudioPreview");
                audioSourceGO.transform.position = Vector3.zero;
                // Make sure the game object persists
                Object.DontDestroyOnLoad(audioSourceGO);
#endif

                _previewSource = audioSourceGO.AddComponent<AudioSource>();

                // Ensure the AudioSource is properly configured
                if (_previewSource)
                {
                    _previewSource.playOnAwake = false;
                    _previewSource.loop = false;
                }
                else
                {
                    Debug.LogError("Failed to create audio source component");
                }
            }
        }

        public static void Release()
        {
            if (_previewSource && _previewSource.gameObject)
            {
                Object.DestroyImmediate(_previewSource.gameObject);
                _previewSource = null;
            }
        }


        static Action<float> _currentPlaybackProgress;

        public static void PlayClip(AudioClip clip, Action<float> progress)
        {
            // Thoroughly validate the audio source
            if (!_previewSource || !_previewSource.gameObject)
            {
                Debug.Log("Preview source was null or invalid, setting up a new one");
                Setup();
            }

            // Double-check after setup
            if (_previewSource && _previewSource.gameObject)
            {
                StopPreview();
                _previewSource.clip = clip;
                _previewSource.Play();
                _isPreviewing = true;
#if UNITY_EDITOR
                EditorApplication.update += CheckIfStillPlaying;
#endif
            }
            else
            {
                Debug.LogError("Failed to create or use audio preview source");
            }

            _currentPlaybackProgress = progress;
        }


        public static void StopPreview()
        {
            if (_previewSource != null)
            {
                _previewSource.Stop();
                _isPreviewing = false;
#if UNITY_EDITOR
                EditorApplication.update -= CheckIfStillPlaying;
#endif
            }
        }

        private static void CheckIfStillPlaying()
        {
            _currentPlaybackProgress?.Invoke(_previewSource.time / _previewSource.clip.length);
            if (_previewSource != null && !_previewSource.isPlaying)
            {
                _currentPlaybackProgress = null;
                _isPreviewing = false;
#if UNITY_EDITOR
                EditorApplication.update -= CheckIfStillPlaying;
#endif
            }
        }


        static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }

        public static void SaveClipToWav(AudioClip clip, string filePath)
        {
            // Get all the data from the clip
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // Create byte array for the WAV file
            byte[] wavFile = EncodeToWav(samples, clip.channels, clip.frequency);
            File.WriteAllBytes(filePath, wavFile);
        }

        static byte[] EncodeToWav(float[] samples, int channels, int sampleRate)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    int sampleCount = samples.Length;
                    int dataSize = sampleCount * 2; // 16-bit samples = 2 bytes per sample

                    // RIFF header
                    writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                    writer.Write(36 + dataSize); // File size - 8
                    writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                    // Format chunk
                    writer.Write(new char[] { 'f', 'm', 't', ' ' });
                    writer.Write(16); // Chunk size
                    writer.Write((short)1); // Audio format (1 = PCM)
                    writer.Write((short)channels);
                    writer.Write(sampleRate);
                    writer.Write(sampleRate * channels * 2); // Byte rate
                    writer.Write((short)(channels * 2)); // Block align
                    writer.Write((short)16); // Bits per sample

                    // Data chunk
                    writer.Write(new char[] { 'd', 'a', 't', 'a' });
                    writer.Write(dataSize);

                    // Convert float samples to 16-bit PCM
                    for (int i = 0; i < sampleCount; i++)
                    {
                        // Convert float to 16-bit with proper clamping
                        short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
                        writer.Write(value);
                    }
                }

                return stream.ToArray();
            }
        }
    }
}