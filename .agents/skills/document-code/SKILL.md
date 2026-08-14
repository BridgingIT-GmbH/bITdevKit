---
name: document-code
description: Adds high-quality XML documentation to undocumented public C# symbols by analyzing their implementation and relevant surrounding code. Existing meaningful XML documentation and inherited documentation are preserved by default.
argument-hint: "[optional symbol or documentation focus]"
user-invocable: true
disable-model-invocation: true
---

# Document Code

Add accurate XML documentation comments to undocumented public C# APIs.

The primary purpose of this skill is to create missing documentation.

Do not rewrite existing documentation merely to improve wording, style, completeness, or consistency.

## Core Principle

Never generate documentation from signatures alone.

Before documenting a member, inspect its implementation and understand its observable behavior.

Analyze, where relevant:

- execution flow
- validation behavior
- side effects
- state changes
- dependencies
- exception conditions
- asynchronous behavior
- cancellation behavior
- result semantics
- retry behavior
- transactional behavior
- persistence interactions
- ordering guarantees
- lifecycle implications
- observable outcomes

Documentation must describe what the code actually does.

If behavior cannot be determined confidently from the available code, document only what can be verified.

Never invent behavior.

## Target Safety

Prefer the narrowest identifiable documentation target.

A focused or selected symbol always wins over the current file.

Never expand a symbol-level request into whole-file documentation.

When uncertain whether the invocation targets one symbol or the whole file,
prefer the single symbol when focused code context exists.

---

# Target Selection

Determining the correct target is critical.

Never process the whole file merely because no symbol name was explicitly
provided in the user's command.

Use the following priority.

## 1. Explicit Symbol

If the user explicitly names a symbol, document only that symbol.

Example:

```text
/document-code WaitForTokenAsync
```

## 2. Selected or Focused Code Context

If the invocation contains selected or focused editor code, treat that code
as the target.

Determine the C# symbol that contains the provided code and document only
that symbol.

The provided editor context takes precedence over whole-file mode.

This applies even when:

- only part of the method is selected
- only the method signature is selected
- only a few lines inside the implementation are selected
- surrounding code from the file is also available as context

Do not interpret access to the complete file as a request to document the
complete file when focused code context is also available.

## 3. Current Symbol

If the invocation context clearly identifies a single current C# symbol,
document only that symbol.

Only use this rule when the available editor context actually identifies
the current symbol.

Do not guess the current symbol from the active file alone.

## 4. Whole File

Process the whole current file only when there is genuinely no explicit,
selected, focused, or otherwise identifiable symbol context.

Before entering whole-file mode, verify that no narrower target was supplied.

In whole-file mode:

- find all public symbols in the file
- add XML documentation only to undocumented symbols
- skip meaningful existing XML documentation
- treat `<inheritdoc/>` as documented
- prefer `<inheritdoc/>` for applicable overrides and interface implementations

Do not modify private or internal symbols unless explicitly requested.

---

# Existing Documentation

This skill is primarily intended to create documentation for previously undocumented public APIs.

Before generating documentation for a symbol, determine whether it is already documented.

## Already Documented

Consider a symbol documented when it has meaningful XML documentation such as:

```csharp
/// <summary>
/// Dispatches the orchestration for background execution.
/// </summary>
/// <returns>
/// A result containing the execution identifier when dispatch succeeds.
/// </returns>
```

Leave such documentation unchanged.

Do not:

- rewrite it
- rephrase it
- normalize its style
- add remarks merely for completeness
- add examples merely because this skill normally encourages examples
- replace developer-written documentation with generated wording

Treat existing developer-written documentation as intentional.

## Inherited Documentation

`<inheritdoc/>` counts as existing documentation.

For example:

```csharp
/// <inheritdoc/>
public override Task ExecuteAsync(CancellationToken cancellationToken)
{
    ...
}
```

is already documented and must be skipped.

Do not replace `<inheritdoc/>` with duplicated documentation.

## Effectively Undocumented

Empty or meaningless documentation stubs may be treated as undocumented.

Examples:

```csharp
/// <summary>
/// TODO
/// </summary>
```

```csharp
/// <summary>
/// Method.
/// </summary>
```

```csharp
/// <summary>
/// Gets the value.
/// </summary>
```

when that text conveys no useful information beyond the signature.

Replace such placeholders with useful documentation.

## Incorrect Existing Documentation

Do not normally review existing documentation for correctness.

