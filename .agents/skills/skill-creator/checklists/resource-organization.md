# Resource Organization Checklist

Use this checklist to determine when and how to organize your skill using subdirectories. Proper organization improves maintainability and usability through progressive disclosure.

## Decision Framework: Single File vs Subdirectories

### Use Single File When

- [ ] **Total content < 500 lines**: Skill fits comfortably in one file
- [ ] **0-2 templates**: Templates can be shown inline (< 20 lines each)
- [ ] **Single linear workflow**: One primary workflow, no complex branches
- [ ] **Minimal reference material**: Examples and pitfalls fit in main file
- [ ] **Simple skill scope**: Tool wrapper or straightforward process

**Example**: `nuget-manager` (69 lines total)

### Use Subdirectories When

- [ ] **Total content > 800 lines**: Would make single file unwieldy
- [ ] **3+ substantial templates**: Each template is 50+ lines of code
- [ ] **Multi-layer workflow**: Spans multiple architectural layers or phases
- [ ] **Extensive examples**: Examples are 100+ lines each
- [ ] **Complex reference material**: Multiple conceptual docs needed

**Example**: `domain-add-aggregate` (~2500 lines across 34 files)

## Standard Directory Structure

### Recommended Organization

```
.github/skills/[skill-name]/
├── SKILL.md                  # Main entry point (required)
├── templates/                # Code templates (optional)
│   └── *.cs, *.md, etc.
├── examples/                 # Extended examples (optional)
│   └── *.md
├── checklists/               # Validation checklists (optional)
│   └── *.md
└── docs/                     # Reference documentation (optional)
    └── *.md
```

### When to Use Each Subdirectory

