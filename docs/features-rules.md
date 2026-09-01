# Rules

> Express business rules as composable validations with consistent Result-based outcomes.

[TOC]

## Overview

The Rules feature defines and evaluates business rules through `IRule`, the `Rule` entry point,
`RuleBuilder`, and the predefined `RuleSet`. Every evaluation integrates with the
[Results feature](./features-results.md).

Key benefits:

- Centralized rule definition and execution
- Composable rule chains
- Clear error handling through the Result pattern
- Rich set of predefined rules through RuleSet
- Extensible design for custom rules

## Challenges

Business rules often become scattered conditions with inconsistent failure handling. Applications
also need to evaluate several rules, collect failures when required, apply rules conditionally, and
classify collections without duplicating control flow.

## Solution

Rules implement `IRule` and return `Result`. Use `Rule.Check` for one rule or `RuleBuilder` for a
chain. `RuleSet` supplies common value, text, date, time, collection, and FluentValidation-backed
rules. Synchronous and asynchronous base classes support custom rules.

## Key Features

- Direct synchronous and asynchronous rule evaluation
- Fluent rule chains with stop-on-failure or failure aggregation
- Conditional rules through `When`, `Unless`, and condition-count extensions
- Collection classification through `Filter`, `FilterAsync`, `Switch`, and `SwitchAsync`
- Configurable failure and exception handling
- Custom rules through `RuleBase`, `AsyncRuleBase`, and delegate-backed rules

## Architecture