However, if existing documentation is clearly invalid or directly contradicts the implementation, correct only the inaccurate portion.

Do not use this as an opportunity to rewrite the entire comment.

---

# Inherited API Documentation

Avoid duplicating documentation that already exists on a base member or interface contract.

Inherited documentation takes precedence over generating new documentation.

Before generating any new XML documentation for a public member, first determine whether that member:

- overrides a base member
- implements an interface member
- explicitly implements an interface member
- inherits a documented contract that can be represented with `<inheritdoc/>`

If it does, inspect the inherited member's XML documentation before generating anything new.

## Inheritance Decision Order

For overrides and interface implementations, use this decision order:

1. Existing `<inheritdoc/>` on the implementation → skip.
2. Existing meaningful XML documentation on the implementation → skip.
3. Meaningful XML documentation exists on the implemented interface member or overridden base member → add `<inheritdoc/>`.
4. No useful inherited documentation exists → generate explicit XML documentation.

Do not generate explicit XML documentation merely because the implementation body is available.

Implementation analysis should be used to determine whether the inherited contract is insufficient, not as a reason to duplicate already documented API contracts.

When in doubt, prefer `<inheritdoc/>`.

## Interface Implementations

For every public method, property, event, indexer, or other member that implements
an interface member:

1. Resolve the corresponding interface member.
2. Inspect the interface member's XML documentation.
3. If the interface member has meaningful XML documentation and it accurately represents the implementation's public contract, add only:

```csharp
/// <inheritdoc/>
```

4. Do not copy, paraphrase, summarize, or regenerate the interface documentation.
5. Do not add duplicate `<summary>`, `<param>`, `<typeparam>`, `<returns>`,
   `<remarks>`, `<exception>`, `<example>`, or related elements.
6. Only generate explicit implementation documentation when the implementation
   exposes important observable behavior that is not adequately represented by
   the interface contract.

Example:

```csharp
public interface IOrchestrator
{
    /// <summary>
    /// Dispatches an orchestration for background execution.
    /// </summary>
    /// <param name="request">The orchestration request.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel the dispatch operation.
    /// </param>
    /// <returns>
    /// A result containing the execution identifier when dispatch succeeds.
    /// </returns>
    Task<Result<Guid>> DispatchAsync(
        OrchestrationRequest request,
        CancellationToken cancellationToken);
}
```

The implementation should normally be documented as:

```csharp
/// <inheritdoc/>
public async Task<Result<Guid>> DispatchAsync(
    OrchestrationRequest request,
    CancellationToken cancellationToken)
{
    ...
}
```

Do not generate a second copy of the interface documentation on the implementation.

## Explicit Interface Implementations

Apply the same inheritance rule to explicit interface implementations.

Example:

```csharp
/// <inheritdoc/>
Task<Result<Guid>> IOrchestrator.DispatchAsync(
    OrchestrationRequest request,
    CancellationToken cancellationToken)
{
    ...
}
```

If the implemented interface member already contains meaningful XML documentation,
prefer `<inheritdoc/>`.

## Overrides

For every public override:

1. Resolve the overridden base member.
2. Inspect its XML documentation.
3. If meaningful documentation exists and accurately represents the override's public contract, add only:

```csharp
/// <inheritdoc/>
```

4. Do not duplicate inherited documentation.

Example:

```csharp
/// <inheritdoc/>
public override async Task ExecuteAsync(CancellationToken cancellationToken)
{
    ...
}
```

## When Not to Use `<inheritdoc/>`

Generate explicit documentation instead only when the implementation introduces
important public behavior that is not adequately represented by the inherited contract.

Examples include implementation-specific:

- additional failure conditions
- externally visible side effects
- persistence behavior
- retry semantics
- cancellation semantics that differ from the inherited contract
- ordering guarantees
- lifecycle effects
- additional observable behavior callers must know

This should be the exception, not the default.

Do not choose explicit documentation simply because more implementation detail is available.

Prefer inherited documentation whenever it accurately describes the public contract.

---

# Analysis Process

For every symbol requiring documentation:

1. Determine whether the symbol overrides or implements another documented API member.
2. If so, inspect the inherited documentation first.
3. If meaningful inherited documentation accurately describes the public contract, use `<inheritdoc/>` and stop unless there is material implementation-specific behavior that callers must know.
4. Otherwise, read the complete implementation.
5. Identify the API's purpose.
6. Follow the major execution paths.
7. Inspect invoked methods when their behavior materially affects the public contract.
8. Identify validation rules.
9. Identify success outcomes.
10. Identify expected failure outcomes.
11. Identify observable side effects.
12. Identify state changes.
13. Identify persistence interactions.
14. Determine asynchronous and waiting behavior.
15. Determine cancellation semantics.
16. Determine retry behavior when applicable.
17. Determine transactional behavior when applicable.
18. Determine important ordering or lifecycle guarantees.
19. Determine how callers are expected to use the API.
20. Generate documentation based only on behavior that can be verified.

Avoid unnecessarily exploring implementation details that do not affect the public API contract.

---

# Documentation Philosophy

Good documentation explains:

- why the API exists
- what it accomplishes
- when it should be used
- important behavioral guarantees
- important limitations
- expected failure conditions
- relevant side effects
- common usage patterns

Avoid restating the signature.

Bad:

```csharp
/// <summary>
/// Gets the customer.
/// </summary>
```

Better:

```csharp
/// <summary>
/// Retrieves the customer from the repository using the specified identifier.
/// </summary>
/// <returns>
/// A successful result containing the customer when found; otherwise,
/// a failure result describing why the customer could not be retrieved.
/// </returns>
```

Documentation should add knowledge that is not already obvious from the member name and signature.

Do not duplicate knowledge that is already defined on an inherited contract when `<inheritdoc/>` can represent it.

---

# XML Elements

Use standard C# XML documentation elements where applicable:

- `<summary>`
- `<typeparam>`
- `<param>`
- `<returns>`
- `<exception>`
- `<remarks>`
- `<example>`
- `<code>`
- `<see cref="..."/>`
- `<seealso cref="..."/>`
- `<inheritdoc/>`

Use valid C# XML documentation syntax.

Prefer `<see cref="..."/>` when referring to C# types or members that can be referenced directly.

For interface implementations and overrides, prefer `<inheritdoc/>` over duplicating inherited XML elements.

Do not add XML elements merely to make documentation appear complete.

---

# Summary

The `<summary>` should:

- state the purpose of the API
- explain its primary observable behavior
- remain concise
- avoid implementation trivia
- avoid repeating the member name
- avoid repeating information obvious from the signature

A developer reading IntelliSense should quickly understand why and when the API is useful.

Do not generate a new summary for an interface implementation or override when meaningful inherited documentation already exists and `<inheritdoc/>` is sufficient.

---

# Parameters

Use `<param>` to explain the semantic meaning of parameters.

Document:

- what the value represents
- important constraints
- special values
- behavioral consequences where relevant

Avoid simply repeating parameter names or types.

Bad:

```csharp
/// <param name="cancellationToken">The cancellation token.</param>
```

Prefer, when behavior warrants explanation:

```csharp
/// <param name="cancellationToken">
/// A token that can be used to cancel waiting for an available execution slot.
/// </param>
```

Do not invent constraints that are not enforced or documented by the implementation.

When generating new documentation for a public type that declares a primary
constructor, always include a `<param>` element for every primary constructor
parameter.

Primary constructor parameters are part of the public construction contract
and must be documented together with the type.

For example:

```csharp
/// <summary>
/// Provides a token-bucket rate limiter that controls access based on a
/// configured event rate and burst capacity.
/// </summary>
/// <param name="eventsPerSecond">
/// The number of events permitted per second.
/// </param>
/// <param name="maxBurstSize">
/// The maximum number of tokens that may accumulate for burst processing.
/// </param>
public class RateLimiter(int eventsPerSecond, int maxBurstSize)
{
    ...
}
```

For primary constructor parameters:

- include every parameter in the newly generated XML documentation
- use the exact parameter name
- describe the semantic purpose of the parameter
- include units, limits, defaults, or behavioral effects when they can be verified
- inspect field and property initializers that consume the parameter
- inspect base-constructor arguments that consume the parameter
- inspect instance behavior that materially depends on the parameter
- do not omit a parameter because its purpose appears obvious from its name
- do not invent constraints or semantics that cannot be verified

This rule applies only when generating new documentation.

Do not revisit an already documented type merely because its existing XML
documentation omits one or more primary constructor parameters.

For interface implementations and overrides using `<inheritdoc/>`, do not duplicate
inherited parameter documentation.

---

# Generic Type Parameters

Use `<typeparam>` when the semantic role of a generic parameter is not obvious.

Explain how the type participates in the API rather than merely stating that it is a type parameter.

Do not duplicate inherited generic parameter documentation when `<inheritdoc/>` is used.

