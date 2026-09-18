using System.IO;
using Anysound.Shared.Footsteps;
using Anysound.Shared.Frontend;
using Anysound.Shared.Generators.Footsteps;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anysound.Shared.Exporter
{
    public class AnysoundExporterWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset visualTreeAsset = default;
        private AnysoundFootstepObject _anysoundFootstepObject;
        private float _currentSurfaceType;
        private float _currentMovementSpeed;
        private float _currentSizeValue;
        private int _numberOfClips;

        public static void ShowExporterWindow(AnysoundFootstepObject anysoundFootstepObject,
            float currentSizeValue,
            float currentMovementSpeed,
            float currentSurfaceType)
        {
            AnysoundExporterWindow window = GetWindow<AnysoundExporterWindow>();
            window.titleContent = new GUIContent("Anysound exporter");
            window._anysoundFootstepObject = anysoundFootstepObject;
            window._currentSizeValue = currentSizeValue;
            window._currentMovementSpeed = currentMovementSpeed;
            window._currentSurfaceType = currentSurfaceType;
            window.CreateGUI();
        }

        public void CreateGUI()
        {
            visualTreeAsset ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.floppyclub.anysound/Editor/Shared/Exporter/AnysoundExporter.uxml");

            rootVisualElement.Clear();
            TemplateContainer container = visualTreeAsset.CloneTree();
            rootVisualElement.Add(container);
            var exportButton = container.Q<Button>("ExportButton");
            var clipsCountSlider = container.Q<AnysoundSlider>("NumberOfClips");
            clipsCountSlider.RegisterValueChangedCallback(evt =>
            {
                _numberOfClips = (int)evt.newValue;
                Debug.Log($"Number of clips: {_numberOfClips}");
            });
            exportButton.clicked += ExportClips;
        }

        void ExportClips()
        {
            if (_numberOfClips <= 0)
            {
                EditorUtility.DisplayDialog("Export Error", "Number of clips must be greater than 0.", "OK");
                return;
            }

            Debug.Log($"Exporting {_numberOfClips} audio clips");

            // First ask for file name
            string baseName = EditorUtility.SaveFilePanel(
                "Enter File Name",
                "Assets",
                "AnysoundFootstepClip",
                "");

            if (string.IsNullOrEmpty(baseName))
                return;

            // Extract directory and file name from the full path
            string folderPath = Path.GetDirectoryName(baseName);
            string fileNameBase = Path.GetFileNameWithoutExtension(baseName);

            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileNameBase))
            {
                EditorUtility.DisplayDialog("Export Error", "Invalid file path or name.", "OK");
                return;
            }

            // Convert to a project-relative path if inside the project
            string relativePath = folderPath;
            if (folderPath.StartsWith(Application.dataPath))
            {
                relativePath = "Assets" + folderPath.Substring(Application.dataPath.Length);
            }

            // Use a coroutine-like approach with EditorApplication.update
            bool isExporting = true;
            int currentClip = 0;
            string[] savedPaths = new string[_numberOfClips];

            EditorApplication.update += ExportUpdate;

            void ExportUpdate()
            {
                if (!isExporting)
                {
                    EditorApplication.update -= ExportUpdate;
                    // Show completion message
                    if (EditorUtility.DisplayDialog("Export Complete",
                            $"Successfully exported {_numberOfClips} audio clips to:\n{relativePath}",
                            "OK"))
                    {
                        // Optionally highlight one of the files in the project view
                        if (savedPaths.Length > 0 && !string.IsNullOrEmpty(savedPaths[0]))
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(savedPaths[0]);
                            if (obj != null)
                                Selection.activeObject = obj;
                        }
                    }

                    return;
                }

                // Show progress bar
                float progress = (float)currentClip / _numberOfClips;
                bool canceled = EditorUtility.DisplayCancelableProgressBar(
                    "Exporting Audio Clips",
                    $"Generating clip {currentClip + 1} of {_numberOfClips}...",
                    progress);

                if (canceled)
                {
                    EditorUtility.ClearProgressBar();
                    isExporting = false;
                    Debug.Log("Export canceled by user");
                    return;
                }

                // Generate and save current clip
                if (currentClip < _numberOfClips)
                {
                    try
                    {
                        // Generate a unique audio clip
                        var clip = AnysoundFootstepsHelper.GenerateAudioClip(
                            _anysoundFootstepObject,
                            _currentSizeValue,
                            _currentMovementSpeed,
                            _currentSurfaceType);

                        // Create a filename with index
                        string filename = _numberOfClips > 1 ? 
                            $"{fileNameBase}_{currentClip + 1}.wav" : 
                            $"{fileNameBase}.wav";
                        string fullPath = Path.Combine(folderPath, filename);
                        string relativeFilePath = Path.Combine(relativePath, filename);

                        // Check if file already exists and rename if needed
                        int counter = 1;
                        while (File.Exists(fullPath))
                        {
                            // File exists, create new name with additional counter
                            filename = $"{fileNameBase}_{currentClip + 1}_{counter}.wav";
                            fullPath = Path.Combine(folderPath, filename);
                            relativeFilePath = Path.Combine(relativePath, filename);
                            counter++;
                        }

                        // Save the clip
                        AnysoundFootstepDSP.SaveClipToWav(clip, fullPath);

                        if (folderPath.StartsWith(Application.dataPath))
                        {
                            savedPaths[currentClip] = relativeFilePath;
                        }

                        currentClip++;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error generating clip {currentClip + 1}: {e.Message}");
                        currentClip++;
                    }
                }
                else
                {
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.Refresh(); // Refresh the asset database to show the new files
                    Debug.Log($"Successfully exported {_numberOfClips} audio clips");
                    isExporting = false;
                }
            }
            
            
            Close();
        }
    }
}