using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class ListExtensionsTests
    {
        [Fact]
        public void TryAdd_Object_Should_Add_When_Not_Exists_And_Not_Null()
        {
            List<object>? list = new();
            var ok1 = list.TryAdd("x");
            var ok2 = list.TryAdd("x");
            ok1.Should().BeTrue();
            ok2.Should().BeFalse();
            list.Should().HaveCount(1);
        }

        [Fact]
        public void TryAdd_Object_Should_Respect_AddNull_Flag()
        {
            List<object>? list = new();
            list.TryAdd(null!, addNull: false).Should().BeFalse();
            list.Should().BeEmpty();
        }

        [Fact]
        public void TryAdd_String_Should_Add_When_Not_Exists_And_Not_Null()
        {
            List<string>? list = new();
            var ok1 = list.TryAdd("x");
            var ok2 = list.TryAdd("x");
            ok1.Should().BeTrue();
            ok2.Should().BeFalse();
            list.Should().HaveCount(1);
        }

        [Fact]
        public void TryAdd_String_Should_Respect_AddNull_Flag()
        {
            List<string>? list = new();
            list.TryAdd(null!, addNull: false).Should().BeFalse();
            list.Should().BeEmpty();
        }

        [Fact]
        public void ContainsIgnoreCase_Should_Find_Ignoring_Case()
        {
            List<string>? list = new() { "Alpha", "Beta" };
            list.ContainsIgnoreCase("alpha").Should().BeTrue();
            list.ContainsIgnoreCase("gamma").Should().BeFalse();
            ((List<string>?)null).ContainsIgnoreCase("x").Should().BeFalse();
        }

        [Fact]
        public void HasIntersect_String_With_Array_Should_Detect()
        {
            var l1 = new List<string> { "a", "b" };
            l1.HasIntersect(new[] { "c", "B" }).Should().BeTrue();
            l1.HasIntersect(Array.Empty<string>()).Should().BeFalse();
        }

        [Fact]
        public void HasIntersect_Int_With_Array_Should_Detect()
        {
            var l1 = new List<int> { 1, 2, 3 };
            l1.HasIntersect(new[] { 4, 5, 6 }).Should().BeFalse();
            l1.HasIntersect(new[] { 6, 3 }).Should().BeTrue();
        }

        [Fact]
        public void HasIntersect_Int_With_List_Should_Detect()
        {
            var l1 = new List<int> { 1, 2, 3 };
            var l2 = new List<int> { 3, 4, 5 };
            l1.HasIntersect(l2).Should().BeTrue();
            new List<int> { 1 }.HasIntersect(new List<int> { 2 }).Should().BeFalse();
        }
    }
}
