using UnityEditor;
using UnityEngine;

namespace Anysound
{
    [CustomPropertyDrawer(typeof(AnysoundCurve))]
    public class AnysoundCurveDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Begin property drawing
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Don't make child fields be indented
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // Calculate rects for property fields
            var curveRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Get the curve property (assuming AnysoundCurve has a 'curve' property of type AnimationCurve)
            var curveProp = property.FindPropertyRelative("curve");

            EditorGUI.PropertyField(curveRect, curveProp, GUIContent.none);

            

            // Add preset buttons
            var presetsLabelRect = new Rect(position.x,
                position.y + (EditorGUIUtility.singleLineHeight + 2),
                position.width,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(presetsLabelRect, "Presets:");

            // Calculate button widths and positions
            float buttonWidth = position.width / 2 - 5;
            var preset1Rect = new Rect(position.x,
                presetsLabelRect.y + EditorGUIUtility.singleLineHeight + 2,
                buttonWidth,
                EditorGUIUtility.singleLineHeight);

            var preset2Rect = new Rect(position.x + buttonWidth + 10,
                presetsLabelRect.y + EditorGUIUtility.singleLineHeight + 2,
                buttonWidth,
                EditorGUIUtility.singleLineHeight);

            // Draw preset buttons
            if (GUI.Button(preset1Rect, "Init"))
            {
                // Apply preset 1 (Linear curve)
                if (curveProp != null)
                {
                    Undo.RecordObject(property.serializedObject.targetObject, "Apply Init Curve Preset");

                    //AnimationCurve linearCurve = AnimationCurve.Linear(0, 0, 1, 1);
                    AnimationCurve linearCurve = new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(0.01f, 1f, 0, 0),
                        new Keyframe(0.99f, 1, 0, 0),
                        new Keyframe(1, 0, 0, 0)
                    );
                    curveProp.animationCurveValue = linearCurve;


                    property.serializedObject.ApplyModifiedProperties();
                }
            }

            if (GUI.Button(preset2Rect, "Classic"))
            {
                // Apply preset 2 (Ease In/Out curve)
                if (curveProp != null)
                {
                    Undo.RecordObject(property.serializedObject.targetObject, "Apply Classic Curve Preset");

                    AnimationCurve classicCurve = new AnimationCurve(
                        new Keyframe(0, 0, 0, 20),
                        new Keyframe(0.125f, 1f, 0f, 0f),
                        new Keyframe(0.25f, 0.5f, 0, 0f),
                        new Keyframe(1, 0, 0, 0)
                    );



                    curveProp.animationCurveValue = classicCurve;
                
                    property.serializedObject.ApplyModifiedProperties();
                }
            }

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            // End property drawing
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Return the height needed for this property
            int lineCount = 1; // Start with 1 for the curve

            // Check if you have additional properties


            // Add lines for presets label and buttons
            lineCount += 2;

            return EditorGUIUtility.singleLineHeight * lineCount + (lineCount - 1) * 2f;
        }
    }
}