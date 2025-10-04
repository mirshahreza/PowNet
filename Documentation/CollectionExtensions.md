# CollectionExtensions

High-performance helpers for arrays, lists, dictionaries, concurrent collections, and generic sequences. Focus areas: batching, partitioning, safe parallelism, cache locality, and lightweight performance measurement.

---
## Array Methods
### FastCopy<T>(this T[] source) where T: struct
Copies the source array to a new array using `Buffer.BlockCopy` for primitive value types (falls back to `Array.Copy` for reference types). 
Returns the new array.
```csharp
int[] clone = numbers.FastCopy();
```

### FastIndexOf<T>(this T[] array, T item, bool isSorted = false) where T: IComparable<T>
Searches the array for the specified item using binary search if `isSorted` is true, otherwise uses linear search (`Array.IndexOf`). 
Returns the index of the item or -1 if not found.
```csharp
int idx = sorted.FastIndexOf(42, isSorted:true);
```

### ProcessInBatches<T>(this T[] array, Action<T[]> processor, int batchSize = 1000)
Processes the array in fixed-size chunks, copying each batch into a temporary array before invoking the processor action.
```csharp
largeArray.ProcessInBatches(batch => Console.WriteLine(batch.Length), 500);
```

---
## List Methods
### AddRange<T>(this List<T> list, params T[] items)
Adds the specified items to the list if the argument is not null or empty.
```csharp
list.AddRange(1,2,3);
```

### RemoveWhere<T>(this List<T> list, Func<T,bool> predicate)
Removes items from the list that satisfy the given predicate, iterating in reverse order; returns the count of removed items.
```csharp
int removed = users.RemoveWhere(u => u.IsInactive);
```

### FastContains<T>(this List<T> list, T item) where T: IEquatable<T>
Checks if the list contains the specified item using a linear scan for small lists (size <= 10) for better branch prediction; 
for larger lists, it uses `List.Contains`.
```csharp
bool present = small.FastContains(value);
```

### Partition<T>(this List<T> source, int size)
Partitions the source list into chunks of the specified maximum size, yielding new lists.
```csharp
foreach(var chunk in data.Partition(100)) Process(chunk);
```

### ProcessParallel<T>(this List<T> source, Action<T> processor, int? maxDegreeOfParallelism = null)
Processes the items in the list in parallel using `Parallel.ForEach`, with an optional maximum degree of parallelism (defaults to the number of available CPU cores).
```csharp
items.ProcessParallel(DoWork, maxDegreeOfParallelism:4);
```

---
## Dictionary Methods
### GetOrAdd<TKey,TValue>(this Dictionary<TKey,TValue> dict, TKey key, Func<TKey,TValue> factory)
Gets the value associated with the specified key or adds a new key-value pair to the dictionary by using the provided factory function (NOT thread-safe, for regular Dictionary).
```csharp
var cached = map.GetOrAdd("id", k => Load(k));
```

### MergeFrom<TKey,TValue>(this Dictionary<TKey,TValue> target, Dictionary<TKey,TValue> source, bool overwriteExisting = true)
Copies entries from the source dictionary to the target dictionary; optionally preserves existing values in the target dictionary.
```csharp
target.MergeFrom(other, overwriteExisting:false);
```

### GetMany<TKey,TValue>(this Dictionary<TKey,TValue> dict, IEnumerable<TKey> keys)
Returns a new dictionary containing the key-value pairs for the specified keys that are found in the dictionary.
```csharp
var subset = dict.GetMany(requestedKeys);
```

---
## ConcurrentDictionary Methods
### UpdateMany<TKey,TValue>(this ConcurrentDictionary<TKey,TValue> dict, IEnumerable<KeyValuePair<TKey,TValue>> updates)
Sequentially applies the specified updates to the concurrent dictionary for deterministic last-write-wins semantics based on the order of enumeration.
```csharp
concurrent.UpdateMany(new[]{ new("a",1), new("a",2) }); // final value 2
```

