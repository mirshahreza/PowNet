# JsonExtensions

Utility helpers bridging System.Text.Json and Newtonsoft.Json for flexible (de)serialization, safe conversions, dynamic patching, and type inference.

---

## 1. Safe Deserialize Helpers

### TryDeserializeTo<T>(string jsonString, JsonSerializerOptions? options = null)

Attempts to deserialize returning default(T) on failure.

```csharp
var user = JsonExtensions.TryDeserializeTo<User>(rawJson);
```

### TryDeserializeTo<T>(JsonElement jsonElement, JsonSerializerOptions? options = null)

```csharp
JsonElement elem = JsonDocument.Parse(rawJson).RootElement;
var dto = JsonExtensions.TryDeserializeTo<MyDto>(elem);
```

### TryDeserializeTo<T>(JsonObject jsonObject)

```csharp
var obj = new JsonObject { ["Id"] = 1 };
var dto = JsonExtensions.TryDeserializeTo<MyDto>(obj);
```

Returns: `T?` or `default` on exception (silent catch).

---

## 2. Newtonsoft Interop

### ToJObjectByNewtonsoft(this string json)

Parses string to `JObject`.

```csharp
JObject jo = json.ToJObjectByNewtonsoft();
```

### ToJArray(this JToken? token)

Validates token is `JArray`; returns empty `JArray` on null else throws custom exception if not array.

```csharp
JArray arr = token.ToJArray();
```

---

## 3. Serialization Helpers (System.Text.Json)

### ToJsonStringByBuiltIn(this object? obj, bool indented = true, bool includeFields = true, JsonIgnoreCondition ignorePolicy = WhenWritingDefault)

Customizable serialization toggling indentation, fields inclusion, ignore defaults.

```csharp
string json = dto.ToJsonStringByBuiltIn(indented:true);
```

### ToJsonStringByBuiltInAllDefaults(this object? obj)

Default `JsonSerializer.Serialize(obj)` wrapper.

### ToJsonStringByNewtonsoft(this object? obj, bool indented = true)

Simplified Newtonsoft serializer.

### ToJsonElementByBuiltIn(this object obj)

Serializes then deserializes to `JsonElement` for dynamic inspection.

```csharp
JsonElement element = dto.ToJsonElementByBuiltIn();
```

### ToJsonObjectByBuiltIn(this object obj)

Returns `JsonObject` equivalent.

```csharp
JsonObject node = dto.ToJsonObjectByBuiltIn();
```

### ToJsonObjectByBuiltIn(this string json)

Parses JSON string to `JsonObject` (empty if invalid / null root).

### DeserializeAsStringArray(this string? json)

Null-safe string[] deserialization.

```csharp
string[]? tags = json.DeserializeAsStringArray();
```

---

## 4. Type Conversion & Inference

### ToOrigType(this JsonElement el, ParameterInfo parameterInfo)

Converts element to the parameter's target CLR type (supports primitives, DateTime, Guid, byte[], List<string>). Falls back to original JsonElement when type unsupported.

```csharp
object typed = element.ToOrigType(paramInfo);
```

### ToRealType(this JsonElement el)

Heuristically infers .NET primitive from JSON value kind (tries number numeric widths, DateTime, Guid). Returns raw JsonElement for complex kinds.

```csharp
object? inferred = element.ToRealType();
```

### To<T>(this JsonElement el, JsonSerializerOptions? opts = null, T? @default = default)

Wrapper that deserializes returning provided fallback on exception.

```csharp
int count = element.To<int>(@default: 0);
```

---

## 5. Case-Insensitive Property Lookup

### TryGetPropertyCI(this JsonElement el, string name, out JsonElement value)

Enumerates object properties performing case-insensitive name comparison.

```csharp
if (element.TryGetPropertyCI("userid", out var uidEl)) { /* ... */ }
```

---

## 6. JSON Merge Patch

### MergePatch(this JsonObject target, JsonObject patch)

Applies RFC 7396-like merge semantics:

- Null values ? remove key
- Object values ? recursive merge / replace
- Primitives / arrays ? overwrite

Returns modified target (for chaining).

```csharp
var target = new JsonObject { ["a"] = 1, ["user"] = new JsonObject{{"name","x"}} };
var patch  = new JsonObject { ["user"] = new JsonObject{{"name","y"}}, ["a"] = null };
target.MergePatch(patch); // a removed; user.name -> y
```

---

## 7. Error Handling Strategy

All *Try* methods swallow exceptions intentionally for resilience; use explicit deserialize if you need error visibility. `ToJArray` throws a custom exception (legacy `AppEndException`) when token type mismatch.

---

## 8. Usage Scenario

```csharp
// Dynamic patching & mapping
JsonObject current = entity.ToJsonObjectByBuiltIn();
JsonObject patch = new(){ ["Status"] = "Closed" };
current.MergePatch(patch);
var updated = JsonExtensions.TryDeserializeTo<OrderDto>(current);
```

---

## 9. Best Practices

- Prefer strongly typed DTOs; fallback to dynamic only near boundaries.
- Limit use of silent catch wrappers in critical validation code—log when appropriate.
- For high-performance pipelines re-use `JsonSerializerOptions` with cached metadata.

---

## 10. Limitations

- `ToRealType` does not parse ISO durations nor extended numeric types (BigInteger).
- Merge patch loses original ordering (JSON objects are unordered by spec).
- `ToOrigType` handles a fixed primitive set—extend as needed for custom conversions.

---
*Manual documentation.*
