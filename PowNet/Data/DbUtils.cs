namespace PowNet.Data
{
    /// <summary>
    /// Utility helpers for DB related dynamic operations.
    /// </summary>
    public static class DbUtils
    {
        public static string GenParamName(string source, string paramName, object? _)
        {
            if (string.IsNullOrWhiteSpace(paramName)) return source + "_p";
            return (source + "_" + paramName).Replace("__", "_");
        }
    }
}
