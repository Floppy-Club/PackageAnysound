using Anysound.Shared.Browser;
using UnityEngine.UIElements;

namespace Anysound.Shared.BaseClasses
{
    public abstract class AnysoundGeneratorWindowBase
    {
        // All generator windows are drawn into the same browser root, and Clear() does not remove callbacks registered on the root
        // itself. So the key handler of the active generator is tracked here and removed when another generator (or the browser) takes over
        static VisualElement _keyDownRoot;
        static EventCallback<KeyDownEvent> _keyDownHandler;

        public abstract void SetPresetObject(AnysoundPresetObject preset);
        public abstract void SetupObjectEditing();

        protected void Back()
        {
            AnysoundBrowser.ShowSoundBrowser();
        }

        protected static void RegisterKeyDownHandler(VisualElement root, EventCallback<KeyDownEvent> handler)
        {
            UnregisterKeyDownHandler();
            root.RegisterCallback(handler);
            _keyDownRoot = root;
            _keyDownHandler = handler;
        }

        public static void UnregisterKeyDownHandler()
        {
            _keyDownRoot?.UnregisterCallback(_keyDownHandler);
            _keyDownRoot = null;
            _keyDownHandler = null;
        }
    }
}
