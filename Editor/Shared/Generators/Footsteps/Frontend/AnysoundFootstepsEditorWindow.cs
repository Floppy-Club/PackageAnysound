using System.Collections.Generic;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Browser;
using Anysound.Shared.Exporter;
using Anysound.Shared.Footsteps;
using Anysound.Shared.Frontend;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Generators.Footsteps.Frontend
{
#if UNITY_EDITOR
    public class AnysoundFootstepsEditorWindow : AnysoundGeneratorWindowBase
    {
        private AnysoundFootstepObject _anysoundFootstepObject;
        private float _currentSurfaceTypeValue;
        private float _currentSizeValue;
        private float _currentMovementSpeedValue;
        private VisualElement _waveformContainer;
        private VisualElement _sizeLabelsContainer, _sizeIconsContainer, _surfaceIconsContainer, _movementLabelsContainer, _playheadContainer;

        int _currentMovementIndex;

        private Button _previewButton;
        private AnysoundSlider _surfaceSlider;
        private AnysoundSlider _sizeSlider;
        private AnysoundSlider _movementSlider;
        private bool _isInit;


        VisualElement _rootVisualElement;

        public AnysoundFootstepsEditorWindow(VisualElement rootElement, AnysoundFootstepObject footstepObject, AnysoundPresetObject preset)
        {
            _rootVisualElement = rootElement;
            _anysoundFootstepObject = footstepObject;
            SetPresetObject(preset);
            SetupObjectEditing();
        }


        private void CreateGUI()
        {
            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.floppyclub.anysound/Editor/Shared/Generators/Footsteps/Frontend/uxml/AnysoundFootstepsInspector.uxml");

            if (visualTree == null)
            {
                Debug.LogError("Could not find AnysoundFootstepsInspector.uxml");
                return;
            }

            visualTree.CloneTree(_rootVisualElement);
            if (_anysoundFootstepObject != null)
            {
                SetupObjectEditing();
            }

            _rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Space)
            {
                if (!_anysoundFootstepObject) return;

                if (AnysoundFootstepDSP.IsPreviewing)
                    AnysoundFootstepDSP.StopPreview();

                GenerateAndPlayPreview();

                evt.StopPropagation();
                _rootVisualElement.focusController?.IgnoreEvent(evt);
            }
        }


        void GenerateAndPlayPreview()
        {
            var clip = AnysoundFootstepDSP.CreateMorphedAudioClip(_anysoundFootstepObject, _currentSizeValue, _currentMovementSpeedValue,
                _currentSurfaceTypeValue);
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

        private void OnEnable() => AnysoundFootstepDSP.Setup();

        private void OnDisable()
        {
            AnysoundFootstepDSP.Release();
        }


        public sealed override void SetupObjectEditing()
        {
            if (_isInit) return;
            _waveformContainer = _rootVisualElement.Q<VisualElement>("WaveformContainer");
            _playheadContainer = _rootVisualElement.Q<VisualElement>("Playhead");
            _movementSlider = _rootVisualElement.Q<AnysoundSlider>("MovementTypeSlider");
            _sizeSlider = _rootVisualElement.Q<AnysoundSlider>("SizeSlider");
            _surfaceSlider = _rootVisualElement.Q<AnysoundSlider>("SurfaceSlider");


            _surfaceSlider.highValue = _anysoundFootstepObject.surfaceSettings.Count - 1;
            _sizeSlider.highValue = _anysoundFootstepObject.surfaceSettings[0].FootstepSizeSettings.Count - 1;
            _movementSlider.highValue = 2;

            _movementSlider.RegisterValueChangedCallback(evt => { OnMovementSliderValueChanged(_movementSlider.value); });
            _sizeSlider.RegisterValueChangedCallback(evt => { OnSizeSliderValueChanged(_sizeSlider.value); });
            _surfaceSlider.RegisterValueChangedCallback(evt => { OnSurfaceSliderValueChanged(_surfaceSlider.value); });

            _movementSlider.RegisterDragEndCallback(UpdateWaveform);
            _sizeSlider.RegisterDragEndCallback(UpdateWaveform);
            _surfaceSlider.RegisterDragEndCallback(UpdateWaveform);


            _previewButton = _rootVisualElement.Q<Button>("PreviewButton");
            if (_previewButton != null)
            {
                _previewButton.clicked += GenerateAndPlayPreview;
            }

            var exportButton = _rootVisualElement.Q<Button>("ExportButton");
            exportButton.clicked += ExportClips;


            var redrawButton = _rootVisualElement.Q<Button>("RedrawButton");
            if (_previewButton != null)
            {
                redrawButton.clicked += () => { _anysoundFootstepObject.CreatePreset(GetPresetValues()); };
            }

            var backButton = _rootVisualElement.Q<Button>("BackButton");
            if (backButton != null)
            {
                backButton.clicked += Back;
            }

            _sizeLabelsContainer = _rootVisualElement.Q<VisualElement>("SizeLabelsContainer");
            _sizeIconsContainer = _rootVisualElement.Q<VisualElement>("SizeIconsContainer");
            _surfaceIconsContainer = _rootVisualElement.Q<VisualElement>("SurfaceIconsContainer");
            _movementLabelsContainer = _rootVisualElement.Q<VisualElement>("MovementLabelsContainer");


            //OnSizeSliderValueChanged(1);
            //OnSurfaceSliderValueChanged(1);
            //OnMovementSliderValueChanged(1);
            //UpdateWaveform();
            _isInit = true;
        }

        void ExportClips()
        {
            AnysoundExporterWindow.ShowExporterWindow(_anysoundFootstepObject, _currentSizeValue, _currentMovementSpeedValue, _currentSurfaceTypeValue);
        }


        Dictionary<string, float> GetPresetValues()
        {
            Dictionary<string, float> presetValues = new Dictionary<string, float>()
            {
                { "Size", _currentSizeValue },
                { "MovementSpeed", _currentMovementSpeedValue },
                { "SurfaceType", _currentSurfaceTypeValue },
            };
            return presetValues;
        }

        void UpdateWaveform()
        {
            var clip = AnysoundFootstepsHelper.GenerateAudioClip(_anysoundFootstepObject, _currentSizeValue, _currentMovementSpeedValue,
                _currentSurfaceTypeValue);
            AnysoundFootstepsHelper.UpdateWaveform(_waveformContainer, clip);
        }

        void OnMovementSliderValueChanged(float value)
        {
            _movementSlider.SetValueWithoutNotify(value);
            _currentMovementSpeedValue = value;
            _currentMovementIndex = (int)Mathf.Clamp(((_currentMovementSpeedValue / 2f) * 3), 0, 2);
            AnysoundFootstepsHelper.UpdateMovementVisuals(_movementLabelsContainer, _currentMovementSpeedValue);
            float maxSizeValue = _anysoundFootstepObject.surfaceSettings[0].FootstepSizeSettings.Count - 1;
            AnysoundFootstepsHelper.UpdateSizeImages(_sizeIconsContainer, _sizeSlider.value, maxSizeValue, _currentMovementIndex);
        }

        void OnSurfaceSliderValueChanged(float value)
        {
            _surfaceSlider.SetValueWithoutNotify(value);
            _currentSurfaceTypeValue = value;
            AnysoundFootstepsHelper.UpdateSurfaceIcons(_surfaceIconsContainer, _currentSurfaceTypeValue, _anysoundFootstepObject.surfaceSettings.Count);
        }

        void OnSizeSliderValueChanged(float value)
        {
            _sizeSlider.SetValueWithoutNotify(value);
            _currentSizeValue = value;
            float maxSizeValue = _anysoundFootstepObject.surfaceSettings[0].FootstepSizeSettings.Count - 1;
            AnysoundFootstepsHelper.UpdateSizeLabels(_sizeLabelsContainer, value, maxSizeValue);
            AnysoundFootstepsHelper.UpdateSizeImages(_sizeIconsContainer, value, maxSizeValue, _currentMovementIndex);
        }

        public sealed override void SetPresetObject(AnysoundPresetObject preset)
        {
            _anysoundFootstepObject = preset.generatorObject as AnysoundFootstepObject;
            if (!_anysoundFootstepObject)
                return;

            if (_rootVisualElement != null)
            {
                _rootVisualElement.Clear();
                CreateGUI();
            }

            OnMovementSliderValueChanged(preset.GetPresetValue("MovementSpeed"));
            OnSizeSliderValueChanged(preset.GetPresetValue("Size") );
            OnSurfaceSliderValueChanged(preset.GetPresetValue("SurfaceType") );
            UpdateWaveform();
        }

        public void SetTargetObject(AnysoundFootstepObject targetObject)
        {
            _anysoundFootstepObject = targetObject;
            if (_rootVisualElement != null)
            {
                _rootVisualElement.Clear();
                CreateGUI();
            }
        }

        public void SetSliderValues(float surface, float size, float movement)
        {
            OnMovementSliderValueChanged(movement);
            OnSizeSliderValueChanged(size);
            OnSurfaceSliderValueChanged(surface);
            UpdateWaveform();
        }
        
        
    }
#endif
}