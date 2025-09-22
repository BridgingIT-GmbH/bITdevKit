// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

[UnitTest("Common")]
public class ResultValueTests
{
    private readonly Faker faker = new();

    [Fact]
    public void Success_WithVariousOverloads_CreatesSuccessResults()
    {
        // Arrange
        var value = this.faker.Random.Int(1, 100);
        var message = this.faker.Lorem.Sentence();
        var messages = new[] { this.faker.Lorem.Sentence(), this.faker.Lorem.Sentence() };

        // Act
        var result1 = Result<int>.Success();
        var result2 = Result<int>.Success(value);
        var result3 = Result<int>.Success(value, message);
        var result4 = Result<int>.Success(value, messages);

        // Assert
        result1.ShouldBeSuccess();
        result1.Value.ShouldBe(0); // default value

        result2.ShouldBeSuccess();
        result2.ShouldBeValue(value);
        result2.ShouldNotContainMessages();

        result3.ShouldBeSuccess();
        result3.ShouldBeValue(value);
        result3.ShouldContainMessage(message);

        result4.ShouldBeSuccess();
        result4.ShouldBeValue(value);
        result4.Messages.Count.ShouldBe(2);
    }

    [Fact]
    public void Failure_WithVariousOverloads_CreatesFailureResults()
    {
        // Arrange
        var value = this.faker.Random.Int(1, 100);
        var message = this.faker.Lorem.Sentence();
        var messages = new[] { this.faker.Lorem.Sentence(), this.faker.Lorem.Sentence() };
        var error = new Error("Test error");

        // Act
        var result1 = Result<int>.Failure();
        var result2 = Result<int>.Failure<NotFoundError>();
        var result3 = Result<int>.Failure(message);
        var result4 = Result<int>.Failure(value);
        var result5 = Result<int>.Failure(value, message, error);
        var result6 = Result<int>.Failure(value, messages);
        var result7 = Result<int>.Failure<NotFoundError>(message);

        // Assert
        result1.ShouldBeFailure();
        result1.ShouldNotContainMessages();

        result2.ShouldBeFailure();
        result2.ShouldContainError<NotFoundError>();

        result3.ShouldBeFailure();
        result3.ShouldContainMessage(message);

        result4.ShouldBeFailure();
        //Should.Throw<InvalidOperationException>(() => result4.Value);

        result5.ShouldBeFailure();
        //Should.Throw<InvalidOperationException>(() => result5.Value);
        result5.ShouldContainMessage(message);
        result5.ShouldContainError<Error>();

        result6.ShouldBeFailure();
        //Should.Throw<InvalidOperationException>(() => result6.Value);
        result6.Messages.Count.ShouldBe(2);

        result7.ShouldBeFailure();
        result7.ShouldContainMessage(message);
        result7.ShouldContainError<NotFoundError>();
        //Should.Throw<InvalidOperationException>(() => result7.Value);
    }

    public Result<string> To_ConversionBetweenTypes1()
    {
        // Arrange
        var value = this.faker.Random.Int(1, 100);
        var message = this.faker.Lorem.Sentence();
        var error = new Error("Test error");

        var successResult = Result<int>.Success(value).WithMessage(message);
        var failureResult = Result<int>.Failure().WithMessage(message).WithError(error);

        return failureResult.Wrap<string>(); //explicit  conversion
    }

