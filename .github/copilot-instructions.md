# General Guidelines
- Strictly follow the coding conventions and formatting rules defined in the project's .editorconfig file.
- Use C# 12+ and .NET 8+ features where appropriate.
- Write clean, maintainable, and well-documented code.
- Follow SOLID principles and modern .NET best practices.
- Structure code using Onion/Clean Architecture: Domain, Application, Infrastructure, Presentation.

# Library and Package Usage
- Always prefer the .NET Base Class Library (BCL) for all functionality.
- Only use external NuGet packages if the required functionality is not available in the BCL or if the external package is an established industry standard for the task.
- When using external packages, ensure they are well-maintained, widely adopted, and compatible with the project�s .NET version.

# Key Packages Used
- MediatR: For CQS and mediator pattern.
- FluentValidation: For model validation.
- Polly: For resilience and transient-fault handling.
- Serilog: For structured logging.
- Scrutor: For DI registration via assembly scanning.
- Shouldly: For expressive test assertions.
- NSubstitute: For mocking in tests.
- xUnit: For unit testing.
- EntityFramework Core: For data access.
- Testcontainers: For integration testing with containers.
- Azure Storage, ServiceBus, CosmosDb, RabbitMQ: For cloud and messaging support.

# Testing
- Write unit tests for all business logic and critical code paths.
- Use the AAA (Arrange-Act-Assert) pattern:
- **Arrange:** Set up the test objects and prerequisites.
- **Act:** Execute the method or functionality under test.
- **Assert:** Verify the outcome.
- Name the instance under test as `sut` (System Under Test) for clarity.
- Use **Shouldly** for assertions (e.g., `result.ShouldBe(expected)`).
- Use **NSubstitute** for mocking dependencies.
- Name test methods clearly to describe their purpose and expected outcome.
- Keep tests independent and repeatable.
- Example test structure:
```csharp
[Fact]
public void CalculateTotal_ShouldReturnCorrectSum()
{
    // Arrange
    var sut = new Calculator();
    var numbers = new[] { 1, 2, 3 };

    // Act
    var result = sut.CalculateTotal(numbers);

    // Assert
    result.ShouldBe(6);
}
```
- Use xUnit as the default test framework.

# CQS/CQRS and DDD
- Use the requestor/notifier for commands/queries and pub/sub .
- Encapsulate business logic in domain models, value objects, entities, and aggregates.
- Use repositories for data access, following the repository pattern.
- Use the Result pattern for explicit success/failure handling.

# Validation and Resilience
- Use FluentValidation for input and model validation.
- Use Polly for retry, circuit breaker, and other resilience patterns.

# Logging and Observability
- Use Serilog for structured logging but use the dotnet logging ILogger.
- Integrates with Seq for log visualization.

# Dependency Injection
- Use constructor injection for dependencies.
- Register services with the appropriate lifetime in the DI container.
- Use Scrutor for assembly scanning and registration.

# Configuration and Tooling
- Use Directory.Build.props and Directory.Packages.props for consistent configuration.
- Use Docker and Azure for local development and CI/CD.

# Additional Preferences
- Use modern language features (e.g., collection expressions, primary constructors).
- don't use underscores for private fields.
- Prefer top-level statements for minimal APIs and small programs.
- Use global usings to reduce boilerplate.