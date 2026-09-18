using System.Collections.Generic;
using Anysound.Shared.Footsteps;
using Anysound.Shared.Generators.Footsteps;
using UnityEditor;
using UnityEngine;

namespace Anysound
{
    public class AnysoundFootstepEditorBackendWindow : EditorWindow
    {
        //private StepStyleObject stepStyleObject;

        private AnysoundFootstepObject _anysoundFootstepObject;

        private bool normalizeOutput = true;
        private string outputFileName = "MorphedSound";

        // Preview and playback
        private AudioClip previewClip;


        // Scroll view
        private Vector2 scrollPosition;

        // Settings
        private int sampleRate = 44100;

        private float _currentSurfaceType;
        private float _currentSize;
        private float _currentMovementSpeed;


        [MenuItem("Window/Audio/Anysound footstep editor")]
        public static void ShowWindow()
        {
            GetWindow<AnysoundFootstepEditorBackendWindow>("Anysound footstep editor");
        }

        private void OnEnable()
        {
            // Create audio source for preview if needed
            AnysoundFootstepDSP.Setup();
        }

        private void OnDisable()
        {
            // Clean up when window is closed
            AnysoundFootstepDSP.Release();
        }

        private void HandleKeyboardEvents()
        {
            Event currentEvent = Event.current;

            if (focusedWindow != this) return;
            if (currentEvent.type != EventType.KeyDown) return;
            if (currentEvent.keyCode != KeyCode.Space) return;
            if (!_anysoundFootstepObject) return;
            if (AnysoundFootstepDSP.IsPreviewing)
                AnysoundFootstepDSP.StopPreview();

            GenerateAndPlayPreview();
            currentEvent.Use();
            Repaint();
        }

