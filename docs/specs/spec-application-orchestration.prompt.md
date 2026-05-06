# Orchestration Feature - Agent Execution Prompts

This document contains the recommended execution prompts in the correct order for implementing the orchestration feature using VS Code Copilot Agent Mode and/or Copilot CLI `/fleet`.

The prompts are intentionally split into:

* governance and architectural analysis
* bounded implementation phases
* review and stabilization phases

This prevents the agents from prematurely implementing the entire system in one uncontrolled pass.

---

# Prompt 1 — Architecture Analysis Only

Use this FIRST.

Do not allow implementation yet.

```text
Do not implement anything yet.

Read the orchestration specification spec-application-orchestration.md and the orchestration implementation governance prompt completely.

Your task right now is architectural analysis only.

Produce:

- subsystem decomposition
- project dependency graph
- capability-layer implementation order
- agent ownership proposal
- critical runtime invariants
- highest-risk implementation areas
- persistence consistency risks
- concurrency and lease risks
- suggested test strategy
- suggested initial milestones
- unclear or ambiguous areas in the specification
- recommended implementation sequence

Do not create or modify files yet.
Do not generate scaffolding yet.
Do not generate implementation code yet.

Wait for approval before implementation begins.
```

---

# Shared Specification Reference

Add the following instruction at the beginning of every implementation prompt below:

```text
Before implementing, re-read the relevant sections of the orchestration feature specification spec-application-orchestration.md and the orchestration implementation governance prompt.

The orchestration specification is the primary behavioral source of truth.

When implementing:

- preserve the architectural intent of the specification
- follow the defined runtime semantics
- do not invent alternative lifecycle behavior
- do not simplify persistence guarantees unless explicitly allowed
- do not bypass durable boundaries or leases
- follow the state-machine-oriented orchestration model
- add tests for every implemented runtime behavior

Before modifying code, identify:

- affected capability layers
- affected runtime invariants
- persistence implications
- concurrency implications
- lifecycle implications
- required tests

If specification ambiguity exists:

- implement the simplest behavior consistent with the specification
- document the assumption in code comments and tests
- avoid introducing speculative abstractions
```

---

# Prompt 2 — Foundation Layer Implementation

Use this after architecture review.

```text
Implement the Foundation Layer only.

Focus on:

- orchestration definition model
- orchestration base classes
- states
- activities
- inline activities
- activity outcomes
- transitions
- lifecycle statuses
- orchestration context
- builder API
- definition validation
- basic execution model
- in-memory execution only
- XML documentation comments

Implement:

- orchestration definition registration
- typed orchestration context
- sequential activity execution
- explicit state transitions
- first-matching transition semantics
- terminal states
- Continue/Retry/Wait/Complete/Cancel/Terminate outcomes

Do not implement yet:

- EF provider
- endpoints
- leases
- distributed execution
- durable timers
- background workers
- compensation
- child orchestrations
- parallel branches
- advanced retry handling
- metrics
- query APIs

Add unit tests for:

- state progression
- transitions
- outcomes
- inline activities
- class-based activities
- context mutation
- validation failures
- no-transition failures

Ensure the solution builds successfully.
Ensure tests pass.
```

---

# Prompt 3 — Persistence Abstractions and In-Memory Provider

```text
Implement the persistence abstractions and in-memory provider.

Focus on:

- IOrchestrationPersistenceProvider
- instance store
- history store
- signal store
- timer store
- lease store abstractions
- query store abstractions
- serializer integration
- durable snapshot model
- execution history model

Implement:

- in-memory persistence provider
- in-memory snapshot persistence
- append-only execution history
- signal persistence
- timer persistence
- query access to persisted state

Do not implement EF yet.
Do not implement endpoints yet.

Add tests for:

- persistence behavior
- history appending
- signal persistence
- timer persistence
- snapshot consistency
- serializer usage

Ensure no EF references exist in the runtime core.
```

---

# Prompt 4 — Runtime Engine

```text
Implement the orchestration runtime engine.

Focus on:

- ExecuteAsync
- DispatchAsync
- DispatchAndWaitAsync
- state advancement
- activity execution pipeline
- transition evaluation
- waiting behavior
- signal processing
- timer processing
- pause/resume/cancel/terminate
- retry behavior
- durable boundaries
- lifecycle transitions

Implement:

- explicit state advancement
- durable snapshot persistence at boundaries
- append-only execution history
- waiting semantics
- signal-driven transitions
- timeout-driven transitions
- inline execute failure when waiting/paused is reached
- DispatchAndWait timeout and cancellation support

Do not implement EF provider yet.
Do not implement endpoints yet.

Add tests for:

- waiting
- pause/resume
- cancel
- terminate
- retry behavior
- signal-driven transitions
- timeout-driven transitions
- lifecycle correctness
- DispatchAndWait behavior
- timeout semantics
- cancellation semantics

Ensure runtime behavior follows the specification exactly.
```

---

# Prompt 5 — Leases and Concurrency

```text
Implement orchestration instance leases and concurrency protection.

Focus on:

- orchestration instance leases
- lease acquisition
- lease renewal
- lease expiration
- lease loss handling
- exclusive instance mutation
- optimistic concurrency

Implement:

- one active worker per orchestration instance
- lease enforcement around:
  - activity execution
  - signal handling
  - timer handling
  - transitions
  - pause/resume
  - cancel/terminate
- graceful handling of lost leases

Add tests for:

- concurrent execution attempts
- lease conflicts
- lease expiration
- lease recovery
- lease loss during execution
- concurrent signal delivery
- concurrent timer handling

Do not implement EF provider yet.
```

---

# Prompt 6 — Durable Timers and Signals