    [Fact]
    public void To_ConversionBetweenTypes_MaintainsStateAndMessages()
    {
        // Arrange
        var value = this.faker.Random.Int(1, 100);
        var message = this.faker.Lorem.Sentence();
        var error = new Error("Test error");

        var successResult = Result<int>.Success(value).WithMessage(message);
        var failureResult = Result<int>.Failure().WithMessage(message).WithError(error);

        // Act
        var nonGenericSuccess = successResult.Unwrap();
        var nonGenericFailure = failureResult.Unwrap();

        var genericSuccess = successResult.Wrap<string>();
        var genericFailureWithValue = failureResult.Wrap("test");

        // Assert
        nonGenericSuccess.ShouldBeSuccess();
        nonGenericSuccess.ShouldContainMessage(message);

        nonGenericFailure.ShouldBeFailure();
        nonGenericFailure.ShouldContainMessage(message);
        nonGenericFailure.ShouldContainError<Error>();

        genericSuccess.ShouldBeSuccess();
        genericSuccess.ShouldContainMessage(message);
        genericSuccess.Value.ShouldBe(default);

        genericFailureWithValue.ShouldBeFailure();
        genericFailureWithValue.ShouldContainMessage(message);
        genericFailureWithValue.ShouldContainError<Error>();
        //Should.Throw<InvalidOperationException>(() => genericFailureWithValue.Value);
    }

    [Fact]
    public void HasError_WithVariousScenarios_DetectsErrorsCorrectly()
    {
        // Arrange
        var result = Result<int>.Failure()
            .WithError<NotFoundError>()
            .WithError(new Error("Test error"));

        // Act & Assert
        result.HasError().ShouldBeTrue();
        result.HasError<NotFoundError>().ShouldBeTrue();
        result.HasError<ValidationError>().ShouldBeFalse();

        result.TryGetErrors<NotFoundError>(out var errors).ShouldBeTrue();
        errors.Count().ShouldBe(1);
    }

    [Fact]
    public void WithMessage_AndWithMessages_AddsMessagesCorrectly()
    {
        // Arrange
        var message1 = this.faker.Lorem.Sentence();
        var message2 = this.faker.Lorem.Sentence();
        var messages = new[] { this.faker.Lorem.Sentence(), this.faker.Lorem.Sentence() };

        // Act
        var result = Result<int>.Success(42)
            .WithMessage(message1)
            .WithMessage(message2)
            .WithMessages(messages);

        // Assert
        result.Messages.Count.ShouldBe(4);
        result.ShouldContainMessage(message1);
        result.ShouldContainMessage(message2);
        result.Messages.ShouldContain(messages[0]);
        result.Messages.ShouldContain(messages[1]);
    }

    [Fact]
    public void WithError_AndWithErrors_AddsErrorsCorrectly()
    {
        // Arrange
        var error1 = new Error("Error 1");
        var error2 = new Error("Error 2");
        var errors = new[] { new Error("Error 3"), new Error("Error 4") };

        // Act
        var result = Result<int>.Failure()
            .WithError(error1)
            .WithError(error2)
            .WithErrors(errors);

        // Assert
        result.Errors.Count.ShouldBe(4);
        result.ShouldContainError<Error>();
        result.Errors.ShouldContain(error1);
        result.Errors.ShouldContain(error2);
        result.Errors.ShouldContain(errors[0]);
        result.Errors.ShouldContain(errors[1]);
    }