---

# Result and Result<T>

For APIs returning `Result` or `Result<T>`, document result semantics explicitly.

Describe:

- what constitutes success
- what value is available on success
- what constitutes failure
- expected failure conditions that can be determined from the implementation

Prefer:

```csharp
/// <returns>
/// A successful result containing the orchestration execution identifier.
/// Returns a failure result when the definition cannot be resolved or
/// execution cannot be started.
/// </returns>
```

Avoid:

```csharp
/// <returns>
/// The result of the operation.
/// </returns>
```

Do not describe failures represented through `Result` as CLR exceptions.

Do not duplicate inherited result documentation when `<inheritdoc/>` accurately represents the contract.

---

# Async Methods

For asynchronous APIs, document meaningful asynchronous behavior.

Where relevant, explain:

- what work is performed
- what the caller actually waits for
- whether work executes inline or in the background
- whether the method waits for completion or only dispatches work
- cancellation support
- what cancellation affects
- waiting behavior
- retry behavior

Do not add boilerplate such as:

```csharp
/// <summary>
/// Asynchronously executes the operation.
/// </summary>
```

unless asynchronous execution itself is relevant to the caller.

For overrides and interface implementations, use inherited documentation when it already describes the asynchronous contract accurately.

---

# Cancellation

When a `CancellationToken` is accepted, inspect how it is actually used.

Document relevant semantics such as:

- whether cancellation stops waiting
- whether cancellation stops active work
- whether cancellation is propagated to dependencies
- whether already-dispatched work continues
- whether cancellation produces an exception, result failure, or another outcome

Do not assume standard cancellation behavior merely because a token exists.

Do not duplicate inherited cancellation documentation unless implementation behavior materially differs from the inherited contract.

---

# Exceptions

Use `<exception>` only for exceptions that form part of observable API behavior.

Document exceptions when:

- they are explicitly thrown
- they are deliberately propagated as part of the API contract
- callers are reasonably expected to handle them

Do not list every exception that implementation details could theoretically throw.

Do not document `Result` failures as exceptions.

For inherited APIs, do not duplicate exception documentation when `<inheritdoc/>` already represents the contract.

---

# Remarks

Use `<remarks>` for non-trivial public APIs when important information exists beyond the summary.

Remarks may explain:

- execution behavior
- business rules
- state transitions
- side effects
- persistence behavior
- retry semantics
- cancellation semantics
- threading considerations
- concurrency behavior
- ordering guarantees
- lifecycle implications
- important performance characteristics
- interaction with other APIs

Do not create a `<remarks>` section merely because the symbol is public.

If the summary, parameters, and return documentation already communicate everything callers need to know, omit remarks.

For overrides and interface implementations, do not add new remarks merely because the implementation contains additional internal detail.

Only replace inheritance with explicit documentation when callers need to know behavior that is materially absent from the inherited contract.

---

# Usage Examples

Include an `<example>` when the API is directly consumed by developers and a realistic usage example materially improves understanding.

Examples are particularly useful for:

- orchestration APIs
- pipeline APIs
- builder APIs
- configuration APIs
- provider APIs
- registration APIs
- APIs with non-obvious result handling
- APIs with important lifecycle behavior

Examples should:

- demonstrate actual usage patterns
- compile where practical
- use realistic variable names
- demonstrate the common success path
- show important result handling where relevant
- remain concise
- avoid placeholder names such as `Foo`, `Bar`, or `TestService`

Example:

```csharp
/// <example>
/// <code>
/// var result = await orchestrator.DispatchAsync(
///     "OrderProcessing",
///     data,
///     cancellationToken);
///
/// if (result.IsSuccess)
/// {
///     var executionId = result.Value;
/// }
/// </code>
/// </example>
```

Do not add examples to obvious APIs where they provide little additional value.

Do not duplicate inherited examples on interface implementations or overrides when `<inheritdoc/>` is sufficient.

---

# Public Types

For public classes, records, structs, interfaces, enums, and delegates:

- explain their role in the API
- explain important lifecycle or behavioral characteristics
- explain their relationship to surrounding abstractions when useful
- avoid merely repeating the type name

For abstractions such as interfaces, describe the contract rather than an implementation.

When generating new documentation for a public class, record, struct, or record
struct with a primary constructor, include `<param>` documentation for every
primary constructor parameter as part of the type's XML comment.

Do not treat primary constructor parameters as implementation details.

---

# Constructors

For public constructors:

