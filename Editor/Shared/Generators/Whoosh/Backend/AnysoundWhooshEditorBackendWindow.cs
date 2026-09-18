using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Anysound.Shared.Generators.Whoosh.Backend
{
    public class AnysoundWhooshEditorBackendWindow : EditorWindow
    {
        private AnysoundWhooshObject _anysoundWhooshObject;


        // Preview and playback
        private AudioClip previewClip;

        // Scroll view
        private Vector2 scrollPosition;


        private float _movementValue;
        private float _durationValue;

        private float _sizeValue;
        float _fluctuationValue;
        private float _squareWaveValue;

        // Store expanded states using property paths as keys
        private Dictionary<string, bool> _expandedStates = new Dictionary<string, bool>();
        private SerializedObject _serializedObject;

        [MenuItem("Window/Audio/Anysound whoosh editor")]
        public static void ShowWindow()
        {
            GetWindow<AnysoundWhooshEditorBackendWindow>("Anysound whoosh editor");
        }

        private void OnEnable()
        {
            AnysoundWhoosh.Setup();
            _expandedStates = new Dictionary<string, bool>();
        }

        private void OnDisable()
        {
            AnysoundWhoosh.Release();
        }

        private void HandleKeyboardEvents()
        {
            Event currentEvent = Event.current;

            if (focusedWindow != this) return;
            if (currentEvent.type != EventType.KeyDown) return;
            if (currentEvent.keyCode != KeyCode.Space) return;
            if (!_anysoundWhooshObject) return;
            if (AnysoundWhoosh.IsPreviewing)
                AnysoundWhoosh.StopPreview();

            GenerateAndPlayPreview();
            currentEvent.Use();
            Repaint();
        }

        private void OnGUI()
        {
            // Handle keyboard events
            HandleKeyboardEvents();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("Explosion editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            _anysoundWhooshObject = (AnysoundWhooshObject)EditorGUILayout.ObjectField("Whoosh object",
                _anysoundWhooshObject, typeof(AnysoundWhooshObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                // Object changed, clear the expanded states
                _expandedStates.Clear();

                // Initialize the serialized object
                if (_anysoundWhooshObject != null)
                {
                    _serializedObject = new SerializedObject(_anysoundWhooshObject);
                }
            }

            EditorGUILayout.Space();

            if (_anysoundWhooshObject)
            {
                // Update the serialized object
                if (_serializedObject == null || _serializedObject.targetObject != _anysoundWhooshObject)
                {
                    _serializedObject = new SerializedObject(_anysoundWhooshObject);
                }

                _serializedObject.Update();

                // Slider for _slider1 value
                _movementValue = EditorGUILayout.Slider("Movement", _movementValue, 0, _anysoundWhooshObject.MovementSettings.Length - 1);
                _durationValue = EditorGUILayout.Slider("Duration", _durationValue, 0.05f, 0.5f);
                _fluctuationValue = EditorGUILayout.Slider("Fluctuation", _fluctuationValue, 0f, 1);
                _sizeValue = EditorGUILayout.Slider("Size", _sizeValue, 0.5f, 2);
                DrawCurrentWhooshSettings();
            }
            else
            {
                EditorGUILayout.HelpBox("Please assign a Whoosh object to visualize the curve.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }


        void DrawCurrentWhooshSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Whoosh Settings", EditorStyles.boldLabel);

            if (_serializedObject == null) return;

            // Find the movementSettings property
            SerializedProperty movementSettingsArray = _serializedObject.FindProperty("movementSettings");

            if (movementSettingsArray != null && movementSettingsArray.isArray && (int)(_movementValue) < movementSettingsArray.arraySize)
            {
                // Get the serialized property for the current movement setting
                SerializedProperty currentSettingsProp = movementSettingsArray.GetArrayElementAtIndex(Mathf.RoundToInt(_movementValue));

                // Draw the properties with preserved foldout states
                DrawSerializedPropertyWithPreservedFoldouts(currentSettingsProp);

                // Apply the modified properties
                if (_serializedObject.hasModifiedProperties)
                {
                    _serializedObject.ApplyModifiedProperties();
                }

                // Generate & Play Button
                EditorGUILayout.Space();
                if (GUILayout.Button("Generate & Play Whoosh Sound", GUILayout.Height(30)))
                {
                    GenerateAndPlayPreview();
                }

                if (GUILayout.Button("Create preset", GUILayout.Height(30)))
                {
                    CreatePreset();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Cannot display settings. Make sure the whoosh object has valid movement settings.", MessageType.Warning);
            }
        }

        private void DrawSerializedPropertyWithPreservedFoldouts(SerializedProperty property)
        {
            if (property == null) return;

            // We're going to use EditorGUILayout.PropertyField directly but manage the foldout state manually
            SerializedProperty copiedProperty = property.Copy();
            SerializedProperty endProperty = copiedProperty.GetEndProperty();

            // Enter first child property
            bool enterChildren = true;
            while (copiedProperty.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copiedProperty, endProperty))
            {
                // After the first iteration, we only want to get siblings, not children
                enterChildren = false;

                // Store the original isExpanded state
                bool wasExpanded = copiedProperty.isExpanded;

                // If we have a stored state for this property, use it
                string propertyPath = copiedProperty.propertyPath;
                if (_expandedStates.TryGetValue(propertyPath, out bool storedExpanded))
                {
                    copiedProperty.isExpanded = storedExpanded;
                }

                // Use the standard PropertyField which will utilize any custom property drawers
                EditorGUILayout.PropertyField(copiedProperty, true);

                // If the isExpanded state changed, update our stored state
                if (copiedProperty.isExpanded != wasExpanded)
                {
                    _expandedStates[propertyPath] = copiedProperty.isExpanded;
                }
            }
        }

        private void GenerateAndPlayPreview()
        {
            if (!_anysoundWhooshObject)
            {
                EditorUtility.DisplayDialog("Error", "Please add a whoosh object.", "OK");
                return;
            }

            previewClip = AnysoundWhoosh.CreateMorphedAudioClip(_anysoundWhooshObject, _durationValue, _movementValue, _sizeValue, _fluctuationValue);
            if (previewClip)
            {
                AnysoundWhoosh.PlayClip(previewClip, f => { });
            }
        }

        void CreatePreset()
        {
            _anysoundWhooshObject.CreatePreset(GetPresetValue());
        }

        Dictionary<string, float> GetPresetValue()
        {
            Dictionary<string, float> presetValues = new Dictionary<string, float>()
            {
                { "Duration", _durationValue },
                { "Movement", _movementValue },
                { "Size", _sizeValue },
                { "Fluctuation", _fluctuationValue },
            };
            return presetValues;
        }
    }
}