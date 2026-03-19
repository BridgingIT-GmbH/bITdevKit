# Portability Guide for Agent Skills

This document explains how to create Agent Skills that work across different projects, teams, and AI coding agents—maximizing reusability and minimizing project-specific coupling.

## What is Skill Portability?

**Portable Skills** can be:
- Copied to another similar project and work immediately
- Shared across teams working on similar tech stacks
- Used by different AI agents (VS Code Copilot, CLI, future agents)

**Non-Portable Skills** are:
- Hard-coded to one specific project structure
- Reference internal-only documentation
- Assume undocumented conventions
- Use absolute paths

## Levels of Portability

### Level 1: Single-Project Skills

**Scope**: Works only in this specific project
**Use Case**: Highly project-specific workflows

**Characteristics**:
- References project-specific structure (e.g., `src/Modules/CoreModule/...`)
- Assumes knowledge of project conventions
- May reference project-specific docs (AGENTS.md, ADRs)

**Example**: Skill for migrating this project's legacy code to new architecture.

### Level 2: Tech-Stack-Portable Skills

**Scope**: Works across projects using same tech stack
**Use Case**: Technology-specific workflows (e.g., .NET, React, Python)

**Characteristics**:
- References standard tools (dotnet CLI, npm, pip)
- Uses common patterns (Clean Architecture, DDD, REST)
- Assumes typical project structures (src/, tests/, docs/)

**Example**: `nuget-manager` (works on any .NET project with .csproj files)

### Level 3: Universal Skills

**Scope**: Works across any project, any tech stack
**Use Case**: Language/framework-agnostic workflows

**Characteristics**:
- No tech-stack assumptions
- Universal concepts (documentation, versioning, git)
- Works with any directory structure

**Example**: `adr-writer` (ADRs are tech-stack agnostic)

## Designing for Portability

### Strategy 1: Document Assumptions in Prerequisites

Instead of assuming project structure, document it:

**Non-Portable (Assumes Structure)**:
```markdown
## Workflows

### Step 1: Create Aggregate

Create file at: `src/Modules/CoreModule/CoreModule.Domain/Model/[Entity]Aggregate/[Entity].cs`
```

**Portable (Documents Assumption)**:
```markdown
## Prerequisites

- Project uses modular structure: `src/Modules/[Module]/[Module].[Layer]/`
- Domain models live in: `[Module].Domain/Model/[Entity]Aggregate/`

## Workflows

### Step 1: Create Aggregate

Create file at: `src/Modules/[Module]/[Module].Domain/Model/[Entity]Aggregate/[Entity].cs`
Replace `[Module]` with your module name (e.g., CoreModule, CatalogModule)
```

**Why Portable**: New user knows what structure is expected, can adapt if their structure differs.

### Strategy 2: Use Relative Paths, Not Absolute

**Non-Portable**:
```bash
cd C:\Users\Alice\Projects\MyApp\src\
dotnet build
```

**Portable**:
```bash
cd src/
dotnet build
```

**Why**: Works regardless of where user cloned repository.

### Strategy 3: Reference Standard Tools, Document Versions

**Non-Portable**:
```markdown
Run the build script: `./build.ps1`
```

**Portable**:
```markdown
## Prerequisites

- .NET 10 SDK installed (or .NET 8+ compatible)
- PowerShell 7+ (for build script)

## Workflows

Run the build script: `./build.ps1`
Or use standard dotnet CLI: `dotnet build`
```

**Why**: User knows what's required, has fallback if custom script doesn't exist.

### Strategy 4: Provide Templates, Not Hard-Coded Code

**Non-Portable (Hard-Coded)**:
```markdown
Create this file:

\`\`\`csharp
namespace MyCompany.MyApp.Domain
{
    public class Product : AggregateRoot<Guid>
    {
        // ...
    }
}
\`\`\`
```

**Portable (Template)**:
```markdown
Create file using this template:

[See: templates/aggregate-template.cs]

Replace:
- `[Namespace]` with your project's root namespace
- `[Entity]` with your aggregate name
- `[IdType]` with Guid, int, or custom ID type
```

**Why**: Template adapts to different namespaces, naming conventions.

### Strategy 5: Reference External Standards, Not Internal Docs

**Non-Portable**:
```markdown
## References

- See AGENTS.md for architecture rules
- See .github/copilot-instructions.md for conventions
```

