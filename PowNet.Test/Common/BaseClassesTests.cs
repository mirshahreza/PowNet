using FluentAssertions;
using Xunit;
using PowNet.Common;

namespace PowNet.Test.Common
{
    public class BaseClassesTests
    {
        private class Entity : BaseEntity { }
        private class Root : AggregateRoot { public void AddEvt(IDomainEvent e) => base.AddDomainEvent(e); }
        private class Evt : DomainEvent { public override string EventType => "E"; public override object Data => 1; }
        private class C : ValueObject { public int X { get; set; } public int Y { get; set; } protected override IEnumerable<object?> GetEqualityComponents(){ yield return X; yield return Y; }}

        [Fact]
        public void BaseEntity_Audit_And_Equality_And_Clone()
        {
            var e = new Entity { Id = 1 };
            e.MarkAsDeleted("u");
            e.IsDeleted.Should().BeTrue();
            e.UpdateAuditFields("u2");
            e.UpdatedBy.Should().Be("u2");
            var c = (Entity)e.Clone();
            c.Id.Should().Be(1);
            (e==c).Should().BeTrue();
        }

        [Fact]
        public void AggregateRoot_DomainEvents()
        {
            var r = new Root();
            r.AddEvt(new Evt());
            r.DomainEvents.Count.Should().Be(1);
            var evt = r.DomainEvents.First();
            r.RemoveDomainEvent(evt);
            r.DomainEvents.Count.Should().Be(0);
            r.ClearDomainEvents();
        }

        [Fact]
        public void ValueObject_Equality()
        {
            var a = new C{X=1,Y=2};
            var b = new C{X=1,Y=2};
            (a==b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        private class Repo : BaseRepository<Entity>
        {
            public override Task<Entity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<Entity?>(new Entity{Id=id});
            public override Task<IEnumerable<Entity>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Entity>>(new[]{ new Entity{Id=1}});
            public override Task<Entity> AddAsync(Entity entity, CancellationToken cancellationToken = default) => Task.FromResult(entity);
            public override Task<Entity> UpdateAsync(Entity entity, CancellationToken cancellationToken = default) => Task.FromResult(entity);
            public override Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(true);
            public override Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(true);
            public override Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        }

        [Fact]
        public async Task BaseRepository_SoftDelete_Should_Work()
        {
            var repo = new Repo();
            var ok = await repo.SoftDeleteAsync(1, "u");
            ok.Should().BeTrue();
        }

        private class Svc : BaseService { public Task<int> SumAsync(int a, int b) => ExecuteAsync(async ()=> { await Task.Delay(1); return a+b; }, nameof(SumAsync), new { a, b }); }

        [Fact]
        public async Task BaseService_ExecuteAsync_Should_Wrap()
        {
            var svc = new Svc();
            var res = await svc.SumAsync(1,2);
            res.Should().Be(3);
        }

        private class Ctrl : BaseApiController { public ApiResponse<int> Add(int a, int b) => Success(a+b); public ApiResponse Err(string m) => Error(m); public ApiResponse? Val(object m) => ValidateModel(m);}

        [Fact]
        public void BaseApiController_Should_Create_Responses_And_Validate()
        {
            var c = new Ctrl();
            c.Add(1,2).Data.Should().Be(3);
            c.Err("e").IsSuccess.Should().BeFalse();

            var resp = c.Val(new { });
            resp.Should().BeNull();
        }
    }
}
