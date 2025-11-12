# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BridgingIT DevKit (bITDevKit) is a modular .NET library providing components for modern application development centered around Domain-Driven Design (DDD) principles. The project is organized as a multi-layered architecture with Common, Domain, Application, Infrastructure, and Presentation layers, distributed as NuGet packages.

## Solution Structure

The solution follows Onion/Clean Architecture with four main layers:

- **0. Common**: Shared utilities, extensions, options, serialization, caching, mapping, results, rules
- **1. Application**: Commands, queries, entities, messaging, notifications, job scheduling, storage
- **2. Domain**: Core domain models, event sourcing, outbox pattern, mediator, code generation
- **3. Infrastructure**: EntityFramework, Azure (Cosmos, ServiceBus, Storage), AutoMapper, Mapster
- **4. Presentation**: Web endpoints, Blazor components, app state

Project organization:
- `src/` - Source projects organized by layer and functionality
- `tests/` - Unit and integration tests (Domain, Application, Infrastructure, Presentation)
- `examples/` - Example applications (DoFiesta, DinnerFiesta, EventSourcingDemo, WeatherForecast)
- `docs/` - Feature documentation
- `benchmarks/` - Performance benchmarks using BenchmarkDotNet

## Common Development Commands

### Build
```bash
# Build entire solution
dotnet build --configuration Release --no-restore --nologo

# Build specific project
dotnet build src/Domain/Domain.csproj
```

### Testing
```bash
# Run all tests excluding examples
dotnet test --configuration Release --no-restore --no-build --nologo --filter "FullyQualifiedName!~Examples" --logger "trx;LogFileName=test-results.trx"

# Run tests for specific project
dotnet test tests/Domain.UnitTests/Domain.UnitTests.csproj

# Run integration tests (requires containers)
dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj
```

### Cleaning
```bash
# Clean build artifacts
dotnet clean

# Clean specific test project
dotnet clean tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj
```

### Benchmarks
```bash
# Run performance benchmarks
dotnet run -c Release --project benchmarks/Common.Benchmarks/Common.Benchmarks.csproj
```

## Architecture Patterns

### Requester/Notifier Pattern
The project uses a custom **Requester** (for commands/queries) and **Notifier** (for pub/sub notifications) system instead of MediatR, providing:
- Type-safe request/response handling with `Result<TValue>`
- Pipeline behaviors for cross-cutting concerns (validation, retry, timeout, chaos)
- Message metadata (`RequestId`, `RequestTimestamp`, `NotificationId`, `NotificationTimestamp`)
- Async-only handlers inheriting from `RequestHandlerBase<TRequest, TResponse>` and `NotificationHandlerBase<TNotification>`
- Automatic discovery via assembly scanning

### Result Pattern
All operations return `Result`, `Result<T>`, or `Result<PagedList<T>>` for explicit success/failure handling:
- Encapsulates success/failure status, messages, and errors
- Supports functional extensions (Map, Bind, Tap, Filter)
- Maintains immutability with fluent interface
- Commands with no meaningful return value use `Result<Unit>`

### Modules Pattern
Supports modular monolith architecture where features are organized as independent modules:
- Each module implements `IModule` or `IWebModule` with lifecycle methods
- Configuration binding from `appsettings.json` sections
- Feature toggling per environment
- Request scoping with `RequestModuleMiddleware`
- Module-specific DI registration

### Repository Pattern
Domain repositories follow generic repository pattern:
- Interface: `IGenericRepository<TEntity>` with async CRUD operations
- Supports specifications, decorators, and behaviors
- Multiple implementations: EntityFramework, Cosmos, Azure Storage

### Domain-Driven Design
- **Entities**: Base classes with identity and domain events
- **Value Objects**: Immutable objects with equality by value
- **Aggregates**: Enforce consistency boundaries
- **Domain Events**: Published through Notifier system
- **Smart Enumerations**: Rich domain enums using `Enumeration` base class with EF converters
- **TypedIds**: Strongly-typed entity identifiers

## Technology Stack

### Core Libraries
- .NET 8+ with C# 12+ features
- MediatR (legacy, being replaced by Requester/Notifier)
- FluentValidation for validation
- Polly for resilience patterns
- Serilog for structured logging
- Scrutor for DI assembly scanning

### Data Access
- Entity Framework Core
- Azure Cosmos DB
- Azure Storage (Blobs, Tables, Queues)

### Testing
- xUnit as test framework
- Shouldly for assertions (AAA pattern)
- NSubstitute for mocking
- Testcontainers for integration tests
- Name test instance as `sut` (System Under Test)

### Messaging & Scheduling
- Azure ServiceBus
- RabbitMQ
- Quartz for job scheduling

## Coding Standards

### Language Features
- Use C# 12+ features (collection expressions, primary constructors)
- Enable implicit usings
- Prefer top-level statements for minimal APIs
- No underscores for private fields
- Use global usings (System, System.Text, System.Linq, EnsureThat)

### Architecture Principles
- Follow SOLID principles
- Prefer BCL over external packages unless functionality unavailable
- Use constructor injection for dependencies
- Register services with appropriate DI lifetime
- Separate business logic from technical concerns

### Code Organization
- Keep handlers focused on single responsibility
- Use pipeline behaviors for cross-cutting concerns
- Encapsulate business logic in domain models
- Use specifications for complex query logic
- Maintain immutability where appropriate

### Testing Guidelines
- Use AAA (Arrange-Act-Assert) pattern
- Name test instance as `sut`
- Use Shouldly for assertions: `result.ShouldBe(expected)`
- Use NSubstitute for mocking: `Substitute.For<IService>()`
- Keep tests independent and repeatable
- Name tests clearly: `MethodName_ShouldExpectedBehavior_WhenCondition()`

## Configuration

### Build Configuration
- Uses `Directory.Build.props` and `Directory.Packages.props` for centralized configuration
- Treats warnings as errors (`TreatWarningsAsErrors=true`)
- Embedded debug symbols (`DebugType=embedded`)
- Package lock files enabled (`RestorePackagesWithLockFile=true`)

### Project Structure Conventions
- Solution organized by architectural layers (0-4 prefixes)
- Each layer has separate folder in `src/`
- Tests mirror source structure in `tests/`
- Examples demonstrate features in `examples/`

## Key Features Documentation

Detailed documentation available in `docs/` folder:
- `features-requester-notifier.md` - Command/query and pub/sub patterns
- `features-results.md` - Result pattern implementation
- `features-modules.md` - Modular monolith architecture
- `features-domain.md` - DDD patterns and smart enumerations
- `features-domain-repositories.md` - Repository pattern
- `features-filtering.md` - Type-safe filtering for APIs
- `features-messaging.md` - Message handling
- `features-jobscheduling.md` - Background job scheduling
- `features-rules.md` - Business rules engine

## Performance Considerations

- Benchmarks located in `benchmarks/` folder using BenchmarkDotNet
- Focus on low allocations for core infrastructure (Requester pipeline)
- Results published to `BenchmarkDotNet.Artifacts/results/`
- Used to compare performance against alternatives like MediatR

## Common Pitfalls

- Don't use MediatR directly in new code; use Requester/Notifier instead
- Always return `Result` or `Result<T>` from handlers, not exceptions for business logic failures
- Don't mix domain events with application events; use appropriate base classes
- Use `RequestModuleMiddleware` for module-scoped requests in modular monoliths
- Integration tests require Docker/Testcontainers to be available
