# ADR-0013: Unit Testing Strategy with High Coverage Goals

## Status

Accepted

## Context

Software quality and maintainability are directly correlated with comprehensive automated testing. Without a clear testing strategy, codebases suffer from:

- **High defect rates** in production due to undetected regressions
- **Fear of refactoring** because changes might break existing functionality
- **Slow feedback loops** requiring manual testing for every change
- **Unclear specifications** as tests serve as living documentation
- **Technical debt accumulation** when code becomes too complex to test
- **Costly bug fixes** when defects are caught late in the development cycle

The application needed a testing strategy that:

1. Provides fast feedback during development
2. Enables confident refactoring and feature additions
3. Serves as executable documentation of system behavior
4. Catches regressions before code reaches production
5. Maintains high code quality standards across the team
6. Supports continuous integration and deployment practices

## Decision

Adopt a **comprehensive unit testing strategy** with **high coverage goals** (minimum 80% code coverage, target 90%+) across all layers except infrastructure data access.

### Testing Pyramid Approach

1. **Unit Tests** (majority): Fast, isolated tests for individual components
   - Domain logic (aggregates, value objects, business rules, domain events)
   - Application handlers (commands, queries)
   - Mapping configurations
   - Validators
   - Specifications

2. **Integration Tests** (moderate): Tests with real infrastructure
   - Endpoint-to-database flows
   - Repository implementations with real DbContext
   - Module integration scenarios

3. **Architecture Tests**: Automated enforcement of architectural boundaries
   - Layer dependency rules
   - Naming conventions
   - Aggregate encapsulation rules

### Coverage Targets

- **Domain Layer**: 95%+ coverage (critical business logic)
- **Application Layer**: 90%+ coverage (use case orchestration)
- **Presentation Layer**: 85%+ coverage (endpoint mapping)
- **Infrastructure Layer**: 70%+ coverage (focus on repository behaviors, exclude EF configurations)
- **Overall Project**: Minimum 80%, target 90%+

### Testing Framework Stack

- **xUnit**: Test framework for structure and execution
- **NSubstitute**: Mocking framework for test doubles
- **Shouldly**: Fluent assertions for readable test expectations
- **Coverlet**: Code coverage collection
- **ReportGenerator**: HTML coverage reports

## Rationale

### Why High Coverage Matters

1. **Regression Prevention**: Changes that break existing functionality are caught immediately
2. **Refactoring Confidence**: Developers can safely restructure code knowing tests will catch issues
3. **Documentation**: Tests document expected behavior better than comments
4. **Design Feedback**: Hard-to-test code often indicates poor design; tests drive better architecture
5. **Team Communication**: Tests clarify intent and serve as examples for new team members
6. **CI/CD Enablement**: Automated tests are required for safe continuous deployment
7. **Cost Reduction**: Bugs caught early cost 10-100x less to fix than production bugs
8. **Quality Assurance**: Coverage metrics provide objective quality indicators

### Why Unit Tests Are Valuable

1. **Speed**: Run in milliseconds, enabling rapid feedback during development
2. **Isolation**: Test one component at a time, making failures easy to diagnose
3. **Reliability**: No external dependencies means tests don't fail due to infrastructure issues
4. **Maintainability**: Simple, focused tests are easier to update as requirements change
5. **Design Quality**: Writing testable code naturally leads to better separation of concerns

### Coverage Goals Justification

- **80% minimum** ensures core business logic is protected
- **90% target** provides comprehensive safety net without diminishing returns
- **95% domain coverage** because business logic is most critical to protect
- **70% infrastructure** acknowledges EF configurations need less testing than business logic

## Consequences

### Positive

- **Faster Development**: Fast feedback loop catches issues immediately during coding
- **Safer Refactoring**: Comprehensive test suite enables confident code improvements
- **Living Documentation**: Tests serve as up-to-date examples of system behavior
- **Quality Metrics**: Coverage reports provide objective quality indicators for code reviews
- **Reduced Debugging**: Most bugs caught by tests, reducing time spent debugging production issues
- **Team Confidence**: Developers trust the codebase because tests validate expected behavior
- **Onboarding Aid**: New developers learn system behavior by reading tests
- **CI/CD Ready**: Automated testing enables safe continuous integration and deployment
- **Lower Maintenance Cost**: Well-tested code is easier and safer to modify over time

