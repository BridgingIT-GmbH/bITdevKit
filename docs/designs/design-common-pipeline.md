---
status: implemented
---

# Design Document: Generic Processing Pipeline Feature (Common.Utilities\Pipeline)

> This design document describes the architecture and behavior of a new generic processing pipeline framework for structuring ordered in-process processing logic as a sequence of focused steps. It defines the core design principles, conceptual model, control semantics, definition and execution model, static definition and runtime construction, extensibility points, and typical use cases for the pipeline feature.

[TOC]

## 1. Introduction

The generic processing pipeline is a reusable framework feature for structuring ordered in-process processing logic as a sequence of focused steps. It is intended for scenarios where data or requests must pass through multiple stages of processing while preserving consistency, extensibility, testability, and observability.

A pipeline is defined as:

> A pipeline is an ordered sequence of processing steps that operate on an optional mutable execution context. The context may contain input, result-related state, and any other execution state needed by the pipeline. Across the execution, the pipeline carries an accumulated `Result` that collects messages and errors from step to step. Each step may continue, retry, break, skip itself, or request termination of the remaining pipeline.

This feature is designed to provide a lightweight, strongly structured processing model that sits between simple service methods and full workflow or orchestration engines.

---

## 2. Purpose

The purpose of the generic processing pipeline is to provide a common conceptual and architectural model for multi-step processing flows across the framework.

It addresses recurring needs such as:

* decomposing large processing routines into focused steps
* standardizing ordered execution behavior
* sharing execution state across processing steps
* supporting runtime decisions about continuation, skipping, or early completion
* aligning processing outcomes with the framework’s result model
* improving diagnostics, supportability, and testability
* enabling reuse of a common pattern across multiple application areas

The pipeline feature is not intended to replace specialized workflow, orchestration, or state-machine platforms. Its purpose is to structure in-process application and domain processing logic in a clean, reusable way.

---

## 3. Design Goals

The design is guided by the following goals.

### 3.1 Reusable processing abstraction

The pipeline shall provide a generic processing abstraction that can be reused across domains and technical areas.

### 3.2 Ordered step-based execution

The pipeline shall model processing as an ordered sequence of steps where execution order is meaningful and explicit.

### 3.3 Strongly typed optional execution context

When a pipeline requires execution state, it shall use a strongly typed context specific to the pipeline type, while inheriting from a common conceptual base for general execution metadata.

The design shall also support pipelines that do not require a context at all.

### 3.4 Context-centric processing model

The pipeline shall use an optional execution context as the primary carrier of execution data.

This context-centric model allows consumers to decide whether the context contains input, result, both, or any other execution-scoped state relevant to the pipeline.

### 3.5 Explicit control semantics

The design shall explicitly support the following step outcomes:

* continue
* retry the current step
* skip self
* break the pipeline with a successful result
* request termination of the remaining pipeline

### 3.6 Consistent result handling

The feature shall align with the framework’s existing result pattern so that pipeline processing communicates success, failure, and diagnostics consistently.

### 3.7 Extensibility

The pipeline shall support custom steps, custom contexts, and extension points for cross-cutting concerns.

### 3.8 Runtime flexibility

The pipeline shall allow runtime decisions based on optional context, the accumulated carried `Result`, and execution metadata without losing the clarity of static structure.

### 3.9 Static definition with runtime construction

Pipeline structure shall be declared statically as a clear blueprint, while executable pipeline instances shall be constructed at runtime.

### 3.10 Testability

The design shall support isolated testing of steps and deterministic testing of whole pipeline flows.

### 3.11 Observability

The design shall support meaningful execution diagnostics and stage-level visibility.

### 3.12 Dependency injection friendly construction

The design shall support runtime construction through dependency injection so that pipeline steps can use constructor injection for repositories, services, policies, and other collaborators.

### 3.13 Explicit fire-and-forget execution

The design shall support an explicit fire-and-forget execution option so a caller can start a pipeline without waiting for the pipeline to finish, while the pipeline continues in the background.

### 3.14 Progress reporting for long-running execution

The design shall support progress reporting through execution options so long-running pipelines can provide structured feedback to the initiating caller while execution is in progress.

### 3.15 Low-friction developer experience

The design shall optimize for low-friction adoption by pipeline authors through sensible defaults, lightweight conventions, and fluent registration/building APIs so common scenarios require minimal configuration.

---

## 4. Scope and Boundaries

The generic processing pipeline is intentionally limited in scope.

It is intended to be:

* lightweight
* in-process
* deterministic in ordering
* optionally executable in a background fire-and-forget mode within the host process
* suitable for application and domain processing flows
* reusable across multiple scenarios
* no external dependencies beyond the framework’s core libraries

It is NOT intended to be:

* a workflow engine
* a business process management platform
* a distributed orchestration engine
* a scheduler
* a saga coordinator
* a rules engine
* a replacement for event-driven integration infrastructure
* a full state machine platform

These boundaries are essential to keep the design focused and prevent the feature from expanding into a more complex category of system.

---

## 5. Core Conceptual Model

The pipeline consists of a small set of core conceptual elements.

### 5.1 Pipeline

A pipeline is an ordered processing flow composed of multiple steps. It governs how execution progresses from one step to the next and how the accumulated execution `Result` reaches completion.

### 5.2 Execution context

The execution context is the primary carrier of execution data when a pipeline uses context.

It is an optional strongly typed execution-scoped object that may contain:

* input data
* result data
* execution metadata
* shared mutable state
* diagnostics or execution-scoped helper information

Pipelines that do not require execution state should be able to execute without creating or passing a context.

Each context-aware pipeline type defines its own specialized context, but all contexts share a sane conceptual base that provides common properties such as:

* a dedicated `Pipeline` child object for framework-owned execution metadata
* pipeline name
* correlation identifier
* execution identifier
* execution timing and simple pipeline metrics such as UTC started/completed timestamps
* general-purpose property bag
* shared execution metadata relevant to all pipelines

### 5.5 Processing step

A processing step is a focused unit of work within the pipeline. A step may:

* inspect or update the execution context when one is present
* produce diagnostics
* report progress from inside the step when execution options provide a progress reporter
* influence pipeline control flow

Steps should remain narrow in responsibility and aligned with the principle of separation of concerns.

### 5.6 Pipeline execution

A pipeline execution is a single runtime instance of a pipeline processing with an optional context.

Each execution is independent and carries its own context when a context is used.

Execution may be initiated either as a caller-awaited execution or as an explicit fire-and-forget background execution.

### 5.7 Conceptual Processing Model

The relationship between the core elements of the pipeline can be visualized conceptually as follows:

```text
          +--------------------+
          |  EXECUTION CONTEXT |
          | input/result/state |
          +---------+----------+
                    |
                    v
        +-------------------------+
        | PIPELINE PROCESSING |
        | ------------------- |
        | Step 1              |
        | Step 2              |
        | Step 3              |
        | ...                 |
        +-----------+-------------+
                    |
                    v
          +--------------------+
          |  UPDATED CONTEXT   |
          | progressively      |
          | changed state      |
          +---------+----------+
                    |
                    v
          +--------------------+
          |     FINAL RESULT   |
          | from context and   |
          | execution outcome  |
          +--------------------+
```

In this conceptual model:

* the **execution context**, when present, carries execution state and metadata
* the **pipeline steps** progressively act on that context while contributing to the carried `Result`
* the **final result** is the accumulated `Result` for the complete processing flow and may be interpreted alongside the final context

---

## 6. Context-Centric Processing Model

The pipeline follows a context-centric model where the optional execution context is the primary carrier of pipeline data.

### 6.1 Optional execution context

When present, the execution context contains the execution-scoped state needed by the pipeline.

Depending on the scenario, that context may include:

* original input or request data
* intermediate state
* result-related state
* diagnostics or progress-relevant state

### 6.2 Pipelines without context

Some pipelines may intentionally omit context when no shared execution state is needed.

In those cases, execution is driven only by the pipeline definition, the execution engine, the supplied execution options, and the engine-managed accumulated `Result`.

As an implementation detail, such pipelines may still be normalized internally onto a shared execution path by using an internal `NullPipelineContext`.

### 6.3 Why this model matters

This model is preferred because it keeps the framework simple:

* the pipeline framework only needs to understand optional context plus execution behavior
* consumers remain free to model input and result inside the context when that is useful
* the framework avoids imposing a mandatory distinction between input, mutable state, and result-related data when consumers do not need that separation

---

## 7. Context Design Concept

For context-aware pipelines, the context is the central part of the feature.

### 7.1 Strong typing by pipeline type

Each context-aware pipeline shall define its own strongly typed context. This ensures discoverability, correctness, and maintainability.

### 7.2 Common base context

All specialized contexts conceptually derive from a common base that captures shared execution properties.

This common base should include concepts such as:

* a dedicated child object for pipeline execution metadata
* pipeline identity
* correlation identity
* execution identity
* UTC started/completed timestamps and simple execution metrics
* general execution metadata
* general-purpose property bag storage

### 7.3 Role of the context

The context exists to:

* hold execution metadata
* support communication between steps
* support runtime decision-making
* enable correlation and diagnostics
* make execution-scoped capabilities such as progress reporting available to steps
* carry any input, result, or auxiliary state needed across the flow

Pipelines without shared execution state may omit the context entirely and rely only on execution options, the carried `Result`, and execution metadata managed by the engine.

### 7.4 Context discipline

The context should remain intentionally modeled. Strongly typed members should be preferred over ad hoc item storage whenever the data is known and relevant to the pipeline model.

The general-purpose item bag exists as a flexibility mechanism, not as the primary modeling strategy.

---

## 8. Control Semantics

Clear control semantics are a defining characteristic of the pipeline design.

The pipeline should treat step control as an integral combination of two parts:

* the carried non-generic `Result`, which communicates success, failure, messages, and errors
* the step control outcome, which communicates how the pipeline should proceed

Taken together, these form the full step-control contract. The pipeline should never evaluate the outcome without also evaluating the returned `Result`, and it should never evaluate the `Result` without also considering the requested control outcome.

### 8.1 Continue

A step may complete its work and allow processing to continue normally to the next step.

### 8.2 Skip self

A step may determine that it should not participate meaningfully in the current execution. This is a valid runtime outcome and not a failure.

### 8.3 Retry

A step may determine that the current step should be attempted again.

Retrying re-executes the same step and is typically used for transient conditions. The carried `Result` returned with `Retry` becomes the new carried `Result` for the next attempt, and the current context state is preserved exactly as the previous attempt left it.

Retries must be bounded by execution policy. If the configured retry limit for the current step is exhausted, the engine should append a descriptive retry-exhausted error to the carried `Result` and then evaluate continuation through the normal failure policy.

### 8.4 Break

A step may determine that the pipeline has already reached a valid completion point and that no further processing is required.

Breaking represents successful early completion unless the carried `Result` already indicates failure.

### 8.5 Request termination

A step may request termination of the remaining pipeline.

Termination ends further execution intentionally and is distinct from both breaking and failure.

### 8.6 Failure through the carried result

Failure should be represented through the framework’s untyped `Result`, not as a separate control outcome.

A step may therefore:

* return `Continue` while also returning an accumulated `Result` that is now in a failure state
* allow execution policy to decide whether that failure stops the pipeline immediately or permits later steps to continue

This keeps success/failure semantics aligned with the framework’s existing result model while preserving explicit pipeline control flow.

### 8.7 Context-aware and result-aware decision making

Step control decisions may be based on the current context, the current accumulated `Result`, and other execution metadata. This enables runtime flexibility while preserving a stable structural definition.

### 8.8 Control Flow Model

The following diagram illustrates how step control outcomes and the carried `Result` influence execution progression.

```text
                +-------------------+
                |   Execute Step    |
                +---------+---------+
                          |
                          v
                 +-----------------------+
                 | Return Updated Result |
                 | + Control Outcome     |
                 +-----------+-----------+
                             |
                             v
                 +-----------------------+
                 | Evaluate Result State |
                 | and Execution Policy  |
                 +-----------+-----------+
                             |
        +--------------------+--------------------+
        |                    |                    |
        v                    v                    v
   +-----------+       +-----------+        +-----------+
   | Continue  |       | Skip Self |        | Break     |
   |           |       |           |        |           |
   +-----+-----+       +-----+-----+        +-----+-----+
         |                   |                    |
         v                   v                    v
    Next Step           Next Step           Pipeline End

Additional outcomes:

   +-----------+
   | Retry     |
   +-----+-----+
         |
         v
     Same Step

   +-----------+
   | Terminate |
   +-----+-----+
         |
         v
   Pipeline End
```

Step control therefore determines whether execution continues, stops successfully, or stops intentionally by interpreting both the returned control outcome and the returned carried `Result` together.

---

## 9. Pipeline Definition and Execution Model

