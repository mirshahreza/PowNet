using System.Text;
using PowNet.Common;
using PowNet.Extensions;

namespace PowNet.Services
{
    public class PowNetClassGenerator(string className, string namespaceName)
    {
        private readonly string _tempBody = CSharpTemplates.ClassTemplate.Replace("$Namespace$", namespaceName).Replace("$ClassName$", className);

        public List<string> DbDialogMethods { get; set; } = [];
        public List<string> NotMappedMethods { get; set; } = [];
        public Dictionary<string, List<string>> DbProducerMethods { get; set; } = [];
        public Dictionary<string, List<string>> DbScalarFunctionMethods { get; set; } = [];
        public Dictionary<string, List<string>> DbTableFunctionMethods { get; set; } = [];

        public string ToCode()
        {
            StringBuilder methodsSB = new();
            foreach (var method in DbDialogMethods) methodsSB.Append(CSharpTemplates.DbDialogTemplate(method));
            foreach (var method in NotMappedMethods) methodsSB.Append(CSharpTemplates.NotMappedTemplate(method));
            foreach (var method in DbProducerMethods) methodsSB.Append(CSharpTemplates.DbProducerTemplate(method.Key, method.Value));
            foreach (var method in DbScalarFunctionMethods) methodsSB.Append(CSharpTemplates.DbScalarFunctionTemplate(method.Key, method.Value));
            foreach (var method in DbTableFunctionMethods) methodsSB.Append(CSharpTemplates.DbTableFunctionTemplate(method.Key, method.Value));
            return _tempBody.Replace("$Methods$", methodsSB.ToString());
        }
    }

    public class PowNetMethodGenerator(string methodName, MethodTemplate methodTemplate, List<string>? InputArgs = null)
    {
        public string MethodImplementation
        {
            get
            {
                if (methodTemplate == MethodTemplate.DbDialog) return CSharpTemplates.DbDialogTemplate(methodName);
                if (methodTemplate == MethodTemplate.DbProducer) return CSharpTemplates.DbProducerTemplate(methodName, InputArgs);
                if (methodTemplate == MethodTemplate.DbScalarFunction) return CSharpTemplates.DbScalarFunctionTemplate(methodName, InputArgs);
                if (methodTemplate == MethodTemplate.DbTableFunction) return CSharpTemplates.DbTableFunctionTemplate(methodName, InputArgs);
                if (methodTemplate == MethodTemplate.NotMapped) return CSharpTemplates.NotMappedTemplate(methodName);
                return "";
            }
        }
    }

    internal static class CSharpTemplates
    {
        internal static string ClassTemplate => @"
using System;
using System.Text.Json;
using PowNet;
using ServIo;

namespace $Namespace$
{
    public static class $ClassName$
    {
$Methods$
    }
}
";

        internal static string DbDialogTemplate(string MethodName)
        {
            return @"
        public static object? $MethodName$(JsonElement clientQuery, PowNetUser? actor)
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
            string inputArgs = args == null ? "" : string.Join(", ", args);
            inputArgs = inputArgs.Trim().Length == 0 ? "string dbConfigName" : "string dbConfigName," + inputArgs;
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
            string inputArgs = args == null ? "" : string.Join(", ", args);
            inputArgs = inputArgs.Trim().Length == 0 ? "string dbConfigName" : "string dbConfigName," + inputArgs;
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
            string inputArgs = args == null ? "" : string.Join(", ", args);
            inputArgs = inputArgs.Trim().Length == 0 ? "string dbConfigName" : "string dbConfigName," + inputArgs;
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
            if (args is null || args.Count == 0) return "";
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