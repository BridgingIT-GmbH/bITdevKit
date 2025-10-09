// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

[UnitTest("Common")]
public class ResultNonGenericTests
{
    private readonly Faker faker = new();

    [Fact]
    public void WithMessage_WithValidMessage_AddsMessage()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var sut = Result.Success();

        // Act
        var result = sut.WithMessage(message);

        // Assert
        result.ShouldBeSuccess();
        result.Messages.Count.ShouldBe(1);
        result.ShouldContainMessage(message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void WithMessage_WithInvalidMessage_DoesNotAddMessage(string invalidMessage)
    {
        // Arrange
        var sut = Result.Success();

        // Act
        var result = sut.WithMessage(invalidMessage);

        // Assert
        result.ShouldBeSuccess();
        result.Messages.Count.ShouldBe(0);
    }

    [Fact]
    public void WithMessages_WithValidMessages_AddsAllMessages()
    {
        // Arrange
        var messages = new[] { this.faker.Random.Words(), this.faker.Random.Words() };
        var sut = Result.Success();

        // Act
        var result = sut.WithMessages(messages);

        // Assert
        result.ShouldBeSuccess();
        result.Messages.Count.ShouldBe(2);
        result.Messages.ShouldBe(messages);
    }

    [Fact]
    public void WithMessages_WithNullCollection_DoesNotAddMessages()
    {
        // Arrange
        IEnumerable<string> messages = null;
        var sut = Result.Success();

        // Act
        var result = sut.WithMessages(messages);

        // Assert
        result.ShouldBeSuccess();
        result.Messages.Count.ShouldBe(0);
    }

    [Fact]
    public void WithError_WithValidError_AddsErrorAndSetsFailure()
    {
        // Arrange
        var error = new NotFoundError();
        var sut = Result.Success();

        // Act
        var result = sut.WithError(error);

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public void WithError_WithNullError_DoesNotAddError()
    {
        // Arrange
        IResultError error = null;
        var sut = Result.Success();

        // Act
        var result = sut.WithError(error);

        // Assert
        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void WithErrors_WithValidErrors_AddsAllErrorsAndSetsFailure()
    {
        // Arrange
        var errors = new IResultError[]
        {
            new NotFoundError(),
            new Error("Custom error")
        };
        var sut = Result.Success();

        // Act
        var result = sut.WithErrors(errors);

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(2);
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public void WithError_Success_AddsTypedErrorAndSetsFailure()
    {
        // Arrange
        var sut = Result.Success();

        // Act
        var result = sut.WithError<NotFoundError>();

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public void HasError_Failure_ReturnsTrueForMatchingErrorType()
    {
        // Arrange
        var sut = Result.Failure()
            .WithError<NotFoundError>();

        // Act
        var hasError = sut.HasError<NotFoundError>();

        // Assert
        hasError.ShouldBeTrue();
    }

    [Fact]
    public void HasError_WithOutParameter_ReturnsMatchingErrors()
    {
        // Arrange
        var sut = Result.Failure()
            .WithError<NotFoundError>()
            .WithError<NotFoundError>();

        // Act
        var hasError = sut.TryGetErrors<NotFoundError>(out var errors);

        // Assert
        hasError.ShouldBeTrue();
        errors.Count().ShouldBe(2);
        errors.ShouldAllBe(e => e is NotFoundError);
    }

    [Fact]
    public void HasError_WithNoErrors_ReturnsFalse()
    {
        // Arrange
        var sut = Result.Success();

        // Act
        var hasError = sut.HasError();

        // Assert
        hasError.ShouldBeFalse();
    }

    [Fact]
    public void Implicit_BoolOperator_ReturnsTrueForSuccess()
    {
        // Arrange
        var sut = Result.Success();

        // Act
        bool success = sut;

        // Assert
        success.ShouldBeTrue();
    }

    [Fact]
    public void Implicit_BoolOperator_ReturnsFalseForFailure()
    {
        // Arrange
        var sut = Result.Failure();

        // Act
        bool success = sut;

        // Assert
        success.ShouldBeFalse();
    }

    [Fact]
    public void ToString_WithSuccessAndMessages_FormatsCorrectly()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var sut = Result.Success(message);

        // Act
        var result = sut.ToString();

        // Assert
        result.ShouldContain("Result succeeded");
        result.ShouldContain(message);
    }

    [Fact]
    public void ToString_WithFailureAndErrors_FormatsCorrectly()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var sut = Result.Failure(message)
            .WithError<EntityNotFoundError>();

        // Act
        var result = sut.ToString();

        // Assert
        result.ShouldContain("Result failed");
        result.ShouldContain(message);
        result.ShouldContain("EntityNotFoundError");
    }

    [Fact]
    public void To_WithNoValue_CreatesNewResult()
    {
        // Arrange
        // Act
        var result = Result.ToResult();

        // Assert
        result.ShouldBeSuccess();
        result.ShouldNotContainMessages();
    }

    [Fact]
    public void To_String_CreatesResult()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var sut = Result.Success(message);

        // Act
        var result = sut.ToResult<string>();

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage(message);
        result.Value.ShouldBeNull();
    }

    [Fact]
    public void To_WithValue_CreatesResultWithValue()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var value = this.faker.Random.Word();
        var sut = Result.Success(message);

        // Act
        var result = sut.ToResult(value);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage(message);
        result.Value.ShouldBe(value);
    }

    [Fact]
    public void Failure_WitErrorAndMessage_CreatesFailureResult()
    {
        // Arrange
        var message = this.faker.Random.Words();

        // Act
        var result = Result.Failure<NotFoundError>(message);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainMessage(message);
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public void Failure_WithMessagesAndErrors_CreatesFailureResult()
    {
        // Arrange
        var messages = new[] { this.faker.Random.Words(), this.faker.Random.Words() };
        var errors = new IResultError[]
        {
            new NotFoundError(),
            new Error("Custom error")
        };

        // Act
        var result = Result.Failure(messages, errors);

        // Assert
        result.ShouldBeFailure();
        result.Messages.Count.ShouldBe(2);
        result.Errors.Count.ShouldBe(2);
        result.Messages.ShouldBe(messages);
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public void Success_WithMessages_CreatesSuccessResult()
    {
        // Arrange
        var messages = new[] { this.faker.Random.Words(), this.faker.Random.Words() };

        // Act
        var result = Result.Success(messages);

        // Assert
        result.ShouldBeSuccess();
        result.Messages.Count.ShouldBe(2);
        result.Messages.ShouldBe(messages);
    }

    [Fact]
    public void From_WithValidOperation_ReturnsSuccessResult()
    {
        // Arrange
        var operationExecuted = false;
        Action operation = () => operationExecuted = true;

        // Act
        var result = Result.Bind(operation);

        // Assert
        result.ShouldBeSuccess();
        operationExecuted.ShouldBeTrue();
    }

    [Fact]
    public void From_WithNullOperation_ReturnsFailureResult()
    {
        // Arrange
        Action operation = null;

        // Act
        var result = Result.Bind(operation);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<Error>();
    }

    [Fact]
    public void From_WithThrowingOperation_ReturnsFailureResult()
    {
        // Arrange
        var errorMessage = this.faker.Random.Words();
        Action operation = () => throw new InvalidOperationException(errorMessage);

        // Act
        var result = Result.Bind(operation);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ExceptionError>();
        result.ShouldContainMessage(errorMessage);
    }

    [Fact]
    public async Task FromAsync_WithValidOperation_ReturnsSuccessResult()
    {
        // Arrange
        var operationExecuted = false;
        Func<CancellationToken, Task> operation = _ =>
        {
            operationExecuted = true;
            return Task.CompletedTask;
        };

        // Act
        var result = await Result.BindAsync(operation);

        // Assert
        result.ShouldBeSuccess();
        operationExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task FromAsync_WithCancellation_ReturnsFailureResult()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        Func<CancellationToken, Task> operation = async ct =>
        {
            await Task.Delay(1000, ct);
        };

        // Act
        cts.Cancel();
        var result = await Result.BindAsync(operation, cts.Token);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<OperationCancelledError>();
    }

    [Fact]
    public void Ensure_WithValidPredicate_ReturnsOriginalResult()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var sut = Result.Success(message);

        // Act
        var result = sut.Ensure(() => true, new Error("Should not appear"));

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage(message);
    }

    [Fact]
    public void Ensure_WithFailingPredicate_ReturnsFailureResult()
    {
        // Arrange
        var error = new NotFoundError();
        var sut = Result.Success();

        // Act
        var result = sut.Ensure(() => false, error);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<NotFoundError>();
    }

    [Fact]
    public async Task EnsureAsync_WithValidPredicate_ReturnsOriginalResult()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var sut = Result.Success(message);

        // Act
        var result = await sut.EnsureAsync(
            _ => Task.FromResult(true),
            new Error("Should not appear"));

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage(message);
    }

    [Fact]
    public void From_WithSuccessfulOperation_ReturnsSuccessResult()
    {
        // Arrange
        var operationExecuted = false;
        Action operation = () => operationExecuted = true;

        // Act
        var result = Result.Bind(operation);

        // Assert
        result.ShouldBeSuccess();
        operationExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task FromAsync_WithSuccessfulOperation_ReturnsSuccessResult()
    {
        // Arrange
        var operationExecuted = false;
        Func<CancellationToken, Task> operation = _ =>
        {
            operationExecuted = true;
            return Task.CompletedTask;
        };

        // Act
        var result = await Result.BindAsync(operation);

        // Assert
        result.ShouldBeSuccess();
        operationExecuted.ShouldBeTrue();
    }

    [Fact]
    public void Tap_ExecutesActionOnSuccess()
    {
        // Arrange
        var actionExecuted = false;
        var sut = Result.Success();

        // Act
        var result = sut.Tap(() => actionExecuted = true);

        // Assert
        result.ShouldBeSuccess();
        actionExecuted.ShouldBeTrue();
    }

    [Fact]
    public void TeeMap_ExecutesCorrectActionBasedOnResult()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = Result.Success();

        // Act
        var result = sut.TeeMap(
            () => successExecuted = true,
            _ => failureExecuted = true);

        // Assert
        result.ShouldBeSuccess();
        successExecuted.ShouldBeTrue();
        failureExecuted.ShouldBeFalse();
    }

    [Fact]
    public void Filter_WithValidPredicate_ReturnsOriginalResult()
    {
        // Arrange
        var sut = Result.Success();

        // Act
        var result = sut.Filter(() => true, new Error("Should not appear"));

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Unless_WithFailingPredicate_ReturnsOriginalResult()
    {
        // Arrange
        var sut = Result.Success();

        // Act
        var result = sut.Unless(() => false, new Error("Should not appear"));

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void AndThen_ChainsSuccessfulOperations()
    {
        // Arrange
        var firstExecuted = false;
        var secondExecuted = false;
        var sut = Result.Success();

        // Act
        var result = sut
            .AndThen(() =>
            {
                firstExecuted = true;
                return Result.Success();
            })
            .AndThen(() =>
            {
                secondExecuted = true;
                return Result.Success();
            });

        // Assert
        result.ShouldBeSuccess();
        firstExecuted.ShouldBeTrue();
        secondExecuted.ShouldBeTrue();
    }

    [Fact]
    public void OrElse_WithSuccessResult_DoesNotExecuteFallback()
    {
        // Arrange
        var fallbackExecuted = false;
        var sut = Result.Success();

        // Act
        var result = sut.OrElse(() =>
        {
            fallbackExecuted = true;
            return Result.Success();
        });

        // Assert
        result.ShouldBeSuccess();
        fallbackExecuted.ShouldBeFalse();
    }

    [Fact]
    public void Switch_ExecutesActionOnCondition()
    {
        // Arrange
        var actionExecuted = false;
        var sut = Result.Success();

        // Act
        var result = sut.Switch(() => true, () => actionExecuted = true);

        // Assert
        result.ShouldBeSuccess();
        actionExecuted.ShouldBeTrue();
    }

    [Fact]
    public void Match_ReturnsCorrectValue()
    {
        // Arrange
        var successValue = this.faker.Random.Word();
        var failureValue = this.faker.Random.Word();
        var sut = Result.Success();

        // Act
        var result = sut.Match(successValue, failureValue);

        // Assert
        result.ShouldBe(successValue);
    }

    [Fact]
    public void Handle_WithSuccess_ExecutesSuccessAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = Result.Success();

        // Act
        var result = sut.Handle(
            onSuccess: () => successExecuted = true,
            onFailure: _ => failureExecuted = true);

        // Assert
        result.ShouldBeSuccess();
        successExecuted.ShouldBeTrue();
        failureExecuted.ShouldBeFalse();
    }

    [Fact]
    public void Handle_WithFailure_ExecutesFailureAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = Result.Failure()
            .WithError<NotFoundError>();

        // Act
        var result = sut.Handle(
            onSuccess: () => successExecuted = true,
            onFailure: errors => failureExecuted = true);

        // Assert
        result.ShouldBeFailure();
        successExecuted.ShouldBeFalse();
        failureExecuted.ShouldBeTrue();
    }

    [Fact]
    public void Handle_ReturnsOriginalResult()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var sut = Result.Success(message);

        // Act
        var result = sut.Handle(
            onSuccess: () => { },
            onFailure: _ => { });

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage(message);
    }

