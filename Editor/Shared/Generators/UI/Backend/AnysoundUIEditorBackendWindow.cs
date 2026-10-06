using Anysound.Shared.Generators.Footsteps;
using UnityEditor;
using UnityEngine;

namespace Anysound.Shared.Generators.UI.Backend
{
    public class AnysoundUIEditorBackendWindow : EditorWindow
    {
        private AnysoundUIObject _anysoundUIObject;
        private SerializedObject _serializedObject;
        private AnysoundUIParameters _parameters = AnysoundUIParameters.Default;
        private AudioClip _previewClip;
        private Vector2 _scrollPosition;
        private bool _showAllContent;

        // Each preview picks new step values and random clips, like pressing the random buttons in the frontend
        private bool _randomizeOnPreview = true;

        [MenuItem("Window/Audio/Anysound UI editor")]
        public static void ShowWindow()
        {
            GetWindow<AnysoundUIEditorBackendWindow>("Anysound UI editor");
        }

        private void OnEnable()
        {
            AnysoundFootstepDSP.Setup();
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
            if (!_anysoundUIObject) return;
            if (AnysoundFootstepDSP.IsPreviewing)
                AnysoundFootstepDSP.StopPreview();

            GenerateAndPlayPreview();
            currentEvent.Use();
            Repaint();
        }

