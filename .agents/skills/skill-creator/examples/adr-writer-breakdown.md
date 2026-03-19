# ADR Writer Skill - Detailed Breakdown

This document analyzes the `adr-writer` skill from `.github/skills/adr-writer/SKILL.md` to demonstrate effective skill design patterns and best practices.

## Skill Metadata Analysis

```yaml
---
name: adr-writer
description: 'Write high-quality Architectural Decision Records (ADRs) following MADR format. Use when documenting important architectural decisions, technology choices, or cross-cutting concerns. Never use emoji or special characters - use plain words CORRECT for correct and WRONG for wrong.'
---
```

### What Makes This Frontmatter Effective

**Strengths:**

1. **Clear Name**: `adr-writer` is descriptive and action-oriented (writer = creates content)
2. **Comprehensive Description**:
   - States WHAT: Write high-quality ADRs
   - States HOW: Following MADR format
   - States WHEN: Important decisions, technology choices, cross-cutting concerns
   - States CRITICAL RULE: No emoji, use plain words
3. **Self-Documenting**: The description itself teaches the most important rule

**Note:** This skill predates the agentskills.io standard, so it uses `name` instead of `skill` and lacks `globs`/`alwaysApply` fields. If updating to the standard:

```yaml
---
skill: adr-writer
description: Write high-quality Architectural Decision Records (ADRs) following MADR format for important architectural decisions and technology choices
globs:
  - docs/ADR/*.md
  - docs/adr/*.md
alwaysApply: false
---
```

## Structure Analysis

The skill follows a clear 3-level progressive disclosure pattern:

### Level 1: Quick Reference (Lines 6-28)
- Overview paragraph
- Prerequisites list
- Core Rules (5 critical items)

**Purpose**: Gets user started in <30 seconds. Rules are IMMEDIATELY actionable.

**Effective Pattern**: Each core rule uses imperative verbs (NEVER/ALWAYS) for clarity.

### Level 2: Detailed Workflows (Lines 30-301)
- Step-by-step process (13 steps)
- Each step has: clear heading → explanation → code example → output
- Real examples with full context

**Purpose**: Guides user through complete ADR creation process without getting lost.

**Effective Pattern**: 
- Steps are numbered sequentially (1-13)
- Each step is atomic (one clear action)
- Steps build on each other (determine number → create file → write sections)

### Level 3: Reference Material (Lines 303-476)
- Examples (2 complete scenarios)
- Quality checklist
- Common pitfalls (WRONG/CORRECT examples)
- Decision categories
- File structure
- Template summary
- Validation rules
- ASCII character usage
- References

**Purpose**: Deep reference for edge cases, validation, and mastery.

## Core Rules Section Analysis

```markdown
## Core Rules

1. **NEVER** use emoji or special characters in ADRs. Use plain ASCII words:
   - Use `CORRECT:` for correct examples
   - Use `WRONG:` for wrong examples
   - Use `WARNING:` for warnings (not emoji)
2. **ALWAYS** include all required MADR sections (Status, Context, Decision, Rationale, Consequences, Alternatives, References)
3. **NEVER** reference `AGENTS.md` or `.github/copilot-instructions.md` in ADR References section
4. **ALWAYS** use 4-digit numbering (0001, 0002, etc.)
5. **ALWAYS** update Quick Reference table in `docs/ADR/README.md` after creating ADR
```

### Why These Rules Are Effective

1. **Enforceable**: Each rule can be verified programmatically or visually
2. **Specific**: Not "write good ADRs" but "use 4-digit numbering"
3. **Context-Aware**: Rule #3 prevents referencing internal dev docs in user-facing ADRs
4. **Actionable**: Each rule tells you exactly what to do/avoid
5. **Complete**: Cover file naming, content structure, and post-creation tasks

### Pattern to Emulate

When writing Core Rules:
- Use strong imperatives (NEVER/ALWAYS/MUST)
- Make rules binary (yes/no, not subjective)
- Provide concrete examples
- Limit to 5-7 most critical rules (anything more gets ignored)

