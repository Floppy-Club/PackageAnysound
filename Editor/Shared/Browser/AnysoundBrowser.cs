using System.Collections.Generic;
using System.Linq;
using Anysound.Shared.BaseClasses;
using Anysound.Shared.Footsteps;
using Anysound.Shared.Exporter;
using Anysound.Shared.Frontend;
using Anysound.Shared.Generators.Footsteps;
using Anysound.Shared.Generators.Footsteps.Frontend;
using Anysound.Shared.Generators.Whoosh;
using Anysound.Shared.Generators.Whoosh.Frontend;
using Anysound.Shared.Generators.UI;
using Anysound.Shared.Generators.UI.Frontend;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Browser
{
    public class AnysoundBrowser : EditorWindow
    {
        private VisualTreeAsset visualTreeAsset = default;
        private VisualTreeAsset tagTemplate = default;
        private VisualTreeAsset tagLabelTemplate = default;
        private VisualTreeAsset generatorButtonTemplate = default;
        private VisualTreeAsset soundItemTemplate = default;

        List<string> _soundTags = new();

        private List<AnysoundPresetObject> _currentPresetsList = new();
        private List<UISoundListItemController> _soundListItems = new();

        private List<AnysoundGeneratorBase> _generatorsList = new();

        private ListView _listView;
        private List<string> _currentTagsList = new();
        private List<Button> _tagButtons = new();


        public static Color ColorHighLight => AnysoundFootstepsHelper.AnysoundFootstepsEditorExtensions.HexToColor("60C75C");
        public static Color ColorLowLight = new(0.325f, 0.325f, 0.325f, 0);

        private UISoundPreviewController _soundPreviewController;
        private VisualElement _tagsContainer, _generatorButtonsContainer;
        private Button _refreshButton;
        private Button _loadSoundButton;
        private Button _playSoundButton;
        private Button _searchButton;
        private TextField _searchText;
        private List<AnysoundPresetObject> _allPresets = new();
        private List<AnysoundPresetObject> _generatorPresets = new();
        AnysoundGeneratorBase _currentGenerator;
        private VisualElement _playheadContainer;
        private VisualElement _waveformContainer;

        [MenuItem("Window/Anysound Sound Browser")]
        public static void ShowSoundBrowser()
        {
            AnysoundBrowser window = GetWindow<AnysoundBrowser>();
            window.titleContent = new GUIContent("Anysound");
            window.CreateGUI();
        }

        private const string BrowserFolder = "Packages/com.floppyclub.anysound/Editor/Shared/Browser";

        public void CreateGUI()
        {
            visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{BrowserFolder}/AnysoundBrowser.uxml");
            tagTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{BrowserFolder}/SoundBrowserTag.uxml");
            tagLabelTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{BrowserFolder}/SoundBrowserTagLabel.uxml");
            generatorButtonTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{BrowserFolder}/GeneratorButtonTemplate.uxml");
            soundItemTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{BrowserFolder}/SoundBrowserResult.uxml");

            // Remove the space-to-preview handler of the generator that was shown in this window
            AnysoundGeneratorWindowBase.UnregisterKeyDownHandler();
            rootVisualElement.Clear();

            TemplateContainer container = visualTreeAsset.CloneTree();
            // Fill the window, so the sounds list gets a fixed height to scroll within
            container.style.flexGrow = 1;
            rootVisualElement.Add(container);
            SetupObjectEditing();
        }

        private void SetupObjectEditing()
        {
            var preview = rootVisualElement.Q("SoundPreviewContainer").Q<VisualElement>();
            _soundPreviewController = new UISoundPreviewController(preview);
            _listView = rootVisualElement.Q<ListView>("SoundsList");
            // Fill the space left in the window and scroll, instead of growing past the bottom of it
            _listView.style.height = StyleKeyword.Auto;
            _listView.style.flexGrow = 1;
            _listView.style.flexShrink = 1;
            _listView.style.minHeight = 0;
            _listView.selectionChanged += objects =>
            {
                if (_listView.selectedIndex >= 0 && _listView.selectedIndex < _currentPresetsList.Count)
                {
                    _soundPreviewController.SetSound(_currentPresetsList[_listView.selectedIndex]);
                    PlaySound();
                }
            };
            // Double-click (or Enter) opens the sound in its editor
            _listView.itemsChosen += _ => LoadGenerator();
            _tagsContainer = rootVisualElement.Q("Tags");
            _generatorButtonsContainer = rootVisualElement.Q("GeneratorButtonsContainer");

            _currentTagsList.Clear();
            _tagsContainer.Clear();
            _generatorButtonsContainer.Clear();
            _tagButtons.Clear();

            _allPresets.Clear();
            _allPresets.AddRange(LoadAllAssetsOfType<AnysoundPresetObject>());
            _allPresets.AddRange(AnysoundExportedSounds.LoadAll());
            _generatorPresets.AddRange(_allPresets);

            _currentPresetsList.Clear();
            _currentPresetsList.AddRange(_allPresets);
            _generatorsList.Clear();
            foreach (var presetObject in _currentPresetsList)
            {
                if (!_generatorsList.Contains(presetObject.generatorObject))
                {
                    _generatorsList.Add(presetObject.generatorObject);
                }
            }

            CreateGeneratorButtons();

            GetTags();
            CreateTagButtons();


            _waveformContainer = rootVisualElement.Q<VisualElement>("WaveformContainer");
            _playheadContainer = rootVisualElement.Q<VisualElement>("Playhead");


            _loadSoundButton = rootVisualElement.Query<Button>("LoadButton");
            _loadSoundButton.clicked += LoadGenerator;

            _playSoundButton = rootVisualElement.Query<Button>("PlayButton");
            _playSoundButton.clicked += PlaySound;


            _searchButton = rootVisualElement.Query<Button>("SearchButton");
            _searchText = rootVisualElement.Query<TextField>("SearchText");
            _searchButton.clicked += SearchList;
            _searchText.RegisterValueChangedCallback(evt => SearchList());

            PopulateListView();
            SelectFirstSound();
        }

        // Shows the first sound in the preview without playing it
        void SelectFirstSound()
        {
            if (_currentPresetsList.Count == 0) return;
            _listView.SetSelectionWithoutNotify(new[] { 0 });
            _soundPreviewController.SetSound(_currentPresetsList[0]);
        }


        void CreateTagButtons()
        {
            foreach (var tag in _soundTags)
            {
                // Instantiate the template
                TemplateContainer tagTemplateInstance = tagTemplate.CloneTree();

                // Get the button from the template
                Button button = tagTemplateInstance.Query<Button>("SoundTagButton");
                // Configure the button
                _tagButtons.Add(button);
                button.text = tag;

                button.style.backgroundColor = ColorLowLight;
                button.style.color = GetTextColor(ColorLowLight);

                button.clicked += () =>
                {
                    ToggleTag(button.text);
                    var backgroundColor = IsTagActive(button.text) ? ColorHighLight : ColorLowLight;
                    button.style.backgroundColor = backgroundColor;
                    button.style.color = GetTextColor(backgroundColor);
                };

                // Add the instantiated template to the tags container
                _tagsContainer.Add(tagTemplateInstance);
            }
        }

        void CreateGeneratorButtons()
        {
            foreach (var generator in _generatorsList)
            {
                // Instantiate the template
                TemplateContainer generatorTemplateInstance = generatorButtonTemplate.CloneTree();

                // Get the button from the template
                Button button = generatorTemplateInstance.Query<Button>("GeneratorButton");
                // Configure the button

                button.style.backgroundImage = new StyleBackground(generator.CoverTexture);
                button.text = generator.name;
                button.style.color = Color.clear;
                button.SetEnabled(generator.isLoadable);

                button.clicked += () => { ToggleGenerator(generator); };

                // Add the instantiated template to the tags container
                _generatorButtonsContainer.Add(generatorTemplateInstance);
            }
        }

        const int MaxTagButtons = 25;

        // The most used tags first, limited to MaxTagButtons
        void GetTags()
        {
            _soundTags.Clear();
            _soundTags.AddRange(_currentPresetsList
                .Where(preset => preset.tags != null)
                .SelectMany(preset => preset.tags.Distinct())
                .GroupBy(tag => tag)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(MaxTagButtons)
                .Select(group => group.Key));
        }

        private void OnEnable()
        {
            AnysoundFootstepDSP.Setup();
        }

        private void OnDisable()
        {
            AnysoundFootstepDSP.Release();
        }

        void PlaySound()
        {
            if (_listView.selectedIndex < 0 || _listView.selectedIndex >= _currentPresetsList.Count)
            {
                Debug.LogWarning("No sound selected to play");
                return;
            }

            AnysoundPresetObject selectedPreset = _currentPresetsList[_listView.selectedIndex];
            var clip = selectedPreset.GetAudioClip();
            if (clip == null)
            {
                Debug.LogWarning($"No preview clip available for '{selectedPreset.soundName}'");
                return;
            }

            //AnysoundFootsteps.PlayClip(clip, f => { });
            AnysoundFootstepDSP.PlayClip(clip, f =>
            {
                _playheadContainer.style.visibility = new StyleEnum<Visibility>(Visibility.Visible);
                float leftPosition = (_waveformContainer.resolvedStyle.width * -1) + (_waveformContainer.resolvedStyle.width * f);
                _playheadContainer.style.left = new StyleLength(leftPosition);
                if (f >= 1)
                {
                    _playheadContainer.style.visibility = new StyleEnum<Visibility>(Visibility.Hidden);
                }
            });
        }

        void LoadGenerator()
        {
            if (_listView.selectedIndex < 0 || _listView.selectedIndex >= _currentPresetsList.Count)
            {
                Debug.LogWarning("No sound selected to load");
                return;
            }

            LoadPreset(_currentPresetsList[_listView.selectedIndex]);
        }

        // Opens the Anysound window with the generator editor for this preset, with its values loaded
        public static void OpenPreset(AnysoundPresetObject preset)
        {
            if (!preset || !preset.generatorObject)
            {
                Debug.LogWarning("This preset has no generator to load it into");
                return;
            }

            ShowSoundBrowser();
            // A newly opened window builds its GUI itself, which would replace the editor, so load once that's done
            var window = GetWindow<AnysoundBrowser>();
            EditorApplication.delayCall += () =>
            {
                if (window) window.LoadPreset(preset);
            };
        }

        void LoadPreset(AnysoundPresetObject preset)
        {
            AnysoundGeneratorWindowBase newWindow = preset.generatorObject switch
            {
                AnysoundFootstepObject settings => new AnysoundFootstepsEditorWindow(rootVisualElement, settings, preset),
                AnysoundWhooshObject settings => new AnysoundWhooshEditorWindow(rootVisualElement, settings, preset),
                AnysoundUIObject settings => new AnysoundUIEditorWindow(rootVisualElement, settings, preset),
                _ => null
            };
            if (newWindow == null)
                Debug.LogWarning($"No editor can load presets made with {preset.generatorObject.GetType().Name}");
        }

        internal bool IsTagActive(string tag)
        {
            return _currentTagsList.Contains(tag);
        }

        void SearchList()
        {
            Debug.Log("Search for " + _searchText.value);
            if (_searchText.value.Length > 2)
                PopulateListView();
            if (_searchText.value.Length == 0)
                PopulateListView();
        }


        void ToggleGenerator(AnysoundGeneratorBase generator)
        {
            if (_currentGenerator != null && _currentGenerator == generator)
                _currentGenerator = null;
            else
                _currentGenerator = generator;


            _generatorButtonsContainer.Query<Button>().ForEach((b) =>
            {
                if (_currentGenerator == null)
                {
                    b.style.unityBackgroundImageTintColor = Color.white;
                }
                else
                {
                    b.style.unityBackgroundImageTintColor = (b.text == _currentGenerator.name) ? Color.white : Color.grey;
                }
            });


            PopulateListView();
            UpdateTagsList();
        }

        void ToggleTag(string tag)
        {
            if (!_currentTagsList.Contains(tag))
                _currentTagsList.Add(tag);
            else
                _currentTagsList.Remove(tag);

            _currentPresetsList.Clear();
            PopulateListView();
            UpdateTagsList();
        }

        List<AnysoundPresetObject> FilterPresetsByGenerator(AnysoundGeneratorBase generator = null)
        {
            if (generator == null)
            {
                return _allPresets;
            }

            List<AnysoundPresetObject> filterSounds = new();
            foreach (var preset in _currentPresetsList)
            {
                if (preset.generatorObject == generator)
                {
                    filterSounds.Add(preset);
                }
            }

            return filterSounds;
        }

        List<AnysoundPresetObject> FilterPresetsByTags(List<string> currentTagList = null)
        {
            List<AnysoundPresetObject> filterSounds = new();
            if (currentTagList == null)
            {
                return _currentPresetsList;
            }

            foreach (var sound in _currentPresetsList)
            {
                bool add = true;
                foreach (var tag in currentTagList)
                {
                    if (!sound.tags.Contains(tag))
                    {
                        add = false;
                        break;
                    }
                }

                if (add)
                    filterSounds.Add(sound);
            }

            return filterSounds;
        }

        List<AnysoundPresetObject> FilterPresetsBySearchString(string searchString = "")
        {
            List<AnysoundPresetObject> filterSounds = new();
            foreach (var preset in _currentPresetsList)
            {
                if (preset.soundName.ToLower().Contains(searchString.ToLower()))
                {
                    filterSounds.Add(preset);
                }
            }

            return filterSounds;
        }


        void UpdateTagsList()
        {
            List<string> availableTags = new List<string>();
            foreach (var o in _currentPresetsList)
            {
                foreach (var tag in o.tags)
                {
                    if (!availableTags.Contains(tag))
                        availableTags.Add(tag);
                }
            }

            foreach (var tagButton in _tagButtons)
            {
                tagButton.SetEnabled(availableTags.Contains(tagButton.text));
            }
        }


        void PopulateListView()
        {
            _currentPresetsList.Clear();
            _currentPresetsList.AddRange(_allPresets);
            _currentPresetsList = FilterPresetsByGenerator(_currentGenerator);
            _currentPresetsList = FilterPresetsByTags(_currentTagsList);
            _currentPresetsList = FilterPresetsBySearchString(_searchText.value);

            // Clear existing list items
            _soundListItems.Clear();

            // Set the item source
            _listView.itemsSource = _currentPresetsList;
            _listView.fixedItemHeight = 30; // Adjust this value based on your item height
            _listView.unbindItem = null;
            _listView.bindItem = null;

            // Set the makeItem callback if it's not already set
            _listView.makeItem ??= () => soundItemTemplate.CloneTree();

            _listView.bindItem = (element, i) =>
            {
                if (i >= 0 && i < _currentPresetsList.Count)
                {
                    _soundListItems.Add(new UISoundListItemController(element, _currentPresetsList[i], tagLabelTemplate, this));
                }
            };

            // Force refresh the ListView
            _listView.Rebuild();
        }


        private static T[] LoadAllAssetsOfType<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            return guids.Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        }

        internal Color GetTextColor(Color backgroundColor)
        {
            float colorLight = backgroundColor.r + backgroundColor.g + backgroundColor.b;
            return colorLight > 2f ? Color.black : Color.white;
        }

        public static void CreatePreset(string fileName, Dictionary<string, float> presetValues, List<string> tags,
            AnysoundGeneratorBase generatorBase,
            AudioClip previewClip)
        {
            // Let the user name the preset and edit its tags before saving it
            AnysoundCreatePresetWindow.Show(fileName, tags,
                (presetName, presetTags) => SavePreset(presetName, presetValues, presetTags, generatorBase, previewClip));
        }

        static void SavePreset(string presetName, Dictionary<string, float> presetValues, List<string> tags,
            AnysoundGeneratorBase generatorBase,
            AudioClip previewClip)
        {
            // Create a new preset object
            AnysoundPresetObject preset = CreateInstance<AnysoundPresetObject>();

            preset.soundName = presetName;
            preset.SetPresetValues(presetValues);

            // Create the preset with all tags
            preset.Create(presetName, tags, generatorBase);

            // Save the preset file first to get a path
            string presetPath = EditorUtility.SaveFilePanelInProject(
                "Save Preset",
                presetName,
                "asset",
                "Save the preset as an asset file"
            );

            if (string.IsNullOrEmpty(presetPath))
                return;

            // Create and save the preset object
            AssetDatabase.CreateAsset(preset, presetPath);

            // Generate audio clip

            // Create a copy of the generated clip that can be saved as an asset
            AudioClip clipCopy = AudioClip.Create(
                "PreviewClip",
                previewClip.samples,
                previewClip.channels,
                previewClip.frequency,
                false
            );

            // Copy the audio data from the generated clip to the new clip
            float[] samples = new float[previewClip.samples * previewClip.channels];
            previewClip.GetData(samples, 0);
            clipCopy.SetData(samples, 0);

            // Name the audio clip
            clipCopy.name = "PreviewClip";

            preset.AddAudioClip(clipCopy);


            // Save the changes
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created preset at {presetPath} with embedded audio clip");

            // Select the preset in the Project window
            Selection.activeObject = preset;
        }
    }

    class UISoundPreviewController
    {
        private Button _buttonLoad;
        private Button _buttonPlay;
        private Label _labelName;
        private VisualElement _waveform;

        public UISoundPreviewController(VisualElement container)
        {
            _buttonLoad = container.Q("LoadButton").Query<Button>();
            _buttonPlay = container.Q("PlayButton").Query<Button>();
            _labelName = container.Q("SoundName").Q<Label>();
            _waveform = container.Q("WaveformContainer");
            _buttonLoad.SetEnabled(false);
            _buttonPlay.SetEnabled(false);
        }

        public void SetSound(AnysoundPresetObject soundObject)
        {
            _labelName.text = soundObject.soundName;
            if (soundObject.waveformTexture)
                _waveform.style.backgroundImage = new StyleBackground(soundObject.waveformTexture);
            _buttonLoad.SetEnabled(true);
            _buttonPlay.SetEnabled(true);
        }
    }

    class UISoundListItemController
    {
        public UISoundListItemController(VisualElement container, AnysoundPresetObject presetObject, VisualTreeAsset tagTemplate,
            AnysoundBrowser anysoundBrowser)
        {
            var label = container.Q<Label>("SoundName");
            var tagsContainer = container.Q<VisualElement>("TagsContainer");
            tagsContainer.Clear();
            label.text = presetObject.soundName;
            foreach (var tag in presetObject.tags)
            {
                var n = tagTemplate.CloneTree();
                var tagLabel = n.Q<Label>();
                tagLabel.text = tag;
                // Quiet outlined labels; a tag you filter by gets the highlight color, still without a fill
                if (anysoundBrowser.IsTagActive(tag))
                {
                    tagLabel.style.color = AnysoundBrowser.ColorHighLight;
                    tagLabel.style.borderLeftColor = tagLabel.style.borderRightColor =
                        tagLabel.style.borderTopColor = tagLabel.style.borderBottomColor = AnysoundBrowser.ColorHighLight;
                }

                tagsContainer.Add(n);
            }
        }
    }
}