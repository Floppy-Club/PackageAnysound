using UnityEditor;
using UnityEngine;
using Anysound.Shared;

[CustomPropertyDrawer(typeof(AnysoundAudioDSP.ADSREnvelopeSettings))]
public class AudioEnvelopeDrawer : PropertyDrawer
{
    private const float ENVELOPE_HEIGHT = 100f;
    private const float COMPACT_ENVELOPE_HEIGHT = 60f;
    private const float LABEL_WIDTH = 80f;
    private const float SLIDER_HEIGHT = 18f;
    private const float NUMBER_FIELD_WIDTH = 50f;
    private const float SPACING = 2f;
    private const float FOLDOUT_HEIGHT = 16f;
    
    private static bool isCompactMode = false;
    private static bool showNumberFields = true;
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = FOLDOUT_HEIGHT + SPACING; // Space for foldout
        
        if (property.isExpanded)
        {
            float envelopeHeight = isCompactMode ? COMPACT_ENVELOPE_HEIGHT : ENVELOPE_HEIGHT;
            height += envelopeHeight + SPACING;
            
            // Height for 5 parameter rows (attackTime, decayTime, sustainLevel, releaseTime, duration)
            height += 5 * SLIDER_HEIGHT + 5 * SPACING;
            
            // Space for compact mode toggle
            height += SLIDER_HEIGHT + SPACING;
        }
        
