# Skill vs Custom Instructions - Decision Matrix

This document helps you decide whether to create an **Agent Skill** or update **Custom Instructions** for a given task or convention.

## Quick Decision Tree

```
Is this task-specific or project-wide?
├─ Task-specific → Agent Skill
└─ Project-wide → Custom Instructions

Does this need to be invoked on-demand?
├─ Yes → Agent Skill
└─ No (always apply) → Custom Instructions

Is this a multi-step workflow?
├─ Yes → Agent Skill
└─ No (single rule) → Custom Instructions

Does this involve code templates or examples?
├─ Yes → Agent Skill
└─ No (just rules) → Custom Instructions

Is this portable across projects?
├─ Yes → Agent Skill
└─ No (project-specific) → Custom Instructions
```

## Detailed Comparison

### Agent Skills

**Purpose**: Executable workflows that agents invoke for specific tasks.

**Characteristics**:
- **On-Demand**: Activated when needed (by file pattern or explicit invocation)
- **Task-Focused**: "Create ADR", "Add NuGet package", "Add domain aggregate"
- **Workflow-Oriented**: Multi-step procedures with specific outputs
- **Template-Driven**: Often include code templates or file structures
- **Portable**: Can be shared across projects of similar type

**When to Use**:
- Creating specific artifacts (ADRs, aggregates, tests)
- Workflows with 3+ distinct steps
- Tasks requiring code templates
- Operations with validation/quality gates
- Repeatable processes (onboarding new developers benefits from documented workflows)

**Examples**:
- `adr-writer`: Create Architectural Decision Records
- `nuget-manager`: Manage NuGet packages
- `domain-add-aggregate`: Scaffold domain aggregates
- `api-endpoint-generator`: Create REST endpoints
- `test-generator`: Create unit/integration tests

### Custom Instructions

**Purpose**: Always-active rules and conventions that shape agent behavior globally.

**Characteristics**:
- **Always Active**: Applied to every agent interaction
- **Constraint-Focused**: "Never do X", "Always use Y pattern"
- **Convention-Oriented**: Coding standards, architectural rules, project structure
- **Context-Setting**: Project overview, tech stack, design principles
- **Project-Specific**: Tailored to this codebase

**When to Use**:
- Coding standards and style guides
- Architectural boundaries and layer rules
- Naming conventions
- Technology choices (e.g., "use EF Core, not Dapper")
- Project structure and organization
- General principles (DDD, Clean Architecture)

**Examples** (from `.github/copilot-instructions.md` or `AGENTS.md`):
- "Use file-scoped namespaces"
- "Domain layer cannot reference Application layer"
- "Use Result<T> for recoverable errors"
- "Prefer repository abstractions over DbContext"
- "Use Mapster for object mapping"

## Detailed Matrix

| Criterion | Agent Skill | Custom Instructions |
|-----------|-------------|---------------------|
| **Activation** | On-demand (triggered by file pattern or invocation) | Always active |
| **Scope** | Specific task (create X, update Y) | Entire project (all code, all tasks) |
| **Structure** | Workflow (steps 1-N) | Rules and principles |
| **Content** | Templates, examples, checklists | Guidelines, constraints, conventions |
| **Length** | 50-1000+ lines | 100-500 lines (recommended) |
| **Format** | SKILL.md + optional subdirectories | Single markdown file(s) |
| **Portability** | High (works across similar projects) | Low (project-specific) |
| **User Invocation** | Explicit or pattern-triggered | Implicit (always considered) |
| **Maintenance** | Update when workflow changes | Update when project conventions change |

## Real-World Scenarios

### Scenario 1: Adding Error Handling Pattern

**Question**: Should this be a skill or custom instruction?

**Analysis**:
- Is it always-active? Yes (every new code should follow pattern)
- Is it project-wide? Yes (applies to all layers)
- Is it a rule or workflow? Rule ("always return Result<T>")

**Decision**: **Custom Instructions**

**Implementation** (in `.github/copilot-instructions.md`):
```markdown
## Error Handling

- Use `Result<T>` for recoverable errors (validation, business rule violations)
- Use exceptions only for exceptional cases (database down, out of memory)
- Chain operations using `.Ensure()`, `.Bind()`, `.Tap()`
```

### Scenario 2: Creating Integration Tests

**Question**: Should this be a skill or custom instruction?

**Analysis**:
- Is it on-demand? Yes (create test when needed)
- Is it a workflow? Yes (setup → arrange → act → assert)
- Does it need templates? Yes (test class structure, WebApplicationFactory setup)

**Decision**: **Agent Skill**

