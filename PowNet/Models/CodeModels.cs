namespace PowNet.Models
{
    /// <summary>
    /// Represents an API file address for dynamic code mapping
    /// </summary>
    public class ApiFileAddress
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string MethodFullName { get; set; } = string.Empty;
        public List<string> Methods { get; set; } = new();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        public ApiFileAddress() { }

        public ApiFileAddress(string methodFullName, string filePath)
        {
            MethodFullName = methodFullName;
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
        }
    }

    /// <summary>
    /// Represents source code information
    /// </summary>
    public class SourceCode
    {
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string RawCode { get; set; } = string.Empty;
        public string Language { get; set; } = "csharp";
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public List<string> Dependencies { get; set; } = new();

        public SourceCode() { }

        public SourceCode(string filePath, string content)
        {
            FilePath = filePath;
            Content = content;
            RawCode = content;
            LastModified = File.GetLastWriteTime(filePath);
        }
    }
}