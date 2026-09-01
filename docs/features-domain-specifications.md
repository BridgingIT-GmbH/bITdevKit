# Domain Specifications

> Model reusable business criteria as composable specifications for queries and in-memory evaluation.

[TOC]

## Overview

Domain specifications encapsulate reusable criteria that can be evaluated against entities in memory and translated into query expressions for repositories. They are domain building blocks that can be used inside or outside a repository.

At the center of the feature is `ISpecification<T>`, which exposes:

- `ToExpression()` for query translation
- `ToPredicate()` for in-memory evaluation
- `IsSatisfiedBy(...)` for direct checks
- `And(...)`, `Or(...)`, and `Not()` for composition

This makes specifications useful both inside domain logic and at repository boundaries.

## Challenges

Selection criteria are often repeated across domain checks, handlers, and repository queries. Plain
delegates work in memory but cannot always be translated by a query provider, while duplicated LINQ
expressions are difficult to name, compose, and test as domain concepts.

## Solution

`ISpecification<T>` keeps a criterion as an expression tree and exposes compiled evaluation and
logical composition. Repository implementations consume the same expression used by in-memory
code. `Specification<T>` supports typed expressions and parameterized Dynamic LINQ expressions.

## Key Features

- Queryable criteria through `Expression<Func<T, bool>>`
- In-memory evaluation through compiled predicates
- `And`, `Or`, and `Not` composition
- Typed and Dynamic LINQ construction
- Built-in ID and duplicate-value specifications
- Collection helpers that require every supplied specification to match

## Architecture

`ISpecification<T>` defines expression, predicate, direct-evaluation, composition, and diagnostic
string methods. `Specification<T>` implements that contract. `AndSpecification<T>`,
`OrSpecification<T>`, and `NotSpecification<T>` combine expression trees while preserving a single
`ISpecification<T>` result that repository providers can consume.

## Use Cases

Use a domain specification when:

- a selection rule should be reusable across handlers or services
- a business criterion should be expressed as a named domain concept
- the same rule must work both in memory and in repository queries
- several criteria need to be combined dynamically

Typical examples are:

- active customers
- overdue invoices
- entities with a specific id
- uniqueness checks for natural keys

## Basic Usage

This example constructs one specification, evaluates it in memory, and prints the matching names:

```csharp
var customers = new[]
{
	(Name: "Ada", IsActive: true),
	(Name: "Grace", IsActive: false),
	(Name: "Linus", IsActive: true)
};

var activeCustomers = new Specification<(string Name, bool IsActive)>(
	customer => customer.IsActive);
var names = customers
	.Where(activeCustomers.ToPredicate())
	.Select(customer => customer.Name);

Console.WriteLine(string.Join(", ", names));
```

Output:

```text
Ada, Linus
```

## Core building blocks

### `ISpecification<T>`

The specification contract represents one criterion over `T`.

It supports two important use cases:

- expression-based querying through `ToExpression()`
- object-based evaluation through `IsSatisfiedBy(...)`

That dual nature is what makes specifications more useful than a plain `Func<T, bool>`.

### `Specification<T>`

`Specification<T>` is the standard implementation. It can be created from:

- a normal LINQ expression
- a dynamic string expression with values

That gives the devkit both type-safe specifications and more dynamic query scenarios when needed.

### Composite specifications

The feature includes built-in composition types:

- `AndSpecification<T>`
- `OrSpecification<T>`
- `NotSpecification<T>`

These are also exposed fluently through `And(...)`, `Or(...)`, and `Not()` on `ISpecification<T>`.

### Reusable built-in specifications

The package also contains some ready-made specifications:

- `HasIdSpecification<T>` for matching entities by `Id`
- `UniqueSpecification<TEntity>` for uniqueness checks on a property
- `UniqueExceptSpecification<TEntity, TId>` for uniqueness checks that exclude one entity, which is especially useful in update scenarios

`HasIdSpecification<T>` compares the `IEntity.Id` object exposed by the entity. For predictable
in-memory evaluation with boxed value IDs, prefer a typed specification such as
`new Specification<Customer>(customer => customer.Id == customerId)`. Query translation of the
object-typed form also depends on the repository provider.

