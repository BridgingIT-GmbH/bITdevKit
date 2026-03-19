---
name: requirements-engineering
description: Transform vague feature ideas into lightweight, testable requirements using user stories and short acceptance criteria. Use when clarifying scope, defining expected behavior, capturing edge cases, and producing decision-ready requirements with Definition of Ready checks.
---

# Requirements Engineering

Capture what needs to be built before diving into design. This skill produces lightweight, testable requirements using user stories and short acceptance criteria, with explicit readiness quality gates.

## When to Use This Skill

Use requirements engineering when:
- Starting any new feature or project
- Clarifying ambiguous stakeholder requests
- Creating acceptance criteria for user stories
- Documenting system behavior for testing
- Ensuring all team members share understanding

## Lightweight Format

Use simple, consistent statements that are easy to review and test.

**File Naming Convention (Feature Slices)**
```text
prd-<ID>-<SLICE>-<title>.md
```
- Use a zero-padded numeric ID for `<ID>` (minimum 4 digits).
- Use per-slice ranges in steps of 100 (`0000`, `0100`, `0200`, ...).
- Assign additional PRDs within the same slice range (`0100`, `0101`, `0102`, ...).
- Use uppercase for `<SLICE>` (for example: `TASKS`, `PROJECTS`, `SETTINGS`).
- Use lowercase kebab-case for `<title>`.
- Store files under `docs/prd/`.
- A slice can have one or more PRD files.
- Create multiple PRDs for the same slice when there is more than one distinct story or workflow.
- Keep each PRD focused on a coherent requirement scope.

**Minimal Frontmatter (required)**
```yaml
---
id: PRD-<ID>
title: <Feature Slice Title>
slice: <SLICE>
status: Implemented | Partial | Pending
ticket: <Optional Issue/Tracker ID>
---
```
- Keep frontmatter minimal: only `id`, `title`, `slice`, `status`, and `ticket`.
- Match `slice` with the file naming convention.
- Use the same status legend as story status: `Implemented`, `Partial`, or `Pending`.

**User Story**
```text
As a [actor], I want [action], so that [achievement].
```

**Short Acceptance Criteria**
```text
When [action], then [outcome].
Given [context], when [action], then [outcome].
```

Each story should have 3-5 acceptance criteria and at least one edge or error criterion.

**Optional Story Additions (ASCII Diagrams)**
- Use diagrams as optional additions when behavior is stateful, multi-step, or branch-heavy.
- Prefer simple ASCII in fenced `text` code blocks.
- Diagram usage is optional; acceptance criteria remain the source of truth.

**Diagram Rules**
1. Keep diagrams plain ASCII and readable in raw Markdown.
2. Keep width near 80 characters.
3. Place global diagrams under `## Diagram` or `## Diagrams` after `## Scope`.
4. Place story-specific diagrams under the story as `Flow (ASCII)` when needed.
5. Add a short `Covers AC:` line mapping diagram to acceptance criteria.
6. Use diagrams to clarify behavior only; do not duplicate full criteria text.

## Definition of Ready (DoR)

Use this gate before marking a story as ready.

**Core mandatory checks (blockers):**
1. Name is clear, concise, and specific.
2. Story uses the actor-action-achievement format.
3. Acceptance criteria clearly define what the story must achieve.
4. Data requirements (fields, types, constraints) are identified if applicable.
5. Notes include dependencies/external input and known risks/constraints.

**Optional checks (recommended):**
1. Required assets/content/design links are attached when needed.
2. Story size estimate is captured.
3. Priority/order is explicit.

**INVEST quick check (lightweight):**
- I: Independent
- N: Negotiable
- V: Valuable
- E: Estimable
- S: Small
- T: Testable

*Note: You do not need to write out the full INVEST checklist for every story. Use it as a mental check. Only document INVEST failures (e.g., "S: Too large") in the `Ready Reason`.*

