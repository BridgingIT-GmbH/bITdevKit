# Description Quality Examples

This document provides concrete examples of effective vs ineffective skill descriptions to guide you in writing clear, actionable descriptions.

## What Makes a Good Description?

A good description:
1. **Starts with action verb** (Creates, Manages, Analyzes, Generates)
2. **States WHAT** (the artifact or outcome)
3. **States HOW** (the approach or format)
4. **States WHEN** (the use case or trigger)
5. **Is concise** (60-120 characters recommended)
6. **Avoids fluff** (no "helps with", "assists in", "provides support for")

## Length Guidelines

```
TOO SHORT (< 40 chars): Vague, lacks context
IDEAL (60-120 chars):   Specific, actionable, complete
TOO LONG (> 150 chars): Rambling, unfocused
```

## Examples by Skill Type

### Workflow Skills (Multi-Step Processes)

#### CORRECT Examples

```yaml
description: Creates Architectural Decision Records following MADR format with comprehensive context and alternatives analysis
```
**Why it works**:
- Action verb: "Creates"
- Artifact: "Architectural Decision Records"
- Format: "MADR format"
- Quality level: "comprehensive context and alternatives"
- Length: 117 characters

```yaml
description: Adds new Domain Aggregates using Clean Architecture and DDD principles with full CRUD scaffolding
```
**Why it works**:
- Action verb: "Adds"
- Artifact: "Domain Aggregates"
- Approach: "Clean Architecture and DDD principles"
- Scope: "full CRUD scaffolding"
- Length: 104 characters

```yaml
description: Generates integration tests using WebApplicationFactory with realistic data and assertion patterns
```
**Why it works**:
- Action verb: "Generates"
- Artifact: "integration tests"
- Tooling: "WebApplicationFactory"
- Quality: "realistic data and assertion patterns"
- Length: 111 characters

#### WRONG Examples

```yaml
description: Helps with ADRs
```
**Why it fails**:
- Too vague ("helps with" is meaningless)
- No format mentioned
- No context about quality or approach
- Length: 19 characters (too short)

```yaml
description: A skill for creating domain aggregates
```
**Why it fails**:
- Weak opening ("A skill for" is redundant)
- No mention of architecture/principles
- No scope (just create aggregate, or full CRUD?)
- Length: 46 characters (too short)

```yaml
description: This comprehensive skill assists developers in the process of generating, configuring, and validating integration test suites using the WebApplicationFactory pattern with support for various assertion libraries
```
**Why it fails**:
- Too long (186 characters)
- Wordy ("assists developers in the process of")
- Over-specifies ("various assertion libraries" is not specific)
- Rambling structure

### Tool-Focused Skills (CLI Wrappers)

#### CORRECT Examples

```yaml
description: Manages NuGet package dependencies across .NET projects using dotnet CLI with version conflict detection
```
**Why it works**:
- Action verb: "Manages"
- Object: "NuGet package dependencies"
- Scope: "across .NET projects"
- Tool: "dotnet CLI"
- Value-add: "version conflict detection"
- Length: 113 characters

```yaml
description: Executes database migrations using EF Core with rollback support and schema validation
```
**Why it works**:
- Action verb: "Executes"
- Task: "database migrations"
- Tool: "EF Core"
- Features: "rollback support and schema validation"
- Length: 95 characters

```yaml
description: Manages Git workflows for feature branches with automatic conflict detection and PR creation
```
**Why it works**:
- Action verb: "Manages"
- Scope: "Git workflows for feature branches"
- Features: "automatic conflict detection and PR creation"
- Length: 104 characters

#### WRONG Examples

```yaml
description: NuGet management
```
**Why it fails**:
- No verb (not action-oriented)
- No context (manage how?)
- No tool mentioned
- Length: 18 characters (too short)

```yaml
description: Helps manage packages in .NET projects
```
**Why it fails**:
- Weak verb ("helps manage" vs "Manages")
- Vague ("packages" - what kind?)
- No tool mentioned
- No differentiator
- Length: 46 characters (too short)

```yaml
description: This skill provides comprehensive support for managing NuGet package references, versions, and dependencies across single or multiple .NET projects and solutions, with built-in validation
```
**Why it fails**:
- Too long (179 characters)
- Wordy opening ("provides comprehensive support for")
- Lists too many features (pick the most important)

