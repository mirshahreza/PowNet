using System.Collections.Concurrent;
using System.Reflection;
using System.Linq.Expressions;
using PowNet.Common;
using PowNet.Logging;

namespace PowNet.Extensions
{
    /// <summary>
    /// Advanced data mapping and transformation extensions for PowNet framework
    /// </summary>
    public static class DataMapperExtensions
    {
        private static readonly Logger _logger = PowNetLogger.GetLogger("DataMapper");
        private static readonly ConcurrentDictionary<string, Delegate> _mapperCache = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

        #region Smart Mapping

        /// <summary>
        /// Smart map between objects with automatic property matching
        /// </summary>
        public static TDestination SmartMap<TSource, TDestination>(this TSource source, 
            TDestination? destination = default,
            MappingOptions? options = null) 
            where TDestination : new()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            options ??= MappingOptions.Default;
            destination ??= new TDestination();

            var cacheKey = $"{typeof(TSource).FullName}->{typeof(TDestination).FullName}";
            
            if (!_mapperCache.TryGetValue(cacheKey, out var mapper))
            {
                mapper = CreateMapper<TSource, TDestination>(options);
                _mapperCache.TryAdd(cacheKey, mapper);
            }

            return ((Func<TSource, TDestination, TDestination>)mapper)(source, destination);
        }

        /// <summary>
        /// Map collection with parallel processing
        /// </summary>
        public static List<TDestination> SmartMapCollection<TSource, TDestination>(
            this IEnumerable<TSource> source,
            MappingOptions? options = null)
            where TDestination : new()
        {
            if (source == null)
                return new List<TDestination>();

            options ??= MappingOptions.Default;

            if (options.UseParallelProcessing && source.Count() > options.ParallelThreshold)
            {
                return source.AsParallel()
                    .WithDegreeOfParallelism(options.MaxDegreeOfParallelism)
                    .Select(item => item.SmartMap<TSource, TDestination>(options: options))
                    .ToList();
            }

            return source.Select(item => item.SmartMap<TSource, TDestination>(options: options)).ToList();
        }

        /// <summary>
        /// Create custom mapping configuration
        /// </summary>
        public static MappingConfiguration<TSource, TDestination> CreateMapping<TSource, TDestination>()
            where TDestination : new()
        {
            return new MappingConfiguration<TSource, TDestination>();
        }

        #endregion

        #region Deep Mapping

        /// <summary>
        /// Deep clone object with customizable depth
        /// </summary>
        public static T DeepClone<T>(this T source, int maxDepth = 10, CloneOptions? options = null)
        {
            if (source == null)
                return default!;

            options ??= CloneOptions.Default;
            return (T)DeepCloneInternal(source, maxDepth, 0, new Dictionary<object, object>(), options);
        }

