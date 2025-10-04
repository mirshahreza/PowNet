# DataMapperExtensions

High?performance object mapping, cloning, projection and transformation helpers. Provides smart property-to-property mapping with caching, parallel collection mapping, deep cloning with cycle handling, nested property access, flattening, conditional mapping logic and custom rule-based transformations.

---
## 1. Overview
Features:
- Reflection metadata caching (`PropertyInfo[]`) per type
- Mapper delegate caching per (TSource ? TDestination) pair
- Configurable options: null handling, error tolerance, parallel execution
- Deep clone with max depth & cycle detection
- Merge multiple source objects into one target (case-insensitive property matching)
- Object flattening to `Dictionary<string,object?>`
- Expression-based custom mapping configuration
- Rule-based transformations with required/optional rules

Thread Safety: All caches are `ConcurrentDictionary`; building a mapper is done once per type pair.

---
## 2. Smart Mapping
### SmartMap<TSource,TDestination>(this TSource source, TDestination? destination = null, MappingOptions? options = null) where TDestination : new()
Copies matching properties from `source` to `destination` (creates new if null). Matches controlled by `MappingOptions.PropertyMatcher`. Values are converted if necessary (enum / primitive) using `Convert.ChangeType` semantics. Caches compiled mapping delegate.
```csharp
var userDto = userEntity.SmartMap<UserEntity,UserDto>();
```
Parameters:
- destination: Optional existing instance (reuse / partial update)
- options: MappingOptions (see section 7)

### SmartMapCollection<TSource,TDestination>(this IEnumerable<TSource> source, MappingOptions? options = null) where TDestination:new()
Maps each element. Uses PLINQ if `options.UseParallelProcessing == true` and `source.Count() > ParallelThreshold`.
```csharp
var dtos = users.SmartMapCollection<UserEntity,UserDto>();
```

### CreateMapping<TSource,TDestination>() where TDestination:new()
Starts a fluent custom mapping configuration returning `MappingConfiguration<TSource,TDestination>` (see section 8).
```csharp
var config = DataMapperExtensions.CreateMapping<UserEntity,UserSummary>();
```

---
## 3. Deep Mapping / Cloning
### DeepClone<T>(this T source, int maxDepth = 10, CloneOptions? options = null)
Performs depth-limited clone with cycle detection. Handles primitives, strings, DateTime, Guid, decimal, arrays, `List<T>`; other collections returned as-is (extend `CloneCollection` for more types). Properties ignored if listed in `CloneOptions.IgnoredProperties`.
```csharp
var clone = original.DeepClone(maxDepth:5);
```

### MergeObjects<T>(this T target, params object[] sources) where T:new()
Copies non-null readable source property values into writable target properties (case-insensitive name match). Attempts type conversion when assignable types differ.
```csharp
profile.MergeObjects(patchDto, defaults);
```

---
## 4. Flattening & Projection
### Flatten(this object source, string separator = ".", int maxDepth = 5)
Produces `Dictionary<string,object?>` with dotted paths for nested properties up to `maxDepth`.
```csharp
var dict = order.Flatten(); // e.g. Customer.Name => "Customer.Name"
```

### Project<TSource,TProjection>(this TSource source, Expression<Func<TSource,TProjection>> projectionExpression)
Executes compiled projection expression against the source. Suitable for building lightweight DTOs.
```csharp
var summary = order.Project(o => new { o.Id, Total = o.Lines.Sum(l => l.Price) });
```

### GetNestedProperty(this object source, string propertyPath)
Traverses case-insensitive dot path retrieving property value or null.
```csharp
var city = user.GetNestedProperty("Address.City");
```

### SetNestedProperty(this object target, string propertyPath, object? value)
Ensures intermediate objects exist (if they have a public parameterless constructor). Returns true on success.
```csharp
user.SetNestedProperty("Preferences.Theme", "Dark");
```

---
## 5. Data Transformation
### Transform<TSource,TResult>(this TSource source, params DataTransformationRule<TSource,TResult>[] rules) where TResult:new()
Applies ordered rules. Each rule: `Name`, `IsRequired`, `Action<TSource,TResult> Apply`. Exceptions in optional rules are logged; required rule exceptions propagate.
```csharp
var result = request.Transform(new DataTransformationRule<Request,Response>{
    Name = "MapId", IsRequired = true, Apply = (src,dst) => dst.Id = src.Id },
    new DataTransformationRule<Request,Response>{ Name="OptionalMeta", Apply=(src,dst)=> dst.Meta = BuildMeta(src) }
);
```

### MapIf<TSource,TDestination>(this TSource source, Func<TSource,bool> condition, Func<TSource,TDestination> trueMapping, Func<TSource,TDestination>? falseMapping = null)
Conditional mapping returning mapping result of chosen branch; returns default(TDestination) if condition false and `falseMapping` null.
```csharp
var outcome = model.MapIf(m => m.IsActive, m => new ActiveDto(m), m => new InactiveDto(m));
```