    [Fact]
    public async Task Collect_TransformsCollectionMaintainingErrors()
    {
        // Arrange
        var persons = new List<PersonStub>
        {
            new(this.faker.Name.FirstName(), this.faker.Name.LastName(), this.faker.Internet.Email(), 20),
            new(this.faker.Name.FirstName(), this.faker.Name.LastName(), this.faker.Internet.Email(), 17),
            new(this.faker.Name.FirstName(), this.faker.Name.LastName(), this.faker.Internet.Email(), 25)
        };

        var result = Result<IEnumerable<PersonStub>>.Success(persons);

        // Act
        var collected = await result.CollectAsync(async (p, ct) =>
        {
            await Task.Delay(10, ct);

            return p.Age >= 18
                ? Result<int>.Success(p.Age)
                : Result<int>.Failure()
                    .WithError(new ValidationError("Must be 18 or older", "age"));
        });

        // Assert
        collected.ShouldBeFailure();
        collected.Errors.Count.ShouldBe(1);
        collected.Errors.First().ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public void ErrorAndMessagePropagation_ThroughChainedOperations()
    {
        // Arrange
        var initialMessage = this.faker.Lorem.Sentence();
        var mapMessage = this.faker.Lorem.Sentence();
        var bindMessage = this.faker.Lorem.Sentence();

        var result = Result<PersonStub>.Success(new PersonStub(
                this.faker.Name.FirstName(),
                this.faker.Name.LastName(),
                this.faker.Internet.Email(),
                17))
            .WithMessage(initialMessage);

        // Act
        var finalResult = result
            .Map(p => p.Age)
            .WithMessage(mapMessage)
            .Bind(age => age >= 18
                ? Result<string>.Success($"Age {age} is valid")
                : Result<string>.Failure()
                    .WithError(new ValidationError("Must be 18 or older", "age")))
            .WithMessage(bindMessage);

        // Assert
        finalResult.ShouldBeFailure();
        finalResult.Messages.Count.ShouldBe(3);
        finalResult.Messages.ShouldContain(initialMessage);
        finalResult.Messages.ShouldContain(mapMessage);
        finalResult.Messages.ShouldContain(bindMessage);
        finalResult.ShouldContainError<ValidationError>();
    }

    [Fact]
    public async Task CollectAsync_WithParallelOperations_AccumulatesAllErrors()
    {
        // Arrange
        var persons = new List<PersonStub>
        {
            new(this.faker.Name.FirstName(), this.faker.Name.LastName(), this.faker.Internet.Email(), 15),
            new(this.faker.Name.FirstName(), this.faker.Name.LastName(), this.faker.Internet.Email(), 16),
            new(this.faker.Name.FirstName(), this.faker.Name.LastName(), this.faker.Internet.Email(), 17)
        };

        var result = Result<IEnumerable<PersonStub>>.Success(persons);

        // Act
        var collected = await result.CollectAsync(async (p, ct) =>
        {
            await Task.Delay(10, ct);

            return Result<int>.Failure()
                .WithError(new ValidationError($"Age {p.Age} is invalid", "age"))
                .WithMessage($"Validation failed for {p.FirstName}");
        });

        // Assert
        collected.ShouldBeFailure();
        collected.Errors.Count.ShouldBe(3);
        collected.Messages.Count.ShouldBe(3);
        collected.Errors.ShouldAllBe(e => e is ValidationError);
    }

    [Fact]
    public void ImplicitConversion_ToBoolAndResult_WorksCorrectly()
    {
        // Arrange
        var successResult = Result<int>.Success(42);
        var failureResult = Result<int>.Failure();

        // Act & Assert
        // Test implicit bool conversion
        bool successBool = successResult;
        bool failureBool = failureResult;
        successBool.ShouldBeTrue();
        failureBool.ShouldBeFalse();

        // Test implicit Result conversion
        Result nonGenericSuccess = successResult;
        Result nonGenericFailure = failureResult;
        nonGenericSuccess.ShouldBeSuccess();
        nonGenericFailure.ShouldBeFailure();
    }

    [Fact]
    public void Map_SuccessfulResult_TransformsValue()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var result = Result<PersonStub>.Success(person);

        // Act
        var mappedResult = result.Map(p => p.Age);

        // Assert
        mappedResult.ShouldBeSuccess();
        mappedResult.ShouldBeValue(person.Age);
    }

    [Fact]
    public async Task MapAsync_SuccessfulResult_TransformsValueAsynchronously()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var result = Result<PersonStub>.Success(person);

        // Act
        var mappedResult = await result.MapAsync(async (p, ct) =>
        {
            await Task.Delay(10, ct);

            return p.Age * 2;
        });