**Portable**:
```markdown
## References

- Clean Architecture: https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html
- Domain-Driven Design: https://martinfowler.com/bliki/DomainDrivenDesign.html
- Project-Specific: AGENTS.md (if using this project template)
```

**Why**: External links work for anyone, project-specific docs are optional.

### Strategy 6: Detect Project Context, Provide Guidance

**Non-Portable**:
```markdown
Versions are managed in Directory.Packages.props.
```

**Portable**:
```markdown
### Determine Version Management

Check if project uses centralized package management:

\`\`\`bash
ls Directory.Packages.props
\`\`\`

- **If file exists**: Versions managed centrally (update in Directory.Packages.props)
- **If file missing**: Versions managed per-project (update in .csproj files)
```

**Why**: Detects project convention, adapts workflow accordingly.

## Portability Checklist

### Paths & Structure

- [ ] **No absolute paths**: Use relative paths from repository root
- [ ] **Structure documented**: Prerequisites explain expected directory layout
- [ ] **Adaptable placeholders**: Use `[Module]`, `[Entity]`, `[Namespace]` not hard-coded values
- [ ] **Fallback options**: Provide alternatives if custom structure differs

### Tools & Dependencies

- [ ] **Standard tools**: Reference dotnet CLI, git, npm (not custom scripts)
- [ ] **Version requirements**: Specify minimum versions (e.g., ".NET 8+")
- [ ] **Installation instructions**: Link to official install guides
- [ ] **Verification commands**: Show how to check tool is installed

### Conventions & Standards

- [ ] **External references**: Link to industry standards (REST, DDD, Clean Architecture)
- [ ] **Pattern documentation**: Explain patterns (Factory Method, Repository, etc.)
- [ ] **No assumed knowledge**: Document conventions used by skill
- [ ] **Project-specific optional**: Internal docs referenced as optional, not required

### Templates & Examples

- [ ] **Parameterized templates**: Use placeholders, not hard-coded values
- [ ] **Multiple variants**: Show examples for different scenarios
- [ ] **Generic naming**: Use Entity, Product, Customer (not proprietary domains)
- [ ] **Language neutrality**: If possible, avoid language-specific idioms

## Testing Portability

### Test 1: Fresh Project Test

**Setup**:
1. Create new project (different structure from original)
2. Copy skill to `.github/skills/[skill-name]/`
3. Attempt to use skill

**Pass Criteria**:
- [ ] Prerequisites clearly state what's needed
- [ ] User can adapt workflow to their structure
- [ ] Templates work with minor placeholder replacements
- [ ] No references to files that don't exist

### Test 2: Different Tech Stack Test (if applicable)

**Setup**:
1. Use skill in project with different tech stack (e.g., .NET 6 vs .NET 10)
2. Check if skill specifies version compatibility

**Pass Criteria**:
- [ ] Prerequisites specify version requirements
- [ ] Skill notes if features require specific versions
- [ ] Fallbacks provided for older versions

### Test 3: External User Test

**Setup**:
1. Give skill to someone unfamiliar with your project
2. Ask them to follow workflow

**Pass Criteria**:
- [ ] User understands task without prior context
- [ ] User knows what to install (tools, SDKs)
- [ ] User can complete workflow with only skill documentation
- [ ] User doesn't encounter "file not found" errors

## Portability Patterns by Skill Type

### Tool-Focused Skills (High Portability)

**Examples**: nuget-manager, git-workflow, docker-manager

**Strategy**:
- Wrap standard CLI tools (dotnet, git, docker)
- Document tool versions
- Detect project structure (centralized vs per-project package management)

**Portability Score**: ⭐⭐⭐⭐⭐ (works on any project using that tool)

### Workflow Skills (Medium Portability)

**Examples**: adr-writer, changelog-generator, migration-manager

**Strategy**:
- Assume standard file locations (docs/ADR/, CHANGELOG.md, migrations/)
- Document expected structure in Prerequisites
- Provide examples showing typical locations

**Portability Score**: ⭐⭐⭐⭐ (works on projects following conventions)

### Code Scaffolding Skills (Lower Portability)

**Examples**: domain-add-aggregate, api-endpoint-generator

**Strategy**:
- Document architecture assumptions (Clean Architecture, modular structure)
- Use parameterized templates
- Reference external architecture docs (Uncle Bob, Martin Fowler)
- Provide adaptation guidance

**Portability Score**: ⭐⭐⭐ (works on projects using same architecture patterns)

