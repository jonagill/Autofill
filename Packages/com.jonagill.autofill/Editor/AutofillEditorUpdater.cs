using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Autofill.Editor
{
    public static class AutofillEditorUpdater
    {
        /// <summary>
        /// Scope that you can declare to prevent autofill error dialogs
        /// from opening during a code block (such as when batch-modifying assets)
        /// </summary>
        public class SuppressDialogsScope : IDisposable
        {
            public SuppressDialogsScope()
            {
                SuppressDialogs = true;
            }

            public void Dispose()
            {
                SuppressDialogs = false;
            }
        }

        private const string DIALOGS_MUTED_KEY = "Autofill_DialogsMuted";

        private static readonly MethodInfo getFieldInfoFromPropertyMethod;
        private static readonly MethodInfo getFieldAttributesMethod;


        static AutofillEditorUpdater()
        {
            // Access several Unity internal methods by reflection to make sure our type handling
            // is consistent with theirs
            Type scriptAttributeUtility = Type.GetType("UnityEditor.ScriptAttributeUtility,UnityEditor");
            getFieldInfoFromPropertyMethod = scriptAttributeUtility.GetMethod(
                "GetFieldInfoFromProperty",
                BindingFlags.NonPublic | BindingFlags.Static);

            getFieldAttributesMethod = scriptAttributeUtility.GetMethod(
                "GetFieldAttributes",
                BindingFlags.NonPublic | BindingFlags.Static);

            LoadIgnoredErrors();
        }

        private static bool SuppressDialogs = false;

        public static bool UpdateAndSavePrefab(GameObject prefab)
        {
            if (UpdateGameObject(prefab))
            {
                PrefabUtility.SavePrefabAsset(prefab);
                return true;
            }

            return false;
        }

        public static bool UpdateGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            var updatedAnyProperties = false;

            var allBehaviours = gameObject.GetComponentsInChildren<MonoBehaviour>(true);
            if (allBehaviours != null)
            {
                foreach (var behaviour in allBehaviours)
                {
                    if (behaviour != null)
                    {
                        SerializedObject serializedObject = new SerializedObject(behaviour);
                        var serializedProperty = serializedObject.GetIterator();

                        bool enterChildren;
                        do
                        {
                            enterChildren = true;
                            if (serializedProperty.isArray)
                            {
                                // We don't support autofilling arrays, and iterating through very long arrays
                                // (e.g. ProBuilder mesh vertex data) can cause the editor to hang
                                // while it looks up FieldInfo for every array element
                                enterChildren = false;
                                continue;
                            }

                            Type fieldType = null;
                            var fieldInfo = GetFieldInfoFromPropertyInternal(serializedProperty, out fieldType);
                            if (fieldInfo != null)
                            {
                                if (fieldType.IsSubclassOf(typeof(Component)))
                                {
                                    var attributes = GetFieldAttributesInternal(fieldInfo);
                                    if (attributes != null)
                                    {
                                        var autofillAttributes = attributes
                                            .Select(a => a as AutofillAttribute)
                                            .Where(a => a != null);

                                        if (autofillAttributes.Count() > 1)
                                        {
                                            Debug.LogError(
                                                $"Multiple autofill attributes declared on property {behaviour.GetType().Name}.{fieldInfo.Name} on GameObject {gameObject.name}. Using the first attribute found...",
                                                gameObject);
                                        }

                                        var autofill = autofillAttributes.FirstOrDefault();
                                        if (autofill != null)
                                        {
                                            // Forcibly update all fields when manually updating a GameObject
                                            AutofillUpdateResult result = UpdateProperty(
                                                serializedProperty,
                                                fieldType,
                                                autofill,
                                                force: true
                                            );

                                            if (result == AutofillUpdateResult.Updated)
                                            {
                                                updatedAnyProperties = true;
                                            }

                                            if (result.IsError())
                                            {
                                                var errorKey = GenerateErrorText(
                                                    serializedProperty,
                                                    fieldInfo,
                                                    result,
                                                    useRawResult: true);

                                                if (!IgnoredErrors.Contains(errorKey))
                                                {
                                                    var readableError = GenerateErrorText(
                                                        serializedProperty,
                                                        fieldInfo,
                                                        result,
                                                        useRawResult: false);

                                                    var displayError = $"Error updating autofill: {readableError}";

                                                    if (!SuppressDialogs && !SessionState.GetBool(DIALOGS_MUTED_KEY, false))
                                                    {
                                                        var response = EditorUtility.DisplayDialogComplex(
                                                            "Error updating autofilled fields",
                                                            $"{displayError}\n\n",
                                                            "Okay",
                                                            "Don't warn again for this prefab",
                                                            "Disable all warnings until restart" );

                                                        switch (response)
                                                        {
                                                            case 1:
                                                                IgnoreError(errorKey);
                                                                break;
                                                            case 2:
                                                                SessionState.SetBool(DIALOGS_MUTED_KEY, true);
                                                                break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Debug.LogError(displayError, serializedObject.targetObject);
                                                    }
                                                }
                                            }

                                            if (result == AutofillUpdateResult.Updated)
                                            {
                                                EditorUtility.SetDirty(serializedObject.targetObject);
                                            }
                                        }
                                    }
                                }
                            }
                        } while (serializedProperty.NextVisible(enterChildren));
                    }
                }
            }

            return updatedAnyProperties;
        }

        public static AutofillUpdateResult UpdateProperty(
            SerializedProperty property,
            Type propertyType,
            AutofillAttribute autofillAttribute,
            bool force = false)
        {
            if (property.isArray || propertyType.IsArray)
            {
                // Attributes on serialized arrays will run on each individual array element, rather than
                // the array itself. It's probably possible to hack around this and autofill arrays somehow,
                // but for now we won't try to autofill arrays at all
                return AutofillUpdateResult.Error_PropertyIsArray;
            }

            if (!propertyType.IsSubclassOf(typeof(Component)))
            {
                // Only component types can be autofilled
                return AutofillUpdateResult.Error_InvalidType;
            }

            var result = AutofillUpdateResult.Unchanged;
            var targetObject = property.serializedObject.targetObject;
            var targetComponent = targetObject as Component;
            if (targetComponent != null)
            {
                var targetGameObject = targetComponent.gameObject;
                if (targetGameObject != null && autofillAttribute != null)
                {
                    Component propertyComponent = property.objectReferenceValue as Component;

                    if (ShouldRunUpdate(targetGameObject, propertyComponent, autofillAttribute, force))
                    {
                        // Collect all of the possible components that could fill this field
                        Component[] allPossibleComponents = null;
                        switch (autofillAttribute.Type)
                        {
                            case AutofillType.Self:
                                allPossibleComponents = targetGameObject.GetComponents(propertyType);
                                break;
                            case AutofillType.Parent:
                                // GetComponentsInParent includes self for some reason, so start at the parent transform
                                if (targetGameObject.transform.parent != null)
                                {
                                    allPossibleComponents =
                                        targetGameObject.transform.parent.GetComponentsInParent(propertyType, true);
                                }

                                break;
                            case AutofillType.SelfAndParent:
                                allPossibleComponents = targetGameObject.GetComponentsInParent(propertyType, true);
                                break;
                            case AutofillType.Children:
                                allPossibleComponents = targetGameObject.GetComponentsInChildren(propertyType, true)
                                    .Where(c => c.gameObject != targetGameObject)
                                    .ToArray();
                                break;
                            case AutofillType.SelfAndChildren:
                                allPossibleComponents = targetGameObject.GetComponentsInChildren(propertyType, true);
                                break;
                        }

                        // Either set component to the chosen component, or set result to the error that explains
                        // why we couldn't choose a component.
                        if (allPossibleComponents == null || allPossibleComponents.Length == 0)
                        {
                            if (autofillAttribute.IsOptional)
                            {
                                propertyComponent = null;
                            }
                            else
                            {
                                result = AutofillUpdateResult.Error_NoValidComponentFound;
                            }
                        }
                        else
                        {
                            if (allPossibleComponents.Length == 1 || autofillAttribute.AcceptFirstValidResult)
                            {
                                propertyComponent = allPossibleComponents[0];
                            }
                            else
                            {
                                result = AutofillUpdateResult.Error_MultipleComponentsFound;
                            }
                        }

                        if (result == AutofillUpdateResult.Unchanged && property.objectReferenceValue != propertyComponent)
                        {
                            property.objectReferenceValue = propertyComponent;
                            property.serializedObject.ApplyModifiedProperties();
                            result = AutofillUpdateResult.Updated;
                        }
                    }
                }
            }

            return result;
        }

        internal static bool PropertyHasManualOverride(
            SerializedProperty property,
            AutofillAttribute autofillAttribute)
        {
            var targetObject = property.serializedObject.targetObject;
            var targetComponent = targetObject as Component;
            if (targetComponent != null)
            {
                var targetGameObject = targetComponent.gameObject;
                if (targetGameObject != null && autofillAttribute != null)
                {
                    Component propertyComponent = property.objectReferenceValue as Component;
                    
                    // We have a value, but it doesn't match our expected location
                    return propertyComponent != null &&
                           !VerifyAutofillSatisfied(targetGameObject, autofillAttribute, propertyComponent);
                }
            }

            return false;
        }

        private static bool ShouldRunUpdate(
            GameObject targetGameObject, 
            Component propertyComponent, 
            AutofillAttribute autofillAttribute, 
            bool force)
        {
            if (propertyComponent == null)
            {
                // We have no value -- try to find one
                return true;
            }

            if (autofillAttribute.AllowManualAssignment)
            {
                // We have manually assigned a value
                return false;
            }

            if (force)
            {
                // Update all fields that aren't manually assigned
                return true;
            }
            
            // Update only if we don't already have a valid target 
            return !VerifyAutofillSatisfied(targetGameObject, autofillAttribute, propertyComponent);
        }

        private static string GenerateErrorText(
            SerializedProperty property,
            FieldInfo fieldInfo,
            AutofillUpdateResult result,
            bool useRawResult)
        {
            var sb = new StringBuilder();

            void CollectHierarchyPathRecursive(Transform transform, bool first)
            {
                if (transform.parent != null)
                {
                    CollectHierarchyPathRecursive(transform.parent, false);
                }

                sb.Append(transform.gameObject.name);
                if (!first)
                {
                    sb.Append("/");
                }
            }

            var serializedComponent = (Component) property.serializedObject.targetObject;
            CollectHierarchyPathRecursive(serializedComponent.gameObject.transform, true);
            sb.Append($" ({fieldInfo.DeclaringType.ToString().Split('.').LastOrDefault()}.{property.name})");
            if (useRawResult)
            {
                sb.Append($" {result}");
            }
            else
            {
                sb.Append($" {result.ToErrorString(fieldInfo.FieldType)}");
            }

            return sb.ToString();
        }

        private static bool VerifyAutofillSatisfied(
            GameObject targetGameObject, 
            AutofillAttribute autofillAttribute,
            Component propertyValue)
        {
            if (propertyValue == null)
            {
                return false;
            }

            if (autofillAttribute.IncludesSelf && propertyValue.gameObject == targetGameObject)
            {
                return true;
            }

            switch (autofillAttribute.Type)
            {
                case AutofillType.Parent:
                case AutofillType.SelfAndParent:
                    return targetGameObject.transform.IsChildOf(propertyValue.transform);
                case AutofillType.SelfAndChildren:
                    return propertyValue.transform.IsChildOf(targetGameObject.transform);
            }

            return false;
        }

        [MenuItem("Assets/Autofill/Update Current Selection", false, 500)]
        private static void UpdateCurrentSelection()
        {
            foreach (var gameObject in Selection.gameObjects)
            {
                if (gameObject != null)
                {
                    var target = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
                    if (target == null)
                    {
                        target = gameObject;
                    }

                    UpdateAndSavePrefab(target);
                    Debug.LogFormat(target, "Updated autofilled properties for {0}", target);
                }
            }
        }
        
        [MenuItem("Assets/Autofill/Update Current Selection", true)]
        private static bool ValidateUpdateCurrentSelection()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }


        [MenuItem("Assets/Autofill/Update All Prefabs", false, 501)]
        public static void UpdateAllPrefabs()
        {
            if (EditorUtility.DisplayDialog(
                "Autofill all prefabs",
                "Are you sure you want to update autofilled fields on every prefab?\nThis operation could take some time.",
                "Okay",
                "Cancel"))
            {
                var prefabGuids = AssetDatabase.FindAssets("t:prefab");
                try
                {
                    for (var i = 0; i < prefabGuids.Length; i++)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                        
                        // Only update the progress bar occasionally as it can slow down bulk updates significantly
                        if (i % 50 == 0)
                        {
                            if (EditorUtility.DisplayCancelableProgressBar(
                                $"Updating prefabs {i}/{prefabGuids.Length}",
                                path,
                                i / (float) prefabGuids.Length))
                            {
                                break;
                            }
                        }
                        
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null)
                        {
                            UpdateAndSavePrefab(prefab);
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        private static readonly HashSet<string> IgnoredErrors = new HashSet<string>();

        private static readonly string IgnoredErrorSettingsPath = Path.Combine(
            Application.dataPath, "..", "ProjectSettings", "AutofillIgnoredErrors.txt"
        );

        private static void LoadIgnoredErrors()
        {
            if (File.Exists(IgnoredErrorSettingsPath))
            {
                var errors = File.ReadLines(IgnoredErrorSettingsPath);
                foreach (var error in errors)
                {
                    IgnoredErrors.Add(error);
                }
            }
        }

        private static void IgnoreError(string error)
        {
            if (!IgnoredErrors.Contains(error))
            {
                IgnoredErrors.Add(error);
                using (StreamWriter sw = File.AppendText(IgnoredErrorSettingsPath))
                {
                    sw.WriteLine(error);
                }
            }
        }

        private static FieldInfo GetFieldInfoFromPropertyInternal(SerializedProperty serializedProperty,
            out Type fieldType)
        {
            fieldType = null;

            object[] parameters = {serializedProperty, fieldType};
            var fieldInfo = (FieldInfo) getFieldInfoFromPropertyMethod.Invoke(null, parameters);
            if (fieldInfo != null)
            {
                // The 'out' parameter gets written into the parameters array
                fieldType = (Type) parameters[1];
            }

            return fieldInfo;
        }

        private static List<PropertyAttribute> GetFieldAttributesInternal(FieldInfo fieldInfo)
        {
            return (List<PropertyAttribute>) getFieldAttributesMethod.Invoke(null, new object[] {fieldInfo});
        }
    }
}