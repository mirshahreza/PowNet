using System.Reflection;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using PowNet.Common;

namespace PowNet.Extensions
{
    public static class JsonExtensions
    {
        public static T? TryDeserializeTo<T>(string jsonString, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonString, options: jsonSerializerOptions);
            }
            catch
            {
                return default;
            }
        }

        public static T? TryDeserializeTo<T>(JsonElement jsonElement, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonElement, options: jsonSerializerOptions);
            }
            catch
            {
                return default;
            }
        }

        public static T? TryDeserializeTo<T>(JsonObject jsonObject)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonObject);
            }
            catch
            {
                return default;
            }
        }

        public static JObject ToJObjectByNewtonsoft(this string s)
        {
            return JObject.Parse(s);
        }

        public static JArray ToJArray(this JToken? jToken)
        {
            if (jToken is null) return [];
            if (jToken is not JArray)
                new AppEndException("InputParameterIsNotJArray", System.Reflection.MethodBase.GetCurrentMethod())
                    .AddParam("Input", jToken)
                    .GetEx();
            return (JArray)jToken;
        }

        public static string ToJsonStringByBuiltIn(this object? o, bool indented = true, bool includeFields = true, JsonIgnoreCondition ignorePolicy = JsonIgnoreCondition.WhenWritingDefault)
        {
            return JsonSerializer.Serialize(o, options: new()
            {
                IncludeFields = includeFields,
                WriteIndented = indented,
                DefaultIgnoreCondition = ignorePolicy,
                IgnoreReadOnlyProperties = true
            });
        }

        public static string ToJsonStringByBuiltInAllDefaults(this object? o)
        {
            return JsonSerializer.Serialize(o);
        }

        public static string ToJsonStringByNewtonsoft(this object? o, bool indented = true)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(o, indented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
        }

        public static JsonElement ToJsonElementByBuiltIn(this object o)
        {
            return JsonSerializer.Deserialize<JsonElement>(o.ToJsonStringByBuiltIn(false));
        }

        public static JsonObject ToJsonObjectByBuiltIn(this object o)
        {
            return JsonSerializer.Deserialize<JsonObject>(o.ToJsonStringByBuiltIn(false)) ?? new JsonObject();
        }

        public static JsonObject ToJsonObjectByBuiltIn(this string o)
        {
            return JsonSerializer.Deserialize<JsonObject>(o) ?? new JsonObject();
        }

        public static string[]? DeserializeAsStringArray(this string? o)
        {
            if (o is null) return null;
            return JsonSerializer.Deserialize<string[]>(o);
        }

        public static object ToOrigType(this JsonElement s, ParameterInfo parameterInfo)
        {
            if (parameterInfo.ParameterType == typeof(int)) return int.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Int16)) return Int16.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Int32)) return Int32.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Int64)) return Int64.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(bool)) return bool.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(DateTime)) return DateTime.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Guid)) return Guid.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(float)) return float.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Decimal)) return Decimal.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(string)) return s.ToString();
            if (parameterInfo.ParameterType == typeof(byte[])) return Encoding.UTF8.GetBytes(s.ToString());
            if (parameterInfo.ParameterType == typeof(List<string>)) return s.Deserialize<List<string>>().FixNull(new List<string>());

            return s;
        }

        public static object? ToRealType(this JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    if (DateTime.TryParse(jsonElement.GetString(), out DateTime dateTimeValue))
                    {
                        return dateTimeValue;
                    }
                    if (Guid.TryParse(jsonElement.GetString(), out Guid guidValue))
                    {
                        return guidValue;
                    }
                    return jsonElement.GetString();
                case JsonValueKind.Number:
                    if (jsonElement.TryGetInt16(out Int16 int16Value)) return int16Value;
                    if (jsonElement.TryGetInt32(out Int32 intValue)) return intValue;
                    if (jsonElement.TryGetInt64(out long longValue)) return longValue;
                    if (jsonElement.TryGetDouble(out double doubleValue)) return doubleValue;
                    if (jsonElement.TryGetDecimal(out decimal decimalValue)) return decimalValue;
                    if (jsonElement.TryGetSingle(out float singleValue)) return singleValue;
                    if (jsonElement.TryGetUInt16(out ushort ushortValue)) return ushortValue;
                    if (jsonElement.TryGetUInt32(out uint uintValue)) return uintValue;
                    if (jsonElement.TryGetUInt64(out ulong uint64Value)) return uint64Value;
                    return null; // Should not happen for valid numbers
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return jsonElement;
            }
        }

        #region Additional (Merged)
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
        #endregion
    }
}