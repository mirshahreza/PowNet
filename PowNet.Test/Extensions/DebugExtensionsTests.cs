using System.Diagnostics;
using FluentAssertions;
using Moq;
using Xunit;
using PowNet.Extensions;
using PowNet.Common;
using PowNet.Configuration;

namespace PowNet.Test.Extensions
{
    public class DebugExtensionsTests : IDisposable
    {
        private readonly string? _oldDotnetEnv;
        private readonly string? _oldAspNetEnv;
        private readonly string? _oldPowNetEnv;
        private readonly string _prevEnvironmentValue;

        public DebugExtensionsTests()
        {
            // Capture previous environment variables (do not touch PowNetConfiguration yet)
            _oldDotnetEnv = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            _oldAspNetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            _oldPowNetEnv = Environment.GetEnvironmentVariable("PowNet_ENVIRONMENT");
            _prevEnvironmentValue = _oldDotnetEnv ?? _oldAspNetEnv ?? _oldPowNetEnv ?? "Production";

            // Force Development for tests
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("PowNet_ENVIRONMENT", "Development");

            // Set framework config without triggering file IO
            PowNetConfiguration.Environment = "Development";
            PowNetConfiguration.RefreshSettings();
        }

        public void Dispose()
        {
            // Restore original environment variables
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _oldDotnetEnv);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _oldAspNetEnv);
            Environment.SetEnvironmentVariable("PowNet_ENVIRONMENT", _oldPowNetEnv);

