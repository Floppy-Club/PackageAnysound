using System;
using System.Collections.Generic;
using Anysound.Shared.Browser;
using Anysound.Shared.Generators.Footsteps;
using UnityEditor;
using UnityEngine;

namespace Anysound.Shared.Generators.Retro.Backend
{
    /// <summary>
    /// Edits the ranges of every 8-bit category. The frontend's random button picks a sound within them
    /// </summary>
    public class AnysoundRetroEditorBackendWindow : EditorWindow
    {
        const string DefaultObjectPath = "Packages/com.floppyclub.anysound/Editor/Shared/Generators/Retro/AnysoundData/Retro.asset";
        const string DefaultPresetFolder = "Packages/com.floppyclub.anysound/Editor/Presets/Retro";

        const float FieldWidth = 50f;

        private AnysoundRetroObject _anysoundRetroObject;
        private SerializedObject _serializedObject;
        // Set in OnEnable: the default draws a random seed, which Unity doesn't allow in a window's field initializer
        private AnysoundRetroParameters _parameters;
        private Vector2 _scrollPosition;

        [MenuItem("Window/Audio/Anysound 8-bit editor")]
        public static void ShowWindow()
        {
            // GetWindow reuses an existing instance, also one left behind by an open that failed, which never shows.
            // So remove any existing instance and open a fresh window
            foreach (var window in Resources.FindObjectsOfTypeAll<AnysoundRetroEditorBackendWindow>())
                DestroyImmediate(window);

            var newWindow = CreateWindow<AnysoundRetroEditorBackendWindow>("Anysound 8-bit editor");
            newWindow.Show();
            newWindow.Focus();
        }

        private void OnEnable()
        {
            AnysoundFootstepDSP.Setup();
            _parameters = AnysoundRetroParameters.Default;
            if (!_anysoundRetroObject)
                _anysoundRetroObject = AssetDatabase.LoadAssetAtPath<AnysoundRetroObject>(DefaultObjectPath);
        }

        private void OnDisable()
        {
            AnysoundFootstepDSP.Release();
        }

        private void HandleKeyboardEvents()
        {
            Event currentEvent = Event.current;

            if (focusedWindow != this) return;
            if (currentEvent.type != EventType.KeyDown) return;
            if (currentEvent.keyCode != KeyCode.Space) return;
            if (!_anysoundRetroObject) return;

            GenerateAndPlayPreview();
            currentEvent.Use();
            Repaint();
        }