The design distinguishes clearly between pipeline definition and pipeline execution.

### 9.1 Pipeline definition

A pipeline definition is the static blueprint of a pipeline.

It expresses:

* the pipeline name
* the ordered processing steps
* structural conditions for step participation
* conceptual execution policies
* extension points around execution

The definition describes what the pipeline is.

### 9.2 Pipeline execution

A pipeline execution is a concrete runtime instance of the pipeline carrying a specific accumulated `Result` and, when relevant, a specific context.

Execution describes how the pipeline behaves for one processing request.

Pipeline execution also includes runtime execution options that influence how control outcomes such as failure, retry, and break are handled for that specific run.

Pipeline execution may also differ by initiation mode, including an explicit fire-and-forget mode where the caller starts the execution but does not wait for final completion.

### 9.3 Ordered progression

Pipeline execution proceeds through the defined step sequence in order, with each step participating according to current runtime conditions and prior outcomes.

The order of steps is defined by the order in which they are registered in the static fluent builder. This registration order represents the canonical execution order of the pipeline.

Order is part of the meaning of the pipeline and must be treated as intentional.

### 9.4 Execution initiation modes

The design should support at least two execution initiation modes:

* awaited execution, where the caller waits for the final result
* fire-and-forget execution, where the caller explicitly starts the pipeline and returns without waiting for completion

Fire-and-forget execution is a deliberate API choice on pipeline execution rather than an implicit policy side effect. The caller should opt into it explicitly.

In fire-and-forget mode, the pipeline still executes according to the same ordered definition and execution policies, but the final result is produced asynchronously in the background rather than being returned directly to the initiating caller.

---

## 10. Static Definition and Runtime Construction

The feature separates static structural definition from runtime construction.

### 10.1 Static structural definition

The pipeline structure is declared statically. The goal of static definition is clarity, discoverability, and intentional design.

The definition acts as a reusable blueprint.

### 10.2 Runtime construction

Executable pipelines are constructed at runtime based on the previously defined blueprint.

Runtime construction enables:

* named pipeline resolution
* dynamic enablement or disablement
* contextual configuration
* integration with application infrastructure

Runtime construction must also integrate with dependency injection. Pipeline definitions should describe step identity and order without requiring pre-created step instances. Concrete step instances should be resolved at runtime through the active service provider so constructor-injected dependencies are honored.

This means the construction model should treat steps as DI-managed components, typically resolved by type or by a factory that itself participates in dependency injection, rather than as manually `new`-created objects captured in the static definition.

Where execution uses scoped services such as repositories or unit-of-work abstractions, step resolution should occur within the appropriate execution scope so lifetimes remain correct and consistent with the rest of the application.

For fire-and-forget execution, runtime construction and execution must use a scope whose lifetime is owned by the background execution itself rather than by the initiating caller. Background execution must not depend on the caller's request scope remaining alive.

### 10.3 Rationale for the separation

This separation keeps the design conceptually clean:

* definition describes the intended processing structure
* construction produces an executable form for a concrete runtime scenario
* runtime construction can resolve DI-managed step instances and other execution services safely

### 10.4 High-Level Interface Direction

To steer the design toward implementation without over-committing to low-level details, the first interface cut should make the following ideas explicit:

* pipeline definitions are immutable blueprints
* step definitions store step descriptors, not step instances
* concrete steps are DI-resolved runtime components
* execution options remain execution-scoped and are not baked into the static definition
* hooks and behaviors are attached at definition time, but participate at execution time

The following interfaces are a suitable high-level starting point:

```csharp
public sealed class PipelineExecutionContext
{
    public string Name { get; set; }

    public Guid ExecutionId { get; set; }

    public string CorrelationId { get; set; }

    public DateTimeOffset StartedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }

    public string CurrentStepName { get; set; }

    public int ExecutedStepCount { get; set; }

    public int TotalStepCount { get; set; }

    public TimeSpan? Duration =>
        this.CompletedUtc is { } completedUtc
            ? completedUtc - this.StartedUtc
            : null;

    public PropertyBag Items { get; } = new();
}

public abstract class PipelineContextBase
{
    public PipelineExecutionContext Pipeline { get; } = new();
}

public sealed class NullPipelineContext : PipelineContextBase;

public interface IPipelineHook<TContext>
    where TContext : PipelineContextBase
{
    ValueTask OnPipelineStartingAsync(
        TContext context,
        CancellationToken cancellationToken);

    ValueTask OnStepStartingAsync(
        TContext context,
        IPipelineStepDefinition step,
        CancellationToken cancellationToken);

    ValueTask OnStepCompletedAsync(
        TContext context,
        IPipelineStepDefinition step,
        PipelineControl control,
        CancellationToken cancellationToken);

    ValueTask OnPipelineCompletedAsync(
        TContext context,
        Result result,
        CancellationToken cancellationToken);

    ValueTask OnPipelineFailedAsync(
        TContext context,
        Result result,
        CancellationToken cancellationToken);
}

public abstract class PipelineHook<TContext> : IPipelineHook<TContext>
    where TContext : PipelineContextBase
{
    public virtual ValueTask OnPipelineStartingAsync(
        TContext context,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public virtual ValueTask OnStepStartingAsync(
        TContext context,
        IPipelineStepDefinition step,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public virtual ValueTask OnStepCompletedAsync(
        TContext context,
        IPipelineStepDefinition step,
        PipelineControl control,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public virtual ValueTask OnPipelineCompletedAsync(
        TContext context,
        Result result,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public virtual ValueTask OnPipelineFailedAsync(
        TContext context,
        Result result,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}

public interface IPipelineBehavior<TContext>
    where TContext : PipelineContextBase
{
    ValueTask<Result> ExecuteAsync(
        TContext context,
        Func<ValueTask<Result>> next,
        CancellationToken cancellationToken);
}

public static class PipelineStepNameConvention
{
    public static string FromType(Type stepType)
    {
        var name = stepType.Name.EndsWith("Step", StringComparison.Ordinal)
            ? stepType.Name[..^4]
            : stepType.Name;

        return Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
    }
}

public static class PipelineNameConvention
{
    public static string FromType(Type pipelineType)
    {
        var name = pipelineType.Name.EndsWith("Pipeline", StringComparison.Ordinal)
            ? pipelineType.Name[..^8]
            : pipelineType.Name;

        return Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
    }
}

public interface IPipelineStep
{
    string Name { get; }

    ValueTask<PipelineControl> ExecuteAsync(
        PipelineContextBase context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken);
}

public abstract class PipelineStep : PipelineStep<NullPipelineContext>
{
    protected abstract PipelineControl Execute(
        Result result,
        PipelineExecutionOptions options);

    protected sealed override PipelineControl Execute(
        NullPipelineContext context,
        Result result,
        PipelineExecutionOptions options) =>
        this.Execute(result, options);
}

public abstract class PipelineStep<TContext> : IPipelineStep
    where TContext : PipelineContextBase
{
    public virtual string Name => PipelineStepNameConvention.FromType(this.GetType());

    ValueTask<PipelineControl> IPipelineStep.ExecuteAsync(
        PipelineContextBase context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(this.Execute((TContext)context, result, options));

    protected abstract PipelineControl Execute(
        TContext context,
        Result result,
        PipelineExecutionOptions options);
}

public abstract class AsyncPipelineStep : AsyncPipelineStep<NullPipelineContext>
{
    protected abstract ValueTask<PipelineControl> ExecuteAsync(
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken);

    protected sealed override ValueTask<PipelineControl> ExecuteAsync(
        NullPipelineContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken) =>
        this.ExecuteAsync(result, options, cancellationToken);
}

public abstract class AsyncPipelineStep<TContext> : IPipelineStep
    where TContext : PipelineContextBase
{
    public virtual string Name => PipelineStepNameConvention.FromType(this.GetType());

    ValueTask<PipelineControl> IPipelineStep.ExecuteAsync(
        PipelineContextBase context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken) =>
        this.ExecuteAsync((TContext)context, result, options, cancellationToken);

    protected abstract ValueTask<PipelineControl> ExecuteAsync(
        TContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken);
}

public enum PipelineControlOutcome
{
    Continue,
    Skip,
    Retry,
    Break,
    Terminate
}

public sealed class PipelineControl
{
    public Result Result { get; }

    public PipelineControlOutcome Outcome { get; }

    public static PipelineControl Continue(Result result);

    public static PipelineControl Skip(Result result, string message = null);

    public static PipelineControl Retry(Result result, string message = null);

    public static PipelineControl Break(Result result, string message = null);

    public static PipelineControl Terminate(Result result, string message = null);
}

public interface IPipelineDefinition
{
    string Name { get; }

    Type ContextType { get; }

    IReadOnlyList<IPipelineStepDefinition> Steps { get; }

    IReadOnlyList<Type> HookTypes { get; }

    IReadOnlyList<Type> BehaviorTypes { get; }
}

public interface IPipelineStepDefinition
{
    string Name { get; }

    PipelineStepSourceKind SourceKind { get; }

    Type StepType { get; } // populated when SourceKind is Type

    PipelineInlineStepDescriptor InlineStep { get; } // populated when SourceKind is Inline

    IPipelineDefinitionCondition Condition { get; }

    IReadOnlyDictionary<string, object> Metadata { get; }
}

public interface IPipelineDefinitionBuilder
{
    IPipelineDefinitionBuilder AddStep<TStep>(
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
        where TStep : class, IPipelineStep;

    IPipelineDefinitionBuilder AddStep(
        Action step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder AddAsyncStep(
        Func<Task> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder AddStep(
        Func<IPipelineInlineStepExecution, PipelineControl> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder AddAsyncStep(
        Func<IPipelineInlineStepExecution, Task<PipelineControl>> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder AddHook<THook>(bool enabled = true)
        where THook : class;

    IPipelineDefinitionBuilder AddBehavior<TBehavior>(bool enabled = true)
        where TBehavior : class;

    IPipelineDefinition Build();
}

public interface IPipelineDefinitionBuilder<TContext>
    where TContext : PipelineContextBase
{
    IPipelineDefinitionBuilder<TContext> AddStep<TStep>(
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
        where TStep : class, IPipelineStep;

    IPipelineDefinitionBuilder<TContext> AddStep(
        Action step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<Task> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder<TContext> AddStep(
        Action<TContext> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<TContext, Task> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder<TContext> AddStep(
        Func<IPipelineInlineStepExecution, PipelineControl> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<IPipelineInlineStepExecution, Task<PipelineControl>> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder<TContext> AddStep(
        Func<IPipelineInlineStepExecution<TContext>, PipelineControl> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<IPipelineInlineStepExecution<TContext>, Task<PipelineControl>> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    IPipelineDefinitionBuilder<TContext> AddHook<THook>(bool enabled = true)
        where THook : class;

    IPipelineDefinitionBuilder<TContext> AddBehavior<TBehavior>(bool enabled = true)
        where TBehavior : class;

    IPipelineDefinition Build();
}

public sealed class PipelineDefinitionBuilder(string name) : IPipelineDefinitionBuilder;

public sealed class PipelineDefinitionBuilder<TContext>(string name) : IPipelineDefinitionBuilder<TContext>
    where TContext : PipelineContextBase;

public enum PipelineStepSourceKind
{
    Type,
    Inline
}

public interface IPipelineServiceResolver
{
    T GetRequiredService<T>();

    object GetRequiredService(Type serviceType);

    IEnumerable<T> GetServices<T>();

    IEnumerable<object> GetServices(Type serviceType);

    object GetService(Type serviceType);
}

public interface IPipelineInlineStepExecution
{
    string Name { get; }

    Result Result { get; }

    PipelineExecutionOptions Options { get; }

    CancellationToken CancellationToken { get; }

    IPipelineServiceResolver Services { get; }

    PipelineControl Continue();

    PipelineControl Continue(Result result);

    PipelineControl Skip(string message = null);

    PipelineControl Skip(Result result, string message = null);

    PipelineControl Retry(string message = null);

    PipelineControl Retry(Result result, string message = null);

    PipelineControl Break(string message = null);

    PipelineControl Break(Result result, string message = null);

    PipelineControl Terminate(string message = null);

    PipelineControl Terminate(Result result, string message = null);
}

public interface IPipelineInlineStepExecution<TContext> : IPipelineInlineStepExecution
    where TContext : PipelineContextBase
{
    TContext Context { get; }
}

public sealed class PipelineInlineStepDescriptor
{
    public Type ContextType { get; }

    public bool IsAsync { get; }

    public Delegate Handler { get; }
}

public interface IPipelineStepDefinitionBuilder
{
    IPipelineStepDefinitionBuilder When(IPipelineDefinitionCondition condition);

    IPipelineStepDefinitionBuilder WithMetadata(string key, object value);
}

public interface IPipelineDefinitionCondition
{
    bool IsSatisfied(PipelineDefinitionContext context);
}

public sealed class PipelineExecutionOptions
{
    public IProgress<ProgressReport> Progress { get; set; }

    public Func<PipelineCompletion, ValueTask> CompletionCallback { get; set; }

    public bool ContinueOnFailure { get; set; }

    public bool AccumulateDiagnosticsOnFailure { get; set; } = true;

    public bool AccumulateDiagnosticsOnBreak { get; set; } = true;

    public int MaxRetryAttemptsPerStep { get; set; } = 3;
}

public interface IPipelineExecutionOptionsBuilder
{
    IPipelineExecutionOptionsBuilder WithProgress(IProgress<ProgressReport> progress);

    IPipelineExecutionOptionsBuilder WhenCompleted(Func<PipelineCompletion, ValueTask> callback);

    IPipelineExecutionOptionsBuilder ContinueOnFailure(bool value = true);

    IPipelineExecutionOptionsBuilder AccumulateDiagnosticsOnFailure(bool value = true);

    IPipelineExecutionOptionsBuilder AccumulateDiagnosticsOnBreak(bool value = true);

    IPipelineExecutionOptionsBuilder MaxRetryAttemptsPerStep(int value);

    PipelineExecutionOptions Build();
}

public interface IPipeline
{
    string Name { get; }

    Type ContextType { get; }

    Task<Result> ExecuteAsync(
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result> ExecuteAsync(
        PipelineContextBase context,
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result> ExecuteAsync(
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);

    Task<Result> ExecuteAsync(
        PipelineContextBase context,
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);

    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        PipelineContextBase context,
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);

    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        PipelineContextBase context,
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);
}

public interface IPipeline<TContext> : IPipeline
    where TContext : PipelineContextBase
{
    Task<Result> ExecuteAsync(
        TContext context,
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    Task<Result> ExecuteAsync(
        TContext context,
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);

    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        TContext context,
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        TContext context,
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);
}

public interface IPipelineFactory
{
    IPipeline Create(string name);

    IPipeline Create<TPipelineDefinition>()
        where TPipelineDefinition : class, IPipelineDefinitionSource;

    IPipeline<TContext> Create<TContext>(string name)
        where TContext : PipelineContextBase;

    IPipeline<TContext> Create<TPipelineDefinition, TContext>()
        where TPipelineDefinition : class, IPipelineDefinitionSource<TContext>
        where TContext : PipelineContextBase;
}

public interface IPipelineExecutionTracker
{
    Task<PipelineExecutionSnapshot> GetAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);
}

public sealed class PipelineExecutionHandle
{
    public Guid ExecutionId { get; }
}

public enum PipelineExecutionStatus
{
    Accepted,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed class PipelineCompletion
{
    public Guid ExecutionId { get; }

    public PipelineExecutionStatus Status { get; }

    public Result Result { get; }
}

public sealed class PipelineExecutionSnapshot
{
    public Guid ExecutionId { get; }

    public string PipelineName { get; }

    public PipelineExecutionStatus Status { get; }

    public string CurrentStepName { get; }

    public DateTimeOffset StartedUtc { get; }

    public DateTimeOffset? CompletedUtc { get; }

    public Result Result { get; }
}

public interface IPipelineDefinitionSource
{
    string Name { get; }

    IPipelineDefinition Build();
}

public interface IPipelineDefinitionSource<TContext> : IPipelineDefinitionSource
    where TContext : PipelineContextBase;

public abstract class PipelineDefinition<TContext> : IPipelineDefinitionSource<TContext>
    where TContext : PipelineContextBase
{
    public virtual string Name => PipelineNameConvention.FromType(this.GetType());

    public IPipelineDefinition Build()
    {
        var builder = new PipelineDefinitionBuilder<TContext>(this.Name);

        this.Configure(builder);

        return builder.Build();
    }

    protected abstract void Configure(IPipelineDefinitionBuilder<TContext> builder);
}

public interface IPipelineRegistrationBuilder
{
    IPipelineRegistrationBuilder WithPipeline<TPipelineDefinition>()
        where TPipelineDefinition : class, IPipelineDefinitionSource;

    IPipelineRegistrationBuilder WithPipeline<TContext>(
        string name,
        Action<IPipelineDefinitionBuilder<TContext>> configure)
        where TContext : PipelineContextBase;

    IPipelineRegistrationBuilder WithPipelinesFromAssembly<TMarker>();

    IPipelineRegistrationBuilder WithPipelinesFromAssemblies(
        params Assembly[] assemblies);
}

public static class PipelineServiceCollectionExtensions
{
    public static IPipelineRegistrationBuilder AddPipelines(
        this IServiceCollection services);
}
```