        /// <summary>
        /// Merge multiple objects into one
        /// </summary>
        public static T MergeObjects<T>(this T target, params object[] sources) where T : new()
        {
            if (target == null)
                target = new T();

            var targetProperties = GetCachedProperties(typeof(T));

            foreach (var source in sources.Where(s => s != null))
            {
                var sourceProperties = GetCachedProperties(source.GetType());

                foreach (var sourceProp in sourceProperties)
                {
                    var targetProp = targetProperties.FirstOrDefault(p => 
                        p.Name.Equals(sourceProp.Name, StringComparison.OrdinalIgnoreCase) &&
                        p.CanWrite && sourceProp.CanRead);

                    if (targetProp != null)
                    {
                        try
                        {
                            var value = sourceProp.GetValue(source);
                            if (value != null)
                            {
                                if (targetProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                                {
                                    targetProp.SetValue(target, value);
                                }
                                else
                                {
                                    var convertedValue = ConvertValue(value, targetProp.PropertyType);
                                    targetProp.SetValue(target, convertedValue);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to merge property {PropertyName}: {Error}", 
                                sourceProp.Name, ex.Message);
                        }
                    }
                }
            }

            return target;
        }

        #endregion

        #region Flattening & Projection

        /// <summary>
        /// Flatten nested object properties
        /// </summary>
        public static Dictionary<string, object?> Flatten(this object source, 
            string separator = ".", int maxDepth = 5)
        {
            if (source == null)
                return new Dictionary<string, object?>();

            var result = new Dictionary<string, object?>();
            FlattenInternal(source, string.Empty, result, separator, maxDepth, 0);
            return result;
        }

        /// <summary>
        /// Create projection with selective property mapping
        /// </summary>
        public static TProjection Project<TSource, TProjection>(this TSource source,
            Expression<Func<TSource, TProjection>> projectionExpression)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var compiled = projectionExpression.Compile();
            return compiled(source);
        }

        /// <summary>
        /// Dynamic property selector
        /// </summary>
        public static object? GetNestedProperty(this object source, string propertyPath)
        {
            if (source == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var current = source;
            var properties = propertyPath.Split('.');

            foreach (var property in properties)
            {
                if (current == null)
                    return null;

                var propInfo = current.GetType().GetProperty(property, 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (propInfo == null)
                    return null;

                current = propInfo.GetValue(current);
            }

            return current;
        }

        /// <summary>
        /// Set nested property value
        /// </summary>
        public static bool SetNestedProperty(this object target, string propertyPath, object? value)
        {
            if (target == null || string.IsNullOrEmpty(propertyPath))
                return false;

            var properties = propertyPath.Split('.');
            var current = target;

            // Navigate to parent object
            for (int i = 0; i < properties.Length - 1; i++)
            {
                var propInfo = current.GetType().GetProperty(properties[i], 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (propInfo == null)
                    return false;

                var propValue = propInfo.GetValue(current);
                if (propValue == null)
                {
                    // Try to create instance if possible
                    if (propInfo.PropertyType.GetConstructor(Type.EmptyTypes) != null)
                    {
                        propValue = Activator.CreateInstance(propInfo.PropertyType);
                        propInfo.SetValue(current, propValue);
                    }
                    else
                    {
                        return false;
                    }
                }

                current = propValue!;
            }

            // Set final property
            var finalProp = current.GetType().GetProperty(properties.Last(), 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (finalProp == null || !finalProp.CanWrite)
                return false;

            try
            {
                var convertedValue = ConvertValue(value, finalProp.PropertyType);
                finalProp.SetValue(current, convertedValue);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to set property {PropertyPath}: {Error}", propertyPath, ex.Message);
                return false;
            }
        }

        #endregion

        #region Data Transformation

        /// <summary>
        /// Transform data using custom transformation rules
        /// </summary>
        public static TResult Transform<TSource, TResult>(this TSource source,
            params DataTransformationRule<TSource, TResult>[] rules)
            where TResult : new()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = new TResult();

            foreach (var rule in rules)
            {
                try
                {
                    rule.Apply(source, result);
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Transformation rule failed: {RuleName}", rule.Name);
                    if (rule.IsRequired)
                        throw;
                }
            }

            return result;
        }

        /// <summary>
        /// Conditional mapping based on predicate
        /// </summary>
        public static TDestination MapIf<TSource, TDestination>(this TSource source,
            Func<TSource, bool> condition,
            Func<TSource, TDestination> trueMapping,
            Func<TSource, TDestination>? falseMapping = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (condition(source))
            {
                return trueMapping(source);
            }

            if (falseMapping != null)
            {
                return falseMapping(source);
            }

            return default!;
        }

        /// <summary>
        /// Aggregate multiple mappings
        /// </summary>
        public static TDestination AggregateMap<TSource, TDestination>(this IEnumerable<TSource> sources,
            Func<TSource, TDestination> mapper,
            Func<IEnumerable<TDestination>, TDestination> aggregator)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));

            var mappedResults = sources.Select(mapper);
            return aggregator(mappedResults);
        }

        #endregion

        #region Private Helper Methods

        private static Func<TSource, TDestination, TDestination> CreateMapper<TSource, TDestination>(MappingOptions options)
        {
            return (source, destination) =>
            {
                var sourceProps = GetCachedProperties(typeof(TSource));
                var destProps = GetCachedProperties(typeof(TDestination));

                foreach (var sourceProp in sourceProps.Where(p => p.CanRead))
                {
                    var destProp = destProps.FirstOrDefault(p => 
                        options.PropertyMatcher(sourceProp, p) && p.CanWrite);

                    if (destProp != null)
                    {
                        try
                        {
                            var value = sourceProp.GetValue(source);
                            if (value != null || options.MapNullValues)
                            {
                                var convertedValue = ConvertValue(value, destProp.PropertyType);
                                destProp.SetValue(destination, convertedValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (options.ThrowOnMappingError)
                                throw;
                            
                            _logger.LogWarning("Mapping failed for property {PropertyName}: {Error}", 
                                sourceProp.Name, ex.Message);
                        }
                    }
                }

                return destination;
            };
        }

        private static PropertyInfo[] GetCachedProperties(Type type)
        {
            return _propertyCache.GetOrAdd(type, t => 
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType.IsEnum)
            {
                return Enum.Parse(underlyingType, value.ToString()!);
            }

            return Convert.ChangeType(value, underlyingType);
        }

        private static object DeepCloneInternal(object source, int maxDepth, int currentDepth, 
            Dictionary<object, object> visited, CloneOptions options)
        {
            if (source == null || currentDepth >= maxDepth)
                return source!;

            if (visited.TryGetValue(source, out var existingClone))
                return existingClone;

            var type = source.GetType();

            // Handle primitives and strings
            if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || 
                type == typeof(decimal) || type == typeof(Guid))
            {
                return source;
            }

            // Handle arrays
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var sourceArray = (Array)source;
                var clonedArray = Array.CreateInstance(elementType, sourceArray.Length);
                visited[source] = clonedArray;

                for (int i = 0; i < sourceArray.Length; i++)
                {
                    var element = sourceArray.GetValue(i);
                    clonedArray.SetValue(DeepCloneInternal(element!, maxDepth, currentDepth + 1, visited, options), i);
                }

                return clonedArray;
            }

            // Handle generic collections
            if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            {
                return CloneCollection(source, maxDepth, currentDepth, visited, options);
            }

            // Handle objects
            var clone = Activator.CreateInstance(type);
            visited[source] = clone!;

            var properties = GetCachedProperties(type);
            foreach (var prop in properties.Where(p => p.CanRead && p.CanWrite))
            {
                if (options.IgnoredProperties.Contains(prop.Name))
                    continue;

                var value = prop.GetValue(source);
                if (value != null)
                {
                    var clonedValue = DeepCloneInternal(value, maxDepth, currentDepth + 1, visited, options);
                    prop.SetValue(clone, clonedValue);
                }
            }

            return clone!;
        }

        private static object CloneCollection(object source, int maxDepth, int currentDepth,
            Dictionary<object, object> visited, CloneOptions options)
        {
            var sourceType = source.GetType();
            var genericArgs = sourceType.GetGenericArguments();

            if (sourceType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var listType = typeof(List<>).MakeGenericType(genericArgs);
                var clonedList = (System.Collections.IList)Activator.CreateInstance(listType)!;
                visited[source] = clonedList;

                foreach (var item in (System.Collections.IEnumerable)source)
                {
                    var clonedItem = DeepCloneInternal(item, maxDepth, currentDepth + 1, visited, options);
                    clonedList.Add(clonedItem);
                }

                return clonedList;
            }

            // Handle other collection types...
            return source;
        }

        private static void FlattenInternal(object source, string prefix, Dictionary<string, object?> result,
            string separator, int maxDepth, int currentDepth)
        {
            if (source == null || currentDepth >= maxDepth)
                return;

            var type = source.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || 
                type == typeof(decimal) || type == typeof(Guid))
            {
                result[prefix] = source;
                return;
            }

            var properties = GetCachedProperties(type);
            foreach (var prop in properties.Where(p => p.CanRead))
            {
                var value = prop.GetValue(source);
                var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}{separator}{prop.Name}";

                if (value == null)
                {
                    result[key] = null;
                }
                else if (value.GetType().IsPrimitive || value is string || value is DateTime || 
                         value is decimal || value is Guid)
                {
                    result[key] = value;
                }
                else
                {
                    FlattenInternal(value, key, result, separator, maxDepth, currentDepth + 1);
                }
            }
        }

        #endregion
    }

    #region Configuration Classes

    public class MappingOptions
    {
        public bool MapNullValues { get; set; } = false;
        public bool ThrowOnMappingError { get; set; } = false;
        public bool UseParallelProcessing { get; set; } = true;
        public int ParallelThreshold { get; set; } = 100;
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
        public Func<PropertyInfo, PropertyInfo, bool> PropertyMatcher { get; set; } = DefaultPropertyMatcher;

        public static MappingOptions Default => new();

        private static bool DefaultPropertyMatcher(PropertyInfo source, PropertyInfo destination)
        {
            return source.Name.Equals(destination.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class CloneOptions
    {
        public HashSet<string> IgnoredProperties { get; set; } = new();
        public bool CloneReadOnlyProperties { get; set; } = true;

        public static CloneOptions Default => new();
    }

    public class MappingConfiguration<TSource, TDestination> where TDestination : new()
    {
        private readonly List<PropertyMapping<TSource, TDestination>> _mappings = new();

        public MappingConfiguration<TSource, TDestination> Map<TProperty>(
            Expression<Func<TDestination, TProperty>> destinationProperty,
            Expression<Func<TSource, TProperty>> sourceProperty)
        {
            _mappings.Add(new PropertyMapping<TSource, TDestination>
            {
                DestinationProperty = destinationProperty,
                SourceProperty = sourceProperty
            });
            return this;
        }

        public Func<TSource, TDestination> Build()
        {
            return source =>
            {
                var destination = new TDestination();
                foreach (var mapping in _mappings)
                {
                    try
                    {
                        if (mapping.SourceProperty is Expression<Func<TSource, object>> sourceExpr &&
                            mapping.DestinationProperty is Expression<Func<TDestination, object>> destExpr)
                        {
                            var sourceValue = sourceExpr.Compile()(source);
                            var destProp = ((MemberExpression)destExpr.Body).Member as PropertyInfo;
                            destProp?.SetValue(destination, sourceValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = PowNetLogger.GetLogger("MappingConfiguration");
                        logger.LogException(ex, "Failed to apply custom mapping");
                    }
                }
                return destination;
            };
        }
    }

    public class PropertyMapping<TSource, TDestination>
    {
        public LambdaExpression? DestinationProperty { get; set; }
        public LambdaExpression? SourceProperty { get; set; }
    }

    public class DataTransformationRule<TSource, TResult>
    {
        public string Name { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = false;
        public Action<TSource, TResult> Apply { get; set; } = null!;
    }

    #endregion
}