        private void OnGUI()
        {
            // Handle keyboard events
            HandleKeyboardEvents();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("Footstep editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _anysoundFootstepObject = (AnysoundFootstepObject)EditorGUILayout.ObjectField("Footstep object",
                _anysoundFootstepObject, typeof(AnysoundFootstepObject), false);


            EditorGUILayout.Space();

            if (_anysoundFootstepObject)
            {
                DrawSurfaceSettings();
                EditorGUILayout.Space();

                DrawSoundSelector();
                EditorGUILayout.Space();
                DrawSettings();
                EditorGUILayout.Space();

                DrawPreviewControls();
                EditorGUILayout.Space();

                DrawExportControls();
            }
            else
            {
                EditorGUILayout.HelpBox("Add footstep object.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawSurfaceSettings()
        {
            SerializedObject serializedObject = new SerializedObject(_anysoundFootstepObject);
            SerializedProperty surfaceSettingsProperty = serializedObject.FindProperty("surfaceSettings");
            EditorGUILayout.PropertyField(surfaceSettingsProperty);

            serializedObject.ApplyModifiedProperties();
        }

        private string[] walkStyleName = new[] { "Sneak", "Walk", "Run" };


        private void DrawSettings()
        {
            int currentSurfaceIndex = Mathf.RoundToInt(_currentSurfaceType);
            int currentSizeIndex = Mathf.RoundToInt(_currentSize);
            int currentMovementSpeedIndex = Mathf.RoundToInt(_currentMovementSpeed);
            SerializedObject serializedObject = new SerializedObject(_anysoundFootstepObject);
            SerializedProperty surfaceSettingsProperty = serializedObject.FindProperty("surfaceSettings");
            SerializedProperty currentSizeProperty = null;
            SerializedProperty currentWalkStyleProperty = null;


            var currentSurfaceProperty = surfaceSettingsProperty.GetArrayElementAtIndex(currentSurfaceIndex);


            if (currentSurfaceProperty != null)
            {
                currentSizeProperty = currentSurfaceProperty.FindPropertyRelative("footstepSizeSettings").GetArrayElementAtIndex(currentSizeIndex);
                currentWalkStyleProperty = currentSurfaceProperty.FindPropertyRelative("footstepStyleEnvelopeCurves")
                    .GetArrayElementAtIndex(currentMovementSpeedIndex);
            }

            if (currentSurfaceProperty == null || currentSizeProperty == null)
            {
                EditorGUILayout.HelpBox("Failed to find preset property", MessageType.Warning);
                return;
            }

            string editingString = "Surface: " + currentSurfaceProperty.FindPropertyRelative("surfaceTypeName").stringValue +
                                   "  --  Size: " + currentSizeProperty.FindPropertyRelative("name").stringValue +
                                   " --  Movement speed: " + _currentMovementSpeed;


            serializedObject.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(editingString, EditorStyles.boldLabel);
            EditorGUILayout.Space();
            // Iterate through all properties of the preset
            SerializedProperty iterator = currentSizeProperty.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();


            // Skip the first property (which is the preset itself)
            iterator.NextVisible(true);

            while (iterator.NextVisible(false))
            {
                // Exit if we've gone past the end of the preset properties
                if (SerializedProperty.EqualContents(iterator, endProperty))
                    break;

                EditorGUILayout.PropertyField(iterator, true);
            }

            EditorGUILayout.EndVertical();

            // Apply changes
            serializedObject.ApplyModifiedProperties();


            if (currentWalkStyleProperty != null)
            {
                GUILayout.Label("Movement speed envelope");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(currentWalkStyleProperty);
                EditorGUILayout.EndVertical();
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                EditorGUILayout.HelpBox("Failed to find movement speed property", MessageType.Warning);
            }
        }

        private void DrawSoundSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Sound selector", EditorStyles.boldLabel);
            if (_anysoundFootstepObject)
            {
                _currentSurfaceType =
                    EditorGUILayout.Slider("Surface type", _currentSurfaceType, 0f, _anysoundFootstepObject.surfaceSettings.Count - 1);

                _currentSize = EditorGUILayout.Slider("Footstep size", _currentSize, 0f,
                    _anysoundFootstepObject.surfaceSettings[Mathf.RoundToInt(_currentSurfaceType)].FootstepSizeSettings.Count - 1);

                _currentMovementSpeed = EditorGUILayout.Slider("Movement speed", _currentMovementSpeed, 0f,
                    _anysoundFootstepObject.surfaceSettings[Mathf.RoundToInt(_currentSurfaceType)].footstepStyleEnvelopeCurves.Count - 1);

                EditorGUILayout.Space();
            }

            EditorGUILayout.EndVertical();
        }


        private void DrawPreviewControls()
        {
            GUILayout.Label("Preview", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(AnysoundFootstepDSP.IsPreviewing ? "Stop Preview" : "Generate Preview", GUILayout.Height(30)))
            {
                if (AnysoundFootstepDSP.IsPreviewing)
                {
                    AnysoundFootstepDSP.StopPreview();
                }
                else
                {
                    GenerateAndPlayPreview();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Press Space to toggle preview playback", MessageType.Info);
        }

        private void DrawExportControls()
        {
            GUILayout.Label("Export", EditorStyles.boldLabel);
            if (GUILayout.Button("Create preset", GUILayout.Height(30)))
            {
                _anysoundFootstepObject.CreatePreset(GetPresetValues());
            }

            return;
            normalizeOutput = EditorGUILayout.Toggle("Normalize Output", normalizeOutput);


            // Sample rate selection
            string[] sampleRateOptions = new string[] { "22050 Hz", "44100 Hz", "48000 Hz", "96000 Hz" };
            int[] sampleRates = new int[] { 22050, 44100, 48000, 96000 };

            int selectedSampleRateIndex = 1; // Default to 44100
            for (int i = 0; i < sampleRates.Length; i++)
            {
                if (sampleRates[i] == sampleRate)
                {
                    selectedSampleRateIndex = i;
                    break;
                }
            }

            selectedSampleRateIndex = EditorGUILayout.Popup("Sample Rate", selectedSampleRateIndex, sampleRateOptions);
            sampleRate = sampleRates[selectedSampleRateIndex];

            outputFileName = EditorGUILayout.TextField("Output Filename", outputFileName);

            if (GUILayout.Button("Generate and Save Morphed Audio", GUILayout.Height(30)))
            {
                AnysoundFootstepDSP.CreateMorphedAudioClip(_anysoundFootstepObject, _currentSize, _currentMovementSpeed, _currentSurfaceType);
                //SaveMultipleVariants(5, 0.15f);
            }
        }


        private void GenerateAndPlayPreview()
        {
            if (!_anysoundFootstepObject)
            {
                EditorUtility.DisplayDialog("Error", "Please add a footstep object.", "OK");
                return;
            }

            previewClip = AnysoundFootstepDSP.CreateMorphedAudioClip(_anysoundFootstepObject, _currentSize, _currentMovementSpeed, _currentSurfaceType);
            if (previewClip != null)
            {
                AnysoundFootstepDSP.PlayClip(previewClip, f => { });
            }
        }

        Dictionary<string, float> GetPresetValues()
        {
            Dictionary<string, float> presetValues = new Dictionary<string, float>()
            {
                { "MovementSpeed", _currentMovementSpeed },
                { "Size", _currentSize },
                { "SurfaceType", _currentSurfaceType },
            };
            return presetValues;
        }
    }
}