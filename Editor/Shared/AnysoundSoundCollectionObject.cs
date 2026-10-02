using System.Collections.Generic;
using UnityEngine;

namespace Anysound.Shared
{
    [CreateAssetMenu(fileName = "New SoundCollectionObject", menuName = "SoundCollectionObject")]
    public class AnysoundSoundCollectionObject : ScriptableObject
    {
        [SerializeField] List<AudioClip> clips;

        public int Count => clips?.Count ?? 0;

        public AudioClip GetClip()
        {
            return clips[Random.Range(0, clips.Count)];
        }

        /// <summary>
        /// The clip at the index (clamped to the collection), or null if the collection is empty
        /// </summary>
        public AudioClip GetClip(int index)
        {
            return Count == 0 ? null : clips[Mathf.Clamp(index, 0, clips.Count - 1)];
        }

        public void AddClip(AudioClip clip)
        {
            clips ??= new List<AudioClip>();
            clips.Add(clip);
        }
    }
}