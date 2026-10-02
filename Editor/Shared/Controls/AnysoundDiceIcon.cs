using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A die (five face) drawn as vectors, used on the random buttons. Drawn in the inherited text color,
/// so it follows the color set on its button
/// </summary>
#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
[UxmlElement]
#endif
public partial class AnysoundDiceIcon : VisualElement
{
#if !(UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER)
    public new class UxmlFactory : UnityEngine.UIElements.UxmlFactory<AnysoundDiceIcon, UxmlTraits>
    {
    }
#endif

    // Drawn in a 20x20 frame, scaled to fit the element
    const float Size = 20f;
    const float CornerRadius = 4f;
    const float PipRadius = 1.6f;
    const float LineWidth = 1.4f;

    static readonly Vector2[] Pips =
    {
        new(6f, 6f), new(14f, 6f),
        new(10f, 10f),
        new(6f, 14f), new(14f, 14f),
    };

    public AnysoundDiceIcon()
    {
        AddToClassList("anysound-dice-icon");
        pickingMode = PickingMode.Ignore;
        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
    }

    void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        Rect rect = contentRect;
        if (rect.width <= 0 || rect.height <= 0) return;

        float scale = Mathf.Min(rect.width, rect.height) / Size;
        Vector2 origin = new(rect.x + (rect.width - Size * scale) * 0.5f, rect.y + (rect.height - Size * scale) * 0.5f);
        Vector2 T(float x, float y) => origin + new Vector2(x, y) * scale;

        var painter = mgc.painter2D;
        Color color = resolvedStyle.color;
        painter.strokeColor = color;
        painter.fillColor = color;
        painter.lineWidth = LineWidth * scale;
        painter.lineJoin = LineJoin.Round;

        // Rounded square, inset by half the line width so the stroke stays inside the frame
        float inset = LineWidth * 0.5f;
        float min = inset, max = Size - inset, r = CornerRadius * scale;
        painter.BeginPath();
        painter.MoveTo(T(min + CornerRadius, min));
        painter.ArcTo(T(max, min), T(max, max), r);
        painter.ArcTo(T(max, max), T(min, max), r);
        painter.ArcTo(T(min, max), T(min, min), r);
        painter.ArcTo(T(min, min), T(max, min), r);
        painter.ClosePath();
        painter.Stroke();

        foreach (var pip in Pips)
        {
            painter.BeginPath();
            painter.Arc(T(pip.x, pip.y), PipRadius * scale, Angle.Degrees(0), Angle.Degrees(360));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
