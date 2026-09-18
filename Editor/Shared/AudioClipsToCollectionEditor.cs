using System.Collections.Generic;
using System.IO;
using Anysound.Shared;
using UnityEditor;
using UnityEngine;

namespace Anysound
{
    public class AudioClipsToCollectionEditor : EditorWindow
    {
        [MenuItem("Assets/Create/Anysound/Create Sound Collection from Selection", false, 80)]
        static void CreateSoundCollectionFromSelection()
        {
            // Get selected audio clips
            List<AudioClip> selectedClips = new List<AudioClip>();
            foreach (Object obj in Selection.objects)
            {
                if (obj is AudioClip clip)
                {
                    selectedClips.Add(clip);
                }
            }

            // Check if any audio clips are selected
            if (selectedClips.Count == 0)
            {
                EditorUtility.DisplayDialog("No Audio Clips Selected",
                    "Please select at least one audio clip in the Project window.", "OK");
                return;
            }

            // Determine default save path and name
            string defaultPath = "Assets";
            string defaultName = "New Sound Collection";

            // Try to get the folder path of the currently selected asset
            if (Selection.activeObject != null)
            {
                string activePath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(activePath))
                {
                    if (Directory.Exists(activePath))
                    {
                        // It's a folder
                        defaultPath = activePath;
                    }
                    else if (File.Exists(activePath))
                    {
                        // It's a file, get its directory
                        defaultPath = Path.GetDirectoryName(activePath);
                    }
                }
            }

            // Convert Unity asset path to full file system path
            string projectPath = Application.dataPath;
            string fullPath = Path.Combine(projectPath, defaultPath.Substring("Assets".Length));
            
            // Show save file dialog
            string path = EditorUtility.SaveFilePanel(
                "Save Sound Collection",
                fullPath,
                defaultName,
                "asset");

            if (string.IsNullOrEmpty(path))
            {
                // User canceled the save dialog
                return;
            }

            // Convert the full path back to a Unity asset path
            if (path.StartsWith(Application.dataPath))
            {
                string assetPath = "Assets" + path.Substring(Application.dataPath.Length);
                
                // Create and save the sound collection
                CreateSoundCollection(Path.GetFileNameWithoutExtension(path), selectedClips, assetPath);
            }
            else
            {
                EditorUtility.DisplayDialog("Invalid Path", 
                    "Please save the asset inside your project's Assets folder.", "OK");
            }
        }

        private static void CreateSoundCollection(string newName, List<AudioClip> clips, string assetPath)
        {
            // Create the scriptable object
            AnysoundSoundCollectionObject anysoundSoundCollection = ScriptableObject.CreateInstance<AnysoundSoundCollectionObject>();

            if (anysoundSoundCollection == null)
            {
                Debug.LogError("Failed to create SoundCollectionObject instance.");
                return;
            }

            // Add all selected clips
            foreach (AudioClip clip in clips)
            {
                if (clip)
                {
                    anysoundSoundCollection.AddClip(clip);
                }
            }

            AssetDatabase.CreateAsset(anysoundSoundCollection, assetPath);
            AssetDatabase.SaveAssets();

            // Select the newly created asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = anysoundSoundCollection;

            Debug.Log("Sound Collection created: " + assetPath);
        }
    }
}