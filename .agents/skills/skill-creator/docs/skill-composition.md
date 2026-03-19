# Skill Composition Patterns

This document explores strategies for combining multiple Agent Skills to solve complex tasks—creating powerful workflows from modular, reusable components.

## What is Skill Composition?

**Skill Composition** is the practice of:
- Chaining multiple skills sequentially
- Invoking one skill from within another
- Reusing skill patterns across related workflows
- Building complex workflows from simple building blocks

**Goal**: Maximize reusability, minimize duplication, create composable skill ecosystem.

## Why Compose Skills?

### Problem: Monolithic Skills

**Without Composition**:
```
skill-1: scaffold-feature (2000 lines)
├── Create domain aggregate (500 lines)
├── Create application commands (500 lines)
├── Create infrastructure (300 lines)
├── Create API endpoints (400 lines)
└── Generate tests (300 lines)
```

**Issues**:
- Duplicates patterns from other skills
- Hard to maintain (changes affect entire skill)
- Can't reuse sub-workflows independently

**With Composition**:
```
skill-1: scaffold-feature (300 lines)
├── Invoke: domain-add-aggregate
├── Invoke: command-generator
├── Invoke: infrastructure-setup
├── Invoke: endpoint-generator
└── Invoke: test-generator
```

**Benefits**:
- Each sub-skill is independently usable
- Changes to sub-skills automatically benefit composite skill
- Reduces duplication across similar workflows

## Composition Patterns

### Pattern 1: Sequential Chaining

**Use Case**: Multi-phase workflows where each phase depends on previous

**Structure**:
```markdown
## Workflows

### Phase 1: Domain Layer
Execute domain-add-aggregate skill:
[Invoke: domain-add-aggregate with Entity=[EntityName]]

### Phase 2: Application Layer
Execute command-generator skill:
[Invoke: command-generator with Entity=[EntityName]]

### Phase 3: Presentation Layer
Execute endpoint-generator skill:
[Invoke: endpoint-generator with Entity=[EntityName]]
```

**Example**: `scaffold-crud-feature` skill chains:
1. `domain-add-aggregate` → creates aggregate
2. `command-query-generator` → creates CQRS operations
3. `endpoint-generator` → creates API endpoints
4. `test-generator` → creates test suite

**Benefits**:
- Clear phase boundaries
- Each phase can be validated independently
- Failures isolated to specific phase

### Pattern 2: Conditional Branching

**Use Case**: Workflow adapts based on project structure or user choice

**Structure**:
```markdown
### Step 3: Determine Architecture Style

Check project structure:

\`\`\`bash
# Check if using vertical slices
if [ -d "src/Features/" ]; then
  [Invoke: vertical-slice-generator]
else
  [Invoke: layered-architecture-generator]
fi
\`\`\`
```

**Example**: `scaffold-feature` skill checks:
- If `src/Features/` exists → vertical slice architecture
- If `src/Domain/`, `src/Application/` exist → layered architecture

**Benefits**:
- Single skill adapts to different project styles
- Reuses specialized skills for each style

### Pattern 3: Parallel Execution

**Use Case**: Independent tasks that can run concurrently

**Structure**:
```markdown
### Step 5: Generate Supporting Files (Parallel)

Execute these in parallel:
- [Invoke: dto-generator with Entity=[EntityName]]
- [Invoke: validator-generator with Entity=[EntityName]]
- [Invoke: mapper-generator with Entity=[EntityName]]

All three are independent and can run concurrently.
```

**Example**: `scaffold-application-layer` generates:
- Commands
- Queries
- Validators
- Mappers

(All independent, can run in parallel)

**Benefits**:
- Faster execution
- Clear independence of tasks

### Pattern 4: Template Reuse

**Use Case**: Multiple skills share common templates

