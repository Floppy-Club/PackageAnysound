using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A custom dropdown (no system popup) where every choice has an icon. The value is the selected index.
/// When opened the choices are shown as a grid of icon tiles, in the style of the footstep surface selector.
/// </summary>
#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
[UxmlElement]
#endif
public partial class AnysoundDropdown : BaseField<int>
{
#if !(UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER)
    public new class UxmlFactory : UnityEngine.UIElements.UxmlFactory<AnysoundDropdown, UxmlTraits>
    {
    }
#endif

    public struct Item
    {
        public string name;
        public AnysoundIcon icon;
        public Texture2D customIcon;
        public Texture2D customIconSelected;

        public Item(string name, AnysoundIcon icon = AnysoundIcon.Auto, Texture2D customIcon = null, Texture2D customIconSelected = null)
        {
            this.name = name;
            this.icon = icon == AnysoundIcon.Auto ? AnysoundIconElement.FromName(name) : icon;
            this.customIcon = customIcon;
            this.customIconSelected = customIconSelected;
        }
    }

    public const string UssClassName = "anysound-dropdown";
    const float PopupMaxHeight = 280f;

    Color _color = new(0.376f, 0.78f, 0.36f);

#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
    [UxmlAttribute]
#endif
    public Color color
    {
        get => _color;
        set
        {
            _color = value;
            RefreshInput();
        }
    }

    readonly List<Item> _items = new();
    public IReadOnlyList<Item> items => _items;
    public string selectedName => value >= 0 && value < _items.Count ? _items[value].name : "";

    readonly VisualElement _input;
    readonly AnysoundIconElement _inputIcon;
    readonly Label _inputLabel;
    readonly VisualElement _chevron;

    VisualElement _popup;
    VisualElement _popupRoot;
    readonly List<VisualElement> _tiles = new();
    int _highlightIndex = -1;

    public bool isOpen => _popup != null;

    public AnysoundDropdown() : this(null)
    {
    }