**templates/**
- [ ] Have 3+ code/file templates
- [ ] Each template is 50+ lines
- [ ] Templates are reusable (not skill-specific examples)
- [ ] Inline inclusion would bloat main file

**examples/**
- [ ] Have 2+ extended examples
- [ ] Each example is 100+ lines
- [ ] Examples show complete scenarios (not code snippets)
- [ ] Examples benefit from detailed annotations

**checklists/**
- [ ] Have 20+ checklist items
- [ ] Workflow has distinct phases (domain → infrastructure → application)
- [ ] Each phase needs separate validation
- [ ] Layer-specific checklists improve usability

**docs/**
- [ ] Have extensive reference material (architecture, conventions)
- [ ] Need 3+ conceptual documents
- [ ] Documents are referenced multiple times in workflow
- [ ] Separating docs reduces duplication in main file

## Detailed Checklist by Subdirectory

### Templates Directory

#### When to Create

- [ ] **Count templates**: 3 or more templates needed
- [ ] **Assess template size**: Each template 50+ lines
- [ ] **Check reusability**: Templates used across multiple workflow steps
- [ ] **Verify complexity**: Templates have substantial inline comments/logic

#### What to Include

- [ ] **Code templates**: Actual compilable/runnable code
- [ ] **File structure templates**: Directory layouts, config files
- [ ] **Inline comments**: Explain "why" and "pattern", not just "what"
- [ ] **Placeholder markers**: Consistent format (`[Entity]`, `[Module]`)
- [ ] **Complete examples**: Templates should be production-ready

#### What to Avoid

- [ ] **Example code**: Specific instances (those go in `examples/`)
- [ ] **Pseudocode**: Templates should be real code
- [ ] **One-off snippets**: Small snippets stay in SKILL.md
- [ ] **Project-specific code**: Templates should be generalizable

#### Naming Conventions

```
templates/
├── [artifact]-template.[ext]           # Generic template
├── [layer]-[artifact]-template.[ext]   # Layer-specific template
└── [operation]-[artifact]-template.[ext] # Operation-specific template
```

**Examples**:
- `aggregate-template.cs`
- `command-create-template.cs`
- `ef-configuration-template.cs`
- `endpoint-template.cs`

### Examples Directory

#### When to Create

- [ ] **Count examples**: 3 or more extended examples
- [ ] **Assess example length**: Each example 100+ lines
- [ ] **Check detail level**: Examples have extensive annotations
- [ ] **Verify value**: Examples show complete, realistic scenarios

#### What to Include

- [ ] **Complete walkthroughs**: End-to-end scenario with all steps
- [ ] **Real code analysis**: Breakdown of actual project code
- [ ] **Pattern demonstrations**: Show specific patterns in context
- [ ] **Before/after comparisons**: Show transformations
- [ ] **Annotated code**: Comments explaining decisions

#### What to Avoid

- [ ] **Code snippets**: Short snippets stay in SKILL.md
- [ ] **Generic Foo/Bar**: Use realistic domain examples
- [ ] **Incomplete examples**: Examples should be self-contained
- [ ] **Duplicated templates**: Don't repeat template content

#### Naming Conventions

```
examples/
├── [domain]-complete-walkthrough.md     # Full scenario
├── [pattern]-patterns.md                # Pattern collection
├── [concept]-examples.md                # Concept demonstrations
└── [scenario]-breakdown.md              # Detailed analysis
```

**Examples**:
- `customer-complete-walkthrough.md`
- `value-object-patterns.md`
- `mapping-patterns.md`
- `result-chaining-patterns.md`

### Checklists Directory

#### When to Create

- [ ] **Count items**: Main checklist has 20+ items
- [ ] **Assess phases**: Workflow has 3+ distinct phases
- [ ] **Check complexity**: Each phase has 5+ validation points
- [ ] **Verify separation benefit**: Layer-specific checklists improve usability

#### What to Include

- [ ] **Phase-specific checklists**: One per major workflow phase
- [ ] **Binary verification items**: Each item is yes/no
- [ ] **File path references**: Examples of where to check
- [ ] **"How to Fix" guidance**: Brief instructions for failures
- [ ] **Build checkpoints**: Compile/test steps after each phase

#### What to Avoid

- [ ] **Subjective criteria**: Avoid "good", "sufficient", "quality"
- [ ] **Duplicated items**: Don't repeat across checklists
- [ ] **Instructional content**: Checklists verify, don't teach
- [ ] **Overly granular**: Each item should be significant

#### Naming Conventions

```
checklists/
├── [NN]-[phase]-layer.md                # Layer-specific
├── [concept]-checklist.md               # Concept validation
├── build-checkpoints.md                 # Compilation checks
└── quality-gates.md                     # Overall quality
```

**Examples**:
- `01-domain-layer.md`
- `02-infrastructure-layer.md`
- `03-application-layer.md`
- `build-checkpoints.md`
- `quality-gates.md`

### Docs Directory

#### When to Create

- [ ] **Count documents**: 3+ substantial reference docs
- [ ] **Assess total size**: Reference material is 500+ lines
- [ ] **Check references**: Documents referenced multiple times in workflow
- [ ] **Verify value**: Separating docs reduces duplication

#### What to Include

- [ ] **Architecture overviews**: Layer boundaries, dependencies
- [ ] **Naming conventions**: Centralized naming reference
- [ ] **Common pitfalls**: Anti-patterns and corrections
- [ ] **Framework references**: bITdevKit, libraries, base classes
- [ ] **Conceptual guides**: Progressive disclosure patterns

#### What to Avoid

- [ ] **Workflow steps**: Those belong in SKILL.md
- [ ] **Code templates**: Those belong in `templates/`
- [ ] **Examples**: Those belong in `examples/`
- [ ] **External docs**: Link instead of duplicating

#### Naming Conventions

```
docs/
├── architecture-overview.md             # System architecture
├── [concept]-guide.md                   # Conceptual guidance
├── naming-conventions.md                # Naming standards
├── common-pitfalls.md                   # Anti-patterns
└── [framework]-references.md            # External framework docs
```

**Examples**:
- `architecture-overview.md`
- `naming-conventions.md`
- `common-pitfalls.md`
- `devkit-references.md`

## File Count Guidelines

### By Skill Complexity

| Complexity | Total Files | Structure |
|------------|-------------|-----------|
| **Simple** | 1 | SKILL.md only |
| **Medium** | 1-5 | SKILL.md + maybe one subdirectory |
| **Complex** | 6-20 | SKILL.md + 2-3 subdirectories |
| **Very Complex** | 20-40 | SKILL.md + all 4 subdirectories |

### Warning Thresholds

- [ ] **> 40 files total**: Consider splitting into multiple skills
- [ ] **> 5 subdirectory levels**: Too deep, flatten structure
- [ ] **> 20 files in one subdirectory**: Create sub-subdirectories or split skill

## Progressive Disclosure Strategy

### Level 1: SKILL.md (Always Load First)

User/agent reads:
- [ ] Overview (what skill does)
- [ ] Prerequisites (what's needed)
- [ ] Core Rules (critical constraints)
- [ ] Workflow outline (high-level steps)

**Goal**: Understand scope and start workflow in < 60 seconds

### Level 2: Workflow Details (Load During Execution)

User/agent reads:
- [ ] Step-by-step procedures
- [ ] Inline code examples (< 20 lines each)
- [ ] Decision points
- [ ] References to templates/examples (lazy loading)

**Goal**: Execute workflow step-by-step without leaving main file

### Level 3: Supporting Resources (Load On-Demand)

User/agent reads:
- [ ] Specific template when creating file
- [ ] Specific example when pattern is unclear
- [ ] Specific checklist after completing phase
- [ ] Specific doc when encountering edge case

**Goal**: Get deep context only when needed

## Organization Anti-Patterns

### Anti-Pattern 1: Premature Subdirectories

**WRONG**: Creating subdirectories for simple skill (< 300 lines)

**Symptoms**:
- Subdirectories with 1-2 files
- Each file < 50 lines
- User must navigate multiple files for simple task

**FIX**: Consolidate into single SKILL.md

### Anti-Pattern 2: Dumping Everything in SKILL.md

**WRONG**: 3000-line SKILL.md with 17 templates inline

**Symptoms**:
- SKILL.md > 1000 lines
- Multiple 100+ line code blocks
- Hard to navigate or scan

**FIX**: Extract templates to `templates/`, examples to `examples/`

### Anti-Pattern 3: Unclear File Purposes

**WRONG**: Files named `stuff.md`, `notes.md`, `misc.md`

**Symptoms**:
- Vague filenames
- Mixed content types in one file
- No clear organization principle

**FIX**: Use descriptive names following conventions above

### Anti-Pattern 4: Deep Nesting

**WRONG**: `.github/skills/foo/templates/commands/create/v2/template.cs`

**Symptoms**:
- 5+ directory levels
- Hard to reference from SKILL.md
- Confusing navigation

**FIX**: Flatten to 2-3 levels max

## Maintenance Checklist

### When Adding New Content

- [ ] **Assess size**: Does new content push SKILL.md over 800 lines?
- [ ] **Check type**: Is it a template, example, checklist, or doc?
- [ ] **Follow convention**: Use established naming pattern
- [ ] **Update references**: Add links from SKILL.md to new file
- [ ] **Test navigation**: Verify user can find new content easily

### When Reorganizing

- [ ] **Document reason**: Why is reorganization needed?
- [ ] **Update all links**: Fix references in SKILL.md and other files
- [ ] **Test after change**: Verify no broken links
- [ ] **Communicate change**: Update changelog or commit message
- [ ] **Consider versioning**: Major reorganizations may warrant version bump

## Quick Reference: Decision Matrix

```
Q: How many templates?
   0-2 → Inline in SKILL.md
   3-5 → Consider templates/
   6+  → Definitely templates/

Q: How many examples?
   0-2 (< 50 lines each) → Inline in SKILL.md
   2-3 (50-100 lines) → Consider examples/
   3+ (100+ lines) → Definitely examples/

Q: How many checklist items?
   < 20 → Inline in SKILL.md
   20-40 → Consider checklists/ (split by phase)
   40+ → Definitely checklists/ (one per phase)

Q: How much reference material?
   < 200 lines → Inline in SKILL.md
   200-500 lines → Consider docs/
   500+ lines → Definitely docs/
```

## Final Validation

Before finalizing organization:

- [ ] **Navigation test**: Can user find any resource in < 30 seconds?
- [ ] **Lazy loading test**: Can agent load only what it needs?
- [ ] **Maintenance test**: Is it clear where new content should go?
- [ ] **Naming test**: Are all filenames descriptive and consistent?
- [ ] **Link test**: Are all internal references working?
- [ ] **Size test**: Is SKILL.md < 1000 lines?

## References

- Skill Examples: `.github/skills/adr-writer/`, `.github/skills/nuget-manager/`
- Skill Creator Examples: `.github/skills/skill-creator/examples/`
- Domain Aggregate Preview: `.github/skills/skill-creator/examples/domain-add-aggregate-breakdown.md`
