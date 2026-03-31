# Domain Specifications Feature Documentation

> Model reusable business criteria as composable specifications for queries and in-memory evaluation.

[TOC]

## Overview

Domain specifications encapsulate reusable criteria that can be evaluated against entities in memory and translated into query expressions for repositories. They are a core domain building block in bITdevKit, not just a repository helper.

At the center of the feature is `ISpecification<T>`, which exposes:

- `ToExpression()` for query translation
- `ToPredicate()` for in-memory evaluation
- `IsSatisfiedBy(...)` for direct checks
- `And(...)`, `Or(...)`, and `Not()` for composition

This makes specifications useful both inside domain logic and at repository boundaries.

## When To Use It

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

## Core Building Blocks

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

### Composite Specifications

The feature includes built-in composition types:

- `AndSpecification<T>`
- `OrSpecification<T>`
- `NotSpecification<T>`

These are also exposed fluently through `And(...)`, `Or(...)`, and `Not()` on `ISpecification<T>`.

### Reusable Built-In Specifications

The package also contains some ready-made specifications:

- `HasIdSpecification<T>` for matching entities by `Id`
- `UniqueSpecification<TEntity>` for uniqueness checks on a property
- `UniqueExceptSpecification<TEntity, TId>` for uniqueness checks that exclude one entity, which is especially useful in update scenarios

## Basic Usage

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

Specifications can be combined into richer criteria without creating a new monolithic predicate:

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

## Dynamic Specifications

`Specification<T>` also supports dynamic expressions:

```csharp
var specification = new Specification<Customer>(
    "Status == @0 && Visits >= @1",
    CustomerStatus.Active,
    5);
```

This is helpful when criteria are assembled from external input or metadata, though strongly typed expressions should remain the default for domain code where possible.

## Uniqueness Specifications

The built-in uniqueness specs are useful when natural-key rules need to be expressed as queryable domain criteria.

### Unique value

```csharp
var specification = new UniqueSpecification<Customer>(c => c.Email, email);
```

This expresses “find entities where the selected property already has this value”.

### Unique value except current entity

```csharp
var specification = new UniqueExceptSpecification<Customer, CustomerId>(
    c => c.Email,
    email,
    customerId);
```

This is the common update scenario where one entity is allowed to keep its current value, but no other entity may already use it.

## Collections Of Specifications

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

## Domain Specifications vs Filtering

Specifications and filtering are related but not the same:

- domain specifications are named reusable domain criteria
- filtering is an external query model that can be translated into specifications and find options

So filtering is often a consumer of the specifications feature, not a replacement for it.

## Domain Specifications vs Policies and Rules

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

## Relationship To Other Features

- [Domain Repositories](./features-domain-repositories.md) uses specifications as a primary query mechanism.
- [Filtering](./features-filtering.md) translates filter models into specifications and find options.
- [Domain](./features-domain.md) covers the broader domain modeling building blocks around aggregates, value objects, and typed ids.
