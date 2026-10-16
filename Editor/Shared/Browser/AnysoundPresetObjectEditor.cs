using Anysound.Shared.Generators.Footsteps;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Browser
{
    [CustomEditor(typeof(AnysoundPresetObject))]
    public class AnysoundPresetObjectEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var preset = (AnysoundPresetObject)target;
            var root = new VisualElement();

            var nameField = new TextField("Name") { value = preset.soundName, isReadOnly = true };
            root.Add(nameField);

            var tagsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 4 } };
            tagsRow.Add(new Label("Tags") { style = { minWidth = 120, unityTextAlign = TextAnchor.MiddleLeft } });
            if (preset.tags == null || preset.tags.Count == 0)
                tagsRow.Add(new Label("No tags") { style = { opacity = 0.5f } });
            else
                foreach (var tag in preset.tags)
                    tagsRow.Add(new Label(tag)
                    {
                        style =
                        {
                            backgroundColor = new Color(0.325f, 0.325f, 0.325f),
                            paddingLeft = 6, paddingRight = 6, paddingTop = 2, paddingBottom = 2,
                            marginRight = 4, marginBottom = 4,
                            borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                        }
                    });
            root.Add(tagsRow);

            // Same layout as the browser's preview: preview button, waveform, load button
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.floppyclub.anysound/Editor/Shared/uss/AnysoundStyles.uss");
            if (styles != null) root.styleSheets.Add(styles);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexEnd, marginTop = 8 } };

            var waveform = new VisualElement
            {
                style =
                {
                    flexGrow = 1, height = ButtonSize, marginLeft = 8, marginRight = 8,
                    backgroundColor = Color.black,
                }
            };
            if (preset.waveformTexture) waveform.style.backgroundImage = new StyleBackground(preset.waveformTexture);
            var playhead = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, top = 0, bottom = 0, width = 2,
                    backgroundColor = new Color(0.957f, 0.251f, 0.306f),
                    visibility = Visibility.Hidden,
                }
            };
            waveform.Add(playhead);

            row.Add(CreateIconButton("Preview", "button_preview", "Play the sound saved with this preset", () => PlayPreview(preset, playhead)));
            row.Add(waveform);
            var load = CreateIconButton("Load", "button_load", "Open the generator editor with this preset's values", () => AnysoundBrowser.OpenPreset(preset));
            load.Q<Button>().SetEnabled(preset.generatorObject);
            row.Add(load);
            root.Add(row);

            return root;
        }

        const float ButtonSize = 100;

        // A labelled icon button, like the ones in the generator editors and the browser
        static VisualElement CreateIconButton(string label, string icon, string tooltip, System.Action onClick)
        {
            var container = new VisualElement { style = { flexShrink = 0 } };
            container.AddToClassList("anysound-button");
            container.Add(new Label(label) { style = { height = 30, unityTextAlign = TextAnchor.MiddleLeft } });

            var button = new Button(onClick)
            {
                tooltip = tooltip,
                style =
                {
                    width = ButtonSize, height = ButtonSize,
                    marginLeft = 0, marginRight = 0, marginTop = 0, marginBottom = 0,
                    backgroundImage = Resources.Load<Texture2D>(icon),
                    backgroundSize = new BackgroundSize(80, 80),
                    borderTopLeftRadius = 0, borderTopRightRadius = 0, borderBottomLeftRadius = 0, borderBottomRightRadius = 0,
                    borderLeftWidth = 1, borderRightWidth = 1, borderTopWidth = 1, borderBottomWidth = 1,
                    borderLeftColor = ButtonBorder, borderRightColor = ButtonBorder, borderTopColor = ButtonBorder, borderBottomColor = ButtonBorder,
                }
            };
            button.AddToClassList("anysound-button-box");
            container.Add(button);
            return container;
        }

        static readonly Color ButtonBorder = new(142 / 255f, 142 / 255f, 142 / 255f);

        static void PlayPreview(AnysoundPresetObject preset, VisualElement playhead)
        {
            var clip = preset.GetAudioClip();
            if (!clip) return;

            AnysoundFootstepDSP.PlayClip(clip, progress =>
            {
                if (playhead == null) return;
                playhead.style.visibility = progress >= 1f ? Visibility.Hidden : Visibility.Visible;
                playhead.style.left = Length.Percent(Mathf.Clamp01(progress) * 100f);
            });
        }
    }
}