        return height;
    }
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        // Get the individual properties
        SerializedProperty attackTimeProp = property.FindPropertyRelative("attackTime");
        SerializedProperty decayTimeProp = property.FindPropertyRelative("decayTime");
        SerializedProperty sustainLevelProp = property.FindPropertyRelative("sustainLevel");
        SerializedProperty releaseTimeProp = property.FindPropertyRelative("releaseTime");
        SerializedProperty durationProp = property.FindPropertyRelative("duration");
        
        // Initialize with defaults if this appears to be a new/uninitialized instance
        if (NeedsDefaultInitialization(attackTimeProp, decayTimeProp, sustainLevelProp, releaseTimeProp, durationProp))
        {
            var defaults = AnysoundAudioDSP.ADSREnvelopeSettings.Default;
            attackTimeProp.floatValue = defaults.attackTime;
            decayTimeProp.floatValue = defaults.decayTime;
            sustainLevelProp.floatValue = defaults.sustainLevel;
            releaseTimeProp.floatValue = defaults.releaseTime;
            durationProp.floatValue = defaults.duration;
        
        // Apply the changes immediately
        property.serializedObject.ApplyModifiedProperties();
        }
        
        float currentY = position.y;
        
        // Foldout header
        Rect foldoutRect = new Rect(position.x, currentY, position.width, FOLDOUT_HEIGHT);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        currentY += FOLDOUT_HEIGHT + SPACING;
        
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }
        
        EditorGUI.indentLevel++;
        
        // Compact mode and number fields toggles
        Rect toggleRect = new Rect(position.x, currentY, position.width / 2, SLIDER_HEIGHT);
        isCompactMode = EditorGUI.Toggle(toggleRect, "Compact Mode", isCompactMode);
        
        toggleRect.x += position.width / 2;
        showNumberFields = EditorGUI.Toggle(toggleRect, "Show Numbers", showNumberFields);
        currentY += SLIDER_HEIGHT + SPACING;
        
        // Calculate envelope rect
        float envelopeHeight = isCompactMode ? COMPACT_ENVELOPE_HEIGHT : ENVELOPE_HEIGHT;
        Rect envelopeRect = new Rect(position.x, currentY, position.width, envelopeHeight);
        currentY += envelopeHeight + SPACING;
        
        // Draw the envelope visualization
        DrawEnvelopeVisualization(envelopeRect, 
            attackTimeProp?.floatValue ?? 0.01f, 
            decayTimeProp?.floatValue ?? 0.01f, 
            sustainLevelProp?.floatValue ?? 1f, 
            releaseTimeProp?.floatValue ?? 0.01f,
            durationProp?.floatValue ?? 1f);
        
        // Draw parameter controls
        currentY = DrawParameterControl(new Rect(position.x, currentY, position.width, SLIDER_HEIGHT), 
            "Attack Time", attackTimeProp, 0.001f, 2f, "s");
        currentY += SPACING;
        
        currentY = DrawParameterControl(new Rect(position.x, currentY, position.width, SLIDER_HEIGHT), 
            "Decay Time", decayTimeProp, 0.001f, 2f, "s");
        currentY += SPACING;
        
        currentY = DrawParameterControl(new Rect(position.x, currentY, position.width, SLIDER_HEIGHT), 
            "Sustain Level", sustainLevelProp, 0f, 1f, "");
        currentY += SPACING;
        
        currentY = DrawParameterControl(new Rect(position.x, currentY, position.width, SLIDER_HEIGHT), 
            "Release Time", releaseTimeProp, 0.001f, 5f, "s");
        currentY += SPACING;
        
        currentY = DrawParameterControl(new Rect(position.x, currentY, position.width, SLIDER_HEIGHT), 
            "Duration", durationProp, 0.001f, 5f, "s");
        
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
    
    private float DrawParameterControl(Rect rect, string label, SerializedProperty prop, float min, float max, string unit)
    {
        if (prop == null) return rect.y + rect.height;
        
        string displayLabel = string.IsNullOrEmpty(unit) ? label : $"{label} ({unit})";
        
        if (showNumberFields)
        {
            // Split the rect: slider gets most space, number field gets fixed width
            Rect sliderRect = new Rect(rect.x, rect.y, rect.width - NUMBER_FIELD_WIDTH - 5f, rect.height);
            Rect numberRect = new Rect(rect.x + rect.width - NUMBER_FIELD_WIDTH, rect.y, NUMBER_FIELD_WIDTH, rect.height);
            
            // Draw slider
            EditorGUI.BeginChangeCheck();
            float sliderValue = EditorGUI.Slider(sliderRect, displayLabel, prop.floatValue, min, max);
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = sliderValue;
            }
            
            // Draw number field
            EditorGUI.BeginChangeCheck();
            float numberValue = EditorGUI.FloatField(numberRect, prop.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = Mathf.Clamp(numberValue, min, max);
            }
        }
        else
        {
            // Just draw the slider using the full width
            prop.floatValue = EditorGUI.Slider(rect, displayLabel, prop.floatValue, min, max);
        }
        
        return rect.y + rect.height;
    }
    
    private void DrawEnvelopeVisualization(Rect rect, float attackTime, float decayTime, float sustainLevel, float releaseTime, float duration)
    {
        if (Event.current.type != EventType.Repaint)
            return;
            
        // Draw background
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1f));
        
        // Calculate envelope timing
        float totalVisualTime = Mathf.Max(attackTime + decayTime + releaseTime, duration + releaseTime);
        float sustainDuration = Mathf.Max(0.1f, duration - attackTime - decayTime);
        
        // Normalize times for visualization
        float attackRatio = attackTime / totalVisualTime;
        float decayRatio = decayTime / totalVisualTime;
        float sustainRatio = sustainDuration / totalVisualTime;
        float releaseRatio = releaseTime / totalVisualTime;
        
        // Calculate envelope points
        Vector3[] points = new Vector3[]
        {
            // Start point
            new Vector3(rect.x, rect.y + rect.height, 0),
            
            // Attack peak
            new Vector3(rect.x + rect.width * attackRatio, rect.y, 0),
            
            // Decay to sustain
            new Vector3(rect.x + rect.width * (attackRatio + decayRatio), rect.y + rect.height * (1f - sustainLevel), 0),
            
            // Sustain level (end of sustain duration)
            new Vector3(rect.x + rect.width * (attackRatio + decayRatio + sustainRatio), rect.y + rect.height * (1f - sustainLevel), 0),
            
            // Release to zero
            new Vector3(rect.x + rect.width, rect.y + rect.height, 0)
        };
        
        // Draw the envelope curve
        Handles.color = Color.cyan;
        Handles.DrawAAPolyLine(2f, points);
        
        // Draw grid lines (fewer in compact mode)
        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        
        int gridLines = isCompactMode ? 2 : 4;
        
        // Vertical grid lines
        for (int i = 1; i <= gridLines; i++)
        {
            float x = rect.x + (rect.width * i / (gridLines + 1));
            Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.y + rect.height));
        }
        
        // Horizontal grid lines
        for (int i = 1; i <= gridLines; i++)
        {
            float y = rect.y + (rect.height * i / (gridLines + 1));
            Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.x + rect.width, y));
        }
        
        // Draw labels (only in non-compact mode or if there's enough space)
        if (!isCompactMode || rect.height > 80f)
        {
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, 50, 16), "1.0", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 2, rect.y + rect.height - 16, 50, 16), "0.0", EditorStyles.miniLabel);
            
            // Draw phase labels
            float labelY = rect.y + rect.height + 2;
            if (attackTime > 0.01f)
                GUI.Label(new Rect(rect.x + rect.width * (attackRatio * 0.5f) - 8, labelY, 16, 16), "A", EditorStyles.centeredGreyMiniLabel);
            if (decayTime > 0.01f)
                GUI.Label(new Rect(rect.x + rect.width * (attackRatio + decayRatio * 0.5f) - 8, labelY, 16, 16), "D", EditorStyles.centeredGreyMiniLabel);
            GUI.Label(new Rect(rect.x + rect.width * (attackRatio + decayRatio + sustainRatio * 0.5f) - 8, labelY, 16, 16), "S", EditorStyles.centeredGreyMiniLabel);
            if (releaseTime > 0.01f)
                GUI.Label(new Rect(rect.x + rect.width * (1f - releaseRatio * 0.5f) - 8, labelY, 16, 16), "R", EditorStyles.centeredGreyMiniLabel);
        }
        
        // Draw timing indicators in compact mode
        if (isCompactMode && rect.height <= 80f)
        {
            // Show total envelope time in the corner
            GUI.Label(new Rect(rect.x + rect.width - 50, rect.y + 2, 50, 16), 
                $"{totalVisualTime:F2}s", EditorStyles.miniLabel);
        }
        
        // Draw duration marker
        if (duration > attackTime + decayTime)
        {
            float durationEndX = rect.x + rect.width * ((attackTime + decayTime + sustainDuration) / totalVisualTime);
            Handles.color = new Color(1f, 0.8f, 0f, 0.8f); // Yellow-orange color
            Handles.DrawLine(
                new Vector3(durationEndX, rect.y),
                new Vector3(durationEndX, rect.y + rect.height)
            );
            
            // Duration label
            if (!isCompactMode)
            {
                GUI.Label(new Rect(durationEndX - 15, rect.y + rect.height + 2, 30, 16), 
                    "D", EditorStyles.centeredGreyMiniLabel);
            }
        }
        
        // Draw keyframe points for better visualization
        Handles.color = Color.white;
        float pointSize = isCompactMode ? 2f : 3f;
        
        foreach (Vector3 point in points)
        {
            if (point.x >= rect.x && point.x <= rect.x + rect.width &&
                point.y >= rect.y && point.y <= rect.y + rect.height)
            {
                Handles.DrawSolidDisc(point, Vector3.forward, pointSize);
            }
        }
    }

    private bool NeedsDefaultInitialization(SerializedProperty attackTime, SerializedProperty decayTime,
        SerializedProperty sustainLevel, SerializedProperty releaseTime, SerializedProperty duration)
    {
        // Check if all values are exactly zero (indicating uninitialized struct)
        // or if the values are clearly invalid (like having 0 duration)
        return (attackTime?.floatValue ?? 0f) == 0f &&
               (decayTime?.floatValue ?? 0f) == 0f &&
               (sustainLevel?.floatValue ?? 0f) == 0f &&
               (releaseTime?.floatValue ?? 0f) == 0f &&
               (duration?.floatValue ?? 0f) == 0f;
    }

}