- explain what kind of instance is created when useful
- document dependency semantics when they matter to consumers
- document validation that occurs during construction
- document meaningful exceptions

Avoid boilerplate such as:

```csharp
/// <summary>
/// Initializes a new instance of the class.
/// </summary>
```

unless additional useful information follows.

For primary constructors, place `<param>` documentation on the containing type's
XML documentation comment.

When creating a new XML comment for a type with a primary constructor, do not
generate only `<summary>` and `<remarks>` while omitting the primary constructor
parameters.

---

# Properties

Document public properties when they form part of the public API.

For simple self-explanatory properties, keep documentation concise.

Do not force `<remarks>` or examples for trivial getters and setters.

Document important semantics such as:

- units
- valid ranges
- defaults
- mutability
- lifecycle meaning
- whether a value is calculated, cached, or persisted

only when these behaviors can be verified.

If a property implements an interface property or overrides a documented base property,
prefer `<inheritdoc/>` instead of duplicating the inherited documentation.

---

# Enums

Document public enums and their public members.

The enum summary should explain what concept the enum represents.

Individual values should explain their semantic meaning rather than simply restating the member name.

---

# Whole-File Mode

When no symbol is selected or in scope, inspect the entire current file.

Process public symbols using this sequence:

1. Identify all public API symbols.
2. Detect existing XML documentation.
3. Treat `<inheritdoc/>` as already documented.
4. For each undocumented member, determine whether it overrides or implements another member.
5. Resolve the corresponding base or interface member when applicable.
6. If meaningful inherited XML documentation exists and accurately represents the contract, add `<inheritdoc/>`.
7. Skip symbols with meaningful existing documentation.
8. Skip correctly inherited documentation.
9. Identify remaining undocumented symbols.
10. Analyze each remaining undocumented symbol independently.
11. When an undocumented type uses a primary constructor, include `<param>` documentation for every primary constructor parameter in the newly generated type comment.
12. Add only the required XML comments.

The purpose of whole-file mode is to efficiently complete missing public API documentation, not to rewrite the file's documentation.

Whole-file mode must not generate duplicate documentation on implementation classes when the corresponding interface or base member is already documented.

---

# Public API Quality Standard

Generated documentation should be suitable for:

- IntelliSense
- generated API documentation
- NuGet packages
- internal developer portals
- developers consuming the API without reading its implementation

A developer should be able to understand the public contract without having to inspect the implementation.

For inherited APIs, that contract may be supplied through `<inheritdoc/>`; duplicated XML comments are not required.

---

# Editing Rules

These rules are strict.

- Preserve implementation code exactly.
- Add XML comments primarily to previously undocumented public symbols.
- Skip symbols with meaningful existing XML documentation.
- Treat `<inheritdoc/>` as existing documentation.
- Before generating new documentation for an override or interface implementation, inspect the inherited member's XML documentation.
- If meaningful inherited documentation exists and accurately describes the contract, add `<inheritdoc/>`.
- Prefer `<inheritdoc/>` over newly generated duplicate documentation for interface implementations and overrides.
- Do not duplicate inherited documentation.
- Do not copy or paraphrase interface or base-member documentation into implementations when `<inheritdoc/>` is sufficient.
- When generating a new comment for a type with a primary constructor, include `<param>` documentation for every primary constructor parameter.
- Do not revisit existing documentation solely to add missing primary constructor parameter documentation.
- Do not rewrite documentation for stylistic consistency.
- Do not refactor code.
- Do not rename symbols.
- Do not change method signatures.
- Do not change code formatting outside XML documentation comments.
- Do not modify private or internal symbols unless explicitly requested.
- Keep comments concise but informative.
- Prefer accuracy over completeness.
- Do not invent behavior.
- Do not generate boilerplate merely to populate XML elements.
- Make the smallest documentation change necessary.
- When uncertain between inherited documentation and duplicated explicit documentation, prefer `<inheritdoc/>`.

---

# Invocation Examples

Document the symbol at the cursor:

```text
/document-code
```

Document the selected symbol:

```text
/document-code
```

Document a specific symbol:

```text
/document-code WaitForTokenAsync
```

Document missing public API comments across the current file by invoking the skill while no specific symbol is selected or in scope:

```text
/document-code
```

Add a particular emphasis when useful:

```text
/document-code focus on cancellation and retry semantics
```

Explicitly request review of existing documentation:

```text
/document-code review and improve existing documentation
```

Existing documentation should only be rewritten when such a request is explicit.