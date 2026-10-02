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
            DrawRange(row, property, "Pitch", "pitchMin", "pitchMax", -AnysoundUIObject.UIActionStep.MaxPitch,
                AnysoundUIObject.UIActionStep.MaxPitch);
            row.y += line;
            DrawRange(row, property, "Volume", "volumeMin", "volumeMax", 0f, 1f);
            row.y += line;
            DrawRange(row, property, "Duration", "durationMin", "durationMax", 0f, AnysoundUIObject.UIActionStep.MaxDuration);

            EditorGUI.EndProperty();
        }

        static void DrawRange(Rect row, SerializedProperty property, string name, string minName, string maxName, float lowLimit,
            float highLimit)
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

            float min = minProperty.floatValue;
            float max = maxProperty.floatValue;

            EditorGUI.BeginChangeCheck();
            min = EditorGUI.FloatField(minRect, min);
            EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, lowLimit, highLimit);
            max = EditorGUI.FloatField(maxRect, max);
            if (EditorGUI.EndChangeCheck())
            {
                min = Mathf.Clamp(min, lowLimit, highLimit);
                max = Mathf.Clamp(max, lowLimit, highLimit);
                // Moving one end past the other drags the other end along
                if (min > max)
                {
                    if (!Mathf.Approximately(min, minProperty.floatValue)) max = min;
                    else min = max;
                }

                minProperty.floatValue = min;
                maxProperty.floatValue = max;
            }

            EditorGUI.indentLevel = oldIndent;
        }
    }
}