            // Restore framework environment and refresh cache
            PowNetConfiguration.Environment = _prevEnvironmentValue;
            PowNetConfiguration.RefreshSettings();
        }

        #region Method Profiling Extensions Tests

        [Fact]
        public void Profile_Should_Execute_Function_And_Return_Result()
        {
            // Arrange
            var expectedResult = 42;
            Func<int> testFunc = () => expectedResult;

            // Act
            var result = testFunc.Profile();

            // Assert
            result.Should().Be(expectedResult);
        }

        [Fact]
        public void Profile_Should_Handle_Exception_And_Rethrow()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test exception");
            Func<int> testFunc = () => throw expectedException;

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => testFunc.Profile());
            exception.Message.Should().Be("Test exception");
        }

        [Fact]
        public void Profile_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";
            var callCount = 0;
            Func<int> testFunc = () => { callCount++; return 42; };

            // Act
            var result = testFunc.Profile();

            // Assert
            result.Should().Be(42);
            callCount.Should().Be(1);
        }

        [Fact]
        public async Task ProfileAsync_Should_Execute_Async_Function_And_Return_Result()
        {
            // Arrange
            var expectedResult = "test result";
            Func<Task<string>> testFunc = async () =>
            {
                await Task.Delay(10);
                return expectedResult;
            };

            // Act
            var result = await testFunc.ProfileAsync();

            // Assert
            result.Should().Be(expectedResult);
        }

        [Fact]
        public async Task ProfileAsync_Should_Handle_Exception_And_Rethrow()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Async test exception");
            Func<Task<string>> testFunc = async () =>
            {
                await Task.Delay(10);
                throw expectedException;
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => testFunc.ProfileAsync());
            exception.Message.Should().Be("Async test exception");
        }

        [Fact]
        public async Task ProfileAsync_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";
            var callCount = 0;
            Func<Task<int>> testFunc = async () =>
            {
                await Task.Delay(10);
                callCount++;
                return 42;
            };

            // Act
            var result = await testFunc.ProfileAsync();

            // Assert
            result.Should().Be(42);
            callCount.Should().Be(1);
        }

        #endregion

        #region Object Debugging Extensions Tests

        [Fact]
        public void DebugDump_Should_Return_Same_Object()
        {
            // Arrange
            var testObject = new { Name = "Test", Value = 123 };

            // Act
            var result = testObject.DebugDump("TestObject");

            // Assert
            result.Should().BeSameAs(testObject);
        }

        [Fact]
        public void DebugDump_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";
            var testObject = new { Name = "Test" };

            // Act
            var result = testObject.DebugDump();

            // Assert
            result.Should().BeSameAs(testObject);
        }

        [Fact]
        public void DebugTrace_Should_Execute_Without_Parameters()
        {
            // Act & Assert - Should not throw
            var exception = Record.Exception(() => DebugExtensions.DebugTrace());
            exception.Should().BeNull();
        }

        [Fact]
        public void DebugTrace_Should_Execute_With_Parameters()
        {
            // Arrange
            var parameters = new { UserId = 123, Action = "Test" };

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => DebugExtensions.DebugTrace(parameters));
            exception.Should().BeNull();
        }

        [Fact]
        public void DebugTrace_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => DebugExtensions.DebugTrace());
            exception.Should().BeNull();
        }

        [Fact]
        public void DebugAssert_Should_Pass_When_Condition_True()
        {
            // Act & Assert - Should not throw
            var exception = Record.Exception(() => true.DebugAssert("This should not fail"));
            exception.Should().BeNull();
        }

        [Fact]
        public void DebugAssert_Should_Throw_When_Condition_False()
        {
            // Act & Assert
            var exception = Assert.Throws<PowNetException>(() => false.DebugAssert("Test assertion failure"));
            exception.Message.Should().Contain("ASSERTION FAILED");
            exception.Message.Should().Contain("Test assertion failure");
        }

        [Fact]
        public void DebugAssert_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";

            // Act & Assert - Should not throw even with false condition
            var exception = Record.Exception(() => false.DebugAssert("This should be skipped"));
            exception.Should().BeNull();
        }

        [Fact]
        public void DebugAssert_Should_Skip_When_Condition_True_In_Non_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => true.DebugAssert("This should pass anyway"));
            exception.Should().BeNull();
        }

        #endregion

        #region Performance Monitoring Extensions Tests

        [Fact]
        public void MonitorMemory_Should_Execute_Function_And_Return_Result()
        {
            // Arrange
            var expectedResult = "memory test result";
            Func<string> testFunc = () => expectedResult;

            // Act
            var result = testFunc.MonitorMemory("TestOperation");

            // Assert
            result.Should().Be(expectedResult);
        }

        [Fact]
        public void MonitorMemory_Should_Handle_Exception_And_Rethrow()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Memory test exception");
            Func<string> testFunc = () => throw expectedException;

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => testFunc.MonitorMemory("TestOperation"));
            exception.Message.Should().Be("Memory test exception");
        }

        [Fact]
        public void MonitorMemory_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";
            var callCount = 0;
            Func<int> testFunc = () => { callCount++; return 42; };

            // Act
            var result = testFunc.MonitorMemory("TestOperation");

            // Assert
            result.Should().Be(42);
            callCount.Should().Be(1);
        }

        [Fact]
        public void Benchmark_Should_Execute_Function_And_Return_Result()
        {
            // Arrange
            var expectedResult = 100;
            Func<int> testFunc = () =>
            {
                Thread.Sleep(10); // Small delay to measure
                return expectedResult;
            };

            // Act
            var result = testFunc.Benchmark("TestBenchmark");

            // Assert
            result.Should().Be(expectedResult);
        }

        [Fact]
        public void Benchmark_Should_Handle_Exception_And_Rethrow()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Benchmark test exception");
            Func<int> testFunc = () => throw expectedException;

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => testFunc.Benchmark("TestBenchmark"));
            exception.Message.Should().Be("Benchmark test exception");
        }

        [Fact]
        public void Benchmark_Should_Compare_With_Expected_Duration()
        {
            // Arrange
            var expectedDuration = TimeSpan.FromMilliseconds(50);
            Func<int> fastFunc = () => 42; // Very fast function
            Func<int> slowFunc = () =>
            {
                Thread.Sleep(100); // Intentionally slow
                return 42;
            };

            // Act & Assert - Should not throw for both cases
            var fastException = Record.Exception(() => fastFunc.Benchmark("FastTest", expectedDuration));
            var slowException = Record.Exception(() => slowFunc.Benchmark("SlowTest", expectedDuration));
            
            fastException.Should().BeNull();
            slowException.Should().BeNull();
        }

        [Fact]
        public void Benchmark_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";
            var callCount = 0;
            Func<int> testFunc = () => { callCount++; return 42; };

            // Act
            var result = testFunc.Benchmark("TestBenchmark");

            // Assert
            result.Should().Be(42);
            callCount.Should().Be(1);
        }

        #endregion

        #region Data Validation Extensions Tests

        [Fact]
        public void ValidateAndTrace_Should_Return_Object_When_Valid()
        {
            // Arrange
            var testObject = "test string";
            Func<string, bool> validator = s => s.Length > 0;

            // Act
            var result = testObject.ValidateAndTrace(validator, "NonEmptyString");

            // Assert
            result.Should().Be(testObject);
        }

        [Fact]
        public void ValidateAndTrace_Should_Throw_When_Invalid()
        {
            // Arrange
            var testObject = "";
            Func<string, bool> validator = s => s.Length > 0;

            // Act & Assert
            var exception = Assert.Throws<PowNetValidationException>(() => 
                testObject.ValidateAndTrace(validator, "NonEmptyString"));
            exception.Message.Should().Contain("Validation 'NonEmptyString' failed");
        }

        [Fact]
        public void ValidateAndTrace_Should_Handle_Validator_Exception()
        {
            // Arrange
            var testObject = "test";
            Func<string, bool> validator = s => throw new InvalidOperationException("Validator error");

            // Act & Assert
            var exception = Assert.Throws<PowNetValidationException>(() => 
                testObject.ValidateAndTrace(validator, "TestValidation"));
            exception.Message.Should().Contain("threw exception");
        }

        [Fact]
        public void ValidateAndTrace_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";
            var testObject = "";
            Func<string, bool> validator = s => s.Length > 0; // This would normally fail

            // Act
            var result = testObject.ValidateAndTrace(validator, "NonEmptyString");

            // Assert
            result.Should().Be(testObject);
        }

        [Fact]
        public void ValidateCollection_Should_Return_Collection_When_Valid()
        {
            // Arrange
            var testCollection = new List<int> { 1, 2, 3, 4, 5 };
            Func<int, bool> itemValidator = i => i > 0;

            // Act
            var result = testCollection.ValidateCollection("TestNumbers", itemValidator, 3, 10);

            // Assert
            result.Should().BeEquivalentTo(testCollection);
        }

        [Fact]
        public void ValidateCollection_Should_Handle_Count_Constraints()
        {
            // Arrange
            var smallCollection = new List<int> { 1, 2 };
            var largeCollection = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

            // Act & Assert - Should not throw but may log warnings
            var smallException = Record.Exception(() => smallCollection.ValidateCollection("SmallCollection", null, 5, 10));
            var largeException = Record.Exception(() => largeCollection.ValidateCollection("LargeCollection", null, 3, 5));
            
            smallException.Should().BeNull();
            largeException.Should().BeNull();
        }

        [Fact]
        public void ValidateCollection_Should_Handle_Item_Validation_Failures()
        {
            // Arrange
            var mixedCollection = new List<int> { 1, -2, 3, -4, 5 };
            Func<int, bool> itemValidator = i => i > 0;

            // Act & Assert - Should not throw but may log warnings about invalid items
            var exception = Record.Exception(() => mixedCollection.ValidateCollection("MixedNumbers", itemValidator));
            exception.Should().BeNull();
        }

        [Fact]
        public void ValidateCollection_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";
            var testCollection = new List<int> { 1, 2, 3 };

            // Act
            var result = testCollection.ValidateCollection("TestCollection");

            // Assert
            result.Should().BeEquivalentTo(testCollection);
        }

        #endregion

        #region Exception Context Extensions Tests

        [Fact]
        public void AddDebugContext_Should_Add_Context_To_PowNetException()
        {
            // Arrange
            var originalException = new PowNetException("Test exception");

            // Act
            var result = originalException.AddDebugContext();

            // Assert
            result.Should().BeSameAs(originalException);
            var PowNetEx = (PowNetException)result;
            PowNetEx.GetParam<string>("DebugLocation").Should().NotBeNull();
            PowNetEx.GetParam<string>("DebugTimestamp").Should().NotBeNull();
        }

        [Fact]
        public void AddDebugContext_Should_Add_Context_To_Regular_Exception()
        {
            // Arrange
            var originalException = new InvalidOperationException("Test exception");

            // Act
            var result = originalException.AddDebugContext();

            // Assert
            result.Should().BeSameAs(originalException);
            result.Data["DebugLocation"].Should().NotBeNull();
            result.Data["DebugTimestamp"].Should().NotBeNull();
        }

        [Fact]
        public void AddDebugContext_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";
            var originalException = new InvalidOperationException("Test exception");
            var originalDataCount = originalException.Data.Count;

            // Act
            var result = originalException.AddDebugContext();

            // Assert
            result.Should().BeSameAs(originalException);
            result.Data.Count.Should().Be(originalDataCount); // No new data added
        }

        [Fact]
        public void LogWithDebugContext_Should_Execute_Without_Throwing()
        {
            // Arrange
            var testException = new InvalidOperationException("Test logging exception");

            // Act & Assert - Should not throw
            var exceptionWithoutMessage = Record.Exception(() => testException.LogWithDebugContext());
            var exceptionWithMessage = Record.Exception(() => testException.LogWithDebugContext("Additional message"));
            
            exceptionWithoutMessage.Should().BeNull();
            exceptionWithMessage.Should().BeNull();
        }

        #endregion

        #region Conditional Debugging Tests

        [Fact]
        public void WhenDebuggerAttached_Should_Execute_Action_When_Development()
        {
            // Arrange
            var actionExecuted = false;
            Action testAction = () => actionExecuted = true;

            // Act
            DebugExtensions.WhenDebuggerAttached(testAction);

            // Assert
            // Note: This test behavior depends on whether debugger is attached
            // In CI/CD environments, debugger typically won't be attached
            if (Debugger.IsAttached)
            {
                actionExecuted.Should().BeTrue();
            }
        }

        [Fact]
        public void WhenDebuggerAttached_Should_Handle_Action_Exception()
        {
            // Arrange
            Action throwingAction = () => throw new InvalidOperationException("Test exception");

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => DebugExtensions.WhenDebuggerAttached(throwingAction));
            exception.Should().BeNull();
        }

        [Fact]
        public void WhenDevelopment_Should_Execute_Action_When_Development()
        {
            // Arrange
            var actionExecuted = false;
            Action testAction = () => actionExecuted = true;

            // Act
            DebugExtensions.WhenDevelopment(testAction);

            // Assert
            actionExecuted.Should().BeTrue();
        }

        [Fact]
        public void WhenDevelopment_Should_Skip_When_Not_Development()
        {
            // Arrange
            PowNetConfiguration.Environment = "Production";
            var actionExecuted = false;
            Action testAction = () => actionExecuted = true;

            // Act
            DebugExtensions.WhenDevelopment(testAction);

            // Assert
            actionExecuted.Should().BeFalse();
        }

        [Fact]
        public void WhenDevelopment_Should_Handle_Action_Exception()
        {
            // Arrange
            Action throwingAction = () => throw new InvalidOperationException("Test exception");

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => DebugExtensions.WhenDevelopment(throwingAction));
            exception.Should().BeNull();
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData("test string", "\"test string\"")]
        [InlineData(42, "42")]
        [InlineData(true, "True")]
        [InlineData(3.14, "3.14")]
        public void ToDebugString_Should_Handle_Primitive_Types(object? input, string expected)
        {
            // Act
            var result = input.ToDebugString();

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void ToDebugString_Should_Handle_Complex_Objects()
        {
            // Arrange
            var complexObject = new { Name = "Test", Value = 123, Items = new[] { 1, 2, 3 } };

            // Act
            var result = complexObject.ToDebugString();

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Name");
            result.Should().Contain("Test");
            result.Should().Contain("Value");
            result.Should().Contain("123");
        }

        [Fact]
        public void ToDebugString_Should_Truncate_Long_Strings()
        {
            // Arrange
            var longString = new string('x', 2000);
            var maxLength = 100;

            // Act
            var result = longString.ToDebugString(maxLength);

            // Assert
            result.Length.Should().BeLessThanOrEqualTo(maxLength + 10); // Account for quotes and ellipsis
            result.Should().EndWith("...");
        }

        [Fact]
        public void ToDebugString_Should_Handle_Serialization_Failures()
        {
            // Arrange
            var problematicObject = new ProblematicClass();

            // Act
            var result = problematicObject.ToDebugString();

            // Assert
            result.Should().NotBeNull();
            // Should fallback to ToString() or type name
        }

        #endregion

        #region Debug Attributes Tests

        [Fact]
        public void ProfileAttribute_Should_Have_Correct_Defaults()
        {
            // Arrange & Act
            var attribute = new ProfileAttribute();

            // Assert
            attribute.Category.Should().BeNull();
            attribute.LogParameters.Should().BeFalse();
            attribute.LogResult.Should().BeFalse();
            attribute.ExpectedDuration.Should().BeNull();
        }

        [Fact]
        public void ProfileAttribute_Should_Set_Properties_Correctly()
        {
            // Arrange & Act
            var attribute = new ProfileAttribute
            {
                Category = "TestCategory",
                LogParameters = true,
                LogResult = true,
                ExpectedDuration = TimeSpan.FromMilliseconds(100)
            };

            // Assert
            attribute.Category.Should().Be("TestCategory");
            attribute.LogParameters.Should().BeTrue();
            attribute.LogResult.Should().BeTrue();
            attribute.ExpectedDuration.Should().Be(TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void MonitorMemoryAttribute_Should_Have_Correct_Defaults()
        {
            // Arrange & Act
            var attribute = new MonitorMemoryAttribute();

            // Assert
            attribute.ExpectedMemoryUsage.Should().BeNull();
            attribute.FailOnExcessive.Should().BeFalse();
        }

        [Fact]
        public void MonitorMemoryAttribute_Should_Set_Properties_Correctly()
        {
            // Arrange & Act
            var attribute = new MonitorMemoryAttribute
            {
                ExpectedMemoryUsage = 1024,
                FailOnExcessive = true
            };

            // Assert
            attribute.ExpectedMemoryUsage.Should().Be(1024);
            attribute.FailOnExcessive.Should().BeTrue();
        }

        [Fact]
        public void DebugOnlyAttribute_Should_Have_Correct_Defaults()
        {
            // Arrange & Act
            var attribute = new DebugOnlyAttribute();

            // Assert
            attribute.Reason.Should().BeNull();
        }

        [Fact]
        public void DebugOnlyAttribute_Should_Set_Properties_Correctly()
        {
            // Arrange & Act
            var attribute = new DebugOnlyAttribute
            {
                Reason = "Performance testing only"
            };

            // Assert
            attribute.Reason.Should().Be("Performance testing only");
        }

        #endregion

        #region Helper Classes

        private class ProblematicClass
        {
            public override string ToString()
            {
                throw new InvalidOperationException("Cannot convert to string");
            }
        }

        #endregion
    }
}