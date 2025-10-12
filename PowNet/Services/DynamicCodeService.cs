using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.ComponentModel;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PowNet.Configuration;
using PowNet.Extensions;
using PowNet.Common;
using PowNet.Models;

namespace PowNet.Services
{
    public static class DynamicCodeService
    {
        private static IEnumerable<SyntaxTree> GetEntireCodeSyntaxes()
        {
            List<SourceCode> sourceCodes = GetAllSourceCodes();
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp13);
            IEnumerable<SyntaxTree> entireCodeSyntaxes = sourceCodes.Select(sourceCode => SyntaxFactory.ParseSyntaxTree(sourceCode.RawCode, options, sourceCode.FilePath));
            return entireCodeSyntaxes;
        }

        private static string[]? scriptFiles;
        public static string[] ScriptFiles
        {
            get
            {
                scriptFiles ??= [.. new DirectoryInfo(PowNetConfiguration.ServerPath).GetFilesRecursive("*.cs")];
                return scriptFiles;
            }
        }

        private static string? asmName;
        private static string AsmName
        {
            get
            {
                asmName ??= $"DynaAsm{Guid.NewGuid().ToString().Replace("-", "")}.dll";
                return asmName;
            }
        }

        private static string? asmPath;
        public static string AsmPath
        {
            get
            {
                asmPath ??= $"{PowNetConfiguration.PluginsPath}/{AsmName}";
                return asmPath;
            }
        }

        private static Assembly? dynaAsm;
        public static Assembly DynaAsm
        {
            get
            {
                if (dynaAsm == null)
                {
                    if (!File.Exists(AsmPath)) Build();
                    dynaAsm = Assembly.LoadFrom(AsmPath);
                }
                return dynaAsm;
            }
        }

        private static List<ApiFileAddress>? codeMaps;
        public static List<ApiFileAddress> CodeMaps
        {
            get
            {
                codeMaps ??= GenerateSourceCodeMap();
                return codeMaps;
            }
        }

        public static string GetMethodFilePath(string methodFullName)
        {
            ApiFileAddress? codeMap = CodeMaps.FirstOrDefault(cm => cm.MethodFullName.EqualsIgnoreCase(methodFullName));
            return codeMap is null
                ? throw new PowNetException($"MethodDoesNotExist : [ {methodFullName} ]", System.Reflection.MethodBase.GetCurrentMethod())
                    .AddParam("MethodFullName", methodFullName)
                    .GetEx() : codeMap.FilePath;
        }

        public static string GetClassFilePath(string typeFullName)
        {
            ApiFileAddress? codeMap = CodeMaps.FirstOrDefault(cm => cm.FilePath.EndsWithIgnoreCase(typeFullName + ".cs"));
            return codeMap is null
                ? throw new PowNetException("ClassFileDoesNotExist", System.Reflection.MethodBase.GetCurrentMethod())
                    .AddParam("TypeFullName", typeFullName)
                    .GetEx() : codeMap.FilePath;
        }

        public static string TryGetClassFilePath(string typeFullName)
        {
            ApiFileAddress? codeMap = CodeMaps.FirstOrDefault(cm => cm.FilePath.EndsWithIgnoreCase(typeFullName + ".cs"));
            if (codeMap is null) return "";
            return codeMap.FilePath;
        }

        public static bool MethodExist(string methodFullName)
        {
            var parts = MethodPartsNames(methodFullName);
            string filePath = TryGetClassFilePath(methodFullName);
            if (filePath.IsNullOrEmpty()) return false;
            string fileBody = File.ReadAllText(filePath);
            fileBody = fileBody.Replace(" ", "");
            return fileBody.Contains($"publicstaticobject?{parts.Item3}(");
        }

        public static void CreateMethod(string methodFullName, string methodName, MethodTemplate methodTemplate = MethodTemplate.JqlMethod)
        {
            string? filePath = GetClassFilePath(methodFullName);
            string controllerBody = File.ReadAllText(filePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(controllerBody);
            string mBody = new PowNetMethodGenerator(methodName, methodTemplate).MethodImplementation;
            MethodDeclarationSyntax method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Last();
            string m = method.GetText().ToString();
            TextChange tc = new(method.Span, $"{m.Trim()}{Environment.NewLine}{Environment.NewLine}{mBody}");
            controllerBody = tree.GetText().WithChanges(tc).ToString().RemoveWhitelines();
            File.WriteAllText(filePath, controllerBody);
            Refresh();
        }

        public static void RemoveMethod(string methodFullName)
        {
            var parts = MethodPartsNames(methodFullName);
            string classFullName = methodFullName.Replace($".{parts.Item3}", "");
            string methodName = parts.Item3;
            string? filePath = GetMethodFilePath(methodFullName) ?? throw new PowNetException("MethodFullNameDoesNotExist", System.Reflection.MethodBase.GetCurrentMethod())
                    .AddParam("MethodFullName", methodFullName)
                    .GetEx();
            string controllerBody = File.ReadAllText(filePath);

            SyntaxTree tree = CSharpSyntaxTree.ParseText(controllerBody);
            MethodDeclarationSyntax method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First(m => m.Identifier.ToString() == methodName);

            TextChange tc = new(method.Span, string.Empty);
            controllerBody = tree.GetText().WithChanges(tc).ToString().RemoveWhitelines();
            File.WriteAllText(filePath, controllerBody);
            Refresh();
        }

        public static void Build()
        {
            Refresh();
            using var mStream = new MemoryStream();

            var compileRefs = GetCompilationReferences();
            var compilerOptions = new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);
            CSharpCompilation cSharpCompilation = CSharpCompilation.Create(AsmName, GetEntireCodeSyntaxes(), compileRefs, compilerOptions);

            var result = cSharpCompilation.Emit(mStream);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                var error = failures.FirstOrDefault();
                throw new PowNetException($"{error?.Id}: {error?.GetMessage()}", MethodBase.GetCurrentMethod()).GetEx();
            }