This first cut intentionally keeps some supporting types lightweight, but the runtime contracts are now explicit:

* `PipelineContextBase` stays clean for client usage by exposing a dedicated `Pipeline` child object that contains shared execution metadata, simple engine-managed metrics, and a reusable `PropertyBag`
* `NullPipelineContext` is the no-context implementation used to normalize no-context pipelines onto the same execution path
* `IPipelineHook<TContext>` defines the observable execution events, while `PipelineHook<TContext>` offers no-op defaults so hooks can override only the moments they care about
* `IPipelineBehavior<TContext>` defines the around-execution wrapper contract for full pipeline behavior composition
* `PipelineControl` represents the full step-control contract: the updated accumulated `Result` together with the step control outcome
* `IPipelineInlineStepExecution` / `IPipelineInlineStepExecution<TContext>` define the full-parity execution object exposed to advanced inline step delegates
* `IPipelineServiceResolver` keeps inline step DI access lightweight while still sharing the active pipeline execution scope, including support for resolving single services and multi-registration collections
* `PipelineInlineStepDescriptor` represents an immutable delegate-backed step descriptor for inline step registrations
* `PipelineDefinitionContext` represents configuration or environment data used for structural step inclusion
* `PipelineExecutionHandle` represents the acknowledgement returned when background execution is accepted, without carrying the final `Result` directly
* `PipelineCompletion` is the callback payload for `WhenCompleted(...)`
* `PipelineExecutionSnapshot` represents the tracked state for a previously started execution
* `IPipelineDefinitionSource<TContext>` / `PipelineDefinition<TContext>` provide an optional strongly typed higher-level packaging model for keeping a named pipeline definition, its nested steps, and its configuration together in one class
* `IPipelineRegistrationBuilder` represents the application setup API for registering packaged or inline pipeline definitions, whether they are added one by one or discovered from one or more assemblies

The important implementation direction is that a pipeline step definition may describe either a DI-backed step type or an inline delegate-backed step descriptor, while the static definition remains a pure immutable descriptor model that is safe to cache and reuse.

For execution, the important direction is that awaited execution and background execution are expressed as separate explicit methods rather than as an implicit mode switch hidden inside execution options.

The important usability direction is that the framework should keep author friction low by combining explicit structure with sane defaults. Conventions such as default pipeline names, default step names, no-op hook base methods, and fluent builders should reduce ceremony for the common case while still allowing explicit overrides where needed.

The important context direction is:

* the engine uses one non-generic context-based execution path internally
* context-aware pipelines still declare their expected context through `ContextType` internally
* context-aware definitions set their context explicitly through `PipelineDefinitionBuilder<TContext>`, while no-context definitions default internally to `NullPipelineContext`
* no-context pipelines therefore default internally to `NullPipelineContext`
* callers still use no-context execution overloads and do not need to know about `NullPipelineContext`
* `PipelineContextBase` should expose a `Pipeline` child object that holds common execution metadata such as pipeline name, execution identity, correlation identity, UTC start/completion timestamps, current step name, and simple execution counters
* engine-managed lifecycle values on `context.Pipeline` should be populated by the pipeline engine as execution progresses rather than by individual steps
* `context.Pipeline.TotalStepCount` should be initialized by the engine from the resolved definition before step processing begins
* specialized derived contexts should add pipeline-specific data at the top level, while the `Pipeline` child object remains focused on cross-pipeline execution concerns
* `context.Pipeline.Items` should use the existing `PropertyBag` abstraction as the fallback for execution-scoped data that is not worth modeling strongly, but strongly typed members on derived contexts should remain the default
* execution options remain focused on execution behavior, not on carrying the core execution data

The important authoring direction is:

* infrastructure-facing contracts are non-generic
* client-facing entry points may still use generic facades where that improves clarity and explicitness
* client-facing definition authoring should support both direct builder usage with `PipelineDefinitionBuilder<TContext>` and packaged definition usage with `PipelineDefinition<TContext>`
* `IPipelineDefinitionBuilder<TContext>` should expose real typed fluent methods so context-aware builder usage stays explicit all the way through definition authoring
* the same inline step authoring surface should be available everywhere the definition builder is exposed: direct builder usage, packaged `PipelineDefinition<TContext>.Configure(...)`, and inline application registration via `services.AddPipelines().WithPipeline(..., builder => ...)`
* packaged definitions should also stay strongly typed through `IPipelineDefinitionSource<TContext>` so the definition itself carries its declared context type
* packaged pipeline definitions should default their `Name` from the class name with a trailing `Pipeline` removed and converted to kebab-case, so `OrderImportPipeline` becomes `order-import`, while still allowing explicit override when needed
* `PipelineDefinition<TContext>` should remain a definition source only; the executable runtime contract remains `IPipeline<TContext>` resolved from the factory
* direct builder usage should require the pipeline name in the builder constructor so named pipelines stay first-class and cannot be forgotten
* step names should belong directly to the step type through `IPipelineStep.Name` rather than optional `WithName(...)` calls inside fluent registration
* the default step name should come from the class name with a trailing `Step` removed and converted to kebab-case, so `PersistOrdersStep` becomes `persist-orders`, while still allowing explicit override when a different stable step identity is needed
* inline steps should also have stable names for logging, diagnostics, and tracking; if no explicit name is provided, the builder should generate names such as `inline-step-1`, `inline-step-2`, and so on within the built pipeline definition
* the step builder callback remains useful for optional structural concerns such as conditions and metadata, but not for mandatory step identity
* `AddStep(...)`, `AddAsyncStep(...)`, `AddHook(...)`, and `AddBehavior(...)` should all support a simple optional `enabled` boolean for low-friction conditional registration when richer structural conditions are not needed
* when `enabled` is `false`, the step, hook, or behavior should not be added to the registered pipeline definition at all and therefore should not participate in execution later
* typed sync step authoring remains available through `PipelineStep<TContext>`
* typed async step authoring remains available through `AsyncPipelineStep<TContext>`
* no-context sync step authoring remains explicit through `PipelineStep`, which internally maps to `PipelineStep<NullPipelineContext>`
* no-context async step authoring remains explicit through `AsyncPipelineStep`, which internally maps to `AsyncPipelineStep<NullPipelineContext>`
* inline step authoring should support both low-friction shorthand delegates and advanced execution-object delegates without creating a second runtime model
* shorthand delegates such as `Action`, `Func<Task>`, `Action<TContext>`, and `Func<TContext, Task>` should adapt into the same internal inline step model and automatically return `PipelineControl.Continue(...)` with the unchanged carried `Result`
* advanced inline delegates should receive an execution object that exposes the carried `Result`, execution options, cancellation token, typed context when available, and lightweight scoped service resolution for both single services and multi-service collections
* generic factory calls such as `Create<TContext>(...)` should validate that `TContext` matches the registered pipeline definition `ContextType`
* `Create<TPipelineDefinition>()` should support ergonomic lookup by packaged definition type for packaged pipelines that execute without a shared context
* `Create<TPipelineDefinition, TContext>()` should remain available when the caller wants both packaged definition lookup and an explicitly typed runtime pipeline contract
* the engine should validate that the provided runtime context matches the definition `ContextType`
* calling a no-context execution overload for a context-aware pipeline should fail fast with a clear configuration/runtime error rather than relying on an invalid cast
* the definition builder should validate that added steps are compatible with the configured pipeline context type by inferring step context from the closed generic `PipelineStep<TContext>` or `AsyncPipelineStep<TContext>` base type for type-backed steps, while inline step descriptors carry their own context type and non-generic step bases imply `NullPipelineContext`
* the definition builder should validate hook and behavior compatibility by inspecting their closed generic `IPipelineHook<TContext>` and `IPipelineBehavior<TContext>` interfaces
* hook and behavior compatibility should use context assignability rather than exact type equality so reusable cross-cutting components such as `PipelineHook<PipelineContextBase>` or `IPipelineBehavior<PipelineContextBase>` can participate in pipelines with more specific derived contexts
* if a step, hook, or behavior does not expose a determinable supported context type through those patterns, validation should fail explicitly rather than deferring to runtime casts
* step-context mismatches should surface as explicit pipeline validation errors rather than raw `InvalidCastException` behavior

The important validation direction is:

* pipeline definitions should be validated before execution
* validation should include duplicate step names, incompatible step/context combinations, missing required context configuration, unresolved DI step registrations, and invalid inline step descriptors
* validation should also include duplicate pipeline names across all registered definitions
* validation failures should surface as explicit pipeline configuration/validation errors

The important sync/async direction is:

* the engine should use one internal execution contract based on `ValueTask<PipelineControl>`
* sync and async steps should be freely mixable within the same pipeline definition
* sync steps should not be wrapped in `Task.Run` just to fit the pipeline model
* the engine should simply await every step through the unified `ValueTask` contract
* sync step bases exist for ergonomics and lower overhead, not to create a second engine path

The important result direction is:

* the pipeline starts each execution with an accumulated untyped `Result`
* because `Result` is immutable, each step receives the current accumulated `Result` and returns the updated accumulated `Result` inside `PipelineControl`
* step code can keep reassigning the same local `result` variable, even though each assignment produces a new immutable `Result`
* the final awaited result returned by the pipeline is the last carried `Result`
* success or failure is expressed through the carried `Result`, while `PipelineControl.Outcome` expresses control semantics such as continue, skip, retry, break, or terminate
* `PipelineControl` should be understood as one integral control object, not as a primary outcome plus secondary metadata

The important options direction is:

* callers may still provide a ready-made `PipelineExecutionOptions` instance
* callers should also be able to configure execution options inline through a fluent builder callback
* the fluent builder is a convenience for execution-time ergonomics and should produce the same final options model
* `CompletionCallback` is especially useful for `ExecuteAndForgetAsync`, where no final `Result` is awaited directly
* `WhenCompleted(...)` on the execution options builder should populate that same `CompletionCallback` with a `PipelineCompletion` payload

The important registration direction is:

* application setup should support packaged registration through `services.AddPipelines().WithPipeline<TPipelineDefinition>()`
* application setup should also support inline registration through `services.AddPipelines().WithPipeline<TContext>(name, builder => ...)`
* application setup should also support packaged bulk registration through `services.AddPipelines().WithPipelinesFromAssembly<TMarker>()` and `services.AddPipelines().WithPipelinesFromAssemblies(params Assembly[] assemblies)`
* both registration styles should produce the same underlying immutable `IPipelineDefinition` model
* `services.AddPipelines()` should be additive and safe to call multiple times in the same host so each module can register its own pipelines independently
* repeated `AddPipelines()` calls should contribute to one shared pipeline registration set rather than resetting or replacing earlier registrations
* assembly-based registration should discover concrete packaged pipeline definition types only; it should not auto-register steps, hooks, or behaviors
* registration should reject duplicate pipeline names immediately during setup instead of allowing ambiguous runtime lookup
* duplicate pipeline-name validation should apply across the full combined registration set built from all `AddPipelines()` calls and all assembly scans in the host
* attempting to register a second pipeline with the same logical name should throw an explicit configuration/registration exception
* because pipeline names are mandatory, both inline registration and direct builder usage should require the logical name up front rather than relying on a later `WithName(...)` call

### 10.5 Fictional Example

The following fictional example shows how a pipeline could be defined and then executed with a typed context.

In this example:

* the context is `OrderImportContext`
* that context contains request data and result data
* the pipeline is named `order-import`

```csharp
public sealed class OrderImportContext : PipelineContextBase
{
    public string SourceFileName { get; init; }

    public Guid RequestedByUserId { get; init; }

    public string TenantId { get; init; }

    public int ImportedOrderCount { get; set; }

    public bool IsValidated { get; set; }

    public bool IsPersisted { get; set; }

    public List<string> Warnings { get; } = [];
}
```

Steps may be authored either as DI-managed types deriving from the sync or async typed base classes, or inline delegates attached directly to the pipeline definition builder. Both paths still participate in the same single internal engine contract:

```csharp
public sealed class ValidateOrderImportStep : PipelineStep<OrderImportContext>
{
    protected override PipelineControl Execute(
        OrderImportContext context,
        Result result,
        PipelineExecutionOptions options)
    {
        if (string.IsNullOrWhiteSpace(context.SourceFileName))
        {
            context.Warnings.Add("Source file name is missing.");
            result = result
                .WithMessage("Validation failed.")
                .WithError(new ValidationError("A source file name is required for import."));

            // The carried Result is now failed; runtime policy decides whether execution stops here.
            return PipelineControl.Continue(result);
        }

        options.Progress?.Report(new ProgressReport(
            "order-import",
            ["Validating import request"],
            10));

        context.IsValidated = true;
        result = result.WithMessage("Validation succeeded.");

        return PipelineControl.Continue(result);
    }
}

public sealed class PersistOrdersStep(IOrderRepository repository)
    : AsyncPipelineStep<OrderImportContext>
{
    protected override async ValueTask<PipelineControl> ExecuteAsync(
        OrderImportContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken)
    {
        await repository.SaveImportedOrdersAsync(context.TenantId, cancellationToken);

        context.IsPersisted = true;
        result = result.WithMessage($"Persisted {context.ImportedOrderCount} orders.");

        options.Progress?.Report(new ProgressReport(
            "order-import",
            [$"Persisted {context.ImportedOrderCount} orders"],
            90));

        return PipelineControl.Continue(result);
    }
}
```

The static definition is built as a named blueprint using step descriptors rather than live step instances. Those descriptors may point either to DI-backed step types or to inline delegates:

```csharp
var loadEnabled = true;

var definition =
    new PipelineDefinitionBuilder<OrderImportContext>("order-import")
        .AddStep<ValidateOrderImportStep>()
        .AddStep(
            () => Console.WriteLine("Preparing order import"),
            name: "prepare-inline")
        .AddAsyncStep(
            async () =>
            {
                await Task.Delay(10);
            },
            name: "warm-up-inline")
        .AddStep(context =>
        {
            context.Warnings.Add("Inline context step executed.");
        })
        .AddAsyncStep(
            async execution =>
            {
                var repository = execution.Services.GetRequiredService<IOrderImportRepository>();
                var orders = await repository.LoadAsync(
                    execution.Context.SourceFileName,
                    execution.CancellationToken);

                execution.Context.ImportedOrderCount = orders.Count;

                var result = execution.Result
                    .WithMessage($"Loaded {orders.Count} orders inline.");

                if (orders.Count == 0)
                {
                    return execution.Break(
                        result.WithMessage("No orders were found for import."),
                        "Nothing to import.");
                }

                execution.Options.Progress?.Report(new ProgressReport(
                    "order-import",
                    [$"Loaded {orders.Count} orders inline"],
                    50));

                return execution.Continue(result);
            },
            name: "load-inline",
            enabled: loadEnabled)
        .AddStep<PersistOrdersStep>()
        .AddHook<PipelineAuditHook>()
        .AddBehavior<PipelineTracingBehavior>()
        .AddBehavior<PipelineTimingBehavior>()
        .Build();
```

The same definition can also be packaged into a dedicated class so the pipeline name, related steps, and structural configuration stay grouped together:

```csharp
public sealed class OrderImportPipeline : PipelineDefinition<OrderImportContext>
{
    protected override void Configure(IPipelineDefinitionBuilder<OrderImportContext> builder)
    {
        var loadEnabled = true;

        builder
            .AddStep<ValidateOrderImportStep>()
            .AddStep(
                () => Console.WriteLine("Packaged pipeline setup inline step"),
                name: "packaged-prepare")
            .AddAsyncStep(
                async () =>
                {
                    await Task.Delay(10);
                },
                name: "packaged-warm-up")
            .AddStep(context =>
            {
                context.Warnings.Add("Packaged inline context step executed.");
            })
            .AddAsyncStep(
                async execution =>
                {
                    var repository = execution.Services.GetRequiredService<IOrderImportRepository>();
                    var orders = await repository.LoadAsync(
                        execution.Context.SourceFileName,
                        execution.CancellationToken);

                    execution.Context.ImportedOrderCount = orders.Count;

                    var result = execution.Result.WithMessage(
                        $"Loaded {orders.Count} orders from packaged inline step.");

                    return execution.Continue(result);
                },
                name: "packaged-load-inline",
                enabled: loadEnabled)
            .AddStep<PersistOrdersStep>()
            .AddHook<PipelineAuditHook>()
            .AddBehavior<PipelineTracingBehavior>()
            .AddBehavior<PipelineTimingBehavior>();
    }

    public sealed class ValidateOrderImportStep : PipelineStep<OrderImportContext>
    {
        protected override PipelineControl Execute(
            OrderImportContext context,
            Result result,
            PipelineExecutionOptions options)
        {
            result = result.WithMessage("Validation from packaged pipeline definition.");
            return PipelineControl.Continue(result);
        }
    }

    public sealed class PersistOrdersStep(IOrderRepository repository)
        : AsyncPipelineStep<OrderImportContext>
    {
        protected override async ValueTask<PipelineControl> ExecuteAsync(
            OrderImportContext context,
            Result result,
            PipelineExecutionOptions options,
            CancellationToken cancellationToken)
        {
            await repository.SaveImportedOrdersAsync(context.TenantId, cancellationToken);
            context.IsPersisted = true;

            return PipelineControl.Continue(result.WithMessage("Persisted imported orders."));
        }
    }
}
```

Application setup should support both registration styles:

```csharp
services.AddPipelines()
    .WithPipeline<OrderImportPipeline>();

services.AddPipelines()
    .WithPipeline<OrderImportContext>("order-import-inline", builder => builder
        .AddStep<ValidateOrderImportStep>()
        .AddStep(
            () => Console.WriteLine("Registration-time inline step"),
            name: "registration-prepare")
        .AddAsyncStep(
            async () =>
            {
                await Task.Delay(10);
            },
            name: "registration-warm-up")
        .AddStep(context =>
        {
            context.Warnings.Add("Inline registration context step executed.");
        })
        .AddAsyncStep(
            async execution =>
            {
                var repository = execution.Services.GetRequiredService<IOrderImportRepository>();
                var orders = await repository.LoadAsync(
                    execution.Context.SourceFileName,
                    execution.CancellationToken);

                execution.Context.ImportedOrderCount = orders.Count;

                return execution.Continue(
                    execution.Result.WithMessage($"Loaded {orders.Count} orders from registration inline step."));
            },
            name: "registration-load-inline",
            enabled: true)
        .AddStep<PersistOrdersStep>()
        .AddHook<PipelineAuditHook>()
        .AddBehavior<PipelineTracingBehavior>()
        .AddBehavior<PipelineTimingBehavior>());

services.AddPipelines()
    .WithPipelinesFromAssembly<OrderImportPipeline>();

services.AddPipelines()
    .WithPipelinesFromAssemblies(
        typeof(OrderImportPipeline).Assembly,
        typeof(FileCleanupPipeline).Assembly);
```

This additive registration model is especially important for modular hosts, where each module should be able to register its own pipelines without coordinating one large central pipeline-registration block:

```csharp
public static class OrdersModuleServiceCollectionExtensions
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddPipelines()
            .WithPipelinesFromAssembly<OrderImportPipeline>();

        return services;
    }
}

public static class MaintenanceModuleServiceCollectionExtensions
{
    public static IServiceCollection AddMaintenanceModule(this IServiceCollection services)
    {
        services.AddPipelines()
            .WithPipeline<FileCleanupPipeline>();

        return services;
    }
}
```

At runtime, a context-aware executable pipeline should be resolved either by name plus context type or by packaged definition type plus explicit context type. The one-generic packaged-definition overload is best reserved for no-context pipelines where no typed context contract is needed:

```csharp
var typedPipeline = pipelineFactory.Create<OrderImportPipeline, OrderImportContext>();
var namedPipeline = pipelineFactory.Create<OrderImportContext>("order-import");

var context = new OrderImportContext
{
    SourceFileName = "orders-2026-03.csv",
    RequestedByUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
    TenantId = "tenant-acme",
    Pipeline =
    {
        CorrelationId = Guid.NewGuid().ToString("N")
    }
};

var result = await typedPipeline.ExecuteAsync(
    context,
    o => o
        .WithProgress(new Progress<ProgressReport>(report =>
            Console.WriteLine($"{report.Operation}: {report.PercentageComplete}% - {string.Join(", ", report.Messages)}")))
        .ContinueOnFailure(false)
        .AccumulateDiagnosticsOnFailure(),
    cancellationToken);
```

For packaged pipelines without shared context, the definition-type overload can stay ergonomic:

```csharp
var typedPipeline = pipelineFactory.Create<FileCleanupPipeline>(); // no context, so no generic context type needed

var cleanupResult = await typedPipeline.ExecuteAsync(
    o => o.AccumulateDiagnosticsOnFailure(),
    cancellationToken);
```

The same pipeline can also be started explicitly in the background:

