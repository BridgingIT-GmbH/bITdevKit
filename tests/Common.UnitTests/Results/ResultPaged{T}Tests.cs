#nullable enable

namespace BridgingIT.DevKit.Common.UnitTests.Results;

[UnitTest("Common")]
public class ResultPagedTests
{
    private readonly IEnumerable<string> messages = ["message1", "message2"];
    private readonly long count = 100;
    private readonly int page = 2;
    private readonly int pageSize = 10;
    private readonly IEnumerable<PersonStub> values = new[] { PersonStub.Create(1), PersonStub.Create(2) }.ToList();
    private readonly Faker faker = new();

    [Fact]
    public void Success_ShouldSetProperties()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values, this.count, this.page, this.pageSize);

        // Act & Assert
        sut.Value.ShouldBe(this.values);
        sut.CurrentPage.ShouldBe(this.page);
        sut.PageSize.ShouldBe(this.pageSize);
        sut.TotalCount.ShouldBe(this.count);
        sut.TotalPages.ShouldBe((int)Math.Ceiling(this.count / (double)this.pageSize));
        sut.HasNextPage.ShouldBeTrue();
        sut.HasPreviousPage.ShouldBeTrue();
        sut.ShouldBeSuccess();
    }

    [Fact]
    public void Success_WithMessage_ShouldSetMessage()
    {
        // Arrange
        const string message = "message1";
        var sut = ResultPaged<PersonStub>.Success(this.values, message, this.count, this.page, this.pageSize);

        // Act & Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(message);
    }

    [Fact]
    public void Success_WithMessages_ShouldSetMessages()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values, this.messages, this.count, this.page, this.pageSize);

        // Act & Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(this.messages.First());
    }

    [Fact]
    public void SuccessIf_WithTrueCondition_ShouldReturnSuccess()
    {
        // Arrange & Act
        var result = ResultPaged<PersonStub>.SuccessIf(
            true,
            this.values,
            this.count,
            this.page,
            this.pageSize);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(this.values);
        result.CurrentPage.ShouldBe(this.page);
    }

    [Fact]
    public void SuccessIf_WithFalseCondition_ShouldReturnFailure()
    {
        // Arrange
        var error = new ValidationError("test", "Test error");

        // Act
        var result = ResultPaged<PersonStub>.SuccessIf(
            false,
            this.values,
            this.count,
            this.page,
            this.pageSize,
            error);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ValidationError>();
    }

    [Fact]
    public void SuccessIf_WithPredicate_ShouldEvaluateCorrectly()
    {
        // Arrange & Act
        var result = ResultPaged<PersonStub>.SuccessIf(
            values => values.Any(),
            this.values,
            this.count,
            this.page,
            this.pageSize);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(this.values);
    }

    [Fact]
    public void Failure_ShouldSetSuccessToFalse()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Failure();

        // Act & Assert
        sut.ShouldBeFailure();
    }

    [Fact]
    public void FailureTError_ShouldAddError()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Failure<NotFoundError>();

        // Act & Assert
        sut.ShouldContainError<NotFoundError>();
        sut.ShouldBeFailure();
    }

    [Fact]
    public void Failure_ShouldSetMessage()
    {
        // Arrange
        const string message = "message1";

        // Act
        var sut = ResultPaged<PersonStub>.Failure(message);

        // Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(message);
    }

    [Fact]
    public void FailureIf_WithTrueCondition_ShouldReturnFailure()
    {
        // Arrange
        var error = new ValidationError("test", "Test error");

        // Act
        var result = ResultPaged<PersonStub>.FailureIf(
            true,
            this.values,
            this.count,
            this.page,
            this.pageSize,
            error);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ValidationError>();
    }

    [Fact]
    public void FailureIf_WithFalseCondition_ShouldReturnSuccess()
    {
        // Arrange & Act
        var result = ResultPaged<PersonStub>.FailureIf(
            false,
            this.values,
            this.count,
            this.page,
            this.pageSize);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(this.values);
    }

    [Fact]
    public void For_WithValidOperation_ShouldReturnSuccess()
    {
        // Arrange & Act
        var result = ResultPaged<PersonStub>.For(
            () => (this.values, this.count),
            this.page,
            this.pageSize);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(this.values);
        result.TotalCount.ShouldBe(this.count);
    }

    [Fact]
    public void For_WithException_ShouldReturnFailure()
    {
        // Arrange & Act
        var result = ResultPaged<PersonStub>.For(
            () => throw new InvalidOperationException("Test exception"),
            this.page,
            this.pageSize);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ExceptionError>();
    }

    [Fact]
    public async Task ForAsync_WithValidOperation_ShouldReturnSuccess()
    {
        // Arrange & Act
        var result = await ResultPaged<PersonStub>.ForAsync(
            async ct =>
            {
                await Task.Delay(10, ct);

                return (this.values, this.count);
            },
            this.page,
            this.pageSize);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldBe(this.values);
        result.TotalCount.ShouldBe(this.count);
    }

    [Fact]
    public async Task ForAsync_WithCancellation_ShouldReturnFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await ResultPaged<PersonStub>.ForAsync(
            async ct =>
            {
                await Task.Delay(1000, ct);

                return (this.values, this.count);
            },
            this.page,
            this.pageSize,
            cts.Token);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<OperationCancelledError>();
    }

    [Fact]
    public void Failure_List_ShouldSetMessages()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Failure(this.messages);

        // Act & Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(this.messages.First());
    }

    [Fact]
    public void WithMessage_ShouldAddMessage()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values);
        const string message = "message1";

        // Act
        sut = sut.WithMessage(message);

        // Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(message);
    }

    [Fact]
    public void WithMessages_ShouldAddMessages()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values);

        // Act
        sut = sut.WithMessages(this.messages);

        // Assert
        sut.ShouldContainMessages();
        sut.ShouldContainMessage(this.messages.First());
    }

    [Fact]
    public void WithError_ShouldAddError()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values);

        // Act
        sut = sut.WithError(new NotFoundError());

        // Assert
        sut.ShouldContainError<NotFoundError>();
        sut.ShouldBeFailure();
    }

    [Fact]
    public void WithErrorWithGenericParameter_ShouldAddError()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values);

        // Act
        sut = sut.WithError<NotFoundError>();

        // Assert
        sut.ShouldContainError<NotFoundError>();
        sut.ShouldBeFailure();
    }

    [Fact]
    public void For_ConversionToResult_ShouldMaintainState()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values, "Test message")
            .WithError(new ValidationError("test", "Test error"));

        // Act