    [Fact]
    public async Task HandleAsync_WithSuccess_ExecutesSuccessAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = Result.Success();

        // Act
        var result = await sut.HandleAsync(
            onSuccess: ct =>
            {
                successExecuted = true;
                return Task.CompletedTask;
            },
            onFailure: (errors, ct) =>
            {
                failureExecuted = true;
                return Task.CompletedTask;
            });

        // Assert
        result.ShouldBeSuccess();
        successExecuted.ShouldBeTrue();
        failureExecuted.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithFailure_ExecutesFailureAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = Result.Failure()
            .WithError<NotFoundError>();

        // Act
        var result = await sut.HandleAsync(
            onSuccess: ct =>
            {
                successExecuted = true;
                return Task.CompletedTask;
            },
            onFailure: (errors, ct) =>
            {
                failureExecuted = true;
                return Task.CompletedTask;
            });

        // Assert
        result.ShouldBeFailure();
        successExecuted.ShouldBeFalse();
        failureExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_MixedHandlers_WithSuccess_ExecutesSuccessAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = Result.Success();

        // Act
        var result = await sut.HandleAsync(
            onSuccess: ct =>
            {
                successExecuted = true;
                return Task.CompletedTask;
            },
            onFailure: _ => failureExecuted = true);

        // Assert
        result.ShouldBeSuccess();
        successExecuted.ShouldBeTrue();
        failureExecuted.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_CancelsOperation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var sut = Result.Success();
        cts.Cancel();

        // Act/Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await sut.HandleAsync(
                async ct =>
                {
                    await Task.Delay(1000, ct);
                },
                async (_, ct) =>
                {
                    await Task.Delay(1000, ct);
                },
                cts.Token);
        });
    }

    [Fact]
    public async Task HandleAsync_ReturnsOriginalResult()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var sut = Result.Success(message);

        // Act
        var result = await sut.HandleAsync(
            onSuccess: _ => Task.CompletedTask,
            onFailure: (_, _) => Task.CompletedTask);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage(message);
    }
}