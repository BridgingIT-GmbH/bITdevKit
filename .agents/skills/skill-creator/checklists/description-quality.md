# Description Quality Checklist

Use this checklist when writing or refining your skill's `description` field in the YAML frontmatter. The description is critical—it's what helps AI agents and users understand when to invoke your skill.

## Pre-Writing Checklist

Before you write, answer these questions:

- [ ] **What does the skill create/manage/analyze?** (the artifact or object)
- [ ] **How does it do this?** (the approach, tool, or standard)
- [ ] **When should it be used?** (the use case or trigger)
- [ ] **What makes it valuable?** (the key feature or benefit)

**Example**:
- What: Architectural Decision Records
- How: Following MADR format
- When: Documenting important decisions
- Value: Comprehensive alternatives analysis

→ "Creates Architectural Decision Records following MADR format with comprehensive alternatives analysis"

## Core Quality Criteria

### 1. Structure & Format

- [ ] **Starts with action verb**: Creates, Manages, Analyzes, Generates, Validates, etc.
- [ ] **Single line**: No line breaks or newlines
- [ ] **No trailing period**: Description should not end with `.`
- [ ] **Proper capitalization**: First word capitalized, rest lowercase except proper nouns
- [ ] **Valid YAML**: Special characters properly escaped if needed

**CORRECT**:
```yaml
description: Creates Architectural Decision Records following MADR format with alternatives
```

**WRONG**:
```yaml
description: creates architectural decision records.    # WRONG: lowercase start, trailing period
description: Creates ADRs 
following MADR format                                   # WRONG: multi-line
```

### 2. Length & Conciseness

- [ ] **Within range**: 60-120 characters (recommended)
- [ ] **Not too short**: At least 40 characters (avoid vagueness)
- [ ] **Not too long**: At most 150 characters (avoid rambling)
- [ ] **Every word earns its place**: No filler words or redundancy

**Check Length**:
```bash
# Count characters in description
echo "Your description here" | wc -c
```

**CORRECT** (98 characters):
```yaml
description: Manages NuGet packages across .NET projects using dotnet CLI with version validation
```

**WRONG** (19 characters - too short):
```yaml
description: NuGet management
```

**WRONG** (187 characters - too long):
```yaml
description: This comprehensive skill assists developers in the complex process of managing NuGet package dependencies across multiple .NET projects and solutions with built-in version conflict detection
```

### 3. Action Verb Strength

- [ ] **Uses strong verb**: Creates, Manages, Analyzes (NOT "Helps with", "Provides")
- [ ] **Active voice**: Agent does the action (NOT passive "is used to")
- [ ] **Specific verb**: Matches the primary task (Generate for code, Validate for checks)

**Strong Verbs**:
- Creates / Generates (for content/code creation)
- Manages / Updates (for state management)
- Analyzes / Reviews (for examination)
- Validates / Verifies (for quality checks)
- Executes / Runs (for operations)

**Weak Verbs** (avoid):
- Helps with
- Provides support for
- Assists in
- Enables
- Facilitates

**CORRECT**:
```yaml
description: Analyzes code changes for architectural violations and suggests corrections
```

**WRONG**:
```yaml
description: Helps with analyzing code for issues    # WRONG: "Helps with" is weak
```

### 4. Specificity

- [ ] **Names the artifact**: ADRs, aggregates, packages, migrations (NOT "documents", "stuff")
- [ ] **Mentions approach/tool**: MADR format, dotnet CLI, EF Core (NOT just "using tools")
- [ ] **States key feature**: Version detection, rollback support, alternatives analysis
- [ ] **Avoids vague qualifiers**: Specific features, not "better" or "improved"

**CORRECT**:
```yaml
description: Generates integration tests using WebApplicationFactory with realistic data scenarios
```

**WRONG**:
```yaml
description: Creates better tests for your application    # WRONG: vague ("better tests")
```

### 5. Target Audience Clarity

- [ ] **Developer-focused language**: Uses technical terms appropriately
- [ ] **Assumes context**: Can reference project concepts (Clean Architecture, DDD)
- [ ] **Avoids jargon overload**: Balances specificity with readability
- [ ] **Clear to unfamiliar user**: Someone new to project can understand task

**CORRECT**:
```yaml
description: Adds new Domain Aggregates using Clean Architecture with full CRUD scaffolding
```
(Uses "Domain Aggregates", "Clean Architecture", "CRUD" - technical but clear)

**WRONG**:
```yaml
description: Instantiates AR entities via factory pattern with full CQRS ops and bounded context isolation
```
(Too jargon-heavy, unclear task)

## Avoid Anti-Patterns

### Anti-Pattern 1: Redundant Phrases

- [ ] **No "This skill..."**: Description is already in `description` field
- [ ] **No "A tool for..."**: Redundant, starts with weak noun
- [ ] **No "Use this to..."**: Instruction, not description
- [ ] **No "Provides a way to..."**: Wordy, focus on action

**WRONG Examples**:
```yaml
description: This skill creates ADRs              # WRONG: "This skill" is redundant
description: A tool for managing packages         # WRONG: "A tool for" is weak opening
description: Use this to generate tests           # WRONG: "Use this to" is instructional
description: Provides a way to analyze code       # WRONG: "Provides a way to" is wordy
```

**CORRECT**:
```yaml
description: Creates ADRs
description: Manages packages
description: Generates tests
description: Analyzes code
```

### Anti-Pattern 2: Kitchen Sink Descriptions

- [ ] **Describes primary task only**: Not every sub-step
- [ ] **One key feature**: Not listing 5 features
- [ ] **Focused scope**: Not "does everything"

