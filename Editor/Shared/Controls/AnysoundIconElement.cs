using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Built-in icons drawn as vectors, in the style of the footstep surface icons (56x32 frame with line art)
/// </summary>
public enum AnysoundIcon
{
    Auto,
    Generic,
    Wood,
    Plastic,
    Paper,
    Bubbles,
    EightBit,
    Abstract,
    Glass,
    Metal,
    Stone,
    Water,
    Synth,
    None,
    // Appended to keep the serialized values of the icons above
    Dirt,
    Mouth,
    Crunch,
    Pebbles,
    Leaves,
    Ice,
    Lever,
    Bottle,
    Stick,
    Electricity,
    Button,
}

public enum AnysoundIconState
{
    Normal,
    Hover,
    Selected,
}

#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
[UxmlElement]
#endif
public partial class AnysoundIconElement : VisualElement
{
#if !(UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER)
    public new class UxmlFactory : UnityEngine.UIElements.UxmlFactory<AnysoundIconElement, UxmlTraits>
    {
    }
#endif

    public const float IconWidth = 56f;
    public const float IconHeight = 32f;
    static readonly Color SelectedStrokeColor = new(0.141f, 0.141f, 0.141f);

    AnysoundIcon _icon = AnysoundIcon.Generic;
    AnysoundIconState _state;
    Color _color = new(0.376f, 0.78f, 0.36f);
    Texture2D _customIcon, _customIconSelected;

#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
    [UxmlAttribute]
#endif
    public AnysoundIcon icon
    {
        get => _icon;
        set
        {
            _icon = value;
            Refresh();
        }
    }

#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
    [UxmlAttribute]
#endif
    public Color color
    {
        get => _color;
        set
        {
            _color = value;
            Refresh();
        }
    }

    public AnysoundIconState state
    {
        get => _state;
        set
        {
            _state = value;
            Refresh();
        }
    }

    /// <summary>
    /// Optional textures replacing the built-in vector icon (like the footstep surface_x / surface_x_fill pngs)
    /// </summary>
    public void SetCustomIcon(Texture2D normal, Texture2D selected)
    {
        _customIcon = normal;
        _customIconSelected = selected;
        Refresh();
    }

    public AnysoundIconElement()
    {
        AddToClassList("anysound-icon");
        pickingMode = PickingMode.Ignore;
        generateVisualContent += OnGenerateVisualContent;
    }

    void Refresh()
    {
        if (_customIcon)
        {
            var texture = _state == AnysoundIconState.Selected && _customIconSelected ? _customIconSelected : _customIcon;
            style.backgroundImage = new StyleBackground(texture);
        }
        else
        {
            style.backgroundImage = StyleKeyword.None;
        }

        MarkDirtyRepaint();
    }