### ClearWhere<TKey,TValue>(this ConcurrentDictionary<TKey,TValue> dict, Func<KeyValuePair<TKey,TValue>,bool>? predicate = null)
Removes items from the dictionary that meet the specified predicate (or ALL items when predicate is null). Returns the count of removed items.
```csharp
int removed = concurrent.ClearWhere(kv => kv.Value == null);
```

---
## HashSet Methods
### HasIntersection<T>(this HashSet<T> first, IEnumerable<T> second)
Determines whether the hash set and the specified collection share at least one common element.
```csharp
bool overlap = setA.HasIntersection(listB);
```

### AddMany<T>(this HashSet<T> hashSet, IEnumerable<T> items)
Adds multiple items to the hash set; returns the count of newly added items.
```csharp
int added = hs.AddMany(values);
```

---
## IEnumerable Methods
### ChunkOptimal<T>(this IEnumerable<T> source, int? chunkSize = null)
Chunks the sequence into smaller sequences using the provided chunk size or a default size based on the number of processors.
```csharp
foreach(var chunk in stream.ChunkOptimal()) Handle(chunk);
```

### SafeParallelSelect<TSource,TResult>(this IEnumerable<TSource> source, Func<TSource,TResult> selector, Action<Exception>? errorHandler = null)
Applies the selector function to each item in the source sequence in parallel, capturing and optionally handling exceptions per element. 
Returns an unordered `IEnumerable<TResult>`.
```csharp
var processed = source.SafeParallelSelect(ToDto, ex => log(ex));
```

### FastDistinct<T>(this IEnumerable<T> source, IEqualityComparer<T>? comparer = null)
Returns distinct elements from the sequence by using a hash set to track seen elements, without requiring the full result to be materialized first.
```csharp
var unique = nums.FastDistinct();
```

### ProcessInBatchesAsync<T>(this IEnumerable<T> source, Func<IEnumerable<T>,Task> processor, int batchSize = 1000, int maxConcurrency = -1)
Asynchronously processes the sequence in batches concurrently with an optional semaphore limit on the degree of concurrency.
```csharp
await items.ProcessInBatchesAsync(async batch => await Persist(batch), batchSize:500, maxConcurrency:4);
```

---
## Performance Measurement
### MeasureEnumeration<T>(this IEnumerable<T> source)
Materializes the sequence into an array while tracking the duration and memory usage. Returns a tuple containing the results array, the duration, and the memory used.
```csharp
var (results, elapsed, mem) = query.MeasureEnumeration();
```

---
## CollectionPerformanceAnalyzer
Utility to accumulate and analyze operation metrics (mapping from operation name to aggregated statistics).

### RecordOperation(string operationType, int itemCount, TimeSpan duration)
Records the performance of an operation, accumulating counters and tracking extrema.
```csharp
CollectionPerformanceAnalyzer.RecordOperation("batch-insert", 1000, sw.Elapsed);
```

### GetMetrics()
Returns a snapshot of the current metrics as a dictionary.
```csharp
var metrics = CollectionPerformanceAnalyzer.GetMetrics();
```

### ClearMetrics()
Resets all recorded metrics, clearing the internal state.
```csharp
CollectionPerformanceAnalyzer.ClearMetrics();
```

### CollectionMetricsSnapshot
| Field | Meaning |
|-------|---------|
| OperationCount | Total number of recorded operations |
| TotalItems | Cumulative count of items across all operations |
| AverageDuration | Average duration of recorded operations |
| MinDuration / MaxDuration | Minimum and maximum duration across all operations |
| ItemsPerSecond | Approximate throughput in terms of items processed per second |

---
## Practical Example
```csharp
var batches = data.ChunkOptimal();
await batches.ProcessInBatchesAsync(async batch => await Save(batch), batchSize:1000, maxConcurrency:2);
var (list, took, mem) = data.MeasureEnumeration();
Console.WriteLine($"Loaded {list.Length} items in {took.TotalMilliseconds} ms (+{mem} bytes)");
```

---
## Notes & Guidance
- Prefer `ProcessInBatchesAsync` for I/O heavy workloads; adjust `maxConcurrency` to avoid resource saturation.
- `SafeParallelSelect` swallows individual item failures; ensure error logging for observability.
- `UpdateMany` is intentionally not parallel to guarantee ordering semantics.
