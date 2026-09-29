using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Frontend;
using Anysound.Shared.Generators.Footsteps;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Generators.Whoosh.Frontend
{
#if UNITY_EDITOR
    public class AnysoundWhooshEditorWindow : AnysoundGeneratorWindowBase
    {
        private VisualTreeAsset _mVisualTreeAsset;

        private AnysoundWhooshObject _anysoundWhooshObject;

        private float _currentSizeValue;
        private float _currentMovementSpeed;
        private float _currentDurationType;
        private float _currentFluctuation;
        private VisualElement _waveformContainer, _playheadContainer;

        // Same ranges as the backend window, so presets created there load with the same values
        const float MovementMin = 0f, MovementMax = 1f, MovementDefault = 0.5f;
        const float SizeMin = 0.5f, SizeMax = 2f, SizeDefault = 1f;
        const float DurationMin = 0.05f, DurationMax = 0.5f, DurationDefault = 0.25f;
        const float FluctuationMin = 0f, FluctuationMax = 1f, FluctuationDefault = 0.5f;

        private Button _previewButton;
        private AnysoundSlider _durationSlider;
        private AnysoundSlider _sizeSlider;
        private AnysoundSlider _movementSlider;
        AnysoundSlider _fluctuationSlider;
        private bool _isInit;

        VisualElement _rootVisualElement;

        public AnysoundWhooshEditorWindow(VisualElement rootElement, AnysoundWhooshObject whooshObject, AnysoundPresetObject preset)
        {
            _rootVisualElement = rootElement;
            _anysoundWhooshObject = whooshObject;
            SetPresetObject(preset);
            SetupObjectEditing();
        }

        private void CreateGUI()
        {
            // Import UXML
            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/com.floppyclub.anysound/Editor/Shared/Generators/Whoosh/Frontend/uxml/AnysoundWhooshInspector.uxml");
            if (visualTree == null)
            {
                Debug.LogError("Could not find AnysoundWhooshInspector.uxml");
                return;
            }

            visualTree.CloneTree(_rootVisualElement);
            if (_anysoundWhooshObject != null)
            {
                SetupObjectEditing();
            }

            RegisterKeyDownHandler(_rootVisualElement, OnKeyDownEvent);
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Space)
            {
                if (!_anysoundWhooshObject) return;

                if (AnysoundWhoosh.IsPreviewing)
                    AnysoundWhoosh.StopPreview();

                GenerateAndPlayPreview();

                evt.StopPropagation();
                _rootVisualElement.focusController?.IgnoreEvent(evt);
            }
        }

        Dictionary<string, float> GetCurrentPreset()
        {
            Dictionary<string, float> presetValues = new Dictionary<string, float>
            {
                { "Size", _currentSizeValue },
                { "Movement", _currentMovementSpeed },
                { "Duration", _currentDurationType },
                { "Fluctuation", _currentFluctuation }
            };
            return presetValues;
        }

        void GenerateAndPlayPreview()
        {
            var clip = AnysoundWhooshHelper.GenerateAudioClip(_anysoundWhooshObject, GetCurrentPreset());
            AnysoundFootstepsHelper.UpdateWaveform(_waveformContainer, clip);
            AnysoundWhoosh.PlayClip(clip, f =>
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

        private void OnEnable() => AnysoundFootstepDSP.Setup();

        private void OnDisable()
        {
            AnysoundFootstepDSP.Release();
        }


        public sealed override void SetupObjectEditing()
        {
            if (_isInit) return;
            _waveformContainer = _rootVisualElement.Q<VisualElement>("WaveformContainer");


            _movementSlider = SetupSlider("MovementSpeedSlider", MovementMin, MovementMax, OnMovementSliderValueChanged);
            _sizeSlider = SetupSlider("SizeSlider", SizeMin, SizeMax, OnSizeSliderValueChanged);
            _durationSlider = SetupSlider("DurationSlider", DurationMin, DurationMax, OnDurationSliderValueChanged);
            _fluctuationSlider = SetupSlider("FluctuationSlider", FluctuationMin, FluctuationMax, OnFluctuationSliderValueChanged);

            _playheadContainer = _rootVisualElement.Q<VisualElement>("Playhead");

            _previewButton = _rootVisualElement.Q<Button>("PreviewButton");
            if (_previewButton != null)
            {
                _previewButton.clicked += GenerateAndPlayPreview;
            }

            var exportButton = _rootVisualElement.Q<Button>("ExportButton");
            exportButton.clicked += ExportClips;


            var redrawButton = _rootVisualElement.Q<Button>("RedrawButton");
            if (redrawButton != null)
            {
                redrawButton.clicked += () =>
                {
                    Debug.Log("create preset with 0 values");
                    _anysoundWhooshObject.CreatePreset(GetCurrentPreset());
                };
            }

            var backButton = _rootVisualElement.Q<Button>("BackButton");
            if (backButton != null)
            {
                backButton.clicked += Back;
            }

            OnMovementSliderValueChanged(MovementDefault);
            OnSizeSliderValueChanged(SizeDefault);
            OnDurationSliderValueChanged(DurationDefault);
            OnFluctuationSliderValueChanged(FluctuationDefault);
            UpdateWaveform();
            _isInit = true;
        }

        AnysoundSlider SetupSlider(string sliderName, float min, float max, System.Action<float> onValueChanged)
        {
            var slider = _rootVisualElement.Q<AnysoundSlider>(sliderName);
            slider.lowValue = min;
            slider.highValue = max;
            slider.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            slider.RegisterDragEndCallback(UpdateWaveform);
            return slider;
        }


        void ExportClips()
        {
            Debug.Log("Exporting audio clip");

            var clip = AnysoundWhooshHelper.GenerateAudioClip(_anysoundWhooshObject, GetCurrentPreset());

            string path = EditorUtility.SaveFilePanel(
                "Save Audio Clip",
                "Assets",
                "AnysoundFootstepClip.wav",
                "wav");

            if (!string.IsNullOrEmpty(path))
            {
                // Convert to a project-relative path if inside the project
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }

                AnysoundFootstepDSP.SaveClipToWav(clip, path);
                AssetDatabase.Refresh(); // Refresh the asset database to show the new file
            }
        }


        void UpdateWaveform()
        {
            var clip = AnysoundWhooshHelper.GenerateAudioClip(_anysoundWhooshObject, GetCurrentPreset());
            AnysoundWhooshHelper.UpdateWaveform(_waveformContainer, clip);
        }

        void OnMovementSliderValueChanged(float value)
        {
            _movementSlider.SetValueWithoutNotify(value);
            _currentMovementSpeed = value;
        }

        void OnSizeSliderValueChanged(float value)
        {
            _sizeSlider.SetValueWithoutNotify(value);
            _currentSizeValue = value;
        }

        void OnDurationSliderValueChanged(float value)
        {
            _durationSlider.SetValueWithoutNotify(value);
            _currentDurationType = value;
        }

        void OnFluctuationSliderValueChanged(float value)
        {
            _fluctuationSlider.SetValueWithoutNotify(value);
            _currentFluctuation = value;
        }

        // Presets without stored values return 0, which is outside some ranges (e.g. duration), so fall back to the default
        static float PresetValueOrDefault(AnysoundPresetObject preset, string key, float min, float max, float defaultValue)
        {
            float value = preset.GetPresetValue(key);
            return value >= min && value <= max ? value : defaultValue;
        }

        public sealed override void SetPresetObject(AnysoundPresetObject preset)
        {
            _anysoundWhooshObject = preset.generatorObject as AnysoundWhooshObject;
            if (!_anysoundWhooshObject)
                return;

            if (_rootVisualElement != null)
            {
                _rootVisualElement.Clear();
                _isInit = false;
                CreateGUI();
            }

            OnMovementSliderValueChanged(PresetValueOrDefault(preset, "Movement", MovementMin, MovementMax, MovementDefault));
            OnSizeSliderValueChanged(PresetValueOrDefault(preset, "Size", SizeMin, SizeMax, SizeDefault));
            OnDurationSliderValueChanged(PresetValueOrDefault(preset, "Duration", DurationMin, DurationMax, DurationDefault));
            OnFluctuationSliderValueChanged(PresetValueOrDefault(preset, "Fluctuation", FluctuationMin, FluctuationMax, FluctuationDefault));

            UpdateWaveform();
        }
    }
#endif
}