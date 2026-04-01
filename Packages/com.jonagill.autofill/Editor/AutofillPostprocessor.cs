using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            EditorSceneManager.sceneSaving += HandleSceneSaving;

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
                if (IsPrefabPath(assetPath))
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
        
        private static void HandleSceneSaving(Scene scene, string path)
        {
            if (EditorApplication.isCompiling)
            {
                if (!assetPathsForDeferredUpdate.Contains(path))
                {
                    assetPathsForDeferredUpdate.Add(path);
                }
            }
            else
            {
                AutofillEditorUpdater.UpdateScene(scene);
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
                    if (IsPrefabPath(assetPath))
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        if (prefab != null)
                        {
                            AutofillEditorUpdater.UpdateAndSavePrefab(prefab);
                        }
                    }
                    else if (IsScenePath(assetPath))
                    {
                        // Process our scene if it's still open
                        var scene = EditorSceneManager.GetSceneByPath(assetPath);
                        if (scene.IsValid())
                        {
                            AutofillEditorUpdater.UpdateScene(scene);
                        }
                    }
                }
            }
            
            // Clear our previous state
            SessionState.EraseString(DEFERRED_PATHS_STATE_KEY);
        }

        private static bool IsPrefabPath(string path)
        {
            return path.EndsWith(".prefab");
        }

        private static bool IsScenePath(string path)
        {
            return path.EndsWith(".unity");
        }
    }
}