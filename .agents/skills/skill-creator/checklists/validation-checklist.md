# Pre-Publication Validation Checklist

Use this comprehensive checklist before publishing or committing your Agent Skill. This is your final quality gate—ensure all items pass before making the skill available.

## Section 1: Frontmatter Validation

### YAML Syntax

- [ ] **Valid YAML**: Parses without errors (test with YAML validator)
- [ ] **Proper indentation**: Uses spaces (not tabs), consistent 2-space indents
- [ ] **Delimiter present**: Frontmatter starts and ends with `---`
- [ ] **No trailing spaces**: Lines don't have unnecessary whitespace
- [ ] **Special chars escaped**: Quotes, colons in values are properly escaped

**Test Command**:
```bash
# Extract and validate YAML
head -n 10 SKILL.md | yaml-validator
# Or use online validator: https://www.yamllint.com/
```

### Required Fields

- [ ] **skill**: Field is present
- [ ] **description**: Field is present
- [ ] **globs**: Field is present (even if empty array)
- [ ] **alwaysApply**: Field is present (explicitly `true` or `false`)

**CORRECT**:
```yaml
---
skill: example-skill
description: Example skill for demonstration purposes
globs: []
alwaysApply: false
---
```

### Field: skill

- [ ] **Matches directory name**: `skill: foo` matches directory `.github/skills/foo/`
- [ ] **kebab-case format**: Uses lowercase with hyphens (not camelCase, snake_case, or spaces)
- [ ] **Descriptive name**: Name indicates purpose (action + object)
- [ ] **No file extension**: Not `foo.md` or `foo-skill`
- [ ] **Unique within project**: No duplicate skill names

**CORRECT**: `skill: domain-add-aggregate`
**WRONG**: `skill: DomainAddAggregate` (PascalCase), `skill: domain_add` (snake_case), `skill: addAggregate` (camelCase)

### Field: description

- [ ] **Length**: 60-120 characters (or 40-150 acceptable range)
- [ ] **Starts with action verb**: Creates, Manages, Analyzes, etc. (not "Helps", "Provides")
- [ ] **Single line**: No line breaks
- [ ] **No trailing period**: Description doesn't end with `.`
- [ ] **Specific**: Names artifact, approach, and key feature
- [ ] **No redundant phrases**: Doesn't start with "This skill" or "A tool for"

**Test**:
```bash
# Count characters (exclude 'description: ' prefix)
echo "Your description here" | wc -c
```

### Field: globs

- [ ] **Valid array**: Uses JSON array syntax `[]` even if empty
- [ ] **Appropriate patterns**: File patterns match skill purpose
- [ ] **Forward slashes**: Uses `/` not `\` in paths
- [ ] **Specific enough**: Patterns don't over-trigger (avoid `**/*`)
- [ ] **Empty if always-apply**: If `alwaysApply: true`, globs is typically `[]`

**CORRECT**:
```yaml
globs:
  - "*.csproj"
  - "Directory.*.props"
```

**WRONG**:
```yaml
globs: "*.cs"             # WRONG: Not an array
globs:
  - src\**\*.cs           # WRONG: Backslashes
globs:
  - "**/*"                # WRONG: Too broad
```

### Field: alwaysApply

- [ ] **Boolean value**: Set to `true` or `false` (not "yes"/"no" or 1/0)
- [ ] **Justified if true**: If `true`, skill is genuinely globally relevant
- [ ] **False for most skills**: Domain-specific skills should be `false`
- [ ] **Consistent with globs**: If `true`, globs is usually `[]`

**Decision Matrix**:
- Meta-skill (skill-creator) → `alwaysApply: true`
- Domain-specific (domain-add-aggregate) → `alwaysApply: false`
- File-specific (adr-writer) → `alwaysApply: false`

## Section 2: Content Quality

### Overview Section

- [ ] **Present**: SKILL.md has ## Overview heading
- [ ] **Concise**: 2-4 sentences (150-300 words max)
- [ ] **Explains purpose**: What problem does skill solve?
- [ ] **States scope**: What's included / excluded?
- [ ] **Sets expectations**: Complexity level, time estimate?

### Prerequisites Section

- [ ] **Present**: SKILL.md has ## Prerequisites heading
- [ ] **Lists tools**: Required CLIs, SDKs, frameworks
- [ ] **Lists knowledge**: Concepts user must understand
- [ ] **Lists setup**: Environment configuration needed
- [ ] **Verifiable**: Each prerequisite can be checked/installed
- [ ] **Not assumed**: Don't assume "obvious" prerequisites

**CORRECT**:
```markdown
## Prerequisites

