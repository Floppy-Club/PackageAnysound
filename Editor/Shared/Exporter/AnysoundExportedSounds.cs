using System;
using System.Collections.Generic;
using System.Linq;
using Anysound.Shared.BaseClasses;
using UnityEditor;
using UnityEngine;

namespace Anysound.Shared.Exporter
{
    // Exported .wav files remember the generator and values that made them (in their .meta file),
    // so the browser can list them next to the presets and load them back into the editor
    public static class AnysoundExportedSounds
    {
        public const string Label = "AnysoundExport";
        public const string ExportedTag = "exported";

        [Serializable]
        class ExportData
        {
            public string generatorGuid;
            public List<string> keys = new();
            public List<float> values = new();
        }

        public static void MarkAsExported(string assetPath, AnysoundGeneratorBase generator, Dictionary<string, float> presetValues)
        {
            if (!generator || presetValues == null || string.IsNullOrEmpty(assetPath)) return;

            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null) return;

            var data = new ExportData
            {
                generatorGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(generator)),
                keys = presetValues.Keys.ToList(),
                values = presetValues.Values.ToList(),
            };
            importer.userData = JsonUtility.ToJson(data);
            importer.SaveAndReimport();

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset != null)
                AssetDatabase.SetLabels(asset, AssetDatabase.GetLabels(asset).Append(Label).Distinct().ToArray());
        }

        // Unsaved preset objects for every exported sound in the project
        public static List<AnysoundPresetObject> LoadAll()
        {
            var presets = new List<AnysoundPresetObject>();
            foreach (var guid in AssetDatabase.FindAssets($"l:{Label} t:AudioClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = Load(path);
                if (preset != null) presets.Add(preset);
            }

            return presets;
        }

        static AnysoundPresetObject Load(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null || string.IsNullOrEmpty(importer.userData)) return null;

            ExportData data;
            try
            {
                data = JsonUtility.FromJson<ExportData>(importer.userData);
            }
            catch (ArgumentException)
            {
                return null;
            }

            var generator = AssetDatabase.LoadAssetAtPath<AnysoundGeneratorBase>(AssetDatabase.GUIDToAssetPath(data?.generatorGuid ?? ""));
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (!generator || !clip) return null;

            var presetValues = new Dictionary<string, float>();
            for (int i = 0; i < Mathf.Min(data.keys.Count, data.values.Count); i++)
                presetValues[data.keys[i]] = data.values[i];

            List<string> tags;
            try
            {
                generator.DescribePreset(presetValues, out _, out tags);
            }
            catch (Exception)
            {
                // Values from an older version of the generator can miss keys
                tags = new List<string>();
            }

            tags.Add(ExportedTag);

            var preset = ScriptableObject.CreateInstance<AnysoundPresetObject>();
            preset.hideFlags = HideFlags.DontSave;
            preset.SetPresetValues(presetValues);
            preset.Create(clip.name, tags, generator);
            preset.SetExportedClip(clip);
            return preset;
        }
    }
}