**Implementation**: Create `.github/skills/test-creator/SKILL.md` with:
- Test class template
- WebApplicationFactory configuration
- Arrange-Act-Assert examples
- Step-by-step workflow

### Scenario 3: Naming Conventions

**Question**: Should this be a skill or custom instruction?

**Analysis**:
- Is it always-active? Yes (every file/class follows conventions)
- Is it project-wide? Yes (applies to all code)
- Is it a rule or workflow? Rule ("Commands end with Command")

**Decision**: **Custom Instructions**

**Implementation** (in `AGENTS.md`):
```markdown
## Naming Conventions

- Commands: `[Entity][Action]Command` (e.g., `CustomerCreateCommand`)
- Queries: `[Entity][Action]Query` (e.g., `CustomerFindAllQuery`)
- Handlers: `[Entity][Command|Query]Handler`
- Value Objects: Singular descriptive (e.g., `EmailAddress`)
```

### Scenario 4: Database Migration Process

**Question**: Should this be a skill or custom instruction?

**Analysis**:
- Is it on-demand? Yes (create migration when schema changes)
- Is it a workflow? Yes (add migration → review → apply → test)
- Does it need validation? Yes (verify migration before applying)

**Decision**: **Agent Skill**

**Implementation**: Create `.github/skills/migration-manager/SKILL.md` with:
- `dotnet ef migrations add` workflow
- Migration review checklist
- Rollback procedures
- Testing steps

### Scenario 5: Repository Pattern Usage

**Question**: Should this be a skill or custom instruction?

**Analysis**:
- Is it always-active? Yes (Application layer always uses repositories)
- Is it project-wide? Yes (applies to all data access)
- Is it a rule or workflow? Rule ("never inject DbContext into handlers")

**Decision**: **Custom Instructions**

**Implementation** (in `AGENTS.md`):
```markdown
## Data Access

- Application handlers MUST use repository abstractions (`IGenericRepository<T>`)
- NEVER inject `DbContext` directly into Application layer
- Use specifications for complex queries
- Repository behaviors: logging, audit, domain events (in that order)
```

## When Both Are Needed

Some concepts span both categories:

### Example: Mapster Object Mapping

**Custom Instructions** (always-active rules):
```markdown
## Object Mapping

- Use Mapster for domain ↔ DTO mapping
- Define mappings in module-specific MapperRegister classes
- MapperRegister lives in Presentation layer
- NEVER use manual mapping in handlers
```

**Agent Skill** (on-demand workflow):
```
.github/skills/mapper-updater/SKILL.md
- Step 1: Identify domain entity and DTO
- Step 2: Locate module's MapperRegister class
- Step 3: Add mapping configuration
- Step 4: Handle value object conversions
- Step 5: Test mapping
```

**Why Both?**
- **Instructions**: Enforce the rule (always use Mapster, never manual mapping)
- **Skill**: Execute the task (add new mapping configuration)

### Example: Domain Aggregates

**Custom Instructions** (always-active rules):
```markdown
## Domain Design

- Aggregates inherit from `AuditableAggregateRoot<TId>`
- Factory methods return `Result<T>`
- Properties have private setters
- Register domain events in factory/change methods
```

**Agent Skill** (on-demand workflow):
```
.github/skills/domain-add-aggregate/SKILL.md
- Phase 1: Planning (gather requirements)
- Phase 2: Domain Layer (create aggregate, value objects, events)
- Phase 3: Infrastructure (EF configuration)
...
```

**Why Both?**
- **Instructions**: Enforce design principles (factory methods, private setters)
- **Skill**: Execute aggregate creation workflow (multi-layer scaffolding)

## Migration Path

### Converting Custom Instructions to Skills

**When to Convert**:
- Instructions include step-by-step procedures (should be workflow)
- Instructions are rarely relevant (should be on-demand)
- Instructions include code templates (should be in skill)

**Example**:
```markdown
<!-- BEFORE: In .github/copilot-instructions.md -->
## Creating Commands

1. Create command class in Application/Commands folder
2. Add properties with init-only setters
3. Create validator inheriting from AbstractValidator
4. Create handler inheriting from RequestHandlerBase
...
```

**AFTER: Extract to skill**
```
.github/skills/command-creator/SKILL.md
(Full workflow with templates)
```

Keep in custom instructions:
```markdown
## Application Layer

- Commands use init-only property setters
- Validators live in same file as commands
- Handlers inherit from RequestHandlerBase
```

### Converting Skills to Custom Instructions

**When to Convert**:
- Skill is always needed (every code change uses it)
- Skill is really just a rule, not a workflow
- Skill has 1-2 steps (too simple to be a skill)

