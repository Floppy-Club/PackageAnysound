using Anysound.Shared.Footsteps;
using Anysound.Shared.Generators.Footsteps;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Frontend
{
    public class AnysoundWhooshRuntime : MonoBehaviour
    {
        [SerializeField] AnysoundFootstepObject footstepObject;
        [SerializeField] private UIDocument document;

        [SerializeField] private VisualTreeAsset mVisualTreeAsset;

        private AnysoundFootstepObject _anysoundFootstepObject;
        private float _currentSurfaceType;
        private float _currentSizeValue;
        private float _currentMovementSpeed;
        private VisualElement _waveformContainer;
        private VisualElement _sizeLabelsContainer, _sizeIconsContainer, _surfaceIconsContainer, _movementLabelsContainer;


        int _currentMovementIndex;
        AnysoundSlider _surfaceSlider, _movementSlider, _sizeSlider;

        string[] _surfaceFilenames =
        {
            "surface_grass",
            "surface_sand",
            "surface_mud",
            "surface_wood",
            "surface_concrete",
        };

        string[] _sizeFilenames =
        {
            "size_xxl",
            "size_xl",
            "size_l",
            "size_m",
            "size_s",
            "size_xs",
        };

        string[] _movementTypeFilenames =
        {
            "_sneak",
            "_walk",
            "_run",
        };


        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            // Check if space key was pressed
            if (evt.keyCode == KeyCode.Space)
            {
                if (!_anysoundFootstepObject) return;

                if (AnysoundFootstepDSP.IsPreviewing)
                    AnysoundFootstepDSP.StopPreview();

                GenerateAndPlayPreview();
                evt.StopPropagation();
                document.rootVisualElement.focusController?.IgnoreEvent(evt);
            }
        }


        private void Start()
        {
            _anysoundFootstepObject = footstepObject;
            AnysoundFootstepDSP.Setup();
            SetupObjectEditing();
            document.rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
        }

        private void OnDestroy()
        {
            AnysoundFootstepDSP.Release();
        }

        void GeneratePreview()
        {
            var previewClip =
                AnysoundFootstepDSP.CreateMorphedAudioClip(_anysoundFootstepObject, _currentSizeValue, _currentMovementSpeed, _currentSurfaceType);

            if (previewClip != null)
            {
                _waveformContainer.style.backgroundImage = new StyleBackground(WaveformMaker.GenerateWaveformTexture(previewClip, 2));
            }
        }

        private void GenerateAndPlayPreview()
        {
            var previewClip =
                AnysoundFootstepDSP.CreateMorphedAudioClip(_anysoundFootstepObject, _currentSizeValue, _currentMovementSpeed, _currentSurfaceType);

            if (previewClip != null)
            {
                AnysoundFootstepDSP.PlayClip(previewClip, f => {});
                _waveformContainer.style.backgroundImage =
                    new StyleBackground(WaveformMaker.GenerateWaveformTexture(previewClip));
            }
        }

        private void SetupObjectEditing()
        {
            // Create a SerializedObject from the target object


            _waveformContainer = document.rootVisualElement.Q<VisualElement>("WaveformContainer");


            _movementSlider = document.rootVisualElement.Q<AnysoundSlider>("MovementTypeSlider");
            _movementSlider.highValue = 2;
            _movementSlider.RegisterValueChangedCallback(evt =>
            {
                _currentMovementSpeed = _movementSlider.value;
                UpdateMovementVisuals();
                GeneratePreview();
            });

            _sizeSlider = document.rootVisualElement.Q<AnysoundSlider>("SizeSlider");
            _sizeSlider.highValue = 3;
            _sizeSlider.RegisterValueChangedCallback(evt =>
            {
                _currentSizeValue = _sizeSlider.value;
                OnSizeSliderValueChanged();
                GeneratePreview();
            });


            _surfaceSlider = document.rootVisualElement.Q<AnysoundSlider>("SurfaceSlider");
            _surfaceSlider.highValue = 2;
            _surfaceSlider.RegisterValueChangedCallback(evt =>
            {
                UpdateVisuals();
                GeneratePreview();
            });


            var previewButton = document.rootVisualElement.Q<Button>("PreviewButton");
            if (previewButton != null)
            {
                previewButton.clicked += GenerateAndPlayPreview;
            }


            _sizeLabelsContainer = document.rootVisualElement.Q<VisualElement>("SizeLabelsContainer");
            _sizeIconsContainer = document.rootVisualElement.Q<VisualElement>("SizeIconsContainer");
            _surfaceIconsContainer = document.rootVisualElement.Q<VisualElement>("SurfaceIconsContainer");
            _movementLabelsContainer = document.rootVisualElement.Q<VisualElement>("MovementLabelsContainer");

            _surfaceSlider.SetValueWithoutNotify(1);
            _sizeSlider.SetValueWithoutNotify(1);
            _movementSlider.SetValueWithoutNotify(1);
            UpdateVisuals();
            OnSizeSliderValueChanged();
            GeneratePreview();
        }


        void UpdateVisuals()
        {
            _currentSurfaceType = _surfaceSlider.value;
            int index = 0;
            int surfaceIndex = (int)Mathf.Clamp(((_currentSurfaceType / 2f) * 3), 0, 2);
            _surfaceIconsContainer.Query<AnysoundSurfaceToggleControl>().ForEach((element =>
            {
                string fileName = _surfaceFilenames[index];
                fileName += index == surfaceIndex ? "_fill" : "";

                element.IconImage = Resources.Load<Texture2D>(fileName);
                element.SetToggleState(index == surfaceIndex);
                index++;
            }));
        }

        void UpdateMovementVisuals()
        {
            int index = 0;
            _currentMovementIndex = (int)Mathf.Clamp(((_currentMovementSpeed / 2f) * 3), 0, 2);
            _movementLabelsContainer.Query<Label>().ForEach((element =>
            {
                if (element != _sizeIconsContainer)
                {
                    element.parent.style.backgroundColor =
                        (index == _currentMovementIndex ? AnysoundFootstepsHelper.AnysoundFootstepsEditorExtensions.HexToColor("60C75C") : Color.clear);
                    element.style.color = (index == _currentMovementIndex ? Color.black : AnysoundFootstepsHelper.AnysoundFootstepsEditorExtensions.HexToColor("60C75C"));
                    index++;
                }
            }));
            UpdateSizeImages();
        }

        void OnSizeSliderValueChanged()
        {
            int sizeIndex = 5 - (int)((_sizeSlider.value / 3f) * 6);
            if (sizeIndex < 0) sizeIndex = 0;

            int index = 0;
            _sizeLabelsContainer.Query<Label>().ForEach(label =>
            {
                label.style.color = index == sizeIndex
                    ? new Color(0.2352941176f, 0.6509803922f, 1)
                    : new Color(0.1254901961f, 0.368627451f, 0.5764705882f);
                index++;
            });

            UpdateSizeImages();
        }


        void UpdateSizeImages()
        {
            int sizeIndex = 5 - (int)((_currentSizeValue / 3f) * 6);
            if (sizeIndex < 0) sizeIndex = 0;
            int index2 = 5;
            _sizeIconsContainer.Query<VisualElement>().ForEach(element =>
            {
                if (element != _sizeIconsContainer)
                {
                    element.style.backgroundImage = new StyleBackground(GetSizeImage(index2, index2 == sizeIndex));
                    index2--;
                }
            });
        }

        VectorImage GetSizeImage(int currentSizeIndex, bool isActive)
        {
            string sizeFilename = _sizeFilenames[currentSizeIndex] + _movementTypeFilenames[_currentMovementIndex];
            sizeFilename += isActive ? "_fill" : "";
            return Resources.Load<VectorImage>(sizeFilename);
        }
    }
}