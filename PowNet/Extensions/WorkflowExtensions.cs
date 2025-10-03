using System.Collections.Concurrent;
using PowNet.Common;
using PowNet.Logging;

namespace PowNet.Extensions
{
    /// <summary>
    /// Workflow and business process management extensions for PowNet framework
    /// </summary>
    public static class WorkflowExtensions
    {
        private static readonly Logger _logger = PowNetLogger.GetLogger("Workflow");
        private static readonly ConcurrentDictionary<string, WorkflowEngine> _workflows = new();

        #region Workflow Builder

        /// <summary>
        /// Create a new workflow builder
        /// </summary>
        public static WorkflowBuilder CreateWorkflow(string name)
        {
            return new WorkflowBuilder(name);
        }

        /// <summary>
        /// Execute workflow with automatic state management
        /// </summary>
        public static async Task<WorkflowResult<T>> ExecuteWorkflowAsync<T>(
            this T input,
            string workflowName,
            CancellationToken cancellationToken = default)
        {
            if (!_workflows.TryGetValue(workflowName, out var workflow))
            {
                throw new PowNetException($"Workflow '{workflowName}' not found")
                    .AddParam("WorkflowName", workflowName);
            }

            return await workflow.ExecuteAsync(input, cancellationToken);
        }

        /// <summary>
        /// Register workflow for reuse
        /// </summary>
        public static void RegisterWorkflow(WorkflowEngine workflow)
        {
            _workflows.TryAdd(workflow.Name, workflow);
            _logger.LogInformation("Workflow '{WorkflowName}' registered successfully", workflow.Name);
        }

        #endregion

        #region Business Process Extensions

        /// <summary>
        /// Create approval workflow
        /// </summary>
        public static ApprovalWorkflow CreateApprovalWorkflow(string name, params string[] approvers)
        {
            return new ApprovalWorkflow(name, approvers);
        }

        /// <summary>
        /// Process batch operations with workflow
        /// </summary>
        public static async Task<BatchProcessResult<T>> ProcessBatchAsync<T>(
            this IEnumerable<T> items,
            Func<T, Task<bool>> processor,
            BatchProcessOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= BatchProcessOptions.Default;
            var results = new ConcurrentBag<BatchItemResult<T>>();
            var semaphore = new SemaphoreSlim(options.MaxConcurrency);
            var totalItems = items.Count();
            var processedCount = 0;

            var tasks = items.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var startTime = DateTime.UtcNow;
                    var success = false;
                    Exception? exception = null;

                    try
                    {
                        success = await processor(item);
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                        if (!options.ContinueOnError)
                            throw;
                    }

                    var result = new BatchItemResult<T>
                    {
                        Item = item,
                        Success = success,
                        Exception = exception,
                        ProcessedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };

                    results.Add(result);

                    var completed = Interlocked.Increment(ref processedCount);
                    options.ProgressCallback?.Invoke(completed, totalItems);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return new BatchProcessResult<T>
            {
                TotalItems = totalItems,
                ProcessedItems = results.Count,
                SuccessfulItems = results.Count(r => r.Success),
                FailedItems = results.Count(r => !r.Success),
                Results = results.ToList(),
                StartTime = DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(results.Sum(r => r.Duration.TotalMilliseconds))),
                EndTime = DateTime.UtcNow
            };
        }

        #endregion

        #region State Machine Extensions

        /// <summary>
        /// Create state machine for entity lifecycle management
        /// </summary>
        public static StateMachine<TState, TTrigger> CreateStateMachine<TState, TTrigger>(
            TState initialState,
            Action<StateMachineBuilder<TState, TTrigger>>? configure = null) 
            where TState : struct, Enum 
            where TTrigger : struct, Enum
        {
            var machine = new StateMachine<TState, TTrigger>(initialState);
            var builder = new StateMachineBuilder<TState, TTrigger>(machine);
            configure?.Invoke(builder);
            return machine;
        }