### Project-Specific Skills (Minimal Portability)

**Examples**: legacy-migration, custom-deployment-workflow

**Strategy**:
- Accept that skill is project-specific
- Document all assumptions clearly
- Provide "porting guide" if someone wants to adapt to their project

**Portability Score**: ⭐ (works only on this project, but well-documented)

## Portability vs Specificity Trade-Off

### High Portability = Less Specific

**Pros**:
- Works on many projects
- Easy to share and reuse
- Low maintenance (no project-specific changes)

**Cons**:
- Can't assume project structure
- Must document all conventions
- May be less efficient (can't optimize for specific project)

### High Specificity = Less Portable

**Pros**:
- Optimized for this project
- Can leverage project-specific tools/scripts
- Less documentation needed (assumptions are known)

**Cons**:
- Works only on this project
- Hard to share with other teams
- Breaks if project structure changes

### Optimal Balance

**For Skills Shared Across Projects**: Favor portability
- Use Level 2 (Tech-Stack-Portable) or Level 3 (Universal)
- Document assumptions thoroughly
- Provide adaptation guidance

**For Skills Internal to One Project**: Favor specificity
- Use Level 1 (Single-Project)
- Leverage project structure directly
- Still document assumptions (for new team members)

## Portability Anti-Patterns

### Anti-Pattern 1: Hard-Coded Paths

**WRONG**:
```markdown
Create file at: `C:\Projects\MyApp\src\Domain\Customer.cs`
```

**CORRECT**:
```markdown
Create file at: `src/Domain/[Entity].cs`
Replace `[Entity]` with your aggregate name.
```

### Anti-Pattern 2: Assumed Knowledge

**WRONG**:
```markdown
## Workflows

### Step 1: Create Aggregate
Create the aggregate as usual.
```

**CORRECT**:
```markdown
## Workflows

### Step 1: Create Aggregate

Create aggregate class with:
- Factory method returning Result<T>
- Private constructors
- Domain events registered

[See: templates/aggregate-template.cs]
```

### Anti-Pattern 3: Internal-Only References

**WRONG**:
```markdown
## References

- See Confluence page: https://internal.company.com/wiki/architecture
- Read team Slack discussion: #architecture channel
```

**CORRECT**:
```markdown
## References

- Clean Architecture: https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html
- Project-Specific: Internal wiki (if available at https://internal.company.com/wiki/architecture)
```

### Anti-Pattern 4: No Version Requirements

**WRONG**:
```markdown
## Prerequisites

- .NET SDK installed
```

**CORRECT**:
```markdown
## Prerequisites

- .NET 10 SDK installed (or .NET 8+ compatible)
- Verify: `dotnet --version` (should show 8.0.0 or higher)
- Install: https://dotnet.microsoft.com/download
```

## Gradual Portability Improvement

### Phase 1: Make Skill Work (Ignore Portability)

- Write skill for your specific project
- Hard-code paths, assume structure
- Get workflow working end-to-end

### Phase 2: Document Assumptions (Basic Portability)

- Add Prerequisites section
- Document expected structure
- Note required tools and versions

### Phase 3: Parameterize (Medium Portability)

- Replace hard-coded values with placeholders
- Extract templates
- Add decision points (detect structure variants)

### Phase 4: Generalize (High Portability)

- Remove project-specific assumptions
- Reference external standards, not internal docs
- Test with different projects

### Phase 5: Publish (Maximum Portability)

- Test with external users
- Refine based on feedback
- Share publicly (GitHub, skill marketplace)

## Conclusion

**Portability is a spectrum**:
- Level 1: Project-specific (works on one project)
- Level 2: Tech-stack-portable (works on similar projects)
- Level 3: Universal (works on any project)

**Key Strategies**:
1. Document assumptions in Prerequisites
2. Use relative paths, not absolute
3. Reference standard tools with versions
4. Provide templates, not hard-coded code
5. Link external standards, not internal docs
6. Detect project context, adapt workflow

**Balance**: Favor portability for shared skills, accept specificity for internal skills—but always document assumptions.

## References

- Portable Skill Examples: `.github/skills/adr-writer/`, `.github/skills/nuget-manager/`
- agentskills.io Standard: `.github/skills/skill-creator/docs/agent-skills-standard.md`
- Prerequisites Documentation: `.github/skills/skill-creator/templates/basic-skill-template.md`