        private void OnGUI()
        {
            HandleKeyboardEvents();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Label("UI editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _anysoundUIObject = (AnysoundUIObject)EditorGUILayout.ObjectField("UI object", _anysoundUIObject, typeof(AnysoundUIObject), false);

            EditorGUILayout.Space();

            if (_anysoundUIObject)
            {
                if (_serializedObject == null || _serializedObject.targetObject != _anysoundUIObject)
                    _serializedObject = new SerializedObject(_anysoundUIObject);

                _serializedObject.Update();

                DrawSoundSelector();
                EditorGUILayout.Space();

                DrawSelectedSettings();
                EditorGUILayout.Space();

                DrawGlobalSettings();
                EditorGUILayout.Space();

                DrawAllContent();
                EditorGUILayout.Space();

                _serializedObject.ApplyModifiedProperties();

                DrawPreviewControls();
                EditorGUILayout.Space();

                DrawExportControls();
            }
            else
            {
                EditorGUILayout.HelpBox("Add UI object (Create > Anysound > AnysoundUIObject).", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        // The same controls the user gets in the frontend
        private void DrawSoundSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Sound selector", EditorStyles.boldLabel);

            string[] materialNames = _anysoundUIObject.MaterialNames;
            string[] actionNames = _anysoundUIObject.ActionNames;
            string[] extraNames = _anysoundUIObject.ExtraNames;

            _parameters.material = EditorGUILayout.Popup("Material", _parameters.material, materialNames);
            _parameters.materialClip = ClipPopup(_anysoundUIObject.GetMaterialCollection(_parameters.material), _parameters.materialClip);
            _parameters.extraMaterial = EditorGUILayout.Popup("Extra material", _parameters.extraMaterial, WithNone(materialNames));
            if (_parameters.HasExtraMaterial)
                _parameters.extraMaterialClip = ClipPopup(_anysoundUIObject.GetMaterialCollection(_parameters.ExtraMaterialIndex),
                    _parameters.extraMaterialClip);
            _parameters.action = EditorGUILayout.Popup("Action", _parameters.action, actionNames);
            _parameters.extraSample = EditorGUILayout.Popup("A little extra", _parameters.extraSample, WithNone(extraNames));
            if (_parameters.HasExtraSample)
                _parameters.extraSampleClip = ClipPopup(_anysoundUIObject.GetExtraCollection(_parameters.ExtraSampleIndex, _parameters.action),
                    _parameters.extraSampleClip);
            _parameters.size = EditorGUILayout.Slider("Size", _parameters.size, 0f, 1f);

            // Same as the random buttons in the frontend: picks new step values within the min/max ranges and random clips
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Action values", _parameters.actionSeed == 0 ? "Middle of ranges" : $"Seed {_parameters.actionSeed}");
            if (GUILayout.Button("Randomize", GUILayout.Width(80)))
            {
                RandomizePreviewValues();
                GenerateAndPlayPreview(randomize: false);
            }

            if (GUILayout.Button("Middle", GUILayout.Width(60)))
                _parameters.actionSeed = 0;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // The exact sample within a collection, named like the frontend's clip dropdowns
        static int ClipPopup(AnysoundSoundCollectionObject collection, int clipIndex)
        {
            var items = AnysoundUIObject.GetClipDropdownItems(collection);
            if (items.Count == 0)
            {
                EditorGUILayout.LabelField("    Clip", "No samples");
                return 0;
            }

            var names = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
                names[i] = items[i].name;
            return EditorGUILayout.Popup("    Clip", Mathf.Clamp(clipIndex, 0, items.Count - 1), names);
        }

        // New step values and a random clip for each chosen material / extra
        private void RandomizePreviewValues()
        {
            _parameters.actionSeed = AnysoundUIParameters.NewActionSeed();
            _parameters.materialClip = RandomClip(_anysoundUIObject.GetMaterialCollection(_parameters.material));
            if (_parameters.HasExtraMaterial)
                _parameters.extraMaterialClip = RandomClip(_anysoundUIObject.GetMaterialCollection(_parameters.ExtraMaterialIndex));
            if (_parameters.HasExtraSample)
                _parameters.extraSampleClip = RandomClip(_anysoundUIObject.GetExtraCollection(_parameters.ExtraSampleIndex, _parameters.action));
        }

        static int RandomClip(AnysoundSoundCollectionObject collection)
        {
            int count = collection ? collection.Count : 0;
            return count > 0 ? Random.Range(0, count) : 0;
        }

        static string[] WithNone(string[] names)
        {
            var result = new string[names.Length + 1];
            result[0] = "None";
            names.CopyTo(result, 1);
            return result;
        }

        // Shows the settings for the currently selected material, action and extra, so they can be tweaked while previewing
        private void DrawSelectedSettings()
        {
            DrawSelectedElement("materials", _parameters.material, "Material");
            if (_parameters.HasExtraMaterial)
                DrawSelectedElement("materials", _parameters.ExtraMaterialIndex, "Extra material");
            DrawSelectedElement("actions", _parameters.action, "Action");
            if (_parameters.HasExtraSample)
                DrawSelectedElement("extras", _parameters.ExtraSampleIndex, "A little extra");
        }

        private void DrawSelectedElement(string listName, int index, string title)
        {
            SerializedProperty list = _serializedObject.FindProperty(listName);
            if (list == null || index < 0 || index >= list.arraySize)
            {
                EditorGUILayout.HelpBox($"No {title.ToLower()} selected. Add one under 'All content'.", MessageType.Warning);
                return;
            }

            SerializedProperty element = list.GetArrayElementAtIndex(index);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"{title}: {element.FindPropertyRelative("name").stringValue}", EditorStyles.boldLabel);

            // The scale only matters when the action's pitch is set to notes
            SerializedProperty pitchMode = element.FindPropertyRelative("pitchMode");
            bool hideScale = pitchMode != null && pitchMode.enumValueIndex != (int)AnysoundUIObject.UIPitchMode.Notes;

            SerializedProperty iterator = element.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (SerializedProperty.EqualContents(iterator, endProperty))
                    break;
                if (hideScale && iterator.name == "scale")
                    continue;

                EditorGUILayout.PropertyField(iterator, true);
            }

            if (listName == "actions")
            {
                EditorGUILayout.HelpBox(
                    $"Each step triggers the material (and extras) after 'delay' seconds from the previous step, pitched in semitones. " +
                    $"'Duration' cuts the step after that many seconds (0 = the whole sample). Max {AnysoundUIObject.MaxActionSteps} steps. " +
                    "Every value is a min/max range: the random action button picks a value within it, set min = max for a fixed value. " +
                    "Pitch 'Notes' snaps the pitch ranges to whole semitones and picks only notes of the chosen scale (0 = root).",
                    MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGlobalSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Size and mix", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Size lerps from the narrow filter and short envelope (0) to the wide filter and full envelope (1). " +
                                    "The envelope is applied to each step of the action.", MessageType.None);

            EditorGUILayout.PropertyField(_serializedObject.FindProperty("narrowFilter"), true);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("wideFilter"), true);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("shortEnvelope"), true);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("fullEnvelope"), true);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("extraMaterialVolume"));
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("normalizeOutput"));
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("normalizePeak"));
            EditorGUILayout.EndVertical();
        }

        private void DrawAllContent()
        {
            _showAllContent = EditorGUILayout.Foldout(_showAllContent, "All content (add / remove materials, actions and extras)", true);
            if (!_showAllContent) return;

            EditorGUILayout.PropertyField(_serializedObject.FindProperty("materials"), true);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("actions"), true);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("extras"), true);

            if (GUILayout.Button("Reset actions to defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset actions", "Replace all actions with the default action presets?", "Reset", "Cancel"))
                {
                    Undo.RecordObject(_anysoundUIObject, "Reset UI actions");
                    _anysoundUIObject.actions = AnysoundUIObject.CreateDefaultActions();
                    EditorUtility.SetDirty(_anysoundUIObject);
                    _serializedObject.Update();
                }
            }
        }

        private void DrawPreviewControls()
        {
            GUILayout.Label("Preview", EditorStyles.boldLabel);

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

            _randomizeOnPreview = EditorGUILayout.ToggleLeft(
                new GUIContent("Randomize on preview", "Every preview picks new step values within the ranges and random clips"),
                _randomizeOnPreview);

            if (_previewClip)
            {
                EditorGUILayout.LabelField("Length", $"{_previewClip.length * 1000f:0} ms");
            }

            EditorGUILayout.HelpBox("Press Space to toggle preview playback", MessageType.Info);
        }

        private void DrawExportControls()
        {
            GUILayout.Label("Export", EditorStyles.boldLabel);
            if (GUILayout.Button("Create preset", GUILayout.Height(30)))
            {
                _anysoundUIObject.CreatePreset(_parameters.ToPresetValues());
            }
        }

        private void GenerateAndPlayPreview() => GenerateAndPlayPreview(_randomizeOnPreview);

        private void GenerateAndPlayPreview(bool randomize)
        {
            if (!_anysoundUIObject)
            {
                EditorUtility.DisplayDialog("Error", "Please add a UI object.", "OK");
                return;
            }

            if (randomize)
                RandomizePreviewValues();

            _previewClip = AnysoundUIDSP.CreateAudioClip(_anysoundUIObject, _parameters);
            if (_previewClip != null)
            {
                AnysoundFootstepDSP.PlayClip(_previewClip, f => { });
            }
        }
    }
}
