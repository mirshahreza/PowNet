# CollectionExtensions
?????????? ??? ?? ?????? ????? ???????? HashSet ? IEnumerable.

## ?????
### FastCopy
??? ???? primitive ??.
```csharp
int[] copy = numbers.FastCopy();
```
### FastIndexOf
?????? ?????? ?? ???? ???? ????.
```csharp
int idx = arr.FastIndexOf(42, isSorted:true);
```
### ProcessInBatches
????? locality.
```csharp
arr.ProcessInBatches(batch => Process(batch), batchSize:500);
```

## ????
### AddRange(params T[])
### RemoveWhere(predicate)
```csharp
int removed = list.RemoveWhere(x => x.IsDeleted);
```
### FastContains (???? ???????? ????)
### Partition(size)
### ProcessParallel(processor, maxDegree)

## Dictionary
### GetOrAdd(key, factory)
### MergeFrom(source, overwrite)
### GetMany(keys)

## ConcurrentDictionary
### UpdateMany(updates)
??? ????? (last write wins).
### ClearWhere(predicate)

## HashSet
### HasIntersection(second)
### AddMany(items)

## IEnumerable
### ChunkOptimal(chunkSize?)
### SafeParallelSelect(selector, errorHandler)
### FastDistinct(comparer)
### ProcessInBatchesAsync(processor, batchSize, maxConcurrency)
```csharp
await items.ProcessInBatchesAsync(async batch => await Save(batch), batchSize:500, maxConcurrency:4);
```
### MeasureEnumeration
```csharp
var (results, duration, mem) = query.MeasureEnumeration();
```

## Performance Metrics
`CollectionPerformanceAnalyzer.RecordOperation` ? `GetMetrics()` ???? ???? ?????? ??????.