- .NET 10 SDK installed
- Understanding of Domain-Driven Design (DDD) concepts
- Familiarity with Clean Architecture layer boundaries
- Project uses bITdevKit framework
```

### Core Rules Section

- [ ] **Present**: SKILL.md has ## Core Rules heading
- [ ] **3-7 rules**: Not too few (< 3) or too many (> 10)
- [ ] **Imperative voice**: Uses NEVER, ALWAYS, MUST (enforceable)
- [ ] **Specific**: Rules are concrete, not vague
- [ ] **Enforceable**: Each rule can be verified
- [ ] **Actionable**: User knows exactly what to do/avoid
- [ ] **Critical only**: Rules cover most important constraints

**CORRECT**:
```markdown
1. **NEVER** reference outer layers from inner layers
2. **ALWAYS** use factory methods returning `Result<T>`
3. **MUST** compile after each layer
```

**WRONG**:
```markdown
1. Write good code                    # WRONG: Vague, subjective
2. Try to follow conventions          # WRONG: Not imperative
3. Consider using patterns            # WRONG: Not enforceable
```

### Workflow Section

- [ ] **Present**: SKILL.md has workflow (## Workflows or step-by-step sections)
- [ ] **Sequential steps**: Steps numbered or clearly ordered
- [ ] **Atomic steps**: Each step is one clear action
- [ ] **Actionable**: Each step tells user what to do
- [ ] **Complete**: Workflow covers task end-to-end
- [ ] **Examples included**: Each step has code example or command
- [ ] **Expected outputs**: User knows what success looks like

**Step Quality**:
- [ ] Clear heading (#### Step N: Action Verb + Object)
- [ ] Brief explanation (1-2 sentences)
- [ ] Code example or command
- [ ] Expected output or result

### Examples Section

- [ ] **Present**: SKILL.md has ## Examples heading
- [ ] **2+ scenarios**: At least one simple, one complex
- [ ] **Realistic user language**: "Add X to Y", not technical jargon
- [ ] **Complete responses**: Show all agent actions
- [ ] **Step references**: Map to workflow steps
- [ ] **Actual values**: Real filenames, versions, not placeholders

**CORRECT**:
```markdown
### User: "Add Serilog to the WebApi project"
**Action**: Execute `dotnet add src/WebApi/WebApi.csproj package Serilog`.
```

**WRONG**:
```markdown
### Example 1
Action: Add package
```

### Quality Checklist Section (Meta)

- [ ] **Present**: SKILL.md has ## Quality Checklist or similar
- [ ] **Comprehensive**: Covers all major workflow steps
- [ ] **Binary items**: Each item is yes/no, not subjective
- [ ] **Markdown checkboxes**: Uses `- [ ]` format
- [ ] **Specific criteria**: "5+ reasons" not "sufficient rationale"

### Common Pitfalls Section

- [ ] **Present**: SKILL.md has ## Common Pitfalls or similar
- [ ] **Balanced**: WRONG section paired with CORRECT section
- [ ] **Concrete examples**: Shows actual anti-patterns, not abstract
- [ ] **Parallel structure**: Same number of items in WRONG and CORRECT
- [ ] **Uses plain ASCII**: "WRONG" and "CORRECT", not emoji

**CORRECT**:
```markdown
**WRONG:**
- Use emoji (✅❌) in ADRs

