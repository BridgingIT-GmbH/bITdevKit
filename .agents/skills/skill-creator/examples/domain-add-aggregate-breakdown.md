# Domain Add Aggregate Skill - Preview Breakdown

This document previews the design of the `domain-add-aggregate` skill, which will be the most complex skill in the repository. It demonstrates patterns for large, multi-layer workflow skills.

## Skill Classification

**Type**: Workflow Skill (Large, Multi-Layer)
**Complexity**: High (17 templates, 4 examples, 8 checklists, 4 docs = 34 files total)
**Target**: Adding complete Domain Aggregates with full CRUD operations following Clean Architecture and DDD

**Comparison**:
- `adr-writer`: 476 lines, 1 file (medium complexity)
- `nuget-manager`: 69 lines, 1 file (low complexity)
- `domain-add-aggregate`: ~800-1000 lines, 34 files (high complexity)

## Why This Skill Requires 34 Files

### Justification for Large Structure

Unlike `adr-writer` (single workflow) or `nuget-manager` (3 simple workflows), domain aggregate creation:

1. **Spans 5 Layers**: Domain → Application → Infrastructure → Mapping → Presentation
2. **Has 17 Distinct Templates**: Each layer needs multiple file types (aggregate, commands, queries, handlers, configs, endpoints)
3. **Requires Layer Coordination**: Changes in Domain ripple through all other layers
4. **Enforces Architecture**: Must maintain strict layer boundaries (no leakage)
5. **Follows Complex Patterns**: Result pattern, factory methods, domain events, specifications, etc.

**Pattern Insight**: When a skill guides multi-layer code generation, subdirectories become essential for organization.

## Projected Structure

```
.github/skills/domain-add-aggregate/
├── SKILL.md                                    # Main workflow (plan-first approach)
├── templates/                                   # 17 C# code templates
│   ├── aggregate-template.cs
│   ├── value-object-template.cs
│   ├── enumeration-template.cs
│   ├── domain-events-template.cs              # Created/Updated/Deleted events
│   ├── command-create-template.cs
│   ├── command-create-handler-template.cs
│   ├── command-update-template.cs
│   ├── command-update-handler-template.cs
│   ├── command-delete-template.cs
│   ├── command-delete-handler-template.cs
│   ├── query-findone-template.cs
│   ├── query-findone-handler-template.cs
│   ├── query-findall-template.cs
│   ├── query-findall-handler-template.cs
│   ├── model-template.cs                      # DTO
│   ├── ef-configuration-template.cs
│   ├── mapper-register-template.cs
│   └── endpoint-template.cs
├── examples/                                   # 4 example files
│   ├── customer-complete-walkthrough.md       # Real Customer aggregate analysis
│   ├── value-object-patterns.md               # EmailAddress, Address patterns
│   ├── mapping-patterns.md                    # Mapster configurations
│   └── result-chaining-patterns.md            # .Ensure().Bind().Tap() examples
├── checklists/                                 # 8 checkpoint files
│   ├── 01-domain-layer.md                     # Aggregate, value objects, events
│   ├── 02-infrastructure-layer.md             # EF config, migrations
│   ├── 03-application-layer.md                # Commands, queries, handlers
│   ├── 04-mapping-layer.md                    # Mapster registrations
│   ├── 05-presentation-layer.md               # Endpoints
│   ├── 06-tests.md                            # Unit/integration tests (optional)
│   ├── build-checkpoints.md                   # Compile after each layer
│   └── quality-gates.md                       # Architecture compliance, naming
└── docs/                                       # 4 reference docs
    ├── architecture-overview.md               # Layer boundaries, dependencies
    ├── naming-conventions.md                  # File naming, class naming
    ├── common-pitfalls.md                     # Layer leakage, mapping mistakes
    └── devkit-references.md                   # bITdevKit base classes, helpers
```

## Main SKILL.md Structure (Projected)

### Section Breakdown

