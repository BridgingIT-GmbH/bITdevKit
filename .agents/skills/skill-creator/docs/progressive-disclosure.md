# Progressive Disclosure in Agent Skills

This document explains the **progressive disclosure** design pattern for Agent Skills—a strategy that presents information in layers so users and agents can efficiently navigate from quick start to deep expertise.

## What is Progressive Disclosure?

**Progressive Disclosure** is a UX principle where:
- Complex information is hidden until needed
- Users start with essentials, access details on-demand
- Cognitive load is minimized by showing only relevant information

**In Agent Skills Context**:
- Level 1: Quick orientation (< 30 seconds)
- Level 2: Guided execution (during workflow)
- Level 3: Deep reference (for edge cases)

## Why Progressive Disclosure Matters

### Problem: Information Overload

**Without Progressive Disclosure**:
```markdown
SKILL.md (5000 lines)
├── Frontmatter
├── Overview
├── 50 pages of templates
├── 30 pages of examples
├── 100-item checklist
├── Troubleshooting (every possible error)
└── References
```

**User Experience**: Overwhelmed, can't find starting point, abandons skill.

**With Progressive Disclosure**:
```markdown
SKILL.md (800 lines)
├── Frontmatter
├── Overview (2 paragraphs)
├── Prerequisites (5 items)
├── Core Rules (5 critical items)
├── Workflow (20 steps with inline examples)
├── References to templates/ (load on-demand)
└── References to examples/ (load on-demand)
```

**User Experience**: Clear entry point, immediate progress, access details when needed.

## Three-Level Strategy

### Level 1: Quick Reference (Always Visible)

**Location**: Top of SKILL.md
**Length**: 100-300 lines
**Purpose**: Orientation and immediate action

**Contents**:
- [ ] Overview (2-4 sentences)
- [ ] Prerequisites (3-7 items)
- [ ] Core Rules (3-7 critical constraints)
- [ ] When to Use / When NOT to Use

**Goal**: User understands scope in < 30 seconds and can start workflow immediately.

**Example (adr-writer)**:
```markdown
## Overview
This skill helps you write comprehensive Architectural Decision Records (ADRs) 
following the MADR format. It enforces consistent structure, thoroughness, 
and adherence to project conventions.

## Prerequisites
- Understanding of the problem/decision to document
- Research completed on alternatives and tradeoffs
- Team discussion completed (if applicable)
- Clear recommendation ready

## Core Rules
1. **NEVER** use emoji or special characters in ADRs
2. **ALWAYS** include all required MADR sections
3. **NEVER** reference AGENTS.md in ADR References
4. **ALWAYS** use 4-digit numbering (0001, 0002)
5. **ALWAYS** update Quick Reference table after creating ADR
```

**Why This Works**:
- User knows exactly what skill does (Overview)
- User knows if they're ready (Prerequisites)
- User knows critical constraints (Core Rules)
- Total: ~20 lines, < 30 seconds to read

### Level 2: Guided Workflow (Loaded During Execution)

**Location**: Middle of SKILL.md
**Length**: 400-700 lines
**Purpose**: Step-by-step execution

**Contents**:
- [ ] Sequentially numbered steps
- [ ] Inline code examples (< 20 lines each)
- [ ] Expected outputs
- [ ] Decision points
- [ ] References to templates/examples (lazy loading)

**Goal**: User completes task without leaving main file, loads resources only when needed.

**Example Structure**:
```markdown
## Workflows

### Creating a New ADR

#### Step 1: Determine ADR Number
Check existing ADRs and use next sequential number:
\`\`\`bash
ls docs/ADR/*.md
# If last is 0012, use 0013
\`\`\`

#### Step 2: Create File
Filename format: `docs/ADR/XXXX-decision-title.md`
Example: `docs/ADR/0013-graphql-api-strategy.md`

#### Step 3: Write Context Section
Include problem statement, requirements, challenges.
[See template: templates/adr-context-template.md]

...
```

**Why This Works**:
- Steps are atomic (one clear action per step)
- Inline examples show expected format
- References to templates are optional (load if needed)
- User makes progress without jumping to 10 different files

### Level 3: Deep Reference (Loaded On-Demand)

**Location**: Subdirectories (templates/, examples/, checklists/, docs/)
**Length**: 100-500 lines per file
**Purpose**: Edge cases, advanced patterns, validation

**Contents**:
- [ ] Complete code templates (templates/)
- [ ] Extended examples with annotations (examples/)
- [ ] Layer-specific checklists (checklists/)
- [ ] Conceptual documentation (docs/)

**Goal**: User solves complex scenarios without bloating main file.

