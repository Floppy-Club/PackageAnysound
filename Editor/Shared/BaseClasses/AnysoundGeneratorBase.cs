using System.Collections.Generic;
using UnityEngine;

namespace Anysound.Shared.BaseClasses
{
    public class AnysoundGeneratorBase : ScriptableObject
    {
        [SerializeField] private Texture2D coverTexture;
        public Texture2D CoverTexture => coverTexture;
        public bool isLoadable = true;
        
        public virtual void CreatePreset(Dictionary<string, float> presetValues)
        {

        }
    }
}