    /// <summary>
    /// Guesses an icon from a name, e.g. "Wood" or "8-bit" (used when a material's icon is set to Auto)
    /// </summary>
    public static AnysoundIcon FromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return AnysoundIcon.Generic;
        string n = name.ToLowerInvariant();
        if (n == "none") return AnysoundIcon.None;
        if (n.Contains("wood")) return AnysoundIcon.Wood;
        if (n.Contains("dirt") || n.Contains("soil")) return AnysoundIcon.Dirt;
        if (n.Contains("mouth") || n.Contains("lip")) return AnysoundIcon.Mouth;
        if (n.Contains("crunch")) return AnysoundIcon.Crunch;
        if (n.Contains("pebble") || n.Contains("gravel")) return AnysoundIcon.Pebbles;
        if (n.Contains("leaf") || n.Contains("leaves")) return AnysoundIcon.Leaves;
        if (n.Contains("ice")) return AnysoundIcon.Ice;
        if (n.Contains("lever") || n.Contains("switch")) return AnysoundIcon.Lever;
        if (n.Contains("bottle")) return AnysoundIcon.Bottle;
        if (n.Contains("stick") || n.Contains("twig") || n.Contains("branch")) return AnysoundIcon.Stick;
        if (n.Contains("electr") || n.Contains("spark") || n.Contains("zap")) return AnysoundIcon.Electricity;
        if (n.Contains("click") || n.Contains("button") || n.Contains("key")) return AnysoundIcon.Button;
        if (n.Contains("plast")) return AnysoundIcon.Plastic;
        if (n.Contains("paper") || n.Contains("card")) return AnysoundIcon.Paper;
        if (n.Contains("bubbl")) return AnysoundIcon.Bubbles;
        if (n.Contains("8") || n.Contains("bit") || n.Contains("retro") || n.Contains("chip")) return AnysoundIcon.EightBit;
        if (n.Contains("abstr")) return AnysoundIcon.Abstract;
        if (n.Contains("glass")) return AnysoundIcon.Glass;
        if (n.Contains("metal") || n.Contains("steel") || n.Contains("iron")) return AnysoundIcon.Metal;
        if (n.Contains("stone") || n.Contains("rock")) return AnysoundIcon.Stone;
        if (n.Contains("water") || n.Contains("liquid")) return AnysoundIcon.Water;
        if (n.Contains("synth") || n.Contains("digital") || n.Contains("sine")) return AnysoundIcon.Synth;
        return AnysoundIcon.Generic;
    }

    void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (_customIcon) return;

        Rect rect = contentRect;
        if (rect.width <= 0 || rect.height <= 0) return;

        float scale = Mathf.Min(rect.width / IconWidth, rect.height / IconHeight);
        Vector2 origin = new(
            rect.x + (rect.width - IconWidth * scale) * 0.5f,
            rect.y + (rect.height - IconHeight * scale) * 0.5f);

        Color stroke = _state switch
        {
            AnysoundIconState.Selected => SelectedStrokeColor,
            AnysoundIconState.Hover => _color,
            _ => _color * 0.5f,
        };
        stroke.a = 1f;

        var pen = new IconPen(mgc.painter2D, origin, scale, stroke);

        if (_state == AnysoundIconState.Selected)
            pen.Rect(0.5f, 0.5f, 55f, 31f, _color);

        pen.Rect(0.5f, 0.5f, 55f, 31f);
        DrawIcon(pen, _icon == AnysoundIcon.Auto ? AnysoundIcon.Generic : _icon);
    }

    static void DrawIcon(IconPen pen, AnysoundIcon icon)
    {
        switch (icon)
        {
            case AnysoundIcon.Wood:
                pen.Curve(4, 9, 18, 5, 30, 12, 52, 8);
                pen.Curve(4, 24, 20, 27, 34, 19, 52, 23);
                pen.Ellipse(30, 16, 8, 3.5f);
                pen.Ellipse(30, 16, 3.5f, 1.4f);
                pen.Curve(4, 16, 10, 15, 16, 13, 22, 15);
                break;

            case AnysoundIcon.Plastic:
                pen.RoundedRect(14, 8, 28, 16, 5);
                pen.Line(19, 12, 29, 12);
                pen.Line(17, 28, 39, 28);
                break;

            case AnysoundIcon.Paper:
                pen.Polygon(18, 5, 34, 5, 40, 11, 40, 27, 18, 27);
                pen.Polyline(34, 5, 34, 11, 40, 11);
                pen.Line(22, 15, 36, 15);
                pen.Line(22, 19, 36, 19);
                pen.Line(22, 23, 30, 23);
                break;

            case AnysoundIcon.Bubbles:
                pen.Circle(19, 19, 7);
                pen.Arc(19, 19, 4.5f, 200, 260);
                pen.Circle(33, 11, 4.5f);
                pen.Circle(39, 22, 3);
                pen.Circle(45, 12, 1.8f);
                break;

            case AnysoundIcon.EightBit:
                string[] invader =
                {
                    "..XXXX..",
                    ".XXXXXX.",
                    "XX.XX.XX",
                    "XXXXXXXX",
                    ".X.XX.X.",
                    "X......X",
                };
                const float pixel = 3.4f;
                float left = (IconWidth - pixel * 8) * 0.5f;
                float top = (IconHeight - pixel * invader.Length) * 0.5f;
                for (int y = 0; y < invader.Length; y++)
                for (int x = 0; x < invader[y].Length; x++)
                    if (invader[y][x] == 'X')
                        pen.Rect(left + x * pixel, top + y * pixel, pixel, pixel, pen.StrokeColor);
                break;

            case AnysoundIcon.Abstract:
                pen.Polygon(9, 25, 19, 7, 29, 25);
                pen.Circle(38, 13, 6);
                pen.Curve(28, 27, 34, 19, 40, 31, 48, 22);
                break;

            case AnysoundIcon.Glass:
                pen.Rect(14, 5, 28, 22);
                pen.Line(19, 20, 27, 10);
                pen.Line(24, 23, 33, 12);
                break;

            case AnysoundIcon.Metal:
                pen.Rect(8, 7, 40, 18);
                pen.Line(8, 16, 48, 16);
                pen.Circle(11.5f, 10.5f, 1.2f, true);
                pen.Circle(44.5f, 10.5f, 1.2f, true);
                pen.Circle(11.5f, 21.5f, 1.2f, true);
                pen.Circle(44.5f, 21.5f, 1.2f, true);
                break;

            case AnysoundIcon.Stone:
                pen.Polygon(8, 24, 12, 17, 20, 15, 26, 20, 24, 26, 12, 27);
                pen.Polygon(28, 14, 33, 8, 42, 8, 46, 14, 41, 19, 31, 19);
                pen.Polygon(34, 26, 37, 22, 43, 23, 44, 27, 38, 28);
                break;

            case AnysoundIcon.Water:
                for (int i = 0; i < 4; i++)
                {
                    float y = 9 + i * 5;
                    pen.Curve(6, y, 16, y - 4, 20, y + 4, 28, y);
                    pen.Curve(28, y, 36, y - 4, 40, y + 4, 50, y);
                }

                break;

            case AnysoundIcon.Dirt:
                pen.Curve(6, 26, 16, 12, 40, 12, 50, 26);
                pen.Line(4, 26, 52, 26);
                pen.Circle(20, 21, 1.2f, true);
                pen.Circle(28, 18, 1, true);
                pen.Circle(35, 22, 1.3f, true);
                pen.Circle(26, 23.5f, 0.8f, true);
                pen.Circle(42, 23, 0.9f, true);
                pen.Circle(14, 9, 1.1f, true);
                pen.Circle(40, 7, 1.3f, true);
                pen.Circle(33, 11, 0.8f, true);
                pen.Circle(22, 6, 0.8f, true);
                break;

            case AnysoundIcon.Mouth:
                pen.Curve(12, 16, 18, 8, 24, 9, 28, 12);
                pen.Curve(28, 12, 32, 9, 38, 8, 44, 16);
                pen.Curve(12, 16, 20, 27, 36, 27, 44, 16);
                pen.Curve(12, 16, 22, 19, 34, 19, 44, 16);
                break;

            case AnysoundIcon.Crunch:
                pen.Star(28, 16, 12, 5, 8);
                pen.Line(8, 8, 13, 11);
                pen.Line(48, 24, 43, 21);
                pen.Line(9, 25, 14, 22);
                pen.Line(47, 7, 43, 10);
                break;

            case AnysoundIcon.Pebbles:
                pen.Ellipse(16, 23, 5.5f, 3.5f);
                pen.Ellipse(29, 24, 4, 2.8f);
                pen.Ellipse(41, 22, 6, 4);
                pen.Ellipse(23, 14, 3.2f, 2.2f);
                pen.Ellipse(36, 12, 2.6f, 1.9f);
                break;

            case AnysoundIcon.Leaves:
                pen.Curve(14, 24, 14, 12, 26, 6, 38, 7);
                pen.Curve(38, 7, 38, 19, 26, 26, 14, 24);
                pen.Line(14, 24, 33, 11);
                pen.Line(14, 24, 10, 28);
                pen.Curve(36, 27, 38, 21, 44, 18, 49, 18);
                pen.Curve(49, 18, 49, 23, 43, 27, 36, 27);
                break;

            case AnysoundIcon.Ice:
                pen.Polygon(20, 5, 36, 5, 44, 16, 36, 27, 20, 27, 12, 16);
                pen.Polyline(23, 9, 28, 16, 25, 22);
                pen.Polyline(28, 16, 34, 18, 38, 13);
                break;

            case AnysoundIcon.Lever:
                pen.Line(12, 27, 44, 27);
                pen.Circle(28, 23, 2.5f);
                pen.Line(29.5f, 21, 39, 9);
                pen.Circle(40.5f, 7, 3);
                pen.Arc(28, 23, 11, 195, 240);
                break;

            case AnysoundIcon.Bottle:
                pen.Polygon(25, 3, 31, 3, 31, 8, 36, 13, 36, 29, 20, 29, 20, 13, 25, 8);
                pen.Rect(20, 17, 16, 7);
                break;

            case AnysoundIcon.Stick:
                pen.Line(8, 24, 48, 9);
                pen.Line(21, 19.2f, 25, 12);
                pen.Line(36, 13.5f, 41, 17);
                pen.Line(12, 8, 44, 27);
                pen.Line(30, 18.5f, 33, 24);
                break;

            case AnysoundIcon.Electricity:
                pen.Polygon(33, 3, 18, 18, 27, 18, 22, 29, 39, 13, 30, 13, 35, 3);
                break;

            case AnysoundIcon.Button:
                pen.RoundedRect(16, 4, 24, 24, 3);
                pen.Circle(28, 16, 7);
                pen.Circle(28, 16, 2.2f, true);
                break;

            case AnysoundIcon.None:
                pen.Line(20, 26, 36, 6);
                break;

            case AnysoundIcon.Synth:
                pen.Curve(6, 16, 12, 2, 16, 2, 20, 16);
                pen.Curve(20, 16, 24, 30, 28, 30, 34, 16);
                pen.Polyline(34, 16, 34, 8, 42, 8, 42, 24, 50, 24, 50, 16);
                break;

            default:
                float[] bars = { 4, 9, 14, 7, 18, 11, 5, 13, 8, 3 };
                for (int i = 0; i < bars.Length; i++)
                {
                    float x = 10 + i * 4;
                    pen.Line(x, 16 - bars[i] * 0.5f, x, 16 + bars[i] * 0.5f);
                }

                break;
        }
    }

    /// <summary>
    /// Small helper drawing in the 56x32 icon space
    /// </summary>
    readonly struct IconPen
    {
        readonly Painter2D _painter;
        readonly Vector2 _origin;
        readonly float _scale;
        public readonly Color StrokeColor;

        public IconPen(Painter2D painter, Vector2 origin, float scale, Color strokeColor)
        {
            _painter = painter;
            _origin = origin;
            _scale = scale;
            StrokeColor = strokeColor;
            _painter.lineWidth = Mathf.Max(1f, scale);
            _painter.strokeColor = strokeColor;
            _painter.lineJoin = LineJoin.Round;
            _painter.lineCap = LineCap.Round;
        }

        Vector2 T(float x, float y) => new(_origin.x + x * _scale, _origin.y + y * _scale);

        public void Line(float x0, float y0, float x1, float y1) => Polyline(x0, y0, x1, y1);

        public void Polyline(params float[] points)
        {
            _painter.BeginPath();
            _painter.MoveTo(T(points[0], points[1]));
            for (int i = 2; i < points.Length; i += 2)
                _painter.LineTo(T(points[i], points[i + 1]));
            _painter.Stroke();
        }

        public void Polygon(params float[] points)
        {
            _painter.BeginPath();
            _painter.MoveTo(T(points[0], points[1]));
            for (int i = 2; i < points.Length; i += 2)
                _painter.LineTo(T(points[i], points[i + 1]));
            _painter.ClosePath();
            _painter.Stroke();
        }

        public void Curve(float x0, float y0, float c0x, float c0y, float c1x, float c1y, float x1, float y1)
        {
            _painter.BeginPath();
            _painter.MoveTo(T(x0, y0));
            _painter.BezierCurveTo(T(c0x, c0y), T(c1x, c1y), T(x1, y1));
            _painter.Stroke();
        }

        public void Rect(float x, float y, float width, float height, Color? fill = null)
        {
            _painter.BeginPath();
            _painter.MoveTo(T(x, y));
            _painter.LineTo(T(x + width, y));
            _painter.LineTo(T(x + width, y + height));
            _painter.LineTo(T(x, y + height));
            _painter.ClosePath();
            if (fill.HasValue)
            {
                _painter.fillColor = fill.Value;
                _painter.Fill();
            }
            else
            {
                _painter.Stroke();
            }
        }

        public void RoundedRect(float x, float y, float width, float height, float radius)
        {
            _painter.BeginPath();
            _painter.MoveTo(T(x + radius, y));
            _painter.ArcTo(T(x + width, y), T(x + width, y + height), radius * _scale);
            _painter.ArcTo(T(x + width, y + height), T(x, y + height), radius * _scale);
            _painter.ArcTo(T(x, y + height), T(x, y), radius * _scale);
            _painter.ArcTo(T(x, y), T(x + width, y), radius * _scale);
            _painter.ClosePath();
            _painter.Stroke();
        }

        public void Circle(float cx, float cy, float radius, bool fill = false)
        {
            _painter.BeginPath();
            _painter.Arc(T(cx, cy), radius * _scale, Angle.Degrees(0), Angle.Degrees(360));
            _painter.ClosePath();
            if (fill)
            {
                _painter.fillColor = StrokeColor;
                _painter.Fill();
            }
            else
            {
                _painter.Stroke();
            }
        }

        public void Arc(float cx, float cy, float radius, float startDegrees, float endDegrees)
        {
            _painter.BeginPath();
            _painter.Arc(T(cx, cy), radius * _scale, Angle.Degrees(startDegrees), Angle.Degrees(endDegrees));
            _painter.Stroke();
        }

        /// <summary>
        /// A burst star with the given number of points (slightly flattened to fit the wide frame)
        /// </summary>
        public void Star(float cx, float cy, float outerRadius, float innerRadius, int points)
        {
            _painter.BeginPath();
            for (int i = 0; i < points * 2; i++)
            {
                float radius = i % 2 == 0 ? outerRadius : innerRadius;
                float angle = Mathf.PI * i / points - Mathf.PI * 0.5f;
                Vector2 point = T(cx + radius * Mathf.Cos(angle), cy + radius * Mathf.Sin(angle) * 0.8f);
                if (i == 0) _painter.MoveTo(point);
                else _painter.LineTo(point);
            }

            _painter.ClosePath();
            _painter.Stroke();
        }

        public void Ellipse(float cx, float cy, float rx, float ry)
        {
            // Four bezier quarter arcs
            const float k = 0.5523f;
            _painter.BeginPath();
            _painter.MoveTo(T(cx + rx, cy));
            _painter.BezierCurveTo(T(cx + rx, cy + ry * k), T(cx + rx * k, cy + ry), T(cx, cy + ry));
            _painter.BezierCurveTo(T(cx - rx * k, cy + ry), T(cx - rx, cy + ry * k), T(cx - rx, cy));
            _painter.BezierCurveTo(T(cx - rx, cy - ry * k), T(cx - rx * k, cy - ry), T(cx, cy - ry));
            _painter.BezierCurveTo(T(cx + rx * k, cy - ry), T(cx + rx, cy - ry * k), T(cx + rx, cy));
            _painter.ClosePath();
            _painter.Stroke();
        }
    }
}
