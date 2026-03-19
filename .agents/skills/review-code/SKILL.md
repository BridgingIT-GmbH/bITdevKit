---
name: review-code
description: Perform comprehensive csharp/dotnet code reviews focusing on clean code, security, testing, performance, and documentation
---

# Code Review - C# and .NET

## Overview

This skill provides comprehensive code review guidelines for GitHub Copilot focused on C# and .NET development. It follows industry best practices and provides a structured approach to evaluating code quality, security, testing, performance, and architecture.

**What This Skill Does**:

- Performs systematic code reviews using priority-based checklists
- Identifies critical security vulnerabilities and correctness issues
- Evaluates test coverage and quality
- Checks for performance bottlenecks and anti-patterns
- Validates documentation and code clarity
- Ensures compliance with project `.editorconfig` standards

**When to Apply**: Use this skill when reviewing pull requests, conducting code audits, or validating code quality before merge. This skill is **on-demand** (not automatic) - invoke it explicitly when performing code reviews.

## Review Priorities

When performing a code review, prioritize issues in the following order:

### 🔴 CRITICAL (Block merge)

Issues that **must** be fixed before merging. These represent serious risks to security, correctness, or system stability.

- **Security Vulnerabilities**:
  - Exposed secrets (API keys, passwords, connection strings hardcoded in code)
  - Authentication/authorization bypasses or weaknesses
  - Injection vulnerabilities (SQL injection, command injection)
  - Insecure cryptography or weak random number generation
  - Missing input validation on external data

- **Correctness Issues**:
  - Logic errors that cause incorrect behavior
  - Data corruption risks (improper transaction handling, race conditions)
  - Null reference exceptions (missing null checks on nullable types)
  - Off-by-one errors, incorrect loop bounds
  - Type conversion errors that lose data

- **Breaking Changes**:
  - Public API contract changes without versioning
  - Breaking interface modifications without migration path
  - Removal of public members still in use

- **Data Loss Risks**:
  - Operations that can permanently delete data without confirmation
  - Missing database transactions for multi-step operations
  - Improper error handling that could corrupt state

- **Resource Management**:
  - Undisposed `IDisposable` objects (DbContext, streams, HttpClient)
  - Memory leaks in long-running processes (event handler leaks, static references)
  - Connection pool exhaustion (improper connection management)

- **.editorconfig Violations (ERROR level)**:
  - File-scoped namespace violations (`csharp_style_namespace_declarations = file_scoped:error`)
  - `var` usage violations (`csharp_style_var_*:error`)
  - Using directive placement violations (`csharp_using_directive_placement = inside_namespace:error`)

### 🟡 IMPORTANT (Requires discussion)

Issues that **should** be addressed but may not block merge if there's a valid reason to defer.

- **Code Quality**:
  - Severe SOLID principle violations (God classes, tight coupling)
  - Excessive code duplication (copy-paste code in multiple locations)
  - Overly complex methods (cyclomatic complexity > 10, nesting > 3 levels)
  - Anemic domain models (classes with only properties, no behavior)

- **Test Coverage**:
  - Missing tests for critical business logic paths
  - No tests for new functionality being added
  - Tests that don't actually verify behavior (assertion-free tests)
  - Brittle tests with tight coupling to implementation details

- **Performance Issues**:
  - Obvious bottlenecks (N+1 queries, synchronous I/O in loops)
  - Inefficient algorithms (O(n²) when O(n) is possible)
  - Boxing/unboxing in hot paths
  - Excessive allocations (string concatenation in loops)

- **Architecture Violations**:
  - Cross-layer dependencies (breaking Clean Architecture boundaries)
  - Circular dependencies between components
  - Improper separation of concerns

- **Async/Await Issues**:
  - Blocking on async code (`.Result`, `.Wait()`) - can cause deadlocks
  - Missing async propagation (sync wrapper around async code)
  - Async void methods (except event handlers)
  - Missing `CancellationToken` parameters in async methods

### 🟢 SUGGESTION (Non-blocking improvements)

Improvements that enhance code quality but don't require immediate action.

- **Readability**:
  - Poor naming (unclear variable/method names)
  - Complex expressions that could be simplified
  - Missing intermediate variables for clarity
  - Long parameter lists (> 4 parameters)

