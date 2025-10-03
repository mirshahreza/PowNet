using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class JsonExtensionsTests
    {
        public class Sample
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        [Fact]
        public void TryDeserializeTo_Should_Return_Default_On_Invalid()
        {
            string bad = "{"; // invalid json
            var obj = JsonExtensions.TryDeserializeTo<Sample>(bad);
            obj.Should().BeNull();
        }

        [Fact]
        public void TryDeserializeTo_Should_Deserialize_From_String_Element_And_Object()
        {
            var json = "{\"Name\":\"A\",\"Age\":10}";
            JsonExtensions.TryDeserializeTo<Sample>(json)!.Name.Should().Be("A");

            using var doc = JsonDocument.Parse(json);
            JsonExtensions.TryDeserializeTo<Sample>(doc.RootElement)!.Age.Should().Be(10);

            var obj = JsonNode.Parse(json)!.AsObject();
            JsonExtensions.TryDeserializeTo<Sample>(obj)!.Age.Should().Be(10);
        }

        [Fact]
        public void NewtonsoftHelpers_Should_Work()
        {
            var jo = JsonExtensions.ToJObjectByNewtonsoft("{\"a\":1}");
            jo["a"]!.Value<int>().Should().Be(1);

            var token = JArray.Parse("[1,2]");
            var arr = JsonExtensions.ToJArray(token);
            arr.Should().BeOfType<JArray>();
            arr.Count.Should().Be(2);
        }

        [Fact]
        public void ToJsonString_ByBuiltIn_Should_Serialize_With_Options()
        {
            var obj = new Sample { Name = "N" };
            var s = obj.ToJsonStringByBuiltIn(indented: false);
            s.Should().Contain("\"Name\":");
        }

        [Fact]
        public void ToJsonElement_And_JsonObject_Conversions_Should_Work()
        {
            var obj = new Sample { Name = "N", Age = 1 };
            var el = obj.ToJsonElementByBuiltIn();
            el.ValueKind.Should().Be(JsonValueKind.Object);

            var jo = obj.ToJsonObjectByBuiltIn();
            jo["Name"]!.ToString().Should().Be("N");

            var jo2 = ("{\"x\":1}").ToJsonObjectByBuiltIn();
            jo2["x"]!.ToString().Should().Be("1");
        }

        [Fact]
        public void DeserializeAsStringArray_Should_Handle_Null()
        {
            string? s = null;
            s.DeserializeAsStringArray().Should().BeNull();
            var a = "[\"x\",\"y\"]".DeserializeAsStringArray();
            a.Should().Equal("x","y");
        }

        [Fact]
        public void ToOrigType_Should_Map_To_Parameter_Type()
        {
            object[] args = new object[] { 1, (short)2, 3, 4L, true, DateTime.UtcNow, Guid.NewGuid(), 1.2f, 2.3m, "s", new byte[] {1,2}, new List<string>{"a"} };
            var method = typeof(JsonExtensionsTests).GetMethod(nameof(Foo), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            using var doc = JsonDocument.Parse("[1,2,3,4,true,\"2020-01-01\",\"1B4E28BA-2FA1-11D2-883F-0016D3CCA427\",1.2,2.3,\"s\",\"bytes\",[\"a\"]]");
            var arr = doc.RootElement.EnumerateArray().ToArray();
            var mapped = new object?[args.Length];
            var parameters = method.GetParameters();
            for (int i = 0; i < args.Length; i++)
            {
                mapped[i] = arr[i].ToOrigType(parameters[i]);
            }
            mapped[0].Should().BeOfType<int>();
            mapped[10].Should().BeOfType<byte[]>();
            mapped[11].Should().BeOfType<List<string>>();
        }

        private static void Foo(int a, short b, int c, long d, bool e, DateTime f, Guid g, float h, decimal i, string j, byte[] k, List<string> l) {}

        [Theory]
        [InlineData("\"2020-01-01\"")]
        [InlineData("\"1B4E28BA-2FA1-11D2-883F-0016D3CCA427\"")]
        [InlineData("123")]
        [InlineData("true")]
        [InlineData("null")]
        public void ToRealType_Should_Return_CLR_Types(string jsonLiteral)
        {
            using var doc = JsonDocument.Parse(jsonLiteral);
            var el = doc.RootElement;
            var real = el.ToRealType();
            if (jsonLiteral == "null")
            {
                real.Should().BeNull();
            }
            else
            {
                real.Should().NotBeOfType<JsonElement>();
            }
        }

        // AdditionalJsonExtensions tests
        [Fact]
        public void TryGetPropertyCI_MergePatch_To_Fallback_Should_Work()
        {
            using var doc = JsonDocument.Parse("{\"Name\":\"Ali\",\"Age\":20}");
            doc.RootElement.TryGetPropertyCI("name", out var nameEl).Should().BeTrue();
            nameEl.GetString().Should().Be("Ali");

            var target = JsonNode.Parse("{\"a\":1,\"o\":{\"x\":1}}")!.AsObject();
            var patch = JsonNode.Parse("{\"b\":2,\"o\":{\"y\":2},\"a\":null}")!.AsObject();
            target.MergePatch(patch);
            target.ContainsKey("a").Should().BeFalse();
            target["b"]!.ToString().Should().Be("2");
            target["o"]!["x"]!.ToString().Should().Be("1");
            target["o"]!["y"]!.ToString().Should().Be("2");

            var fallback = nameEl.To<string>(@default:"none");
            fallback.Should().Be("Ali");
            var bad = new JsonElement();
            bad.To<int>(@default:42).Should().Be(42);
        }
    }
}