**CORRECT:**
- Use plain ASCII (CORRECT/WRONG) in ADRs
```

## Section 3: Templates Validation (if applicable)

### Template Quality

- [ ] **Real code**: Actual compilable/runnable code, not pseudocode
- [ ] **Inline comments**: Explains "why" and "pattern", not just "what"
- [ ] **Placeholder markers**: Consistent format (`[Entity]`, `[Module]`, `[Property]`)
- [ ] **Complete**: Templates are production-ready
- [ ] **Generalizable**: Not overly specific to one project

### Template Organization

- [ ] **Proper directory**: Templates in `templates/` subdirectory (if 3+)
- [ ] **Naming convention**: `[artifact]-template.[ext]` format
- [ ] **Referenced**: SKILL.md references templates in workflow
- [ ] **Documented**: Templates have header comments explaining purpose

## Section 4: Examples Validation (if applicable)

### Example Quality

- [ ] **Complete scenarios**: End-to-end walkthroughs
- [ ] **Real code**: Based on actual project code
- [ ] **Annotated**: Comments explaining decisions
- [ ] **Realistic domain**: Uses real domain concepts (Customer, Product), not Foo/Bar

### Example Organization

- [ ] **Proper directory**: Examples in `examples/` subdirectory (if 3+ or 100+ lines each)
- [ ] **Naming convention**: `[domain]-[type].md` format
- [ ] **Referenced**: SKILL.md references examples in workflow
- [ ] **Self-contained**: Each example can be understood independently

## Section 5: Checklists Validation (if applicable)

### Checklist Quality

- [ ] **Binary items**: Each item is yes/no
- [ ] **Specific criteria**: Quantifiable where possible
- [ ] **File path examples**: Shows where to check
- [ ] **"How to Fix" guidance**: Brief instructions for failures

### Checklist Organization

- [ ] **Proper directory**: Checklists in `checklists/` subdirectory (if 20+ items or multi-phase)
- [ ] **Naming convention**: `[NN]-[phase].md` or `[concept]-checklist.md`
- [ ] **Referenced**: SKILL.md references checklists after phases
- [ ] **Phase alignment**: One checklist per workflow phase

## Section 6: Documentation Validation (if applicable)

### Doc Quality

- [ ] **Conceptual**: Explains architecture, patterns, conventions
- [ ] **Referenced multiple times**: Used throughout workflow
- [ ] **Reduces duplication**: Centralizes repeated information
- [ ] **Clear structure**: Headings, lists, tables for scannability

### Doc Organization

- [ ] **Proper directory**: Docs in `docs/` subdirectory (if 3+ docs or 500+ lines)
- [ ] **Naming convention**: `[concept]-guide.md` or `[topic].md`
- [ ] **Referenced**: SKILL.md links to docs when relevant
- [ ] **No duplication**: Doesn't repeat SKILL.md workflow steps

## Section 7: Technical Validation

### File Structure

- [ ] **Directory name matches skill**: `.github/skills/[skill-name]/`
- [ ] **SKILL.md present**: Main file exists and is named correctly
- [ ] **Subdirectories appropriate**: Only created when justified (see resource-organization.md)
- [ ] **No extraneous files**: No temp files, `.DS_Store`, etc.
- [ ] **Proper file extensions**: `.md` for markdown, `.cs` for C#, etc.

### Links & References

- [ ] **Internal links work**: All links to other files in skill directory resolve
- [ ] **External links work**: All URLs return HTTP 200 (not 404)
- [ ] **Anchor links work**: Section anchors (`#heading`) resolve correctly
- [ ] **File paths valid**: Referenced files/directories exist
- [ ] **No broken images**: All images load correctly

**Test Command**:
```bash
# Check for broken links (requires markdown-link-check)
markdown-link-check .github/skills/[skill-name]/**/*.md
```

### Markdown Quality

