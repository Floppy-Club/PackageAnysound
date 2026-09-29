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
            _parameters.extraMaterial = EditorGUILayout.Popup("Extra material", _parameters.extraMaterial, WithNone(materialNames));
            _parameters.action = EditorGUILayout.Popup("Action", _parameters.action, actionNames);
            _parameters.extraSample = EditorGUILayout.Popup("A little extra", _parameters.extraSample, WithNone(extraNames));
            _parameters.size = EditorGUILayout.Slider("Size", _parameters.size, 0f, 1f);

            EditorGUILayout.EndVertical();
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

            SerializedProperty iterator = element.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (SerializedProperty.EqualContents(iterator, endProperty))
                    break;

                EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }

            if (listName == "actions")
            {
                EditorGUILayout.HelpBox(
                    $"Each step triggers the material (and extras) after 'delay' seconds from the previous step, pitched in semitones. Max {AnysoundUIObject.MaxActionSteps} steps.",
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

        private void GenerateAndPlayPreview()
        {
            if (!_anysoundUIObject)
            {
                EditorUtility.DisplayDialog("Error", "Please add a UI object.", "OK");
                return;
            }

            _previewClip = AnysoundUIDSP.CreateAudioClip(_anysoundUIObject, _parameters);
            if (_previewClip != null)
            {
                AnysoundFootstepDSP.PlayClip(_previewClip, f => { });
            }
        }
    }
}