### Negative

- **Initial Time Investment**: Writing comprehensive tests takes time upfront
- **Test Maintenance**: Tests must be updated when requirements change
- **False Confidence**: High coverage doesn't guarantee correctness (quality matters more than quantity)
- **Test Bloat**: Poorly written tests can slow down build pipelines
- **Learning Curve**: Team members need training in testing best practices and frameworks

### Neutral

- **Coverage as Guideline**: 80-90% is a target, not absolute requirement; some code legitimately doesn't need testing
- **Test Discipline**: Requires team commitment to write tests before or alongside production code
- **Tooling Investment**: Need coverage analysis tools, reporting infrastructure, and CI integration
- **Review Focus**: Code reviews must verify test quality, not just existence of tests

## Implementation Guidelines

### Unit Test Structure (Arrange-Act-Assert)

```csharp
namespace CoreModule.UnitTests.Domain;

public class CustomerTests
{
    [Fact]
    public void Create_Should_SetPropertiesCorrectly_When_ValidInputProvided()
    {
        // Arrange
        var customerNumber = CustomerNumber.Create("CUST-12345").Value;
        var emailAddress = EmailAddress.Create("john.doe@example.com").Value;
        var firstName = "John";
        var lastName = "Doe";

        // Act
        var result = Customer.Create(customerNumber, emailAddress, firstName, lastName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerNumber.Should().Be(customerNumber);
        result.Value.Email.Should().Be(emailAddress);
        result.Value.FirstName.Should().Be(firstName);
        result.Value.LastName.Should().Be(lastName);
    }

    [Fact]
    public void ChangeEmail_Should_ReturnFailure_When_EmailIsNull()
    {
        // Arrange
        var customer = CustomerTestDataBuilder.WithDefaults().Build();

        // Act
        var result = customer.ChangeEmail(null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("Email cannot be null"));
    }
}
```

### Test Naming Convention

**Pattern**: `MethodName_Should_ExpectedBehavior_When_StateOrCondition`

Examples:

- `Create_Should_ReturnSuccess_When_ValidDataProvided`
- `ChangeEmail_Should_ReturnFailure_When_EmailIsInvalid`
- `Handle_Should_CreateCustomer_When_CommandIsValid`
- `Validate_Should_FailValidation_When_FirstNameIsEmpty`

### What to Test

#### Domain Layer (95%+ coverage)

- CORRECT Aggregate factory methods (`Customer.Create()`)
- CORRECT Business rule enforcement (`EmailShouldBeUniqueRule`)
- CORRECT Value object creation and validation (`EmailAddress.Create()`)
- CORRECT Domain event publishing
- CORRECT State transitions and invariant enforcement
- CORRECT Enumeration behavior

#### Application Layer (90%+ coverage)

- CORRECT Command/query handlers
- CORRECT Validators (FluentValidation rules)
- CORRECT Mapping configurations (Mapster)
- CORRECT Specifications for queries
- WRONG Pipeline behaviors (covered by integration tests)

#### Presentation Layer (85%+ coverage)

- CORRECT Endpoint routing and parameter binding
- CORRECT HTTP method mappings
- WRONG Minimal API infrastructure (framework code)

#### Infrastructure Layer (70%+ coverage)

- CORRECT Repository behavior decorators
- CORRECT Custom query logic
- WRONG EF Core entity configurations (convention-based)
- WRONG Migration files (generated code)

### Test Coverage Tooling

```powershell
# Run unit tests with coverage
pwsh -NoProfile -File .\bdk.ps1 -Task test-unit-all

# Generate HTML coverage report
pwsh -NoProfile -File .\bdk.ps1 -Task coverage-all-html

# Open coverage report in browser
pwsh -NoProfile -File .\bdk.ps1 -Task coverage-open
```

Coverage configuration in `coverlet.runsettings`:

- Threshold: 80% minimum
- Excludes: migrations, generated code, program entry points
- Output: Cobertura XML + HTML reports

### Test Organization

