using Anysound.Shared.Browser;
using UnityEngine;

namespace Anysound.Shared.BaseClasses
{
    public abstract class AnysoundGeneratorWindowBase
    {
        public abstract void SetPresetObject(AnysoundPresetObject preset);
        public abstract void SetupObjectEditing();

        protected void Back()
        {
            AnysoundBrowser.ShowSoundBrowser();
        }
    }
}