- [ ] **Valid markdown**: Parses without errors
- [ ] **Consistent heading levels**: No skipped levels (H1 → H3)
- [ ] **Code blocks have language**: \`\`\`yaml, \`\`\`bash, \`\`\`csharp
- [ ] **Lists formatted correctly**: Consistent bullet/number style
- [ ] **Tables formatted**: Proper alignment, headers present

**Test Command**:
```bash
# Validate markdown (requires markdownlint)
markdownlint .github/skills/[skill-name]/**/*.md
```

### Code Quality (in templates/examples)

- [ ] **Syntax valid**: Code compiles/runs
- [ ] **Follows project conventions**: Matches `.editorconfig`, coding standards
- [ ] **Proper formatting**: Indentation, spacing consistent
- [ ] **Comments helpful**: Explains "why", not obvious "what"
- [ ] **No sensitive data**: No passwords, API keys, secrets

## Section 8: Usability Testing

### Navigation Test

- [ ] **Find in < 30 seconds**: User can locate any resource quickly
- [ ] **Clear entry point**: SKILL.md is obvious starting point
- [ ] **Breadcrumbs**: User knows where they are in workflow
- [ ] **Cross-references**: Related sections are linked

### Comprehension Test

- [ ] **5-second test**: Unfamiliar user understands purpose in 5 seconds
- [ ] **15-minute test**: New user can start workflow in 15 minutes
- [ ] **Complete task test**: User can complete task without external help

### Agent Test

- [ ] **Agent understands trigger**: Agent knows when to invoke skill
- [ ] **Agent follows workflow**: Agent executes steps sequentially
- [ ] **Agent produces output**: Agent creates expected files/changes
- [ ] **Agent validates**: Agent checks quality before finishing

**Manual Test**:
```
Prompt: "[User request matching skill purpose]"
Expected: Agent invokes skill and completes task successfully
```

## Section 9: Standards Compliance

### agentskills.io Standard

- [ ] **Uses 'skill' field**: Not 'name' or other
- [ ] **Has 'description' field**: Descriptive, action-oriented
- [ ] **Has 'globs' field**: File patterns defined (or empty array)
- [ ] **Has 'alwaysApply' field**: Boolean value set

**Reference**: [agentskills.io](https://agentskills.io)

### Project Conventions

- [ ] **Matches existing skills**: Style consistent with adr-writer, nuget-manager
- [ ] **Uses plain ASCII**: "CORRECT"/"WRONG", no emoji
- [ ] **Self-contained**: Doesn't reference chatmodes or internal dev docs
- [ ] **Portable**: Works across similar projects
- [ ] **Professional tone**: Technical, concise, helpful

### VS Code Compatibility

- [ ] **Markdown renders**: Displays correctly in VS Code preview
- [ ] **Links clickable**: Internal links navigate correctly in VS Code
- [ ] **Code blocks highlight**: Syntax highlighting works

## Section 10: Final Checks

### Spelling & Grammar

- [ ] **No typos**: Run spell checker
- [ ] **Grammar correct**: Sentences are complete and clear
- [ ] **Consistent terminology**: Same concept uses same terms throughout
- [ ] **Capitalization consistent**: Proper nouns capitalized correctly

**Test Command**:
```bash
# Spell check (requires aspell or similar)
aspell check SKILL.md
```

### Formatting

- [ ] **Consistent style**: Headings, lists, code blocks follow pattern
- [ ] **Proper whitespace**: Blank lines between sections
- [ ] **Line length**: Lines < 200 characters (for readability)
- [ ] **No trailing spaces**: Lines don't end with spaces

### Version Control

- [ ] **Git tracked**: All files added to git
- [ ] **No ignored files**: Important files not in `.gitignore`
- [ ] **Meaningful commit**: Commit message describes skill
- [ ] **Clean diff**: No unrelated changes in commit

**Commit Message Format**:
```
Add [skill-name] agent skill

