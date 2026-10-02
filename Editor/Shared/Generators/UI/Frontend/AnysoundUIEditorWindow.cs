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
        private AnysoundDropdown _materialClipDropdown, _extraMaterialClipDropdown, _extraClipDropdown;
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

            // Key events only reach the root while something inside it has focus. The root itself is focusable,
            // so clicking a non-focusable element (like the random buttons) keeps the focus here
            _rootVisualElement.focusable = true;
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
            // Changing a type starts at its first clip. The action can change the extra's clips, so it keeps the index (clamped)
            _actionDropdown = SetupDropdown("ActionDropdown", _anysoundUIObject.GetActionDropdownItems(), v => _parameters.action = v);
            _materialDropdown = SetupDropdown("MaterialDropdown", _anysoundUIObject.GetMaterialDropdownItems(), v =>
            {
                _parameters.material = v;
                _parameters.materialClip = 0;
            });
            _extraMaterialDropdown = SetupDropdown("ExtraMaterialDropdown", _anysoundUIObject.GetMaterialDropdownItems(includeNone: true), v =>
            {
                _parameters.extraMaterial = v;
                _parameters.extraMaterialClip = 0;
            });
            _extraDropdown = SetupDropdown("ExtraDropdown", _anysoundUIObject.GetExtraDropdownItems(), v =>
            {
                _parameters.extraSample = v;
                _parameters.extraSampleClip = 0;
            });

            // The clip items depend on the chosen types, so they are filled in by RefreshClipDropdowns
            _materialClipDropdown = SetupDropdown("MaterialClipDropdown", new List<AnysoundDropdown.Item>(), v => _parameters.materialClip = v);
            _extraMaterialClipDropdown = SetupDropdown("ExtraMaterialClipDropdown", new List<AnysoundDropdown.Item>(),
                v => _parameters.extraMaterialClip = v);
            _extraClipDropdown = SetupDropdown("ExtraClipDropdown", new List<AnysoundDropdown.Item>(), v => _parameters.extraSampleClip = v);

            SetupRandomButton("ActionRandomButton", () => _parameters.actionSeed = AnysoundUIParameters.NewActionSeed());
            SetupRandomButton("MaterialRandomButton", RandomizeMaterial);
            SetupRandomButton("ExtraMaterialRandomButton", RandomizeExtraMaterial);
            SetupRandomButton("ExtraRandomButton", RandomizeExtra);
            RefreshClipDropdowns();
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
                RefreshClipDropdowns();
                UpdateWaveform();
            });
            return dropdown;
        }

        void SetupRandomButton(string buttonName, System.Action randomize)
        {
            var button = _rootVisualElement.Q<Button>(buttonName);
            if (button == null) return;

            // Not focusable, so space keeps replaying the current sound instead of clicking the button again
            button.focusable = false;
            button.clicked += () =>
            {
                randomize();
                UpdateControls();

                if (AnysoundFootstepDSP.IsPreviewing)
                    AnysoundFootstepDSP.StopPreview();
                GenerateAndPlayPreview();

                // So the next space replays this sound
                _rootVisualElement.Focus();
            };
        }

        void RandomizeMaterial()
        {
            int count = _anysoundUIObject.materials?.Count ?? 0;
            if (count == 0) return;
            _parameters.material = Random.Range(0, count);
            _parameters.materialClip = RandomClip(_anysoundUIObject.GetMaterialCollection(_parameters.material));
        }

        // Material 2 and extra are stored as index + 1 (0 = none). Randomizing always picks one of them, never none
        void RandomizeExtraMaterial()
        {
            int count = _anysoundUIObject.materials?.Count ?? 0;
            if (count == 0) return;
            _parameters.extraMaterial = Random.Range(1, count + 1);
            _parameters.extraMaterialClip = RandomClip(_anysoundUIObject.GetMaterialCollection(_parameters.ExtraMaterialIndex));
        }

        void RandomizeExtra()
        {
            int count = _anysoundUIObject.extras?.Count ?? 0;
            if (count == 0) return;
            _parameters.extraSample = Random.Range(1, count + 1);
            _parameters.extraSampleClip = RandomClip(_anysoundUIObject.GetExtraCollection(_parameters.ExtraSampleIndex, _parameters.action));
        }

        static int RandomClip(AnysoundSoundCollectionObject collection)
        {
            int count = collection ? collection.Count : 0;
            return count > 0 ? Random.Range(0, count) : 0;
        }

        /// <summary>
        /// Fills the clip dropdowns with the clips of the chosen types and clamps the clip indices to them
        /// </summary>
        void RefreshClipDropdowns()
        {
            _parameters.materialClip = RefreshClipDropdown(_materialClipDropdown,
                _anysoundUIObject.GetMaterialCollection(_parameters.material), _parameters.materialClip);
            _parameters.extraMaterialClip = RefreshClipDropdown(_extraMaterialClipDropdown,
                _parameters.HasExtraMaterial ? _anysoundUIObject.GetMaterialCollection(_parameters.ExtraMaterialIndex) : null,
                _parameters.extraMaterialClip);
            _parameters.extraSampleClip = RefreshClipDropdown(_extraClipDropdown,
                _parameters.HasExtraSample ? _anysoundUIObject.GetExtraCollection(_parameters.ExtraSampleIndex, _parameters.action) : null,
                _parameters.extraSampleClip);
        }

        static int RefreshClipDropdown(AnysoundDropdown dropdown, AnysoundSoundCollectionObject collection, int clipIndex)
        {
            if (dropdown == null) return clipIndex;
            dropdown.SetItems(AnysoundUIObject.GetClipDropdownItems(collection));
            dropdown.SetValueWithoutNotify(clipIndex);
            return dropdown.value;
        }

        // Pushes the current parameters to the controls without triggering callbacks
        void UpdateControls()
        {
            _actionDropdown?.SetValueWithoutNotify(_parameters.action);
            _materialDropdown?.SetValueWithoutNotify(_parameters.material);
            _extraMaterialDropdown?.SetValueWithoutNotify(_parameters.extraMaterial);
            _extraDropdown?.SetValueWithoutNotify(_parameters.extraSample);
            _sizeSlider?.SetValueWithoutNotify(_parameters.size);
            RefreshClipDropdowns();
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