## Usage details

### Define a simple specification

```csharp
public sealed class ActiveCustomerSpecification : Specification<Customer>
{
    public override Expression<Func<Customer, bool>> ToExpression()
    {
        return customer => customer.Status == CustomerStatus.Active;
    }
}
```

For simple cases, you can also construct a specification directly:

```csharp
var specification = new Specification<Customer>(c => c.Status == CustomerStatus.Active);
```

### Evaluate in memory

```csharp
var isSatisfied = specification.IsSatisfiedBy(customer);
```

This is useful in domain logic, guards, or tests where you already have the entity instance.

### Use in repository queries

```csharp
var activeCustomers = await repository.FindAllAsync(
    new ActiveCustomerSpecification(),
    cancellationToken: cancellationToken);
```

The repository can translate the specification expression into the underlying query provider.

## Composition

Specifications can combine several criteria without creating one monolithic predicate:

```csharp
var specification = new Specification<Customer>(c => c.Status == CustomerStatus.Active)
    .And(new Specification<Customer>(c => c.IsDeleted == false))
    .And(new Specification<Customer>(c => c.Visits > 5));
```

You can also negate and branch conditions:

```csharp
var specification = new Specification<Customer>(c => c.Status == CustomerStatus.Active)
    .Or(new Specification<Customer>(c => c.IsVip))
    .And(new Specification<Customer>(c => c.Country == "NL"))
    .Not();
```

The important point is that the resulting specification is still an `ISpecification<T>` and can still be evaluated in memory or translated into a query expression.

## Dynamic specifications

`Specification<T>` also supports dynamic expressions:

```csharp
var specification = new Specification<Customer>(
    "Status == @0 && Visits >= @1",
    CustomerStatus.Active,
    5);
```

This is helpful when criteria are assembled from external input or metadata, though strongly typed expressions should remain the default for domain code where possible.

The expression string is parsed by Dynamic LINQ. Treat the expression text as trusted or build it
from an allow-list, and pass user-provided values through `@0`, `@1`, and later placeholders.

## Uniqueness specifications

The built-in uniqueness specs are useful when natural-key rules need to be expressed as queryable domain criteria.

### Unique value

```csharp
var specification = new UniqueSpecification<Customer>(c => c.Email, email);
```

This expresses "find entities where the selected property already has this value."

Despite the type name, a satisfied uniqueness specification identifies a conflicting entity. A
caller establishes uniqueness by confirming that the repository query returns no matches.

### Unique value except current entity

```csharp
var specification = new UniqueExceptSpecification<Customer, CustomerId>(
    c => c.Email,
    email,
    customerId);
```

This is the common update scenario where one entity is allowed to keep its current value, but no other entity may already use it.

## Collections of specifications

`SpecificationExtensions` contains helpers for evaluating multiple specifications together:

```csharp
var specifications = new ISpecification<Customer>[]
{
    new Specification<Customer>(c => c.Status == CustomerStatus.Active),
    new Specification<Customer>(c => c.Visits > 0)
};

var isSatisfied = specifications.IsSatisfiedBy(customer);
```

That helper returns `true` when all supplied specifications are satisfied, and it treats a null or empty collection as satisfied.

## Domain specifications and filtering

Specifications and filtering are related but not the same:

- domain specifications are named reusable domain criteria
- filtering is an external query model that can be translated into specifications and find options

So filtering is often a consumer of the specifications feature, not a replacement for it.

## Domain specifications, policies, and rules

Use specifications when:

- you are expressing selection criteria
- you need query translation
- you want composable predicates over entities

Use [Domain Policies](./features-domain-policies.md) when:

- you are modeling a broader business decision over a context
- the output is more than a true-or-false criterion

Use [Rules](./features-rules.md) when:

- you want fluent validation-style checks
- the concern is validation flow rather than queryable entity criteria

## Relationship to other features

- [Domain Repositories](./features-domain-repositories.md) uses specifications as a primary query mechanism.
- [Filtering](./features-filtering.md) translates filter models into specifications and find options.
- [Domain](./features-domain.md) covers the broader domain modeling building blocks around aggregates, value objects, and typed ids.
