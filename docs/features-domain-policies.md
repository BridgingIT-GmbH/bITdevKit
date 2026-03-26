# Domain Policies Feature Documentation

[TOC]

## Overview

Domain policies model business decisions that should be evaluated against a domain context. They are useful when the logic is broader than a single property check or aggregate invariant and you want to express it as a reusable, composable unit.

In bITdevKit, a domain policy:

- implements `IDomainPolicy<TContext>`
- can decide whether it applies to a given context through `IsEnabledAsync(...)`
- returns an `IResult` from `ApplyAsync(...)`
- can be executed alone or as part of a policy set through `DomainPolicies.ApplyAsync(...)`

The feature is meant for domain-level decision logic, not infrastructure authorization policies and not low-level fluent validation. It complements [Rules](./features-rules.md) and [Results](./features-results.md) rather than replacing them.

## When To Use It

Domain policies are a good fit when:

- a business decision spans multiple entities or value objects
- the check needs a dedicated context object rather than a single input value
- you want to compose several policy checks into one domain decision
- each policy may return its own typed result value
- the caller needs control over whether evaluation should continue, stop, or throw on failure

Typical examples are eligibility checks, approval preconditions, order-placement constraints, or workflow transition rules.

## Core Building Blocks

### `IDomainPolicy<TContext>`

This is the core contract:

- `IsEnabledAsync(...)` decides whether the policy should run for the supplied context
- `ApplyAsync(...)` performs the actual business check and returns an `IResult`

That separation is important because some policies are conditional rather than universally applicable.

### `DomainPolicyBase<TContext>`

Most policies can inherit from `DomainPolicyBase<TContext>`, which already provides a default `IsEnabledAsync(...)` implementation returning `true`. You only override it when the policy should be skipped for some contexts.

### `DomainPolicies`

`DomainPolicies` is the orchestration helper. It applies one or more policies to a context and produces a `DomainPolicyResult<TContext>` that aggregates:

- overall success or failure
- messages
- errors
- per-policy result values

### `DomainPolicyResult<TContext>`

This result type follows the same explicit success-or-failure style as the general `Result` feature, but adds `PolicyResults` so callers can inspect the individual outputs of policies that ran.

### `DomainPolicyProcessingMode`

When applying multiple policies, you can choose how failures are handled:

- `ContinueOnPolicyFailure`: evaluate all enabled policies and aggregate the failures
- `StopOnPolicyFailure`: stop at the first failure and return the partial result
- `ThrowOnPolicyFailure`: raise a `DomainPolicyException` on the first failure

## Basic Usage

### Define a policy context

The context is the input model against which policies are evaluated:

```csharp
public sealed record CheckoutContext(
    Customer Customer,
    Cart Cart,
    Money Total);
```

### Implement a policy

```csharp
public sealed class CustomerMustBeActivePolicy : DomainPolicyBase<CheckoutContext>
{
    public override Task<IResult> ApplyAsync(
        CheckoutContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Customer.Status != CustomerStatus.Active)
        {
            return Task.FromResult<IResult>(
                Result.Failure().WithMessage("Customer must be active to check out"));
        }

        return Task.FromResult<IResult>(Result.Success());
    }
}
```

### Add a conditional policy

Policies can be enabled only for certain contexts:

```csharp
public sealed class LargeOrderApprovalPolicy : DomainPolicyBase<CheckoutContext>
{
    public override Task<bool> IsEnabledAsync(
        CheckoutContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(context.Total.Amount >= 1000m);
    }

    public override Task<IResult> ApplyAsync(
        CheckoutContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IResult>(
            context.Customer.CanPlaceLargeOrders
                ? Result.Success()
                : Result.Failure().WithMessage("Large orders require approval"));
    }
}
```

## Executing Policies

### Single policy

```csharp
var result = await DomainPolicies.ApplyAsync(
    context,
    new CustomerMustBeActivePolicy(),
    cancellationToken);
```

### Multiple policies

```csharp
var result = await DomainPolicies.ApplyAsync(
    context,
    [
        new CustomerMustBeActivePolicy(),
        new LargeOrderApprovalPolicy()
    ],
    DomainPolicyProcessingMode.ContinueOnPolicyFailure,
    cancellationToken);
```

If a policy is disabled through `IsEnabledAsync(...)`, it is skipped and does not contribute to the aggregated result.

## Returning Policy-Specific Values

Policies are not limited to plain success/failure. Because `ApplyAsync(...)` returns `IResult`, a policy can return `Result<T>` or `DomainPolicyResult<T>`, and `DomainPolicies` stores the underlying value in `PolicyResults`.

That makes policies useful for decision outputs such as risk scores, approval levels, or computed limits.

Conceptually:

```csharp
var approvalLevel = result.PolicyResults
    .GetValue<LargeOrderApprovalPolicy, ApprovalLevel?>();
```

This lets one policy set produce both validation failures and domain decision data.

## Failure Handling

### Continue

Use `ContinueOnPolicyFailure` when you want the caller to see all policy failures at once.

### Stop

Use `StopOnPolicyFailure` when later policies depend on earlier policies being satisfied or when the first failure is enough.

### Throw

Use `ThrowOnPolicyFailure` when a policy violation should escape as an exception. In that case the framework throws `DomainPolicyException`, which carries the underlying result and integrates naturally with the presentation-layer exception handling documented in [Exception Handling](./features-presentation-exception-handling.md).

## Domain Policies vs Rules

Use domain policies when:

- the logic is contextual and decision-oriented
- the checks are substantial enough to deserve their own domain type
- you want optional execution via `IsEnabledAsync(...)`
- you want per-policy output values

Use [Rules](./features-rules.md) when:

- you want lightweight fluent validation
- the checks are mostly local predicates
- you want inline rule composition rather than named policy objects

These features work well together. A domain policy can internally use the Rules feature and then return a `Result`.

## Relationship To Other Features

- [Domain](./features-domain.md) covers aggregates, value objects, typed ids, and fluent aggregate changes.
- [Results](./features-results.md) explains the result model used by domain policies.
- [Rules](./features-rules.md) covers the fluent rule engine that can complement policy implementations.
- [Exception Handling](./features-presentation-exception-handling.md) documents how `DomainPolicyException` can be translated into HTTP responses.
