// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

[UnitTest("Common")]
public class ResultOperationScopeTests
{
    private readonly Faker faker = new();

    [Fact]
    public void StartOperation_WithSuccessResult_DoesNotCallFactoryImmediately()
    {
        // Arrange
        var factoryCalled = false;
        var value = this.faker.Random.Int(1, 100);

        IOperationScope Factory(CancellationToken ct)
        {
            factoryCalled = true;
            return new TestOperationScope();
        }

        // Act
        var scope = Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult(Factory(ct)));

        // Assert
        factoryCalled.ShouldBeFalse(); // Factory not called yet (lazy)
    }

    [Fact]
    public async Task EndOperationAsync_WithSuccessResult_CallsCommit()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);

        // Act
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .TapAsync(async (v, ct) => await Task.Delay(1, ct))
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(value);
        testScope.IsCommitted.ShouldBeTrue();
        testScope.IsRolledBack.ShouldBeFalse();
    }

    [Fact]
    public async Task EndOperationAsync_WithFailureResult_CallsRollback()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);
        var error = new ValidationError("Test error");

        // Act - Create success result, start operation, then fail during validation
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .TapAsync(async (v, ct) => await Task.Delay(1, ct)) // This starts the operation
            .EnsureAsync(async (v, ct) =>
            {
                await Task.Delay(1, ct);
                return false; // Force failure
            }, error)
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ValidationError>();
        testScope.IsCommitted.ShouldBeFalse();
        testScope.IsRolledBack.ShouldBeTrue();
    }

    [Fact]
    public async Task EndOperationAsync_WithException_CallsRollbackAndReturnsFailure()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);

        // Act
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .TapAsync(async (v, ct) =>
            {
                await Task.Delay(1, ct);
                throw new InvalidOperationException("Test exception");
            })
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Errors.ShouldContain(e => e.Message.Contains("Test exception"));
        testScope.IsCommitted.ShouldBeFalse();
        testScope.IsRolledBack.ShouldBeTrue();
    }

    [Fact]
    public async Task TapAsync_WithinScope_StartsOperationOnFirstAsync()
    {
        // Arrange
        var factoryCalled = false;
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);
        var tapped = false;

        IOperationScope Factory(CancellationToken ct)
        {
            factoryCalled = true;
            return testScope;
        }

        // Act
        var scope = Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult(Factory(ct)))
            .Tap(v => { /* sync operation - should not start operation */ });

        factoryCalled.ShouldBeFalse(); // Factory still not called after sync operation

        var result = await scope
            .TapAsync(async (v, ct) =>
            {
                await Task.Delay(1, ct);
                tapped = true;
            })
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        tapped.ShouldBeTrue();
        factoryCalled.ShouldBeTrue(); // Factory called on first async operation
        testScope.IsCommitted.ShouldBeTrue();
    }

    [Fact]
    public async Task MapAsync_WithinScope_TransformsValue()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);

        // Act
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .MapAsync(async (v, ct) =>
            {
                await Task.Delay(1, ct);
                return v.ToString();
            })
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(value.ToString());
        testScope.IsCommitted.ShouldBeTrue();
    }

    [Fact]
    public async Task BindAsync_WithinScope_ChainsResults()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);

        // Act
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .BindAsync(async (v, ct) =>
            {
                await Task.Delay(1, ct);
                return v >= 50
                    ? Result<string>.Success($"Value {v} is valid")
                    : Result<string>.Failure()
                        .WithError(new ValidationError("Value too small"));
            })
            .EndOperationAsync(CancellationToken.None);

        // Assert
        if (value >= 50)
        {
            result.ShouldBeSuccess();
            result.Value.ShouldContain("valid");
            testScope.IsCommitted.ShouldBeTrue();
        }
        else
        {
            result.ShouldBeFailure();
            result.ShouldContainError<ValidationError>();
            testScope.IsRolledBack.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task EnsureAsync_WithinScope_ValidatesConditions()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);
        var error = new ValidationError("Value must be greater than 50");

        // Act
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .EnsureAsync(
                async (v, ct) =>
                {
                    await Task.Delay(1, ct);
                    return v > 50;
                },
                error)
            .EndOperationAsync(CancellationToken.None);

        // Assert
        if (value > 50)
        {
            result.ShouldBeSuccess();
            testScope.IsCommitted.ShouldBeTrue();
        }
        else
        {
            result.ShouldBeFailure();
            result.ShouldContainError<ValidationError>();
            testScope.IsRolledBack.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task UnlessAsync_WithinScope_ValidatesNegativeConditions()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);
        var error = new ValidationError("Value must not be greater than 50");

        // Act
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .UnlessAsync(
                async (v, ct) =>
                {
                    await Task.Delay(1, ct);
                    return v > 50;
                },
                error)
            .EndOperationAsync(CancellationToken.None);

        // Assert
        if (value > 50)
        {
            result.ShouldBeFailure();
            result.ShouldContainError<ValidationError>();
            testScope.IsRolledBack.ShouldBeTrue();
        }
        else
        {
            result.ShouldBeSuccess();
            testScope.IsCommitted.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task EndOperationAsync_WithDelegates_CallsCustomCommitRollback()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);
        var customCommitCalled = false;
        var customRollbackCalled = false;

        // Act
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .TapAsync(async (v, ct) => await Task.Delay(1, ct))
            .EndOperationAsync(
                commitAsync: async (op, ct) =>
                {
                    customCommitCalled = true;
                    await op.CommitAsync(ct);
                },
                rollbackAsync: async (op, ex, ct) =>
                {
                    customRollbackCalled = true;
                    await op.RollbackAsync(ct);
                },
                CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        customCommitCalled.ShouldBeTrue();
        customRollbackCalled.ShouldBeFalse();
        testScope.IsCommitted.ShouldBeTrue();
    }

    [Fact]
    public async Task ComplexChain_WithMultipleOperations_WorksCorrectly()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(50, 100);
        var steps = new List<string>();

        // Act
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .Tap(v => steps.Add("Tap1"))
            .TapAsync(async (v, ct) =>
            {
                await Task.Delay(1, ct);
                steps.Add("TapAsync1");
            })
            .Map(v => v * 2)
            .Tap(v => steps.Add("Tap2"))
            .MapAsync(async (v, ct) =>
            {
                await Task.Delay(1, ct);
                steps.Add("MapAsync");
                return v.ToString();
            })
            .EnsureAsync(
                async (v, ct) =>
                {
                    await Task.Delay(1, ct);
                    steps.Add("EnsureAsync");
                    return v.Length > 0;
                },
                new ValidationError("Invalid"))
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe((value * 2).ToString());
        testScope.IsCommitted.ShouldBeTrue();
        steps.Count.ShouldBe(5);
        steps.ShouldContain("Tap1");
        steps.ShouldContain("TapAsync1");
        steps.ShouldContain("Tap2");
        steps.ShouldContain("MapAsync");
        steps.ShouldContain("EnsureAsync");
    }

    [Fact]
    public async Task OperationScope_WithShortCircuit_SkipsRemainingOperations()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);
        var operationsExecuted = new List<string>();

        // Act
        var result = await Result<int>.Success(value)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .TapAsync(async (v, ct) =>
            {
                await Task.Delay(1, ct);
                operationsExecuted.Add("First");
            })
            .EnsureAsync(
                async (v, ct) =>
                {
                    await Task.Delay(1, ct);
                    operationsExecuted.Add("Ensure");
                    return false; // This will fail
                },
                new ValidationError("Validation failed"))
            .TapAsync(async (v, ct) =>
            {
                await Task.Delay(1, ct);
                operationsExecuted.Add("After-Ensure"); // Should not execute
            })
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ValidationError>();
        testScope.IsRolledBack.ShouldBeTrue();
        operationsExecuted.Count.ShouldBe(2);
        operationsExecuted.ShouldNotContain("After-Ensure");
    }

    [Fact]
    public async Task StartOperation_WithAsyncFactory_CreatesOperationLazily()
    {
        // Arrange
        var factoryCalled = false;
        var value = this.faker.Random.Int(1, 100);

        async Task<IOperationScope> OperationFactory(CancellationToken ct)
        {
            await Task.Delay(1, ct);
            factoryCalled = true;
            return new TestOperationScope();
        }

        // Act
        var scope = Result<int>.Success(value)
            .StartOperation(OperationFactory);

        // Assert
        factoryCalled.ShouldBeFalse(); // Not called yet (lazy)

        // Now trigger the operation
        var result = await scope
            .TapAsync(async (v, ct) => await Task.Delay(1, ct))
            .EndOperationAsync(CancellationToken.None);

        // Assert
        factoryCalled.ShouldBeTrue();
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task MessagePropagation_ThroughOperationScope_MaintainsMessages()
    {
        // Arrange
        var testScope = new TestOperationScope();
        var value = this.faker.Random.Int(1, 100);
        var message1 = this.faker.Lorem.Sentence();
        var message2 = this.faker.Lorem.Sentence();

        // Act
        var operationResult = await Result<int>.Success(value)
            .WithMessage(message1)
            .StartOperation(ct => Task.FromResult<IOperationScope>(testScope))
            .TapAsync(async (v, ct) => await Task.Delay(1, ct))
            .EndOperationAsync(CancellationToken.None);

        var result = operationResult
            .Map(v => v)
            .WithMessage(message2);

        // Assert
        result.ShouldBeSuccess();
        result.Messages.Count.ShouldBe(2);
        result.Messages.ShouldContain(message1);
        result.Messages.ShouldContain(message2);
    }

}

// Test implementation of IOperationScope
internal class TestOperationScope : IOperationScope
{
    public bool IsCommitted { get; private set; }
    public bool IsRolledBack { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        this.IsCommitted = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        this.IsRolledBack = true;
        return Task.CompletedTask;
    }
}
