using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Exporter;
using Anysound.Shared.Frontend;
using Anysound.Shared.Generators.Footsteps;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Generators.Retro.Frontend
{
#if UNITY_EDITOR
    public class AnysoundRetroEditorWindow : AnysoundGeneratorWindowBase
    {
        private AnysoundRetroObject _anysoundRetroObject;
        private AnysoundRetroParameters _parameters = AnysoundRetroParameters.Default;

        private VisualElement _waveformContainer, _playheadContainer;
        private AnysoundDropdown _categoryDropdown;
        private AnysoundSlider _durationSlider, _hardnessSlider, _characterSlider;
        private Label _characterTagLabel, _characterLowLabel, _characterHighLabel;
        private bool _isInit;

        VisualElement _rootVisualElement;

        public AnysoundRetroEditorWindow(VisualElement rootElement, AnysoundRetroObject retroObject, AnysoundPresetObject preset)
        {
            _rootVisualElement = rootElement;
            _anysoundRetroObject = retroObject;
            SetPresetObject(preset);
            SetupObjectEditing();
        }

        private void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.floppyclub.anysound/Editor/Shared/Generators/Retro/Frontend/uxml/AnysoundRetroInspector.uxml");

            if (visualTree == null)
            {
                Debug.LogError("Could not find AnysoundRetroInspector.uxml");
                return;
            }

            visualTree.CloneTree(_rootVisualElement);
            if (_anysoundRetroObject != null)
            {
                SetupObjectEditing();
            }

            // Key events only reach the root while something inside it has focus. The root itself is focusable,
            // so clicking a non-focusable element (like the random button) keeps the focus here
            _rootVisualElement.focusable = true;
            RegisterKeyDownHandler(_rootVisualElement, OnKeyDownEvent);
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Space)
            {
                if (!_anysoundRetroObject) return;

                if (AnysoundFootstepDSP.IsPreviewing)
                    AnysoundFootstepDSP.StopPreview();

                GenerateAndPlayPreview();

                evt.StopPropagation();
                _rootVisualElement.focusController?.IgnoreEvent(evt);
            }
        }

        void GenerateAndPlayPreview()
        {
            var clip = AnysoundRetroDSP.CreateAudioClip(_anysoundRetroObject, _parameters);
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
            if (_waveformContainer == null) return;

            var categoryItems = new List<AnysoundDropdown.Item>();
            foreach (string categoryName in AnysoundRetroObject.CategoryNames)
                categoryItems.Add(new AnysoundDropdown.Item(categoryName, AnysoundIcon.EightBit));

            _categoryDropdown = _rootVisualElement.Q<AnysoundDropdown>("CategoryDropdown");
            _categoryDropdown.SetItems(categoryItems);
            _categoryDropdown.RegisterValueChangedCallback(evt =>
            {
                _parameters.category = (RetroCategory)evt.newValue;
                UpdateCharacterLabels();
                UpdateWaveform();
            });

            // A new seed is a new random sound within the category's ranges, like the buttons of sfxr
            var randomButton = _rootVisualElement.Q<Button>("CategoryRandomButton");
            if (randomButton != null)
            {
                // Not focusable, so space keeps replaying the current sound instead of clicking the button again
                randomButton.focusable = false;
                randomButton.clicked += () =>
                {
                    _parameters.seed = AnysoundRetroParameters.NewSeed();

                    if (AnysoundFootstepDSP.IsPreviewing)
                        AnysoundFootstepDSP.StopPreview();
                    GenerateAndPlayPreview();

                    // So the next space replays this sound
                    _rootVisualElement.Focus();
                };
            }

            _durationSlider = SetupSlider("DurationSlider", AnysoundRetroParameters.DurationMin, AnysoundRetroParameters.DurationMax,
                v => _parameters.duration = v);
            _hardnessSlider = SetupSlider("HardnessSlider", 0f, 1f, v => _parameters.hardness = v);
            _characterSlider = SetupSlider("CharacterSlider", 0f, 1f, v => _parameters.character = v);

            _characterTagLabel = _rootVisualElement.Q<Label>("CharacterTagLabel");
            _characterLowLabel = _rootVisualElement.Q<Label>("CharacterLowLabel");
            _characterHighLabel = _rootVisualElement.Q<Label>("CharacterHighLabel");

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

            var createPresetButton = _rootVisualElement.Q<Button>("CreatePresetButton");
            if (createPresetButton != null)
            {
                createPresetButton.clicked += () => _anysoundRetroObject.CreatePreset(_parameters.ToPresetValues());
            }

            var backButton = _rootVisualElement.Q<Button>("BackButton");
            if (backButton != null)
            {
                backButton.clicked += Back;
            }

            UpdateControls();
            UpdateWaveform();
            _isInit = true;
        }

        AnysoundSlider SetupSlider(string sliderName, float min, float max, System.Action<float> setParameter)
        {
            var slider = _rootVisualElement.Q<AnysoundSlider>(sliderName);
            // Move the value into the range first: otherwise raising lowValue clamps it, which sends a change event
            // that arrives after a preset has been loaded and overwrites its value with the minimum
            slider.SetValueWithoutNotify(min);
            slider.lowValue = min;
            slider.highValue = max;
            slider.RegisterValueChangedCallback(evt => setParameter(evt.newValue));
            slider.RegisterDragEndCallback(UpdateWaveform);
            return slider;
        }

        // The character slider is the category's own parameter, e.g. the size of an explosion
        void UpdateCharacterLabels()
        {
            if (!_anysoundRetroObject) return;
            var settings = _anysoundRetroObject.GetCategory(_parameters.category);
            if (_characterTagLabel != null) _characterTagLabel.text = settings.characterName;
            if (_characterLowLabel != null) _characterLowLabel.text = settings.characterLowLabel;
            if (_characterHighLabel != null) _characterHighLabel.text = settings.characterHighLabel;
        }

        // Pushes the current parameters to the controls without triggering callbacks
        void UpdateControls()
        {
            _categoryDropdown?.SetValueWithoutNotify((int)_parameters.category);
            _durationSlider?.SetValueWithoutNotify(_parameters.duration);
            _hardnessSlider?.SetValueWithoutNotify(_parameters.hardness);
            _characterSlider?.SetValueWithoutNotify(_parameters.character);
            UpdateCharacterLabels();
        }

        void ExportClip()
        {
            // Copy the current values, so later changes don't affect the export
            var retroObject = _anysoundRetroObject;
            var parameters = _parameters;
            // The exporter asks for one clip at a time: the first is the sound itself, the next ones vary it slightly
            int variation = 0;
            AnysoundExporterWindow.ShowExporterWindow(retroObject, parameters.ToPresetValues(),
                () => AnysoundRetroDSP.CreateAudioClip(retroObject, parameters, variation++),
                "Retro_" + string.Join("_", retroObject.GetDescription(parameters)));
        }

        void UpdateWaveform()
        {
            if (_waveformContainer == null) return;
            var clip = AnysoundRetroDSP.CreateAudioClip(_anysoundRetroObject, _parameters);
            AnysoundFootstepsHelper.UpdateWaveform(_waveformContainer, clip);
        }

        public sealed override void SetPresetObject(AnysoundPresetObject preset)
        {
            _anysoundRetroObject = preset.generatorObject as AnysoundRetroObject;
            if (!_anysoundRetroObject)
                return;

            if (_rootVisualElement != null)
            {
                _rootVisualElement.Clear();
                _isInit = false;
                CreateGUI();
            }

            _parameters = AnysoundRetroParameters.FromPreset(preset);
            UpdateControls();
            UpdateWaveform();
        }
    }
#endif
}