### AggregateMap<TSource,TDestination>(this IEnumerable<TSource> sources, Func<TSource,TDestination> mapper, Func<IEnumerable<TDestination>,TDestination> aggregator)
Maps each source and aggregates final result (e.g. sum, merge, reduce).
```csharp
var summary = orders.AggregateMap(o => new { o.Total }, items => new { Grand = items.Sum(i=>i.Total) });
```

---
## 6. Private Helper Behaviors (High-Level)
- Mapper creation: enumerates readable source props; matches first destination prop satisfying `PropertyMatcher` & writable; copies with optional null skip.
- Value conversion: enums via `Enum.Parse`, primitives via `Convert.ChangeType`, nullable unwrapped.
- Deep clone: cycle detection dictionary; depth limit halts recursion.
- Flatten: stops at primitives or depth limit.

---
## 7. MappingOptions
| Property | Purpose | Default |
|----------|---------|---------|
| MapNullValues | Copy nulls to destination | false |
| ThrowOnMappingError | Throw instead of logging warning | false |
| UseParallelProcessing | Use PLINQ in collection mapping | true |
| ParallelThreshold | Minimum item count to parallelize | 100 |
| MaxDegreeOfParallelism | PLINQ degree | `Environment.ProcessorCount` |
| PropertyMatcher | Delegate controlling name matching | Case-insensitive name equality |

Static: `MappingOptions.Default`.

---
## 8. CloneOptions
| Property | Meaning | Default |
|----------|---------|---------|
| IgnoredProperties | Set of property names to skip cloning | empty |
| CloneReadOnlyProperties | (Reserved for future – currently not applied to logic) | true |

Static: `CloneOptions.Default`.

---
## 9. MappingConfiguration<TSource,TDestination>
Fluent explicit property mapping.
### Map<TProperty>(Expression<Func<TDestination,TProperty>> destinationProperty, Expression<Func<TSource,TProperty>> sourceProperty)
Adds a mapping pair.
```csharp
var config = DataMapperExtensions.CreateMapping<UserEntity,UserCard>()
    .Map(d => d.DisplayName, s => s.Name)
    .Map(d => d.JoinedOn, s => s.CreatedAt);
var mapFunc = config.Build();
var card = mapFunc(userEntity);
```
`Build()` returns `Func<TSource,TDestination>` applying all stored mappings. (Note: Implementation presently expects `Expression<Func<TDestination, object>>` in some cast paths; extend for robust generic handling.)

### PropertyMapping<TSource,TDestination>
Internal structure storing raw expressions.

---
## 10. DataTransformationRule<TSource,TResult>
| Field | Description |
|-------|-------------|
| Name | For logging / diagnostics |
| IsRequired | If true, exceptions propagate |
| Apply | Action to mutate destination |

---
## 11. Examples
### Smart Map & Parallel Collection
```csharp
var usersDto = users.SmartMapCollection<User,UserDto>(new MappingOptions{ ParallelThreshold=50 });
```
### Deep Clone Then Merge Patch
```csharp
var clone = profile.DeepClone();
clone = clone.MergeObjects(patchDto);
```
### Flatten & Access Nested Property
```csharp
var flat = order.Flatten();
object? zip = order.GetNestedProperty("Customer.Address.Zip");
```
### Conditional Mapping
```csharp
var dto = entity.MapIf(e => e.IsDeleted, e => new DeletedDto(e), e => new ActiveDto(e));
```
### Aggregate Map
```csharp
var total = invoices.AggregateMap(i => i.Amount, list => list.Sum());
```
### Rule-Based Transform
```csharp
var response = request.Transform(
    new DataTransformationRule<Request,Response>{ Name="Id", IsRequired=true, Apply=(src,dst)=> dst.Id = src.Id },
    new DataTransformationRule<Request,Response>{ Name="Meta", Apply=(src,dst)=> dst.Meta = BuildMeta(src) }
);
```

---
## 12. Best Practices
- Keep mapping lean: avoid heavy logic inside property getter loops.
- Pre-configure `MappingOptions` and reuse across calls for consistency.
- For frequently changing mapping rules consider source generators or compiled expression trees for performance.
- Validate `maxDepth` in cloning to avoid unintended shallow clones.

---
## 13. Limitations & Extension Ideas
| Area | Current | Potential Improvement |
|------|---------|-----------------------|
| Collection cloning | Only `List<T>` deeply cloned | Add support for more generic collections / dictionaries |
| Error handling | Logs warnings unless `ThrowOnMappingError` | Expose event/hook for failures |
| Custom mapping config | Simplified expression handling | Generate strongly-typed compiled delegate per property |
| Performance | Reflection per property each call (cached props) | Emit IL or use compiled expressions for bulk copy |

---
*Manual documentation.*