```csharp
var handle = await pipeline.ExecuteAndForgetAsync(
    context,
    o => o
        .WithProgress(new Progress<ProgressReport>(report =>
            Console.WriteLine($"{report.Operation}: {report.PercentageComplete}% - {string.Join(", ", report.Messages)}")))
        .WhenCompleted(completion =>
        {
            Console.WriteLine(
                $"Background pipeline completed (executionId={completion.ExecutionId}, success={completion.Result.IsSuccess}).");
            return ValueTask.CompletedTask;
        })
        .ContinueOnFailure(false)
        .AccumulateDiagnosticsOnFailure(),
    cancellationToken);

var snapshot = await executionTracker.GetAsync(handle.ExecutionId, cancellationToken);
```

This example illustrates the intended split of responsibilities:

* step classes contain focused processing logic and may report progress
* inline steps provide a low-friction authoring alternative when creating a dedicated class would add more ceremony than value
* a single pipeline may freely mix synchronous `PipelineStep...` and asynchronous `AsyncPipelineStep...` implementations
* the same inline step API is available everywhere a pipeline definition builder is exposed, including direct builder usage, packaged pipeline definitions, and `AddPipelines().WithPipeline(..., builder => ...)`
* `PipelineTracingBehavior` is the optional switch that enables OpenTelemetry-friendly pipeline activities with nested step activities
* direct builder usage remains available when a definition should be created inline
* `PipelineDefinition<TContext>` offers a higher-level packaging model when a pipeline should be kept together with its related step types
* packaged definitions are also strongly typed through `IPipelineDefinitionSource<TContext>`
* the definition builder captures a reusable static blueprint with an explicit generic context at the client-facing API
* the factory resolves an executable pipeline at runtime, with one-generic definition lookup mainly useful for no-context packaged pipelines
* the caller provides a single mutable context when the pipeline is context-aware
* that context may carry request data, result data, and any other execution state
* one accumulated `Result` is carried forward through all steps, with each step reassigning and returning the updated immutable result
* a step may return `Continue` with a failed `Result`, leaving `ContinueOnFailure` policy to decide whether later steps still run
* the engine internally normalizes no-context execution to `NullPipelineContext` so the execution path stays unified
* advanced inline steps receive an execution object that exposes the current `Result`, lightweight service resolution, execution options, and the cancellation token
* execution options carry the per-run behavior settings and may be configured inline through a builder
* awaited and background execution remain explicit caller choices

A pipeline that does not need shared execution state still uses the same `IPipeline` interface, and its definition builder can use either class-based or inline no-context steps. The caller can omit context entirely and the engine will use its internal `NullPipelineContext`:

```csharp
public sealed class CleanupTempFilesStep(IFileCleanupService cleanupService)
    : PipelineStep
{
    protected override PipelineControl Execute(
        Result result,
        PipelineExecutionOptions options)
    {
        var deletedFiles = cleanupService.DeleteExpired();

        result = result.WithMessage($"Deleted {deletedFiles} expired temp files.");

        return PipelineControl.Continue(result);
    }
}

var cleanupDefinition =
    new PipelineDefinitionBuilder("file-cleanup")
        .AddStep(() => Console.WriteLine("Preparing file cleanup"))
        .AddAsyncStep(async execution =>
        {
            var cleanupService = execution.Services.GetRequiredService<IFileCleanupService>();
            var deletedFiles = cleanupService.DeleteExpired();

            var result = execution.Result.WithMessage(
                $"Deleted {deletedFiles} expired temp files from inline cleanup step.");

            return execution.Continue(result);
        })
        .AddStep<CleanupTempFilesStep>()
        .Build();

var cleanupPipeline = pipelineFactory.Create("file-cleanup");

var cleanupResult = await cleanupPipeline.ExecuteAsync(
    o => o.WithProgress(new Progress<ProgressReport>(report => Console.WriteLine(report.Messages[0]))),
    cancellationToken);
```

---

## 11. Named Pipelines

Named pipelines are a first-class concept in the design.

A named pipeline is a pipeline definition identified by a logical name.

### 11.1 Purpose of naming

Pipeline names allow:

* multiple distinct flows to coexist within the same application or framework
* clear identification of processing behavior
* more meaningful diagnostics and monitoring
* reuse of the generic pipeline mechanism across many scenarios

Pipeline names should therefore be unique within a registration scope. Registering two pipelines with the same logical name should be treated as a configuration error and should throw immediately during application setup rather than allowing ambiguous runtime resolution later.

### 11.2 Conceptual value

Naming turns the pipeline from a generic internal mechanism into a reusable framework-level feature with explicit identity.

The pipeline name should also be part of the shared execution context, when present, and the execution diagnostics.

### 11.3 Variants

Pipeline variants are intentionally not part of the design.

Named pipelines are considered sufficient to represent different processing flows. Variations in behavior should be expressed through:

* distinct named pipelines
* conditional processing within the pipeline
* execution policies

This keeps the conceptual model simple and avoids introducing an additional abstraction layer for variants.

---

## 12. Conditional Processing

Conditional processing is part of the core concept.

### 12.1 Definition-level conditions

A pipeline definition may express that some steps only participate under certain structural conditions.

This allows the blueprint to represent optional participation clearly.

For simple cases, the fluent builder should also support lightweight boolean inclusion flags such as `.AddStep<LoadOrdersStep>(enabled)`, `.AddAsyncStep(async execution => { ... }, enabled: enabled)`, `.AddHook<PipelineAuditHook>(enabled)`, or `.AddBehavior<PipelineTimingBehavior>(enabled)` so authors do not need to create a richer condition object for every basic toggle.

These boolean flags are definition-time or registration-time inclusion decisions. If `enabled` is `false`, the corresponding step, hook, or behavior is omitted from the built pipeline definition and is therefore not present during execution at all.

### 12.2 Runtime conditions

Even when a step is part of the pipeline definition, the step may still decide at execution time whether to:

* execute normally
* skip itself
* retry
* break
* return a failed `Result`
* terminate the remaining pipeline

### 12.3 Two levels of conditionality

The design deliberately supports two distinct levels:

* structural conditionality in the pipeline definition
* runtime conditionality during execution

This allows both clarity of design and flexibility of behavior.

### 12.4 Step Participation Decision Model

Step participation and progression are determined through a two-stage decision process combining **definition-level conditions** and **runtime evaluation**.

#### Stage 1 – Definition-level evaluation

During pipeline definition, structural conditions determine whether a step is considered part of the pipeline structure. These conditions express design intent and may depend on configuration or configuration or environment conditions.

If a step does not satisfy definition-level conditions, it is excluded from the pipeline structure entirely.

#### Stage 2 – Runtime evaluation

Even when a step is structurally present in the pipeline, the runtime still evaluates both whether the step should participate and what the step result means for the remainder of execution.

Runtime evaluation may depend on:

* shared context state when a context is present
* accumulated `Result` state
* policy conditions

After a step runs, runtime evaluation considers both:

* the returned control outcome
* the returned accumulated `Result`, including whether it is now in a failure state

This is important because `Continue` does not imply success. A step may return `Continue` while also returning a failed `Result`.

The combined evaluation may then produce the following effects:

* execute normally
* skip self
* retry the current step
* break the pipeline
* terminate remaining steps
* continue with a failed carried `Result`
* stop because the carried `Result` is failed and execution policy treats that as terminal

#### Combined effect

The interaction between structural and runtime evaluation can be summarized as:

```text
Pipeline Definition
        |
        v
Check Structural Condition
        |
   +----+----+
   |         |
Include   Exclude
   |
   v
Runtime Step Evaluation
   |
   v
Evaluate Control Outcome + Result State
```

This model keeps **pipeline structure deterministic** while still allowing **runtime adaptive behavior**.

---

## 13. Execution Policies

Execution policies define how the pipeline reacts to the combined evaluation of the full processing-step control result.

Processing steps determine *what happened* during execution by returning:

* a control outcome
* an updated accumulated `Result`

Execution policies then determine *how the pipeline responds* to that combination.

In other words, step evaluation is based on `PipelineControl` as a whole. The non-generic `Result` is not incidental metadata attached to step execution; it is an integral part of the step-control decision.

### 13.1 Purpose of execution policies

Execution policies ensure that pipeline behavior remains consistent and predictable across different use cases without requiring individual steps to implement control-flow rules.

### 13.2 Policy-controlled behavior

Execution policies may define behavior such as:

* whether execution continues after a (specific) failure(s) or stops immediately
* whether multiple errors are accumulated or execution fails fast
* whether retry requests are honored and how many attempts are allowed per step
* whether diagnostics are collected on failure or break
* how terminal control outcomes such as break and terminate are reflected in diagnostics and final reporting

### 13.3 Runtime execution options

The design should support runtime execution options for a concrete pipeline execution.

These options are execution-scoped and allow the same named pipeline definition to behave differently when appropriate without changing its structural step definition.

Examples of runtime execution options include:

* whether execution continues after a failure or stops immediately
* whether diagnostics are accumulated when a failure occurs
* whether diagnostics are accumulated when a break occurs
* how many retry attempts are allowed per step before retry exhaustion becomes a failure
* whether a progress reporter is supplied for execution feedback
* whether a completion callback is supplied for background execution completion

These options are part of execution policy, but they are supplied for a specific runtime execution rather than being hard-coded into the pipeline structure itself. The choice between awaited execution and fire-and-forget execution remains an explicit method-level initiation decision rather than an option flag.

### 13.4 Progress reporting

Execution options should support progress reporting for long-running pipelines in a way that is conceptually aligned with the existing `Requester` and `Notifier` patterns.

Conceptually, execution options should allow a progress reporter similar to `SendOptions.Progress`, preferably using the same `IProgress<ProgressReport>` model so progress semantics remain consistent across the framework.

This allows the pipeline engine and individual steps to provide structured feedback such as:

* notable milestones or messages from the step implementation
* percentage completion when meaningful and explicitly reported by the step implementation

Pipeline steps should be able to report progress directly from inside their processing logic. Conceptually, the active progress reporter should therefore be available to the running step through the execution options, execution context, or an equivalent execution-scoped abstraction.

This is important for long-running steps whose internal work cannot be represented adequately by only step-start and step-end notifications from the pipeline engine.

The pipeline engine should not synthesize automatic caller-facing progress percentages or step-by-step progress reports on its own. Progress reporting is owned by step implementations, while the engine’s own progression is covered through internal structured logging.

Progress reporting is intended to provide caller feedback during execution. It complements diagnostics and final results, but it does not replace them.

### 13.5 Effect on execution behavior

Runtime execution options determine how the pipeline interprets a step once it has produced its `PipelineControl`, including both its control outcome and its updated accumulated `Result`.

For example:

* a step may return `Continue` while also returning a failed accumulated `Result`, and policy then decides whether execution stops or later steps still run
* a `Retry` outcome re-executes the current step with the returned carried `Result` and the current context state, subject to the configured retry limit
* a break finalizes the pipeline successfully and always stops later step execution
* diagnostics may either stop at the terminating outcome or continue to be accumulated into the final execution report
* execution may either hold the caller until completion or continue in the background after an explicit fire-and-forget invocation
* progress may be reported continuously to the caller while execution is in progress

This makes the pipeline execution model adaptable while preserving a stable and deterministic definition model.

If a step throws an exception instead of returning `PipelineControl`, the engine should catch that exception, log it, append `new ExceptionError(exception)` to the current accumulated `Result`, and then evaluate continuation based on the resulting failed `Result` and the active failure policy.

If `ContinueOnFailure` is enabled for that execution, later steps run against the context state exactly as it was left by the failing step. The engine should not attempt to roll back or reconstruct context state automatically, so pipelines should only opt into failure continuation when partially updated context is acceptable and later steps are designed to tolerate it.

### 13.6 Separation of concerns

Execution policies separate decision-making responsibilities:

* steps are responsible for producing `PipelineControl`, including both directional outcomes and failed/successful carried `Result` state
* policies are responsible for interpreting those outcomes and determining pipeline progression

This separation prevents processing logic from becoming entangled with control-flow rules and ensures consistent behavior across pipelines.

Fire-and-forget initiation does not change the responsibilities of steps. It changes only how execution is initiated and how the initiating caller relates to completion of the pipeline.

Progress reporting also does not change the responsibilities of steps. It is a feedback mechanism around execution and step advancement, not a substitute for domain outcomes or diagnostics.

---

## 14. Result Handling Concept

The pipeline integrates with the framework’s established result model.

### 14.1 Pipeline-level outcome

A pipeline execution produces a final untyped `Result` representing the complete outcome of the processing flow.

The pipeline should initialize that `Result` at execution start and carry it forward step by step until execution completes.

Because the framework’s `Result` type is immutable, this carried result should progress by replacing the current value with the next `Result` returned from each step rather than by mutating a shared object in place.

