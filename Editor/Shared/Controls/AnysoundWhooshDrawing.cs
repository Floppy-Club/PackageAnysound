using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// An abstract whoosh drawn as vectors in the style of the other icons: the amplitude envelope mirrored around a center
/// line, filled with nested streams of air. Loud parts swell out, quiet parts pinch together.
/// The length along the center line is the duration (centered when short), the thickness is the size,
/// and fluctuation makes the streams wobble
/// </summary>
#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
[UxmlElement]
#endif
public partial class AnysoundWhooshDrawing : VisualElement
{
#if !(UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER)
    public new class UxmlFactory : UnityEngine.UIElements.UxmlFactory<AnysoundWhooshDrawing, UxmlTraits>
    {
    }
#endif

    const float LineWidth = 1.5f;
    const int SmoothingPasses = 2;

    // The streams inside the outline, as a part of the outline's height. The outline itself is 1
    static readonly float[] Streams = { 1f, 0.66f, 0.33f };

    // How far the streams can wobble at full fluctuation, as a part of the half height
    const float MaxWobble = 0.5f;

    Color _color = new(0.376f, 0.78f, 0.36f);
    readonly List<float> _amplitude = new();
    readonly List<float> _wobble = new();
    float _length = 1f;
    float _thickness = 1f;

#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
    [UxmlAttribute]
#endif
    public Color color
    {
        get => _color;
        set
        {
            _color = value;
            MarkDirtyRepaint();
        }
    }

    public AnysoundWhooshDrawing()
    {
        AddToClassList("anysound-whoosh-drawing");
        pickingMode = PickingMode.Ignore;
        generateVisualContent += OnGenerateVisualContent;
    }

    /// <param name="amplitude">The amplitude over the length of the sound (0-1), evenly spaced in time</param>
    /// <param name="wobble">The sideways movement of the streams (-1 to 1) at the same times. Empty for none</param>
    /// <param name="length">The part of the width the sound takes up (0-1), e.g. its duration relative to the longest duration</param>
    /// <param name="thickness">The part of the height the loudest part takes up (0-1), e.g. its size</param>
    public void SetWhoosh(IEnumerable<float> amplitude, IEnumerable<float> wobble, float length, float thickness = 1f)
    {
        _amplitude.Clear();
        _amplitude.AddRange(amplitude);
        Smooth(_amplitude, SmoothingPasses);
        _wobble.Clear();
        if (wobble != null) _wobble.AddRange(wobble);
        _length = Mathf.Clamp01(length);
        _thickness = Mathf.Clamp01(thickness);
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        Rect rect = contentRect;
        if (rect.width <= 0 || rect.height <= 0 || _amplitude.Count < 2) return;

        float halfHeight = (rect.height * 0.5f - LineWidth) * Mathf.Max(_thickness, 0.05f);
        float centerY = rect.y + rect.height * 0.5f;
        float left = rect.x + LineWidth, width = rect.width - LineWidth * 2f;
        float shapeWidth = width * Mathf.Max(_length, 0.05f);
        float shapeLeft = left + (width - shapeWidth) * 0.5f;

        var painter = mgc.painter2D;
        painter.lineWidth = LineWidth;
        painter.lineJoin = LineJoin.Round;
        painter.lineCap = LineCap.Round;

        // The quiet center line, through the whole sound
        painter.strokeColor = Dim(0.35f);
        painter.BeginPath();
        painter.MoveTo(new Vector2(left, centerY));
        painter.LineTo(new Vector2(left + width, centerY));
        painter.Stroke();

        // Inner streams first, so the outline is drawn on top
        for (int s = Streams.Length - 1; s >= 0; s--)
        {
            painter.strokeColor = s == 0 ? _color : Dim(1f - s * 0.3f);
            foreach (float side in new[] { -1f, 1f })
            {
                painter.BeginPath();
                for (int i = 0; i < _amplitude.Count; i++)
                {
                    float x = shapeLeft + shapeWidth * i / (_amplitude.Count - 1);
                    // The whole stream wobbles together, pinched where the sound is quiet
                    float wobble = i < _wobble.Count ? _wobble[i] * _amplitude[i] * MaxWobble * halfHeight : 0f;
                    float y = centerY + side * _amplitude[i] * Streams[s] * halfHeight * (1f - MaxWobble * 0.5f) + wobble;
                    if (i == 0) painter.MoveTo(new Vector2(x, y));
                    else painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }

    Color Dim(float amount)
    {
        Color c = _color * Mathf.Clamp01(amount);
        c.a = 1f;
        return c;
    }

    // Rounds the corners of the envelope, so it reads as a soft stream of air instead of straight segments
    static void Smooth(List<float> values, int passes)
    {
        if (values.Count < 3) return;
        var copy = new float[values.Count];
        for (int pass = 0; pass < passes; pass++)
        {
            values.CopyTo(copy);
            for (int i = 1; i < values.Count - 1; i++)
                values[i] = (copy[i - 1] + copy[i] * 2f + copy[i + 1]) * 0.25f;
        }
    }
}