        // Assert
        mappedResult.ShouldBeSuccess();
        mappedResult.ShouldBeValue(person.Age * 2);
    }

    [Fact]
    public void Bind_ChainMultipleOperations_ExecutesSuccessfully()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var result = Result<PersonStub>.Success(person);

        // Act
        var boundResult = result
            .Bind(p => Result<string>.Success($"{p.FirstName} {p.LastName}", "created fullname"))
            .Bind(fullName => Result<int>.Success(fullName.Length, "checked fullname"));

        // Assert
        boundResult.ShouldBeSuccess();
        boundResult.Value.ShouldBe($"{person.FirstName} {person.LastName}".Length);
    }

    [Fact]
    public async Task TapAsync_PerformsSideEffect_MaintainsOriginalValue()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var result = Result<PersonStub>.Success(person);
        var sideEffectExecuted = false;

        // Act
        var tappedResult = await result.TapAsync(async (p, ct) =>
        {
            await Task.Delay(10, ct);
            sideEffectExecuted = true;
        });

        // Assert
        tappedResult.ShouldBeSuccess();
        tappedResult.Value.ShouldBe(person);
        sideEffectExecuted.ShouldBeTrue();
    }

    [Fact]
    public void Ensure_ConditionMet_ReturnsOriginalResult()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            25);
        var result = Result<PersonStub>.Success(person);

        // Act
        var ensuredResult = result.Ensure(
            p => p.Age >= 18,
            new Error("Must be an adult"));

        // Assert
        ensuredResult.ShouldBeSuccess();
        ensuredResult.Value.ShouldBe(person);
    }

    [Fact]
    public void Unless_ConditionMet_ReturnsFailure()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            15);
        var result = Result<PersonStub>.Success(person);

        // Act
        var unlessResult = result.Unless(
            p => p.Age < 18,
            new Error("Must be an adult"));

        // Assert
        unlessResult.ShouldBeFailure();
        unlessResult.ShouldContainError<Error>();
    }

    [Fact]
    public void TeeMap_TransformsAndExecutesSideEffect_ReturnsMappedResult()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var result = Result<PersonStub>.Success(person);
        var sideEffectExecuted = false;

        // Act
        var teeMappedResult = result.TeeMap(
            p => p.Age,
            age =>
            {
                sideEffectExecuted = true;
            });

        // Assert
        teeMappedResult.ShouldBeSuccess();
        teeMappedResult.ShouldBeValue(person.Age);
        sideEffectExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task TaskExtensions_ChainOperations_ExecutesSuccessfully()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var resultTask = Task.FromResult(Result<PersonStub>.Success(person));

        // Act
        var finalResult = await resultTask
            .Map(p => p.Age)
            .Bind(age => Result<string>.Success($"Age is {age}", "message bind"))
            .TapAsync(async (msg, ct) => await Task.Delay(10, ct))
            .Ensure(msg => msg.Length > 0, new Error("Message cannot be empty"));

        // Assert
        finalResult.ShouldBeSuccess();
        finalResult.Value.ShouldBe($"Age is {person.Age}");
    }

    [Fact]
    public void BiMap_SuccessAndFailurePaths_TransformsCorrectly()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var successResult = Result<PersonStub>.Success(person);
        var failureResult = Result<PersonStub>.Failure().WithError(new Error("Test error"));

        // Act
        var successMapped = successResult.BiMap(
            p => p.Age,
            errors => errors.Select(e => new Error(e.Message)));

        var failureMapped = failureResult.BiMap(
            p => p.Age,
            errors => errors.Select(e => new Error("Transformed: " + e.Message)));

        // Assert
        successMapped.ShouldBeSuccess();
        successMapped.ShouldBeValue(person.Age);

        failureMapped.ShouldBeFailure();
        failureMapped.Errors.First().Message.ShouldStartWith("Transformed:");
    }

    [Fact]
    public void Match_SuccessAndFailureCases_ExecutesCorrectPath()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var successResult = Result<PersonStub>.Success(person);
        var failureResult = Result<PersonStub>.Failure().WithError(new Error("Test error"));

        // Act
        var successMatched = successResult.Match(
            p => $"Success: {p.FirstName}",
            errors => $"Failure: {errors.Count} errors");

        var failureMatched = failureResult.Match(
            p => $"Success: {p.FirstName}",
            errors => $"Failure: {errors.Count} errors");

        // Assert
        successMatched.ShouldBe($"Success: {person.FirstName}");
        failureMatched.ShouldBe("Failure: 1 errors");
    }

    [Fact]
    public async Task AndThen_ComplexChaining_ExecutesAllOperations()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var result = Result<PersonStub>.Success(person);
        var operationsExecuted = 0;

        // Act
        var finalResult = await result
            .AndThen(p => operationsExecuted++)
            .AndThenAsync(async (p, ct) =>
            {
                await Task.Delay(10, ct);
                operationsExecuted++;
            })
            .AndThen(p => operationsExecuted++);

        // Assert
        finalResult.ShouldBeSuccess();
        finalResult.Value.ShouldBe(person);
        operationsExecuted.ShouldBe(3);
    }

    [Fact]
    public async Task DoAsync_ExecutesOperationsInOrder_MaintainsResult()
    {
        // Arrange
        var operationOrder = new List<int>();
        var result = Result<int>.Success(42);

        // Act
        var finalResult = await result
            .Do(() => operationOrder.Add(1))
            .DoAsync(async ct =>
            {
                await Task.Delay(10, ct);
                operationOrder.Add(2);
            })
            .Do(() => operationOrder.Add(3));

        // Assert
        finalResult.ShouldBeSuccess();
        finalResult.ShouldBeValue(42);
        operationOrder.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task OrElseAsync_WithFailureAndFallback_ExecutesFallback()
    {
        // Arrange
        var fallbackPerson = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));
        var result = Result<PersonStub>.Failure().WithError(new NotFoundError());

        // Act
        var finalResult = await result
            .OrElse(() => fallbackPerson)
            .OrElseAsync(async ct =>
            {
                await Task.Delay(10, ct);

                return new PersonStub(
                    this.faker.Name.FirstName(),
                    this.faker.Name.LastName(),
                    this.faker.Internet.Email(),
                    this.faker.Random.Int(20, 50));
            });

        // Assert
        finalResult.ShouldBeSuccess();
        finalResult.Value.ShouldBe(fallbackPerson); // First OrElse should be used
    }

    [Fact]
    public async Task SwitchAsync_ExecutesConditionallySideEffects_MaintainsResult()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            25);
        var result = Result<PersonStub>.Success(person);
        var youngPersonProcessed = false;
        var adultPersonProcessed = false;

        // Act
        var finalResult = await result
            .Switch(
                p => p.Age < 18,
                p => youngPersonProcessed = true)
            .SwitchAsync(
                p => p.Age >= 18,
                async (p, ct) =>
                {
                    await Task.Delay(10, ct);
                    adultPersonProcessed = true;
                });

        // Assert
        finalResult.ShouldBeSuccess();
        youngPersonProcessed.ShouldBeFalse();
        adultPersonProcessed.ShouldBeTrue();
    }

    [Fact]
    public async Task FilterAsync_WithMultipleConditions_AppliesAllFilters()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            17);
        var result = Result<PersonStub>.Success(person);

        // Act
        var finalResult = await result
            .Filter(
                p => p.Age >= 18,
                new ValidationError("Must be 18 or older", "age"))
            .FilterAsync(
                async (p, ct) =>
                {
                    await Task.Delay(10, ct);

                    return p.Email?.Value?.Contains("@") ?? false;
                },
                new ValidationError("Invalid email format", "email"));

        // Assert
        finalResult.ShouldBeFailure();
        finalResult.Errors.Count.ShouldBe(1);
        finalResult.Errors.First().ShouldBeOfType<ValidationError>();
        ((ValidationError)finalResult.Errors.First()).PropertyName.ShouldBe("age");
    }

    [Fact]
    public async Task TryAsync_HandlesExceptionsGracefully()
    {
        // Arrange & Act
        var result = await Result<string>.TryAsync(async ct =>
        {
            await Task.Delay(10, ct);

            throw new InvalidOperationException("Test exception");
        });

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ExceptionError>();
        result.Errors.First().Message.ShouldContain("Test exception");
    }

    [Fact]
    public void Try_CapturesExceptions_AndConvertsToFailure()
    {
        // Arrange & Act
        var result = Result<int>.Try(() => throw new DivideByZeroException());

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ExceptionError>();
        result.Errors.First().Message.ShouldContain("zero");
    }

    [Fact]
    public async Task ValidateAsync_WithFluentValidation_HandlesValidationErrors()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            EmailAddressStub.Create("invalid-email"),
            17);
        var result = Result<PersonStub>.Success(person);
        var validator = new TestValidator();

        // Act
        var validatedResult = await result.ValidateAsync(
            validator,
            strategy => strategy.IncludeAllRuleSets());

        // Assert
        validatedResult.ShouldBeFailure();
        validatedResult.Errors.Count.ShouldBe(1);
        validatedResult.Errors.ShouldAllBe(e => e is FluentValidationError);
        validatedResult.Errors.Select(e => e.Message)
            .ShouldContain(e => e.Contains("Must be 18 or older"));
        validatedResult.Errors.Select(e => e.Message)
            .ShouldContain(e => e.Contains("Invalid email"));
    }

    [Fact]
    public void Validate_WithCustomValidationStrategy_AppliesStrategy()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            EmailAddressStub.Create(this.faker.Internet.Email()),
            17);
        var result = Result<PersonStub>.Success(person);
        var validator = new TestValidator();

        // Act
        var validatedResult = result.Validate(validator, strategy => strategy.IncludeProperties(x => x.Age));

        // Assert
        validatedResult.ShouldBeFailure();
        validatedResult.Errors.Count.ShouldBe(1);
        validatedResult.Errors.First().Message.ShouldContain("Must be 18 or older");
    }

    [Fact]
    public async Task ProcessPeople_WithMultipleOperations_ShouldSucceed()
    {
        // Arrange
        var validator = new TestValidator();
        var metrics = Substitute.For<IMetrics>();
        var logger = Substitute.For<ILogger>();
        var cache = Substitute.For<ICache>();
        var emailService = Substitute.For<IEmailService>();
        var mapper = new StubMapper();
        var database = Substitute.For<IDatabase>();
        database.PersonExistsAsync(Arg.Any<EmailAddressStub>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var people = new List<PersonStub>
        {
            new(firstName: "John",
                lastName: "Doe",
                email: "john@example.com",
                age: 30,
                locations: [LocationStub.Create("Home", "123 Main St", "Apt 4B", "10001", "New York", "USA")]
            )
            {
                Nationality = "USA"
            },
            new(
                firstName: "Jane",
                lastName: "Smith",
                email: "jane@example.com",
                age: 25,
                locations: [LocationStub.Create("Office", "456 Business Ave", "Floor 3", "EC1A 1BB", "London", "UK")]
            )
            {
                Nationality = "UK"
            }
        };

        // Act
        var result = await Result<List<PersonStub>>.Success(people)
            .Do(() => logger.LogInformation("Starting person processing")) // ---------Log the start of processing
            .Validate(validator) // ---------------------------------------------------Validate all persons
            .Ensure(persons => persons.All(p => p.Age >= 18), // ----------------------Ensure proper age
                new Error("All persons must be adults")
            )
            .Tap(list => logger.LogInformation($"Processing {list.Count} persons")) // Tap to log the count
            .Filter( // ---------------------------------------------------------------Ensure all have valid locations
                persons => persons.All(p => p.Locations.Any()),
                new Error("All persons must have at least one location")
            )
            .Map(persons => persons.Select(p => // ------------------------------------Transform emails to lowercase
            {
                var email = EmailAddressStub.Create(p.Email.Value.ToLowerInvariant());
                return new PersonStub(p.FirstName, p.LastName, email, p.Age, p.Locations)
                {
                    Nationality = p.Nationality
                };
            }).ToList())
            .TapAsync(async (persons, ct) => // -------------------------------Perform async cache warmup
                {
                    foreach (var person in persons)
                    {
                        await cache.WarmupPersonDataAsync(person.Email, ct);
                    }
                },
                CancellationToken.None)
            .AndThenAsync(async (persons, ct) => // ----------------------------Send welcome emails
                {
                    foreach (var person in persons)
                    {
                        await emailService.SendWelcomeEmailAsync(person.Email, ct);
                    }
                },
                CancellationToken.None)
            .TeeMap(persons => persons, persons => // ---------------------------------Map to DTOs and save
                {
                    foreach (var person in persons)
                    {
                        var dto = new PersonDtoStub();
                        mapper.Map(person, dto);
                        metrics.RecordProcessedPerson(dto.FullName, person.Nationality);
                    }
                }
            )
            .AndThenAsync(async (persons, ct) => // ----------------------------Save to database
                {
                    foreach (var person in persons)
                    {
                        await database.SavePersonAsync(person, ct);
                    }
                },
                CancellationToken.None)
            .EnsureAsync(async (persons, ct) => // -----------------------------Final validation with locations check
                {
                    foreach (var person in persons)
                    {
                        var exists = await database.PersonExistsAsync(person.Email, ct);
                        if (!exists || !person.Locations.Any())
                        {
                            return false;
                        }
                    }

                    return true;
                },
                new Error("Not all persons were saved correctly or missing locations"))
            .Match( // ------------------------------------------------------------------Handle the final result (map to strings)
                list => $"Successfully processed {list.Count} persons from {list.Select(p => p.Nationality).Distinct().Count()} countries",
                errors => $"Processing failed: {string.Join(", ", errors.Select(e => e.Message))}"
            );

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Successfully processed", result);
        Assert.Contains("2 persons", result);
        Assert.Contains("2 countries", result);

        // Verify all substitute calls
        // logger.Received(1).LogInformation("Starting person processing");
        // logger.Received(1).LogInformation("Processing 2 persons");
        //
        // foreach (var person in people)
        // {
        //     await cache.Received(1).WarmupPersonDataAsync(person.Email, Arg.Any<CancellationToken>());
        //     await emailService.Received(1).SendWelcomeEmailAsync(person.Email, Arg.Any<CancellationToken>());
        //     await database.Received(1).SavePersonAsync(
        //         Arg.Is<PersonStub>(p => p.Email.Value == person.Email.Value),
        //         Arg.Any<CancellationToken>()
        //     );
        //     await database.Received(1).PersonExistsAsync(person.Email, Arg.Any<CancellationToken>());
        //
        //     metrics.Received(1).RecordProcessedPerson(
        //         $"{person.FirstName} {person.LastName}",
        //         person.Nationality
        //     );
        // }
    }

    public interface IMetrics
    {
        void RecordProcessedPerson(string fullName, string nationality);
    }

    public interface ILogger
    {
        void LogInformation(string message);
    }

    public interface ICache
    {
        Task WarmupPersonDataAsync(EmailAddressStub email, CancellationToken ct);
    }

    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(EmailAddressStub email, CancellationToken ct);
    }

    public interface IDatabase
    {
        Task SavePersonAsync(PersonStub person, CancellationToken ct);

        Task<bool> PersonExistsAsync(EmailAddressStub email, CancellationToken ct);
    }

    [Fact]
    public void ImplicitConversion_ValueToResult_WorksCorrectly()
    {
        // Arrange
        var value = this.faker.Random.Int(1, 100);

        // Act
        Result<int> result = value;

        // Assert
        result.ShouldBeSuccess();
        result.ShouldBeValue(value);
    }

    [Fact]
    public void ImplicitConversion_TaskToResult_WorksCorrectly()
    {
        // Arrange
        var value = this.faker.Random.Int(1, 100);
        var task = Task.FromResult(value);

        // Act
        Result<int> result = task;

        // Assert
        result.ShouldBeSuccess();
        result.ShouldBeValue(value);
    }

    [Fact]
    public void ImplicitConversion_ResultToBool_WorksCorrectly()
    {
        // Arrange
        var successResult = Result<int>.Success(42);
        var failureResult = Result<int>.Failure();

        // Act
        bool successBool = successResult;
        bool failureBool = failureResult;

        // Assert
        successBool.ShouldBeTrue();
        failureBool.ShouldBeFalse();
    }

    [Fact]
    public void ImplicitConversion_ResultToNonGenericResult_WorksCorrectly()
    {
        // Arrange
        var successResult = Result<int>.Success(42);
        var failureResult = Result<int>.Failure();

        // Act
        Result nonGenericSuccess = successResult;
        Result nonGenericFailure = failureResult;

        // Assert
        nonGenericSuccess.ShouldBeSuccess();
        nonGenericFailure.ShouldBeFailure();
    }

    [Fact]
    public void ImplicitConversion_NonGenericResultToResult_WorksCorrectly()
    {
        // Arrange
        var nonGenericSuccess = Result.Success().WithMessage("Success");
        var nonGenericFailure = Result.Failure().WithMessage("Failure");

        // Act
        Result<int> successResult = nonGenericSuccess;
        Result<int> failureResult = nonGenericFailure;

        // Assert
        successResult.ShouldBeSuccess();
        successResult.ShouldContainMessage("Success");

        failureResult.ShouldBeFailure();
        failureResult.ShouldContainMessage("Failure");
    }

    [Fact]
    public void ForConversion_ResultToResultOfDifferentType_WorksCorrectly()
    {
        // Arrange
        var successResult = Result<int>.Success(42).WithMessage("Success");
        var failureResult = Result<int>.Failure().WithMessage("Failure").WithError(new Error("Test error"));

        // Act
        // ReSharper disable once SuggestVarOrType_Elsewhere
#pragma warning disable IDE0007 // Use implicit type
        Result<string> convertedSuccessResult = successResult.Wrap<string>();
        // ReSharper disable once SuggestVarOrType_Elsewhere
        Result<string> convertedFailureResult = failureResult.Wrap<string>();
#pragma warning restore IDE0007 // Use implicit type

        // Assert
        convertedSuccessResult.ShouldBeSuccess();
        convertedSuccessResult.ShouldContainMessage("Success");

        convertedFailureResult.ShouldBeFailure();
        convertedFailureResult.ShouldContainMessage("Failure");
        convertedFailureResult.ShouldContainError<Error>();
    }

    [Fact]
    public void Handle_WithSuccess_ExecutesSuccessAction()
    {
        // Arrange
        var value = this.faker.Random.Int(1, 100);
        var successExecuted = false;
        var failureExecuted = false;
        var sut = Result<int>.Success(value);

        // Act
        var result = sut.Handle(
            onSuccess: v =>
            {
                successExecuted = true;
                v.ShouldBe(value);
            },
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
        var sut = Result<int>.Failure()
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
        var value = this.faker.Random.Int(1, 100);
        var successExecuted = false;
        var failureExecuted = false;
        var sut = Result<int>.Success(value);

        // Act
        var result = await sut.HandleAsync(
            onSuccess: async (v, ct) =>
            {
                await Task.Delay(10, ct);
                successExecuted = true;
                v.ShouldBe(value);
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
    }

    [Fact]
    public async Task HandleAsync_MixedHandlers_WithFailure_ExecutesFailureAction()
    {
        // Arrange
        var successExecuted = false;
        var failureExecuted = false;
        var sut = Result<int>.Failure()
            .WithError<NotFoundError>();

        // Act
        var result = await sut.HandleAsync(
            onSuccess: (v, ct) => Task.FromResult(successExecuted = true),
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
        var sut = Result<int>.Success(42);
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
    public void Handle_MaintainsMessagesAndErrors()
    {
        // Arrange
        var message = this.faker.Random.Words();
        var sut = Result<int>.Success(42)
            .WithMessage(message);

        // Act
        var result = sut.Handle(
            onSuccess: _ => { },
            onFailure: _ => { });

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage(message);
        result.Value.ShouldBe(42);
    }
}