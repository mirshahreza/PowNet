using System.Text;
using FluentAssertions;
using Xunit;
using PowNet.Extensions;
using PowNet.Configuration;

namespace PowNet.Test.Extensions
{
    public class StringExtensionsTests
    {
        [Fact]
        public void InternString_Should_Return_Same_Reference_For_Same_Value()
        {
            var s1 = new string("hello".ToCharArray());
            var s2 = new string("hello".ToCharArray());

            var i1 = s1.InternString();
            var i2 = s2.InternString();

            object.ReferenceEquals(i1, i2).Should().BeTrue();
            i1.Should().Be("hello");
        }

        [Fact]
        public void GetUniqueName_Should_Contain_Prefix_And_Be_Unique()
        {
            var n1 = StringExtensions.GetUniqueName("p_");
            var n2 = StringExtensions.GetUniqueName("p_");

            n1.Should().StartWith("p_");
            n2.Should().StartWith("p_");
            n1.Should().NotBe(n2);
        }

        [Fact]
        public void GetRandomName_Should_Contain_Prefix()
        {
            var n = StringExtensions.GetRandomName("pref-");
            n.Should().StartWith("pref-");
            n.Length.Should().BeGreaterThan("pref-".Length);
        }

        [Fact]
        public void TransToX2_Should_Duplicate_String()
        {
            "ab".TransToX2().Should().Be("abab");
            string.Empty.TransToX2().Should().BeEmpty();
        }

        [Fact]
        public void ToByteArray_Should_Decode_Base64()
        {
            var b = "aGVsbG8=".ToByteArray();
            Encoding.UTF8.GetString(b).Should().Be("hello");
        }

        [Fact]
        public void TruncateTo_Should_Trim_When_Too_Long()
        {
            "abcdef".TruncateTo(3).Should().Be("abc");
            "abc".TruncateTo(5).Should().Be("abc");
        }

        [Fact]
        public void NormalizeAsHostPath_Should_Normalize_And_Optionally_Strip_Root()
        {
            var root = PowNetConfiguration.ProjectRoot.FullName.NormalizeAsHostPath(false);
            var mixed = root + "/sub\\dir/file.txt";

            var stripped = mixed.NormalizeAsHostPath(removeBasePath: true);
            stripped.Should().Be("sub/dir/file.txt");

            var kept = mixed.NormalizeAsHostPath(removeBasePath: false);
            kept.Should().StartWith(root);
            kept.Should().EndWith("sub/dir/file.txt");
        }

        [Fact]
        public void ToRangeMinValue_Should_Parse_Various_Formats()
        {
            StringExtensions.ToRangeMinValue(null).Should().Be(new Tuple<int,int>(1,100));
            "range(5)".ToRangeMinValue().Should().Be(new Tuple<int,int>(5,100));
            "range(5,10)".ToRangeMinValue().Should().Be(new Tuple<int,int>(5,10));
            "nope".ToRangeMinValue().Should().Be(new Tuple<int,int>(1,100));
        }

        [Fact]
        public void StartsWithIgnoreCase_EndsWithIgnoreCase_EqualsIgnoreCase_Should_Work()
        {
            "Hello".StartsWithIgnoreCase("he").Should().BeTrue();
            "Hello".EndsWithIgnoreCase("LO").Should().BeTrue();
            ((string?)null).StartsWithIgnoreCase("he").Should().BeFalse();
            "Hello".EqualsIgnoreCase("hello").Should().BeTrue();
        }

        [Fact]
        public void EndsWithIgnoreCase_List_And_ContainsIgnoreCase_Should_Work()
        {
            var list = new List<string> { ".jpg", ".png" };
            "photo.JPG".EndsWithIgnoreCase(list).Should().BeTrue();

            "Hello World".ContainsIgnoreCase("world").Should().BeTrue();
            ((string?)null).ContainsIgnoreCase("x").Should().BeFalse();

            var parts = new List<string> { "abc", "WORLD" };
            "Hello World".ContainsIgnoreCase(parts).Should().BeTrue();
        }

        [Fact]
        public void ReplaceSafe_Should_Handle_Nulls()
        {
            ((string?)null).ReplaceSafe("x","y").Should().BeEmpty();
            "abc".ReplaceSafe(null,"y").Should().Be("abc");
            "abcabc".ReplaceSafe("a","X").Should().Be("XbcXbc");
        }

        [Fact]
        public void BeginningCommonPart_Should_Return_Common_Prefix()
        {
            "foobar".BeginningCommonPart("foobaz").Should().Be("fooba");
            ((string?)null).BeginningCommonPart("x").Should().BeEmpty();
        }

        [Fact]
        public void NullHelpers_Should_Work()
        {
            ((string?)null).IsNullOrEmpty().Should().BeTrue();
            ((string?)null).FixNull("alt").Should().Be("alt");
            ((string?)null).FixNullOrEmpty("alt").Should().Be("alt");
            "val".FixNullOrEmpty("alt").Should().Be("val");
        }