**Example Organization**:
```
.github/skills/domain-add-aggregate/
├── SKILL.md (Levels 1 & 2: 800 lines)
├── templates/ (Level 3: load when creating file)
│   ├── aggregate-template.cs (150 lines)
│   ├── command-template.cs (100 lines)
│   └── ...
├── examples/ (Level 3: load when pattern is unclear)
│   ├── customer-complete-walkthrough.md (500 lines)
│   └── ...
├── checklists/ (Level 3: load after completing phase)
│   ├── 01-domain-layer.md (50 items)
│   └── ...
└── docs/ (Level 3: load when needing context)
    ├── architecture-overview.md (200 lines)
    └── ...
```

**Why This Works**:
- Main file stays manageable (800 lines vs 3000+ if everything inline)
- Resources loaded only when relevant
- Agent can lazy-load specific files (doesn't need all 34 files upfront)

## Implementation Patterns

### Pattern 1: Inline Examples for Simple Cases

**When**: Example is < 20 lines, universally applicable

**Example**:
```markdown
#### Step 5: Add Package Reference

Use dotnet CLI to add package:
\`\`\`bash
dotnet add src/MyProject/MyProject.csproj package Serilog --version 3.0.1
\`\`\`

Expected output:
\`\`\`
info : Adding PackageReference for package 'Serilog' into project...
\`\`\`
```

**Why**: Simple, self-contained, doesn't warrant separate file.

### Pattern 2: Reference External Templates for Complex Cases

**When**: Template is 50+ lines, reusable across steps

**Example**:
```markdown
#### Step 10: Create Aggregate Class

Create aggregate following this structure:
- Private parameterless constructor (for EF Core)
- Private parameterized constructor (for factory)
- Factory method returning Result<T>
- Change methods using Change() builder

[Full template: templates/aggregate-template.cs]
```

**Why**: Template is too long to inline, user loads only if creating aggregate.

### Pattern 3: Link to Examples for Clarification

**When**: User might need to see complete scenario

**Example**:
```markdown
#### Step 15: Configure Mapster Mappings

Add mapping configuration in module's MapperRegister class.

**Value Object Conversion**:
\`\`\`csharp
config.NewConfig<EmailAddress, string>()
    .MapWith(src => src.Value);
\`\`\`

For complex mappings (nested value objects, collections), see:
[examples/mapping-patterns.md]
```

**Why**: Shows common case inline, links to examples for advanced cases.

### Pattern 4: Checklists After Major Milestones

**When**: Workflow has distinct phases, each needs validation

**Example**:
```markdown
#### Step 12: Compile & Verify Domain Layer

Run build to ensure domain layer compiles:
\`\`\`bash
dotnet build src/Modules/CoreModule/CoreModule.Domain/
\`\`\`

Verify domain layer quality:
[checklists/01-domain-layer.md]

- [ ] Aggregate inherits from AuditableAggregateRoot
- [ ] Factory method returns Result<T>
- [ ] Properties have private setters
...
```

**Why**: Phase-specific validation without cluttering main workflow.

### Pattern 5: Reference Docs for Conceptual Context

**When**: User needs architectural understanding, not procedural steps

**Example**:
```markdown
#### Important: Layer Boundaries

Domain layer must NOT reference:
- Application layer
- Infrastructure layer
- Presentation layer

[See: docs/architecture-overview.md] for detailed dependency rules.
```

**Why**: Architectural concepts don't fit in procedural workflow, separate doc provides deep context.

## Agent Behavior with Progressive Disclosure

### Ideal Agent Flow

1. **Agent reads SKILL.md (Level 1)**:
   - Parses frontmatter (skill, description, globs)
   - Reads Overview, Prerequisites, Core Rules
   - Understands task scope and critical constraints

2. **Agent follows Workflow (Level 2)**:
   - Executes Step 1, Step 2, Step 3...
   - Uses inline examples for straightforward steps
   - Encounters reference: `[See template: templates/aggregate-template.cs]`

3. **Agent lazy-loads template (Level 3)**:
   - Reads templates/aggregate-template.cs
   - Uses template to generate code
   - Returns to SKILL.md workflow

4. **Agent validates using checklist (Level 3)**:
   - After completing phase, reads checklists/01-domain-layer.md
   - Verifies all checklist items
   - Proceeds to next phase

**Key Benefit**: Agent loads ~3-5 files total, not all 34 upfront.

### Anti-Pattern: Load Everything Upfront

**WRONG Approach**:
```python
# Agent implementation (anti-pattern)
def invoke_skill(skill_name):
    skill_content = load_all_files(f".github/skills/{skill_name}/")
    # Loads SKILL.md + all templates + all examples + all checklists
    # Result: 10,000+ lines in context, agent is overwhelmed
```

**CORRECT Approach**:
```python
# Agent implementation (progressive)
def invoke_skill(skill_name):
    skill_md = load_file(f".github/skills/{skill_name}/SKILL.md")
    # Loads only main file initially
    
    # During execution, when encountering [template: X]:
    if "[template:" in current_step:
        template_path = extract_template_path(current_step)
        template_content = load_file(template_path)
        # Loads template on-demand
```

## Measuring Progressive Disclosure Effectiveness

### Metrics

**Time to First Action**:
- Goal: User/agent can start workflow in < 30 seconds
- Measure: Time from opening SKILL.md to executing Step 1

**Files Loaded During Execution**:
- Goal: Agent loads < 20% of total files for typical task
- Measure: Count of file reads during skill execution

**Workflow Completion Rate**:
- Goal: > 90% of users complete workflow without abandoning
- Measure: Track completions vs abandonments

### Success Criteria

**Level 1 (Quick Reference)**:
- [ ] New user understands scope in < 30 seconds
- [ ] Core Rules fit on one screen (no scrolling)
- [ ] Prerequisites are clear and verifiable

**Level 2 (Workflow)**:
- [ ] User can follow 80% of steps without leaving main file
- [ ] Inline examples are sufficient for common cases
- [ ] References to external resources are clearly marked

**Level 3 (Deep Reference)**:
- [ ] Templates are loaded only when creating files
- [ ] Examples are loaded only when pattern is unclear
- [ ] Checklists are loaded only after completing phases

## Common Mistakes

### Mistake 1: Everything in SKILL.md

**WRONG**:
```markdown
SKILL.md (3000 lines)
├── Overview
├── 10 inline templates (150 lines each)
├── 5 complete examples (200 lines each)
├── 100-item checklist
└── Troubleshooting (every error ever)
```

**Problem**: User is overwhelmed, can't find starting point.

**FIX**: Extract templates to templates/, examples to examples/, checklists to checklists/.

### Mistake 2: Everything in Subdirectories

**WRONG**:
```markdown
SKILL.md (100 lines)
└── "See workflow.md for steps"

workflow.md
└── "See step-1.md, step-2.md, step-3.md..."

step-1.md
└── "See template-1.md for template"
```

**Problem**: User must navigate 10 files just to understand task.

**FIX**: Keep workflow in SKILL.md, extract only substantial resources.

### Mistake 3: No Inline Examples

**WRONG**:
```markdown
#### Step 5: Add Package Reference

Use dotnet CLI to add package.
[See: examples/add-package-example.md]
```

**Problem**: User must leave main file for trivial example.

**FIX**: Inline simple examples (< 20 lines), extract only complex ones.

### Mistake 4: Vague References

**WRONG**:
```markdown
For more information, see the templates folder.
```

**Problem**: User doesn't know which template to read.

**CORRECT**:
```markdown
[See: templates/aggregate-template.cs]
```

**Why**: Specific file path, user knows exactly where to go.

## Checklist: Is Your Skill Progressively Disclosed?

### Level 1 Checklist

- [ ] Overview is 2-4 sentences (not 2 pages)
- [ ] Prerequisites are 3-7 items (not 20)
- [ ] Core Rules are 3-7 items (not 15)
- [ ] Level 1 content fits in ~100-300 lines
- [ ] User can understand scope in < 30 seconds

### Level 2 Checklist

- [ ] Workflow has clear sequential steps
- [ ] Inline examples are < 20 lines each
- [ ] Complex templates referenced, not inlined
- [ ] Decision points are explicit
- [ ] Level 2 content is 400-700 lines

### Level 3 Checklist

- [ ] Templates extracted to templates/ (if 3+)
- [ ] Examples extracted to examples/ (if 100+ lines each)
- [ ] Checklists extracted to checklists/ (if 20+ items)
- [ ] Docs extracted to docs/ (if substantial reference material)
- [ ] Each resource is referenced explicitly from SKILL.md

### Navigation Checklist

- [ ] User can find any resource in < 30 seconds
- [ ] References use specific paths (not "see folder")
- [ ] No circular references (A → B → A)
- [ ] Main workflow never leaves SKILL.md for navigation

## Conclusion

Progressive Disclosure is essential for usable Agent Skills:
- **Level 1**: Quick orientation (< 30 seconds)
- **Level 2**: Guided execution (stay in main file)
- **Level 3**: Deep reference (load on-demand)

**Key Principle**: Show only what's needed, when it's needed.

**Result**: Skills that are easy to start, complete, and master—without overwhelming users or agents.

## References

- Skill Examples: `.github/skills/adr-writer/` (Level 1 & 2 only)
- Skill Examples: `.github/skills/domain-add-aggregate/` (all 3 levels - coming soon)
- Resource Organization: `.github/skills/skill-creator/checklists/resource-organization.md`
- UX Principle: [Progressive Disclosure (Nielsen Norman Group)](https://www.nngroup.com/articles/progressive-disclosure/)
