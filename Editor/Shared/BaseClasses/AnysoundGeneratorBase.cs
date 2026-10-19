using System.Collections.Generic;
using UnityEngine;

namespace Anysound.Shared.BaseClasses
{
    public class AnysoundGeneratorBase : ScriptableObject
    {
        [SerializeField] private Texture2D coverTexture;
        public Texture2D CoverTexture => coverTexture;
        public bool isLoadable = true;

        // The product a key must hold to use this generator, see AnysoundProducts. Null means free
        public virtual string ProductId => null;
        
        public virtual void CreatePreset(Dictionary<string, float> presetValues)
        {

        }

        // A suggested name and tags for a sound made with these values
        public virtual void DescribePreset(Dictionary<string, float> presetValues, out string presetName, out List<string> tags)
        {
            presetName = name;
            tags = new List<string>();
        }
    }
}
