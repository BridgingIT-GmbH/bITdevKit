![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================
Empowering developers with modular components for modern application development, centered around
Domain-Driven Design principles.

Our goal is to empower developers by offering modular components that can be easily integrated into
your projects. Whether you're working with repositories, commands, queries, or other components, the
bITDevKit provides flexible solutions that can adapt to your specific needs.

This repository includes the complete source code for the bITDevKit, along with a variety of sample
applications located in the ./examples folder within the solution. These samples serve as practical
demonstrations of how to leverage the capabilities of the bITDevKit in real-world scenarios. All
components are available
as [nuget packages](https://www.nuget.org/packages?q=bitDevKit&packagetype=&prerel=true&sortby=relevance).

For the latest updates and release notes, please refer to
the [RELEASES](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/RELEASES.md).

Join us in advancing the world of software development with the bITDevKit!

------------------------

## `Result.cs` Overview

> The `Result` class encapsulates the outcome of an operation, promoting an expressive and
> error-tolerant way to handle success and failure states.

The `Result` class is a central component designed to encapsulate the outcome of an operation,
providing a way to represent both successful and failed operations. This class promotes a more
expressive and error-tolerant approach to handling operation results, encouraging the explicit
declaration of success or failure states.

### Returning a Result

To return a `Result` from a method, you typically define the method to return `Result` or
`Result<T>`, where `T` is the type of the value returned in case of success. Here is an example
method returning a `Result`:

```csharp
public Result PerformOperation()
{
    // Your logic here
    
    if (success)
    {
        return Result.Success();
    }
    else
    {
        return Result.Failure(new Error("Operation Failed"));
    }
}
```

### Handling a Result

When you receive a `Result` from a method, you can handle it by checking its success or failure
state. Here's an example:

```csharp
var result = PerformOperation();

if (result.IsSuccess)
{
    // Handle success
}
else
{
    // Handle failure
    var error = result.Error;
    Console.WriteLine(error.Message);
}
```

### Using Typed Results

Sometimes, you may want to return a result with a value. This is where `Result<T>` comes in handy:

```csharp
public Result<int> CalculateSum(int a, int b)
{
    if (a < 0 || b < 0)
    {
        return Result.Failure<int>(new Error("Inputs must be non-negative"));
    }

    return Result.Success(a + b);
}
```

Handling a `Result<T>` involves extracting the value if the operation was successful:

```csharp
var result = CalculateSum(5, 10);

if (result.IsSuccess)
{
    int sum = result.Value;
    Console.WriteLine($"Sum: {sum}");
}
else
{
    Console.WriteLine(result.Error.Message);
}
```

### Typed Errors

Typed errors provide a more specific and structured way to handle different error scenarios. For
example, the `EntityNotFoundResultError` class can be used to represent an error where an entity is
not found:

#### EntityNotFoundResultError.cs:

```csharp
public class EntityNotFoundResultError : Error
{
    public EntityNotFoundResultError(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.")
    {
    }
}
```

You can return this typed error as follows:

```csharp
public Result GetEntity(int id)
{
    var entity = repository.FindById(id);

    if (entity == null)
    {
        return Result.Failure(new EntityNotFoundResultError("EntityName", id));
    }

    return Result.Success(entity);
}
```

When handling the result, you can check if the error is of a specific type:

```csharp
var result = GetEntity(1);

if (result.IsSuccess)
{
    // Handle success
}
else if (result.Error is EntityNotFoundResultError)
{
    var error = (EntityNotFoundResultError)result.Error;
    Console.WriteLine(error.Message);
}
else
{
    // Handle other errors
}
```

Other available typed errors are:

- [](./Result/Errors/DomainPolicyResultError.cs)
- [](./Result/Errors/DomainRuleResultError.cs)
- [](./Result/Errors/EntityNotFoundResultError.cs)
- [](./Result/Errors/NotFoundResultError.cs)
- [](./Result/Errors/ValidationResultError.cs)
- [](./Result/Errors/UnauthorizedResultError.cs)
- [](./Result/Errors/ValidationResultError.cs)

By using typed errors, you can create more expressive and manageable error handling in your
application.   