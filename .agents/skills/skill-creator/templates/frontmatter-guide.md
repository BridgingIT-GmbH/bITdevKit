# YAML Frontmatter Reference for Agent Skills

This document provides a comprehensive guide to the YAML frontmatter structure used in Agent Skills `SKILL.md` files.

## Required Structure

Every `SKILL.md` file MUST begin with YAML frontmatter delimited by `---`:

```yaml
---
name: skill-name
description: Brief one-line description of what the skill does
---
```

## Field Definitions

### `name` (Required)
- **Type**: String
- **Format**: Lowercase with hyphens (kebab-case)
- **Purpose**: Unique identifier for the skill
- **Rules**:
  - Must match the directory name
  - Use descriptive, action-oriented names
  - Avoid abbreviations unless universally understood

**CORRECT Examples:**
```yaml
name: adr-writer
name: nuget-manager
name: domain-add-aggregate
name: skill-creator
```

**WRONG Examples:**
```yaml
name: ADRWriter          # WRONG: Not kebab-case
name: write_adr          # WRONG: Use hyphens, not underscores
name: adr                # WRONG: Too vague
name: aggregate          # WRONG: Missing action verb
```

### `description` (Required)
- **Type**: String (single line, no line breaks)
- **Length**: 60-120 characters recommended
- **Purpose**: Shown in skill selection UI; helps AI decide when to invoke
- **Rules**:
  - Start with action verb (e.g., "Creates", "Manages", "Analyzes")
  - Be specific about WHAT the skill does
  - Include WHY it's beneficial (optional but recommended)
  - Avoid generic phrases like "helps with" or "assists in"
  - No trailing period

**CORRECT Examples:**
```yaml
description: Creates Architectural Decision Records following project conventions and best practices
description: Manages NuGet package dependencies across the solution with version conflict detection
description: Adds new Domain Aggregates using Clean Architecture and DDD principles with full CRUD scaffolding
description: Guides creation of high-quality Agent Skills following agentskills.io open standard
```

**WRONG Examples:**
```yaml
description: Helps with ADRs                                    # WRONG: Too vague
description: A tool for writing documentation                   # WRONG: Not specific
description: Creates ADRs.                                      # WRONG: Has trailing period
description: This skill will help you create Architectural
Decision Records for your project                               # WRONG: Multi-line
description: NuGet stuff                                        # WRONG: Unprofessional, vague
```

## Optional Fields

### `globs` (Optional - VS Code Specific)
- **Type**: Array of strings
- **Purpose**: File patterns that trigger automatic skill relevance
- **Format**: Standard glob patterns (e.g., `*.md`, `src/**/*.cs`)
- **Rules**:
  - Use empty array `[]` if skill should NOT auto-trigger based on files
  - Use specific patterns to avoid over-triggering
  - Consider both read and write operations
  - Path separators should use forward slashes `/`

**CORRECT Examples:**
```yaml
# ADR writer - triggers when working with ADR files
globs:
  - docs/ADR/*.md
  - docs/adr/*.md

# NuGet manager - triggers when working with project files
globs:
  - "*.csproj"
  - "*.slnx"
  - Directory.Build.props
  - Directory.Packages.props

# Domain aggregate - triggers when working with domain layer
globs:
  - src/Modules/*/Domain/**/*.cs
  - src/**/Domain/Model/**/*.cs

# Skill creator - empty because it's meta-level
globs: []
```

**WRONG Examples:**
```yaml
globs:
  - "**/*"                          # WRONG: Too broad, triggers on everything

globs:
  - docs\ADR\*.md                   # WRONG: Use forward slashes, not backslashes

globs:
  - *.cs                            # WRONG: Too broad for domain-specific skill

```

### `alwaysApply` (Optional - VS Code Specific)
- **Type**: Boolean
- **Default**: `false`
- **Purpose**: Controls whether skill is always considered relevant (VS Code only, not part of standard)
- **Note**: This field is VS Code-specific and not part of the agentskills.io standard
- **Rules**:
  - Omit entirely if not needed
  - Set to `true` ONLY if skill should be globally available
  - Most skills should be `false` (context-triggered)
  - Use `true` sparingly to avoid cognitive overload

