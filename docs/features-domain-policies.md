# Domain Policies

> Encapsulate domain decisions as reusable, context-aware policy objects.

[TOC]

## Overview

Domain policies model business decisions that should be evaluated against a domain context. They are useful when the logic is broader than a single property check or aggregate invariant and you want to express it as a reusable, composable unit.

In bITdevKit, a domain policy:

- implements `IDomainPolicy<TContext>`
- can decide whether it applies to a given context through `IsEnabledAsync(...)`
- returns an `IResult` from `ApplyAsync(...)`
- can be executed alone or as part of a policy set through `DomainPolicies.ApplyAsync(...)`

The feature is meant for domain-level decision logic, not infrastructure authorization policies and not low-level fluent validation. It complements [Rules](./features-rules.md) and [Results](./features-results.md) rather than replacing them.

## Challenges

Some business decisions depend on several domain objects and need more context than a single
predicate. Callers may also need to skip inapplicable decisions, aggregate several failures, stop at
the first failure, or retain a typed output from each decision.

## Solution

An `IDomainPolicy<TContext>` separates applicability from evaluation. `DomainPolicies.ApplyAsync`
executes enabled policies in order and produces a `DomainPolicyResult<TContext>` with combined
messages, errors, and per-policy values.

## Key Features

- Asynchronous applicability and evaluation
- Reusable context-specific policy classes
- Continue, stop, or throw processing modes
- Aggregated messages and errors
- Typed per-policy values keyed by policy type
- Compatibility with the shared `IResult` contract

## Architecture

`IDomainPolicy<TContext>` defines `IsEnabledAsync` and `ApplyAsync`.
`DomainPolicyBase<TContext>` enables a policy by default. `DomainPolicies` evaluates policy arrays
sequentially, combines each `IResult`, and records its value in `DomainPolicyResults<TContext>`.
`DomainPolicyProcessingMode` controls what happens after a failed policy.

## Use Cases

Use domain policies when:

- a business decision spans multiple entities or value objects
- the check needs a dedicated context object rather than a single input value
- you want to compose several policy checks into one domain decision
- each policy may return its own typed result value
- the caller needs control over whether evaluation should continue, stop, or throw on failure

Typical examples are eligibility checks, approval preconditions, order-placement constraints, or workflow transition rules.

## Basic Usage

The following example applies one policy, checks the result before continuing, and prints the
outcome:

```csharp
var context = new CheckoutContext(CustomerIsActive: true);
var result = await DomainPolicies.ApplyAsync(
	context,
	new ActiveCustomerPolicy(),
	CancellationToken.None);

if (result.IsFailure)
{
	var details = result.Messages.Concat(result.Errors.Select(error => error.Message));
	Console.Error.WriteLine(string.Join(Environment.NewLine, details));
	return;
}

Console.WriteLine("Checkout policies passed.");

public sealed record CheckoutContext(bool CustomerIsActive);

public sealed class ActiveCustomerPolicy : DomainPolicyBase<CheckoutContext>
{
	public override Task<IResult> ApplyAsync(
		CheckoutContext context,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IResult>(
			context.CustomerIsActive
				? Result.Success()
				: Result.Failure().WithMessage("Customer must be active."));
	}
}
```

Output:

```text
Checkout policies passed.
```

## Core building blocks

### `IDomainPolicy<TContext>`

This is the core contract:

- `IsEnabledAsync(...)` decides whether the policy should run for the supplied context
- `ApplyAsync(...)` performs the actual business check and returns an `IResult`

This separation allows a policy to be skipped for contexts where it does not apply.

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

## Policy definitions

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

## Executing policies

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

## Returning policy-specific values

Policies are not limited to plain success/failure. Because `ApplyAsync(...)` returns `IResult`, a policy can return `Result<T>` or `DomainPolicyResult<T>`, and `DomainPolicies` stores the underlying value in `PolicyResults`.

That makes policies useful for decision outputs such as risk scores, approval levels, or computed limits.

If `LargeOrderApprovalPolicy.ApplyAsync` returns a `Result<ApprovalLevel>`, retrieve that value with:

```csharp
var approvalLevel = result.PolicyResults
    .GetValue<LargeOrderApprovalPolicy, ApprovalLevel?>();
```

This lets one policy set produce both validation failures and domain decision data.

Values are keyed by the concrete policy type. If the same policy type runs more than once in one
call, the later value replaces the earlier value.

## Failure handling

### Continue

Use `ContinueOnPolicyFailure` when you want the caller to see all policy failures at once.

### Stop

Use `StopOnPolicyFailure` when later policies depend on earlier policies being satisfied or when the first failure is enough.

### Throw

Use `ThrowOnPolicyFailure` when a policy violation should escape as an exception. In that case the
framework throws `DomainPolicyException`, which carries the aggregated result and can be handled by
the presentation-layer exception handling documented in [Exception Handling](./features-presentation-exception-handling.md).

## Domain policies and rules

Use domain policies when:

- the logic is contextual and decision-oriented
- the checks are substantial enough to deserve their own domain type
- you want optional execution via `IsEnabledAsync(...)`
- you want per-policy output values

Use [Rules](./features-rules.md) when:

- you want lightweight fluent validation
- the checks are mostly local predicates
- you want inline rule composition rather than named policy objects

A domain policy can use the Rules feature internally and return its `Result`.

## Relationship to other features

- [Domain](./features-domain.md) covers aggregates, value objects, typed ids, and fluent aggregate changes.
- [Results](./features-results.md) explains the result model used by domain policies.
- [Rules](./features-rules.md) covers the fluent rule engine that can complement policy implementations.
- [Exception Handling](./features-presentation-exception-handling.md) documents how `DomainPolicyException` can be translated into HTTP responses.