    public AnysoundDropdown(string label) : base(label, new VisualElement())
    {
        AddToClassList(UssClassName);
        focusable = true;

        _input = this.Q(className: inputUssClassName);
        _input.AddToClassList(UssClassName + "__input");

        _inputIcon = new AnysoundIconElement();
        _inputIcon.AddToClassList(UssClassName + "__input-icon");
        _input.Add(_inputIcon);

        _inputLabel = new Label();
        _inputLabel.AddToClassList(UssClassName + "__input-label");
        _inputLabel.pickingMode = PickingMode.Ignore;
        _input.Add(_inputLabel);

        _chevron = new Label("▾");
        _chevron.AddToClassList(UssClassName + "__chevron");
        _chevron.pickingMode = PickingMode.Ignore;
        _input.Add(_chevron);

        _input.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            Toggle();
            evt.StopPropagation();
        });
        _input.RegisterCallback<PointerEnterEvent>(_ => _input.AddToClassList(UssClassName + "__input--hover"));
        _input.RegisterCallback<PointerLeaveEvent>(_ => _input.RemoveFromClassList(UssClassName + "__input--hover"));

        RegisterCallback<KeyDownEvent>(OnFieldKeyDown);
        RegisterCallback<DetachFromPanelEvent>(_ => Close());

        RefreshInput();
    }

    public void SetItems(IEnumerable<Item> newItems)
    {
        Close();
        _items.Clear();
        _items.AddRange(newItems);
        SetValueWithoutNotify(value);
    }

    public override void SetValueWithoutNotify(int newValue)
    {
        base.SetValueWithoutNotify(_items.Count == 0 ? 0 : Mathf.Clamp(newValue, 0, _items.Count - 1));
        RefreshInput();
    }

    void RefreshInput()
    {
        if (_inputLabel == null) return;

        _input.style.borderTopColor = _input.style.borderBottomColor =
            _input.style.borderLeftColor = _input.style.borderRightColor = isOpen ? _color : new Color(0.37f, 0.37f, 0.37f);
        _inputLabel.style.color = _color;
        _chevron.style.color = _color;
        _chevron.style.rotate = new Rotate(Angle.Degrees(isOpen ? 180 : 0));
        _inputIcon.color = _color;
        _inputIcon.state = AnysoundIconState.Selected;

        if (value >= 0 && value < _items.Count)
        {
            var item = _items[value];
            _inputLabel.text = item.name.ToUpperInvariant();
            _inputIcon.icon = item.icon;
            _inputIcon.SetCustomIcon(item.customIcon, item.customIconSelected);
            _inputIcon.style.display = DisplayStyle.Flex;
        }
        else
        {
            _inputLabel.text = "-";
            _inputIcon.style.display = DisplayStyle.None;
        }
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (isOpen || panel == null || _items.Count == 0) return;

        _popupRoot = panel.visualTree;

        _popup = new VisualElement { name = "AnysoundDropdownPopup", focusable = true };
        _popup.AddToClassList(UssClassName + "__popup");
        _popup.style.borderTopColor = _popup.style.borderBottomColor =
            _popup.style.borderLeftColor = _popup.style.borderRightColor = _color;

        // The popup lives outside this element's hierarchy, so it needs the style sheets that apply to this element
        for (VisualElement element = this; element != null; element = element.parent)
        {
            for (int i = 0; i < element.styleSheets.count; i++)
                _popup.styleSheets.Add(element.styleSheets[i]);
        }

        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.AddToClassList(UssClassName + "__popup-scroll");
        _popup.Add(scrollView);

        var grid = new VisualElement();
        grid.AddToClassList(UssClassName + "__grid");
        scrollView.Add(grid);

        _tiles.Clear();
        for (int i = 0; i < _items.Count; i++)
        {
            var tile = CreateTile(i);
            _tiles.Add(tile);
            grid.Add(tile);
        }

        // Place it just below the field (or above if there is no room below)
        Rect fieldRect = _popupRoot.WorldToLocal(_input.worldBound);
        _popup.style.position = Position.Absolute;
        _popup.style.left = fieldRect.x;
        _popup.style.width = fieldRect.width;
        _popup.style.maxHeight = PopupMaxHeight;
        float spaceBelow = _popupRoot.layout.height - fieldRect.yMax;
        if (spaceBelow < PopupMaxHeight && fieldRect.y > spaceBelow)
            _popup.style.bottom = _popupRoot.layout.height - fieldRect.y + 2;
        else
            _popup.style.top = fieldRect.yMax + 2;

        _popupRoot.Add(_popup);
        _popupRoot.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        _popupRoot.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        _popup.RegisterCallback<KeyDownEvent>(OnPopupKeyDown);

        SetHighlight(value);
        RefreshInput();
        _popup.schedule.Execute(() =>
        {
            _popup?.Focus();
            if (_highlightIndex >= 0 && _highlightIndex < _tiles.Count) scrollView.ScrollTo(_tiles[_highlightIndex]);
        });
    }

    public void Close()
    {
        if (!isOpen) return;

        _popupRoot.UnregisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        _popupRoot.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        _popup.RemoveFromHierarchy();
        _popup = null;
        _popupRoot = null;
        _tiles.Clear();
        _highlightIndex = -1;
        RefreshInput();
    }

    VisualElement CreateTile(int index)
    {
        var item = _items[index];

        var tile = new VisualElement();
        tile.AddToClassList(UssClassName + "__tile");

        var icon = new AnysoundIconElement { icon = item.icon, color = _color };
        icon.SetCustomIcon(item.customIcon, item.customIconSelected);
        icon.AddToClassList(UssClassName + "__tile-icon");
        tile.Add(icon);

        var label = new Label(item.name.ToUpperInvariant());
        label.AddToClassList(UssClassName + "__tile-label");
        label.pickingMode = PickingMode.Ignore;
        tile.Add(label);

        tile.RegisterCallback<PointerEnterEvent>(_ => SetHighlight(index));
        tile.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.button != 0) return;
            Select(index);
            evt.StopPropagation();
        });

        UpdateTile(tile, index);
        return tile;
    }

    void UpdateTile(VisualElement tile, int index)
    {
        var state = index == value ? AnysoundIconState.Selected : index == _highlightIndex ? AnysoundIconState.Hover : AnysoundIconState.Normal;
        tile.Q<AnysoundIconElement>().state = state;

        var label = tile.Q<Label>();
        Color labelColor = state == AnysoundIconState.Normal ? _color * 0.6f : _color;
        labelColor.a = 1f;
        label.style.color = labelColor;
        tile.EnableInClassList(UssClassName + "__tile--highlighted", index == _highlightIndex);
    }

    void SetHighlight(int index)
    {
        if (_tiles.Count == 0) return;
        _highlightIndex = Mathf.Clamp(index, 0, _tiles.Count - 1);
        for (int i = 0; i < _tiles.Count; i++)
            UpdateTile(_tiles[i], i);
    }

    void Select(int index)
    {
        Close();
        value = index;
        Focus();
    }

    void OnRootPointerDown(PointerDownEvent evt)
    {
        // Clicking outside the popup closes it. Clicks on the field itself are handled by Toggle
        if (evt.target is VisualElement target && (IsSelfOrDescendant(_popup, target) || IsSelfOrDescendant(_input, target))) return;
        Close();
    }

    // VisualElement.Contains() returns false for the element itself
    static bool IsSelfOrDescendant(VisualElement parent, VisualElement element) => element == parent || parent.Contains(element);

    void OnRootGeometryChanged(GeometryChangedEvent evt) => Close();

    void OnFieldKeyDown(KeyDownEvent evt)
    {
        if (isOpen) return;
        switch (evt.keyCode)
        {
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            case KeyCode.DownArrow:
                Open();
                evt.StopPropagation();
                break;
        }
    }

    void OnPopupKeyDown(KeyDownEvent evt)
    {
        int columns = GetColumnCount();
        switch (evt.keyCode)
        {
            case KeyCode.LeftArrow: SetHighlight(_highlightIndex - 1); break;
            case KeyCode.RightArrow: SetHighlight(_highlightIndex + 1); break;
            case KeyCode.UpArrow: SetHighlight(_highlightIndex - columns); break;
            case KeyCode.DownArrow: SetHighlight(_highlightIndex + columns); break;
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                Select(_highlightIndex);
                break;
            case KeyCode.Escape:
                Close();
                Focus();
                break;
            default:
                return;
        }

        evt.StopPropagation();
    }

    int GetColumnCount()
    {
        if (_tiles.Count == 0) return 1;
        float firstRowY = _tiles[0].layout.y;
        int columns = 0;
        foreach (var tile in _tiles)
        {
            if (!Mathf.Approximately(tile.layout.y, firstRowY)) break;
            columns++;
        }

        return Mathf.Max(1, columns);
    }
}
