namespace CableTrayHandler.Utilities
{
    public static class FieldTypeHelper
    {
        public static string GetTypeHint(string drofusFieldName)
        {
            // Simple heuristics to guess field types based on common naming patterns
            var fieldLower = drofusFieldName.ToLower();
            
            if (fieldLower.Contains("_id") || fieldLower.Contains("quantity") || fieldLower.Contains("count"))
                return "integer";
            if (fieldLower.Contains("length") || fieldLower.Contains("width") || fieldLower.Contains("height") || 
                fieldLower.Contains("area") || fieldLower.Contains("volume") || fieldLower.Contains("_double"))
                return "decimal number";
            if (fieldLower.Contains("date") || fieldLower.Contains("time"))
                return "date/time";
            
            return "text or number";
        }
    }

    public static class ValidationHelper
    {
        public static bool IsValidNumericValue(string value)
        {
            // Check if the value can be parsed as a number (int or double)
            return double.TryParse(value, out _);
        }
    }
}