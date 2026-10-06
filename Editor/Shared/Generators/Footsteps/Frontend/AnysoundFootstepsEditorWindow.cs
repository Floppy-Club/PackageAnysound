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
        // The surface slider crossfades between the two surfaces chosen in the dropdowns (0 = only A, 1 = only B)
        private int _surfaceA, _surfaceB;
        private float _surfaceCrossfade;
        private float _currentSizeValue;
        private float _currentMovementSpeedValue;
        private VisualElement _waveformContainer;
        private VisualElement _sizeLabelsContainer, _sizeIconsContainer, _playheadContainer;

        int _currentMovementIndex;

        private Button _previewButton;
        private AnysoundSlider _surfaceSlider;
        private AnysoundDropdown _surfaceADropdown, _surfaceBDropdown;
        private Label _surfaceALabel, _surfaceBLabel;
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

            RegisterKeyDownHandler(_rootVisualElement, OnKeyDownEvent);
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
            var clip = AnysoundFootstepDSP.CreateCrossfadedAudioClip(_anysoundFootstepObject, _currentSizeValue, _currentMovementSpeedValue,
                _surfaceA, _surfaceB, _surfaceCrossfade);
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


            _surfaceSlider.lowValue = 0;
            _surfaceSlider.highValue = 1;
            _sizeSlider.highValue = _anysoundFootstepObject.surfaceSettings[0].FootstepSizeSettings.Count - 1;
            _movementSlider.highValue = 2;

            _movementSlider.RegisterValueChangedCallback(evt => { OnMovementSliderValueChanged(_movementSlider.value); });
            _sizeSlider.RegisterValueChangedCallback(evt => { OnSizeSliderValueChanged(_sizeSlider.value); });
            _surfaceSlider.RegisterValueChangedCallback(evt => { OnSurfaceSliderValueChanged(_surfaceSlider.value); });

            _movementSlider.RegisterDragEndCallback(UpdateWaveform);
            _sizeSlider.RegisterDragEndCallback(UpdateWaveform);
            _surfaceSlider.RegisterDragEndCallback(UpdateWaveform);

            _surfaceALabel = _rootVisualElement.Q<Label>("SurfaceALabel");
            _surfaceBLabel = _rootVisualElement.Q<Label>("SurfaceBLabel");
            var surfaceItems = AnysoundFootstepsHelper.GetSurfaceDropdownItems(_anysoundFootstepObject);
            _surfaceADropdown = SetupSurfaceDropdown("SurfaceADropdown", surfaceItems, v => _surfaceA = v);
            _surfaceBDropdown = SetupSurfaceDropdown("SurfaceBDropdown", surfaceItems, v => _surfaceB = v);


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


            //OnSizeSliderValueChanged(1);
            //OnSurfaceSliderValueChanged(1);
            //OnMovementSliderValueChanged(1);
            //UpdateWaveform();
            _isInit = true;
        }

        AnysoundDropdown SetupSurfaceDropdown(string dropdownName, List<AnysoundDropdown.Item> items, System.Action<int> setSurface)
        {
            var dropdown = _rootVisualElement.Q<AnysoundDropdown>(dropdownName);
            dropdown.SetItems(items);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                setSurface(evt.newValue);
                UpdateSurfaceLabels();
                UpdateWaveform();
            });
            return dropdown;
        }

        void ExportClips()
        {
            AnysoundExporterWindow.ShowExporterWindow(_anysoundFootstepObject, _currentSizeValue, _currentMovementSpeedValue, _surfaceA, _surfaceB,
                _surfaceCrossfade);
        }


        Dictionary<string, float> GetPresetValues()
        {
            Dictionary<string, float> presetValues = new Dictionary<string, float>()
            {
                { "Size", _currentSizeValue },
                { "MovementSpeed", _currentMovementSpeedValue },
                { AnysoundFootstepObject.SurfaceAKey, _surfaceA },
                { AnysoundFootstepObject.SurfaceBKey, _surfaceB },
                { AnysoundFootstepObject.SurfaceCrossfadeKey, _surfaceCrossfade },
            };
            return presetValues;
        }

        void UpdateWaveform()
        {
            var clip = AnysoundFootstepsHelper.GenerateAudioClip(_anysoundFootstepObject, _currentSizeValue, _currentMovementSpeedValue,
                _surfaceA, _surfaceB, _surfaceCrossfade);
            AnysoundFootstepsHelper.UpdateWaveform(_waveformContainer, clip);
        }

        void OnMovementSliderValueChanged(float value)
        {
            _movementSlider.SetValueWithoutNotify(value);
            _currentMovementSpeedValue = value;
            _currentMovementIndex = (int)Mathf.Clamp(((_currentMovementSpeedValue / 2f) * 3), 0, 2);
            float maxSizeValue = _anysoundFootstepObject.surfaceSettings[0].FootstepSizeSettings.Count - 1;
            AnysoundFootstepsHelper.UpdateSizeImages(_sizeIconsContainer, _sizeSlider.value, maxSizeValue, _currentMovementIndex);
        }

        void OnSurfaceSliderValueChanged(float value)
        {
            _surfaceSlider.SetValueWithoutNotify(value);
            _surfaceCrossfade = Mathf.Clamp01(value);
            UpdateSurfaceLabels();
        }

        void SetSurfaces(int surfaceA, int surfaceB, float crossfade)
        {
            _surfaceADropdown?.SetValueWithoutNotify(surfaceA);
            _surfaceBDropdown?.SetValueWithoutNotify(surfaceB);
            // The dropdowns clamp to the existing surfaces
            _surfaceA = _surfaceADropdown?.value ?? surfaceA;
            _surfaceB = _surfaceBDropdown?.value ?? surfaceB;
            OnSurfaceSliderValueChanged(crossfade);
        }

        // Shows how much of each surface is in the mix, e.g. "GRASS 70%" ... "30% SAND"
        void UpdateSurfaceLabels()
        {
            int percentB = Mathf.RoundToInt(_surfaceCrossfade * 100f);
            if (_surfaceALabel != null)
                _surfaceALabel.text = $"{_surfaceADropdown?.selectedName.ToUpperInvariant()} {100 - percentB}%";
            if (_surfaceBLabel != null)
                _surfaceBLabel.text = $"{percentB}% {_surfaceBDropdown?.selectedName.ToUpperInvariant()}";
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
                _isInit = false;
                CreateGUI();
            }

            OnMovementSliderValueChanged(preset.GetPresetValue("MovementSpeed"));
            OnSizeSliderValueChanged(preset.GetPresetValue("Size") );
            _anysoundFootstepObject.GetSurfaceCrossfade(preset.HasPresetValue, preset.GetPresetValue,
                out int surfaceA, out int surfaceB, out float crossfade);
            SetSurfaces(surfaceA, surfaceB, crossfade);
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
            _anysoundFootstepObject.SurfaceTypeToCrossfade(surface, out int surfaceA, out int surfaceB, out float crossfade);
            SetSurfaces(surfaceA, surfaceB, crossfade);
            UpdateWaveform();
        }
        
        
    }
#endif
}