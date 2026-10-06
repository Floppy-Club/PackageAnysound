using UnityEditor;
using UnityEngine;

namespace Anysound.Shared.Generators.UI.Backend
{
    /// <summary>
    /// Draws an action step as one min/max row per parameter: [min] [----slider----] [max]
    /// </summary>
    [CustomPropertyDrawer(typeof(AnysoundUIObject.UIActionStep))]
    public class UIActionStepDrawer : PropertyDrawer
    {
        const int Rows = 4;
        const float FieldWidth = 50f;
        const float Spacing = 4f;

        // The range slider being dragged in whole numbers, with its unrounded values
        static string s_DragPath;
        static float s_DragMin, s_DragMax;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return line * (Rows + 1);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(row, label, EditorStyles.boldLabel);

            row.y += line;
            DrawRange(row, property, "Delay", "delayMin", "delayMax", 0f, AnysoundUIObject.UIActionStep.MaxDelay);
            row.y += line;
            bool notes = IsNotesMode(property);
            DrawRange(row, property, notes ? "Pitch (notes)" : "Pitch", "pitchMin", "pitchMax", -AnysoundUIObject.UIActionStep.MaxPitch,
                AnysoundUIObject.UIActionStep.MaxPitch, notes);
            row.y += line;
            DrawRange(row, property, "Volume", "volumeMin", "volumeMax", 0f, 1f);
            row.y += line;
            DrawRange(row, property, "Duration", "durationMin", "durationMax", 0f, AnysoundUIObject.UIActionStep.MaxDuration);

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// The pitch mode lives on the action owning the step ("actions.Array.data[i].steps.Array.data[j]")
        /// </summary>
        static bool IsNotesMode(SerializedProperty stepProperty)
        {
            string path = stepProperty.propertyPath;
            int stepsIndex = path.LastIndexOf(".steps.Array.data[", System.StringComparison.Ordinal);
            if (stepsIndex < 0) return false;
            var pitchMode = stepProperty.serializedObject.FindProperty(path.Substring(0, stepsIndex) + ".pitchMode");
            return pitchMode != null && pitchMode.enumValueIndex == (int)AnysoundUIObject.UIPitchMode.Notes;
        }

        /// <param name="wholeNumbers">Snaps min and max to integers (pitch in notes mode)</param>
        static void DrawRange(Rect row, SerializedProperty property, string name, string minName, string maxName, float lowLimit,
            float highLimit, bool wholeNumbers = false)
        {
            var minProperty = property.FindPropertyRelative(minName);
            var maxProperty = property.FindPropertyRelative(maxName);

            using var indent = new EditorGUI.IndentLevelScope();
            Rect content = EditorGUI.PrefixLabel(row, new GUIContent(name, minProperty.tooltip));

            // Prefix label already indents, the fields below should not indent again
            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var minRect = new Rect(content.x, content.y, FieldWidth, content.height);
            var maxRect = new Rect(content.xMax - FieldWidth, content.y, FieldWidth, content.height);
            var sliderRect = new Rect(minRect.xMax + Spacing, content.y, maxRect.x - minRect.xMax - Spacing * 2, content.height);

            float oldMin = wholeNumbers ? RoundHalfUp(minProperty.floatValue) : minProperty.floatValue;
            float oldMax = wholeNumbers ? RoundHalfUp(maxProperty.floatValue) : maxProperty.floatValue;
            float min = oldMin;
            float max = oldMax;
            bool changed = false;
            bool minMoved = false;

            EditorGUI.BeginChangeCheck();
            float fieldMin = wholeNumbers ? EditorGUI.IntField(minRect, (int)min) : EditorGUI.FloatField(minRect, min);
            if (EditorGUI.EndChangeCheck())
            {
                min = fieldMin;
                changed = minMoved = true;
            }

            // A new drag (or none) starts from the stored values. While dragging in whole numbers the slider is fed its own
            // unrounded values, so small mouse moves add up instead of being rounded away every event
            string path = minProperty.propertyPath;
            if (GUIUtility.hotControl == 0 && s_DragPath == path)
                s_DragPath = null;
            bool dragging = wholeNumbers && s_DragPath == path;
            float sliderMin = dragging ? s_DragMin : min;
            float sliderMax = dragging ? s_DragMax : max;
            float startSliderMin = sliderMin;
            float startSliderMax = sliderMax;

            EditorGUI.BeginChangeCheck();
            EditorGUI.MinMaxSlider(sliderRect, ref sliderMin, ref sliderMax, lowLimit, highLimit);
            if (EditorGUI.EndChangeCheck())
            {
                changed = true;
                minMoved = !Mathf.Approximately(sliderMin, startSliderMin);
                if (wholeNumbers)
                {
                    s_DragPath = path;
                    s_DragMin = sliderMin;
                    s_DragMax = sliderMax;

                    bool movedBoth = minMoved && !Mathf.Approximately(sliderMax, startSliderMax);
                    if (movedBoth)
                    {
                        // Dragging the whole range: keep the width, only the position snaps
                        float width = max - min;
                        min = Mathf.Clamp(RoundHalfUp(sliderMin), lowLimit, highLimit - width);
                        max = min + width;
                    }
                    else
                    {
                        min = RoundHalfUp(sliderMin);
                        max = RoundHalfUp(sliderMax);
                    }
                }
                else
                {
                    min = sliderMin;
                    max = sliderMax;
                }
            }

            EditorGUI.BeginChangeCheck();
            float fieldMax = wholeNumbers ? EditorGUI.IntField(maxRect, (int)max) : EditorGUI.FloatField(maxRect, max);
            if (EditorGUI.EndChangeCheck())
            {
                max = fieldMax;
                changed = true;
            }

            if (changed)
            {
                min = Mathf.Clamp(min, lowLimit, highLimit);
                max = Mathf.Clamp(max, lowLimit, highLimit);
                // Moving one end past the other drags the other end along
                if (min > max)
                {
                    if (minMoved) max = min;
                    else min = max;
                }

                minProperty.floatValue = min;
                maxProperty.floatValue = max;
            }

            EditorGUI.indentLevel = oldIndent;
        }

        // Mathf.Round rounds .5 to the nearest even number, which makes the slider step unevenly
        static float RoundHalfUp(float value) => Mathf.Floor(value + 0.5f);
    }
}