When execution is awaited, this final accumulated `Result` can be returned directly to the caller. When execution is started in fire-and-forget mode, the initiating call should instead return an acknowledgement of accepted background execution, ideally including execution identity such as a correlation identifier or execution identifier that can be used for diagnostics and tracking.

If the caller needs the final `Result` of a fire-and-forget execution without awaiting it directly, the design should allow an optional completion callback to receive a `PipelineCompletion` payload when background execution finishes.

That completion callback should run only after the pipeline execution has fully finished and its final accumulated `Result` has been determined.

The callback should be treated as a plain caller-supplied delegate captured at execution start rather than as part of the background pipeline execution itself. It should therefore execute outside the background DI scope that was used for the pipeline run and should not rely on any ambient request or caller scope.

That also means the callback should not assume request-scoped or background-scoped services from the original execution are still available.

If the completion callback throws, that callback failure should be logged, but it must not overwrite, replace, or downgrade the pipeline's final `Result` or tracked execution outcome.

When a progress reporter is supplied, the initiating caller may also receive incremental progress feedback while the pipeline is running.

### 14.2 Step-level contribution

Each step receives the current accumulated `Result` and returns the next accumulated `Result` together with its control outcome.

This allows all steps in the flow to contribute messages, warnings, and errors into one carried result model without inventing a second success/failure abstraction beside the framework’s normal `Result`.

In other words, the pipeline carries one logical execution result through the flow, while each step creates the next immutable `Result` value from the previous one.

That carried `Result` is also part of step control itself because the pipeline evaluates the returned `PipelineControl` as a whole.

No separate result-merging mechanism is required. The framework’s immutable `Result` already retains prior messages and errors when a step creates the next result from the current one.

When a step fails by throwing an exception, the engine should translate that into the carried result by creating the next `Result` from the current one and appending `new ExceptionError(exception)` through the normal result API.

### 14.3 Supported outcome forms

At the conceptual level, result handling should support:

* success
* failure
* warnings or notable non-fatal issues where relevant
* structured error information
* execution diagnostics
* knowledge of early completion or early termination

The final result should also reflect the effect of runtime execution options, including whether accumulated failure state stopped execution immediately or allowed later steps to continue, whether retries were attempted before the final outcome, and whether diagnostics were accumulated beyond break or termination outcomes.

For fire-and-forget execution, the final result remains important, but it is observed through execution tracking, diagnostics, hooks, logs, or other monitoring mechanisms rather than through the immediate initiating call.

In fire-and-forget mode, live progress callbacks may still be supported when a suitable progress reporter is supplied, but the design should not treat such callbacks as the sole source of truth for execution status because the initiating caller may no longer be actively awaiting completion.

### 14.4 Importance of consistency

This unified result approach is important because it makes pipeline behavior consistent with the rest of the framework and improves both developer understanding and operational supportability.

---

## 14. Observability and Diagnostics

The pipeline feature should provide meaningful insight into execution behavior.

### 14.1 Observability objectives

The design should support visibility into:

* which pipeline ran
* whether the execution was awaited or fire-and-forget
* what progress was reported during execution
* which steps were part of the definition
* which steps actually executed
* which steps were skipped
* which step ended the flow, if any
* whether execution completed normally, broke early, terminated, or failed
* whether a fire-and-forget execution was accepted, running, completed, or failed
* what notable execution events occurred

### 14.2 Diagnostic value

Diagnostics are valuable for:

* debugging
* testing
* production support
* operational analysis
* traceability

### 14.3 Engine-internal logging

The pipeline execution engine itself should provide extensive built-in structured logging.

This logging should be part of the core engine behavior rather than something delegated only to hooks, behaviors, or consumer-written steps. Hooks and behaviors may enrich logging, but they should not be the only mechanism by which pipeline flow becomes understandable.

The implementation should follow the existing devkit logging pattern:

* define a feature-local `Constants.cs` with `public const string LogKey = "PLN";`
* use source-generated `TypedLogger` partial methods with `[LoggerMessage(...)]` for the internal pipeline and step logs
* prefer the same high-performance logging style already used across devkit features such as commands, queries, jobs, and identity

The log message shape should follow the pipeline-specific general template:

* `[PLN] message (prop1=abc, prop2=xyz, ...)`
* `[PLN] message finished (prop1=abc, prop2=xyz, ...) -> took 12.23ms`

In implementation terms, the internal message templates should use the `Constants.LogKey` placeholder and produce that standardized shape consistently for both pipeline-level and step-level logs.

Illustrative examples:

```csharp
public struct Constants
{
    public const string LogKey = "PLN";
}

public static partial class TypedLogger
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "[{LogKey}] pipeline executing (pipeline={PipelineName}, executionId={ExecutionId}, mode={Mode})")]
    public static partial void LogPipelineExecuting(
        ILogger logger,
        string logKey,
        string pipelineName,
        string executionId,
        string mode);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "[{LogKey}] pipeline executed (pipeline={PipelineName}, executionId={ExecutionId}, success={Success}) -> took {ElapsedMilliseconds:0.00}ms")]
    public static partial void LogPipelineExecuted(
        ILogger logger,
        string logKey,
        string pipelineName,
        string executionId,
        bool success,
        double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] step executing (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId})")]
    public static partial void LogStepExecuting(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        string executionId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] step executed (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId}, outcome={Outcome}, resultSuccess={ResultSuccess}) -> took {ElapsedMilliseconds:0.00}ms")]
    public static partial void LogStepExecuted(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        string executionId,
        string outcome,
        bool resultSuccess,
        double elapsedMilliseconds);
}
```

At a conceptual level, engine logging should make the following visible:

* pipeline execution started, including pipeline name, execution identity, and initiation mode
* step selected for execution, retried, skipped, broke early, or terminated
* step execution started and step execution completed
* the returned `PipelineControl`
* the returned non-generic `Result`, including success/failure state and notable message/error summaries
* exceptions thrown by steps and their translation into `ExceptionError` on the carried `Result`
* policy decisions taken by the engine after evaluating a step
* pipeline execution completed, including final `Result` state

Engine logging should use the resolved pipeline and step names that come from the pipeline/step naming conventions or explicit overrides. It should not default to CLR type names when a framework-level pipeline or step name is available.

Step-level and pipeline-level internal logs should use the same general template and the same `PLN` log key so they remain easy to scan, correlate, and query consistently across the feature.

This internal logging should make it possible to reconstruct how the pipeline flowed through its steps and why the engine continued, retried, stopped, broke early, or terminated.

### 14.4 Beyond logging

Observability is broader than logging. The pipeline should conceptually support structured execution insight that can later be surfaced through logging, metrics, traces, or other monitoring mechanisms.

For tracing specifically, the pipeline design should support OpenTelemetry-friendly activity tracing through `ActivitySource` and the shared `ActivityHelper.StartActvity(...)` pattern already used in other devkit features.

Tracing should be modeled as an optional behavior rather than as an always-on engine responsibility. When that tracing behavior is registered, it should establish a pipeline-level activity around the whole execution and the engine should create nested step activities inside that active pipeline activity for each executed step.

Tracing should use the resolved pipeline and step names, not CLR type names, so trace spans align with the same canonical names used by logging and diagnostics.

The pipeline-specific tracing model should not depend on module names. Instead, it should use pipeline execution metadata such as pipeline name, execution id, correlation id, and step name.

Progress reporting is part of this observability model, but it is specifically caller-oriented and execution-time oriented rather than only operational or post-execution.

---

## 15. Extensibility Model

The pipeline is intended as a framework feature and must therefore be extensible.

### 15.1 Custom processing steps

Consumers of the framework must be able to define custom steps that participate in the common pipeline model.

### 15.2 Custom contexts

Consumers must be able to define pipeline-specific shared contexts while still benefiting from the common base semantics.

The design should also support pipelines that intentionally have no shared context.

### 15.3 Runtime extensibility

The feature must support runtime-dependent behavior without requiring the conceptual model to change.

### 15.4 Execution extension points

The design should provide extension points around execution so that additional behavior can be attached without modifying the logic of the processing steps themselves.

This keeps the step model focused and preserves separation of concerns.

---

## 16. Hooks and Behaviors

The design should distinguish conceptually between hooks and behaviors.

### 16.1 Hooks

Hooks are extension points that observe or react to execution events.

Examples of conceptual hook moments include:

* before pipeline execution
* after pipeline execution
* before step execution
* after step execution
* on error or failure

Hooks are primarily observational or event-like in nature.

Hooks should run in registration order.

By default, hook failures should be logged and ignored so observational extensions such as auditing do not unexpectedly change pipeline success or failure behavior.

Hooks should also remain reusable across pipelines where possible. A hook declared for `PipelineContextBase` should be considered compatible with any more specific derived pipeline context because it depends only on the common framework-owned execution metadata.

For example, if a consumer wants to audit pipeline execution by logging only pipeline start, completion, and failure, a hook is a good fit because it reacts to lifecycle events without wrapping or changing execution behavior:

```csharp
public sealed class PipelineAuditHook(ILogger<PipelineAuditHook> logger)
    : PipelineHook<OrderImportContext>
{
    public override ValueTask OnPipelineStartingAsync(
        OrderImportContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[PipelineAudit] pipeline started (name={PipelineName}, executionId={ExecutionId}, correlationId={CorrelationId})",
            context.Pipeline.Name,
            context.Pipeline.ExecutionId,
            context.Pipeline.CorrelationId);

        return ValueTask.CompletedTask;
    }

    public override ValueTask OnPipelineCompletedAsync(
        OrderImportContext context,
        Result result,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[PipelineAudit] pipeline completed (name={PipelineName}, executionId={ExecutionId}, success={Success})",
            context.Pipeline.Name,
            context.Pipeline.ExecutionId,
            result.IsSuccess);

        return ValueTask.CompletedTask;
    }

    public override ValueTask OnPipelineFailedAsync(
        OrderImportContext context,
        Result result,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "[PipelineAudit] pipeline failed (name={PipelineName}, executionId={ExecutionId}, errorCount={ErrorCount})",
            context.Pipeline.Name,
            context.Pipeline.ExecutionId,
            result.Errors.Count);

        return ValueTask.CompletedTask;
    }
}
```

### 16.2 Behaviors

Behaviors wrap pipeline execution with additional cross-cutting behavior.

Behaviors are the devkit-facing name for this extension point because they describe the added execution behavior in familiar framework terms.

Internally, behaviors follow the decorator pattern: each behavior receives `next()` and can run logic before, after, or around the inner execution.

Behaviors are intended for concerns such as:

* diagnostics
* logging
* timing
* monitoring
* tracing
* policy enforcement

Engine-internal logging remains a core execution responsibility even when behaviors are present. Behaviors may enrich or redirect logging behavior, but they should not replace the baseline structured logging provided by the pipeline engine itself.

Pipeline behaviors should compose in registration order around the full pipeline execution.

If a behavior throws, the engine should treat that the same way it treats a step exception: log it, translate it into `ExceptionError` on the carried `Result`, and then apply the active execution policy.

Behaviors should follow the same compatibility principle as hooks. A behavior declared for `PipelineContextBase` should be reusable across pipelines with more specific derived contexts when it only relies on the shared execution metadata.

For example, a timing behavior is a good fit when the concern must wrap the full pipeline execution and measure elapsed time around the actual work:

```csharp
public sealed class PipelineTimingBehavior(ILogger<PipelineTimingBehavior> logger)
    : IPipelineBehavior<OrderImportContext>
{
    public async ValueTask<Result> ExecuteAsync(
        OrderImportContext context,
        Func<ValueTask<Result>> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next();

            logger.LogInformation(
                "[PipelineTiming] pipeline completed (name={PipelineName}, executionId={ExecutionId}, durationMs={DurationMs}, success={Success})",
                context.Pipeline.Name,
                context.Pipeline.ExecutionId,
                stopwatch.ElapsedMilliseconds,
                result.IsSuccess);

            return result;
        }
        catch
        {
            logger.LogWarning(
                "[PipelineTiming] pipeline failed (name={PipelineName}, executionId={ExecutionId}, durationMs={DurationMs})",
                context.Pipeline.Name,
                context.Pipeline.ExecutionId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
```

This example shows the intended low-level contract directly: the behavior follows the decorator pattern by wrapping `next()` and therefore participates around execution instead of merely observing events after they happen.

Another useful optional behavior is tracing. A `PipelineTracingBehavior` should apply the existing devkit activity pattern to pipeline execution by creating one outer pipeline activity and letting the engine create nested step activities inside that scope.

Tracing should follow the established `ActivityHelper.StartActvity(...)` helper pattern and use `ActivitySourceExtensions.Find(...)` to resolve an `ActivitySource` by pipeline name, falling back to `default` or the current activity source when necessary. Module-specific concepts are not relevant here; the tracing identity should come from the pipeline itself.

