using System;

namespace Autofill
{
    public enum AutofillUpdateResult
    {
        Unchanged = 0,
        Updated = 1,

        // Negative codes indicate errors
        Error_NoValidComponentFound = -1,
        Error_MultipleComponentsFound = -2,
        Error_PropertyIsArray = -3,
        Error_InvalidType = -4,
    }

    public static class AutofillAutofillUpdateResultExt
    {
        public static bool IsError(this AutofillUpdateResult result)
        {
            return result < 0;
        }
        
        public static string ToErrorString(this AutofillUpdateResult result)
        {
            switch (result)
            {
                case AutofillUpdateResult.Unchanged:
                case AutofillUpdateResult.Updated:
                    return string.Empty;
                case AutofillUpdateResult.Error_MultipleComponentsFound:
                    return "Multiple valid components found. Set this field manually.";
                case AutofillUpdateResult.Error_NoValidComponentFound:
                    return "No valid component found.";
                case AutofillUpdateResult.Error_InvalidType:
                    return "Only component types can be autofilled.";
                case AutofillUpdateResult.Error_PropertyIsArray:
                    return "Arrays and lists cannot be autofilled.";
                default:
                    throw new ArgumentException($"Unknown error type: {result}");
            }
        }
    }
}