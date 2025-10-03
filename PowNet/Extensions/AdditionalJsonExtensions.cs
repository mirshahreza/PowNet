using System.Text.Json;
using System.Text.Json.Nodes;

namespace PowNet.Extensions;

/// <summary>
/// Additional helpers for System.Text.Json
/// </summary>
public static class AdditionalJsonExtensions
{
    /// <summary>
    /// Case-insensitive property lookup on JsonElement objects.
    /// </summary>
    public static bool TryGetPropertyCI(this JsonElement el, string name, out JsonElement value)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Merge JSON patch (RFC 7396-like) into target JsonObject.
    /// </summary>
    public static JsonObject MergePatch(this JsonObject target, JsonObject patch)
    {
        foreach (var kv in patch)
        {
            if (kv.Value is null)
            {
                target.Remove(kv.Key);
                continue;
            }

            if (kv.Value is JsonObject patchObj)
            {
                if (target[kv.Key] is JsonObject tgtObj)
                    target[kv.Key] = tgtObj.MergePatch(patchObj);
                else
                    target[kv.Key] = patchObj.DeepClone() as JsonNode;
            }
            else
            {
                target[kv.Key] = kv.Value.DeepClone();
            }
        }
        return target;
    }

    /// <summary>
    /// Safe conversion with default fallback.
    /// </summary>
    public static T? To<T>(this JsonElement el, JsonSerializerOptions? opts = null, T? @default = default)
    {
        try
        {
            return el.Deserialize<T>(opts);
        }
        catch
        {
            return @default;
        }
    }
}
