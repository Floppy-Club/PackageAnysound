using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Frontend;
using Anysound.Shared.Generators.Footsteps;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Generators.UI.Frontend
{
#if UNITY_EDITOR
    public class AnysoundUIEditorWindow : AnysoundGeneratorWindowBase
    {
        const string NoneChoice = "None";

        private AnysoundUIObject _anysoundUIObject;
        private AnysoundUIParameters _parameters = AnysoundUIParameters.Default;

        private VisualElement _waveformContainer, _playheadContainer;
        private AnysoundDropdown _materialDropdown, _extraMaterialDropdown;
        private DropdownField _actionDropdown, _extraDropdown;
        private AnysoundSlider _sizeSlider;
        private bool _isInit;

        VisualElement _rootVisualElement;

        public AnysoundUIEditorWindow(VisualElement rootElement, AnysoundUIObject uiObject, AnysoundPresetObject preset)
        {
            _rootVisualElement = rootElement;
            _anysoundUIObject = uiObject;
            SetPresetObject(preset);
            SetupObjectEditing();
        }

        private void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.floppyclub.anysound/Editor/Shared/Generators/UI/Frontend/uxml/AnysoundUIInspector.uxml");

            if (visualTree == null)
            {
                Debug.LogError("Could not find AnysoundUIInspector.uxml");
                return;
            }

            visualTree.CloneTree(_rootVisualElement);
            if (_anysoundUIObject != null)
            {
                SetupObjectEditing();
            }

            RegisterKeyDownHandler(_rootVisualElement, OnKeyDownEvent);
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Space)
            {
                if (!_anysoundUIObject) return;

                if (AnysoundFootstepDSP.IsPreviewing)
                    AnysoundFootstepDSP.StopPreview();

                GenerateAndPlayPreview();

                evt.StopPropagation();
                _rootVisualElement.focusController?.IgnoreEvent(evt);
            }
        }

        void GenerateAndPlayPreview()
        {
            var clip = AnysoundUIDSP.CreateAudioClip(_anysoundUIObject, _parameters);
            if (!clip) return;

            AnysoundFootstepsHelper.UpdateWaveform(_waveformContainer, clip);
            AnysoundFootstepDSP.PlayClip(clip, f =>
            {
                _playheadContainer.style.visibility = new StyleEnum<Visibility>(Visibility.Visible);

                float leftPosition = f * _waveformContainer.resolvedStyle.width;
                _playheadContainer.style.left = new StyleLength(leftPosition);
                if (f >= 1)
                {
                    _playheadContainer.style.visibility = new StyleEnum<Visibility>(Visibility.Hidden);
                }
            });
        }

        public sealed override void SetupObjectEditing()
        {
            if (_isInit) return;
            _waveformContainer = _rootVisualElement.Q<VisualElement>("WaveformContainer");
            _playheadContainer = _rootVisualElement.Q<VisualElement>("Playhead");

            _materialDropdown = _rootVisualElement.Q<AnysoundDropdown>("MaterialDropdown");
            _extraMaterialDropdown = _rootVisualElement.Q<AnysoundDropdown>("ExtraMaterialDropdown");
            _actionDropdown = _rootVisualElement.Q<DropdownField>("ActionDropdown");
            _extraDropdown = _rootVisualElement.Q<DropdownField>("ExtraDropdown");
            _sizeSlider = _rootVisualElement.Q<AnysoundSlider>("SizeSlider");

            // The choices come from the generator object, so new materials/actions/extras added in the backend show up automatically
            _materialDropdown.SetItems(_anysoundUIObject.GetMaterialDropdownItems());
            _extraMaterialDropdown.SetItems(_anysoundUIObject.GetMaterialDropdownItems(includeNone: true));
            _actionDropdown.choices = new List<string>(_anysoundUIObject.ActionNames);
            _extraDropdown.choices = WithNone(_anysoundUIObject.ExtraNames);

            _materialDropdown.RegisterValueChangedCallback(evt =>
            {
                _parameters.material = evt.newValue;
                UpdateWaveform();
            });
            // Index 0 is "None", matching the extraMaterial encoding (index + 1, 0 = none)
            _extraMaterialDropdown.RegisterValueChangedCallback(evt =>
            {
                _parameters.extraMaterial = evt.newValue;
                UpdateWaveform();
            });
            _actionDropdown.RegisterValueChangedCallback(_ => OnDropdownChanged(ref _parameters.action, _actionDropdown));
            _extraDropdown.RegisterValueChangedCallback(_ => OnDropdownChanged(ref _parameters.extraSample, _extraDropdown));

            _sizeSlider.lowValue = 0;
            _sizeSlider.highValue = 1;
            _sizeSlider.RegisterValueChangedCallback(evt => { _parameters.size = evt.newValue; });
            _sizeSlider.RegisterDragEndCallback(UpdateWaveform);

            var previewButton = _rootVisualElement.Q<Button>("PreviewButton");
            if (previewButton != null)
            {
                previewButton.clicked += GenerateAndPlayPreview;
            }

            var exportButton = _rootVisualElement.Q<Button>("ExportButton");
            if (exportButton != null)
            {
                exportButton.clicked += ExportClip;
            }

            var redrawButton = _rootVisualElement.Q<Button>("RedrawButton");
            if (redrawButton != null)
            {
                redrawButton.clicked += () => { _anysoundUIObject.CreatePreset(_parameters.ToPresetValues()); };
            }

            var backButton = _rootVisualElement.Q<Button>("BackButton");
            if (backButton != null)
            {
                backButton.clicked += Back;
            }

            _isInit = true;
        }

        static List<string> WithNone(string[] names)
        {
            var choices = new List<string> { NoneChoice };
            choices.AddRange(names);
            return choices;
        }

        void OnDropdownChanged(ref int parameter, DropdownField dropdown)
        {
            parameter = Mathf.Max(0, dropdown.index);
            UpdateWaveform();
        }

        // Pushes the current parameters to the controls without triggering callbacks
        void UpdateControls()
        {
            _materialDropdown?.SetValueWithoutNotify(_parameters.material);
            _extraMaterialDropdown?.SetValueWithoutNotify(_parameters.extraMaterial);
            SetDropdownIndex(_actionDropdown, _parameters.action);
            SetDropdownIndex(_extraDropdown, _parameters.extraSample);
            _sizeSlider?.SetValueWithoutNotify(_parameters.size);
        }

        static void SetDropdownIndex(DropdownField dropdown, int index)
        {
            if (dropdown == null || dropdown.choices == null || dropdown.choices.Count == 0) return;
            dropdown.SetValueWithoutNotify(dropdown.choices[Mathf.Clamp(index, 0, dropdown.choices.Count - 1)]);
        }

        void ExportClip()
        {
            var clip = AnysoundUIDSP.CreateAudioClip(_anysoundUIObject, _parameters);
            if (!clip) return;

            string action = _actionDropdown?.value ?? "Sound";
            string path = EditorUtility.SaveFilePanel(
                "Save Audio Clip",
                "Assets",
                $"AnysoundUI_{_materialDropdown?.selectedName}_{action}.wav",
                "wav");

            if (!string.IsNullOrEmpty(path))
            {
                AnysoundFootstepDSP.SaveClipToWav(clip, path);
                AssetDatabase.Refresh();
            }
        }

        void UpdateWaveform()
        {
            var clip = AnysoundUIDSP.CreateAudioClip(_anysoundUIObject, _parameters);
            AnysoundFootstepsHelper.UpdateWaveform(_waveformContainer, clip);
        }

        public sealed override void SetPresetObject(AnysoundPresetObject preset)
        {
            _anysoundUIObject = preset.generatorObject as AnysoundUIObject;
            if (!_anysoundUIObject)
                return;

            if (_rootVisualElement != null)
            {
                _rootVisualElement.Clear();
                _isInit = false;
                CreateGUI();
            }

            _parameters = AnysoundUIParameters.FromPreset(preset);
            UpdateControls();
            UpdateWaveform();
        }
    }
#endif
}