        /// <summary>
        /// Apply business rules to data processing
        /// </summary>
        public static async Task<RuleExecutionResult<T>> ApplyBusinessRulesAsync<T>(
            this T data,
            IEnumerable<IBusinessRule<T>> rules,
            RuleExecutionMode mode = RuleExecutionMode.StopOnFirstFailure)
        {
            var result = new RuleExecutionResult<T> { Data = data };
            var rulesList = rules.ToList();

            foreach (var rule in rulesList)
            {
                try
                {
                    var ruleResult = await rule.ValidateAsync(data);
                    result.RuleResults.Add(ruleResult);

                    if (!ruleResult.IsValid)
                    {
                        result.IsValid = false;
                        if (mode == RuleExecutionMode.StopOnFirstFailure)
                            break;
                    }
                }
                catch (Exception ex)
                {
                    result.RuleResults.Add(new RuleResult
                    {
                        RuleName = rule.GetType().Name,
                        IsValid = false,
                        ErrorMessage = ex.Message
                    });

                    result.IsValid = false;
                    if (mode == RuleExecutionMode.StopOnFirstFailure)
                        break;
                }
            }

            return result;
        }

        #endregion

        #region Pipeline Extensions

        /// <summary>
        /// Create data transformation pipeline
        /// </summary>
        public static DataPipeline<T> CreatePipeline<T>(this T input)
        {
            return new DataPipeline<T>(input);
        }

        /// <summary>
        /// Execute command with undo capability
        /// </summary>
        public static async Task<CommandResult<T>> ExecuteUndoableCommandAsync<T>(
            this IUndoableCommand<T> command,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                var result = await command.ExecuteAsync(cancellationToken);
                
                return new CommandResult<T>
                {
                    Success = true,
                    Result = result,
                    Command = command,
                    ExecutionTime = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                return new CommandResult<T>
                {
                    Success = false,
                    Exception = ex,
                    Command = command,
                    ExecutionTime = DateTime.UtcNow - startTime
                };
            }
        }

