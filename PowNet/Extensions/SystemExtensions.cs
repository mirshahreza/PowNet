using System.Reflection;

namespace PowNet.Extensions
{
    public static class SystemExtensions
    {
        public static string GetFullName(this MethodInfo methodInfo)
        {
            return
                methodInfo.DeclaringType is null
                ?
                methodInfo.Name
                :
                methodInfo.DeclaringType.Namespace + "." + methodInfo.DeclaringType.Name + "." + methodInfo.Name
                ;
        }

        public static MethodInfo[] GetMethodsReal(this Type type)
        {
            return type.GetMethods().Where(m => !m.Name.Equals("GetType") && !m.Name.Equals("ToString") && !m.Name.Equals("Equals") && !m.Name.Equals("GetHashCode")).ToArray();
        }

        public static bool IsRealType(string typeName)
        {
            return true;
            
            //ApiFileAddress? codeMap = DynaCode.CodeMaps.FirstOrDefault(i => DynaCode.MethodPartsNames(i.MethodFullName).Item2 == typeName);
            //return codeMap != null;
        }

        public static Type[] GetTypesReal(this Assembly asm)
        {
            return asm.GetTypes().Where(i => !i.Name.ContainsIgnoreCase("EmbeddedAttribute") && !i.Name.ContainsIgnoreCase("RefSafetyRulesAttribute")).ToArray();
        }

        public static string GetPlaceInfo(this MethodBase? methodBase)
        {
            if (methodBase == null) return "";
            return $"{methodBase?.DeclaringType?.Name}, {methodBase?.Name}";
        }
    }
}