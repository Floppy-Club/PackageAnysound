using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Exporter;
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
        const float MovementMin = AnysoundWhooshHelper.MovementMin, MovementMax = AnysoundWhooshHelper.MovementMax, MovementDefault = 0.5f;
        const float SizeMin = AnysoundWhooshHelper.SizeMin, SizeMax = AnysoundWhooshHelper.SizeMax, SizeDefault = 1f;
        const float DurationMin = AnysoundWhooshHelper.DurationMin, DurationMax = AnysoundWhooshHelper.DurationMax, DurationDefault = 0.25f;
        const float FluctuationMin = 0f, FluctuationMax = 1f, FluctuationDefault = 0.5f;

        private Button _previewButton;
        private AnysoundSlider _durationSlider;
        private AnysoundSlider _sizeSlider;
        private AnysoundSlider _movementSlider;
        private AnysoundWhooshDrawing _movementDrawing;
        const int MovementDrawingSamples = 128;
        // The thickness of the drawing at the smallest size (the biggest size fills the height)
        const float MinDrawingThickness = 0.35f;
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


            _movementDrawing = _rootVisualElement.Q<AnysoundWhooshDrawing>("MovementDrawing");
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


            var createPresetButton = _rootVisualElement.Q<Button>("CreatePresetButton");
            if (createPresetButton != null)
            {
                createPresetButton.clicked += () => _anysoundWhooshObject.CreatePreset(GetCurrentPreset());
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
            // Move the value into the range first: otherwise raising lowValue clamps it, which sends a change event
            // that arrives after a preset has been loaded and overwrites its value with the minimum
            slider.SetValueWithoutNotify(min);
            slider.lowValue = min;
            slider.highValue = max;
            slider.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
            slider.RegisterDragEndCallback(UpdateWaveform);
            return slider;
        }


        void ExportClips()
        {
            // Copy the current values, so later slider changes don't affect the export
            var whooshObject = _anysoundWhooshObject;
            var presetValues = GetCurrentPreset();
            AnysoundExporterWindow.ShowExporterWindow(whooshObject, presetValues,
                () => AnysoundWhooshHelper.GenerateAudioClip(whooshObject, presetValues),
                GetDefaultFileName());
        }

        // Describes the current sound, e.g. "Whoosh_Large_Fast_Short"
        string GetDefaultFileName() =>
            "Whoosh_" + string.Join("_", AnysoundWhooshHelper.GetDescription(_currentSizeValue, _currentMovementSpeed, _currentDurationType));


        void UpdateWaveform()
        {
            var clip = AnysoundWhooshHelper.GenerateAudioClip(_anysoundWhooshObject, GetCurrentPreset());
            AnysoundWhooshHelper.UpdateWaveform(_waveformContainer, clip);
        }

        void OnMovementSliderValueChanged(float value)
        {
            _movementSlider.SetValueWithoutNotify(value);
            _currentMovementSpeed = value;
            UpdateMovementDrawing();
        }

        // Draws the whoosh the current values give, sampled the same way the DSP applies them:
        // the amplitude envelope (movement), the length (duration) and the filter LFO (fluctuation)
        void UpdateMovementDrawing()
        {
            if (_movementDrawing == null || !_anysoundWhooshObject) return;
            var settings = _anysoundWhooshObject.MovementSettings;
            if (settings == null || settings.Length == 0) return;

            var movementSettings = _anysoundWhooshObject.GetWhooshMovementSettings(_currentMovementSpeed);
            var envelope = movementSettings.AmplitudeEnvelope;
            var lfo = movementSettings.FluctuationLFOGenerator;
            float wobbleDepth = Mathf.Clamp01(lfo.amplitude * _currentFluctuation);

            var amplitude = new float[MovementDrawingSamples];
            var wobble = new float[MovementDrawingSamples];
            for (int i = 0; i < amplitude.Length; i++)
            {
                float t = i / (amplitude.Length - 1f);
                amplitude[i] = envelope.Evaluate(t, _currentDurationType);
                float lfoPhase = Mathf.Repeat(2f * Mathf.PI * lfo.speed * t * _currentDurationType, 2f * Mathf.PI);
                wobble[i] = EvaluateLfo(lfo.lfoType, lfoPhase) * wobbleDepth;
            }

            _movementDrawing.SetWhoosh(amplitude, wobble, Mathf.InverseLerp(0f, DurationMax, _currentDurationType),
                Mathf.Lerp(MinDrawingThickness, 1f, Mathf.InverseLerp(SizeMin, SizeMax, _currentSizeValue)));
        }

        // Same shapes as the filter LFO in AnysoundWhooshDSP
        static float EvaluateLfo(AnysoundWhooshObject.LFOGeneratorSettings.LFOTypes type, float phase)
        {
            switch (type)
            {
                case AnysoundWhooshObject.LFOGeneratorSettings.LFOTypes.Square:
                    return phase < Mathf.PI ? 1f : -1f;
                case AnysoundWhooshObject.LFOGeneratorSettings.LFOTypes.Triangle:
                    return phase < Mathf.PI ? phase / Mathf.PI * 2f - 1f : 1f - (phase - Mathf.PI) / Mathf.PI * 2f;
                default:
                    return Mathf.Sin(phase);
            }
        }

        void OnSizeSliderValueChanged(float value)
        {
            _sizeSlider.SetValueWithoutNotify(value);
            _currentSizeValue = value;
            UpdateMovementDrawing();
        }

        void OnDurationSliderValueChanged(float value)
        {
            _durationSlider.SetValueWithoutNotify(value);
            _currentDurationType = value;
            UpdateMovementDrawing();
        }

        void OnFluctuationSliderValueChanged(float value)
        {
            _fluctuationSlider.SetValueWithoutNotify(value);
            _currentFluctuation = value;
            UpdateMovementDrawing();
        }

        // Presets without a stored value get the default. Stored values are clamped, so a value a rounding error
        // outside the range (e.g. 0.50000006 for a duration of 0.5) still loads instead of falling back to the default
        static float PresetValueOrDefault(AnysoundPresetObject preset, string key, float min, float max, float defaultValue) =>
            preset.HasPresetValue(key) ? Mathf.Clamp(preset.GetPresetValue(key), min, max) : defaultValue;

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