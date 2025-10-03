using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace PowNet.Extensions
{
    public static class StreamExtensions
    {
        public static string ToText(this Stream stream)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static async Task<string> ToTextAsync(this Stream stream)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        public static JsonElement ToJson(this Stream stream)
        {
            using var reader = new StreamReader(stream);
            var body = reader.ReadToEnd();
            if (string.IsNullOrEmpty(body)) return new JsonElement();
            var jsonDocument = JsonDocument.Parse(body);
            return jsonDocument.RootElement;
        }

        public static async Task<JsonElement> ToJsonAsync(this HttpRequest request)
        {
            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            using var reader = new StreamReader(memoryStream);
            string body = await reader.ReadToEndAsync();
            if (string.IsNullOrEmpty(body)) return default;
            return JsonDocument.Parse(body).RootElement;
        }
    }
}