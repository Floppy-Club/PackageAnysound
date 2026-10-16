using System;
using System.Collections.Generic;
using System.IO;
using Anysound.Shared.BaseClasses;
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
        private int _numberOfClips;

        // Generates one new clip per call, so each exported clip is unique
        private Func<AudioClip> _generateClip;
        private string _defaultFileName = "AnysoundClip";

        // Remembered on the exported files, so they show up in the browser
        private AnysoundGeneratorBase _generator;
        private Dictionary<string, float> _presetValues;

        public static void ShowExporterWindow(AnysoundFootstepObject anysoundFootstepObject,
            float currentSizeValue,
            float currentMovementSpeed,
            float currentSurfaceType)
        {
            ShowExporterWindow(
                () => AnysoundFootstepsHelper.GenerateAudioClip(anysoundFootstepObject, currentSizeValue,
                    currentMovementSpeed, currentSurfaceType),
                "AnysoundFootstepClip");
        }

        public static void ShowExporterWindow(AnysoundFootstepObject anysoundFootstepObject,
            float currentSizeValue,
            float currentMovementSpeed,
            int surfaceA,
            int surfaceB,
            float surfaceCrossfade)
        {
            ShowExporterWindow(
                () => AnysoundFootstepsHelper.GenerateAudioClip(anysoundFootstepObject, currentSizeValue,
                    currentMovementSpeed, surfaceA, surfaceB, surfaceCrossfade),
                "AnysoundFootstepClip");
        }

        public static void ShowExporterWindow(AnysoundGeneratorBase generator, Dictionary<string, float> presetValues,
            Func<AudioClip> generateClip, string defaultFileName)
        {
            ShowExporterWindow(generateClip, defaultFileName);
            var window = GetWindow<AnysoundExporterWindow>();
            window._generator = generator;
            window._presetValues = presetValues;
        }

        public static void ShowExporterWindow(Func<AudioClip> generateClip, string defaultFileName)
        {
            AnysoundExporterWindow window = GetWindow<AnysoundExporterWindow>();
            window.titleContent = new GUIContent("Anysound exporter");
            window._generateClip = generateClip;
            window._defaultFileName = defaultFileName;
            window._generator = null;
            window._presetValues = null;
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
                _defaultFileName,
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

            // The window closes below, so keep what the export needs
            var generator = _generator;
            var presetValues = _presetValues;

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
                        var clip = _generateClip();

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
                    foreach (var savedPath in savedPaths)
                        AnysoundExportedSounds.MarkAsExported(savedPath, generator, presetValues);
                    Debug.Log($"Successfully exported {_numberOfClips} audio clips");
                    isExporting = false;
                }
            }
            
            
            Close();
        }
    }
}