using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class SecurityExtensionsTests
    {
        #region ValidateSqlSafety
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ValidateSqlSafety_Should_Succeed_On_Null_Or_Empty(string? input)
        {
            var result = SecurityExtensions.ValidateSqlSafety(input, "p");
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("Hello world")] 
        [InlineData("Safe_123_Name")] 
        public void ValidateSqlSafety_Should_Succeed_On_Safe_Inputs(string input)
        {
            var result = input.ValidateSqlSafety("name");
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("DROP TABLE Users")] 
        [InlineData("admin' OR '1'='1")] 
        [InlineData("UNION SELECT * FROM users")] 
        [InlineData("name -- comment")] 
        public void ValidateSqlSafety_Should_Fail_On_Dangerous_Inputs(string input)
        {
            var result = input.ValidateSqlSafety("name");
            result.IsValid.Should().BeFalse();
            result.Issues.Count.Should().BeGreaterThan(0);
        }
        #endregion

        #region ValidateXssSafety
        [Fact]
        public void ValidateXssSafety_Should_Succeed_On_Null_Or_Empty()
        {
            SecurityExtensions.ValidateXssSafety(null, "x").IsValid.Should().BeTrue();
            SecurityExtensions.ValidateXssSafety("", "x").IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("<div onclick=\"do()\">x</div>")]
        [InlineData("javascript:alert(1)")]
        [InlineData("%3Cscript%3Ealert(1)%3C/script%3E")]
        public void ValidateXssSafety_Should_Fail_On_XSS_Patterns(string input)
        {
            var result = input.ValidateXssSafety("html");
            result.IsValid.Should().BeFalse();
        }
        #endregion

        #region ValidatePathSafety
        [Fact]
        public void ValidatePathSafety_Should_Succeed_On_Safe_Relative_Path()
        {
            var result = SecurityExtensions.ValidatePathSafety("images/photo.jpg", "p");
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("..\\secret.txt")]
        [InlineData("../etc/passwd")]
        public void ValidatePathSafety_Should_Fail_On_Path_Traversal(string filePath)
        {
            var result = filePath.ValidatePathSafety("p");
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidatePathSafety_Should_Fail_On_Absolute_Path_When_Not_Allowed()
        {
            var result = SecurityExtensions.ValidatePathSafety("C:/Windows/system32", "p", allowAbsolutePaths: false);
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidatePathSafety_Should_Allow_Absolute_When_Allowed()
        {
            var result = SecurityExtensions.ValidatePathSafety("C:/Windows/system32", "p", allowAbsolutePaths: true);
            // may still fail on reserved names or unsafe; here should be valid
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("run.exe")]
        [InlineData("script.ps1")]
        public void ValidatePathSafety_Should_Fail_On_Dangerous_Extensions(string filePath)
        {
            var result = filePath.ValidatePathSafety("p");
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidatePathSafety_Should_Fail_On_Null_Byte()
        {
            var result = SecurityExtensions.ValidatePathSafety("file\0name.txt", "p");
            result.IsValid.Should().BeFalse();
        }

        [Theory]
        [InlineData("CON.txt")]
        [InlineData("NUL.txt")]
        public void ValidatePathSafety_Should_Fail_On_Reserved_Names(string filePath)
        {
            var result = filePath.ValidatePathSafety("p");
            result.IsValid.Should().BeFalse();
        }
        #endregion

        #region ValidateEmail
        [Theory]
        [InlineData("user@example.com")]
        [InlineData("first.last+tag@sub.domain.co")] 
        public void ValidateEmail_Should_Succeed_On_Valid(string email)
        {
            var result = email.ValidateEmail("email");
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid")]
        [InlineData("user@@example.com")]
        [InlineData("user..dot@example.com")]
        public void ValidateEmail_Should_Fail_On_Invalid(string email)
        {
            var result = email.ValidateEmail("email");
            result.IsValid.Should().BeFalse();
        }
        #endregion

        #region ValidatePhoneNumber
        [Theory]
        [InlineData("+1 (234) 567-8901")]
        [InlineData("0912-123-4567")]
        [InlineData("021 123 4567")]
        public void ValidatePhoneNumber_Should_Succeed_On_Valid(string phone)
        {
            var result = phone.ValidatePhoneNumber("phone");
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData("12-34")]
        [InlineData("abc-123")] 
        public void ValidatePhoneNumber_Should_Fail_On_Invalid(string phone)
        {
            var result = phone.ValidatePhoneNumber("phone");
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidatePhoneNumber_Should_Require_CountryCode_When_Configured()
        {
            var result = "1234567890".ValidatePhoneNumber("phone", requireCountryCode: true);
            result.IsValid.Should().BeFalse();
        }
        #endregion

        #region ValidateUrl
        [Theory]
        [InlineData("http://example.com")]
        [InlineData("https://example.com/path?x=1")] 
        public void ValidateUrl_Should_Succeed_On_Valid_Http_And_Https(string url)
        {
            var result = url.ValidateUrl("u");
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData("not a url")]
        [InlineData("http://")] 
        public void ValidateUrl_Should_Fail_On_Invalid_Format(string url)
        {
            var result = url.ValidateUrl("u");
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateUrl_Should_Fail_On_Disallowed_Scheme()
        {
            var result = "ftp://example.com".ValidateUrl("u");
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateUrl_Should_Allow_Custom_Scheme_When_Specified()
        {
            var result = "ftp://example.com".ValidateUrl("u", new[] { "http", "https", "ftp" });
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("http://localhost")] 
        [InlineData("http://127.0.0.1")] 
        [InlineData("http://192.168.1.10")] 
        [InlineData("http://10.0.0.1")] 
        public void ValidateUrl_Should_Fail_On_Private_Or_Localhost(string url)
        {
            var result = url.ValidateUrl("u");
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateUrl_Should_Fail_On_Suspicious_Patterns()
        {
            var result = "http://example.com/data:text/html,<script>".ValidateUrl("u");
            result.IsValid.Should().BeFalse();
        }
        #endregion

        #region ValidatePasswordStrength
        [Fact]
        public void ValidatePasswordStrength_Should_Fail_On_Empty()
        {
            var res = SecurityExtensions.ValidatePasswordStrength("");
            res.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidatePasswordStrength_Should_Evaluate_Strength_And_Issues()
        {
            var weak = SecurityExtensions.ValidatePasswordStrength("pass");
            weak.IsValid.Should().BeFalse();
            weak.Strength.Should().BeOneOf(new[] { PasswordStrength.Weak, PasswordStrength.VeryWeak });

            var strong = SecurityExtensions.ValidatePasswordStrength("Str0ng!Passw0rd");
            strong.IsValid.Should().BeTrue();
            strong.Strength.Should().BeOneOf(new[] { PasswordStrength.Strong, PasswordStrength.VeryStrong });
        }

        [Fact]
        public void ValidatePasswordStrength_Should_Respect_Policy()
        {
            var policy = new PasswordPolicy
            {
                MinLength = 12,
                RecommendedLength = 16,
                RequireUppercase = true,
                RequireLowercase = true,
                RequireDigits = true,
                RequireSpecialChars = true
            };

            var res = SecurityExtensions.ValidatePasswordStrength("Short1!", policy);
            res.IsValid.Should().BeFalse();

            var ok = SecurityExtensions.ValidatePasswordStrength("VeryStr0ng!Pass", policy);
            ok.IsValid.Should().BeTrue();
        }
        #endregion

        #region Sanitization
        [Fact]
        public void SanitizeForHtml_Should_Encode_And_Remove_Dangerous_Patterns()
        {
            var input = "<img src=\"x\" onerror=\"do()\"> javascript:alert(1)";
            var sanitized = SecurityExtensions.SanitizeForHtml(input);
            // encoded angle brackets
            sanitized.Should().Contain("&lt;");
            // javascript: removed
            sanitized.Should().NotContain("javascript:");
        }

        [Fact]
        public void SanitizeForSql_Should_Escape_And_Remove_Dangerous_Tokens()
        {
            var input = "O'Hara -- comment; /*blk*/";
            var sanitized = SecurityExtensions.SanitizeForSql(input);
            sanitized.Should().Contain("O''Hara");
            sanitized.Should().NotContain("--");
            sanitized.Should().NotContain(";");
            sanitized.Should().NotContain("/*");
            sanitized.Should().NotContain("*/");
        }

        [Fact]
        public void SanitizeFilePath_Should_Remove_Dangerous_Chars_And_Traversal()
        {
            var input = "..\\my<inva>lid:fi|le?.txt";
            var sanitized = SecurityExtensions.SanitizeFilePath(input, allowDirectorySeparators: false);
            sanitized.Should().NotContain("..");
            sanitized.Should().NotContainAny(new[] { "<", ">", ":", "\"", "|", "?", "*", "/", "\\" });
        }
        #endregion

        #region ValidationResult_ThrowIfInvalid
        [Fact]
        public void ValidationResult_ThrowIfInvalid_Should_Throw()
        {
            var result = ValidationResult.Failure("err", new List<string> { "a", "b" });
            Action act = () => result.ThrowIfInvalid();
            act.Should().Throw<PowNet.Common.PowNetValidationException>();
        }

        [Fact]
        public void ValidationResult_ThrowIfInvalid_Should_Not_Throw_When_Valid()
        {
            var result = ValidationResult.Success();
            Action act = () => result.ThrowIfInvalid();
            act.Should().NotThrow();
        }
        #endregion

        #region AdditionalSecurityExtensions
        [Fact]
        public void AdditionalSecurityExtensions_Should_Work()
        {
            // ConstantTimeEquals
            var a = new byte[]{1,2,3};
            var b = new byte[]{1,2,3};
            var c = new byte[]{1,2,4};
            a.AsSpan().ConstantTimeEquals(b).Should().BeTrue();
            a.AsSpan().ConstantTimeEquals(c).Should().BeFalse();

            // SafeJoin
            var root = new DirectoryInfo(Path.GetTempPath());
            var safe = root.SafeJoin("sub","file.txt");
            safe.StartsWith(root.FullName, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
            Action bad = () => root.SafeJoin("..","..","etc","passwd");
            bad.Should().Throw<UnauthorizedAccessException>();

            // EnsureNotDefault
            Action ex = () => default(int).EnsureNotDefault("x");
            ex.Should().Throw<ArgumentException>();
            1.EnsureNotDefault("x").Should().Be(1);
        }
        #endregion
    }
}