```text
tests/
├── Modules/
│   └── CoreModule/
│       ├── CoreModule.UnitTests/
│       │   ├── Domain/
│       │   │   ├── CustomerTests.cs
│       │   │   ├── ValueObjects/
│       │   │   │   └── EmailAddressTests.cs
│       │   │   └── Rules/
│       │   │       └── EmailShouldBeUniqueRuleTests.cs
│       │   ├── Application/
│       │   │   ├── Commands/
│       │   │   │   └── CustomerCreateCommandHandlerTests.cs
│       │   │   └── Queries/
│       │   │       └── CustomerFindAllQueryHandlerTests.cs
│       │   ├── MappingTests.cs
│       │   └── ArchitectureTests.cs
│       └── CoreModule.IntegrationTests/
│           └── Endpoints/
│               └── CustomerEndpointsTests.cs
```

## Alternatives Considered

### Alternative 1: Focus on Integration Tests Only

- Rejected because integration tests are slower and harder to maintain
- Debugging is harder when tests span multiple layers
- Requires infrastructure setup (databases, services)

### Alternative 2: Test-Driven Development (TDD) Requirement

- Considered but not mandated; TDD encouraged but not enforced
- Strict TDD can slow initial exploration of solutions
- Tests required before PR merge, but writing order is flexible

### Alternative 3: Lower Coverage Targets (50-60%)

- Rejected because it doesn't provide sufficient regression protection
- Too many critical paths left untested
- Industry best practices recommend 80%+ for enterprise applications

### Alternative 4: 100% Coverage Requirement

- Rejected as unrealistic and counterproductive
- Diminishing returns beyond 90%
- Forces testing trivial code (getters, setters, auto-properties)
- Can lead to poor quality tests written just to hit coverage goals

## Related Decisions

- [ADR-0001](0001-clean-onion-architecture.md): Clean Architecture enables testability by isolating layers
- [ADR-0002](0002-result-pattern-error-handling.md): Result\<T> pattern makes error cases explicit and testable
- [ADR-0009](0009-fluentvalidation-strategy.md): Validation strategy with testable validators
- [ADR-0011](0011-application-logic-in-commands-queries.md): Application handlers are primary unit test targets

## References

- [xUnit Documentation](https://xunit.net/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)
- [Shouldly Documentation](https://docs.shouldly.org/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [Martin Fowler - Test Pyramid](https://martinfowler.com/bliki/TestPyramid.html)
- [Clean Architecture - Testing Strategies](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)

## Notes

### Coverage Monitoring

- Coverage reports generated automatically in CI pipeline
- Pull requests show coverage delta (increase/decrease)
- Coverage gates prevent merging PRs that significantly decrease coverage
- Regular team reviews of coverage trends

### Quality Over Quantity

While high coverage is the goal, test quality matters more:

- **Good Test**: Tests behavior, not implementation
- **Good Test**: Has clear Arrange-Act-Assert structure
- **Good Test**: Uses meaningful test data
- **Good Test**: Has descriptive name explaining scenario
- **Bad Test**: Tests private methods directly
- **Bad Test**: Tests framework/library code
- **Bad Test**: Brittle tests that break with any refactoring

### Coverage Exclusions (Justified)

```csharp
[ExcludeFromCodeCoverage] // Applied to:
- Generated code (migrations, EF configurations)
- Entry points (Program.cs)
- Trivial properties (auto-properties without logic)
- Framework integration points (minimal API boilerplate)
```

### CI/CD Integration

```yaml
# GitHub Actions / Azure DevOps
- Run unit tests
- Collect coverage
- Generate reports
- Enforce minimum 80% coverage
- Publish coverage to PR comments
- Fail build if coverage drops below threshold
```

### Implementation Location

- **Unit Tests**: `tests/Modules/CoreModule/CoreModule.UnitTests/`
- **Integration Tests**: `tests/Modules/CoreModule/CoreModule.IntegrationTests/`
- **Coverage Config**: `coverlet.runsettings`
- **Tasks**: `bdk.ps1` (test-unit-all, coverage-all-html, coverage-open)
- **Test Data Builders**: `tests/Modules/CoreModule/CoreModule.UnitTests/Builders/`

### Team Practices

1. **Test-First Mindset**: Write tests during feature development, not after
2. **Red-Green-Refactor**: Make test fail → make it pass → improve code
3. **Code Review**: Tests reviewed as carefully as production code
4. **Coverage Monitoring**: Weekly review of coverage trends
5. **Test Ownership**: Feature developers own tests for their features
6. **Continuous Improvement**: Regular retrospectives on testing practices
