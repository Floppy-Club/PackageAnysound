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
        private AnysoundUIObject _anysoundUIObject;
        private AnysoundUIParameters _parameters = AnysoundUIParameters.Default;

        private VisualElement _waveformContainer, _playheadContainer;
        private AnysoundDropdown _actionDropdown, _materialDropdown, _extraMaterialDropdown, _extraDropdown;
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

            // The items come from the generator object, so new materials/actions/extras added in the backend show up automatically.
            // Extra material and extra sample have "None" first, matching their encoding (index + 1, 0 = none)
            _actionDropdown = SetupDropdown("ActionDropdown", _anysoundUIObject.GetActionDropdownItems(), v => _parameters.action = v);
            _materialDropdown = SetupDropdown("MaterialDropdown", _anysoundUIObject.GetMaterialDropdownItems(), v => _parameters.material = v);
            _extraMaterialDropdown = SetupDropdown("ExtraMaterialDropdown", _anysoundUIObject.GetMaterialDropdownItems(includeNone: true),
                v => _parameters.extraMaterial = v);
            _extraDropdown = SetupDropdown("ExtraDropdown", _anysoundUIObject.GetExtraDropdownItems(), v => _parameters.extraSample = v);
            _sizeSlider = _rootVisualElement.Q<AnysoundSlider>("SizeSlider");

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

        AnysoundDropdown SetupDropdown(string dropdownName, List<AnysoundDropdown.Item> items, System.Action<int> setParameter)
        {
            var dropdown = _rootVisualElement.Q<AnysoundDropdown>(dropdownName);
            dropdown.SetItems(items);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                setParameter(evt.newValue);
                UpdateWaveform();
            });
            return dropdown;
        }

        // Pushes the current parameters to the controls without triggering callbacks
        void UpdateControls()
        {
            _actionDropdown?.SetValueWithoutNotify(_parameters.action);
            _materialDropdown?.SetValueWithoutNotify(_parameters.material);
            _extraMaterialDropdown?.SetValueWithoutNotify(_parameters.extraMaterial);
            _extraDropdown?.SetValueWithoutNotify(_parameters.extraSample);
            _sizeSlider?.SetValueWithoutNotify(_parameters.size);
        }

        void ExportClip()
        {
            var clip = AnysoundUIDSP.CreateAudioClip(_anysoundUIObject, _parameters);
            if (!clip) return;

            string action = _actionDropdown?.selectedName ?? "Sound";
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
