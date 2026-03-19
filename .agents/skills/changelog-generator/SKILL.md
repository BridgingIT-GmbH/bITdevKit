---
name: changelog-generator
description: Automatically creates or updates changelogs from git commits by analyzing commit history, categorizing changes and transforming technical commits into clear, customer-friendly release notes. Turns hours of manual changelog writing into minutes of automated generation.
---

# Changelog Generator

This skill transforms technical git commits into polished, user-friendly changelogs that your customers and users will actually understand and appreciate.

## When to Use This Skill

- Preparing release notes for a new version
- Creating weekly or monthly product update summaries
- Documenting changes for customers
- Writing changelog entries for app store submissions
- Generating update notifications
- Creating internal release documentation
- Maintaining a public changelog/product updates page

## What This Skill Does

1. **Scans Git History**: Analyzes commits from a specific time period or between versions
2. **Categorizes Changes**: Groups commits into logical categories (features, improvements, bug fixes, breaking changes, security)
3. **Translates Technical → User-Friendly**: Converts developer commits into customer language
4. **Formats Professionally**: Creates clean, structured changelog entries
5. **Filters Noise**: Excludes internal (refactoring, tests, etc.) and documentation-only commits by default
6. **Follows Best Practices**: Applies changelog guidelines and your brand voice

## Default Filtering Policy (Product-Only)

When generating changelogs, use **strict product relevance** by default.

Include only changes that directly affect shipped behavior, user experience, API behavior, reliability, performance, security, or supported runtime/platform versions.

Exclude by default:

- Documentation-only changes (`docs:`, README, ADRs, guides, diagrams, CODE_OF_CONDUCT, AGENTS.md)
- AI-agent and skill metadata/workflow changes (`.agents/`, `SKILL.md`, prompt/instruction tuning)
- Internal housekeeping with no user impact (formatting, comment-only edits, rename-only refactors)
- Test-only changes unless they fix a production defect or prevent a real regression
- CI/CD, pipeline, and developer tooling changes unless they impact released product behavior
- Changelog-only updates (`chore(changelog)`, `docs(changelog)`)

Borderline rule:

- If uncertain whether a commit is user-visible, **exclude it** and add a short note in a separate "Excluded/Internal" review list for human confirmation.

## Commit Triage Workflow

1. Collect commits in range (between tags, dates, or `last release..HEAD`).
2. Drop merge commits and duplicate back-merges.
3. Classify each commit as `product`, `internal`, or `docs-only`.
4. Only summarize `product` commits in CHANGELOG sections.
5. Keep language user-facing; avoid internal filenames unless needed for clarity.

## How to Use

### Basic Usage

From your project repository:

```
Update the changelog with recent commits
```

```
Create a changelog from commits since last release
```

```
Generate changelog for all commits from the past week
```

```
Create release notes for version 2.5.0
```

### With Specific Date Range

```
Create a changelog for all commits between March 1 and March 15
```

### With Custom Guidelines

```
Create a changelog for commits since v2.4.0, using the changelog
guidelines from [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
```

## Example

**User**: "Create a changelog for commits from the past 7 days"

**Output**:
```markdown
# Updates - Week of March 10, 2024

## ✨ New Features

- **Team Workspaces**: Create separate workspaces for different
  projects. Invite team members and keep everything organized.

- **Keyboard Shortcuts**: Press ? to see all available shortcuts.
  Navigate faster without touching your mouse.

## 🔧 Improvements

- **Faster Sync**: Files now sync 2x faster across devices
- **Better Search**: Search now includes file contents, not just titles

## 🐛 Fixes

- Fixed issue where large images wouldn't upload
- Resolved timezone confusion in scheduled posts
- Corrected notification badge count
```

**Inspired by:** Manik Aggarwal's use case from Lenny's Newsletter

## Tips

- Run from your git repository root
- Use an existing CHANGELOG.md as a basis and add new entries to it
- Specify date ranges for focused changelogs
- Use [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) for consistent formatting
- Review and adjust the generated changelog before publishing
- Save output directly to CHANGELOG.md in the repo root.