**WRONG**:
```yaml
description: Creates aggregates, adds commands, generates queries, configures EF, sets up mappings, and creates endpoints
```
(Lists 6 tasks - too much)

**CORRECT**:
```yaml
description: Adds new Domain Aggregates using Clean Architecture with full CRUD scaffolding
```
(Describes primary task, implies sub-steps)

### Anti-Pattern 3: Vague Benefits

- [ ] **Concrete features**: Not abstract benefits ("productivity", "quality")
- [ ] **Specific capabilities**: "with version conflict detection" NOT "makes things better"
- [ ] **Measurable outcomes**: "with rollback support" NOT "safer operations"

**WRONG**:
```yaml
description: Manages packages to improve productivity        # WRONG: vague benefit
description: Creates better quality ADRs                     # WRONG: subjective
description: Safer database migration process                # WRONG: abstract
```

**CORRECT**:
```yaml
description: Manages NuGet packages with version conflict detection
description: Creates ADRs following MADR format with alternatives analysis
description: Executes database migrations with rollback support and validation
```

## Testing Your Description

### Test 1: The 5-Second Rule

Show description to someone unfamiliar with the project.

**Ask them (after 5 seconds)**:
1. What does this skill do?
2. When would you use it?

**If they can't answer → Rewrite for clarity**

### Test 2: The Removal Test

Remove all the specific terms, replace with "thing":

```yaml
description: Creates thing using thing with thing
```

**If it still makes grammatical sense → Too vague, add specifics**

### Test 3: The Differentiation Test

Could this description apply to 3 or more different skills?

**If yes → Add differentiating details**

### Test 4: The Character Count Test

Count characters (excluding `description:` prefix):

```bash
echo "Your description here" | wc -c
```

- < 40 chars: Likely too vague
- 40-60 chars: Acceptable for simple skills
- 60-120 chars: Ideal range ✓
- 120-150 chars: Acceptable if necessary
- 150+ chars: Too long, trim

## Common Mistakes & Fixes

### Mistake 1: Too Short

**WRONG** (18 chars):
```yaml
description: Package manager
```

**FIX**: Add what, how, key feature (98 chars):
```yaml
description: Manages NuGet packages across .NET projects using dotnet CLI with version validation
```

### Mistake 2: Too Long

**WRONG** (204 chars):
```yaml
description: This comprehensive skill provides assistance to developers who need to create, manage, update, or remove NuGet package dependencies across their .NET projects and solutions with built-in validation and conflict resolution capabilities
```

**FIX**: Trim to essentials (98 chars):
```yaml
description: Manages NuGet packages across .NET projects using dotnet CLI with version validation
```

### Mistake 3: Weak Verb

**WRONG**:
```yaml
description: Helps with creating domain aggregates
```

**FIX**: Use strong verb:
```yaml
description: Creates Domain Aggregates using Clean Architecture with full CRUD scaffolding
```

### Mistake 4: Vague Scope

**WRONG**:
```yaml
description: Creates documents for the project
```

**FIX**: Be specific about artifact:
```yaml
description: Creates Architectural Decision Records following MADR format with alternatives
```

### Mistake 5: Lists Sub-Tasks

**WRONG**:
```yaml
description: Creates commands, queries, handlers, repositories, and endpoints
```

**FIX**: Describe primary task:
```yaml
description: Scaffolds Application layer features with commands, queries, and handlers
```

## Revision Workflow

### Step 1: Draft Multiple Versions

Write 3-5 different descriptions:

**Version 1**: Simple (minimal)
```yaml
description: Creates ADRs
```

**Version 2**: Adds approach
```yaml
description: Creates ADRs following MADR format
```

**Version 3**: Adds key feature
```yaml
description: Creates ADRs following MADR format with alternatives analysis
```

**Version 4**: Adds context
```yaml
description: Creates Architectural Decision Records following MADR format with comprehensive alternatives analysis
```

### Step 2: Evaluate Each Version

| Version | Length | Clarity | Specificity | Rank |
|---------|--------|---------|-------------|------|
| V1 | 12 chars | Low | Low | ❌ |
| V2 | 36 chars | Medium | Medium | ⚠️ |
| V3 | 63 chars | High | High | ✅ |
| V4 | 117 chars | High | Very High | ✅✅ |

### Step 3: Pick Best or Combine

Choose V4 (most complete) or V3 (good balance).

**Final**:
```yaml
description: Creates Architectural Decision Records following MADR format with comprehensive alternatives analysis
```

## Final Checklist

Before committing description, verify:

- [ ] Starts with strong action verb (no "helps with")
- [ ] 60-120 characters (or 40-150 acceptable range)
- [ ] Specifies artifact/object (ADRs, packages, aggregates)
- [ ] Mentions approach/tool/standard (MADR, dotnet CLI)
- [ ] Includes key feature or value (alternatives analysis, version detection)
- [ ] Single line (no line breaks)
- [ ] No trailing period
- [ ] No redundant phrases ("This skill", "A tool for")
- [ ] Passes 5-second test (unfamiliar user understands)
- [ ] Passes differentiation test (unique description)

## References

- Description Examples: `.github/skills/skill-creator/examples/description-examples.md`
- Frontmatter Guide: `.github/skills/skill-creator/templates/frontmatter-guide.md`
- Existing Skills: `.github/skills/adr-writer/SKILL.md`, `.github/skills/nuget-manager/SKILL.md`
- agentskills.io Standard: [https://agentskills.io](https://agentskills.io)
