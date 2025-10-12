using System.Text;
using PowNet.Common;

namespace PowNet.Services
{
    public class PowNetClassGenerator
    {
        private readonly string _className;
        private readonly string _namespaceName;

        public PowNetClassGenerator(string className, string namespaceName)
        {
            _className = className;
            _namespaceName = namespaceName;
            // Minimal default; caller can add more.
            Usings.Add("using System;");
        }

        /// <summary>
        /// Collection of using directives to include at the top of the generated file.
        /// Populate with full statements ending in semicolon (e.g. "using System.Text.Json;").
        /// </summary>
        public HashSet<string> Usings { get; } = new(StringComparer.Ordinal);

        public List<string> JqlModelMethods { get; set; } = [];
        public List<string> NotMappedMethods { get; set; } = [];
        public Dictionary<string, List<string>> DbProducerMethods { get; set; } = [];
        public Dictionary<string, List<string>> DbScalarFunctionMethods { get; set; } = [];
        public Dictionary<string, List<string>> DbTableFunctionMethods { get; set; } = [];

        /// <summary>
        /// Add a using directive (helper ensuring it ends with semicolon).
        /// </summary>
        public PowNetClassGenerator AddUsing(string usingDirective)
        {
            if (string.IsNullOrWhiteSpace(usingDirective)) return this;
            var trimmed = usingDirective.Trim();
            if (!trimmed.EndsWith(";")) trimmed += ";";
            if (!trimmed.StartsWith("using ")) trimmed = "using " + trimmed;
            Usings.Add(trimmed);
            return this;
        }

        public string ToCode()
        {
            StringBuilder methodsSB = new();
            foreach (var method in JqlModelMethods) methodsSB.Append(CSharpTemplates.JqlModelTemplate(method));
            foreach (var method in NotMappedMethods) methodsSB.Append(CSharpTemplates.NotMappedTemplate(method));
            foreach (var method in DbProducerMethods) methodsSB.Append(CSharpTemplates.DbProducerTemplate(method.Key, method.Value));
            foreach (var method in DbScalarFunctionMethods) methodsSB.Append(CSharpTemplates.DbScalarFunctionTemplate(method.Key, method.Value));
            foreach (var method in DbTableFunctionMethods) methodsSB.Append(CSharpTemplates.DbTableFunctionTemplate(method.Key, method.Value));

            var usingsBlock = string.Join('\n', Usings.Order());
            return CSharpTemplates.ClassTemplate
                .Replace("$Usings$", usingsBlock)
                .Replace("$Namespace$", _namespaceName)
                .Replace("$ClassName$", _className)
                .Replace("$Methods$", methodsSB.ToString());
        }
    }

    public class PowNetMethodGenerator(string methodName, MethodTemplate methodTemplate, List<string>? InputArgs = null)
    {
        public string MethodImplementation
        {
            get
            {
                if (methodTemplate == MethodTemplate.JqlMethod) return CSharpTemplates.JqlModelTemplate(methodName);
                if (methodTemplate == MethodTemplate.DbProducer) return CSharpTemplates.DbProducerTemplate(methodName, InputArgs);
                if (methodTemplate == MethodTemplate.DbScalarFunction) return CSharpTemplates.DbScalarFunctionTemplate(methodName, InputArgs);
                if (methodTemplate == MethodTemplate.DbTableFunction) return CSharpTemplates.DbTableFunctionTemplate(methodName, InputArgs);
                if (methodTemplate == MethodTemplate.NotMapped) return CSharpTemplates.NotMappedTemplate(methodName);
                return string.Empty;
            }
        }
    }

    internal static class CSharpTemplates
    {
        internal static string ClassTemplate => @"$Usings$

namespace $Namespace$
{
    public static class $ClassName$
    {
$Methods$
    }
}
";

        internal static string JqlModelTemplate(string MethodName)
        {
            return @"
        public static object? $MethodName$(System.Text.Json.JsonElement clientQuery, PowNetUser? actor)
        {
            // Implement dialog logic here (placeholder).
            return null;
        }
".Replace("$MethodName$", MethodName);
        }

        internal static string NotMappedTemplate(string MethodName)
        {
            return @"
        public static object? $MethodName$(PowNetUser? actor)
        {
            return true;
        }
".Replace("$MethodName$", MethodName);
        }

        internal static string DbProducerTemplate(string MethodName, List<string>? args)
        {
            string inputArgs = args == null ? string.Empty : string.Join(", ", args);
            inputArgs = inputArgs.Trim().Length == 0 ? "string dbConfigName" : "string dbConfigName, " + inputArgs;
            return @"
        public static object? $MethodName$($InputArgs$)
        {
            // Replace with actual stored procedure execution logic.
            return null;
        }
".Replace("$MethodName$", MethodName).Replace("$InputArgs$", inputArgs).Replace("$Args$", ArgsToSqlArgs(args));
        }

        internal static string DbScalarFunctionTemplate(string MethodName, List<string>? args)
        {
            string inputArgs = args == null ? string.Empty : string.Join(", ", args);
            inputArgs = inputArgs.Trim().Length == 0 ? "string dbConfigName" : "string dbConfigName, " + inputArgs;
            return @"
        public static object? $MethodName$($InputArgs$)
        {
            // Replace with actual scalar function call logic.
            return null;
        }
".Replace("$MethodName$", MethodName).Replace("$InputArgs$", inputArgs).Replace("$Args$", ArgsToSqlArgs(args));
        }

        internal static string DbTableFunctionTemplate(string MethodName, List<string>? args)
        {
            string inputArgs = args == null ? string.Empty : string.Join(", ", args);
            inputArgs = inputArgs.Trim().Length == 0 ? "string dbConfigName" : "string dbConfigName, " + inputArgs;
            return @"
        public static object? $MethodName$($InputArgs$)
        {
            // Replace with actual table function call logic.
            return null;
        }
".Replace("$MethodName$", MethodName).Replace("$InputArgs$", inputArgs).Replace("$Args$", ArgsToSqlArgs(args));
        }

        internal static string ArgsToSqlArgs(List<string>? args)
        {
            if (args is null || args.Count == 0) return string.Empty;
            List<string> sb = new();
            foreach (string s in args)
            {
                string[] argParts = s.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (argParts.Length < 2) continue;
                sb.Add("{" + argParts[1] + "}");
            }
            return string.Join(", ", sb);
        }
    }
}