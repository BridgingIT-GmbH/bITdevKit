# Prompts

## Purpose

This document defines a reusable workflow for transforming large technical specifications into safe, incremental AI-agent implementation plans.

The workflow is intended for complex implementation efforts such as:

- frameworks
- infrastructure systems
- orchestration engines
- persistence systems
- distributed runtimes
- SDKs
- platform capabilities
- messaging systems
- developer tooling

The goal is not fully autonomous fire-and-forget implementation.
The goal is controlled, phase-based implementation with architectural checkpoints between phases.

---

## Recommended Usage

Use the prompts in the following order:

1. Generate a phased execution plan from the specification
2. Review and refine the generated phases
3. Run the architecture analysis prompt
4. Review the proposed subsystem decomposition and risks
5. Execute implementation prompts phase-by-phase
6. Stop after every phase
7. Run builds and tests
8. Review architecture and API shape
9. Continue only after validation

Do not execute all implementation prompts in a single agent session.

Do not allow agents to implement the entire specification in one pass.

Use the prompts in this progression:

`Specification -> Phase Plan -> Execution Governance -> Agent Execution System`

---

## Core Principles

The workflow assumes:

- agents are strong at bounded implementation
- agents are weaker at long-running architectural consistency
- humans remain responsible for architecture validation
- architectural checkpoints are required between phases
- tests are mandatory for every implementation phase

The prompts are intentionally designed to:

- reduce semantic drift
- reduce speculative abstractions
- prevent uncontrolled scaffolding
- preserve behavioral and persistence guarantees
- preserve repository conventions and project boundaries
- keep implementations incremental and reviewable

---

Use the prompts in this progression:

## Phase Plan Prompt — Generate Phased Agent Execution Prompts from a large Specification

```text
You are an experienced software architect and AI-agent specification engineer.

Your task is to transform a large technical specification into a structured set of bounded execution prompts for AI coding agents such as VS Code Copilot Agent Mode, Copilot CLI /fleet, Cursor Agents, Claude Code, or similar systems.

The goal is NOT to implement the feature itself.

Every implementation phase must end with explicit build/test/review checkpoints.

Do not generate large placeholder systems, unused abstractions, speculative interfaces, or future-proofing code not required by the specification.

The goal is to produce a safe, architecture-aware execution plan that allows large features to be implemented incrementally and reliably by AI agents.

The specification may describe:
- runtime systems
- frameworks
- infrastructure
- distributed systems
- APIs
- persistence layers
- workflow engines
- messaging systems
- SDKs
- platforms
- libraries
- developer tooling

Your output must produce:

1. Architecture analysis phase
2. Shared governance instructions
3. Sequential implementation phases
4. Testing and hardening phases
5. Optional parallel/fleet execution prompts
6. Recommended execution order

The execution prompts must be optimized for:
- bounded agent context
- architectural stability
- minimizing semantic drift
- minimizing uncontrolled scaffolding
- Behavioral/runtime semantics must be protected explicitly
- incremental reviewability
- testability
- safe concurrency of multiple agents

The generated execution plan must assume:
- agents are strong at bounded implementation
- agents are weaker at long-running architectural consistency
- humans will review between phases

Important constraints:

- Never allow the agents to implement the entire system in one pass
- Split implementation into capability layers
- Earlier phases must stabilize foundations before advanced features
- Infrastructure layers must not leak into runtime core
- Runtime semantics must be protected explicitly
- Persistence and concurrency semantics must receive dedicated phases
- Query/API/endpoint layers should come late
- Hardening and cleanup should be separate from feature implementation

For each phase:
- define exact implementation scope
- define explicit non-goals
- define required tests
- define architectural rules
- define validation expectations

The generated execution prompts should:
- be copy-paste ready
- use imperative language
- be implementation-focused
- explicitly prevent scope creep
- explicitly prevent speculative abstractions
- explicitly require tests

The generated output structure should look like:

- Intro
- Architecture Analysis Prompt
- Shared Governance Instructions
- Prompt 1 — Foundation
- Prompt 2 — Runtime
- Prompt 3 — Persistence
- ...
- Final Hardening Prompt
- Optional Fleet Prompt
- Recommended Execution Order

When determining phases:

- identify architectural seams
- identify runtime boundaries
- identify persistence boundaries
- isolate concurrency-sensitive areas
- isolate distributed-system semantics
- separate authoring model from runtime model
- separate runtime from infrastructure
- separate infrastructure from operational APIs
- defer advanced features until the runtime is stable

For every phase include:
- implementation focus
- implementation exclusions
- required tests
- behavioral/runtime guarantees that must be preserved

Prefer:
- many small bounded prompts
over:
- a few massive prompts

The prompts should feel suitable for implementing production-grade frameworks and infrastructure systems.

If the specification contains:
- orchestration semantics
- leases
- distributed coordination
- timers
- retries
- durable execution
- event sourcing
- messaging guarantees
- persistence guarantees
- transactional semantics

then explicitly create dedicated phases for those concerns.

The resulting prompts should be highly reusable across:
- different agent systems
- different repositories
- different framework features
- different infrastructure projects

Now read the provided specification and generate the complete phased execution prompt plan.
```

## Implementation Prompt 0 - Shared Specification Reference

Add the following instruction at the beginning of every implementation prompt below:

```text
Before implementing, re-read the relevant sections of the feature specification [SPECIFICATION].md and the implementation governance.

The specification is the primary behavioral source of truth.

Do not treat this prompt as a replacement for the specification.
Use it only as implementation governance.

The specification remains the source of truth for detailed behavior, APIs, naming, edge cases, examples, and acceptance criteria.

When implementing:

- preserve the architectural intent of the specification
- preserve existing repository conventions, naming, patterns, and project boundaries
- follow the defined behavioral and runtime semantics
- follow the feature’s intended architectural model and execution semantics
- do not invent alternative lifecycle behavior
- do not simplify persistence guarantees unless explicitly allowed
- do not bypass mandatory runtime, persistence, security, concurrency, or lifecycle guarantees
- do not generate large placeholder systems, unused abstractions, speculative interfaces, or future-proofing code not required by the specification
- add tests for every implemented behavior

Before modifying code, identify:

- affected capability layers
- affected behavioral/runtime invariants
- persistence implications
- security implications
- concurrency implications
- lifecycle implications
- required tests

If specification ambiguity exists:

- implement the simplest behavior consistent with the specification
- document the assumption in code comments and tests
- avoid introducing speculative abstractions
```

---

## Implementation Prompt 1 — Architecture Analysis Only

Use this FIRST.

```text
Do not implement anything yet.

Read the specification [SPECIFICATION].md and the implementation governance prompt completely.

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

Also identify which parts of the specification are:

- foundational
- operational
- infrastructure-specific
- optional or advanced
- risky or ambiguity-prone

Wait for approval before implementation begins.
```