**Structure**:
```
.github/skills/
├── _shared/                          # Shared resources
│   └── templates/
│       ├── entity-base-template.cs
│       ├── command-base-template.cs
│       └── ...
├── domain-add-aggregate/
│   ├── SKILL.md                      # References _shared/templates/
│   └── templates/                    # Skill-specific templates
└── command-generator/
    ├── SKILL.md                      # References _shared/templates/
    └── templates/                    # Skill-specific templates
```

**Example**: Both `domain-add-aggregate` and `command-generator` reference shared base templates.

**Benefits**:
- Single source of truth for common patterns
- Updates to shared templates benefit all skills
- Reduces duplication

### Pattern 5: Skill Inheritance (Conceptual)

**Use Case**: Specialized skills extend base skill

**Structure**:
```markdown
---
skill: aggregate-with-soft-delete
description: Adds aggregate with soft delete support (extends domain-add-aggregate)
extends: domain-add-aggregate
---

## Overview
This skill extends `domain-add-aggregate` with soft delete pattern.

## Workflows

### Step 1-10: Create Base Aggregate
[Invoke: domain-add-aggregate with Entity=[EntityName]]

### Step 11: Add Soft Delete Support
Add `IsDeleted` property and `Delete()` method...
```

**Example**: `aggregate-with-soft-delete` extends `domain-add-aggregate` with additional soft delete logic.

**Benefits**:
- Specialization without duplication
- Base skill updates automatically flow to specialized skills

## Implementation Strategies

### Strategy 1: Explicit Invocation Syntax

**In SKILL.md**:
```markdown
### Step 5: Create Aggregate

Invoke the domain-add-aggregate skill:

**User Action**: Ask agent to execute domain-add-aggregate
**Expected**: Agent creates aggregate, value objects, domain events

Continue when aggregate creation is complete.
```

**Pros**:
- Clear to human readers
- User knows another skill will be invoked

**Cons**:
- Requires manual step (user must invoke sub-skill)
- Not automated

### Strategy 2: Reference Pattern

**In SKILL.md**:
```markdown
### Step 5: Create Aggregate

Follow the workflow from domain-add-aggregate skill:
[See: .github/skills/domain-add-aggregate/SKILL.md]

**Summary**:
1. Create aggregate class with factory method
2. Create value objects
3. Create domain events
4. Configure EF Core mapping

Continue when aggregate is complete.
```

**Pros**:
- Reuses existing documentation
- User can follow detailed workflow

**Cons**:
- User must navigate to other skill
- Not automated

### Strategy 3: Inline Summary with Reference

**In SKILL.md**:
```markdown
### Step 5: Create Aggregate

Create domain aggregate with:
- Factory method returning Result<T>
- Private setters on properties
- Domain events for Created/Updated/Deleted

**Quick Start**:
\`\`\`csharp
[Show minimal example]
\`\`\`

**Detailed Guide**: [.github/skills/domain-add-aggregate/SKILL.md]
```

**Pros**:
- User can proceed quickly (inline example)
- Detailed guide available if needed

**Cons**:
- Some duplication between skills

### Strategy 4: Template Sharing (Recommended)

**Composite Skill**:
```markdown
### Step 5: Create Aggregate

Create aggregate class using shared template:
[See: .github/skills/_shared/templates/aggregate-template.cs]

Follow these patterns:
- Factory method: [Returns Result<T>]
- Domain events: [Register in factory and change methods]
- EF configuration: [See step 10]

For complete workflow, see: domain-add-aggregate skill
```

**Base Skill** (domain-add-aggregate):
```markdown
### Step 5: Create Aggregate Class

Use template:
[See: .github/skills/_shared/templates/aggregate-template.cs]
```

**Pros**:
- Templates shared across skills
- Each skill provides appropriate context
- No skill directly "invokes" another (loose coupling)

**Cons**:
- Requires _shared/ directory for templates

## Dependency Management

### Avoiding Circular Dependencies

**WRONG**:
```
skill-A references skill-B
skill-B references skill-A
```

**Result**: Infinite loop, confusion

**CORRECT**:
```
skill-A references base-skill
skill-B references base-skill
(skill-A and skill-B are independent)
```

