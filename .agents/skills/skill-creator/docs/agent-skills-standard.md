# Agent Skills Open Standard Summary

This document summarizes the agentskills.io open standard for creating Agent Skills that work across VS Code, GitHub Copilot CLI, and other AI coding agents.

## What is the Agent Skills Standard?

The **agentskills.io** open standard defines a portable format for packaging AI agent workflows into reusable skills. Skills are self-contained markdown files with structured frontmatter that agents can discover, understand, and execute.

**Official Website**: [https://agentskills.io](https://agentskills.io)
**VS Code Documentation**: [Agent Skills](https://code.visualstudio.com/docs/copilot/customization/agent-skills)

## Core Concepts

### 1. Self-Contained Workflows

Skills encapsulate complete workflows for specific tasks:
- Creating documents (ADRs, changelogs)
- Managing dependencies (NuGet packages, npm modules)
- Scaffolding code (aggregates, endpoints, tests)
- Analyzing code (architecture violations, security)

### 2. Discoverable via Metadata

Skills use YAML frontmatter so agents can:
- Understand purpose (`description` field)
- Know when to activate (`globs` file patterns)
- Decide relevance (`alwaysApply` boolean)

### 3. Portable Across Agents

Skills work with:
- VS Code Copilot (primary support)
- GitHub Copilot CLI (command-line)
- Future AI coding agents (standard is open)

## YAML Frontmatter Specification

### Required Fields

```yaml
---
skill: skill-name
description: Brief description of what skill does
globs: []
alwaysApply: false
---
```

#### `skill` (Required)

- **Type**: String
- **Format**: kebab-case (lowercase-with-hyphens)
- **Purpose**: Unique identifier for skill
- **Must**: Match directory name exactly

**Examples**:
```yaml
skill: adr-writer
skill: nuget-manager
skill: domain-add-aggregate
```

#### `description` (Required)

- **Type**: String (single line)
- **Length**: 60-120 characters (recommended)
- **Purpose**: Helps agent decide when to invoke skill
- **Format**: Starts with action verb, describes WHAT/HOW/WHEN

**Examples**:
```yaml
description: Creates Architectural Decision Records following MADR format with alternatives analysis
description: Manages NuGet packages across .NET projects using dotnet CLI with version validation
description: Adds new Domain Aggregates using Clean Architecture with full CRUD scaffolding
```

#### `globs` (Required)

- **Type**: Array of strings (glob patterns)
- **Purpose**: File patterns that trigger skill relevance
- **Format**: Standard glob syntax (`*.ext`, `dir/**/*.ext`)
- **Can be empty**: Use `[]` if skill doesn't auto-trigger

**Examples**:
```yaml
# Trigger on ADR files
globs:
  - docs/ADR/*.md
  - docs/adr/*.md

# Trigger on .NET project files
globs:
  - "*.csproj"
  - "*.slnx"
  - Directory.*.props

# No auto-trigger (meta-skill)
globs: []
```

#### `alwaysApply` (Required)

- **Type**: Boolean
- **Values**: `true` or `false`
- **Purpose**: Should skill always be considered relevant?
- **Default behavior**: Use `false` for most skills

**When to use `true`**:
- Meta-skills (skill-creator, debugging-helper)
- Cross-cutting concerns (logging, error handling)
- Generally applicable utilities

**When to use `false`**:
- Domain-specific workflows (domain-add-aggregate)
- File-type-specific operations (adr-writer, nuget-manager)
- Context-triggered skills (most skills)

## File Structure

### Minimal Skill (Single File)

```
.github/skills/simple-skill/
└── SKILL.md                  # Frontmatter + workflow + examples
```

### Complex Skill (With Resources)

```
.github/skills/complex-skill/
├── SKILL.md                  # Main entry point
├── templates/                # Code templates
│   └── *.cs, *.md
├── examples/                 # Extended examples
│   └── *.md
├── checklists/               # Validation checklists
│   └── *.md
└── docs/                     # Reference docs
    └── *.md
```

## Markdown Content Structure

### Recommended Sections

```markdown
---
skill: example-skill
description: Example skill description
globs: []
alwaysApply: false
---

# Skill Name

## Overview
Brief introduction (2-4 sentences)

## Prerequisites
- Required tools
- Required knowledge
- Required setup

## Core Rules
1. **NEVER** do X
2. **ALWAYS** do Y
3. **MUST** verify Z

## Workflows
### Creating X
#### Step 1: Action
...

## Examples
### User: "Request in natural language"
**Action**: What agent does

## Quality Checklist
- [ ] Item 1
- [ ] Item 2

## Common Pitfalls
**WRONG:**
- Anti-pattern 1

**CORRECT:**
- Correct approach 1

## References
- Links to docs
```

## Discovery Mechanisms

### How Agents Find Skills

1. **Directory Scan**: Agents scan `.github/skills/*/SKILL.md`
2. **Frontmatter Parse**: Extract `skill`, `description`, `globs`, `alwaysApply`
3. **File Pattern Match**: Check current files against `globs`
4. **Relevance Score**: Calculate based on patterns + description + context
5. **Invoke Top Match**: Execute skill if confidence threshold met

### How Users Invoke Skills

**Explicit Invocation**:
```
User: "Use the adr-writer skill to document the decision to use EF Core"
```

**Implicit Invocation**:
```
User: "Document the decision to use EF Core"
# Agent sees "document" + "decision" → matches adr-writer description → invokes skill
```

**File Context Invocation**:
```
User opens: docs/ADR/README.md
User: "Add a new ADR for the migration strategy"
# Agent sees ADR files → matches adr-writer globs → invokes skill
```

## Progressive Disclosure Pattern

Skills should support 3 levels of engagement:

### Level 1: Quick Start (< 30 seconds)
- Overview
- Prerequisites
- Core Rules

**Goal**: User understands scope and can start immediately

### Level 2: Execution (during workflow)
- Step-by-step procedures
- Inline examples
- Decision points

**Goal**: User completes task without leaving main file

### Level 3: Deep Dive (as needed)
- Extended examples
- Troubleshooting
- Edge cases
- Reference material

**Goal**: User solves complex/rare scenarios

## Portability Guidelines

### Write Portable Skills

**DO**:
- Use relative paths (`.github/skills/...`)
- Reference standard tools (dotnet, git, npm)
- Document prerequisites explicitly
- Use common terminology

**DON'T**:
- Hard-code absolute paths (`C:\Users\...`)
- Assume specific project structure (unless documented in Prerequisites)
- Reference internal-only docs (chatmodes, internal wikis)
- Use organization-specific jargon

### Project-Specific Adaptations

Skills can reference project-specific concepts IF documented:

**Prerequisites Section**:
```markdown
## Prerequisites

- Project uses bITdevKit framework
- Familiarity with project's Clean Architecture structure (see AGENTS.md)
- Understanding of module organization (src/Modules/[Module]/...)
```

**References Section**:
```markdown
## References

- Project Architecture: AGENTS.md
- Module Structure: src/Modules/README.md
- ADR Template: docs/ADR/README.md
```

## Standard Compliance Checklist

- [ ] **Frontmatter Present**: File starts with `---` YAML frontmatter
- [ ] **Required Fields**: Has `skill`, `description`, `globs`, `alwaysApply`
- [ ] **Valid YAML**: Parses without errors
- [ ] **skill Matches Directory**: Directory is `.github/skills/[skill-name]/`
- [ ] **description Is Descriptive**: 60-120 chars, starts with action verb
- [ ] **globs Is Array**: Uses `[]` syntax (even if empty)
- [ ] **alwaysApply Is Boolean**: `true` or `false` (not yes/no)
- [ ] **Markdown Content**: Structured workflow after frontmatter
- [ ] **Self-Contained**: Skill can be understood without external docs (or references them)

## Differences from Custom Instructions

| Aspect | Agent Skills | Custom Instructions |
|--------|-------------|---------------------|
| Activation | On-demand (triggered) | Always active |
| Scope | Specific task | Entire project |
| Format | YAML frontmatter + markdown | Markdown only |
| Discovery | Scanned by agent | Loaded on startup |
| Portability | High (can share across projects) | Low (project-specific) |
| Content | Workflows, templates, examples | Rules, conventions, principles |

**Use Skills For**: Executable workflows (create ADR, add package, scaffold aggregate)
**Use Instructions For**: Always-active rules (coding style, layer boundaries, conventions)

## Evolution & Versioning

### Standard Evolution

The agentskills.io standard is evolving:
- **Current**: v1.0 (frontmatter + markdown)
- **Future**: Possible additions (parameters, return values, chaining)

### Skill Versioning

**Option 1: In-Place Updates**
- Update skill content directly
- Document changes in commit messages
- Suitable for bug fixes and clarifications

**Option 2: Version Subdirectories**
```
.github/skills/example-skill/
├── v1/
│   └── SKILL.md
├── v2/
│   └── SKILL.md
└── SKILL.md -> v2/SKILL.md (symlink or copy)
```

**Option 3: Deprecation & Replacement**
- Create new skill with different name (`example-skill-v2`)
- Mark old skill deprecated in description
- Provide migration guide

## Resources

### Official Documentation

- **agentskills.io**: [https://agentskills.io](https://agentskills.io)
- **VS Code Copilot Skills**: [https://code.visualstudio.com/docs/copilot/customization/agent-skills](https://code.visualstudio.com/docs/copilot/customization/agent-skills)

### Community

- **GitHub Discussions**: Search "agentskills" or "copilot agent skills"
- **VS Code Community**: Discord, forums

### Project-Specific

- **Existing Skills**: `.github/skills/adr-writer/`, `.github/skills/nuget-manager/`
- **Skill Creator**: `.github/skills/skill-creator/SKILL.md`
- **Custom Instructions**: `AGENTS.md`, `.github/copilot-instructions.md`

## Quick Reference

### Minimal Valid Skill

```markdown
---
skill: hello-world
description: Generates hello world programs in multiple languages
globs:
  - "*.{js,py,cs,go}"
alwaysApply: false
---

# Hello World Generator

## Overview
Generates hello world programs in JavaScript, Python, C#, and Go.

## Workflows
### Generate Hello World
Choose language, create file, write hello world code.
```

### Testing Your Skill

1. **Create Skill**: Write SKILL.md with frontmatter
2. **Reload VS Code**: Restart or reload window
3. **Check Discovery**: Open relevant file (matching globs)
4. **Invoke Skill**: Ask agent to perform task
5. **Verify Output**: Confirm agent follows skill workflow

## Conclusion

The agentskills.io open standard enables:
- **Portability**: Skills work across agents and projects
- **Discoverability**: Agents find relevant skills automatically
- **Reusability**: Package workflows for sharing
- **Extensibility**: Projects can add custom skills

Follow this standard to create skills that are compatible with the growing ecosystem of AI coding agents.