More INVEST details: [INVEST Criteria](https://scrum-master.org/en/creating-the-perfect-user-story-with-invest-criteria/)

## Ready Decision Rule

Set `Ready: Yes` only when:
1. All core mandatory DoR checks pass.
2. There is no critical INVEST failure in `V`, `S`, or `T`.

Otherwise:
- Set `Ready: No`.
- Fill `Ready Reason` with explicit missing conditions or INVEST failures.

## Step-by-Step Process

### Step 1: Capture User Stories

Format: **As a [actor], I want [action], so that [achievement]**

Focus on:
- Who is the actor?
- What action do they need?
- What achievement/outcome matters?

### Step 2: Add Short Acceptance Criteria

For each story, add 3-5 short, testable criteria.

Rules:
- Use observable outcomes.
- Keep each criterion focused on one behavior.
- Include at least one error or edge case.
- Add an ASCII diagram only if it improves clarity for transitions, timelines, or decisions.

### Step 3: Define Data & Technical Context

Capture:
- Specific data fields, types, and validation rules (e.g., regex, max length).
- Technical context (e.g., API endpoints, database changes, architectural constraints).

### Step 4: Evaluate DoR and Set Readiness

Set:
- `Ready: Yes|No`
- `Ready Reason: ...`
- `INVEST Check: I/N/V/E/S/T with short notes`

### Step 5: Validate Requirements Quality

Use this checklist:

**Completeness:**
- [ ] Key user roles identified and covered
- [ ] Normal flow scenarios covered
- [ ] Edge and error cases included
- [ ] Scope boundaries are explicit

**Clarity:**
- [ ] Criteria use plain, precise language
- [ ] Ambiguous words are avoided or defined
- [ ] Outcomes are observable

**Consistency:**
- [ ] Story and criteria format is consistent
- [ ] Terminology is consistent across sections
- [ ] No contradictory behaviors

**Testability:**
- [ ] Every criterion can be verified
- [ ] Inputs/contexts and expected outcomes are stated
- [ ] Normal and error paths are both testable

## Common Mistakes to Avoid

### Mistake 1: Vague Requirements
**Bad:** "System should be fast"
**Good:** "When a user submits search, then results appear within 2 seconds."

### Mistake 2: Implementation Details
**Bad:** "System shall use Redis for caching"
**Good:** "When users request frequently accessed data, then the response is returned quickly from cached data."

### Mistake 3: Missing Error Cases
**Bad:** Only documenting happy path
**Good:** Include at least one invalid input or failure scenario per story.

### Mistake 4: Untestable Requirements
**Bad:** "System should be user-friendly"
**Good:** "When a new user completes onboarding, then they can reach the dashboard in at most 3 clicks."

### Mistake 5: Conflicting Requirements
**Bad:** Requirements that contradict each other
**Good:** Review stories together and resolve conflicts before design.

## Examples

### Example 1: File Upload Feature

```markdown
**User Story:** As a user, I want to upload files, so that I can share documents with my team.

**Acceptance Criteria:**
1. Given the user is authenticated, when they select a supported file up to 10MB, then the upload starts.
2. When they select a file larger than 10MB, then a "file too large (max 10MB)" error appears.
3. When they select an unsupported file type, then an "unsupported format" error appears with allowed types.
4. When upload is in progress, then progress is shown as a percentage.
5. When upload completes successfully, then a success message with the uploaded file link is shown.
6. When upload fails due to network issues, then a retry option is shown.

**Data Requirements:**
- Supported Types: PDF, DOC, DOCX, XLS, XLSX, PNG, JPG, GIF
- Max Size: 20MB (hard limit)
```

### Example 2: Search Feature

```markdown
**User Story:** As a customer, I want to search products, so that I can find items quickly.

**Acceptance Criteria:**
1. When the customer enters a search term, then matching products are shown.
2. When results are found, then the result count is displayed.
3. When no results are found, then a "no products found" message with suggestions is displayed.
4. When the customer submits an empty search, then a validation message is shown.
5. When results exceed 20 items, then pagination is shown with 20 items per page.
6. When the customer searches, then results are returned within 2 seconds.

**Data Requirements:**
- Search Fields: Product name, description, category, SKU
- Min Length: 2 chars
```

## Requirements Document Template

---
id: PRD-<ID>
title: [Feature Slice Name]
slice: [SLICE]
status: Implemented | Partial | Pending
ticket: [Tracker ID/URL]
---

# Product Requirements: [Feature Slice Name]

## Overview
[Short description of the feature and why it exists]

## Scope
- In scope: [items included]
- Out of scope: [items excluded]

## Diagram (Optional)
```text
[State A] --> [State B]
```
Covers AC: [list]

## User Roles
- [Role 1]: [Description]
- [Role 2]: [Description]

## Stories

### Story 1: [Clear, specific name]
- Status: Implemented | Partial | Pending
- Ticket: [Tracker ID/URL]
- Ready: Yes | No
- Ready Reason: [Why this is ready, or missing items/INVEST failures]
- User Story: As a [actor], I want [action], so that [achievement].

Acceptance Criteria:
1. Given [context], when [action], then [outcome].
2. When [action], then [outcome].
3. When [error or edge case], then [outcome].

Flow (ASCII) (optional):
```text
User action -> system action -> outcome
```
Covers AC: [list]

Data Requirements:
- [Field Name]: [Type], [Constraints/Validation]

Notes:
- Dependencies / external input:
- Risks / constraints:
- Technical context: [e.g., API endpoints, DB changes]

### Story 2: [Clear, specific name]
- Status: Implemented | Partial | Pending
- Ticket: [Tracker ID/URL]
- Ready: Yes | No
- Ready Reason: ...
- User Story: As a [actor], I want [action], so that [achievement].

Acceptance Criteria:
1. ...
2. ...
3. ...

Data Requirements:
- ...

Notes:
- Dependencies / external input:
- Risks / constraints:
- Technical context:

## Non-Functional Notes
- Performance: [response targets]
- Security: [access and data expectations]
- Accessibility: [standards and constraints]

## Open Questions
- [Questions that need stakeholder input]