        [Fact]
        public void RepeatN_Should_Repeat_String()
        {
            "ab".RepeatN(3).Should().Be("ababab");
            "x".RepeatN(0).Should().BeEmpty();
        }

        [Fact]
        public void ReplaceLastOccurrence_Should_Replace_Only_Last()
        {
            "one two one".ReplaceLastOccurrence("one","1").Should().Be("one two 1");
            "hello".ReplaceLastOccurrence("zzz","x").Should().Be("hello");
        }

        [Fact]
        public void RemoveUnnecessaryEmptyLines_Should_Collapse_Blanks()
        {
            var nl = StringExtensions.NL;
            var s = $"A{nl}{nl}{nl}{nl}B"; // four new lines
            var r = s.RemoveUnnecessaryEmptyLines();
            r.Should().Be($"A{nl}B");
        }

        [Fact]
        public void ValidateStringNotNullOrEmpty_Should_Throw_On_Null_Or_Empty()
        {
            Action a1 = () => ((string?)null)!.ValidateStringNotNullOrEmpty("p");
            a1.Should().Throw<PowNet.Common.PowNetValidationException>();

            Action a2 = () => "  ".ValidateStringNotNullOrEmpty("p");
            a2.Should().Throw<PowNet.Common.PowNetValidationException>();
        }

        [Fact]
        public void ValidateStringIsNotPotentialSqlInjection_Should_Throw_When_Dangerous()
        {
            Action safe = () => "hello".ValidateStringIsNotPotentialSqlInjection("p");
            safe.Should().NotThrow();

            Action dangerous = () => "DROP TABLE x".ValidateStringIsNotPotentialSqlInjection("p");
            dangerous.Should().Throw<PowNet.Common.PowNetSecurityException>();
        }

        [Fact]
        public void RemoveWhitelines_Should_Remove_Whitespace_Only_Lines()
        {
            var nl = StringExtensions.NL;
            var s = $"Line1{nl}   {nl}{nl}Line2{nl}\t{nl}Line3";
            var r = s.RemoveWhitelines();
            r.Should().NotContain("   ");
            r.Should().Contain("Line1");
            r.Should().Contain("Line2");
            r.Should().Contain("Line3");
        }

        [Fact]
        public void ExtractSqlParameters_Should_Return_Distinct_Param_Names()
        {
            var sql = "select * from t where id=@Id and name=@name and x=@ID";
            var p = sql.ExtractSqlParameters();
            p.Should().BeEquivalentTo(new[] { "Id", "name" }, options => options.WithoutStrictOrdering());
        }

        [Fact]
        public void ExtractSqlParameters_Should_Include_Question_Mark_As_Empty_Token()
        {
            var sql = "select * from t where id=? and name=@name and a=?";
            var p = sql.ExtractSqlParameters();
            p.Should().Contain("");
            p.Should().Contain("name");
        }

        [Fact]
        public void IsPotentialSqlInjection_Should_Detect_Common_Patterns()
        {
            StringExtensions.IsPotentialSqlInjection("SELECT * FROM Users").Should().BeTrue();
            StringExtensions.IsPotentialSqlInjection("Hello world").Should().BeFalse();
        }

        [Fact]
        public void Regex_BackCompat_Should_Work()
        {
            StringExtensions.WhitelinesRegex().IsMatch("   \n").Should().BeTrue();
            StringExtensions.SqlParamsRegex().Matches("@a, @b, ?").Count.Should().Be(3);
            StringExtensions.JsTranslationRegex().IsMatch("shared.translate('x')").Should().BeTrue();
        }

        [Fact]
        public void PerformanceExtensions_Should_Collect_Stats()
        {
            var before = StringPerformanceExtensions.GetPerformanceStats();
            var res = StringPerformanceExtensions.MeasurePerformance(() => "x".TransToX2());
            res.Should().Be("xx");
            var after = StringPerformanceExtensions.GetPerformanceStats();
            after.Count.Should().BeGreaterThan(before.Count);
            after.AvgTimeMicroseconds.Should().BeGreaterOrEqualTo(0);
        }

        // AdditionalStringExtensions tests
        [Fact]
        public void RemoveDiacritics_ToSlug_Mask_TruncateByBytes_NormalizeNewlines_Base64Url_Should_Work()
        {
            "אימצü".RemoveDiacritics().Should().Be("aeiou");
            "Hello, World!".ToSlug().Should().Be("hello-world");
            "SensitiveValue".Mask(2,2,'*').Should().Be("Se**********ue");
            var utf8 = Encoding.UTF8;
            var truncated = "???? ????".TruncateByBytes(8, utf8); // depends on bytes per char; ensure not empty
            truncated.Should().NotBeNull();
            truncated.Length.Should().BeGreaterThan(0);
            "a\r\nb\rc".NormalizeNewlines().Should().Be("a\nb\nc");

            var data = new byte[]{1,2,3,4,5};
            var b64u = data.ToBase64Url();
            var back = b64u.FromBase64Url();
            back.Should().Equal(data);
        }
    }
}
