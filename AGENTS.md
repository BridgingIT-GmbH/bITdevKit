# AGENTS.md

This document provides AI agents with concise, high-signal context about this repository to produce high-quality, maintainable code and helpful developer assistance. It complements `.github/copilot-instructions.md` with a broader perspective on architecture, patterns, workflows, and conventions.

## Project Overview

- **Name**: bITdevKit
- **Architecture**: Onion / Clean Architecture + Modular vertical slices (Domain, Application, Infrastructure, Presentation, Tests). Full details in [ARCHITECTURE.md](./ARCHITECTURE.md).
- **Runtime**: ASP.NET Core minimal APIs, EF Core (SQL Server), Serilog.
- **Testing**: Unit tests in `tests/**.UnitTests`, integration tests in `tests/**.IntegrationTests`.
- **Development Workflow**: Described in [README.md](./README.md) and reinforced in this document.

## Goals for the Agent

- Generate concise, idiomatic C# 10+ (.NET 10) code following DDD and clean architecture.
- Respect layering boundaries and module isolation; avoid cross-layer leakage.
- Prefer repository abstractions and specifications over direct DbContext access in Application code.
- Use existing devkit features (requester, notifier, pipeline behaviors) instead of re-inventing infrastructure.
- Produce testable changes with unit/integration tests where meaningful.

## Agent Skills

**IMPORTANT**: This project uses Agent Skills in  to provide specialized, standardized workflows for common tasks.

### Skills Usage Policy

- **ALWAYS check for and use available skills** when the user's request matches a skill's description.
- Skills are located in `.agents/skills/` directories.
- Each skill provides a tested, standardized approach to specific tasks.
- Using skills ensures consistency, follows best practices, and reduces errors.
- Use the `find-skills` skill to discover available skills when you're unsure which skill applies to your task.

### When to Use Skills

- When a user request explicitly matches a skill's purpose (e.g., "commit changes" → use `git-commit` skill)
- When performing tasks that have established workflows (e.g., adding aggregates, reviewing code)
- Before manually implementing any workflow, check if a skill exists for it
- **Default to using skills over ad-hoc manual approaches**

### Skill Priority

1. **First**: Check if a skill exists for the task
2. **Second**: Load and follow the skill's workflow
3. **Last Resort**: Only use manual approaches when no skill exists

This ensures all agents follow the same high-quality, tested patterns that the project relies on.

## Coding Standards

- Please follow the rules in [.editorconfig](.editorconfig).
- **Language**: C# 10+; file-scoped namespaces.
- **Style**: Follow C# Coding Conventions; descriptive names; expressive syntax (null-conditional, string interpolation).
- **Types**: Use `var` when type is obvious; prefer records, pattern matching, null-coalescing assignment.
- **Naming**:
  - PascalCase for classes, methods, public members.
  - camelCase for locals/private fields; prefix interfaces with `I` (e.g., `IUserService`).
  - Constants in UPPERCASE.
  - Use `this.` for fields.
- **Validation & Errors**: Prefer `Result<T>` for recoverable failures; exceptions only for exceptional cases. Use FluentValidation for inputs.
- **Async**: Use `async/await` for I/O-bound operations.
- **LINQ**: Prefer efficient LINQ; avoid N+1 queries.
- **Nullability**: Project uses disabled nullability annotations; maintain consistency.

## Tech Stack

- **Frameworks**: ASP.NET Core minimal API, EF Core (SQL Server), Mapster, Serilog, Quartz, FluentValidation.
- **Testing**: xUnit, NSubstitute, Shouldly; WebApplicationFactory for integration.

## Architecture & Layering

- **Domain**: Aggregates, Value Objects, Enumerations, Domain Events, Business Rules. No references to outer layers.
- **Application**: Commands/Queries, Handlers, DTO models, Specifications. References Domain only; do not reference Infrastructure/Presentation.
- **Infrastructure**: EF Core DbContext/configurations, repositories, jobs, startup tasks. May reference Domain & Application; expose abstractions.
- **Presentation**: Minimal API endpoints, module registration, mapping profiles; references Application (and Domain types as needed for mapping).
- **Host**: Server project wiring, middleware (Serilog, correlation, problem details, swagger).

## Development Workflows

- Use tasks defined in the workspace to build, test, and manage EF:
  - Build: `Solution - build`
  - Tests: `Solution - tests (unit)`, `Solution - tests (integration)`
- Prefer these tasks over custom scripts to maintain consistency.

## Observability & Logging

- Use Serilog with structured logging.
- Use appropriate log levels (Information for normal ops, Warning for recoverable issues, Error for exceptions).
- Use the following message templates for consistency: `[LogKey] short message (prop1=abc, prop2=123)`
- Avoid logging sensitive PII; use structured templates (e.g., `logger.LogInformation("Customer {CustomerId} created", customer.Id);`).

## Documentation

- Use Markdown for docs located under `/docs/`.

## Architectural Decision Records (ADRs)

The project maintains comprehensive ADRs documenting key architectural decisions in `/docs/adr/`. Reference these when working on related features.

## Testing Strategy

- Unit tests: focus on handlers, domain logic, rules, mapping.
- Integration tests: use WebApplicationFactory; exercise endpoints and persistence.

## Git & PR Process

- Branch naming: `feature/<area>-<short-description>`, `fix/<issue>`, `chore/<task>`.
- Small, focused PRs; follow existing folder structure and naming conventions.
- Include tests and docs updates where applicable.
- Avoid unrelated formatting changes; use `Solution [format apply]` for targeted formatting.

### Commit Guidance

When a user asks to create a commit or mentions `/commit`, use the [`.agents/skills/git-commit/SKILL.md`](./.agents/skills/git-commit/SKILL.md) workflow.

Excerpt:

> Create standardized, semantic git commits using the Conventional Commits specification. Analyze the actual diff to determine appropriate type, scope, and message.

Agent expectations:

- Use the Conventional Commits format: `<type>[optional scope]: <description>`
- Analyze `git diff --staged` first, or `git diff` when nothing is staged
- Check `git status --porcelain` before committing
- Keep the description imperative and under 72 characters
- Prefer one logical change per commit
- Never commit secrets
- Never update git config, skip hooks, force push, or run destructive git commands unless the user explicitly asks

## Guidance for Agent Prompts

When asking an agent to implement something, include:

- Target feature (e.g., `StartupTasks` or `JobScheduling`).
- Layer scope (Domain, Application, Infrastructure and/or Presentation).
- Feature requirements.
- Existing features to reuse (abstractions, extensions, utilities, pipeline behaviors, results, rules etc.).

## Repository Layout Snapshot

```text
/ (root)
  ./agents/skills/
  ./github/copilot-instructions.md
  AGENTS.md
  README.md
  ARCHITECTURE.md
  .editorconfig
  src/
    Common/
    Application/
    Domain/
    Infrastructure/
    Presentation/
  tests/
    UnitTests/
    IntegrationTests/
```

## Alignment with `.github/copilot-instructions.md`

This AGENTS.md reinforces and summarizes the rules found in `.github/copilot-instructions.md`. Agents should treat that file as authoritative for architectural boundaries, naming and module practices.