- [Brief description of what skill does]
- [Key features or scope]
```

### Documentation Update

- [ ] **AGENTS.md updated**: Skill listed in Skills section (if project has this)
- [ ] **README updated**: Skill mentioned in relevant sections (if applicable)
- [ ] **Changelog updated**: Skill addition noted (if project maintains changelog)

## Quick Validation Script

```bash
#!/bin/bash
# Quick validation for agent skill

SKILL_DIR=".github/skills/$1"

if [ -z "$1" ]; then
  echo "Usage: validate-skill.sh <skill-name>"
  exit 1
fi

echo "Validating skill: $1"
echo ""

# Check directory exists
[ -d "$SKILL_DIR" ] && echo "✓ Directory exists" || echo "✗ Directory missing"

# Check SKILL.md exists
[ -f "$SKILL_DIR/SKILL.md" ] && echo "✓ SKILL.md exists" || echo "✗ SKILL.md missing"

# Check frontmatter
head -n 10 "$SKILL_DIR/SKILL.md" | grep -q "^skill:" && echo "✓ skill field present" || echo "✗ skill field missing"
head -n 10 "$SKILL_DIR/SKILL.md" | grep -q "^description:" && echo "✓ description field present" || echo "✗ description field missing"
head -n 10 "$SKILL_DIR/SKILL.md" | grep -q "^globs:" && echo "✓ globs field present" || echo "✗ globs field missing"
head -n 10 "$SKILL_DIR/SKILL.md" | grep -q "^alwaysApply:" && echo "✓ alwaysApply field present" || echo "✗ alwaysApply field missing"

# Check core sections
grep -q "## Overview" "$SKILL_DIR/SKILL.md" && echo "✓ Overview section present" || echo "⚠ Overview section missing"
grep -q "## Prerequisites" "$SKILL_DIR/SKILL.md" && echo "✓ Prerequisites section present" || echo "⚠ Prerequisites section missing"
grep -q "## Core Rules" "$SKILL_DIR/SKILL.md" && echo "✓ Core Rules section present" || echo "⚠ Core Rules section missing"
grep -q "## Examples" "$SKILL_DIR/SKILL.md" && echo "✓ Examples section present" || echo "⚠ Examples section missing"

echo ""
echo "Manual checks required:"
echo "  - Description quality (60-120 chars, starts with verb)"
echo "  - Core Rules (3-7 rules, enforceable)"
echo "  - Workflow completeness"
echo "  - Example realism"
echo "  - Link validity"
```

## Publication Checklist Summary

Before publishing, ALL items must pass:

- [ ] **Frontmatter**: Valid YAML, all required fields, correct values
- [ ] **Content**: Overview, Prerequisites, Core Rules, Workflow, Examples, Checklist, Pitfalls
- [ ] **Templates**: Real code, documented, referenced (if applicable)
- [ ] **Examples**: Complete, realistic, annotated (if applicable)
- [ ] **Checklists**: Binary, specific, phase-aligned (if applicable)
- [ ] **Docs**: Conceptual, reduces duplication (if applicable)
- [ ] **Technical**: Valid markdown, working links, proper structure
- [ ] **Usability**: Tested with agent, user can navigate and complete task
- [ ] **Standards**: agentskills.io compliant, project conventions followed
- [ ] **Final**: No typos, consistent formatting, version control ready

## Post-Publication

After publishing:

- [ ] **Monitor usage**: Track skill invocations
- [ ] **Collect feedback**: Gather user/agent reports
- [ ] **Document issues**: Create backlog for improvements
- [ ] **Plan updates**: Schedule reviews for accuracy
- [ ] **Communicate availability**: Announce skill to team

## References

- Skill Creation Workflow: `.github/skills/skill-creator/checklists/skill-creation-workflow.md`
- Description Quality: `.github/skills/skill-creator/checklists/description-quality.md`
- Resource Organization: `.github/skills/skill-creator/checklists/resource-organization.md`
- Existing Skills: `.github/skills/adr-writer/`, `.github/skills/nuget-manager/`
- agentskills.io: [https://agentskills.io](https://agentskills.io)