```csharp
public sealed class PipelineTracingBehavior(
    IEnumerable<ActivitySource> activitySources)
    : IPipelineBehavior<PipelineContextBase>
{
    public async ValueTask<Result> ExecuteAsync(
        PipelineContextBase context,
        Func<ValueTask<Result>> next,
        CancellationToken cancellationToken)
    {
        var activitySource = activitySources.Find(context.Pipeline.Name);

        return await activitySource.StartActvity(
            $"PIPELINE Execute {context.Pipeline.Name}",
            async (activity, ct) =>
            {
                activity?.AddEvent(new ActivityEvent(
                    $"executing (pipeline={context.Pipeline.Name}, executionId={context.Pipeline.ExecutionId})"));

                return await next();
            },
            ActivityKind.Internal,
            tags: new Dictionary<string, string>
            {
                ["pipeline.name"] = context.Pipeline.Name,
                ["pipeline.execution_id"] = context.Pipeline.ExecutionId.ToString("N")
            },
            baggages: new Dictionary<string, string>
            {
                [ActivityConstants.CorrelationIdTagKey] = context.Pipeline.CorrelationId,
                ["pipeline.execution_id"] = context.Pipeline.ExecutionId.ToString("N"),
                ["pipeline.name"] = context.Pipeline.Name
            },
            cancellationToken: cancellationToken);
    }
}
```

When `PipelineTracingBehavior` is registered, the engine should create nested step activities under the current pipeline activity for every executed step, for example `PIPELINE STEP persist-orders`, and include tags such as:

* `pipeline.name`
* `pipeline.execution_id`
* `pipeline.step`
* `pipeline.result_success`
* `pipeline.control_outcome`

Exceptions inside the pipeline or a step activity should be recorded through the shared `ActivityHelper` behavior so trace status and exception metadata remain aligned with the logging and result-handling model.

### 16.3 Why the distinction matters

Hooks observe execution events. Behaviors wrap and influence execution behavior.

Keeping these concepts separate improves clarity and avoids mixing passive observation with active behavioral extension.

---

## 17. Architectural Phases

The pipeline feature can be understood as a set of architectural phases or conceptual layers.

### 17.1 Core abstractions phase

This phase defines the foundational vocabulary:

* pipeline
* context
* accumulated result
* processing step
* control outcomes
* final result

It establishes the conceptual language of the feature.

### 17.2 Execution phase

This phase describes how the pipeline behaves at runtime:

* ordered progression through steps
* execution initiation mode selection
* runtime participation decisions
* control outcome handling
* result progression
* execution completion semantics

### 17.3 Composition phase

This phase describes how a pipeline is structurally declared:

* naming
* ordered steps
* structural conditions
* conceptual execution options

### 17.4 Construction phase

This phase describes how an executable pipeline is created from its static definition at runtime.

An important responsibility of this phase is resolving pipeline steps through dependency injection. The static definition provides the ordered blueprint, while the construction phase materializes DI-backed step instances for the current execution environment and service scope.

### 17.5 Extension phase

This phase describes how additional behaviors, diagnostics, and cross-cutting concerns attach around the core execution model.

These phases provide a clean way to reason about the design without mixing core concepts with surrounding concerns.

---

## 18. Terminology

This section defines the key terms used throughout the design. The glossary establishes a consistent vocabulary for discussing the pipeline feature.

| Term                         | Definition                                                                                                                                                |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Pipeline**                 | An ordered sequence of processing steps operating on an optional shared context and a carried accumulated `Result`.                                        |
| **Pipeline Execution**       | A single runtime instance of pipeline processing with its own carried `Result` and, when relevant, its own specific context.                              |
| **Pipeline Definition**      | The static blueprint describing the structure, order, and conceptual behavior of a pipeline.                                                              |
| **Named Pipeline**           | A pipeline definition identified by a logical name that distinguishes it from other pipelines within the system.                                          |
| **Processing Step**          | A focused unit of work within the pipeline that can inspect or update the context, contribute messages and errors to the carried `Result`, and influence control flow. |
| **Shared Context**           | An optional strongly typed execution-scoped object shared by all steps that carries execution metadata and shared state when needed.                      |
| **Accumulated Result**       | The untyped framework `Result` that is initialized at execution start and carried forward across all pipeline steps.                                      |
| **Control Outcome**          | The directional part of step control that indicates how pipeline execution should proceed when interpreted together with the returned `Result`.           |
| **Pipeline Control**         | The full step-control object combining the returned control outcome and the returned accumulated non-generic `Result`.                                    |
| **Retry**                    | Re-execution of the current step when a step requests another attempt under the active retry policy.                                                      |
| **Break**                    | Early successful completion of the pipeline when a step determines that no further processing is required.                                                |
| **Termination**              | Intentional ending of remaining pipeline execution without by itself defining success or failure.                                                         |
| **Failure**                  | A failure state represented through the carried `Result`, typically by accumulated errors and failure status.                                             |
| **Conditional Processing**   | Structural or runtime conditions that determine whether a step participates in pipeline execution.                                                        |
| **Hooks**                    | Execution observation points that react to events occurring during pipeline execution, such as before or after steps.                                     |
| **Behaviors**                | Wrappers around pipeline execution that introduce cross-cutting behavior such as diagnostics, logging, or monitoring. They are implemented using the decorator pattern. |
| **Observability**            | The ability to understand pipeline execution behavior through structured diagnostics, traces, and execution insight.                                      |
| **Pipeline Execution State** | The runtime state of an executing pipeline including current step, accumulated diagnostics, control outcomes, and the current carried `Result`.           |

---

## 19. Architecture Overview

To improve conceptual clarity, the architecture of the pipeline feature can be understood through two complementary perspectives:

* **Logical Architecture** – the structural components that make up the pipeline feature.
* **Execution Flow** – the lifecycle of a pipeline during runtime processing.

---

## 19.1 Logical Architecture

The logical architecture describes the structural building blocks of the pipeline feature.

### Core Processing Model

The core processing model defines the essential concepts of the pipeline system:

* pipelines
* optional shared contexts
* accumulated results
* processing steps
* control outcomes
* final results

This model represents the conceptual heart of the pipeline and is independent of infrastructure or runtime mechanics.

### Execution Engine

The execution engine governs how pipelines behave at runtime. Conceptually it is responsible for:

* progressing through ordered steps
* evaluating step participation
* applying control outcomes
* maintaining execution state
* producing final results and diagnostics

### Composition Model

The composition model describes how pipelines are defined structurally.

It includes:

* named pipeline definitions
* ordered step declarations
* structural conditions for step participation
* conceptual execution policies

The composition model defines **what a pipeline is** without describing how it executes.

### Construction Mechanism

The construction mechanism creates executable pipeline instances from pipeline definitions.

It resolves the blueprint into a runtime pipeline capable of processing with a carried accumulated `Result` and, when applicable, a specific context.

The construction mechanism must account for step dependencies explicitly. Because processing steps are expected to use constructor injection, the mechanism should resolve steps from dependency injection rather than instantiate them directly. This keeps pipeline steps aligned with the framework's normal service model and allows them to depend on repositories, validators, policies, loggers, and other collaborators.

Conceptually, the pipeline definition should therefore retain step descriptors such as step types, registration metadata, or DI-aware factories, while the runtime construction mechanism uses the current service provider or service scope to obtain the executable step instances.

This separation between **definition** and **construction** keeps the design flexible while preserving clarity.

### Extension Model

The extension model allows additional behavior to attach around pipeline execution without modifying the steps themselves.

Examples include:

* hooks observing execution events
* behaviors introducing cross-cutting behavior
* diagnostic integrations
* monitoring or instrumentation

This model preserves separation of concerns and keeps processing steps focused on domain logic.

---

## 19.2 Execution Flow

The execution flow describes the lifecycle of a pipeline during runtime.

### Step 1 – Pipeline Definition

A named pipeline definition describes the structure of the processing flow including its ordered steps and conceptual policies.

### Step 2 – Pipeline Construction

A runtime component constructs an executable pipeline instance based on the pipeline definition and execution environment.

During this step, the runtime component resolves the concrete processing step instances through dependency injection so that constructor-injected dependencies are available for the current execution.

The construction and initialization path should also validate that the runtime context and all configured steps are compatible with the pipeline definition’s declared `ContextType`.

This validation phase should also ensure that step registrations are resolvable, step names are structurally valid, and the pipeline definition is internally coherent before execution proceeds.

The resolved pipeline name and resolved step names should also become the canonical names used by engine logging, execution tracking, diagnostics, and current-step reporting.

If execution is initiated in fire-and-forget mode, the runtime component also establishes the background execution ownership and service scope required to let the pipeline complete independently of the initiating caller.

### Step 3 – Execution Initialization

Pipeline execution begins with:

* an optional shared context when the pipeline requires one
* an initial accumulated `Result`, typically starting as success

Execution metadata such as correlation identifiers and pipeline identity are established in the context when a context is present.

At this point, the engine should also initialize the common `context.Pipeline` lifecycle fields, including `StartedUtc`, `ExecutionId`, `Name`, `TotalStepCount`, and an initial `ExecutedStepCount` of zero.

If the caller omits context, the engine should create its internal `NullPipelineContext`. If the caller supplies a context that does not match the declared `ContextType`, execution should fail fast with a clear pipeline validation/configuration error.

If the execution was started in fire-and-forget mode, the initialization phase should also establish enough execution identity to support later diagnostics, monitoring, and completion tracking.

If a progress reporter is supplied through execution options, the initialization phase should also establish the reporting channel so the engine and steps can emit progress updates consistently throughout execution.

If a completion callback is supplied through execution options, the engine should retain it and invoke it only after the background execution finishes, its final accumulated `Result` has been determined, and the final tracked execution snapshot has been updated.

That callback invocation should happen outside the background DI scope used by the pipeline execution and should be treated as a plain caller-supplied delegate invocation rather than as another pipeline step.

The callback should therefore not depend on request-scoped or background-scoped services from the original execution path still being available, and it should receive a `PipelineCompletion` payload rather than relying on any ambient runtime state.

If the completion callback throws, the engine should log that callback failure but preserve the already-determined final pipeline result and tracked execution state.

### Step 4 – Ordered Step Processing

The pipeline progresses through its ordered processing steps.

Each step may:

* read or update context when one is present
* contribute messages or errors to the accumulated `Result`
* produce diagnostics
* determine a control outcome

As step processing advances, the engine should keep the common `context.Pipeline` metrics up to date, for example by setting `CurrentStepName` before step execution and incrementing `ExecutedStepCount` after each completed step attempt.

### Step 5 – Step Evaluation

After each step, the pipeline evaluates the updated accumulated `Result`, the control outcome, and the active execution policy together.

This evaluation may cause execution to:

* continue to the next step
* skip a step
* retry the current step
* break successfully
* terminate remaining steps
* stop because the accumulated `Result` is now in a failure state

In particular, `Continue` does not by itself mean the step succeeded. It only means the step did not request an alternate control outcome such as skip, retry, break, or terminate.

### Step 6 – Completion

Pipeline execution ends when:

* all steps have executed successfully
* a step breaks the pipeline
* a step terminates remaining execution
* the accumulated `Result` is treated as terminal according to policy

When execution reaches completion, the engine should set `CompletedUtc` on `context.Pipeline` so the final context reflects both the UTC end timestamp and the derived execution `Duration`.

### Step 7 – Final Result Production

The pipeline produces a structured final `Result` that includes:

* success or failure status
* accumulated messages and errors
* accumulated diagnostics
* execution metadata

This result represents the complete outcome of the processing flow and may be interpreted together with the final context when a context-aware pipeline was used.

---

## 19.3 Pipeline Execution State Model

The pipeline maintains an execution state representing the current status of a running pipeline instance.

The execution state conceptually contains the runtime information necessary to understand and control pipeline progression.

### Purpose of execution state

The execution state provides a structured representation of pipeline progress and enables observability, diagnostics, and runtime decision-making.

For implementation, this state should be surfaced through `PipelineExecutionSnapshot` with a stable minimum contract rather than being left entirely open-ended.

### Conceptual contents of execution state

A pipeline execution state typically includes:

* pipeline identity
* correlation identifier
* execution initiation mode
* engine log correlation data
* progress reporting configuration
* latest known progress state
* current processing step
* ordered list of executed steps
* skipped steps
* accumulated diagnostics
* step-control decisions produced so far
* the current carried `Result`
* execution start and completion indicators

At minimum, the tracked snapshot contract should expose:

* `ExecutionId`
* `PipelineName`
* `Status`
* `CurrentStepName`
* `StartedUtc`
* `CompletedUtc`
* final `Result` when execution has completed

### Role during execution

During pipeline execution, the execution state evolves as steps participate in the processing flow.

Each step may read the current state and contribute updates such as diagnostics, step-control decisions, or the next accumulated `Result`.

