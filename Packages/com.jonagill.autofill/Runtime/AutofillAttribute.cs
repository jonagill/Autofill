using UnityEngine;

namespace Autofill 
{
	/// <summary>
	/// Automatically gathers the serialized reference to a component on a prefab.
	/// Runs when the prefab saves or when the component is inspected.
	/// If the component is found, we hide the field to avoid cluttering the inspector.
	/// If the component is not found, we draw an error in the inspector.
	/// Should be used instead of [HideInInspector] and [RequiredComponent]
	/// </summary>
	public class AutofillAttribute : PropertyAttribute
	{
		public AutofillType Type;
		public virtual bool IsOptional => false;

		public bool IncludesSelf =>
			Type == AutofillType.Self ||
			Type == AutofillType.SelfAndChildren ||
			Type == AutofillType.SelfAndParent;

		public readonly bool AcceptFirstValidResult;
		public readonly bool AlwaysShowInInspector;
		public readonly bool AllowManualAssignment;

		public AutofillAttribute(
			AutofillType type = AutofillType.Self, 
			bool acceptFirstValidResult = false,
			bool alwaysShowInInspector = false,
			bool allowManualAssignment = false)
		{
			Type = type;
			AcceptFirstValidResult = acceptFirstValidResult;
			AlwaysShowInInspector = alwaysShowInInspector;
			AllowManualAssignment = allowManualAssignment;
		}
	}
}