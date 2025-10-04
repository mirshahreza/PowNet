using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Buffers;
using System.Text.Json;

namespace PowNet.Web.ModelBinding;

/// <summary>
/// Lightweight JSON value binder for simple (scalar / enum / Guid / DateTime) types.
/// Extracted from ServIo and generalized.
/// </summary>
public class SimpleJsonModelBinder : IModelBinder
{
    private const string JsonBodyCacheKey = "__PowNetJsonBody";
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null) throw new ArgumentNullException(nameof(bindingContext));
        if (bindingContext.BindingSource != null && bindingContext.BindingSource != BindingSource.Body) return;
        var request = bindingContext.HttpContext.Request;
        if (request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true) return;

        string? propName = bindingContext.ModelMetadata.Name ?? bindingContext.ModelName;
        if (string.IsNullOrEmpty(propName)) return;

        // Attempt streaming parse first
        if (!bindingContext.HttpContext.Items.ContainsKey(JsonBodyCacheKey))
        {
            var streamed = await TryStreamSingleProperty(bindingContext, propName);
            if (streamed) return;
        }

        if (!bindingContext.HttpContext.Items.TryGetValue(JsonBodyCacheKey, out var bodyObj))
        {
            request.EnableBuffering();
            request.Body.Position = 0;
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms, bindingContext.HttpContext.RequestAborted);
            ms.Position = 0; request.Body.Position = 0;
            var doc = await JsonDocument.ParseAsync(ms, cancellationToken: bindingContext.HttpContext.RequestAborted);
            bindingContext.HttpContext.Items[JsonBodyCacheKey] = doc;
            bodyObj = doc;
        }

        JsonElement root = bodyObj is JsonDocument d ? d.RootElement : (JsonElement)bodyObj!;
        if (root.ValueKind != JsonValueKind.Object)
        {
            bindingContext.ModelState.AddModelError(propName, "JSON root must be object");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        if (root.TryGetProperty(propName, out var value) || TryGetCaseInsensitive(root, propName, out value))
        {
            object? converted = ConvertElement(value, bindingContext.ModelMetadata.ModelType);
            bindingContext.Result = ModelBindingResult.Success(converted);
            return;
        }

        // Missing property
        if (bindingContext.ModelMetadata.IsRequired)
        {
            bindingContext.ModelState.AddModelError(propName, $"Required property '{propName}' not found");
            bindingContext.Result = ModelBindingResult.Failed();
        }
        else
        {
            bindingContext.Result = ModelBindingResult.Success(GetDefault(bindingContext.ModelMetadata.ModelType));
        }
    }

    private async Task<bool> TryStreamSingleProperty(ModelBindingContext ctx, string propName)
    {
        try
        {
            var req = ctx.HttpContext.Request;
            req.EnableBuffering();
            req.Body.Position = 0;
            using var ms = new MemoryStream();
            await req.Body.CopyToAsync(ms, ctx.HttpContext.RequestAborted);
            ms.Position = 0; req.Body.Position = 0;
            var seq = new ReadOnlySequence<byte>(ms.ToArray());
            var reader = new Utf8JsonReader(seq, true, default);
            bool inRoot = false;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject && !inRoot) { inRoot = true; continue; }
                if (!inRoot) continue;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string name = reader.GetString() ?? string.Empty;
                    if (string.Equals(name, propName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!reader.Read()) break;
                        var clone = reader;
                        using var vdoc = JsonDocument.ParseValue(ref clone);
                        object? converted = ConvertElement(vdoc.RootElement, ctx.ModelMetadata.ModelType);
                        ctx.Result = ModelBindingResult.Success(converted);
                        return true;
                    }
                    else
                    {
                        // skip value
                        if (!reader.Read()) break;
                        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                        {
                            int depth = 0;
                            do
                            {
                                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) depth++;
                                else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray) depth--;
                                if (!reader.Read()) break;
                            } while (depth > 0);
                        }
                    }
                }
            }
            if (!ctx.ModelMetadata.IsRequired)
            {
                ctx.Result = ModelBindingResult.Success(GetDefault(ctx.ModelMetadata.ModelType));
                return true;
            }
        }
        catch { /* swallow and fallback */ }
        return false;
    }

    private static bool TryGetCaseInsensitive(JsonElement root, string name, out JsonElement value)
    {
        value = default;
        foreach (var p in root.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        }
        return false;
    }

    private static object? ConvertElement(JsonElement element, Type targetType)
    {
        var nn = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (element.ValueKind == JsonValueKind.Null)
            return Nullable.GetUnderlyingType(targetType) != null ? null : GetDefault(nn);

        try
        {
            if (nn.IsEnum)
            {
                if (element.ValueKind == JsonValueKind.String && Enum.TryParse(nn, element.GetString(), true, out var ev)) return ev;
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int ei)) return Enum.ToObject(nn, ei);
            }
            if (nn == typeof(string)) return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
            if (nn == typeof(Guid) && element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var g)) return g;
            if (nn == typeof(DateTime) && element.ValueKind == JsonValueKind.String && DateTime.TryParse(element.GetString(), out var dt)) return dt;
            if (nn == typeof(DateTimeOffset) && element.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(element.GetString(), out var dto)) return dto;
            if (nn == typeof(bool) && element.ValueKind is JsonValueKind.True or JsonValueKind.False) return element.GetBoolean();
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (nn == typeof(int) && element.TryGetInt32(out var i)) return i;
                if (nn == typeof(long) && element.TryGetInt64(out var l)) return l;
                if (nn == typeof(double) && element.TryGetDouble(out var d)) return d;
                if (nn == typeof(float) && element.TryGetSingle(out var f)) return f;
                if (nn == typeof(decimal) && element.TryGetDecimal(out var m)) return m;
            }
            // fallback full deserialize
            return JsonSerializer.Deserialize(element.GetRawText(), targetType, _options);
        }
        catch
        {
            return GetDefault(nn);
        }
    }

    private static object? GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
}

public class SimpleJsonModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (context.BindingInfo?.BindingSource != null && context.BindingInfo.BindingSource != BindingSource.Body) return null;
        var modelType = context.Metadata.ModelType;
        var u = Nullable.GetUnderlyingType(modelType) ?? modelType;
        bool eligible = u.IsPrimitive || u.IsEnum || u == typeof(string) || u == typeof(Guid) || u == typeof(DateTime) || u == typeof(DateTimeOffset) || u == typeof(decimal);
        return eligible ? new SimpleJsonModelBinder() : null;
    }
}