        #endregion
    }

    #region Workflow Classes

    public class WorkflowBuilder
    {
        private readonly List<WorkflowStep> _steps = new();
        private readonly string _name;

        public WorkflowBuilder(string name)
        {
            _name = name;
        }

        public WorkflowBuilder AddStep<T>(string name, Func<T, Task<T>> action)
        {
            _steps.Add(new WorkflowStep
            {
                Name = name,
                Action = async (input, ct) =>
                {
                    if (input is T typedInput)
                    {
                        return await action(typedInput);
                    }
                    throw new InvalidOperationException($"Input type mismatch in step '{name}'");
                }
            });
            return this;
        }

        public WorkflowBuilder AddCondition<T>(string name, Func<T, bool> condition, 
            WorkflowBuilder trueFlow, WorkflowBuilder? falseFlow = null)
        {
            _steps.Add(new ConditionalWorkflowStep
            {
                Name = name,
                Condition = input => condition((T)input),
                TrueSteps = trueFlow._steps,
                FalseSteps = falseFlow?._steps ?? new List<WorkflowStep>()
            });
            return this;
        }

        public WorkflowEngine Build()
        {
            return new WorkflowEngine(_name, _steps);
        }
    }

    public class WorkflowEngine
    {
        public string Name { get; }
        private readonly List<WorkflowStep> _steps;
        private readonly Logger _logger = PowNetLogger.GetLogger("WorkflowEngine");

        public WorkflowEngine(string name, List<WorkflowStep> steps)
        {
            Name = name;
            _steps = steps;
        }

        public async Task<WorkflowResult<T>> ExecuteAsync<T>(T input, CancellationToken cancellationToken = default)
        {
            var result = new WorkflowResult<T>
            {
                WorkflowName = Name,
                Input = input,
                StartTime = DateTime.UtcNow
            };

            object currentData = input!;

            try
            {
                foreach (var step in _steps)
                {
                    _logger.LogDebug("Executing workflow step: {StepName}", step.Name);
                    
                    var stepStartTime = DateTime.UtcNow;
                    currentData = await step.ExecuteAsync(currentData, cancellationToken);
                    var stepDuration = DateTime.UtcNow - stepStartTime;

                    result.ExecutedSteps.Add(new StepResult
                    {
                        StepName = step.Name,
                        Success = true,
                        Duration = stepDuration,
                        ExecutedAt = stepStartTime
                    });
                }

                result.Success = true;
                result.Output = (T)currentData;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Exception = ex;
                _logger.LogException(ex, "Workflow execution failed: {WorkflowName}", Name);
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
            }

            return result;
        }
    }

    public class ApprovalWorkflow
    {
        public string Name { get; }
        public List<string> Approvers { get; }
        public Dictionary<string, bool> Approvals { get; } = new();
        public ApprovalStatus Status { get; private set; } = ApprovalStatus.Pending;

        public ApprovalWorkflow(string name, params string[] approvers)
        {
            Name = name;
            Approvers = approvers.ToList();
            foreach (var approver in approvers)
            {
                Approvals[approver] = false;
            }
        }

        public bool Approve(string approver)
        {
            if (!Approvers.Contains(approver))
                return false;

            Approvals[approver] = true;
            UpdateStatus();
            return true;
        }

        public bool Reject(string approver)
        {
            if (!Approvers.Contains(approver))
                return false;

            Status = ApprovalStatus.Rejected;
            return true;
        }

        private void UpdateStatus()
        {
            if (Approvals.Values.All(a => a))
            {
                Status = ApprovalStatus.Approved;
            }
        }
    }

    public enum ApprovalStatus
    {
        Pending,
        Approved,
        Rejected
    }

    #endregion

    #region State Machine

    public class StateMachine<TState, TTrigger> 
        where TState : struct, Enum 
        where TTrigger : struct, Enum
    {
        public TState CurrentState { get; private set; }
        private readonly Dictionary<(TState, TTrigger), StateTransition<TState>> _transitions = new();

        public StateMachine(TState initialState)
        {
            CurrentState = initialState;
        }

        public void Configure(TState state, TTrigger trigger, TState destinationState, 
            Func<Task<bool>>? guard = null, Func<Task>? action = null)
        {
            _transitions[(state, trigger)] = new StateTransition<TState>
            {
                DestinationState = destinationState,
                Guard = guard,
                Action = action
            };
        }

        public async Task<bool> FireAsync(TTrigger trigger)
        {
            if (_transitions.TryGetValue((CurrentState, trigger), out var transition))
            {
                if (transition.Guard == null || await transition.Guard())
                {
                    CurrentState = transition.DestinationState;
                    if (transition.Action != null)
                        await transition.Action();
                    return true;
                }
            }
            return false;
        }
    }

    public class StateMachineBuilder<TState, TTrigger> 
        where TState : struct, Enum 
        where TTrigger : struct, Enum
    {
        private readonly StateMachine<TState, TTrigger> _machine;

        public StateMachineBuilder(StateMachine<TState, TTrigger> machine)
        {
            _machine = machine;
        }

        public StateMachineBuilder<TState, TTrigger> Permit(TState state, TTrigger trigger, TState destinationState)
        {
            _machine.Configure(state, trigger, destinationState);
            return this;
        }

        public StateMachineBuilder<TState, TTrigger> PermitIf(TState state, TTrigger trigger, 
            TState destinationState, Func<Task<bool>> guard)
        {
            _machine.Configure(state, trigger, destinationState, guard);
            return this;
        }
    }

    public class StateTransition<TState> where TState : struct, Enum
    {
        public TState DestinationState { get; set; }
        public Func<Task<bool>>? Guard { get; set; }
        public Func<Task>? Action { get; set; }
    }

    #endregion

    #region Business Rules

    public interface IBusinessRule<T>
    {
        string Name { get; }
        Task<RuleResult> ValidateAsync(T data);
    }

    public class RuleResult
    {
        public string RuleName { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class RuleExecutionResult<T>
    {
        public T Data { get; set; } = default!;
        public bool IsValid { get; set; } = true;
        public List<RuleResult> RuleResults { get; set; } = new();
    }

    public enum RuleExecutionMode
    {
        StopOnFirstFailure,
        ExecuteAll
    }

    #endregion

    #region Data Pipeline

    public class DataPipeline<T>
    {
        private T _data;
        private readonly List<Func<T, Task<T>>> _transformations = new();

        public DataPipeline(T data)
        {
            _data = data;
        }

        public DataPipeline<T> Transform(Func<T, T> transformation)
        {
            _transformations.Add(async data => transformation(data));
            return this;
        }

        public DataPipeline<T> TransformAsync(Func<T, Task<T>> transformation)
        {
            _transformations.Add(transformation);
            return this;
        }

        public DataPipeline<T> Validate(Func<T, bool> validator, string? errorMessage = null)
        {
            _transformations.Add(async data =>
            {
                if (!validator(data))
                {
                    throw new PowNetValidationException(errorMessage ?? "Pipeline validation failed", "Pipeline", data);
                }
                return data;
            });
            return this;
        }

        public async Task<T> ExecuteAsync()
        {
            var current = _data;
            foreach (var transformation in _transformations)
            {
                current = await transformation(current);
            }
            return current;
        }
    }

    #endregion

    #region Command Pattern

    public interface IUndoableCommand<T>
    {
        Task<T> ExecuteAsync(CancellationToken cancellationToken = default);
        Task UndoAsync(CancellationToken cancellationToken = default);
    }

    public class CommandResult<T>
    {
        public bool Success { get; set; }
        public T? Result { get; set; }
        public Exception? Exception { get; set; }
        public IUndoableCommand<T>? Command { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    #endregion

    #region Supporting Classes

    public class WorkflowStep
    {
        public string Name { get; set; } = string.Empty;
        public Func<object, CancellationToken, Task<object>>? Action { get; set; }

        public virtual async Task<object> ExecuteAsync(object input, CancellationToken cancellationToken)
        {
            if (Action == null)
                return input;

            return await Action(input, cancellationToken);
        }
    }

    public class ConditionalWorkflowStep : WorkflowStep
    {
        public Func<object, bool>? Condition { get; set; }
        public List<WorkflowStep> TrueSteps { get; set; } = new();
        public List<WorkflowStep> FalseSteps { get; set; } = new();

        public override async Task<object> ExecuteAsync(object input, CancellationToken cancellationToken)
        {
            if (Condition == null)
                return input;

            var steps = Condition(input) ? TrueSteps : FalseSteps;
            var current = input;

            foreach (var step in steps)
            {
                current = await step.ExecuteAsync(current, cancellationToken);
            }

            return current;
        }
    }

    public class WorkflowResult<T>
    {
        public string WorkflowName { get; set; } = string.Empty;
        public T Input { get; set; } = default!;
        public T? Output { get; set; }
        public bool Success { get; set; }
        public Exception? Exception { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public List<StepResult> ExecutedSteps { get; set; } = new();
    }

    public class StepResult
    {
        public string StepName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public DateTime ExecutedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public Exception? Exception { get; set; }
    }

    public class BatchProcessOptions
    {
        public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
        public bool ContinueOnError { get; set; } = true;
        public Action<int, int>? ProgressCallback { get; set; }

        public static BatchProcessOptions Default => new();
    }

    public class BatchProcessResult<T>
    {
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int SuccessfulItems { get; set; }
        public int FailedItems { get; set; }
        public List<BatchItemResult<T>> Results { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public double SuccessRate => ProcessedItems == 0 ? 0 : (double)SuccessfulItems / ProcessedItems * 100;
    }

    public class BatchItemResult<T>
    {
        public T Item { get; set; } = default!;
        public bool Success { get; set; }
        public Exception? Exception { get; set; }
        public DateTime ProcessedAt { get; set; }
        public TimeSpan Duration { get; set; }
    }

    #endregion
}