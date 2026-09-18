using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
[UxmlElement]
#endif

public partial class AnysoundSlider : Slider
{
#if !(UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER)
    public new class UxmlFactory : UnityEngine.UIElements.UxmlFactory<AnysoundSlider, UxmlTraits>
    {
    }
#endif


#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
    [UxmlAttribute] public string unit { get; set; } = "%";
    [UxmlAttribute] public Color color { get; set; } = Color.cyan;
    [UxmlAttribute] public bool isEnabled { get; set; } = true;
    [UxmlAttribute] public bool integerOnly { get; set; } = false;
    
    private bool _showValues = false;
    [UxmlAttribute]
    public bool showValues
    {
        get => _showValues;
        set
        {
            _showValues = value;
            if (_valueLabel != null)
                _valueLabel.style.display = _showValues ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateHeaderVisibility();
        }
    }

    private bool _showLabel = true;
    [UxmlAttribute]
    public bool showLabel
    {
        get => _showLabel;
        set
        {
            _showLabel = value;
            if (labelElement != null)
                labelElement.style.display = _showLabel ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateHeaderVisibility();
        }
    }
#else
    public string unit { get; set; } = "";
    public Color color { get; set; } = Color.yellow;

    public bool isEnabled { get; set; } = true;
    public bool integerOnly { get; set; } = false;
    
    private bool _showValues = false;
    public bool showValues
    {
        get => _showValues;
        set
        {
            _showValues = value;
            if (_valueLabel != null)
                _valueLabel.style.display = _showValues ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateHeaderVisibility();
        }
    }

    private bool _showLabel = true;
    public bool showLabel
    {
        get => _showLabel;
        set
        {
            _showLabel = value;
            if (labelElement != null)
                labelElement.style.display = _showLabel ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateHeaderVisibility();
        }
    }
#endif

#if !(UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER)
    public new class UxmlTraits : Slider.UxmlTraits
    {
        private readonly UxmlStringAttributeDescription _unit = new UxmlStringAttributeDescription { name = "unit", defaultValue = "" };
        private readonly UxmlColorAttributeDescription _color = new UxmlColorAttributeDescription { name = "color", defaultValue = Color.yellow };
        private readonly UxmlBoolAttributeDescription _isEnabled = new UxmlBoolAttributeDescription { name = "isEnabled", defaultValue = true };
        private readonly UxmlBoolAttributeDescription _integerOnly = new UxmlBoolAttributeDescription { name = "integer-only", defaultValue = false };
        private readonly UxmlBoolAttributeDescription _showValues = new UxmlBoolAttributeDescription { name = "show-values", defaultValue = false };
        private readonly UxmlBoolAttributeDescription _showLabel = new UxmlBoolAttributeDescription { name = "show-label", defaultValue = true };

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);
            var anySlider = (AnysoundSlider)ve;
            anySlider.unit = _unit.GetValueFromBag(bag, cc);
            anySlider.color = _color.GetValueFromBag(bag, cc);
            anySlider.isEnabled = _isEnabled.GetValueFromBag(bag, cc);
            anySlider.integerOnly = _integerOnly.GetValueFromBag(bag, cc);
            anySlider.showValues = _showValues.GetValueFromBag(bag, cc);
            anySlider.showLabel = _showLabel.GetValueFromBag(bag, cc);
        }
    }
#endif


    private readonly Label _valueLabel;
    private readonly VisualElement _headerElement;
    private readonly VisualElement _dragTrack, _dragHandle;
    private Action _mouseUp;

    public AnysoundSlider() : this((string)null)
    {
    }

    public AnysoundSlider(float start, float end, SliderDirection direction = SliderDirection.Horizontal, float pageSize = 0.0f) : this((string)null,
        start, end, direction, pageSize)
    {
    }

    protected override void HandleEventTrickleDown(EventBase evt)
    {
        if (evt.GetType() == typeof(MouseCaptureOutEvent))
        {
            _mouseUp?.Invoke();
        }

        base.HandleEventTrickleDown(evt);
    }


    public AnysoundSlider(
        string label,
        float start = 0.0f,
        float end = 10f,
        SliderDirection direction = SliderDirection.Horizontal,
        float pageSize = 0.0f)
        : base(label, start, end, direction, pageSize)
    {
        _valueLabel = new Label
        {
            name = "value-label"
        };
        _valueLabel.AddToClassList("value-label");
        // The display style will be set by the property setter

        _headerElement = new VisualElement
        {
            name = "header-element"
        };
        _headerElement.AddToClassList("slider-header-element");
        this.Add(_headerElement);


        var dragContainerElement = new VisualElement
        {
            name = "drag-element"
        };
        dragContainerElement.AddToClassList("drag-element");
        this.Add(dragContainerElement);

        _headerElement.Add(labelElement);
        _headerElement.Add(_valueLabel);

        AddToClassList("anysound-slider");
        var dragContainerLine = new VisualElement
        {
            name = "drag-container-line"
        };
        dragContainerLine.AddToClassList("drag-container-line");
        dragContainerElement.Add(dragContainerLine);

        dragContainerElement.Add(this.Q<VisualElement>("unity-drag-container").parent);

        labelElement.AddToClassList("slider-label");
        var dragContainer = this.Q<VisualElement>("unity-drag-container");
        dragContainer?.AddToClassList("unity-drag-container");

        _dragHandle = this.Q<VisualElement>("unity-dragger");
        _dragHandle?.AddToClassList("drag-handle");
        _dragHandle?.RegisterCallback<PointerUpEvent>(evt => { Debug.Log("Mouse up"); });


        if (_dragHandle != null) _dragHandle.style.backgroundColor = color;

        _dragTrack = this.Q<VisualElement>("unity-tracker");
        _dragTrack?.AddToClassList("drag-track");
        if (_dragTrack != null) _dragTrack.style.backgroundColor = new StyleColor(color);

        var dragBorder = this.Q<VisualElement>("unity-dragger-border");
        dragBorder?.AddToClassList("drag-handle-border");
        
    }

    

    public void RegisterDragEndCallback(Action callback)
    {
        _mouseUp += callback;
    }

    private void UpdateHeaderVisibility()
    {
        if (_headerElement == null) return;
        _headerElement.style.display = (_showLabel || _showValues) ? DisplayStyle.Flex : DisplayStyle.None;
    }


    public void SetIsEnabled(bool state)
    {
        isEnabled = state;
        _dragHandle.style.backgroundColor = !isEnabled ? new StyleColor(Color.gray) : new StyleColor(color);
        _dragTrack.style.backgroundColor = !isEnabled ? new StyleColor(Color.gray) : new StyleColor(color);
    }

    public override void SetValueWithoutNotify(float newValue)
    {
        if (!isEnabled) return;
        
        // Round the value to an integer if integerOnly is true
        float valueToUse = integerOnly ? Mathf.Round(newValue) : newValue;
        
        base.SetValueWithoutNotify(valueToUse);
        _valueLabel.text = valueToUse + " " + unit;
        float lengthPercent = Mathf.InverseLerp(lowValue, highValue, valueToUse) * 100;
        _dragTrack.style.width = new StyleLength(new Length(lengthPercent, LengthUnit.Percent));
        _dragTrack.style.backgroundColor = new StyleColor(color);
        _dragHandle.style.backgroundColor = color;
    }
}