`IRule` defines synchronous and asynchronous evaluation. `RuleBase` and `AsyncRuleBase` provide the
standard enabled-state behavior. The static `Rule` class evaluates individual rules and creates
`RuleBuilder` instances. A builder executes an ordered rule list or classifies items by applying
item rules. See [Architecture details](#architecture-details) for the component diagram.

## Use Cases

- Validate command and domain inputs with typed result errors.
- Collect several validation failures before returning to a caller.
- Apply shipping, payment, or permission rules only when their conditions hold.
- Separate matching and non-matching collection items for different handlers.
- Wrap application-specific synchronous or asynchronous checks in reusable rules.

## Basic Usage

The Rules feature provides a fluent API for validating conditions and enforcing business rules. Here
are the most common usage patterns:

```csharp
var result = Rule
    .Add(RuleSet.IsNotEmpty("Ada"))
    .Add(RuleSet.GreaterThan(42, 17))
    .Check();

if (result.IsFailure)
{
    Console.Error.WriteLine(
        string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    return;
}

Console.WriteLine("All rules passed.");
```

Output:

```text
All rules passed.
```

### Single rule validation

```csharp
// Basic rule check
var result = Rule.Check(RuleSet.IsNotEmpty(customer.Name));
if (result.IsSuccess)
{
    // Validation passed
}

// Multiple evaluations
var result = Rule
    .Add(RuleSet.IsNotEmpty(order.Id))
    .Add(RuleSet.GreaterThan(order.Amount, 0))
    .Check();
```

### Conditional rules

```csharp
// Basic conditional rule
var result = Rule
    .Add(RuleSet.IsNotEmpty(order.Id))
    .When(!order.IsDigital, RuleSet.IsNotEmpty(order.ShippingAddress))
    .Check();

// Multiple conditional rules
var result = Rule
    .Add(RuleSet.IsNotEmpty(order.Id))
    .When(order.RequiresShipping, builder => builder
        .Add(RuleSet.IsNotEmpty(order.ShippingAddress))
        .Add(RuleSet.IsNotEmpty(order.ContactEmail)))
    .Check();
```

### Working with collections

```csharp
// Filter valid items
var result = Rule
    .Add<Order>(o => RuleSet.GreaterThan(o.Amount, 0))
    .Add<Order>(o => RuleSet.IsNotEmpty(o.CustomerId))
    .Filter(orders);

// Split and process valid/invalid items
var result = Rule
    .Add<Order>(o => RuleSet.GreaterThan(o.Amount, 0))
    .Switch(orders,
        validOrders => ProcessValidOrders(validOrders),
        invalidOrders => LogInvalidOrders(invalidOrders));
```

## Rule builder patterns

The Rule Builder provides a fluent interface for combining multiple rules and conditions into
expressive evaluation chains. This section covers common patterns for building and composing rules.

### Building basic rule chains

Rule chains allow you to combine multiple evaluations in a readable sequence:

```csharp
var result = Rule
    .Add(RuleSet.IsNotEmpty(order.Id))
    .Add(RuleSet.IsNotNull(order.Customer))
    .Add(RuleSet.GreaterThan(order.Amount, 0))
    .Check();
```

You can also continue evaluation after failures to collect all errors:

```csharp
var result = Rule
    .Add(RuleSet.IsNotEmpty(order.Id))
    .Add(RuleSet.IsNotNull(order.Customer))
    .ContinueOnFailure()
    .Check();
```

### Conditional rules

Apply rules based on conditions using `When`, `Unless`, and other conditional methods:

```csharp
// Single condition
var result = Rule
    .When(order.RequiresShipping, RuleSet.IsNotEmpty(order.ShippingAddress))
    .Check();

// Multiple rules under one condition
var result = Rule
    .When(order.HasDiscount, builder => builder
        .Add(RuleSet.GreaterThan(order.DiscountAmount, 0))
        .Add(RuleSet.LessThan(order.DiscountAmount, order.TotalAmount)))
    .Check();

// Inverse condition with Unless
var result = Rule
    .Unless(order.IsDigital, RuleSet.IsNotEmpty(order.ShippingAddress))
    .Check();
```

### Multiple conditions

Handle complex scenarios with multiple conditions:

```csharp
// All conditions must be true
var result = Rule
    .WhenAll(new[]
    {
        order.RequiresShipping,
        order.Amount > 1000,
        order.IsInternational
    }, RuleSet.IsNotEmpty(order.CustomsDeclaration))
    .Check();

// Any condition must be true
var result = Rule
    .WhenAny(new[]
    {
        order.UseCredit,
        order.UsePaypal
    }, RuleSet.IsNotEmpty(order.PaymentDetails))
    .Check();
```

### Collection processing

The Rules feature provides two ways to process collections of items:

#### Filtering collections

Use Filter to get valid items matching your rules:

`Filter` and `FilterAsync` return a successful result containing only matching items. Rejected
items do not turn the result into a failure. Use `Switch` when both groups must be handled.

```csharp
var result = Rule
    .Add<Order>(o => RuleSet.GreaterThan(o.Amount, 0))
    .Add<Order>(o => RuleSet.IsNotEmpty(o.CustomerId))
    .Filter(orders);

if (result.IsFailure)
{
    return;
}

var validOrders = result.Value;
// Process valid orders. Rejected items do not make Filter fail.
```

#### Splitting collections

Use Switch to handle valid and invalid items separately:

```csharp
var result = Rule
    .Add<Order>(o => RuleSet.GreaterThan(o.Amount, 0))
    .Switch(orders,
        validOrders => ProcessValidOrders(validOrders),
        invalidOrders => HandleInvalidOrders(invalidOrders));
```

### Practical example

Here's a complete example showing multiple patterns together:

```csharp
public Result ValidateOrder(Order order)
{
    return Rule
        // Basic evaluation
        .Add(RuleSet.IsNotEmpty(order.Id))
        .Add(RuleSet.IsNotNull(order.Customer))

        // Customer evaluation when present
        .When(order.Customer != null, builder => builder
            .Add(RuleSet.IsValidEmail(order.Customer.Email))
            .Add(RuleSet.IsNotEmpty(order.Customer.Name)))

        // Shipping evaluation for physical goods
        .Unless(order.IsDigital, builder => builder
            .Add(RuleSet.IsNotEmpty(order.ShippingAddress))
            .When(order.IsInternational,
                RuleSet.IsNotEmpty(order.CustomsInfo)))

        // Continue to collect all evaluation errors
        .ContinueOnFailure()
        .Check();
}
```

## Architecture details

### Component overview

The Rules feature consists of several key components that work together to provide flexible rule evaluation:

```mermaid
classDiagram
    %% Interfaces
    class IRule {
        <<interface>>
        +string Message
        +bool IsEnabled
        +Result IsSatisfied()
        +Task~Result~ IsSatisfiedAsync()
    }

    %% Base Classes
    class RuleBase {
        <<abstract>>
        +Result Execute()*
        +Result IsSatisfied()
        +Task~Result~ IsSatisfiedAsync()
    }

    class AsyncRuleBase {
        <<abstract>>
        +Task~Result~ ExecuteAsync(CancellationToken)*
        +Result IsSatisfied()
        +Task~Result~ IsSatisfiedAsync()
    }

    %% Main Classes
    class Rule {
        <<static>>
        +Result Check(IRule)
        +Result Throw(IRule)
        +Task~Result~ CheckAsync(IRule)
        +Task~Result~ ThrowAsync(IRule)
    }

    class RuleBuilder {
        +RuleBuilder Add(IRule)
        +Result Check()
        +Task~Result~ CheckAsync()
        +Result~T~ Filter~T~()
    }

    class RuleSet {
        <<static>>
        +IRule Equal~T~()
        +IRule NotEqual~T~()
        +IRule GreaterThan~T~()
    }

    %% Relationships
    IRule <|-- RuleBase
    IRule <|-- AsyncRuleBase
    Rule --> IRule
    RuleBuilder --> IRule
    RuleSet --> IRule
```

### Key components

#### `Rule` static class

The `Rule` class is the main entry point for rule evaluation:

- Provides static methods for rule checking and rule chain building
- Handles rule execution and error aggregation
- Creates RuleBuilder instances for complex evaluation chains

```csharp
// Direct rule checking
var result = Rule.Check(someRule);

// Start building a rule chain
var builder = Rule.Add();
```

#### `RuleBuilder`

The `RuleBuilder` enables fluent rule chain construction:

- Combines multiple rules into a single evaluation chain
- Handles conditional rule execution
- Provides collection processing capabilities

```csharp
var builder = Rule
    .Add(firstRule)
    .Add(secondRule)
    .When(condition, conditionalRule);
```

#### `RuleSet`

The `RuleSet` class contains predefined rules for common evaluation scenarios:

- Value comparisons (Equal, GreaterThan, etc.)
- String evaluations (IsEmpty, Contains, etc.)
- Collection evaluations (HasItems, All, Any, etc.)

```csharp
// Using predefined rules
var emailRule = RuleSet.IsValidEmail(email);
var rangeRule = RuleSet.NumericRange(amount, 0, 100);
```

### Rule types

#### Value rules

Rules that validate simple values or properties:

- Equality checks
- Numeric comparisons
- String operations

#### Collection rules

Rules for validating collections or sequences:

- Size evaluation
- Item evaluation
- Aggregation checks

#### Composite rules

Rules that combine multiple evaluations:

- Conditional rules
- Rule chains
- Complex business rules

### Integration with the Result pattern

The Rules feature integrates with the Result pattern to provide consistent error handling:

- All rule evaluations return a Result object
- Failed evaluations include detailed error information
- Results can be combined and aggregated

## Advanced usage

This section covers advanced patterns and scenarios for using the Rules feature.

### Complex rule chains

Combine several conditions and rules for application-specific business logic:

```csharp
public Result ValidateOrder(Order order)
{
    return Rule
        // Basic evaluation
        .Add(RuleSet.IsNotEmpty(order.Id))
        .Add(RuleSet.IsNotNull(order.Customer))

        // Payment evaluation based on method
        .WhenAny(new[]
        {
            order.UseCredit,
            order.UseCash,
            order.UsePaypal
        }, builder => builder
            .Add(RuleSet.GreaterThan(order.Amount, 0))
            .Add(RuleSet.IsNotNull(order.PaymentDetails)))

        // Special order handling
        .WhenAll(new[]
        {
            order.IsInternational,
            order.Amount > 1000
        }, RuleSet.IsNotEmpty(order.CustomsDeclaration))

        .ContinueOnFailure()
        .Check();
}
```

### Working with collections

#### Collection filtering with several rules

Apply multiple rules to filter collections:

```csharp
public Result<IEnumerable<Order>> GetValidOrders(IEnumerable<Order> orders)
{
    return Rule
        .Add<Order>(o => RuleSet.IsNotEmpty(o.Id))
        .Add<Order>(o => RuleSet.GreaterThan(o.Amount, 0))
        .Add<Order>(
            o => !o.IsInternational || !string.IsNullOrWhiteSpace(o.CustomsDeclaration),
            "International orders require a customs declaration.")
        .Filter(orders);
}
```

#### Collection processing with `Switch`

Handle complex collection scenarios with Switch:

```csharp
public Result ProcessOrders(IEnumerable<Order> orders)
{
    return Rule
        .Add<Order>(o => RuleSet.IsNotEmpty(o.Id))
        .Add<Order>(o => RuleSet.GreaterThan(o.Amount, 0))
        .Switch(orders,
            validOrders => {
                // Process valid orders
                foreach (var order in validOrders)
                {
                    ProcessOrder(order);
                }
                return Result.Success();
            },
            invalidOrders => {
                // Log invalid orders
                foreach (var order in invalidOrders)
                {
                    LogInvalidOrder(order);
                }
                return Result.Success();
            });
}
```

### Custom rules

Create custom rules by inheriting from RuleBase:

```csharp
public class OfficeHoursRule : RuleBase
{
    private readonly DateTime dateTime;

    public OfficeHoursRule(DateTime dateTime)
    {
        this.dateTime = dateTime;
    }

    public override string Message =>
        "Operation must be performed during business hours (9 AM - 5 PM)";

    public override Result Execute()
    {
        return Result.SuccessIf(
            dateTime.Hour >= 9 &&
            dateTime.Hour < 17 &&
            dateTime.DayOfWeek != DayOfWeek.Saturday &&
            dateTime.DayOfWeek != DayOfWeek.Sunday);
    }
}

// Using custom rule
var result = Rule
    .Add(new OfficeHoursRule(DateTime.Now))
    .Check();
```

### Domain validation example

Here's a complete example showing how to validate a domain entity with complex rules:

```csharp
public class OrderValidator
{
    public Result ValidateOrder(Order order)
    {
        return Rule
            // Basic evaluation
            .Add(RuleSet.IsNotEmpty(order.Id))
            .Add(RuleSet.IsNotNull(order.Customer))

            // Customer evaluation
            .When(order.Customer != null, builder => builder
                .Add(RuleSet.IsValidEmail(order.Customer.Email))
                .Add(RuleSet.IsNotEmpty(order.Customer.Name)))

            // Order items evaluation
            .When(order.Items?.Any() == true, builder => builder
                .Add(RuleSet.All(order.Items, item =>
                    RuleSet.GreaterThan(item.Quantity, 0))))

            // Payment evaluation
            .When(order.HasDiscount, builder => builder
                .Add(RuleSet.GreaterThan(order.DiscountAmount, 0))
                .Add(RuleSet.LessThan(order.DiscountAmount, order.Amount)))

            // Shipping evaluation
            .Unless(order.IsDigital, builder => builder
                .Add(RuleSet.IsNotEmpty(order.ShippingAddress))
                .WhenAll(new[]
                {
                    order.IsInternational,
                    order.Amount > 1000
                }, RuleSet.IsNotEmpty(order.CustomsDeclaration)))

            .ContinueOnFailure()
            .Check();
    }
}
```

## Appendix A: Async usage

While the Rules feature is primarily used synchronously, it also supports asynchronous operations.
This appendix provides a brief overview of async usage patterns.

### Basic async validation

Use `CheckAsync` for async rule evaluation:

```csharp
public async Task<Result> ValidateOrderAsync(Order order)
{
    return await Rule
        .Add(RuleSet.IsNotEmpty(order.Id))
        .Add(new ActiveCustomerRule(order.CustomerId, customerService))
        .CheckAsync();
}
```

### Async conditional rules

Async conditions can be used with `WhenAsync`:

```csharp
public async Task<Result> ValidateOrderAsync(Order order)
{
    return await Rule
        .Add(RuleSet.IsNotEmpty(order.Id))
        .WhenAsync(
            async token => await customerService.ExistsAsync(order.CustomerId, token),
            RuleSet.IsNotEmpty(order.ShippingAddress))
        .CheckAsync();
}
```

### Custom async rules

Create async rules by inheriting from AsyncRuleBase:

```csharp
public class ActiveCustomerRule : AsyncRuleBase
{
    private readonly string customerId;
    private readonly ICustomerService customerService;

    public ActiveCustomerRule(string customerId, ICustomerService customerService)
    {
        this.customerId = customerId;
        this.customerService = customerService;
    }

    public override string Message => "Customer evaluation failed";

    public override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        var customer = await customerService.GetCustomerAsync(customerId, cancellationToken);
        return Result.SuccessIf(customer != null && customer.IsActive);
    }
}
```

### Async collection processing

Both Filter and Switch operations support async rules:

```csharp
public async Task<Result> ProcessOrdersAsync(IEnumerable<Order> orders)
{
    // Async filtering
    var validOrders = await Rule
        .Add<Order>(o => RuleSet.GreaterThan(o.Amount, 0))
        .Add<Order>(async (o, token) =>
            await customerService.ExistsAsync(o.CustomerId, token))
        .FilterAsync(orders);

    // Async switch operation
    return await Rule
        .Add<Order>(o => RuleSet.GreaterThan(o.Amount, 0))
        .SwitchAsync(orders,
            async valid => await ProcessValidOrdersAsync(valid),
            async invalid => await HandleInvalidOrdersAsync(invalid));
}
```

### Important notes

1. All async methods accept an optional CancellationToken parameter
2. Async rules should inherit from AsyncRuleBase
3. Use CheckAsync() instead of Check() when working with async rules
4. Async and sync rules can be mixed in the same chain
5. For best performance, prefer sync rules when async operations aren't required

## Appendix B: scope

> The Rules feature is designed to be a lightweight, code-based solution for handling business
> rules and evaluations within your application.

The Rules feature is not meant to replace dedicated workflow or business rule engines. While it
handles common evaluation and business rule scenarios effectively, some situations call for more
specialized tools. If your project requires visual rule design tools, business user configuration of
rules, complex workflow orchestration, or long-running rule processes, a dedicated workflow or rules
engine might be more appropriate. Similarly, if you need rule persistence, versioning, or dynamic
rule compilation, consider exploring specialized solutions.

### When to use the Rules feature

The feature requires no external rule engine or persisted rule model. Because rules remain part of
the application code, they can use the same types and Results conventions as other bITdevKit
features.

Remember: Choose the simplest tool that meets your requirements. The feature provides a
lightweight, code-based approach to handling business rules, while staying consistent with
the bITdevKit philosophy of simple, effective solutions to common development problems.
