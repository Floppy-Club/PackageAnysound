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
        [SerializeField] private VisualTreeAsset mVisualTreeAsset;

        private AnysoundWhooshObject _anysoundWhooshObject;
        private float _currentSurfaceType;
        private float _currentSizeValue;
        private float _currentMovementSpeed;
        private float _currentFluctuation;
        private VisualElement _waveformContainer;
        private VisualElement _sizeLabelsContainer, _sizeIconsContainer, _surfaceIconsContainer, _movementLabelsContainer, _playheadContainer;

        int _currentMovementIndex;

        private Button _previewButton;
        private AnysoundSlider _surfaceSlider;
        private AnysoundSlider _sizeSlider;
        private AnysoundSlider _movementSlider;
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
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.floppyclub.anysound/Editor/Shared/Generators/Whoosh/Frontend/uxml/AnysoundWhooshInspector.uxml");
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

            _rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
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
                { "Duration", _currentSurfaceType },
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
            _movementSlider = _rootVisualElement.Q<AnysoundSlider>("MovementTypeSlider");
            _sizeSlider = _rootVisualElement.Q<AnysoundSlider>("SizeSlider");
            _surfaceSlider = _rootVisualElement.Q<AnysoundSlider>("SurfaceSlider");
            _playheadContainer = _rootVisualElement.Q<VisualElement>("Playhead");

            _surfaceSlider.highValue = 2;
            _sizeSlider.highValue = 3;
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

            _sizeLabelsContainer = _rootVisualElement.Q<VisualElement>("SizeLabelsContainer");
            _sizeIconsContainer = _rootVisualElement.Q<VisualElement>("SizeIconsContainer");
            _surfaceIconsContainer = _rootVisualElement.Q<VisualElement>("SurfaceIconsContainer");
            _movementLabelsContainer = _rootVisualElement.Q<VisualElement>("MovementLabelsContainer");


            OnSizeSliderValueChanged(1);
            OnSurfaceSliderValueChanged(1);
            OnMovementSliderValueChanged(1);
            UpdateWaveform();
            _isInit = true;
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
            _currentMovementIndex = (int)Mathf.Clamp(((_currentMovementSpeed / 2f) * 3), 0, 2);
            AnysoundFootstepsHelper.UpdateMovementVisuals(_movementLabelsContainer, _currentMovementSpeed);
            AnysoundFootstepsHelper.UpdateSizeImages(_sizeIconsContainer, _sizeSlider.value, 5, _currentMovementIndex);
        }

        void OnSurfaceSliderValueChanged(float value)
        {
            _surfaceSlider.SetValueWithoutNotify(value);
            _currentSurfaceType = value;
            AnysoundFootstepsHelper.UpdateSurfaceIcons(_surfaceIconsContainer, _currentSurfaceType, 3);
        }

        void OnSizeSliderValueChanged(float value)
        {
            _sizeSlider.SetValueWithoutNotify(value);
            _currentSizeValue = value;
            AnysoundFootstepsHelper.UpdateSizeLabels(_sizeLabelsContainer, value, 6);
            AnysoundFootstepsHelper.UpdateSizeImages(_sizeIconsContainer, value, 5, _currentMovementIndex);
        }

        public sealed override void SetPresetObject(AnysoundPresetObject preset)
        {
            _anysoundWhooshObject = preset.generatorObject as AnysoundWhooshObject;
            if (_rootVisualElement != null)
            {
                _rootVisualElement.Clear();
                CreateGUI();
            }

            _currentFluctuation = preset.GetPresetValue("Fluctuation");
            _currentMovementSpeed = preset.GetPresetValue("Movement");
            _currentSizeValue = preset.GetPresetValue("Size");
            _currentSurfaceType = preset.GetPresetValue("Surface");

            UpdateWaveform();
        }
    }
#endif
}