using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using PowNet.Configuration; // reference a known public type to force assembly load

namespace PowNet.DocGen
{
    internal static class Program
    {
        private record MethodDoc(string Anchor, string Name, string Signature, string Summary, string ReturnType, bool IsExtension, ParameterInfo[] Parameters, int OverloadIndex);

        static void Main()
        {
            Run();
            Console.WriteLine("Documentation generation completed.");
        }

        private static void Run()
        {
            var asm = typeof(PowNetConfiguration).Assembly;
            var baseDir = FindRepoRoot();
            var docDir = Path.Combine(baseDir, "Documentation");
            Directory.CreateDirectory(docDir);

            var types = asm.GetTypes()
                .Where(t => t.IsPublic && t.Namespace != null && t.Namespace.StartsWith("PowNet"))
                .OrderBy(t => t.Namespace).ThenBy(t => t.Name)
                .ToList();

            foreach (var t in types)
            {
                try
                {
                    WriteTypeDoc(docDir, t);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Failed documenting {t.FullName}: {ex.Message}");
                }
            }
        }

        private static void WriteTypeDoc(string docDir, Type t)
        {
            var methods = CollectMethodDocs(t).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("# " + t.Name);
            sb.AppendLine();
            sb.AppendLine(GenerateTypeOverview(t));
            sb.AppendLine();
            sb.AppendLine("## Overview");
            sb.AppendLine($"This document describes the public API surface of `{t.Name}`. Each method section includes signature, parameter list, return type, and a usage snippet.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("## Methods");
            if (!methods.Any())
            {
                sb.AppendLine();
                sb.AppendLine("(No public methods exposed on this type.)");
            }
            else
            {
                foreach (var m in methods)
                {
                    sb.AppendLine();
                    sb.AppendLine($"### {m.Name}{FormatParametersInline(m.Parameters)}");
                    sb.AppendLine();
                    sb.AppendLine($"Signature: `{m.Signature}`");
                    sb.AppendLine();
                    sb.AppendLine(m.Summary);
                    sb.AppendLine();
                    if (m.Parameters.Length > 0)
                    {
                        sb.AppendLine("Parameters:");
                        foreach (var p in m.Parameters)
                        {
                            var opt = p.IsOptional ? " (optional)" : string.Empty;
                            sb.AppendLine($"- `{p.Name}`: `{FriendlyType(p.ParameterType)}`{opt}");
                        }
                        sb.AppendLine();
                    }
                    sb.AppendLine($"Returns: `{m.ReturnType}`");
                    sb.AppendLine();
                    sb.AppendLine("Example:");
                    sb.AppendLine("```csharp");
                    sb.AppendLine(GenerateUsageSnippet(t, m));
                    sb.AppendLine("```");
                }
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("*Auto-generated. Enhance summaries by adding XML documentation comments in source.*");

            var file = Path.Combine(docDir, t.Name + ".md");
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        }

        private static IEnumerable<MethodDoc> CollectMethodDocs(Type t)
        {
            var raw = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToList();

            var groups = raw.GroupBy(m => m.Name);
            foreach (var g in groups)
            {
                int idx = 1;
                foreach (var m in g)
                {
                    var isExt = m.IsDefined(typeof(ExtensionAttribute), false);
                    var pars = m.GetParameters();
                    yield return new MethodDoc(
                        Anchor: g.Key + (g.Count() > 1 ? idx.ToString() : string.Empty),
                        Name: g.Key,
                        Signature: BuildSignature(m),
                        Summary: BuildSummary(m),
                        ReturnType: FriendlyType(m.ReturnType),
                        IsExtension: isExt,
                        Parameters: pars,
                        OverloadIndex: idx++
                    );
                }
            }
        }

        private static string BuildSignature(MethodInfo m)
        {
            var pars = string.Join(", ", m.GetParameters().Select(p => FriendlyType(p.ParameterType) + " " + p.Name + (p.IsOptional ? " = ..." : string.Empty)));
            return $"{FriendlyType(m.ReturnType)} {m.Name}({pars})";
        }

        private static string BuildSummary(MethodInfo m)
        {
            var ext = m.IsDefined(typeof(ExtensionAttribute), false) ? " Extension method." : string.Empty;
            return $"Autogenerated summary for `{m.DeclaringType!.Name}.{m.Name}`.{ext}";
        }

        private static string GenerateTypeOverview(Type t)
        {
            var kind = t.IsSealed && t.IsAbstract ? "static class" : t.IsClass ? "class" : t.IsEnum ? "enum" : t.IsInterface ? "interface" : "type";
            return $"**Type:** `{t.FullName}`  "+Environment.NewLine+
                   $"**Kind:** {kind}  "+Environment.NewLine+
                   $"**Assembly:** {t.Assembly.GetName().Name}";
        }

        private static string GenerateUsageSnippet(Type t, MethodDoc m)
        {
            var sb = new StringBuilder();
            var firstIsExtTarget = m.IsExtension && m.Parameters.Length > 0;
            string targetVar = string.Empty;
            if (firstIsExtTarget)
            {
                var targetType = m.Parameters[0].ParameterType;
                targetVar = Camel(targetType.Name.Replace("`1", ""));
                sb.AppendLine($"var {targetVar} = default({FriendlyType(targetType)}); // TODO initialize");
            }

            var argList = new List<string>();
            var start = firstIsExtTarget ? 1 : 0;
            for (int i = start; i < m.Parameters.Length; i++)
            {
                var p = m.Parameters[i];
                var varName = Camel(p.Name ?? $"arg{i}");
                sb.AppendLine($"var {varName} = default({FriendlyType(p.ParameterType)}); // TODO assign");
                argList.Add(varName);
            }

            var call = firstIsExtTarget
                ? $"{targetVar}.{m.Name}({string.Join(", ", argList)})"
                : (t.IsSealed && t.IsAbstract ? $"{t.Name}.{m.Name}({string.Join(", ", argList)})" : $"new {t.Name}().{m.Name}({string.Join(", ", argList)})");

            if (m.ReturnType != "void") sb.AppendLine($"var result = {call};"); else sb.AppendLine(call + ";");
            return sb.ToString();
        }

        private static string FriendlyType(Type t)
        {
            if (t == typeof(void)) return "void";
            if (t.IsGenericType)
            {
                var name = t.Name.Split('`')[0];
                var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyType));
                return name + "<" + args + ">";
            }
            if (t.IsArray) return FriendlyType(t.GetElementType()!) + "[]";
            return t.Name;
        }

        private static string FormatParametersInline(ParameterInfo[] pars)
        {
            return "(" + string.Join(", ", pars.Select(p => p.Name)) + ")";
        }

        private static string Camel(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.Length == 1) return name.ToLowerInvariant();
            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        private static string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                if (Directory.GetFiles(dir, "PowNet.sln").Any()) return dir;
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return AppContext.BaseDirectory;
        }
    }
}