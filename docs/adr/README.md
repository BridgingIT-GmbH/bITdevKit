# Architectural Decision Records (ADRs)

This directory contains Architecture Decision Records (ADRs) for the project.

## What is an ADR?

An **Architectural Decision Record (ADR)** is a document that captures an important architectural decision made along with its context and consequences. ADRs help teams:

- **Understand past decisions**: Why was this approach chosen?
- **Onboard new team members**: Quickly learn the architectural rationale
- **Avoid rehashing old debates**: Decisions are documented with reasoning
- **Track technical debt**: Understand tradeoffs and negative consequences
- **Maintain consistency**: Follow established patterns

## ADR Format

We use the **MADR (Markdown Architectural Decision Records)** format for consistency and clarity.

## Quick Reference

| ADR | Title | Status |
|-----|-------|--------|
| [ADR-0001](0001-clean-onion-architecture.md) | Clean/Onion Architecture with Strict Layer Boundaries | Accepted |
| [ADR-0002](0002-result-pattern-error-handling.md) | Result Pattern for Error Handling | Accepted |
| [ADR-0003](0003-modular-monolith-architecture.md) | Modular Monolith Architecture | Accepted |
| [ADR-0004](0004-repository-decorator-behaviors.md) | Repository Pattern with Decorator Behaviors | Accepted |
| [ADR-0005](0005-requester-notifier-mediator-pattern.md) | Requester/Notifier (Mediator) Pattern | Accepted |
| [ADR-0006](0006-outbox-pattern-domain-events.md) | Outbox Pattern for Domain Events | Accepted |
| [ADR-0007](0007-entity-framework-core-code-first-migrations.md) | Entity Framework Core with Code-First Migrations | Accepted |
| [ADR-0008](0008-typed-entity-ids-source-generators.md) | Typed Entity IDs using Source Generators | Accepted |
| [ADR-0009](0009-fluentvalidation-strategy.md) | FluentValidation Strategy | Accepted |
| [ADR-0010](0010-mapster-object-mapping.md) | Mapster for Object Mapping | Accepted |
| [ADR-0011](0011-application-logic-in-commands-queries.md) | Application Logic in Commands/Queries | Accepted |
| [ADR-0012](0012-domain-logic-in-domain-layer.md) | Domain Logic in Domain Layer | Accepted |
| [ADR-0013](0013-unit-testing-high-coverage-strategy.md) | Unit Testing Strategy with High Coverage Goals | Accepted |
| [ADR-0014](0014-minimal-api-endpoints-dto-exposure.md) | Minimal API Endpoints with DTO Exposure | Accepted |
| [ADR-0015](0015-background-jobs-quartz-scheduling.md) | Background Jobs & Scheduling with Quartz.NET | Accepted |
| [ADR-0016](0016-logging-observability-strategy.md) | Logging & Observability Strategy (Serilog) | Accepted |
| [ADR-0017](0017-integration-testing-strategy.md) | Integration Testing Strategy | Accepted |
| [ADR-0018](0018-dependency-injection-service-lifetimes.md) | Dependency Injection & Service Lifetime Management | Accepted |
| [ADR-0019](0019-specification-pattern-repository-queries.md) | Specification Pattern for Repository Queries | Accepted |

## When to Write an ADR

Create an ADR when making decisions about:

- **Architecture patterns** (layering, modularization, communication patterns)
- **Technology choices** (frameworks, libraries, databases)
- **Cross-cutting concerns** (logging, validation, error handling, security)
- **API design** (REST vs GraphQL, versioning strategy)
- **Data persistence** (ORM choice, migration strategy)
- **Performance** (caching strategy, optimization techniques)
- **Testing** (testing strategy, test architecture)

**Don't create ADRs for**:

- Implementation details (how a specific method works)
- Tactical code decisions (variable naming, minor refactorings)
- Decisions that are easily reversible

## How to Create a New ADR

### Step 1: Copy the Template

```bash
# Copy the template below to a new file
# Use 4-digit numbering: 0013, 0014, etc.
# Use hyphen-separated naming
cp docs/ADR/TEMPLATE.md docs/ADR/0013-your-decision-title.md
```

### Step 2: Fill in the Sections

Work through each section of the template:

1. **Status**: Start with "Proposed", change to "Accepted" after review
2. **Context**: Describe the problem and forces at play
3. **Decision**: State what you're proposing clearly
4. **Rationale**: Explain *why* this is the best choice
5. **Consequences**: List positive, negative, and neutral impacts
6. **Alternatives**: Show you considered other options
7. **Related Decisions**: Link to other ADRs
8. **References**: Link to documentation, articles, discussions
9. **Notes**: Add implementation details, examples, migration notes

### Step 3: Review and Accept

- Share with the team for feedback
- Address concerns and update the ADR
- Change status to "Accepted" when approved
- Update the Quick Reference table in this README

### Step 4: Implement

- Reference the ADR number in pull requests
- Update architecture tests to enforce the decision
- Add code comments linking to the ADR for complex patterns

## MADR Template

```markdown
# ADR-XXXX: [Title]

## Status
[Proposed | Accepted | Deprecated | Superseded by ADR-YYYY]

## Context
[What is the issue we're facing? Describe the problem and the forces at play.
Include technical, business, organizational, and project context.]

## Decision
[What is the change we're proposing and/or doing? Be specific and clear.]

## Rationale
[Why did we choose this solution? What factors influenced this decision?
Explain the reasoning that led to this choice.]

## Consequences

### Positive
- [Benefit 1]
- [Benefit 2]
- [Benefit 3]

### Negative
- [Drawback 1]
- [Drawback 2]

### Neutral
- [Impact 1 - neither clearly positive nor negative]
- [Impact 2]

## Alternatives Considered

- **Alternative 1: [Name]**
  - [Brief description]
  - Rejected because [reason]

- **Alternative 2: [Name]**
  - [Brief description]
  - Rejected because [reason]

## Related Decisions
- [ADR-XXXX](XXXX-title.md): [How it relates]
- [ADR-YYYY](YYYY-title.md): [How it relates]

## References
- [Link to external documentation]
- [Link to articles or books]
- [Link to internal documentation]
- [Link to discussions or RFCs]

## Notes
[Additional context, implementation notes, migration path, code examples, etc.
This section is optional but often very useful.]

### Implementation Example
\`\`\`csharp
// Example code demonstrating the decision
\`\`\`

### Migration Path
[If this changes existing code, describe how to migrate]

### Implementation Files
- [File path 1]
- [File path 2]
```

## ADR Lifecycle

### Status Values

- **Proposed**: Decision is under discussion
- **Accepted**: Decision has been approved and should be followed
- **Deprecated**: Decision is no longer relevant but kept for historical context
- **Superseded by ADR-XXXX**: Decision replaced by a newer ADR

### Updating ADRs

**If a decision needs to change**:

1. Don't delete or heavily modify the original ADR
2. Create a new ADR that supersedes it
3. Update the original ADR's status to "Superseded by ADR-XXXX"
4. Link the new ADR back to the old one in "Related Decisions"

**For minor corrections** (typos, clarifications):

- Update the ADR directly
- These are improvements, not decision changes

## Tips for Writing Good ADRs

### Do

- Write clearly and concisely
- Focus on *why*, not just *what*
- Include code examples and diagrams
- List alternatives you considered
- Be honest about tradeoffs
- Link to related ADRs and documentation
- Update the Quick Reference table

### Don't

- Write implementation documentation (that belongs elsewhere)
- Make ADRs too long (aim for 1-2 pages)
- Skip the "Alternatives Considered" section
- Hide negative consequences
- Write ADRs for trivial decisions

## File Naming Convention

- Use 4-digit numbering: `0001`, `0002`, `0013`, etc.
- Use lowercase with hyphens: `0013-my-decision-title.md`
- Be descriptive but concise in the filename
- Match the filename to the ADR title

## Examples

Good ADR titles:

- `0001-clean-onion-architecture.md`
- `0005-requester-notifier-mediator-pattern.md`
- `0011-application-logic-in-commands-queries.md`

Bad ADR titles:

- `adr1.md` (no leading zeros, not descriptive)
- `decision_about_logging.md` (underscores instead of hyphens)
- `THE-ARCHITECTURE.md` (too vague, all caps)

## Resources

- [ADR GitHub Organization](https://adr.github.io/)
- [MADR](https://adr.github.io/madr/)
- [Joel Parker Henderson's ADR Templates](https://github.com/joelparkerhenderson/architecture-decision-record)
- [Thoughtworks Technology Radar - ADRs](https://www.thoughtworks.com/radar/techniques/lightweight-architecture-decision-records)
