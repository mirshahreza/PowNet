using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class DataMapperExtensionsTests
    {
        public class A { public int Id { get; set; } public string? Name { get; set; } public B? Child { get; set; } }
        public class B { public int Code { get; set; } public string? Desc { get; set; } }
        public class C { public int Id { get; set; } public string? Name { get; set; } public int Code { get; set; } }

        [Fact]
        public void SmartMap_Should_Map_Matching_Props()
        {
            var a = new A { Id = 1, Name = "N" };
            var c = a.SmartMap<A,C>();
            c.Id.Should().Be(1);
            c.Name.Should().Be("N");
        }

        [Fact]
        public void SmartMapCollection_Should_Work()
        {
            var list = Enumerable.Range(1, 10).Select(i => new A { Id = i }).ToList();
            var outList = list.SmartMapCollection<A,C>();
            outList.Count.Should().Be(10);
            outList[0].Id.Should().Be(1);
        }

        [Fact]
        public void DeepClone_And_MergeObjects_Should_Work()
        {
            var a = new A { Id = 1, Name = "N", Child = new B { Code = 2, Desc = "d" } };
            var clone = a.DeepClone();
            clone.Child!.Desc.Should().Be("d");

            var t = new A();
            t.MergeObjects(new { Id = 3 }, new { Name = "X" });
            t.Id.Should().Be(3);
            t.Name.Should().Be("X");
        }

        [Fact]
        public void Flatten_And_Project_Should_Work()
        {
            var a = new A { Id = 1, Name = "N", Child = new B { Code = 2 } };
            var flat = a.Flatten();
            flat["Id"].Should().Be(1);
            flat["Child.Code"].Should().Be(2);

            var proj = a.Project(x => new { x.Id, x.Name });
            proj.Id.Should().Be(1);
        }

        [Fact]
        public void NestedProperty_Get_Set_Should_Work()
        {
            var a = new A();
            a.SetNestedProperty("Child.Code", 9).Should().BeTrue();
            a.GetNestedProperty("Child.Code").Should().Be(9);
        }

        [Fact]
        public void Transform_MapIf_AggregateMap_Should_Work()
        {
            var a = new A { Id = 2, Name = "N" };
            var res = a.Transform<A,C>(
                new DataTransformationRule<A,C> { Name = "Id", IsRequired = true, Apply = (s,d) => d.Id = s.Id },
                new DataTransformationRule<A,C> { Name = "Name", Apply = (s,d) => d.Name = s.Name }
            );
            res.Id.Should().Be(2);
            res.Name.Should().Be("N");

            var mapped = a.MapIf(x => x.Id > 1, x => new C { Id = x.Id }, x => new C { Id = 0 });
            mapped.Id.Should().Be(2);

            var agg = Enumerable.Range(1,3).Select(i => new A{ Id=i}).AggregateMap(x => new C{ Id = x.Id }, list => new C{ Id = list.Sum(i=>i.Id)});
            agg.Id.Should().Be(6);
        }
    }
}
