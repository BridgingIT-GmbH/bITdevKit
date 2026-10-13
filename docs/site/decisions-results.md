---
title: "Result&lt;T&gt; or plain return type"
---

# `Result<T>` or plain return type

Use `Result<T>` when an operation returns a value and has expected failures that the caller needs to handle explicitly. Keep a plain value when the operation does not need that outcome contract.

## Choose Result when

- invalid input, a missing record, or a rejected business rule is an expected outcome
- callers need typed errors and messages to decide what to do next
- several operations compose and failures must propagate without losing error information
- the existing DevKit API already requires a Result contract

Requester handlers return `Result<TResponse>`. Document and Blob Storage clients also use Result contracts. Keep those contracts consistent when implementing or consuming the feature.

## Keep a plain return type when

- a calculation or mapping returns a value for every valid input
- absence is the only alternative and a nullable or `Try...` contract expresses it clearly
- a framework callback requires another signature
- adding a wrapper would introduce no failure decision for the caller

Domain properties and internal helpers do not need Result wrappers merely because the surrounding use case returns one.

## Comparison

| Contract | Use |
| --- | --- |
| `T` or `Task<T>` | A value without an expected-failure result contract |
| Nullable value or `Try...` method | An explicitly optional value or a narrow success test |
| `Result<T>` or `Task<Result<T>>` | A value plus expected failure information |
| `Result` or `Task<Result>` | An outcome without a response value |
| `Result<Unit>` | A request contract requires a generic result but no response value |
| Exception | Unexpected failure or a violated programming contract |

## Example

An application-defined title validator can make its failure explicit:

```csharp
using BridgingIT.DevKit.Common;

static Result<string> NormalizeTitle(string title)
{
	if (string.IsNullOrWhiteSpace(title))
	{
		return Result<string>.Failure(
			new ValidationError("A title is required.", nameof(title)));
	}

	return Result<string>.Success(title.Trim());
}

var result = NormalizeTitle("  Weather report  ");
if (result.IsFailure)
{
	Console.Error.WriteLine(string.Join(
		Environment.NewLine, result.Errors.Select(error => error.Message)));
	return;
}

Console.WriteLine(result.Value);
```

The caller handles failure before using `Value`. A helper that calculates a number from already-valid domain values can return the number directly.

## Results and exceptions

Result does not remove unexpected exceptions. Convert an exception only at a boundary with a defined error policy, and retain its diagnostic context. Keep cancellation distinct from business validation failures and honor the API's cancellation contract.

Inspect `IsSuccess` or `IsFailure`; `Value` alone does not prove success. At an endpoint, map typed errors to the appropriate response instead of returning every result as HTTP success.

A failed Result does not undo side effects or create a transaction. Persistence, retries, and compensation still need their own policies.

## Related features

- [Results](reference/features-results.md)
- [Requester and Notifier](reference/features-requester-notifier.md)
- [Domain Repositories](reference/features-domain-repositories.md)
- [Retry or Compensation](decisions-retry-vs-compensation.md)