### Analysis/Validation Skills

#### CORRECT Examples

```yaml
description: Analyzes code changes for architectural violations using ADR rules and suggests corrections
```
**Why it works**:
- Action verb: "Analyzes"
- Target: "code changes"
- Criteria: "architectural violations using ADR rules"
- Output: "suggests corrections"
- Length: 99 characters

```yaml
description: Validates API responses against OpenAPI specifications with detailed mismatch reporting
```
**Why it works**:
- Action verb: "Validates"
- Target: "API responses"
- Standard: "OpenAPI specifications"
- Output: "detailed mismatch reporting"
- Length: 98 characters

```yaml
description: Reviews domain models for DDD compliance and identifies aggregate boundary violations
```
**Why it works**:
- Action verb: "Reviews"
- Target: "domain models"
- Standard: "DDD compliance"
- Specific check: "aggregate boundary violations"
- Length: 97 characters

#### WRONG Examples

```yaml
description: Code analyzer
```
**Why it fails**:
- No verb
- No context (analyzes what? for what?)
- No differentiator
- Length: 14 characters (too short)

```yaml
description: Helps you find problems in your code
```
**Why it fails**:
- Weak verb ("helps you find")
- Vague ("problems" - what kind?)
- No approach or standard
- Length: 43 characters (too short)

```yaml
description: This skill performs comprehensive static analysis of your codebase to identify potential architectural violations, design pattern misuse, and deviations from established best practices as documented in your ADRs
```
**Why it fails**:
- Too long (197 characters)
- Unfocused (too many disparate features)
- Wordy ("performs comprehensive static analysis of")

### Meta-Skills (Always Available)

#### CORRECT Examples

```yaml
description: Guides creation of high-quality Agent Skills following agentskills.io open standard with templates
```
**Why it works**:
- Action verb: "Guides"
- Task: "creation of high-quality Agent Skills"
- Standard: "agentskills.io open standard"
- Features: "with templates"
- Length: 103 characters

```yaml
description: Assists in debugging by analyzing error messages and suggesting targeted fixes from codebase
```
**Why it works**:
- Action verb: "Assists"
- Task: "debugging"
- Method: "analyzing error messages"
- Output: "targeted fixes from codebase"
- Length: 102 characters

#### WRONG Examples

```yaml
description: Skill creator
```
**Why it fails**:
- No verb
- No context (creates what kind of skills?)
- No standard or approach
- Length: 14 characters (too short)

```yaml
description: Helps with creating skills
```
**Why it fails**:
- Weak verb ("helps with")
- Vague (what kind of skills?)
- No standard or quality indicator
- Length: 31 characters (too short)

## Common Anti-Patterns

### Anti-Pattern 1: Weak Verbs

**WRONG**:
- "Helps with X"
- "Provides support for X"
- "Assists in X"
- "Enables X"
- "Facilitates X"

**CORRECT**:
- "Creates X"
- "Manages X"
- "Generates X"
- "Analyzes X"
- "Validates X"

### Anti-Pattern 2: Redundant Phrases

**WRONG**:
- "This skill..."
- "A tool for..."
- "Use this to..."
- "Provides a way to..."

**CORRECT**:
- Start directly with action verb

### Anti-Pattern 3: Vague Qualifiers

**WRONG**:
- "Better X"
- "Improved X"
- "Enhanced X"
- "Stuff" / "Things"

**CORRECT**:
- Specific features: "X with conflict detection"
- Specific standards: "X following MADR format"
- Specific outcomes: "X with validation"

### Anti-Pattern 4: Kitchen Sink Descriptions

**WRONG**:
```yaml
description: Creates aggregates, adds commands, generates queries, configures EF Core, sets up mappings, and creates endpoints
```

**CORRECT**:
```yaml
description: Adds new Domain Aggregates using Clean Architecture with full CRUD scaffolding
```

**Principle**: Describe the PRIMARY task, not every sub-step.

## Templates by Category

### Workflow Skill Template

```yaml
description: [Verb]s [Artifact] using [Approach/Standard] with [Key Feature/Scope]
```

