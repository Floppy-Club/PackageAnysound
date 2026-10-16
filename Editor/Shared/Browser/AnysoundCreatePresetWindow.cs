using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared
{
    // Lets the user name a new preset and edit its tags before it is saved
    public class AnysoundCreatePresetWindow : EditorWindow
    {
        string _presetName;
        List<string> _tags = new();
        Action<string, List<string>> _onCreate;

        TextField _nameField;
        TextField _tagField;
        VisualElement _tagsContainer;
        VisualElement _suggestionsContainer;
        Button _createButton;

        public static void Show(string suggestedName, List<string> suggestedTags, Action<string, List<string>> onCreate)
        {
            var window = CreateInstance<AnysoundCreatePresetWindow>();
            window.titleContent = new GUIContent("Create preset");
            window._presetName = suggestedName;
            window._tags = suggestedTags?.Select(NormalizeTag).Where(t => t.Length > 0).Distinct().ToList() ?? new List<string>();
            window._onCreate = onCreate;
            window.minSize = new Vector2(380, 300);
            window.ShowUtility();
            window.BuildGUI();
        }

        void BuildGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.floppyclub.anysound/Editor/Shared/uss/AnysoundStyles.uss");
            if (styles != null) root.styleSheets.Add(styles);
            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = root.style.paddingBottom = 12;

            root.Add(new Label("Name") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });
            _nameField = new TextField { value = _presetName };
            _nameField.RegisterValueChangedCallback(evt =>
            {
                _presetName = evt.newValue;
                UpdateCreateButton();
            });
            root.Add(_nameField);

            root.Add(new Label("Tags") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 12, marginBottom = 4 } });
            _tagsContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            root.Add(_tagsContainer);

            var addRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            _tagField = new TextField { style = { flexGrow = 1 }, tooltip = "Separate several tags with commas" };
            // Enter adds the tag instead of leaving the field
            _tagField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                AddTagsFromField();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            addRow.Add(_tagField);
            addRow.Add(new Button(AddTagsFromField) { text = "Add tag" });
            root.Add(addRow);

            root.Add(new Label("Existing tags (click to add)") { style = { marginTop = 12, marginBottom = 4, opacity = 0.7f } });
            var suggestionsScroll = new ScrollView { style = { flexGrow = 1, minHeight = 40 } };
            _suggestionsContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            suggestionsScroll.Add(_suggestionsContainer);
            root.Add(suggestionsScroll);

            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 12 } };
            buttonRow.Add(new Button(Close) { text = "Cancel" });
            _createButton = new Button(CreatePreset) { text = "Create preset" };
            buttonRow.Add(_createButton);
            root.Add(buttonRow);

            RefreshTags();
            UpdateCreateButton();
            _nameField.Focus();
        }

        void AddTagsFromField()
        {
            foreach (var tag in _tagField.value.Split(','))
                AddTag(tag);
            _tagField.SetValueWithoutNotify("");
            _tagField.Focus();
        }

        void AddTag(string tag)
        {
            tag = NormalizeTag(tag);
            if (tag.Length == 0 || _tags.Contains(tag)) return;
            _tags.Add(tag);
            RefreshTags();
        }

        void RemoveTag(string tag)
        {
            _tags.Remove(tag);
            RefreshTags();
        }

        void RefreshTags()
        {
            _tagsContainer.Clear();
            if (_tags.Count == 0)
                _tagsContainer.Add(new Label("No tags") { style = { opacity = 0.5f } });
            foreach (var tag in _tags)
            {
                var chip = new Button(() => RemoveTag(tag)) { text = $"{tag}  ×", tooltip = "Remove tag" };
                _tagsContainer.Add(chip);
            }

            _suggestionsContainer.Clear();
            foreach (var tag in GetExistingTags().Where(t => !_tags.Contains(t)))
            {
                var suggestion = new Button(() => AddTag(tag)) { text = tag, style = { opacity = 0.7f } };
                _suggestionsContainer.Add(suggestion);
            }
        }

        void UpdateCreateButton() => _createButton?.SetEnabled(!string.IsNullOrWhiteSpace(_presetName));

        void CreatePreset()
        {
            if (string.IsNullOrWhiteSpace(_presetName)) return;
            // Include a tag that was typed but not added yet
            if (!string.IsNullOrWhiteSpace(_tagField.value)) AddTagsFromField();

            var onCreate = _onCreate;
            var presetName = _presetName.Trim();
            var tags = new List<string>(_tags);
            Close();
            onCreate?.Invoke(presetName, tags);
        }

        static string NormalizeTag(string tag) => tag?.Trim().ToLower() ?? "";

        static IEnumerable<string> GetExistingTags() =>
            AssetDatabase.FindAssets($"t:{nameof(AnysoundPresetObject)}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<AnysoundPresetObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(preset => preset != null && preset.tags != null)
                .SelectMany(preset => preset.tags)
                .Select(NormalizeTag)
                .Where(tag => tag.Length > 0)
                .Distinct()
                .OrderBy(tag => tag);
    }
}
