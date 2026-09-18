using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Anysound.Shared.Generators.Whoosh
{
    public static class AnysoundWhoosh
    {
        private static bool _isPreviewing;
        public static bool IsPreviewing => _isPreviewing;

        public static AudioClip CreateMorphedAudioClip(AnysoundWhooshObject anysoundWhooshObject, float duration, float movement, float size,
            float fluctuation)
        {
            return AnysoundWhooshDSP.CreateMorphedAudioClip(anysoundWhooshObject, duration, movement, size, fluctuation);
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
                if (_previewSource != null)
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
                Debug.Log("Playing audio clip: " + (clip ? clip.name : "null"));
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
    }
}