## Workflow Structure Analysis

The "Creating a New ADR" workflow demonstrates excellent step design:

**Step 1: Determine ADR Number**
```markdown
#### Step 1: Determine ADR Number

Check existing ADRs and use next sequential number:

\`\`\`bash
ls docs/ADR/*.md
# If last is 0012, use 0013
\`\`\`
```

### Why This Works

1. **Clear Action**: "Determine ADR Number" (specific verb)
2. **How to Execute**: Shows exact command
3. **Example**: Comments explain logic ("If last is 0012...")
4. **Minimal**: No extra explanation needed

**Step 3: Follow MADR Structure**
```markdown
#### Step 3: Follow MADR Structure

**Required Sections (in order):**

1. **Status**: Proposed | Accepted | Deprecated | Superseded
2. **Context**: Problem description, forces, constraints, requirements
3. **Decision**: Clear statement of what is being decided
...
```

### Why This Works

1. **Numbered List**: Easy to scan
2. **Format Shown**: "Status: Proposed | Accepted" teaches acceptable values
3. **Brief Explanation**: One phrase per section
4. **Expandable**: Later steps expand each section in detail

### Pattern to Emulate

**Step Template:**
1. Action verb in heading ("Determine", "Create", "Write")
2. Brief explanation (1 sentence)
3. Code example or command
4. Expected output or result

## Examples Section Analysis