The tracker should move through clearly defined statuses such as `Accepted`, `Running`, `Completed`, `Failed`, and `Cancelled`.

### Role for observability

The execution state also provides a structured representation that can be used to:

* produce execution reports
* generate diagnostics
* support debugging and support analysis
* enable monitoring and tracing systems
* support lookup of fire-and-forget execution status and final outcome

By modeling execution state explicitly, the pipeline feature maintains a clear representation of processing progress and decisions made during execution.

---

## 20. Testability Considerations

Testability is a design goal and a natural consequence of the conceptual structure.

### 20.1 Step-level testing

Each processing step should be testable in isolation with controlled context when applicable, a known incoming `Result`, and clear assertions against the returned `PipelineControl`.

### 20.2 Pipeline-level testing

Whole pipeline definitions should be testable as ordered flows so that step participation and control-flow behavior can be verified.

### 20.3 Diagnostic testing

Execution diagnostics should be testable so that runtime decisions and execution paths can be asserted.

The structured nature of the pipeline makes this significantly more tractable than monolithic service-method processing.

---

## 21. Applicability

The pipeline is intentionally generic and suitable for a broad range of in-process processing scenarios.

Typical examples include:

* request preparation and validation
* data normalization and enrichment
* staged business evaluation
* import processing flows
* application-level multi-step processing routines
* transformation or preparation flows that benefit from ordered structure and shared execution state

The pipeline is appropriate wherever processing benefits from:

* clear step ordering
* separation of concerns
* a shared strongly typed context
* explicit continuation semantics
* consistent result reporting

---

## 22. Design Principles

The following principles guide the design.

### 22.1 Clarity over cleverness

The pipeline should be easy to reason about and easy to explain.

### 22.2 Strong typing by default

The model should prefer explicit, typed concepts over loosely structured bags of state.

### 22.3 Separation of concerns

Processing logic, execution behavior, and cross-cutting concerns should remain conceptually distinct.

### 22.4 Runtime flexibility within a stable structure

The pipeline should support dynamic runtime behavior without sacrificing clarity of definition.

### 22.5 Consistent outcomes

Success, failure, warnings, and early completion behavior should be expressed consistently.

### 22.6 Lightweight scope

The feature should remain intentionally smaller and simpler than workflow and orchestration systems.

### 22.7 Reuse through explicit identity

Named pipelines and a shared conceptual model should make the feature broadly reusable across the framework.

### 22.8 Low-friction defaults

The framework should prefer sensible defaults and conventions for common cases so developers can adopt pipelines with minimal ceremony, while still being able to override those defaults when a more explicit or specialized setup is needed.

---

## 23. Summary

The generic processing pipeline is a reusable framework feature for expressing ordered processing flows as a sequence of focused steps working on:

* an optional strongly typed shared context
* an accumulated carried `Result`

Its defining characteristics are:

* ordered step-based execution
* strongly typed pipeline-specific context with a common base
* explicit control semantics
* named pipeline support
* static structural definition with runtime construction
* runtime execution options for continuation and diagnostic accumulation behavior
* explicit fire-and-forget execution initiation for background processing
* progress reporting through execution options for long-running pipelines
* integration with the framework’s result model
* support for diagnostics and observability
* extension points for hooks, behaviors, and other cross-cutting concerns
* deliberate boundaries that keep it lightweight and distinct from workflow engines

This makes it a strong foundational feature for a devkit framework, enabling structured, reusable, and maintainable processing logic across multiple application areas.

---

## 24. References

https://www.hojjatk.com/2012/11/chain-of-responsibility-pipeline-design.html

https://www.dofactory.com/net/chain-of-responsibility-design-pattern

https://medium.com/@bonnotguillaume/software-architecture-the-pipeline-design-pattern-from-zero-to-hero-b5c43d8a4e60

https://github.com/guillaumebonnot/software-architecture/tree/master/Helios.Architecture.Pipeline

https://www.devleader.ca/2026/03/14/decorator-design-pattern-in-c-complete-guide-with-examples

---

## Appendix A. Pipeline Code Generation Design

This appendix captures a possible future enhancement for the pipeline feature. It is intentionally deferred until after the main manual pipeline implementation and its testing are complete.

The goal is to reduce declaration boilerplate for packaged pipelines while preserving the current runtime model, registration shape, execution semantics, and debugging clarity.

### A.1 Purpose

Pipeline code generation is intended to:

* reduce repetitive plumbing for packaged pipeline definitions
* improve developer ergonomics for common pipeline declaration scenarios
* preserve the existing explicit runtime concepts rather than replacing them with a second hidden model

This appendix does not change the main implementation scope. It documents a future direction that should reuse the same runtime engine, the same `PipelineControl`, the same carried immutable `Result`, the same hook/behavior model, and the same registration and factory APIs already described in the main design.

### A.2 Design Goals

The code generation design should optimize for:

* less boilerplate for pipeline authors
* clear generated code and predictable conventions
* compile-time diagnostics for invalid pipeline authoring
* full compatibility with the manual packaged pipeline model

The design should remain intentionally conservative. It should help authors write less plumbing, but it should not make pipeline behavior magical or opaque.

### A.3 Authoring Model

The recommended future direction is a method-based source generation model.

The developer writes a `partial` packaged pipeline class and annotates:

* the class with pipeline-level attributes
* methods with step-level attributes

At a conceptual level, the attribute model should include:

* `PipelineAttribute`
* `PipelineStepAttribute`
* `PipelineHookAttribute`
* `PipelineBehaviorAttribute`

The class-level pipeline attribute should declare whether the generated pipeline is:

* a no-context packaged pipeline, or
* a context-aware packaged pipeline with an explicit `TContext`

Class-level hook and behavior attributes should declare optional generated additions such as:

* `PipelineAuditHook`
* `PipelineTracingBehavior`
* `PipelineTimingBehavior`

Method-level step attributes should declare step participation and order. Step order should be explicit through `PipelineStepAttribute(order)` and should not rely on source-file order as the primary contract.

### A.4 Generated Output

The generator should be implemented as a Roslyn source generator, following the same devkit style already used by existing generators such as those in `Domain.CodeGen`. A separate generator project is recommended, for example:

* `src/Common.Utilities.CodeGen`

The generated output should include:

* generated wrapper step classes for attributed step methods
* generated packaged pipeline-definition plumbing implementing `IPipelineDefinitionSource` or `IPipelineDefinitionSource<TContext>`
* generated use of the existing naming conventions for pipeline names and step names
* generated compile-time diagnostics for invalid authoring

The generated wrappers should remain conceptually normal pipeline steps. In other words, code generation should remove author boilerplate, but it should still target the normal runtime model described in this design.

Default naming should remain aligned with the existing conventions:

* pipeline class name with trailing `Pipeline` removed and converted to kebab-case
* generated step name from the method name, with a trailing `Async` removed before kebab-case conversion unless an explicit step name is supplied through the attribute

The generator should also support one escape hatch:

* a partial extension point such as `OnConfigureGenerated(builder)` so manual additions can still be appended when needed

That escape hatch should make it possible to add extra manual steps, hooks, behaviors, or definition-time conditions without abandoning generation entirely.

### A.5 Registration and Resolution

Generated packaged pipelines should register exactly like normal packaged pipelines.

That means the fluent application setup remains:

```csharp
services.AddPipelines()
    .WithPipeline<OrderImportPipeline>();
```

The same additive modular registration model should apply here as well, so repeated `AddPipelines()` calls and assembly-based registration should work for generated packaged pipelines exactly as they do for hand-written packaged pipelines.

There should be no separate generated registration API in the first code generation iteration.

This is intentional because generated pipelines should not create a second registration mental model. Registration should stay identical to manual packaged pipelines so that generated and hand-written packaged definitions can coexist cleanly.

Factory resolution should also remain unchanged:

* `pipelineFactory.Create<OrderImportPipeline>()` for no-context packaged pipelines
* `pipelineFactory.Create<OrderImportPipeline, OrderImportContext>()` for context-aware packaged pipelines

### A.6 Supported Method Signatures

The first code generation iteration should focus on packaged pipelines and support method signatures that cover the common step shapes while remaining predictable.

Supported method inputs may include:

* `TContext`
* `Result`
* `CancellationToken`

Additional method parameters should be treated as DI services and should become constructor-injected dependencies on the generated wrapper step classes.

Supported return types should include:

* `void`
* `Task`
* `Result`
* `Task<Result>`
* `PipelineControl`
* `Task<PipelineControl>`

This provides a useful balance:

* low-friction generated methods for simple processing
* full pipeline-control support when retry, break, or termination semantics are needed

### A.7 Runtime Semantics

Generated pipelines should preserve the same runtime semantics as manual pipelines.

The generated wrapper behavior should be:

* `void` / `Task` => keep the incoming carried `Result` unchanged and return `PipelineControl.Continue(...)`
* `Result` / `Task<Result>` => use the returned `Result` as the next carried `Result` and return `PipelineControl.Continue(...)`
* `PipelineControl` / `Task<PipelineControl>` => use the returned control object directly

This is especially important for early completion semantics:

* generated methods that return `PipelineControl` or `Task<PipelineControl>` may explicitly return `Retry(...)`
* generated methods that return `PipelineControl` or `Task<PipelineControl>` may explicitly return `Break(...)`
* generated methods that return `PipelineControl` or `Task<PipelineControl>` may explicitly return `Terminate(...)`

That means retry, break, and termination remain explicit and are not inferred heuristically by the generator.

Exceptions in generated steps should be handled exactly like exceptions in manually authored step classes:

* the engine catches the exception
* logs it
* appends `new ExceptionError(exception)` to the carried `Result`
* evaluates continuation according to the normal execution policy

### A.8 Diagnostics

The generator should produce strong compile-time diagnostics for invalid authoring patterns.

Examples include:

* pipeline class is not `partial`
* unsupported attributed step method signatures
* `async void` step methods
* duplicate explicit step orders
* duplicate generated step names
* invalid hook or behavior types
* context mismatches between the declared generated pipeline context and generated step method usage
* no declared steps on a generated pipeline class

These diagnostics are important because they preserve developer trust and keep code generation aligned with the rest of the explicit pipeline model.

### A.9 Deferred Scope

This appendix is intentionally future-facing and not part of the first implementation of the pipeline feature.

The deferred scope assumptions are:

* code generation is documented here only and is not required for the first implementation
* the first code generation iteration focuses on packaged pipelines, not direct builder pipelines
* registration remains intentionally identical to manual packaged pipelines
* the main manual implementation and testing of the pipeline feature should come first

Possible later extensions, after the first generator iteration, could include:

* generated registration/discovery helpers
* additional compile-time analyzers around pipeline graphs
* richer generated support for direct builder authoring scenarios

### A.10 Fictional Example

The following compact example illustrates the intended generated authoring experience:

```csharp
[Pipeline(typeof(OrderImportContext))]
[PipelineHook(typeof(PipelineAuditHook))]
[PipelineBehavior(typeof(PipelineTracingBehavior))]
[PipelineBehavior(typeof(PipelineTimingBehavior))]
public partial class OrderImportPipeline
{
    [PipelineStep(10)]
    public Result Validate(OrderImportContext context, Result result)
    {
        if (string.IsNullOrWhiteSpace(context.SourceFileName))
        {
            return result
                .WithMessage("Validation failed.")
                .WithError(new ValidationError("A source file name is required for import."));
        }

        return result.WithMessage("Validation succeeded.");
    }

    [PipelineStep(20)]
    public async Task<Result> LoadAsync(
        OrderImportContext context,
        Result result,
        IOrderImportRepository repository,
        CancellationToken cancellationToken)
    {
        var orders = await repository.LoadAsync(context.SourceFileName, cancellationToken);
        context.ImportedOrderCount = orders.Count;

        return result.WithMessage($"Loaded {orders.Count} orders.");
    }

    [PipelineStep(30)]
    public async Task<PipelineControl> PersistAsync(
        OrderImportContext context,
        Result result,
        IOrderRepository repository,
        CancellationToken cancellationToken)
    {
        if (context.ImportedOrderCount == 0)
        {
            return PipelineControl.Break(
                result.WithMessage("Nothing to persist."),
                "no imported orders");
        }

        await repository.SaveImportedOrdersAsync(context.TenantId, cancellationToken);
        context.IsPersisted = true;

        return PipelineControl.Continue(
            result.WithMessage("Persisted imported orders."));
    }

    partial void OnConfigureGenerated(IPipelineDefinitionBuilder<OrderImportContext> builder);
}

services.AddPipelines()
    .WithPipeline<OrderImportPipeline>();
```

In this future model, the generator would emit the wrapper step classes and packaged pipeline-definition plumbing, but runtime registration and execution would remain the same as for any other packaged pipeline.
