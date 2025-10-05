using PowNet.Common;

namespace PowNet.Data
{
    /// <summary>
    /// Comparison clause used for dynamic where generation.
    /// </summary>
    public class CompareClause
    {
        public CompareOperator Operator { get; set; } = CompareOperator.Equal;
    }

    /// <summary>
    /// Lightweight DB parameter description used for code generation or mapping.
    /// </summary>
    public class DbParam
    {
        public string Name { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
    }
}
