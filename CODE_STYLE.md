# C# Code Style Guidelines

This document delineates the coding standards for C# within this project, ensuring consistency and maintainability. The majority of formatting rules, such as indentation (4 spaces), trailing whitespace removal, and CRLF line endings, are enforced automatically via the `.editorconfig` file.

## Core Conventions

### Mandatory Use of `var`

The `var` keyword is required for all variable declarations, including built-in types (e.g., `int`), explicitly instantiated types (e.g., `var list = new List<string>()`), and cases where the type is evident. Non-compliance is treated as an error by the `.editorconfig`.

### File-Scoped Namespaces

Namespaces must be declared using the file-scoped syntax (e.g., `namespace MyApp;`). Traditional block-style namespaces with braces are prohibited, and violations are flagged as errors in the `.editorconfig`.

### Mandatory Braces

All control flow statements (e.g., `if`, `for`) must use braces, even for single-line bodies. This is a strict requirement to enhance code clarity and prevent errors, though it is not enforced as an error by the `.editorconfig`.

### Required License Header

Every C# source file must commence with the following MIT license header:

```
MIT-License
Copyright BridgingIT GmbH - All Rights Reserved
Use of this source code is governed by an MIT-style license that can be
found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
```

## Coding Practices

### Qualification with `this.`

Qualify references to events, fields, methods, and properties with `this.` to ensure explicitness. This is recommended as a suggestion-level practice in the `.editorconfig`.

### Adoption of Modern C# Features

Utilize contemporary C# constructs where applicable:

- Employ null-coalescing (`??`) and null-conditional (`?.`) operators.
- Prefer pattern matching over `as` or `is` with subsequent casts.
- Use expression-bodied syntax for properties, accessors, and lambdas.
These practices are encouraged at a suggestion level in the `.editorconfig`.

### Modifier Sequence

When applying multiple modifiers, adhere to the following order: `public, private, protected, internal, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, volatile, async`. This ensures uniformity across the codebase.

## Formatting Standards

### Newline Requirements

Insert newlines before `catch`, `else`, `finally`, and all opening braces to enhance readability. These rules are specified in the `.editorconfig`.

### Spacing Rules

Apply spaces around binary operators (e.g., `x + y`), after commas, and following control flow keywords (e.g., `if`). Avoid spaces after casts (e.g., `(int)x`) or around dots. These preferences are enforced by the `.editorconfig`.

### Placement of Using Directives

All `using` directives must reside within the namespace declaration. External placement is disallowed and marked as an error in the `.editorconfig`.

## Naming Conventions

Private members must follow these naming standards:

- **Constants**: `UpperCamelCase` (e.g., `MaxRetries`)
- **Instance Fields**: `lowerCamelCase` (e.g., `userCount`)
- **Static Fields**: `_lowerCamelCase` (e.g., `_cache`)
- **Static Readonly Fields**: `UpperCamelCase` (e.g., `DefaultTimeout`)

Deviations from these conventions are flagged as warnings in the `.editorconfig`.

## Additional Details

All other formatting, diagnostic, and style preferences—including indentation specifics, parentheses usage, and diagnostic severities—are managed by the `.editorconfig`.