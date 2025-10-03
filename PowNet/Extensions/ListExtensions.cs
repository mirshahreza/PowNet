namespace PowNet.Extensions
{
    public static class ListExtensions
    {
        public static bool TryAdd(this List<object>? list, object o, bool addNull = true)
        {
            if (list is null) list = [];
            if (addNull is false && o is null) return false;
            if (list.Contains(o)) return false;
            list.Add(o);
            return true;
        }

        public static bool TryAdd(this List<string>? list, string o, bool addNull = true)
        {
            if (list is null) list = [];
            if (addNull is false && o is null) return false;
            if (list.Contains(o)) return false;
            list.Add(o);
            return true;
        }

        public static bool ContainsIgnoreCase(this List<string>? list, string? testString)
        {
            if (list is null) return false;
            if (testString is null || testString == "") return false;
            foreach (string str in list)
                if (str.Equals(testString, StringComparison.CurrentCultureIgnoreCase)) return true;
            return false;
        }

        public static bool HasIntersect(this List<string>? l1, string[]? l2)
        {
            if (l2 is null || l2.Length == 0) return false;
            if (l1 is null || l1.Count == 0) return false;

            // Use HashSet for O(1) lookups instead of O(n) for large collections
            if (l2.Length > 10)
            {
                var hashSet = new HashSet<string>(l2, StringComparer.OrdinalIgnoreCase);
                return l1.Any(str => hashSet.Contains(str));
            }

            // For small collections, direct iteration is faster
            foreach (string str in l1)
            {
                if (l2.Any(s => s.Equals(str, StringComparison.OrdinalIgnoreCase))) return true;
            }
            return false;
        }

        public static bool HasIntersect(this List<int>? l1, int[]? l2)
        {
            if (l2 is null || l2.Length == 0) return false;
            if (l1 is null || l1.Count == 0) return false;

            // Use HashSet for O(1) lookups for large collections
            if (l2.Length > 10)
            {
                var hashSet = new HashSet<int>(l2);
                return l1.Any(i => hashSet.Contains(i));
            }

            // For small collections, direct iteration is faster
            foreach (int i in l1)
            {
                if (l2.Contains(i)) return true;
            }
            return false;
        }

        public static bool HasIntersect(this List<int>? l1, List<int>? l2)
        {
            if (l2 is null || l2.Count == 0) return false;
            if (l1 is null || l1.Count == 0) return false;

            // Use the smaller list for HashSet to minimize memory usage
            if (l1.Count > l2.Count)
            {
                var hashSet = new HashSet<int>(l2);
                return l1.Any(i => hashSet.Contains(i));
            }
            else
            {
                var hashSet = new HashSet<int>(l1);
                return l2.Any(i => hashSet.Contains(i));
            }
        }
    }
}