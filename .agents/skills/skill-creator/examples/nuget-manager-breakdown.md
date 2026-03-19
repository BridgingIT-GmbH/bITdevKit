# NuGet Manager Skill - Detailed Breakdown

This document analyzes the `nuget-manager` skill from `.github/skills/nuget-manager/SKILL.md` to demonstrate concise, tool-focused skill design patterns.

## Skill Metadata Analysis

```yaml
---
name: nuget-manager
description: 'Manage NuGet packages in .NET projects/solutions. Use this skill when adding, removing, or updating NuGet package versions. It enforces using `dotnet` CLI for package management and provides strict procedures for direct file edits only when updating versions.'
---
```

### What Makes This Frontmatter Effective

**Strengths:**

1. **Clear Scope**: "Manage NuGet packages" (verb + object)
2. **When to Use**: "adding, removing, or updating NuGet package versions"
3. **Key Constraint**: "enforces using `dotnet` CLI" (tells agent the approach)
4. **Critical Exception**: "strict procedures for direct file edits only when updating versions"

**Specificity**: The description itself teaches the core rule (prefer CLI, only edit files for version updates).

### Updated for agentskills.io Standard

```yaml
---
skill: nuget-manager
description: Manages NuGet package dependencies across .NET projects using dotnet CLI with version conflict detection and validation
globs:
  - "*.csproj"
  - "*.slnx"
  - Directory.Build.props
  - Directory.Packages.props
alwaysApply: false
---
```

**Key Changes:**
- Added `globs` to trigger when working with project files
- Simplified description (removed procedural details)
- `alwaysApply: false` because skill is context-specific

## Structure Analysis

The skill is remarkably concise (69 lines) yet complete:

### Level 1: Quick Start (Lines 6-26)
- Overview (2 sentences)
- Prerequisites (3 items)
- Core Rules (3 critical rules)

**Total: 21 lines**

**Effectiveness**: User can start using skill in <15 seconds.

### Level 2: Workflows (Lines 28-57)
- Adding a Package (3 lines)
- Removing a Package (3 lines)
- Updating Package Versions (27 lines with 4 sub-steps)

**Total: 33 lines**

**Effectiveness**: Most common operations (add/remove) take 1 line each. Complex operation (update) gets detailed treatment.

### Level 3: Examples (Lines 59-69)
- 2 concrete examples

**Total: 11 lines**

**Effectiveness**: Shows both simple (add) and complex (update) scenarios.

## Core Rules Analysis

```markdown
## Core Rules

1.  **NEVER** directly edit `.csproj`, `.props`, or `Directory.Packages.props` files to **add** or **remove** packages. Always use `dotnet add package` and `dotnet remove package` commands.
2.  **DIRECT EDITING** is ONLY permitted for **changing versions** of existing packages.
3.  **VERSION UPDATES** must follow the mandatory workflow:
    - Verify the target version exists on NuGet.
    - Determine if versions are managed per-project (`.csproj`) or centrally (`Directory.Packages.props`).
    - Update the version string in the appropriate file.
    - Immediately run `dotnet restore` to verify compatibility.
```

### Why These Rules Are Effective

1. **Safety-Focused**: Rules prevent common mistakes (manual file editing)
2. **Explicit Exceptions**: Rule #2 clarifies when direct editing IS allowed
3. **Workflow Preview**: Rule #3 summarizes the complex workflow (detailed later)
4. **Bold Emphasis**: Uses **NEVER**, **ALWAYS**, **ONLY** for critical constraints

### Pattern Analysis

**Structure:**
- Rule 1: Prohibition (NEVER do X, ALWAYS do Y instead)
- Rule 2: Exception (ONLY when Z)
- Rule 3: Mandatory workflow (MUST follow these steps)

**Why This Works:**
- Establishes default behavior (use CLI)
- Clarifies exception (version updates)
- Provides roadmap for exception case

### Pattern to Emulate

For tool-focused skills:
1. State the primary approach (ALWAYS use X)
2. State exceptions explicitly (ONLY edit when Y)
3. Summarize complex workflows in rules (preview of details)

## Workflow Structure Analysis

### Simple Workflows: Add & Remove