**Pattern**: Use shared resources, not cross-references.

### Versioning Composed Skills

**Challenge**: What happens when sub-skill changes?

**Strategy 1: Loose Coupling (Recommended)**
- Composite skill references sub-skill by name, not version
- Assumes sub-skill maintains backward compatibility
- If breaking change occurs, update composite skill

**Strategy 2: Version Pinning**
```markdown
[Invoke: domain-add-aggregate v2.0]
```
- Composite skill pins to specific version
- Sub-skill changes don't break composite skill

**Strategy 3: Feature Detection**
```markdown
### Step 5: Create Aggregate

Check if domain-add-aggregate supports soft delete:
- If yes: [Invoke with soft delete option]
- If no: [Manual soft delete setup]
```
- Composite skill detects sub-skill capabilities
- Adapts workflow accordingly

## Skill Composition Anti-Patterns

### Anti-Pattern 1: Deep Nesting

**WRONG**:
```
scaffold-feature
└─ calls domain-add-aggregate
   └─ calls entity-creator
      └─ calls base-class-selector
         └─ calls ...
```

**Problem**: Too many levels, hard to follow

**CORRECT**:
```
scaffold-feature
├─ calls domain-add-aggregate (self-contained)
├─ calls command-generator (self-contained)
└─ calls endpoint-generator (self-contained)
```

**Guideline**: Maximum 2 levels of composition

### Anti-Pattern 2: Tight Coupling