**Examples**:
- Creates Architectural Decision Records using MADR format with comprehensive alternatives analysis
- Adds new Domain Aggregates using Clean Architecture with full CRUD scaffolding
- Generates integration tests using WebApplicationFactory with realistic data scenarios

### Tool Skill Template

```yaml
description: [Verb]s [Object] across [Scope] using [Tool] with [Key Feature]
```

**Examples**:
- Manages NuGet packages across .NET projects using dotnet CLI with version conflict detection
- Executes database migrations across modules using EF Core with rollback support
- Deploys applications to Azure using Bicep with environment-specific configuration

### Analysis Skill Template

```yaml
description: [Verb]s [Target] for [Criteria/Standard] and [Action/Output]
```

**Examples**:
- Analyzes code changes for architectural violations and suggests corrections
- Validates API responses against OpenAPI specs with detailed mismatch reporting
- Reviews domain models for DDD compliance and identifies boundary violations

### Meta-Skill Template

```yaml
description: [Verb]s [Task] following [Standard/Approach] with [Key Feature]
```

**Examples**:
- Guides creation of Agent Skills following agentskills.io standard with templates
- Assists in debugging by analyzing error messages with targeted fix suggestions
- Teaches Clean Architecture principles through interactive examples and validation

## Quality Checklist

Before finalizing your description, verify:

- [ ] Starts with strong action verb (not "helps", "provides", "enables")
- [ ] Specifies artifact/object (what it creates/manages)
- [ ] Mentions approach/standard/tool (how it does it)
- [ ] Includes key feature or scope (what makes it valuable)
- [ ] Is 60-120 characters (specific enough, not too long)
- [ ] No redundant phrases ("This skill...", "A tool for...")
- [ ] No vague qualifiers ("better", "improved", "stuff")
- [ ] No trailing period
- [ ] Single line (no line breaks)

## A/B Testing Your Description

### Test 1: The 5-Second Rule

Show description to someone unfamiliar. In 5 seconds, can they answer:
- What does this skill do?
- When would I use it?

If no → rewrite

### Test 2: The Specificity Test

Replace key terms with "thing" or "stuff":
- "Creates things using stuff with more things"

If it still makes grammatical sense → too vague, add specifics

### Test 3: The Differentiation Test

Could this description apply to 3+ different skills?

If yes → add differentiating details

### Test 4: The Length Test

- < 60 chars: Probably too vague
- 60-120 chars: Ideal range
- 120-150 chars: Acceptable if necessary
- 150+ chars: Definitely too long, trim

## Revision Examples

### Revision 1: From Vague to Specific

**WRONG (v1)**:
```yaml
description: Helps with tests
```

**BETTER (v2)**:
```yaml
description: Creates integration tests for APIs
```

**CORRECT (v3)**:
```yaml
description: Generates integration tests using WebApplicationFactory with realistic data and assertions
```

### Revision 2: From Wordy to Concise

**WRONG (v1)**:
```yaml
description: This skill provides comprehensive assistance in the creation and management of NuGet package references across your .NET project files
```

**BETTER (v2)**:
```yaml
description: Manages NuGet package references across .NET project files
```

**CORRECT (v3)**:
```yaml
description: Manages NuGet packages across .NET projects using dotnet CLI with version conflict detection
```

### Revision 3: From Weak to Strong

**WRONG (v1)**:
```yaml
description: Helps with creating ADRs for your project
```

**BETTER (v2)**:
```yaml
description: Creates Architectural Decision Records for project decisions
```

**CORRECT (v3)**:
```yaml
description: Creates Architectural Decision Records following MADR format with comprehensive alternatives analysis
```

## Final Tips

1. **Write Multiple Versions**: Draft 3-5 descriptions, then pick the best
2. **Test With Users**: Show to someone unfamiliar, get feedback
3. **Compare With Existing Skills**: Study descriptions of successful skills
4. **Read Aloud**: If it sounds awkward, rewrite
5. **Cut Mercilessly**: Every word should earn its place

## References

- Project Skills: `.github/skills/*/SKILL.md`
- [VS Code Copilot Skills Documentation](https://code.visualstudio.com/docs/copilot/customization/agent-skills)
- [agentskills.io Standard](https://agentskills.io)
