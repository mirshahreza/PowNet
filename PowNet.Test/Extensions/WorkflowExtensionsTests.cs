using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class WorkflowExtensionsTests
    {
        [Fact]
        public async Task Workflow_Builder_And_Engine_Should_Work()
        {
            var wf = WorkflowExtensions.CreateWorkflow("wf1")
                .AddStep<int>("inc", async x => { await Task.Delay(1); return x+1; })
                .AddStep<int>("double", async x => x*2)
                .Build();
            WorkflowExtensions.RegisterWorkflow(wf);
            var res = await 1.ExecuteWorkflowAsync("wf1");
            res.Success.Should().BeTrue();
            res.Output.Should().Be(4);
        }

        [Fact]
        public async Task ApprovalWorkflow_Should_Approve()
        {
            var aw = WorkflowExtensions.CreateApprovalWorkflow("ap", "a","b");
            aw.Approve("a").Should().BeTrue();
            aw.Approve("b").Should().BeTrue();
            aw.Status.Should().Be(ApprovalStatus.Approved);
        }

        [Fact]
        public async Task ProcessBatchAsync_Should_Return_Stats()
        {
            var items = Enumerable.Range(1, 10);
            var res = await items.ProcessBatchAsync(async i => { await Task.Delay(1); return i % 2 == 0;});
            res.TotalItems.Should().Be(10);
            res.ProcessedItems.Should().Be(10);
            (res.SuccessfulItems + res.FailedItems).Should().Be(10);
        }

        public enum S { A, B }
        public enum Tr { Go }

        [Fact]
        public async Task StateMachine_Should_Transition()
        {
            var sm = WorkflowExtensions.CreateStateMachine<S,Tr>(S.A, b => b.Permit(S.A, Tr.Go, S.B));
            (await sm.FireAsync(Tr.Go)).Should().BeTrue();
            sm.CurrentState.Should().Be(S.B);
        }

        public class R : IBusinessRule<int>
        {
            public string Name => "R";
            public Task<RuleResult> ValidateAsync(int data)
            {
                return Task.FromResult(new RuleResult{ RuleName = Name, IsValid = data > 0});
            }
        }

        [Fact]
        public async Task Rules_And_Pipeline_Should_Work()
        {
            var rr = await 1.ApplyBusinessRulesAsync(new []{ new R() });
            rr.IsValid.Should().BeTrue();

            var p = 1.CreatePipeline()
                .Transform(x => x+1)
                .TransformAsync(async x => { await Task.Delay(1); return x*2; })
                .Validate(x => x == 4);
            (await p.ExecuteAsync()).Should().Be(4);
        }

        public class Cmd : IUndoableCommand<int>
        {
            public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) => Task.FromResult(5);
            public Task UndoAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        [Fact]
        public async Task Command_Should_Return_Result()
        {
            var cmd = new Cmd();
            var res = await cmd.ExecuteUndoableCommandAsync();
            res.Success.Should().BeTrue();
            res.Result.Should().Be(5);
        }
    }
}