**Example**:
```yaml
# BEFORE: .github/skills/logger-helper/SKILL.md
skill: logger-helper
description: Add structured logging to methods

Workflow:
1. Inject ILogger<T>
2. Use structured logging: LogInformation("Message {Param}", value)
```

**AFTER: Move to custom instructions**
```markdown
## Logging

- Inject `ILogger<T>` in constructors
- Use structured logging: `LogInformation("Message {Param}", value)`
- Avoid logging sensitive PII
```

## Best Practices

### For Agent Skills

1. **Clear Trigger**: Define when skill should be invoked (file patterns or task descriptions)
2. **Self-Contained**: Don't assume user has read custom instructions
3. **Template-Rich**: Provide copy-paste-ready templates
4. **Validation Steps**: Include checkpoints/quality gates
5. **Examples**: Show realistic scenarios

### For Custom Instructions

1. **Concise Rules**: State what to do/avoid clearly
2. **Rationale**: Briefly explain why (references to ADRs okay)
3. **No Procedures**: If it has steps, it's probably a skill
4. **Project Context**: Describe structure, tech stack, architecture
5. **Always Relevant**: Every rule should apply to most code changes

## Common Mistakes

### Mistake 1: Workflow in Custom Instructions

**WRONG** (in `.github/copilot-instructions.md`):
```markdown
## Creating Domain Aggregates

Step 1: Create aggregate class inheriting from AuditableAggregateRoot
Step 2: Add private parameterless constructor for EF Core
Step 3: Add factory method returning Result<T>
...
(20 more steps)
```

**CORRECT**: Extract to skill, keep only principles in instructions:
```markdown
## Domain Layer

- Aggregates inherit from `AuditableAggregateRoot<TId>`
- Factory methods return `Result<T>` with validation
- Properties have private setters
```

### Mistake 2: Rules in Skills

**WRONG** (in `.github/skills/code-writer/SKILL.md`):
```markdown
## Core Rules

- Always use file-scoped namespaces
- Always use var when type is obvious
- Always use PascalCase for classes
```

**CORRECT**: These are project-wide conventions, belong in custom instructions.

### Mistake 3: Skill for Simple Rule

**WRONG**: Creating `.github/skills/var-checker/SKILL.md` to enforce `var` usage.

**CORRECT**: Add to custom instructions:
```markdown
## Coding Style

- Use `var` when type is obvious from right-hand side
```

## Summary Decision Framework

### Create an Agent Skill if:

- [ ] Task is specific and on-demand (not always-active)
- [ ] Workflow has 3+ distinct steps
- [ ] Task requires code templates or file structures
- [ ] Task has validation/quality gates
- [ ] Task is repeatable across projects
- [ ] Task produces specific artifacts (files, migrations, etc.)

### Use Custom Instructions if:

- [ ] Rule applies to all code (always-active)
- [ ] Rule is project-wide convention (naming, structure)
- [ ] Rule is a constraint ("never X", "always Y")
- [ ] Rule is part of architecture (layer boundaries)
- [ ] Rule is a design principle (DDD, Result pattern)
- [ ] Rule requires no workflow (just awareness)

### Use Both if:

- [ ] Concept has always-active rules AND on-demand workflows
- [ ] Rules constrain behavior, skills execute tasks
- [ ] Example: Mapster (instructions: "use Mapster"; skill: "add mapping")

## References

- [VS Code Copilot Agent Skills Documentation](https://code.visualstudio.com/docs/copilot/customization/agent-skills)
- [agentskills.io Open Standard](https://agentskills.io)
- Project Custom Instructions: `.github/copilot-instructions.md`, `AGENTS.md`
- Project Agent Skills: `.github/skills/*/SKILL.md`

## Quick Reference Cheat Sheet

| If You Want To... | Use... | Example |
|-------------------|--------|---------|
| Create ADRs | Agent Skill | `adr-writer` |
| Enforce ADR format | Custom Instructions | "ADRs follow MADR format" |
| Add NuGet packages | Agent Skill | `nuget-manager` |
| Set allowed packages | Custom Instructions | "Use Mapster, not AutoMapper" |
| Scaffold aggregates | Agent Skill | `domain-add-aggregate` |
| Define aggregate rules | Custom Instructions | "Aggregates use factory methods" |
| Generate tests | Agent Skill | `test-creator` |
| Set test conventions | Custom Instructions | "Tests use xUnit + Shouldly" |
| Create migrations | Agent Skill | `migration-manager` |
| Define migration rules | Custom Instructions | "One migration per feature" |
