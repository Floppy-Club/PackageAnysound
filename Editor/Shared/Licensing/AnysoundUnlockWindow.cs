using System.Linq;
using Anysound.Shared.BaseClasses;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Licensing
{
    // Enter the key, see what it unlocks, and get a key for a locked generator
    public class AnysoundUnlockWindow : EditorWindow
    {
        // The generator the user tried to open, or null when the window was opened from the browser header
        [SerializeField] AnysoundGeneratorBase generator;

        TextField _keyField;
        Label _statusLabel;
        Label _messageLabel;
        Button _unlockButton;
        Button _refreshButton;
        Button _removeButton;
        Button _getButton;

        string ProductId => generator ? generator.ProductId : null;
        string ProductName => AnysoundProducts.DisplayName(ProductId);

        public static void Show(AnysoundGeneratorBase lockedGenerator)
        {
            var window = GetWindow<AnysoundUnlockWindow>(true, "Anysound license", true);
            window.generator = lockedGenerator;
            window.minSize = new Vector2(420, 260);
            window.BuildGUI();
        }

        void OnEnable() => AnysoundLicense.Changed += UpdateStatus;
        void OnDisable() => AnysoundLicense.Changed -= UpdateStatus;

        // Rebuilds after a domain reload
        void CreateGUI() => BuildGUI();

        void BuildGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.floppyclub.anysound/Editor/Shared/uss/AnysoundStyles.uss");
            if (styles != null) root.styleSheets.Add(styles);
            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = root.style.paddingBottom = 12;

            string title = ProductId != null ? $"{ProductName} is locked" : "Anysound license";
            root.Add(new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14, marginBottom = 4 } });
            root.Add(new Label("Anysound is free: get a key with your email and it unlocks every generator. Enter it once and it works in all your projects on this machine.")
                { style = { whiteSpace = WhiteSpace.Normal, opacity = 0.7f, marginBottom = 12 } });

            root.Add(new Label("Key") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });
            var keyRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _keyField = new TextField { value = AnysoundLicense.Key ?? "", style = { flexGrow = 1 } };
            _keyField.textEdition.placeholder = AnysoundLicenseKey.Placeholder;
            _keyField.RegisterValueChangedCallback(_ => UpdateButtons());
            // Enter unlocks instead of leaving the field
            _keyField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                Unlock();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            keyRow.Add(_keyField);
            _unlockButton = new Button(Unlock) { text = "Unlock" };
            keyRow.Add(_unlockButton);
            root.Add(keyRow);

            _messageLabel = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 6 } };
            root.Add(_messageLabel);

            _statusLabel = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 6, opacity = 0.7f } };
            root.Add(_statusLabel);

            if (AnysoundLicenseConfig.UsePlaceholderBackend)
            {
                string testKeys = string.Join("\n", AnysoundPlaceholderLicenseBackend.TestKeys.Select(pair =>
                    $"{pair.Key}  {(pair.Value.Length > 0 ? string.Join(", ", pair.Value.Select(AnysoundProducts.DisplayName)) : "nothing")}"));
                var testLabel = new Label($"Placeholder backend. Test keys:\n{testKeys}")
                    { style = { whiteSpace = WhiteSpace.Normal, marginTop = 12, opacity = 0.5f, fontSize = 10 } };
                testLabel.selection.isSelectable = true;
                root.Add(testLabel);
            }

            root.Add(new VisualElement { style = { flexGrow = 1 } });

            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 12 } };
            _removeButton = new Button(RemoveKey) { text = "Remove key" };
            buttonRow.Add(_removeButton);
            _refreshButton = new Button(() => AnysoundLicense.Refresh()) { text = "Refresh", tooltip = "Check the key online again" };
            buttonRow.Add(_refreshButton);
            buttonRow.Add(new VisualElement { style = { flexGrow = 1 } });
            _getButton = new Button(GetKey) { text = ProductId != null ? $"Get {ProductName}" : "Get generators" };
            buttonRow.Add(_getButton);
            root.Add(buttonRow);

            UpdateStatus();
            _keyField.Focus();
        }

        void Unlock()
        {
            _messageLabel.text = "";
            if (!AnysoundLicense.SetKey(_keyField.value))
            {
                ShowMessage($"That doesn't look like a key. It should look like {AnysoundLicenseKey.Placeholder}.", true);
                return;
            }

            _keyField.SetValueWithoutNotify(AnysoundLicense.Key);
            UpdateStatus();
        }

        void RemoveKey()
        {
            AnysoundLicense.RemoveKey();
            _keyField.SetValueWithoutNotify("");
            ShowMessage("", false);
        }

        // Opens the page where the user gets a free key with their email
        void GetKey()
        {
            Application.OpenURL(AnysoundLicenseConfig.GetKeyUrl);
            ShowMessage("Paste the key from the page above and press Unlock.", false);
        }

        void UpdateStatus()
        {
            if (_statusLabel == null) return;

            if (AnysoundLicense.IsRefreshing)
                ShowMessage("Checking key...", false);
            else if (AnysoundLicense.LastError != null)
                ShowMessage(AnysoundLicense.LastError, true);
            else if (ProductId != null && AnysoundLicense.IsUnlocked(ProductId))
                ShowMessage($"{ProductName} is unlocked.", false);
            else if (ProductId != null && AnysoundLicense.State == AnysoundLicenseState.Valid)
                ShowMessage($"This key doesn't include {ProductName} yet.", false);
            else if (_messageLabel.text == "Checking key...")
                ShowMessage("", false);

            _statusLabel.text = AnysoundLicense.State switch
            {
                AnysoundLicenseState.NoKey => "No key entered.",
                AnysoundLicenseState.NotChecked => "The key hasn't been checked yet.",
                AnysoundLicenseState.Expired => "The key's list has expired. Go online and press Refresh.",
                _ => $"Unlocked: {UnlockedProducts()}.{CheckedText()}",
            };

            UpdateButtons();
        }

        static string UnlockedProducts()
        {
            var products = AnysoundLicense.Products;
            return products.Length > 0 ? string.Join(", ", products.Select(AnysoundProducts.DisplayName)) : "nothing yet";
        }

        static string CheckedText()
        {
            var lastRefresh = AnysoundLicense.LastRefresh;
            return lastRefresh == null ? "" : $" Checked {lastRefresh.Value.ToLocalTime():g}.";
        }

        void UpdateButtons()
        {
            bool busy = AnysoundLicense.IsRefreshing;
            bool hasKey = AnysoundLicense.Key != null;
            _unlockButton.SetEnabled(!busy && !string.IsNullOrWhiteSpace(_keyField.value));
            _refreshButton.SetEnabled(!busy && hasKey);
            _removeButton.SetEnabled(!busy && hasKey);
            // Nothing to get when this generator is already unlocked
            _getButton.SetEnabled(ProductId == null || !AnysoundLicense.IsUnlocked(ProductId));
        }

        void ShowMessage(string message, bool isError)
        {
            _messageLabel.text = message;
            _messageLabel.style.color = isError ? new StyleColor(new Color(0.96f, 0.25f, 0.31f)) : new StyleColor(StyleKeyword.Null);
        }
    }
}