**When to Use `true`:**
- Meta-skills (like skill-creator)
- Cross-cutting concerns (logging, error handling)
- General-purpose utilities

**When to Use `false`:**
- Domain-specific operations (most skills)
- Context-dependent workflows
- File-type-specific operations

**CORRECT Examples:**
```yaml
# Meta-skill that could be needed anytime
name: skill-creator
alwaysApply: true

# Domain-specific workflow (only relevant when working with domain code)
name: domain-add-aggregate
alwaysApply: false

# File-specific operation
name: adr-writer
alwaysApply: false
```

## Complete Example Templates

### Minimal Skill (Simple Tool)
```yaml
---
name: code-formatter
description: Formats code files according to .editorconfig rules with validation
---
```

### Workflow Skill (Multi-Step Process)
```yaml
---
name: feature-scaffolder
description: Scaffolds complete feature implementation with tests, docs, and migrations using vertical slice architecture
---
```

### Meta-Skill (Always Available)
```yaml
---
name: architecture-advisor
description: Analyzes code changes for architectural violations and suggests improvements based on project ADRs
---
```

### Documentation Skill (Specific File Type)
```yaml
---
name: api-doc-generator
description: Generates API documentation from OpenAPI spec with examples and authentication flows
---
```

## Common Patterns (VS Code Specific)

> **Note**: These patterns use `globs` and `alwaysApply` fields which are VS Code-specific extensions. For maximum portability, rely on the `description` field to trigger skill relevance.

### Pattern 1: Single File Type
When skill operates on one type of file (VS Code):
```yaml
globs:
  - "*.csproj"
```

### Pattern 2: Multiple Related Files
When skill operates on related file types (VS Code):
```yaml
globs:
  - "*.csproj"
  - "*.slnx"
  - Directory.*.props
```

### Pattern 3: Directory-Scoped
When skill operates on files in specific directories (VS Code):
```yaml
globs:
  - docs/ADR/*.md
  - src/Modules/*/Domain/**/*.cs
```

### Pattern 4: Always Available
When skill is globally relevant (VS Code):
```yaml
alwaysApply: true
```

## Validation Checklist

Before finalizing frontmatter, verify:

**Required Fields:**
- [ ] `name` matches directory name exactly
- [ ] `name` uses kebab-case (lowercase with hyphens)
- [ ] `description` is a single line (no line breaks)
- [ ] `description` includes what it does AND when to use it
- [ ] `description` is 50-1024 characters
- [ ] `description` has no trailing period

**Optional Fields (VS Code Specific):**
- [ ] `globs` patterns use forward slashes `/` (if used)
- [ ] `globs` are specific enough to avoid over-triggering (if used)
- [ ] `alwaysApply: true` is justified and not overused (if used)

**General:**
- [ ] YAML syntax is valid (proper indentation, quotes for special chars)

## Testing Your Frontmatter

After creating frontmatter, test it by:

1. **Syntax Validation**: Ensure YAML parses correctly
   ```bash
   # Use any YAML validator or parser
   cat SKILL.md | head -n 10 | yaml-validator
   ```

2. **Context Triggering**: Verify skill activates in correct contexts
   - Open files matching `globs` patterns
   - Confirm skill appears in suggestions
   - Check skill doesn't appear when irrelevant

3. **Description Clarity**: Show description to someone unfamiliar
   - Can they understand what it does?
   - Would they know when to use it?

## References

- [agentskills.io Open Standard](https://agentskills.io)
- [VS Code Copilot Agent Skills Documentation](https://code.visualstudio.com/docs/copilot/customization/agent-skills)
- [YAML Specification](https://yaml.org/spec/)
- Project example: `.github/skills/adr-writer/SKILL.md`
- Project example: `.github/skills/nuget-manager/SKILL.md`