```markdown
### Adding a Package
Use `dotnet add [<PROJECT>] package <PACKAGE_NAME> [--version <VERSION>]`.
Example: `dotnet add src/MyProject/MyProject.csproj package Newtonsoft.Json`

### Removing a Package
Use `dotnet remove [<PROJECT>] package <PACKAGE_NAME>`.
Example: `dotnet remove src/MyProject/MyProject.csproj package Newtonsoft.Json`
```

### Why This Is Brilliant

1. **One Command Each**: No explanation needed beyond syntax
2. **Concrete Example**: Shows actual project path, not placeholder
3. **Self-Explanatory**: Command syntax is the documentation

**Lesson**: When operation is simple, don't over-explain. Show syntax + example = done.

### Complex Workflow: Updating Versions

```markdown
### Updating Package Versions
When updating a version, follow these steps:

1.  **Verify Version Existence**:
    Check if the version exists using the `dotnet package search` command with exact match and JSON formatting.
    Using `jq`:
    `dotnet package search <PACKAGE_NAME> --exact-match --format json | jq -e '.searchResult[].packages[] | select(.version == "<VERSION>")'`
    Using PowerShell:
    `(dotnet package search <PACKAGE_NAME> --exact-match --format json | ConvertFrom-Json).searchResult.packages | Where-Object { $_.version -eq "<VERSION>" }`

2.  **Determine Version Management**:
    - Search for `Directory.Packages.props` in the solution root. If present, versions should be managed there via `<PackageVersion Include="Package.Name" Version="1.2.3" />`.
    - If absent, check individual `.csproj` files for `<PackageReference Include="Package.Name" Version="1.2.3" />`.

3.  **Apply Changes**:
    Modify the identified file with the new version string.

4.  **Verify Stability**:
    Run `dotnet restore` on the project or solution. If errors occur, revert the change and investigate.
```

### Why This Works

1. **Sequential Steps**: Numbered 1-4, clear progression
2. **Verify Before Acting**: Step 1 prevents updating to non-existent versions
3. **Context Detection**: Step 2 handles both centralized and per-project management
4. **Safety Check**: Step 4 ensures changes don't break the build
5. **Tool Options**: Provides both `jq` (Linux/Mac) and PowerShell (Windows) approaches

### Pattern Analysis

**Workflow Design:**
- Precondition verification (Step 1)
- Context detection (Step 2)
- Action execution (Step 3)
- Validation (Step 4)

**This is a universal pattern for safe operations:**
1. Verify inputs
2. Detect environment
3. Execute change
4. Validate result

### Pattern to Emulate

For workflows that modify state:
1. **Always verify first** (does version exist?)
2. **Detect context** (centralized vs per-project)
3. **Execute precisely** (update specific file)
4. **Validate immediately** (run restore)

## Examples Section Analysis

```markdown
## Examples

### User: "Add Serilog to the WebApi project"
**Action**: Execute `dotnet add src/WebApi/WebApi.csproj package Serilog`.

### User: "Update Newtonsoft.Json to 13.0.3 in the whole solution"
**Action**:
1. Verify 13.0.3 exists: `dotnet package search Newtonsoft.Json --exact-match --format json` (and parse output to confirm "13.0.3" is present).
2. Find where it's defined (e.g., `Directory.Packages.props`).
3. Edit the file to update the version.
4. Run `dotnet restore`.
```

### Why This Works

1. **Contrast**: Example 1 is simple (one command), Example 2 is complex (4 steps)
2. **Realistic User Language**: "Add X to Y project" and "Update X to version Y.Z"
3. **Direct Mapping**: Examples map directly to workflows (Add vs Update)
4. **Step Reference**: Example 2 steps match the numbered workflow

### Pattern Analysis

**Example Selection Criteria:**
- Cover both simple and complex cases
- Use realistic user language
- Show exact commands/actions
- Reference workflow steps implicitly

### Pattern to Emulate

Choose examples that:
1. Demonstrate simplest case (one-liner if possible)
2. Demonstrate most complex case (multi-step workflow)
3. Use realistic project names and versions
4. Show expected agent actions, not explanations

## What Makes This Skill Excellent

### Strengths