**WRONG** (skill-A depends on skill-B's internal structure):
```markdown
Execute skill-B, then read its output from:
`/tmp/skill-b-output.json`
```

**Problem**: skill-A breaks if skill-B changes output location

**CORRECT** (skill-A uses skill-B's documented interface):
```markdown
Execute skill-B with Entity=[EntityName].
skill-B will create files in standard locations:
- src/Domain/Model/[Entity].cs
- src/Domain/Events/[Entity]CreatedEvent.cs

Continue when files exist.
```

**Guideline**: Depend on documented outputs, not implementation details

### Anti-Pattern 3: Duplication "Just in Case"

**WRONG**:
```
scaffold-feature (2000 lines)
├─ Inline: full aggregate creation workflow (500 lines)
│  (duplicates domain-add-aggregate skill)
├─ Inline: full command creation workflow (500 lines)
│  (duplicates command-generator skill)
...
```

**Reasoning**: "What if domain-add-aggregate changes?"

**Problem**: Duplication maintenance burden

**CORRECT**:
```
scaffold-feature (300 lines)
├─ Reference: domain-add-aggregate skill
├─ Reference: command-generator skill
...
```

**Guideline**: Trust sub-skills, reference them. Update composite skill only if sub-skill breaks compatibility.

## Composition Examples

### Example 1: scaffold-crud-feature

**Purpose**: Create complete CRUD feature for entity

**Composition**:
```markdown
## Workflows

### Phase 1: Planning
[User provides entity name and properties]

### Phase 2: Domain Layer
[Invoke: domain-add-aggregate]
Creates: Aggregate, Value Objects, Domain Events

### Phase 3: Application Layer
[Invoke: command-query-generator]
Creates: Create/Update/Delete commands + FindOne/FindAll queries

### Phase 4: Infrastructure Layer
[Invoke: ef-configuration-generator]
Creates: EF Core type configuration, migration

### Phase 5: Presentation Layer
[Invoke: endpoint-generator]
Creates: Minimal API endpoints (POST, GET, PUT, DELETE)

### Phase 6: Validation
Run build: `dotnet build`
[Invoke: architecture-validator] (checks layer boundaries)
```

**Result**: Full CRUD feature with ~300 lines in composite skill, reusing 5 sub-skills.

### Example 2: migrate-to-clean-architecture

**Purpose**: Refactor existing code to Clean Architecture

**Composition**:
```markdown
## Workflows

### Phase 1: Analysis
[Invoke: architecture-analyzer]
Identifies: Current architecture, dependencies, violations

### Phase 2: Create Target Structure
[Invoke: clean-architecture-scaffolder]
Creates: Domain/, Application/, Infrastructure/, Presentation/ layers

### Phase 3: Move Domain Logic
[Invoke: domain-extractor]
Extracts: Entities, value objects from existing code

### Phase 4: Refactor Application Layer
[Invoke: cqrs-converter]
Converts: Service methods to commands/queries

### Phase 5: Validate
[Invoke: architecture-validator]
Confirms: Layer boundaries respected, no violations
```

**Result**: Multi-phase refactoring workflow reusing 5 specialized skills.

## Best Practices

### 1. Design Skills for Single Responsibility

**GOOD**:
- `domain-add-aggregate`: Creates aggregate, value objects, events
- `ef-configuration-generator`: Creates EF configuration
- `test-generator`: Creates tests

**BAD**:
- `domain-and-infrastructure-generator`: Creates aggregate + EF config + migrations

**Why**: Single-responsibility skills are more reusable.

### 2. Document Expected Outputs

Each skill should document what files/changes it creates:

```markdown
## Expected Outputs

After execution, these files will exist:
- `src/Domain/Model/[Entity]Aggregate/[Entity].cs`
- `src/Domain/Events/[Entity]CreatedEvent.cs`
- `src/Domain/Events/[Entity]UpdatedEvent.cs`
```

**Why**: Composite skills can verify sub-skill completion.

### 3. Make Skills Independently Usable

Even if skill is designed to be invoked from composite skill, it should be usable standalone:

```markdown
## Overview
This skill creates domain aggregates following DDD and Clean Architecture.

**Standalone Use**: Create aggregates independently
**Composed Use**: Invoked by scaffold-crud-feature skill
```

**Why**: Increases skill utility, enables ad-hoc usage.

### 4. Minimize Coupling

**Loose Coupling (Good)**:
- Composite skill references sub-skill by name
- Composite skill uses sub-skill's documented outputs
- Sub-skill can change implementation without breaking composite

**Tight Coupling (Bad)**:
- Composite skill depends on sub-skill's temp files
- Composite skill assumes sub-skill's internal workflow order
- Sub-skill change breaks composite

### 5. Test Composition

**Test Scenarios**:
- Can composite skill execute end-to-end?
- If sub-skill changes, does composite still work?
- Can sub-skills be used independently?
- Are outputs from one phase valid inputs for next?

## Skill Marketplace Vision

**Future State**: Skill composition enables marketplace of reusable skills:

```
.github/skills/
├── @community/domain-add-aggregate/     # Community skill
├── @company/custom-aggregate/           # Company-specific skill
└── my-scaffold-feature/                 # Project skill (composes community skills)
```

**Benefits**:
- Projects reuse community skills
- Companies extend with custom skills
- Individuals compose for specific needs

**Prerequisite**: Strong composition patterns, loose coupling, version compatibility.

## Conclusion

**Skill Composition Enables**:
- Reusable building blocks
- Complex workflows from simple skills
- Reduced duplication
- Maintainable skill ecosystem

**Key Patterns**:
1. Sequential Chaining (multi-phase workflows)
2. Conditional Branching (adapt to project context)
3. Parallel Execution (independent tasks)
4. Template Reuse (shared resources)
5. Skill Inheritance (specialization)

**Best Practices**:
- Single responsibility per skill
- Document expected outputs
- Make skills independently usable
- Minimize coupling
- Test compositions

**Result**: Powerful, flexible skill ecosystem that scales from simple tasks to complex multi-phase workflows.

## References

- Skill Examples: `.github/skills/adr-writer/`, `.github/skills/nuget-manager/`
- Composition Example: `.github/skills/domain-add-aggregate/` (uses shared templates pattern)
- agentskills.io Standard: `.github/skills/skill-creator/docs/agent-skills-standard.md`
- Single Responsibility Principle: [Uncle Bob - Clean Code](https://blog.cleancoder.com/uncle-bob/2014/05/08/SingleReponsibilityPrinciple.html)