```markdown
### User: "Document the decision to use Entity Framework Core"

**Actions**:

1. Determine next number: check `ls docs/ADR/*.md` (assume 0007)
2. Create file: `docs/ADR/0007-entity-framework-core-code-first-migrations.md`
3. Write comprehensive ADR covering:
   - Context: ORM requirements, code-first vs database-first
   - Decision: EF Core with code-first migrations, DbContext per module
   ...
```

### Why This Works

1. **Realistic User Request**: Actual language a user would use
2. **Complete Response**: Shows EXACTLY what agent would do
3. **Step-by-Step**: Numbered actions matching workflow
4. **Specific Details**: Not "create ADR" but "create file with exact name"

### Pattern to Emulate

**Example Template:**
```markdown
### User: "[Exact user request in quotes]"

**Actions**:
1. [First action with specific details]
2. [Second action with exact commands/filenames]
3. [Third action with expected content]
...
```

## Quality Checklist Analysis

```markdown
## Quality Checklist

Before finalizing, verify:

- [ ] Status is "Proposed" or "Accepted"
- [ ] Context explains problem clearly (2-4 paragraphs)
- [ ] Decision is specific and actionable
...
```

### Why This Works

1. **Markdown Checkboxes**: Visually scannable
2. **Binary Checks**: Each item is yes/no (no subjectivity)
3. **Specific Criteria**: "2-4 paragraphs" not "sufficient context"
4. **Complete Coverage**: All critical elements from workflow

### Pattern to Emulate

Make checklists with:
- Concrete, measurable criteria
- No ambiguous terms ("good", "sufficient")
- Quantifiable where possible ("5+ reasons", "2-4 paragraphs")
- Ordered by importance (critical items first)

## Common Pitfalls Section Analysis

```markdown
## Common Pitfalls

**WRONG:**

- Use emoji or special characters (✅❌⚠️) - use CORRECT/WRONG instead
- Reference AGENTS.md or .github/copilot-instructions.md
- Write vague decisions ("use better error handling")
...

**CORRECT:**

- Use plain ASCII: CORRECT for correct, WRONG for wrong
- Write comprehensive context (problem, forces, constraints)
...
```

### Why This Works

1. **Explicit Examples**: Shows actual emoji characters to avoid
2. **Anti-Patterns**: "Write vague decisions" with example
3. **Positive Alternatives**: WRONG section balanced with CORRECT section
4. **Context-Specific**: Pitfalls unique to ADR writing

### Pattern to Emulate

Structure pitfalls as:
1. **WRONG** section: List anti-patterns with examples
2. **CORRECT** section: Show proper alternatives
3. Use parallel structure (same number of items in each)
4. Be concrete (show actual bad examples, not abstract descriptions)

## Resource Organization

The skill has NO subdirectories but could benefit from:

**Potential Structure:**
```
.github/skills/adr-writer/
├── SKILL.md (main file - keep comprehensive)
├── templates/
│   └── adr-template.md (full MADR template)
├── examples/
│   ├── complete-adr-example.md (real ADR showing all sections)
│   └── minimal-adr-example.md (simplest acceptable ADR)
└── checklists/
    ├── pre-writing-checklist.md (research done? alternatives explored?)
    └── post-writing-checklist.md (validation checklist)
```

**Why This Wasn't Done:**
- Skill is already comprehensive in single file (476 lines)
- ADR template exists at `docs/ADR/README.md` (referenced)
- Real examples exist at `docs/ADR/0001-*.md` (referenced)
- Single-file approach reduces cognitive load

**When to Split:**
- If SKILL.md exceeds ~800 lines
- If multiple distinct workflows emerge
- If templates become complex enough to need their own files

## Key Takeaways for Skill Creators

### What adr-writer Does Exceptionally Well

1. **Strong Core Rules**: 5 critical rules that are enforceable and specific
2. **Step-by-Step Workflow**: 13 atomic steps that build sequentially
3. **Concrete Examples**: Shows actual commands, filenames, and content
4. **Honest About Mistakes**: Common Pitfalls section shows real anti-patterns
5. **Quality Gates**: Checklist ensures nothing is forgotten
6. **ASCII Emphasis**: Repeats the no-emoji rule in multiple sections

### What Could Be Improved

1. **Frontmatter**: Update to agentskills.io standard (add `globs`, `alwaysApply`)
2. **Length**: 476 lines is approaching limit for single file
3. **Navigation**: Could benefit from table of contents
4. **Templates**: Could extract full ADR template to separate file

### Patterns to Emulate

- Use imperative verbs in rules (NEVER/ALWAYS)
- Number workflow steps sequentially
- Show exact commands and filenames
- Balance WRONG with CORRECT examples
- Include quality checklist
- Repeat critical rules in multiple places

### Patterns to Avoid

- Don't assume user knows project conventions
- Don't use vague criteria ("write good context")
- Don't hide negative examples (show actual mistakes)
- Don't create subdirectories unnecessarily

## Comparison with Other Skills

### adr-writer vs nuget-manager

| Aspect | adr-writer | nuget-manager |
|--------|-----------|---------------|
| Length | 476 lines | 69 lines |
| Complexity | High (13-step workflow) | Low (3 workflows) |
| Subdirectories | None | None |
| Examples | 2 scenarios | 2 scenarios |
| Checklists | 1 comprehensive | None |
| Core Rules | 5 rules | 3 rules |

**Key Insight**: Skill complexity should match task complexity. ADR writing is inherently more complex than package management, justifying longer, more detailed skill.

## Usage in Practice

When invoked, the agent should:

1. **Read Core Rules** first (lines 19-28)
2. **Follow Workflow** (lines 30-301) step by step
3. **Reference Checklist** before finalizing (lines 303-319)
4. **Check Pitfalls** if unsure (lines 321-342)

The skill enables the agent to:
- Create ADRs without asking user for structure
- Enforce project conventions (4-digit numbering, no emoji)
- Avoid common mistakes (referencing AGENTS.md)
- Produce consistent, high-quality output

## Conclusion

The `adr-writer` skill demonstrates mature skill design with:
- Clear, enforceable rules
- Comprehensive step-by-step guidance
- Concrete examples and anti-patterns
- Quality gates (checklists)
- Appropriate complexity for the task

It serves as an excellent reference for creating workflow-oriented skills that guide agents through complex, multi-step processes.
