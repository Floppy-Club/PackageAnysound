using UnityEngine;
using UnityEngine.UIElements;
using System;

#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
[UxmlElement]
#endif

public partial class AnysoundSurfaceToggleControl : VisualElement
{
    // Event that will be triggered when toggle state changes
    public event Action<bool> OnToggleStateChanged;

    // Toggle state
    private bool _isToggled;

#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
    [UxmlAttribute("is-toggled")]
#endif
    public bool IsToggled
    {
        get => _isToggled;
        set
        {
            if (_isToggled != value)
            {
                _isToggled = value;
                UpdateVisualState();
                OnToggleStateChanged?.Invoke(_isToggled);
            }
        }
    }

    // Properties required by the spec
#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
    [UxmlAttribute("name-text")]
#endif
    public string Name { get; set; } = "Toggle";

    private Texture2D _iconImage;

    public Texture2D IconImage
    {
        get => _iconImage;
        set
        {
            _iconImage = value;
            UpdateVisualState();
        }
    }

    // UI Elements
    private readonly Label _nameLabel;
    private readonly VisualElement _iconContainer;

    private string _iconImagePath;
#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
    [UxmlAttribute("icon-image")]
#endif
    public string IconImagePath
    {
        get => _iconImagePath;
        set
        {
            _iconImagePath = value;
            if (!string.IsNullOrEmpty(_iconImagePath))
            {
                // Load the vector image from the Resources folder
                _iconImage = Resources.Load<Texture2D>(_iconImagePath);
                UpdateVisualState();
            }
        }
    }

#if UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER
    // In Unity 2023.2+/6, UxmlFactory is deprecated in favor of UxmlElement attribute
#else
    public new class UxmlFactory : UnityEngine.UIElements.UxmlFactory<AnysoundSurfaceToggleControl, UxmlTraits>
    {
    }
#endif

#if !(UNITY_2023_2_OR_NEWER || UNITY_6000_0_OR_NEWER)
    public class UxmlTraits : VisualElement.UxmlTraits
    {
        private readonly UxmlStringAttributeDescription _name = new UxmlStringAttributeDescription { name = "name-text", defaultValue = "Toggle" };
        private readonly UxmlBoolAttributeDescription _isToggled = new UxmlBoolAttributeDescription { name = "is-toggled", defaultValue = false };
        private readonly UxmlStringAttributeDescription _icon = new UxmlStringAttributeDescription { name = "icon-image" };

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);

            var toggleControl = (AnysoundSurfaceToggleControl)ve;
            toggleControl.Name = _name.GetValueFromBag(bag, cc);
            toggleControl.IsToggled = _isToggled.GetValueFromBag(bag, cc);
            
            // Get the icon path and load the vector image if the path is not empty
            string iconPath = _icon.GetValueFromBag(bag, cc);
            if (!string.IsNullOrEmpty(iconPath))
            {
                toggleControl.IconImagePath = iconPath;
            }
        }
    }
#endif




    public AnysoundSurfaceToggleControl()
    {
        // Create container structure
        AddToClassList("custom-toggle-control");

        // Create icon container
        _iconContainer = new VisualElement
        {
            name = "icon-container",
        };
        _iconContainer.AddToClassList("icon-container");
        Add(_iconContainer);

        // Create name label
        _nameLabel = new Label();
        _nameLabel.AddToClassList("toggle-name-label");
        Add(_nameLabel);
        

        // Set up event handling
        RegisterCallback<ClickEvent>(evt => Toggle());

        // Schedule a call to UpdateVisualState after the element is attached to panel
        schedule.Execute(UpdateVisualState).ExecuteLater(10);
    }

    public void Toggle()
    {
        IsToggled = !IsToggled;
    }

    public void SetToggleState(bool state)
    {
        IsToggled = state;
    }

    private void UpdateVisualState()
    {
        // Update toggle indicator
        if (_isToggled)
        {
            //_iconContainer.AddToClassList("toggled");
            _nameLabel.AddToClassList("toggled");
            
            AddToClassList("toggled");
        }
        else
        {
            //_iconContainer.RemoveFromClassList("toggled");
            _nameLabel.RemoveFromClassList("toggled");
            RemoveFromClassList("toggled");
        }

        // Update name and icon
        _nameLabel.text = Name;

        if (_iconImage != null)
        {
            _iconContainer.style.backgroundImage = new StyleBackground(_iconImage);
            _iconContainer.style.display = DisplayStyle.Flex;
        }
        else
        {
            _iconContainer.style.display = DisplayStyle.None;
        }
    }
}