- **Optimization Opportunities**:
  - Places where `Span<T>` could improve performance
  - Opportunities for `ValueTask<T>` over `Task<T>`
  - Collection expressions (C# 12+) for cleaner syntax
  - `ReadOnlySpan<T>` for string operations

- **Best Practices**:
  - Minor deviations from C# conventions
  - Opportunities for readonly fields
  - Missing const for compile-time constants
  - Inconsistent code style (handled by formatters)

- **Documentation**:
  - Missing XML documentation comments for public APIs
  - Inadequate `<summary>` descriptions
  - Missing `<param>` or `<returns>` tags
  - No examples for complex APIs

- **Modern C# Features**:
  - Opportunities to use pattern matching
  - Record types for immutable data
  - File-scoped namespaces (if not already enforced)
  - Collection expressions
  - Primary constructors

## General Review Principles

Follow these principles when conducting code reviews:

### 1. Be Specific

- **Reference exact locations**: File names, line numbers, method names
- **Quote the code**: Include the problematic code snippet in your comment
- **Avoid vague statements**: Instead of "this could be better", say "this method has cyclomatic complexity of 15, consider extracting the validation logic into separate methods"

### 2. Provide Context

- **Explain WHY**: Don't just say what's wrong, explain the impact
- **Describe consequences**: "This can cause a deadlock in ASP.NET contexts"
- **Link to resources**: Reference documentation, ADRs, or best practice articles
- **Consider the bigger picture**: How does this fit into the overall architecture?

### 3. Suggest Solutions

- **Show corrected code**: Provide a "CORRECT" example alongside the "WRONG" code
- **Offer alternatives**: "Consider using Pattern A or Pattern B"
- **Explain trade-offs**: "This approach is faster but uses more memory"
- **Make it actionable**: Give clear steps to fix the issue

### 4. Be Constructive

- **Focus on the code, not the person**: "This method could be simplified" not "You wrote this poorly"
- **Assume positive intent**: The author likely has reasons for their approach
- **Ask questions**: "Have you considered...?" rather than "You should..."
- **Acknowledge constraints**: Time, knowledge, or requirements may limit options

### 5. Recognize Good Practices

- **Highlight excellent code**: "Great use of the specification pattern here!"
- **Acknowledge improvements**: "This is much cleaner than the previous version"
- **Learn from others**: Good code reviews benefit both reviewer and author
- **Build team knowledge**: Share interesting patterns you discover

### 6. Be Pragmatic

- **Not everything needs immediate fixing**: Distinguish between must-fix and nice-to-have
- **Consider the PR scope**: Don't demand unrelated refactoring
- **Balance perfection and progress**: "This could be optimized further, but it's acceptable for now"
- **Technical debt is okay**: Sometimes it's appropriate to defer improvements

### 7. Group Related Comments

- **Avoid scattered feedback**: Group all comments about the same issue
- **Provide a summary**: "There are 5 places with similar error handling issues"
- **Use checklists**: Link to relevant checklists for systematic issues
- **Offer to pair program**: For complex issues, offer to work together

## Quick Reference to Checklists

Use these checklists for systematic code review:

### Code Quality Checklist

**File**: `checklists/01-code-quality.md`

**Focus Areas**:

- ✅ Naming conventions (PascalCase, camelCase, interfaces with `I` prefix)
- ✅ **File-scoped namespaces** (🔴 MANDATORY - enforced by `.editorconfig`)
- ✅ **`var` usage** (🔴 MANDATORY - enforced by `.editorconfig`)
- ✅ **Using directive placement** (🔴 MANDATORY - inside namespace)
- ✅ Method size and complexity (< 20 lines, < 3 nesting levels)
- ✅ DRY principle (no code duplication)
- ✅ Magic numbers/strings (extract to constants)
- ✅ Modern C# features (pattern matching, records, collection expressions)

**When to Use**: Every code review should check these fundamentals.

### Security Checklist

**File**: `checklists/02-security.md`

**Focus Areas**:

- ✅ No hardcoded secrets (🔴 CRITICAL - connection strings, API keys, passwords)
- ✅ Configuration usage (IConfiguration, IOptions<T>)
- ✅ Input validation (validate all external inputs)
- ✅ SQL injection prevention (🔴 CRITICAL - parameterized queries only)
- ✅ Cryptography best practices (use BCL classes, no custom crypto)
- ✅ Dependency vulnerabilities (keep packages updated)

**When to Use**: Always check when code handles sensitive data, external inputs, or database operations.

### Testing Checklist

**File**: `checklists/03-testing.md`

**Focus Areas**:

- ✅ Test coverage (> 80% for critical paths)
- ✅ Test naming (`Should_ExpectedBehavior_When_Condition`)
- ✅ AAA pattern (Arrange-Act-Assert with clear separation)
- ✅ Test independence (no shared state)
- ✅ Edge cases (null, empty, boundary conditions)
- ✅ Mocking strategies (NSubstitute for dependencies)
- ✅ xUnit patterns (Fact vs Theory, Shouldly assertions)

**When to Use**: When reviewing test code or checking if new features have adequate tests.

### Performance Checklist

**File**: `checklists/04-performance.md`

**Focus Areas**:

- ✅ Async/await usage (for I/O-bound operations)
- ✅ No blocking calls (🔴 CRITICAL - avoid `.Result`, `.Wait()`)
- ✅ CancellationToken propagation (include in async methods)
- ✅ Resource disposal (🔴 CRITICAL - using statements for IDisposable)
- ✅ String operations (StringBuilder in loops)
- ✅ Collection choices (List, Dictionary, HashSet)
- ✅ Span<T> opportunities (high-performance scenarios)

**When to Use**: When reviewing performance-critical code, async code, or long-running operations.

### Documentation Checklist

**File**: `checklists/05-documentation.md`

**Focus Areas**:

- ✅ XML documentation on public APIs (🔴 MANDATORY - `<summary>`, `<param>`, `<returns>`)
- ✅ Exception documentation (`<exception>` tags) only for public methods and methods that throw and do not return a Result<T> or Result
- ✅ Complex logic comments (explain WHY, not WHAT)
- ✅ README updates (document new features)
- ✅ API documentation (OpenAPI/Swagger summaries)

**When to Use**: When reviewing public APIs or complex domain logic.

## .editorconfig Integration

This project enforces code style through `.editorconfig` rules. The following rules are **MANDATORY** (error level) and violations are 🔴 CRITICAL:

### File-Scoped Namespaces (🔴 CRITICAL)

**Rule**: `csharp_style_namespace_declarations = file_scoped:error`

**What it means**: All C# files **must** use file-scoped namespace syntax (not block-scoped).

**Example**:

```csharp
// 🔴 WRONG: Block-scoped namespace (CRITICAL VIOLATION)
namespace MyApp.Services {
    public class PaymentService { }
}

// ✅ CORRECT: File-scoped namespace (MANDATORY)
namespace MyApp.Services;

public class PaymentService { }
```

**Why it matters**: File-scoped namespaces reduce indentation, improve readability, and are the modern C# convention (C# 10+).

**How to fix**: Convert all block-scoped namespaces to file-scoped. Run `dotnet format` to auto-fix.

### Var Usage (🔴 CRITICAL)

**Rules**:

- `csharp_style_var_elsewhere = true:error`
- `csharp_style_var_for_built_in_types = true:error`
- `csharp_style_var_when_type_is_apparent = true:error`

**What it means**: **Always** use `var` for local variables (unless there's a specific reason not to).

**Example**:

```csharp
// 🔴 WRONG: Explicit type when obvious (CRITICAL VIOLATION)
Customer customer = new Customer();
int count = 42;
List<string> names = new List<string>();

// ✅ CORRECT: Use var (MANDATORY)
var customer = new Customer();
var count = 42;
var names = new List<string>();
```

**Why it matters**: `var` reduces verbosity, improves maintainability (when types change), and is the modern C# convention.

**How to fix**: Replace explicit types with `var`. Run `dotnet format` to auto-fix.

### Using Directive Placement (🔴 CRITICAL)

**Rule**: `csharp_using_directive_placement = inside_namespace:error`

**What it means**: `using` directives **must** be placed inside the namespace (after the namespace declaration).

**Example**:

```csharp
// 🔴 WRONG: Using directives outside namespace (CRITICAL VIOLATION)
using System;
using System.Collections.Generic;

namespace MyApp.Services;

public class PaymentService { }

// ✅ CORRECT: Using directives inside namespace (MANDATORY)
namespace MyApp.Services;

using System;
using System.Collections.Generic;

public class PaymentService { }
```

**Why it matters**: Placement inside namespace prevents naming conflicts and follows project conventions.

**How to fix**: Move `using` directives after the namespace declaration. Run `dotnet format` to auto-fix.

### How to Verify Compliance

**Run formatter**:

```bash
dotnet format
```

This will automatically fix most `.editorconfig` violations including the three CRITICAL rules above.

**Check for violations** (dry run):

```bash
dotnet format --verify-no-changes
```

This reports violations without modifying files. Use in CI/CD pipelines to block PRs with violations.

### Common .editorconfig Violations

See `examples/editorconfig-compliance.md` for detailed examples of:

- File-scoped namespace violations and fixes
- Var usage violations and fixes
- Using directive placement violations and fixes
- Other common style violations

## Quick Reference to Examples

Use these example files for code patterns:

### Clean Code Examples

**File**: `examples/clean-code-examples.md`

**Contains**:

- File-scoped namespace patterns (WRONG vs CORRECT)
- `var` usage patterns
- Expression-bodied members
- Pattern matching
- Guard clauses
- Private setters (encapsulation)
- Null-conditional operators
- String interpolation
- Collection expressions

**When to Use**: Reference when reviewing code style, naming, or C# idioms.

### Security Examples

**File**: `examples/security-examples.md`

**Contains**:

- Hardcoded secrets (WRONG vs CORRECT)
- SQL injection prevention
- Configuration patterns (IConfiguration, IOptions<T>)
- Input validation (FluentValidation)
- API key management

**When to Use**: Reference when reviewing security-sensitive code.

### Testing Examples

**File**: `examples/testing-examples.md`

**Contains**:

- Test naming patterns (WRONG vs CORRECT)
- AAA pattern examples
- Fact vs Theory usage
- Shouldly assertions
- NSubstitute mocking patterns

**When to Use**: Reference when reviewing test code or suggesting test improvements.

### .editorconfig Compliance Examples

**File**: `examples/editorconfig-compliance.md`

**Contains**:

- Detailed .editorconfig rule explanations
- File-scoped namespace violations and fixes
- Var usage violations and fixes
- Using directive placement violations and fixes
- How to verify compliance with `dotnet format`

**When to Use**: Reference when identifying or fixing .editorconfig violations.

## Review Templates

Use these templates for consistent, actionable review comments.

### Comment Template

**File**: `templates/review-comment-template.md`

**Provides**:

- 🔴 CRITICAL comment format (Issue, Why This Matters, Suggested Fix, Reference)
- 🟡 IMPORTANT comment format
- 🟢 SUGGESTION comment format
- Examples for each priority level

**When to Use**: When writing review comments to ensure they're specific, contextual, and actionable.

### Summary Template

**File**: `templates/review-summary-template.md`

**Provides**:

- Issues Found (count by priority: 🔴🟡🟢)
- Top 3 Priorities
- Overall Assessment
- Next Steps checklist

**When to Use**: At the end of a review to summarize findings and provide clear next steps.

## Example Workflow

Here's a step-by-step workflow for conducting a comprehensive code review:

### Step 1: Start with Critical Issues (🔴)

**Focus**: Security, correctness, resource management, .editorconfig ERROR violations

**Process**:

1. Scan for hardcoded secrets (search for "password", "apikey", "connectionstring")
2. Check for SQL injection risks (look for string concatenation in queries)
3. Verify resource disposal (IDisposable objects have `using` statements)
4. Check for blocking async calls (search for `.Result`, `.Wait()`)
5. Verify .editorconfig compliance (file-scoped namespaces, var usage, using placement)

**Action**: Flag any CRITICAL issues immediately. These **must** be fixed before merge.

### Step 2: Review Code Quality (🟡)

**Focus**: SOLID principles, test coverage, architecture, async patterns

**Process**:

1. Check method complexity (are methods < 20 lines, < 3 nesting levels?)
2. Look for code duplication (is the same logic repeated in multiple places?)
3. Verify test coverage (are new features tested? Are critical paths covered?)
4. Check async/await usage (is CancellationToken propagated? Is async used for I/O?)
5. Review architecture boundaries (are layer dependencies correct?)

**Action**: Discuss IMPORTANT issues with the author. Decide if they block merge or can be deferred.

### Step 3: Suggest Improvements (🟢)

**Focus**: Readability, modern C# features, documentation, optimizations

**Process**:

1. Check naming (are variables/methods clearly named?)
2. Look for modern C# opportunities (pattern matching, records, collection expressions)
3. Verify documentation (do public APIs have XML comments?)
4. Identify optimization opportunities (Span<T>, ValueTask<T>)
5. Check for readability improvements (complex expressions that could be simplified)

**Action**: Provide SUGGESTIONS for improvements. Mark as non-blocking.

### Step 4: Use Checklists

Reference the appropriate checklists based on the code being reviewed:

- **All reviews**: Code Quality Checklist
- **Sensitive data/external inputs**: Security Checklist
- **Test code**: Testing Checklist
- **Async code/performance-critical**: Performance Checklist
- **Public APIs**: Documentation Checklist

### Step 5: Provide Examples

When suggesting changes, reference the example files:

- **Style issues**: Reference `examples/clean-code-examples.md`
- **Security issues**: Reference `examples/security-examples.md`
- **Test issues**: Reference `examples/testing-examples.md`
- **.editorconfig violations**: Reference `examples/editorconfig-compliance.md`

### Step 6: Write Comments Using Templates

Use the comment template (`templates/review-comment-template.md`) to structure feedback:

- Start with priority emoji (🔴🟡🟢)
- Include: Issue, Why This Matters, Suggested Fix, Reference
- Provide corrected code examples
- Link to documentation or examples

### Step 7: Summarize Findings

Use the summary template (`templates/review-summary-template.md`) to wrap up:

- List issues found by priority
- Highlight top 3 priorities
- Provide overall assessment
- List actionable next steps

### Example Review Comment

```markdown
🔴 CRITICAL - Security: Hardcoded Database Password

**Issue**: Line 42 contains a hardcoded database password in the connection string.

**Why This Matters**: Hardcoded secrets in source code are a critical security vulnerability. They are visible in version control, can be accidentally exposed, and cannot be rotated without code changes.

**Suggested Fix**:
```csharp
// Current (WRONG):
var connectionString = "Server=myserver;Database=mydb;User Id=admin;Password=secret123;";

// Corrected (CORRECT):
var connectionString = this.configuration.GetConnectionString("MyDatabase");
```

Then store the actual connection string in:

- `appsettings.json` (for local dev, not committed)
- Azure Key Vault (for production)
- Environment variables (for containers)

**Reference**: See `examples/security-examples.md` for more secure configuration patterns.

```

## Integration with Other Skills

This skill works well with other repository skills:

### domain-add-aggregate
When reviewing new domain aggregates, use both skills:
- **review-code**: Check code quality, security, testing
- **domain-add-aggregate**: Verify DDD patterns, layer boundaries, aggregate structure

### review-architecture
For architectural reviews, use both skills:
- **review-code**: Focus on implementation details, security, performance
- **review-architecture**: Focus on DDD patterns, Clean Architecture boundaries, CQRS

### adr-writer
When identifying architectural issues, reference ADRs or create new ones:
- Use **review-code** to identify patterns that should be documented
- Use **adr-writer** to document architectural decisions

## Core Rules

1. **ALWAYS start with CRITICAL issues** (🔴): Security and correctness come first
2. **ALWAYS check .editorconfig compliance**: File-scoped namespaces, var usage, using placement are MANDATORY
3. **ALWAYS provide context**: Explain WHY something is an issue, not just WHAT
4. **ALWAYS suggest solutions**: Show corrected code, don't just point out problems
5. **ALWAYS be specific**: Reference exact locations, methods, line numbers
6. **ALWAYS use priority indicators**: 🔴🟡🟢 to clearly communicate severity
7. **ALWAYS be constructive**: Focus on improving code, not criticizing the author
8. **NEVER demand unrelated refactoring**: Keep feedback scoped to the PR
9. **NEVER block on style issues**: Use formatters (`dotnet format`) for style consistency
10. **NEVER forget to recognize good code**: Acknowledge excellent practices when you see them

## When to Use This Skill

**Use this skill when**:
- Reviewing pull requests before merge
- Conducting code quality audits
- Onboarding new team members (teach review standards)
- Preparing for production releases (final quality check)
- Identifying technical debt (catalog issues for future work)

**DO NOT use this skill when**:
- Code is generated (migrations, scaffolding)
- Code is third-party/vendored (not under your control)
- Review is purely architectural (use `review-architecture` instead)
- Quick bug fix in emergency (defer review to post-fix PR)

## Success Criteria

A successful code review includes:

✅ **All CRITICAL issues identified and fixed** (🔴)
✅ **IMPORTANT issues discussed with author** (🟡)
✅ **Suggestions provided for future improvements** (🟢)
✅ **.editorconfig compliance verified** (file-scoped namespaces, var usage, using placement)
✅ **Security vulnerabilities caught** (hardcoded secrets, SQL injection, input validation)
✅ **Test coverage adequate** (critical paths tested, new features have tests)
✅ **Performance bottlenecks identified** (no blocking async, proper resource disposal)
✅ **Documentation complete** (public APIs have XML comments)
✅ **Feedback is specific and actionable** (includes corrected code examples)
✅ **Review summary provided** (using template, with prioritized next steps)

## Additional Resources

- **Checklists**: See `checklists/` for systematic review guides
- **Examples**: See `examples/` for WRONG vs CORRECT code patterns
- **Templates**: See `templates/` for structured comment and summary formats
- **Project .editorconfig**: See root `.editorconfig` for complete style rules
- **C# Coding Conventions**: [Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **.NET Best Practices**: [Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/)