            mStream.Seek(0, SeekOrigin.Begin);
            byte[] dllBytes = mStream.ToArray();

            File.WriteAllBytes(AsmPath, dllBytes);
            Thread.Sleep(100);
            _ = DynaAsm;
        }

        public static void Refresh()
        {
            string[] oldAsmFiles = Directory.GetFiles(PowNetConfiguration.PluginsPath, "DynaAsm*");
            foreach (string oldAsmFile in oldAsmFiles) IOExtensions.TryDelete(oldAsmFile);
            scriptFiles = null;
            asmPath = null;
            dynaAsm = null;
            codeMaps = null;
            asmName = null;
        }

        private static List<SourceCode> GetAllSourceCodes()
        {
            List<SourceCode> sourceCodes = [];
            foreach (string f in ScriptFiles) sourceCodes.Add(new(f, File.ReadAllText(f)));
            return sourceCodes;
        }

        private static List<MetadataReference> GetCompilationReferences()
        {
            var references = new List<MetadataReference>();

            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    AddReferencesFor(a, references);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            AddReferencesFor(Assembly.GetExecutingAssembly(), references);
            AddReferencesFor(Assembly.GetEntryAssembly(), references);
            AddReferencesFor(Assembly.GetCallingAssembly(), references);

            AddReferencesFor(typeof(object).Assembly, references);
            AddReferencesFor(typeof(TypeConverter).Assembly, references);
            AddReferencesFor(Assembly.Load("netstandard, Version=2.1.0.0"), references);
            AddReferencesFor(typeof(System.Linq.Expressions.Expression).Assembly, references);
            AddReferencesFor(typeof(System.Text.Encodings.Web.JavaScriptEncoder).Assembly, references);
            AddReferencesFor(typeof(Exception).Assembly, references);
            AddReferencesFor(typeof(PowNetException).Assembly, references);
            AddReferencesFor(typeof(ArgumentNullException).Assembly, references);

            return references;
        }

        private static void AddReferencesFor(Assembly? asm, List<MetadataReference> references)
        {
            if (asm is null || !File.Exists(asm.Location)) return;
            references.Add(MetadataReference.CreateFromFile(asm.Location));
            var rfs = asm.GetReferencedAssemblies();
            foreach (var a in rfs)
            {
                var asmF = Assembly.Load(a);
                if (asmF is null) continue;
                if (File.Exists(asmF.Location)) references.Add(MetadataReference.CreateFromFile(asmF.Location));
            }
        }

        private static List<ApiFileAddress> GenerateSourceCodeMap()
        {
            List<ApiFileAddress> codeMaps = [];
            foreach (var st in GetEntireCodeSyntaxes())
            {
                var members = st.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>();
                foreach (var member in members)
                {
                    if (member is MethodDeclarationSyntax method)
                    {
                        string nsn = "";
                        SyntaxNode? parentClass = method.Parent as ClassDeclarationSyntax;
                        SyntaxNode? parentNameSpace = parentClass?.Parent;
                        if (parentNameSpace is not null) nsn = ((NamespaceDeclarationSyntax)parentNameSpace).Name.ToString() + ".";
                        string tn = parentClass is null ? "" : ((ClassDeclarationSyntax)parentClass).Identifier.ValueText + ".";
                        string mn = method.Identifier.ValueText;
                        codeMaps.Add(new(nsn + tn + mn, st.FilePath));
                    }
                }
            }
            return codeMaps;
        }

        public static Tuple<string?, string, string> MethodPartsNames(string methodFullPath)
        {
            if (methodFullPath.Trim() == "") throw new PowNetException("MethodFullPathCanNotBeEmpty", System.Reflection.MethodBase.GetCurrentMethod())
                            .GetEx();
            string[] parts = methodFullPath.Trim().Split('.');
            if (parts.Length < 2 || parts.Length > 3) throw new PowNetException($"MethodMustContainsAtLeast2PartsSeparatedByDot", System.Reflection.MethodBase.GetCurrentMethod())
                    .AddParam("MethodFullPath", methodFullPath)
                    .GetEx();
            return parts.Length == 3 ? new(parts[0], parts[1], parts[2]) : new(null, parts[0], parts[1]);
        }
    }
}