1. **Brevity**: 69 lines total (13% of adr-writer's length)
2. **Clarity**: Each workflow is crystal clear
3. **Safety**: Emphasizes verification and validation
4. **Cross-Platform**: Provides both *nix (`jq`) and Windows (PowerShell) commands
5. **No Fluff**: Zero unnecessary explanation

### Why It Doesn't Need More

**No quality checklist?**
- Not needed. Operations are binary (package added or not).

**No common pitfalls?**
- Core Rules section covers the main pitfall (manual editing).

**No subdirectories?**
- Skill is simple enough for single file.

**Only 2 examples?**
- Sufficient to cover simple and complex cases.

### Pattern Insight

**Skill length should match task complexity:**
- Simple tasks (package management) → Short skills (69 lines)
- Complex tasks (ADR writing) → Long skills (476 lines)

**Don't pad skills to match arbitrary length targets.**

## Comparison: adr-writer vs nuget-manager

| Aspect | adr-writer | nuget-manager | Why Different? |
|--------|-----------|---------------|----------------|
| Lines | 476 | 69 | ADR writing is inherently complex |
| Core Rules | 5 | 3 | ADRs have more conventions to enforce |
| Workflows | 1 (13 steps) | 3 (1-4 steps each) | ADR is single complex flow; NuGet has distinct operations |
| Examples | 2 scenarios | 2 scenarios | Both provide sufficient coverage |
| Checklists | Yes (17 items) | No | ADRs need quality validation; package ops are binary |
| Subdirectories | No | No | Both fit comfortably in single file |

**Key Insight**: Skill complexity should be **proportional to task complexity**, not standardized.

## When to Use This Pattern

Use the nuget-manager pattern (concise, workflow-focused) when:

1. **Tool has clear commands** (dotnet CLI, git, etc.)
2. **Operations are distinct** (add ≠ remove ≠ update)
3. **Each operation is relatively simple** (1-4 steps)
4. **Safety checks are straightforward** (run restore, check status)
5. **Outcomes are binary** (worked or didn't)

**Examples of similar skills:**
- git-workflow (commit, push, pull, merge)
- docker-manager (build, run, stop, remove)
- database-migrator (create, apply, rollback)

## What Could Be Improved

### Minor Enhancements

1. **Add Visual Separator**:
   ```markdown
   ---

   ### Adding a Package
   ```
   Makes workflows easier to scan.

2. **Show Expected Output**:
   ```bash
   $ dotnet add package Serilog
   info : Adding PackageReference for package 'Serilog' into project...
   ```
   Helps users confirm success.

3. **Add Troubleshooting**:
   ```markdown
   ## Troubleshooting

   **Package not found**: Verify package name on nuget.org
   **Restore fails**: Check for version conflicts in output
   ```

### Why These Weren't Included

**Current Approach:** Skill focuses on happy path. Agent can handle errors contextually.

**Alternative Approach:** Add minimal troubleshooting (5-10 lines) for common issues.

**Recommendation:** Current approach is fine for simple tools. Add troubleshooting only if users frequently encounter specific errors.

## Key Takeaways for Skill Creators

### What nuget-manager Does Exceptionally Well

1. **Concise**: No wasted words
2. **Command-Focused**: Shows exact commands, not concepts
3. **Safety-First**: Verify → Execute → Validate
4. **Cross-Platform**: Provides alternatives for different OSes
5. **Proportional**: Complexity matches task complexity

### Patterns to Emulate

1. **Simple Operations**: Command syntax + Example = Complete
2. **Complex Operations**: 4-step pattern (Verify → Detect → Execute → Validate)
3. **Prerequisites**: List specific tools/versions needed
4. **Core Rules**: Establish default + exceptions + workflow summary
5. **Examples**: One simple + one complex

### Patterns to Avoid

1. **Over-Explanation**: Don't explain what the command already says
2. **Unnecessary Structure**: Don't add checklists/subdirectories if not needed
3. **Arbitrary Length**: Don't pad to match other skills

### When to Use This Style

Choose the nuget-manager style for:
- CLI tool wrappers
- Operations with clear commands
- Tasks with distinct, non-overlapping workflows
- Binary outcomes (success/failure)

## Conclusion

The `nuget-manager` skill demonstrates **lean skill design** with:
- Minimal but sufficient content (69 lines)
- Clear command-focused workflows
- Strong safety emphasis (verify → execute → validate)
- Appropriate complexity (simple task → simple skill)

It proves that **effective skills don't need to be long**. When the tool has clear commands and operations are straightforward, embrace brevity.

**Guideline**: Write the shortest skill that completely fulfills the task. If that's 50 lines, perfect. If it's 500 lines, also perfect. Match complexity to need.
