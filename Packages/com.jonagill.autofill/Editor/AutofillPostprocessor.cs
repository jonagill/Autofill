using System;
using UnityEditor;
using UnityEngine;

namespace Autofill.Editor
{
    public class AutofillPostprocessor : AssetPostprocessor
    {
        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var assetPath in importedAssets)
            {
                if (assetPath.EndsWith(".prefab"))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab != null)
                    {
                        AutofillEditorUpdater.UpdateAndSavePrefab(prefab);
                    }
                }
            }
        }
    }
}