using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Autofill.Editor
{
    [InitializeOnLoad]
    public class AutofillPostprocessor : AssetPostprocessor
    {
        private const string DEFERRED_PATHS_STATE_KEY = "AutofillPostProcessorDeferredPaths";
        private const char DEFERRED_PATHS_SEPARATOR = ';';

        private static readonly List<string> assetPathsForDeferredUpdate = new();
        
        static AutofillPostprocessor()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;

            if (!EditorApplication.isCompiling)
            {
                ProcessDeferredUpdates();
            }
        }

        public static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var assetPath in importedAssets)
            {
                if (assetPath.EndsWith(".prefab"))
                {
                    if (EditorApplication.isCompiling)
                    {
                        if (!assetPathsForDeferredUpdate.Contains(assetPath))
                        {
                            assetPathsForDeferredUpdate.Add(assetPath);
                        }
                    }
                    else
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
        
        private static void BeforeAssemblyReload()
        {
            // Save any deferred paths to SessionState
            if (assetPathsForDeferredUpdate.Count > 0)
            {
                var prevPaths = SessionState.GetString(DEFERRED_PATHS_STATE_KEY, "");
                if (!string.IsNullOrEmpty(prevPaths))
                {
                    assetPathsForDeferredUpdate.Add(prevPaths);
                }

                var joinedPaths = string.Join(DEFERRED_PATHS_SEPARATOR, assetPathsForDeferredUpdate);
                SessionState.SetString(DEFERRED_PATHS_STATE_KEY, joinedPaths);
            }
        }

        private static void ProcessDeferredUpdates()
        {
            var deferredPaths = 
                SessionState.GetString(DEFERRED_PATHS_STATE_KEY, "")
                .Split(DEFERRED_PATHS_SEPARATOR);
            
            foreach (var assetPath in deferredPaths)
            {
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab != null)
                    {
                        AutofillEditorUpdater.UpdateAndSavePrefab(prefab);
                    }
                }
            }
            
            // Clear our previous state
            SessionState.EraseString(DEFERRED_PATHS_STATE_KEY);
        }
    }
}