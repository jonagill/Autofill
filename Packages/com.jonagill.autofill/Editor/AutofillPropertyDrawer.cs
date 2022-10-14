using Autofill.Editor;
using UnityEngine;
using UnityEditor;

namespace Autofill
{
    [CustomPropertyDrawer(typeof(AutofillAttribute), true)]
    public class AutofillPropertyDrawer : PropertyDrawer
    {
        private AutofillUpdateResult result = AutofillUpdateResult.Unchanged;

        private float HelpBoxHeight => EditorGUIUtility.singleLineHeight + 15f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (result.IsError())
            {
                return EditorGUI.GetPropertyHeight(property, label) + HelpBoxHeight;
            }

            return 0f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            result = AutofillUpdateResult.Unchanged;
            var autofillAttribute = attribute as AutofillAttribute;

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
                        string.Format("Autofill failed: {0}", result.ToErrorString()),
                        MessageType.Warning);
                    position.y += HelpBoxHeight;

                    // Only render the field if there was an error to display.
                    // Otherwise, hide this field entirely to avoid cluttering the inspector
                    position.height = EditorGUI.GetPropertyHeight(property, label);
                    EditorGUI.PropertyField(position, property, label, true);
                }
            }
        }
    }
}