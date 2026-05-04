---
title: Repository vs ActiveEntity
---

# Repository vs ActiveEntity

`bITdevKit` supports both patterns. The right choice depends on the complexity of the domain and the
kind of codebase being built.

## Choose Repository when

- aggregates and domain boundaries matter
- query specifications or richer composition are needed
- strict separation of concerns is important
- the codebase is expected to grow in complexity

See:
[Domain Repositories](reference/features-domain-repositories.md)

## Choose ActiveEntity when

- the scenario is more CRUD-oriented
- development speed matters more than abstraction purity
- the domain is simple and the persistence model is straightforward
- a more direct style is acceptable

See:
[ActiveEntity](reference/features-domain-activeentity.md)

## Quick comparison

| Concern | Repository | ActiveEntity |
|---|---|---|
| Complexity fit | Better for richer domains | Better for simpler CRUD-style domains |
| Testing | Clear abstractions for mocking or substitution | More direct persistence style |
| DDD alignment | Stronger aggregate boundary emphasis | Lighter-weight model |
| Query flexibility | Better for specifications and filtering | More limited |
| Onboarding simplicity | More concepts up front | Easier to start with |

## Practical rule of thumb

Start with `Repository` when the domain model is expected to matter.

Use `ActiveEntity` when speed and simplicity are the higher priority.