#pragma warning disable IDE0007 // Use implicit type
        Result result = sut.For();
#pragma warning restore IDE0007 // Use implicit type

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainMessage("Test message");
        result.ShouldContainError<ValidationError>();
    }

    [Fact]
    public void For_ConversionToNewType_ShouldMaintainPagination()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values, this.count, this.page, this.pageSize);

        // Act
        var result = sut.For<string>();

        // Assert
        result.CurrentPage.ShouldBe(this.page);
        result.PageSize.ShouldBe(this.pageSize);
        result.TotalCount.ShouldBe(this.count);
    }

    [Fact]
    public void For_ConversionWithValues_ShouldSetNewValues()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values, this.count, this.page, this.pageSize);
        var newValues = new[] { "test1", "test2" };

        // Act
        var result = sut.For(newValues);

        // Assert
        result.Value.ShouldBe(newValues);
        result.CurrentPage.ShouldBe(this.page);
        result.TotalCount.ShouldBe(this.count);
    }

    [Fact]
    public void ImplicitBoolConversion_ShouldReflectSuccessState()
    {
        // Arrange
        var success = ResultPaged<PersonStub>.Success(this.values);
        var failure = ResultPaged<PersonStub>.Failure();

        // Act & Assert
        bool successBool = success;
        bool failureBool = failure;

        successBool.ShouldBeTrue();
        failureBool.ShouldBeFalse();
    }

    [Fact]
    public void NavigationFlags_ShouldBeCorrect_ForDifferentPages()
    {
        // First page
        var firstPage = ResultPaged<PersonStub>.Success(this.values, 30, 1, 10);
        firstPage.HasPreviousPage.ShouldBeFalse();
        firstPage.HasNextPage.ShouldBeTrue();

        // Middle page
        var middlePage = ResultPaged<PersonStub>.Success(this.values, 30, 2, 10);
        middlePage.HasPreviousPage.ShouldBeTrue();
        middlePage.HasNextPage.ShouldBeTrue();

        // Last page
        var lastPage = ResultPaged<PersonStub>.Success(this.values, 30, 3, 10);
        lastPage.HasPreviousPage.ShouldBeTrue();
        lastPage.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void TotalPages_ShouldCalculateCorrectly_WithDifferentSizes()
    {
        // Exact division
        var result1 = ResultPaged<PersonStub>.Success(this.values, 100, 1, 10);
        result1.TotalPages.ShouldBe(10);

        // With remainder
        var result2 = ResultPaged<PersonStub>.Success(this.values, 95, 1, 10);
        result2.TotalPages.ShouldBe(10);

        // Single page
        var result3 = ResultPaged<PersonStub>.Success(this.values, 5, 1, 10);
        result3.TotalPages.ShouldBe(1);
    }

    [Fact]
    public void Handle_WithSuccess_ExecutesSuccessAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = ResultPaged<PersonStub>.Success(this.values, this.count, this.page, this.pageSize);

        // Act
        var result = sut.Handle(
            onSuccess: value =>
            {
                successExecuted = true;
                value.Count().ShouldBe(this.values.Count());
            },
            onFailure: _ => failureExecuted = true);

        // Assert
        result.ShouldBeSuccess();
        successExecuted.ShouldBeTrue();
        failureExecuted.ShouldBeFalse();
        result.CurrentPage.ShouldBe(this.page);
        result.PageSize.ShouldBe(this.pageSize);
        result.TotalCount.ShouldBe(this.count);
    }

    [Fact]
    public void Handle_WithFailure_ExecutesFailureAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = ResultPaged<PersonStub>.Failure()
            .WithError<NotFoundError>();

        // Act
        var result = sut.Handle(
            onSuccess: _ => successExecuted = true,
            onFailure: errors =>
            {
                failureExecuted = true;
                errors.Count.ShouldBe(1);
            });

        // Assert
        result.ShouldBeFailure();
        successExecuted.ShouldBeFalse();
        failureExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithSuccess_ExecutesSuccessAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = ResultPaged<PersonStub>.Success(this.values, this.count, this.page, this.pageSize);

        // Act
        var result = await sut.HandleAsync(
            onSuccess: async (value, ct) =>
            {
                await Task.Delay(10, ct);
                successExecuted = true;
                value.Count().ShouldBe(this.values.Count());
            },
            onFailure: async (errors, ct) =>
            {
                await Task.Delay(10, ct);
                failureExecuted = true;
            });

        // Assert
        result.ShouldBeSuccess();
        successExecuted.ShouldBeTrue();
        failureExecuted.ShouldBeFalse();
        result.CurrentPage.ShouldBe(this.page);
        result.PageSize.ShouldBe(this.pageSize);
        result.TotalCount.ShouldBe(this.count);
    }

    [Fact]
    public async Task HandleAsync_MixedHandlers_WithFailure_ExecutesFailureAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = ResultPaged<PersonStub>.Failure()
            .WithError<NotFoundError>();

        // Act
        var result = await sut.HandleAsync(
            onSuccess: async (value, ct) =>
            {
                await Task.Delay(10, ct);
                successExecuted = true;
            },
            onFailure: errors =>
            {
                failureExecuted = true;
                errors.Count.ShouldBe(1);
            });

        // Assert
        result.ShouldBeFailure();
        successExecuted.ShouldBeFalse();
        failureExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_CancelsOperation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var sut = ResultPaged<PersonStub>.Success(this.values, this.count, this.page, this.pageSize);
        cts.Cancel();

        // Act/Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await sut.HandleAsync(
                async (_, ct) =>
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
    public void Handle_PreservesPaginationMetadata()
    {
        // Arrange
        var sut = ResultPaged<PersonStub>.Success(this.values, this.count, this.page, this.pageSize);

        // Act
        var result = sut.Handle(
            onSuccess: _ => { },
            onFailure: _ => { });

        // Assert
        result.CurrentPage.ShouldBe(this.page);
        result.PageSize.ShouldBe(this.pageSize);
        result.TotalCount.ShouldBe(this.count);
        result.TotalPages.ShouldBe((int)Math.Ceiling(this.count / (double)this.pageSize));
        result.HasNextPage.ShouldBeTrue();
        result.HasPreviousPage.ShouldBeTrue();
    }
}