**Level 1: Quick Reference (Lines 1-150)**
- Overview & Purpose
- Prerequisites (knowledge of DDD, Clean Architecture, bITdevKit)
- Core Rules (10 rules):
  1. ALWAYS plan before coding (create checklist)
  2. ALWAYS work layer-by-layer (Domain → Infrastructure → Application → Mapping → Presentation)
  3. NEVER reference outer layers from inner layers (e.g., Domain can't reference Application)
  4. ALWAYS use factory methods returning `Result<T>` (no public constructors)
  5. ALWAYS use private setters on properties
  6. ALWAYS register domain events in factory/change methods
  7. ALWAYS compile after each layer (incremental validation)
  8. ALWAYS use Mapster for domain ↔ DTO mapping (no manual mapping)
  9. ALWAYS use typed entity IDs (`[TypedEntityId<Guid>]`)
  10. NEVER skip EF configuration (fluent API required)
- When to Use This Skill
- When NOT to Use This Skill

**Level 2: Main Workflow (Lines 150-650)**

**Phase 1: Planning (30 lines)**
- Step 1: Gather Requirements
- Step 2: Identify Properties & Value Objects
- Step 3: Design State Transitions
- Step 4: Create Todo Checklist

**Phase 2: Domain Layer (100 lines)**
- Step 5: Create Aggregate Root
- Step 6: Create Value Objects
- Step 7: Create Enumeration (if applicable)
- Step 8: Create Domain Events (Created, Updated, Deleted)
- Step 9: Compile & Verify

**Phase 3: Infrastructure Layer (80 lines)**
- Step 10: Create EF Core Configuration
- Step 11: Add DbSet to Module DbContext
- Step 12: Generate Migration
- Step 13: Compile & Verify

**Phase 4: Application Layer (150 lines)**
- Step 14: Create Command (Create)
- Step 15: Create Command Handler (Create)
- Step 16: Create Command (Update)
- Step 17: Create Command Handler (Update)
- Step 18: Create Command (Delete)
- Step 19: Create Command Handler (Delete)
- Step 20: Create Query (FindOne)
- Step 21: Create Query Handler (FindOne)
- Step 22: Create Query (FindAll)
- Step 23: Create Query Handler (FindAll)
- Step 24: Compile & Verify

**Phase 5: Mapping Layer (60 lines)**
- Step 25: Create DTO Model
- Step 26: Update Mapper Register (domain ↔ model)
- Step 27: Update Mapper Register (value objects)
- Step 28: Compile & Verify

**Phase 6: Presentation Layer (80 lines)**
- Step 29: Create Minimal API Endpoints (POST, GET, PUT, DELETE)
- Step 30: Register Endpoints in Module
- Step 31: Compile & Verify

**Phase 7: Validation (50 lines)**
- Step 32: Run Build
- Step 33: Run Tests (if applicable)
- Step 34: Verify API with Swagger
- Step 35: Mark Complete

**Level 3: Reference Material (Lines 650-1000)**
- Examples Section (2 scenarios: Simple aggregate, Complex aggregate with value objects)
- Quality Checklist (25 items covering all layers)
- Common Pitfalls (15 anti-patterns with corrections)
- Architecture Guidelines (layer boundaries diagram)
- Template Reference (brief description of each of 17 templates)
- File Naming Conventions
- Testing Guidance
- References (bITdevKit docs, project ADRs)

**Total: ~1000 lines** (manageable for main file)

## Why Subdirectories Are Essential

### Templates Directory (17 Files)

**Purpose**: Provide copy-paste-ready C# code with inline comments explaining patterns.

**Why Separate Files?**
1. Each template is 50-200 lines of C# code
2. Including all 17 in SKILL.md would make it ~3000+ lines (unmanageable)
3. Agents can read specific templates as needed (lazy loading)
4. Templates are reusable across different aggregates

**Pattern**: Each template:
- Has inline comments explaining "why" and "pattern"
- Uses placeholder markers: `[Entity]`, `[Module]`, `[Property]`
- Shows real bITdevKit base classes (e.g., `AuditableAggregateRoot`)
- Demonstrates Result pattern chaining (`.Ensure().Bind().Tap()`)

### Examples Directory (4 Files)

**Purpose**: Show real implementations from the codebase (Customer aggregate).

**Why Separate Files?**
1. `customer-complete-walkthrough.md`: Full Customer.cs analysis with annotations (500+ lines)
2. `value-object-patterns.md`: EmailAddress, Address patterns (200 lines)
3. `mapping-patterns.md`: Mapster configurations with explanations (150 lines)
4. `result-chaining-patterns.md`: Result pattern usage (150 lines)

**Total: ~1000 lines** if included in SKILL.md would make it bloated.

### Checklists Directory (8 Files)

**Purpose**: Provide layer-specific validation checklists.

**Why Separate Files?**
1. Each checklist focuses on one layer (10-20 items)
2. Agent can reference specific checklist after completing a layer
3. User can track progress per layer
4. Reduces cognitive load (don't show all 100+ items at once)

**Pattern**: Each checklist:
- Has layer-specific items (e.g., "Domain properties have private setters")
- Includes file path examples
- References specific templates
- Provides "How to Fix" guidance

### Docs Directory (4 Files)

**Purpose**: Provide architectural context and reference material.

**Why Separate Files?**
1. `architecture-overview.md`: Explains layer boundaries (referenced multiple times in workflow)
2. `naming-conventions.md`: Centralized naming reference (reduces repetition in SKILL.md)
3. `common-pitfalls.md`: Anti-patterns too numerous to list in main file
4. `devkit-references.md`: Lists bITdevKit base classes, helpers (saves repetition)

**Pattern**: Docs are reference material, not workflow. Separate them from procedural steps.

## Progressive Disclosure Strategy

### How Agent Loads the Skill

**Step 1: Read SKILL.md (Overview + Core Rules)**
- Understands task scope
- Knows prerequisites
- Learns core rules (layer boundaries, factory methods, etc.)

**Step 2: Follow Workflow (Phases 1-7)**
- Executes steps sequentially
- References templates as needed (lazy loading)
- Checks layer-specific checklists after each phase

**Step 3: Consult Reference Material (As Needed)**
- Looks up naming conventions when creating files
- Checks common pitfalls if encountering issues
- References examples for complex patterns

**Key Benefit**: Agent doesn't need to load all 34 files upfront. It loads ~1-3 files at a time.

## Comparison: When to Use Single File vs Subdirectories

### Use Single File When:
- Total content < 500 lines
- Single linear workflow
- Few or no code templates
- Minimal reference material
- Examples can be shown inline (< 20 lines each)

**Example**: `nuget-manager` (69 lines)

### Use Subdirectories When:
- Total content > 800 lines
- Multiple distinct artifacts (templates, examples, checklists, docs)
- Code templates are substantial (> 50 lines each)
- Workflow spans multiple layers/phases
- Reference material is extensive

**Example**: `domain-add-aggregate` (~1000 lines main + 1500 lines subdirectories)

### Pattern Insight

**Rule of Thumb**:
- 1-500 lines → Single file
- 500-800 lines → Single file with anchors/TOC
- 800+ lines → Single file (SKILL.md) + subdirectories

**Directory Threshold**:
- 0-2 templates → Inline in SKILL.md
- 3-5 templates → Consider subdirectory
- 6+ templates → Definitely use subdirectory

## Key Differences from Existing Skills

| Aspect | adr-writer | nuget-manager | domain-add-aggregate |
|--------|-----------|---------------|---------------------|
| Complexity | Medium | Low | High |
| Files | 1 | 1 | 34 |
| Lines (total) | 476 | 69 | ~2500 |
| Layers | 1 (docs) | 1 (packages) | 5 (Domain, App, Infra, Map, Present) |
| Templates | 0 | 0 | 17 |
| Examples | 2 inline | 2 inline | 4 files |
| Checklists | 1 inline | 0 | 8 files |
| Workflow Steps | 13 | 4 | 35 |
| Incremental Validation | No | Yes (restore) | Yes (compile after each layer) |

**Key Insight**: As complexity grows, structure must scale proportionally.

## Expected Agent Behavior

### User Request: "Add a Product aggregate to the Catalog module"

**Agent Actions:**

1. **Planning Phase**: 
   - Read `SKILL.md` overview and core rules
   - Ask user: "What properties should Product have?"
   - Create todo checklist with 35 steps

2. **Domain Layer**:
   - Read `templates/aggregate-template.cs`
   - Read `templates/value-object-template.cs` (for Price?)
   - Read `templates/domain-events-template.cs`
   - Create files in `src/Modules/CatalogModule/CatalogModule.Domain/Model/ProductAggregate/`
   - Run `dotnet build` to verify
   - Reference `checklists/01-domain-layer.md` to validate

3. **Infrastructure Layer**:
   - Read `templates/ef-configuration-template.cs`
   - Create EF configuration
   - Add DbSet to CatalogModuleDbContext
   - Generate migration
   - Run `dotnet build`
   - Reference `checklists/02-infrastructure-layer.md`

4. **Application Layer**:
   - Read command/query templates (10 files)
   - Create commands, queries, handlers
   - Run `dotnet build`
   - Reference `checklists/03-application-layer.md`

5. **Mapping Layer**:
   - Read `templates/model-template.cs`
   - Read `templates/mapper-register-template.cs`
   - Read `examples/mapping-patterns.md` (if complex mapping needed)
   - Update mapper
   - Run `dotnet build`
   - Reference `checklists/04-mapping-layer.md`

6. **Presentation Layer**:
   - Read `templates/endpoint-template.cs`
   - Create endpoints
   - Register in module
   - Run `dotnet build`
   - Reference `checklists/05-presentation-layer.md`

7. **Final Validation**:
   - Reference `checklists/quality-gates.md`
   - Verify all files follow naming conventions
   - Confirm no layer boundary violations
   - Test endpoints via Swagger

**Total Files Loaded**: ~15-20 of 34 (agent only loads what it needs)

## Lessons for Large Skills

### Design Principles

1. **Progressive Disclosure**: Main SKILL.md is entry point; subdirectories are on-demand.
2. **Layered Checklists**: Validate incrementally (after each layer), not at the end.
3. **Lazy Loading**: Agent reads templates only when needed.
4. **Template-Driven**: Don't describe code patterns in prose; show actual code templates.
5. **Example-Driven**: Don't explain architecture abstractly; show real Customer aggregate.

### What Makes a Large Skill Successful

1. **Clear Workflow**: 35 steps sound overwhelming, but grouped into 7 phases makes it manageable.
2. **Build Checkpoints**: Compile after each layer prevents cascading errors.
3. **Layer Isolation**: Checklists enforce boundaries (no Infrastructure in Domain).
4. **Template Quality**: Each template must be production-ready (not pseudocode).
5. **Real Examples**: Customer aggregate analysis teaches by showing, not telling.

### Common Mistakes to Avoid

1. **Dumping Everything in SKILL.md**: Would create 3000-line monster file.
2. **Too Many Subdirectories**: More than 4-5 subdirectories adds cognitive load.
3. **Generic Templates**: Placeholders like `[PROPERTY_HERE]` without examples are useless.
4. **Abstract Examples**: Foo/Bar examples don't teach real patterns.
5. **No Validation**: Without checkpoints, user creates 50 files then discovers mistakes.

## Conclusion

The `domain-add-aggregate` skill will demonstrate **large-scale skill design** with:
- Main workflow in SKILL.md (~1000 lines)
- 34 supporting files organized in 4 subdirectories
- Progressive disclosure (lazy loading)
- Incremental validation (compile after each layer)
- Template-driven code generation
- Real-world examples (Customer aggregate)

**Key Principle**: As workflow complexity grows, skill structure must scale proportionally. A 35-step multi-layer workflow cannot fit comfortably in a single file—and shouldn't.

**Takeaway for Skill Creators**: Don't fight complexity. Embrace subdirectories when task justifies it. The alternative (cramming everything into one file) hurts usability more than having organized subdirectories.
