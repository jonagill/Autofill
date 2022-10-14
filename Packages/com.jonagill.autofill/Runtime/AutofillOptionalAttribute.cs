namespace Autofill
{
    /// <summary>
    /// Variant of AutofillAttribute that does not show an error if the component is not found.
    /// 
    /// Automatically gathers the serialized reference to a component on a prefab.
    /// Runs when the prefab saves or when the component is inspected.
    /// If the component is found, we hide the field to avoid cluttering the inspector.
    /// If the component is not found, we draw the empty field but do not print an error
    /// as this component is considered optional.
    /// Should be used instead of [HideInInspector] and [RequiredComponent]
    /// </summary>
    public class AutofillOptionalAttribute : AutofillAttribute
    {
        public AutofillOptionalAttribute(
            AutofillType type = AutofillType.Self,
            bool acceptFirstValidResult = false,
            bool alwaysShowInInspector = false) : base(type, acceptFirstValidResult, alwaysShowInInspector)
        {
        }

        public override bool IsOptional => true;
    }
}