        private void OnGUI()
        {
            HandleKeyboardEvents();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Label("8-bit editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _anysoundRetroObject = (AnysoundRetroObject)EditorGUILayout.ObjectField("8-bit object",
                _anysoundRetroObject, typeof(AnysoundRetroObject), false);

            if (!_anysoundRetroObject)
            {
                EditorGUILayout.HelpBox("Please assign an 8-bit object.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (_serializedObject == null || _serializedObject.targetObject != _anysoundRetroObject)
                _serializedObject = new SerializedObject(_anysoundRetroObject);

            EditorGUILayout.Space();
            _parameters.category = (RetroCategory)EditorGUILayout.EnumPopup("Category", _parameters.category);
            _parameters.duration = EditorGUILayout.Slider("Duration", _parameters.duration,
                AnysoundRetroParameters.DurationMin, AnysoundRetroParameters.DurationMax);
            _parameters.hardness = EditorGUILayout.Slider("Hardness (sine/FM - square/saw)", _parameters.hardness, 0f, 1f);

            // Make sure the category exists before its properties are looked up
            var categorySettings = _anysoundRetroObject.GetCategory(_parameters.category);
            _parameters.character = EditorGUILayout.Slider($"Character ({categorySettings.characterName})", _parameters.character, 0f, 1f);

            using (new EditorGUILayout.HorizontalScope())
            {
                _parameters.seed = EditorGUILayout.IntField(new GUIContent("Seed", "0 = the middle of every range"), _parameters.seed);
                if (GUILayout.Button("New seed", GUILayout.Width(80)))
                {
                    _parameters.seed = AnysoundRetroParameters.NewSeed();
                    GenerateAndPlayPreview();
                }
            }

            DrawCategorySettings();

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate & Play (space)", GUILayout.Height(30)))
                GenerateAndPlayPreview();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create preset", GUILayout.Height(30)))
                    _anysoundRetroObject.CreatePreset(_parameters.ToPresetValues());

                if (GUILayout.Button("Reset category to defaults", GUILayout.Height(30)) &&
                    EditorUtility.DisplayDialog("Reset category", $"Reset all ranges of {_parameters.category} to the defaults?", "Reset", "Cancel"))
                {
                    Undo.RecordObject(_anysoundRetroObject, "Reset 8-bit category");
                    _anysoundRetroObject.ResetCategory(_parameters.category);
                    _serializedObject.Update();
                }
            }

            if (GUILayout.Button("Create default presets (one per category)", GUILayout.Height(30)))
                CreateDefaultPresets();

            EditorGUILayout.EndScrollView();
        }

        void DrawCategorySettings()
        {
            _serializedObject.Update();
            var category = _serializedObject.FindProperty("categories").GetArrayElementAtIndex((int)_parameters.category);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{_parameters.category} settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(category.FindPropertyRelative("waveforms"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ranges", EditorStyles.boldLabel);
            var ranges = category.FindPropertyRelative("ranges");
            for (int i = 0; i < ranges.arraySize; i++)
                DrawRange((RetroParameter)i, ranges.GetArrayElementAtIndex(i));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(category.FindPropertyRelative("characterName"));
            EditorGUILayout.PropertyField(category.FindPropertyRelative("characterLowLabel"));
            EditorGUILayout.PropertyField(category.FindPropertyRelative("characterHighLabel"));
            EditorGUILayout.PropertyField(category.FindPropertyRelative("characterModulations"), true);

            _serializedObject.ApplyModifiedProperties();
        }

        // [min] [----slider----] [max]
        static void DrawRange(RetroParameter parameter, SerializedProperty range)
        {
            var minProperty = range.FindPropertyRelative("min");
            var maxProperty = range.FindPropertyRelative("max");
            var limits = RetroCategorySettings.Limits(parameter);
            float min = minProperty.floatValue, max = maxProperty.floatValue;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(ObjectNames.NicifyVariableName(parameter.ToString()));
                min = EditorGUILayout.FloatField(min, GUILayout.Width(FieldWidth));
                EditorGUILayout.MinMaxSlider(ref min, ref max, limits.x, limits.y);
                max = EditorGUILayout.FloatField(max, GUILayout.Width(FieldWidth));
            }

            min = Mathf.Clamp(min, limits.x, limits.y);
            max = Mathf.Clamp(max, min, limits.y);
            minProperty.floatValue = min;
            maxProperty.floatValue = max;
        }

        private void GenerateAndPlayPreview()
        {
            if (!_anysoundRetroObject)
            {
                EditorUtility.DisplayDialog("Error", "Please add an 8-bit object.", "OK");
                return;
            }

            if (AnysoundFootstepDSP.IsPreviewing)
                AnysoundFootstepDSP.StopPreview();

            var clip = AnysoundRetroDSP.CreateAudioClip(_anysoundRetroObject, _parameters);
            if (clip)
                AnysoundFootstepDSP.PlayClip(clip, f => { });
        }

        // The tone of each default preset (0 = sine/FM, 1 = square/sawtooth), so together they show the range of the slider
        static readonly Dictionary<RetroCategory, float> DefaultPresetTones = new()
        {
            { RetroCategory.Pickup, 0.75f },
            { RetroCategory.Laser, 1f },
            { RetroCategory.Explosion, 1f },
            { RetroCategory.Powerup, 0.6f },
            { RetroCategory.Hit, 1f },
            { RetroCategory.Jump, 0.7f },
            { RetroCategory.Click, 0.5f },
            { RetroCategory.Select, 0.3f },
        };

        // One preset per category with the middle of its ranges, so the generator shows up in the browser
        void CreateDefaultPresets()
        {
            if (!EditorUtility.DisplayDialog("Create default presets",
                    $"Replace the 8-bit presets in {DefaultPresetFolder} with one preset per category?", "Create", "Cancel"))
                return;

            // The names depend on the values, so the old presets are removed instead of overwritten
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(AnysoundPresetObject)}", new[] { DefaultPresetFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<AnysoundPresetObject>(path);
                if (preset && preset.generatorObject == _anysoundRetroObject)
                    AssetDatabase.DeleteAsset(path);
            }

            foreach (RetroCategory category in Enum.GetValues(typeof(RetroCategory)))
            {
                var parameters = AnysoundRetroParameters.Default;
                parameters.category = category;
                parameters.seed = 0;
                parameters.hardness = DefaultPresetTones.TryGetValue(category, out float tone) ? tone : AnysoundRetroParameters.HardnessDefault;

                var presetValues = parameters.ToPresetValues();
                _anysoundRetroObject.DescribePreset(presetValues, out string presetName, out List<string> tags);
                var clip = AnysoundRetroDSP.CreateAudioClip(_anysoundRetroObject, parameters);
                AnysoundBrowser.SavePresetAt($"{DefaultPresetFolder}/{presetName}.asset", presetName, presetValues, tags,
                    _anysoundRetroObject, clip);
            }
        }
    }
}
