using Autofill.Editor;
using UnityEngine;
using UnityEditor;

#if UNITY_2022_1_OR_NEWER
using UnityEditor.UIElements;
using UnityEngine.UIElements;
#endif

namespace Autofill
{
    [CustomPropertyDrawer(typeof(AutofillAttribute), true)]
    public class AutofillPropertyDrawer : PropertyDrawer
    {
        private AutofillUpdateResult result = AutofillUpdateResult.Unchanged;

        private float HelpBoxHeight => EditorGUIUtility.singleLineHeight + 15f;
        private const float HelpBoxSpacing = 3f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var autofillAttribute = attribute as AutofillAttribute;
            
            if (result.IsError())
            {
                return EditorGUI.GetPropertyHeight(property, label) + HelpBoxHeight + HelpBoxSpacing;
            }
            else if (autofillAttribute.AlwaysShowInInspector || AutofillEditorSettings.DisplayAllFieldsInInspector)
            {
                return EditorGUI.GetPropertyHeight(property, label);
            }

            return 0f;
        }

#if UNITY_2022_1_OR_NEWER
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var autofillAttribute = attribute as AutofillAttribute;
            var holder = new VisualElement();

            var helpBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            holder.Add(helpBox);
            
            var propertyField = new PropertyField(property);
            propertyField.SetEnabled(false);
            holder.Add(propertyField);

            void UpdateFields()
            {
                result = AutofillUpdateResult.Unchanged;
                string errorText = null;

                // Don't update autofilled fields when the game is playing
                if (!Application.isPlaying)
                {
                    var fieldType = fieldInfo.FieldType;
                    if (autofillAttribute != null)
                    {
                        result = AutofillEditorUpdater.UpdateProperty(property, fieldType, autofillAttribute);
                        if (result.IsError())
                        {
                            errorText = string.Format("Autofill failed: {0}", result.ToErrorString(fieldType));
                        }
                    }
                }
                
                var showError = errorText != null;

                var isNull = property.objectReferenceValue == null;
                var isManualOverride = AutofillEditorUpdater.PropertyHasManualOverride(property, autofillAttribute);
                
                var showField = showError || 
                                autofillAttribute.AlwaysShowInInspector ||
                                (autofillAttribute.AllowManualAssignment && (isNull || isManualOverride) || 
                                AutofillEditorSettings.DisplayAllFieldsInInspector);

                helpBox.text = errorText;
                helpBox.style.display = showError ? DisplayStyle.Flex : DisplayStyle.None;
                propertyField.style.display = showField ? DisplayStyle.Flex : DisplayStyle.None;
                propertyField.SetEnabled(autofillAttribute.AllowManualAssignment);
            }
            
            propertyField.TrackSerializedObjectValue(property.serializedObject, obj => UpdateFields());

            AutofillEditorSettings.RegisterSettingsChangedCallback(UpdateFields);
            holder.RegisterCallbackOnce<DetachFromPanelEvent>((_ =>
            {
                AutofillEditorSettings.UnregisterSettingsChangedCallback(UpdateFields);
            }));
            
            UpdateFields();
            
            return holder;
        }
#endif

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            result = AutofillUpdateResult.Unchanged;
            var autofillAttribute = attribute as AutofillAttribute;
            
            var inspectorDrawn = false;
            
            // Don't update autofilled fields when the game is playing
            if (!Application.isPlaying)
            {
                var fieldType = fieldInfo.FieldType;
                if (autofillAttribute != null)
                {
                    result = AutofillEditorUpdater.UpdateProperty(property, fieldType, autofillAttribute);
                }

                if (result.IsError())
                {
                    position.height = HelpBoxHeight;
                    EditorGUI.HelpBox(
                        position,
                        string.Format("Autofill failed: {0}", result.ToErrorString(fieldType)),
                        MessageType.Warning);
                    position.y += HelpBoxHeight + HelpBoxSpacing;

                    // Only render the field if there was an error to display.
                    // Otherwise, hide this field entirely to avoid cluttering the inspector
                    position.height = EditorGUI.GetPropertyHeight(property, label);
                    EditorGUI.PropertyField(position, property, label, true);
                    inspectorDrawn = true;
                }
            }
            
            if (!inspectorDrawn && (autofillAttribute.AlwaysShowInInspector || AutofillEditorSettings.DisplayAllFieldsInInspector))
            {
                using (new EditorGUI.DisabledGroupScope(true))
                {
                    position.height = EditorGUI.GetPropertyHeight(property, label);
                    EditorGUI.PropertyField(position, property, label, true);
                }
            }
        }
    }
}