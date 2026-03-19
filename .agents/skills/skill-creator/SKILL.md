---
name: skill-creator
description: Guide for creating high-quality Agent Skills following the open standard (agentskills.io). Use this when asked to create or update a skill, write a SKILL.md file, convert custom instructions or chatmodes into portable skills, or design specialized AI agent capabilities.
---

# Skill Creator

This skill provides guidance for creating effective skills.

## Overview

This skill helps you create high-quality Agent Skills following the [agentskills.io](https://agentskills.io) open standard. Agent Skills are folders containing instructions, scripts, and resources that AI agents can load when relevant to perform specialized tasks. Skills you create work across multiple AI agents, including GitHub Copilot in VS Code, GitHub Copilot CLI, and GitHub Copilot coding agent.

## When to Use This Skill

Use this skill when you need to:

- Create a new Agent Skill from scratch
- Write a SKILL.md file with proper structure
- Convert custom instructions into a portable skill
- Convert chatmode files into skills
- Design specialized agent capabilities
- Structure skill resources and examples
- Validate skill format and conventions

## Agent Skills vs Custom Instructions

### Decision Matrix

**Use Agent Skills When:**

- Creating reusable capabilities across different AI tools
- Including scripts, examples, or other resources alongside instructions
- Sharing capabilities with the wider AI community
- Defining specialized workflows (testing, debugging, deployment)
- Need portability (works in VS Code, Copilot CLI, and coding agent)
- Task-specific, loaded on-demand based on description matching

**Use Custom Instructions When:**

- Defining project-specific coding standards
- Setting language or framework conventions
- Specifying code review or commit message guidelines
- Applying rules based on file types using glob patterns
- VS Code and GitHub.com only (not portable to other agents)
- Always applied or scoped via glob patterns

## Agent Skills Anatomy

### Required: SKILL.md File

Every skill must have a `SKILL.md` file in the skill's directory.

#### YAML Frontmatter (Required)

The file starts with YAML frontmatter containing metadata:

```yaml
---
name: skill-name
description: Description of what the skill does and when to use it
---
```

**Field Requirements:**

1. **name** (required):
   - Unique identifier for the skill
   - Format: lowercase with hyphens for spaces
   - Length: 1-64 characters
   - Examples: `webapp-testing`, `github-actions-debugging`, `adr-writer`

2. **description** (required):
   - Describes WHAT the skill does AND WHEN to use it
   - Length: 50-1024 characters
   - Must include use case keywords to trigger skill loading
   - Format: "Brief description. Use this when [specific scenarios]."
   - Example: "Guide for testing web applications using Playwright. Use this when asked to create or run browser-based tests."

#### Body (Required)

The body contains the actual skill instructions:

```markdown
# Skill Name

## Overview
[What this skill helps accomplish]

## When to Use This Skill
[Specific scenarios and triggers]

## Instructions
[Step-by-step procedures]

## Best Practices
[Guidelines and recommendations]

## Common Pitfalls
[What to avoid and how]

## References
[Links to resources]
```

### Optional: Resource Files

Skills can include supporting resources:

**templates/** - Code templates, file skeletons, boilerplate
**examples/** - Working examples, walkthroughs, demonstrations
**checklists/** - Step-by-step verification lists
**docs/** - Additional documentation, guides, references

## Progressive Disclosure Strategy

Agent Skills use a three-level loading system for efficiency:

### Level 1: Discovery (Always Loaded)

- The `name` and `description` from YAML frontmatter
- Lightweight metadata that helps the agent decide if skill is relevant
- Always visible to the agent

### Level 2: Instructions (Loaded When Relevant)

- The SKILL.md file body content
- Detailed instructions and guidelines
- Loaded only when skill description matches the user's request

### Level 3: Resources (Loaded On-Demand)

- Template files, examples, documentation in subdirectories
- Loaded only when the agent references them
- Keeps context efficient

This architecture means you can install many skills without consuming context - only relevant content loads.

## Creating a Skill: Step-by-Step

### Step 1: Define Purpose

Answer these questions:

- What specific task does this skill help with?
- When should it be used? (Be specific about triggers)
- What makes it better than custom instructions?
- Does it need supporting resources?

### Step 2: Choose a Name

Follow these rules:

- Lowercase only
- Use hyphens for spaces (not underscores, not camelCase)
- Descriptive but concise
- Maximum 64 characters
- Examples:
  - CORRECT: `webapp-testing`, `database-migrations`
  - WRONG: `WebApp_Testing`, `dbMigrate`, `test`

### Step 3: Write Description

The description is critical - it determines when your skill loads.

**Requirements:**

- State WHAT the skill does (capabilities)
- State WHEN to use it (trigger keywords)
- Be specific about use cases
- Maximum 1024 characters
- Include the phrase "Use this when" for clarity

**Examples:**

WRONG: "Helps with testing"
(Too vague, no trigger keywords)

CORRECT: "Guide for testing web applications using Playwright. Use this when asked to create or run browser-based tests."
(Specific tool, clear triggers)

WRONG: "Database stuff"
(Vague, unprofessional)

CORRECT: "Guide for managing database migrations with EF Core. Use this when asked to create, apply, or rollback database schema changes."
(Specific framework, clear use cases)

### Step 4: Structure the Body

Recommended sections:

```markdown
# Skill Name

## Overview
Brief introduction to what this skill accomplishes

## When to Use This Skill
Specific scenarios where this skill should be invoked:
- Scenario 1
- Scenario 2
- Scenario 3

## Prerequisites
Information, tools, or setup required before using this skill

## Instructions
Detailed step-by-step procedures

### Step 1: [First Step]
...

### Step 2: [Second Step]
...

## Best Practices
Guidelines for optimal use:
- Practice 1
- Practice 2

## Common Pitfalls
What to avoid:
- WRONG: [Description and why it's wrong]
- CORRECT: [How to do it right]

## References
Links to related resources:
- [Internal skill resources](./templates/example.md)
- [External documentation](https://example.com)
```

### Step 5: Add Resources (Optional)

When to include resources:

**templates/** - When you need:

- Code boilerplate or file skeletons
- Configuration file templates
- Reusable patterns

**examples/** - When you need:

- Working demonstrations
- Step-by-step walkthroughs
- Before/after comparisons

**checklists/** - When you need:

- Step-by-step verification
- Quality gates
- Validation procedures

**docs/** - When you need:

- Extended documentation
- Reference materials
- Additional context

### Step 6: Validate

Before finalizing, check:

**Name:**

- [ ] Lowercase with hyphens
- [ ] 1-64 characters
- [ ] Unique (no conflicts)

**Description:**

- [ ] Includes what it does
- [ ] Includes when to use it
- [ ] Has trigger keywords
- [ ] 50-1024 characters

**Body:**

- [ ] Clear, actionable instructions
- [ ] Organized logically
- [ ] Examples where helpful
- [ ] No emoji or special characters

**Resources:**

- [ ] Well-organized directory structure
- [ ] Documented purpose for each resource
- [ ] Referenced in SKILL.md

## Skill Directory Placement

### Project Skills (Recommended)

- **Location**: `.github/skills/your-skill-name/`
- **Scope**: Available to all users of this repository
- **Legacy**: `.claude/skills/` (backward compatibility)

### Personal Skills

- **Location**: `~/.copilot/skills/your-skill-name/`
- **Scope**: Available only to you across all projects
- **Legacy**: `~/.claude/skills/` (backward compatibility)

## Writing Effective Descriptions

### Good Description Characteristics

1. **Specific about capabilities**: Names tools, frameworks, or techniques
2. **Clear about triggers**: Uses keywords that match user requests
3. **Includes "Use this when"**: Makes triggering conditions explicit
4. **Concise but complete**: Provides enough detail without being verbose

### Description Examples

**Example 1: Web Testing Skill**
CORRECT: "Guide for testing web applications using Playwright. Use this when asked to create or run browser-based tests, debug failing UI tests, or set up test infrastructure."

**Example 2: GitHub Actions Skill**
CORRECT: "Guide for debugging failing GitHub Actions workflows. Use this when asked to debug failing GitHub Actions workflows, analyze CI/CD failures, or fix build pipeline issues."

**Example 3: ADR Writing Skill**
CORRECT: "Write high-quality Architectural Decision Records (ADRs) following MADR format. Use when documenting important architectural decisions, technology choices, or cross-cutting concerns."

### Common Description Mistakes

WRONG: "Helps with coding" - Too vague
CORRECT: "Guide for Python development with type hints and pytest. Use when writing or testing Python code."

WRONG: "Database things" - Unprofessional, vague
CORRECT: "Guide for EF Core migrations and database schema management. Use when creating, applying, or troubleshooting database migrations."

WRONG: "For fixing bugs" - No specificity
CORRECT: "Guide for debugging .NET applications using Visual Studio debugger. Use when investigating exceptions, setting breakpoints, or analyzing runtime behavior."

## Quality Standards

### Name Requirements

- Format: lowercase-with-hyphens
- Length: 1-64 characters
- Uniqueness: No conflicts with other skills in the same location
- Descriptive: Clear what the skill does

### Description Requirements

- Length: 50-1024 characters (aim for 100-300)
- Content: Capabilities + Use cases
- Clarity: Unambiguous triggering conditions
- Keywords: Include terms users would naturally use

### Body Requirements

- Clear, actionable instructions
- Logical organization with headings
- Examples where helpful
- No emoji or special characters (use CORRECT/WRONG for examples)
- References to resources using relative paths

### Resource Requirements

- Well-organized directory structure
- Each resource has a clear purpose
- Resources are referenced in SKILL.md
- Examples are working and tested

## Portability Considerations

To ensure skills work across all agents:

1. **Follow the Standard**: Stick to agentskills.io specification
2. **Use Relative Paths**: Reference resources with `./templates/example.md`
3. **Avoid Platform-Specific References**: Don't mention VS Code features specifically
4. **Test in Multiple Environments**: Verify skill works in VS Code, CLI, and coding agent
5. **Keep Dependencies Minimal**: Skills should be self-contained

## Common Patterns

### Pattern 1: Workflow Skills

Skills that guide multi-step processes.

**Characteristics:**

- Clear step-by-step instructions
- Build checkpoints or validation gates
- Plan-first approach (present plan, wait for confirmation)
- Examples: `domain-add-aggregate`, `adr-writer`

**Template Structure:**

```markdown
## Step 1: Clarifying Questions
Ask user for required information

## Step 2: Present Plan
Show what will be done

## Step 3: Execute
Perform the work with progress reporting
```

### Pattern 2: Tool Skills

Skills that teach using specific tools or commands.

**Characteristics:**

- Focus on a specific tool (npm, docker, git)
- Command examples and syntax
- Common use cases and workflows
- Examples: `nuget-manager`, `github-actions-debugging`

**Template Structure:**

```markdown
## Tool Overview
Brief description of the tool

## Common Operations
- Operation 1: Command and explanation
- Operation 2: Command and explanation

## Examples
Real-world scenarios with commands
```

### Pattern 3: Knowledge Skills

Skills that provide domain expertise or best practices.

**Characteristics:**

- Guidelines and principles
- Decision matrices
- Best practices and anti-patterns
- Examples: `architecture-decision-guidance`, `security-review`

**Template Structure:**

```markdown
## Principles
Core concepts and guidelines

## Decision Making
How to choose between options

## Best Practices
What to do

## Anti-Patterns
What to avoid
```

## Character Usage Standards

**ALWAYS use plain ASCII:**

```markdown
CORRECT: Use typed IDs for type safety
WRONG: Use raw Guid everywhere
WARNING: This can cause runtime errors
NOTE: Important detail
```

**NEVER use emoji or special Unicode:**

```markdown
✓ Correct    <- DO NOT USE
❌ Wrong      <- DO NOT USE
⚠️ Warning   <- DO NOT USE
```

## Validation Checklist

Before publishing your skill:

**Structure:**

- [ ] SKILL.md file exists in skill directory
- [ ] YAML frontmatter is valid and complete
- [ ] Body is well-organized with clear headings
- [ ] All internal links work

**Metadata:**

- [ ] Name follows format (lowercase-with-hyphens, 1-64 chars)
- [ ] Description includes "what" and "when" (50-1024 chars)
- [ ] Description has trigger keywords
- [ ] Name is unique

**Content:**

- [ ] Instructions are clear and actionable
- [ ] Examples are provided where helpful
- [ ] No emoji or special characters
- [ ] Plain ASCII (CORRECT/WRONG) used for examples

**Resources:**

- [ ] Organized in appropriate subdirectories
- [ ] Referenced from SKILL.md
- [ ] Have clear purpose
- [ ] Use relative paths

**Portability:**

- [ ] Follows agentskills.io standard
- [ ] No platform-specific references
- [ ] Self-contained (minimal external dependencies)
- [ ] Tested in multiple agent environments

## References

- [Agent Skills Standard](https://agentskills.io)
- [VS Code Agent Skills Documentation](https://code.visualstudio.com/docs/copilot/customization/agent-skills)
- Templates in this skill: [./templates/basic-skill-template.md](./templates/basic-skill-template.md)
- Examples in this skill: [./examples/](./examples/)
- Checklists in this skill: [./checklists/](./checklists/)

## Notes

### Directory Structure Example

```
.github/skills/your-skill-name/
├── SKILL.md                    # Main skill file (required)
├── templates/                  # Code templates (optional)
│   ├── template1.cs
│   └── template2.md
├── examples/                   # Working examples (optional)
│   ├── example1.md
│   └── example2.md
├── checklists/                 # Verification checklists (optional)
│   └── checklist.md
└── docs/                       # Additional docs (optional)
    └── reference.md
```

### Referencing Resources in SKILL.md

Use relative paths to reference resources:

```markdown
See the [basic template](./templates/basic-template.md) for a starting point.

Review the [example walkthrough](./examples/complete-example.md) for a full demonstration.

Use the [validation checklist](./checklists/validation.md) before finalizing.
```

### Testing Your Skill

1. **Description Triggers**: Ask questions that should trigger your skill. Does it load?
2. **Instructions Clarity**: Can someone follow the instructions without confusion?
3. **Resources Accessible**: Are all referenced files loadable?
4. **Cross-Agent Compatibility**: Test in VS Code, CLI, and coding agent if possible

### Common Mistakes to Avoid

WRONG: Using emoji or special characters
CORRECT: Use plain ASCII words like CORRECT, WRONG, WARNING, NOTE

WRONG: Vague descriptions like "helps with testing"
CORRECT: Specific descriptions with tools and scenarios

WRONG: Absolute file paths
CORRECT: Relative paths from skill directory

WRONG: Platform-specific instructions
CORRECT: Generic instructions that work across agents

WRONG: Missing "when to use" in description
CORRECT: Clear triggering conditions using "Use this when"
