using System.Collections.Generic;
using UnityEngine;

namespace Anysound.Shared
{
    [CreateAssetMenu(fileName = "New SoundCollectionObject", menuName = "SoundCollectionObject")]
    public class AnysoundSoundCollectionObject : ScriptableObject
    {
        [SerializeField] List<AudioClip> clips;

        public AudioClip GetClip()
        {
            return clips[Random.Range(0, clips.Count)];
        }

        public void AddClip(AudioClip clip)
        {
            clips ??= new List<AudioClip>();
            clips.Add(clip);
        }
    }
}