```text
Implement durable timer and signal processing.

Focus on:

- persisted timers
- persisted signals
- timer scheduling
- timer consumption
- signal correlation
- signal idempotency
- ignored/rejected signals

Implement:

- WaitForSignal
- WhenSignal
- timeout scheduling
- durable signal inbox
- deterministic timer ordering
- obsolete timer suppression
- paused-orchestration timer behavior
- persisted signal payloads

Add tests for:

- signal idempotency
- ignored signals
- rejected signals
- timer ordering
- overdue timers
- paused timer behavior
- resume behavior
- timeout transitions

Ensure signals are persisted before processing.
Ensure timers are persisted before execution.
```

---

# Prompt 7 — Test Harness

```text
Implement the orchestration test harness.

Focus on:

- orchestration runtime testing support
- fake clock support
- signal helpers
- assertion helpers
- deterministic execution

Implement:

- OrchestrationTestHarness
- fake time advancement
- orchestration registration
- inline execution
- dispatch support
- signal support
- wait assertions
- history assertions
- retry assertions
- compensation assertions

The harness should support fluent testing.

Add end-to-end tests using:

- OrderApprovalOrchestration
- TelephoneCallOrchestration
```

---

# Prompt 8 — Entity Framework Provider

```text
Implement the Entity Framework orchestration provider.

Focus on:

- EF persistence types
- IOrchestrationContext
- DbContext integration
- EF stores
- EF query support
- EF metrics support
- optimistic concurrency
- lease persistence

Implement:

- orchestration instance tables
- history tables
- signal tables
- timer tables
- lease metadata
- EF provider registration
- serializer integration

Rules:

- do not add EF dependencies to runtime core
- do not use Entity suffixes on persistence types
- use annotation-based EF mapping where appropriate
- use optimistic concurrency
- support transactional persistence

Add integration tests for:

- persistence
- queries
- metrics
- concurrency
- leases
- signals
- timers
- snapshot consistency
```

---

# Prompt 9 — Query Services and Metrics

```text
Implement orchestration query services and metrics.

Focus on:

- orchestration queries
- execution history queries
- timer queries
- signal queries
- metrics
- dashboard-ready query contracts

Implement:

- get instance
- get context
- get history
- get timers
- get signals
- query instances
- metrics aggregation

Ensure all query data comes from persisted state.
Do not use worker memory as source of truth.

Add tests for:

- metrics correctness
- query filtering
- paging
- history retrieval
- signal retrieval
- timer retrieval
```

---

# Prompt 10 — Endpoint Layer

```text
Implement the orchestration administration endpoint layer.

Focus on:

- endpoint registration
- route mapping
- Result-to-HTTP mapping
- ProblemDetails mapping
- administration operations

Implement endpoints for:

- querying orchestrations
- querying history
- querying signals
- querying timers
- querying metrics
- signal delivery
- pause
- resume
- cancel
- terminate
- archive
- repair operations

Rules:

- endpoint layer depends only on service abstractions
- no EF references in endpoints
- follow specified HTTP mappings
- use stable orchestration problem types

Add integration tests for:

- HTTP status mappings
- ProblemDetails responses
- invalid lifecycle operations
- endpoint routing
- successful orchestration operations
```

---

# Prompt 11 — Advanced Workflow Features

```text
Implement the advanced workflow layer.

Focus on:

- compensation
- child orchestrations
- parallel branches
- loops
- advanced retries
- built-in activities
- approval/human-task helpers

Implement:

- compensation execution ordering
- child orchestration execution
- branch coordination
- loop execution semantics
- advanced retry policies
- built-in activity catalog

Add tests for:

- compensation ordering
- child orchestration lifecycle
- parallel branch completion
- loop termination
- advanced retry behavior
- built-in activity behavior

Ensure advanced features still obey:

- durable boundaries
- leases
- context persistence
- execution history
- explicit transitions
```

---

# Prompt 12 — Final Hardening and Cleanup

```text
Perform final hardening and cleanup.

Focus on:

- XML documentation completeness
- naming consistency
- runtime consistency
- dead code removal
- architecture validation
- edge-case handling
- performance review
- concurrency review
- persistence review
- test completeness

Verify:

- runtime has no EF dependency
- EF provider has no endpoint dependency
- endpoint layer only depends on abstractions
- durable boundaries persist correctly
- signals are persisted before processing
- timers are persisted before execution
- leases protect all state mutations
- tests cover happy paths and failure paths
- examples compile and run
- public APIs are clean and usable

Run:

- full solution build
- all unit tests
- all integration tests
- analyzers
- formatting

Do not introduce new functionality.
Focus only on correctness, consistency, cleanup and stabilization.
```

---

# Optional Fleet Prompt

Use this with Copilot CLI `/fleet`.

```text
/fleet

Agent A:
Implement Core Authoring Model and Builder API.

Agent B:
Implement Persistence Abstractions and In-Memory Provider.

Agent C:
Implement Runtime Engine.

Agent D:
Implement Test Harness and Example Orchestrations.

Rules:

- avoid modifying the same files simultaneously
- communicate interface assumptions explicitly
- compile after each major change
- add tests continuously
- do not implement EF or endpoints yet
```

---

# Recommended Execution Order

1. Prompt 1 — Architecture Analysis
2. Prompt 2 — Foundation Layer
3. Prompt 3 — Persistence Abstractions
4. Prompt 4 — Runtime Engine
5. Prompt 5 — Leases and Concurrency
6. Prompt 6 — Durable Timers and Signals
7. Prompt 7 — Test Harness
8. Prompt 8 — Entity Framework Provider
9. Prompt 9 — Query Services and Metrics
10. Prompt 10 — Endpoint Layer
11. Prompt 11 — Advanced Workflow Features
12. Prompt 12 — Final Hardening and Cleanup

Stop after every phase.
Review architecture.
Run tests.
Validate API shape before continuing.
