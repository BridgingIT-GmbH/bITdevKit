// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

public class EitherTests
{
    public class ConstructorTests
    {
        private readonly Faker faker = new();

        [Fact]
        public void Constructor_WithFirstValue_SetsIsFirstToTrue()
        {
            // Arrange
            var firstValue = this.faker.Random.Int();

            // Act
            var sut = new Either<int, string>(firstValue);

            // Assert
            sut.IsFirst.ShouldBeTrue();
            sut.IsSecond.ShouldBeFalse();
            sut.FirstValue.ShouldBe(firstValue);
        }

        [Fact]
        public void Constructor_WithSecondValue_SetsIsSecondToTrue()
        {
            // Arrange
            var secondValue = this.faker.Lorem.Word();

            // Act
            var sut = new Either<int, string>(secondValue);

            // Assert
            sut.IsFirst.ShouldBeFalse();
            sut.IsSecond.ShouldBeTrue();
            sut.SecondValue.ShouldBe(secondValue);
        }

        [Fact]
        public void Constructor_WithNullFirstValue_ThrowsArgumentNullException()
        {
            // Arrange & Act
            var action = () => new Either<string, int>(null);

            // Assert
            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_WithNullSecondValue_ThrowsArgumentNullException()
        {
            // Arrange & Act
            var action = () => new Either<int, string>(null);

            // Assert
            action.ShouldThrow<ArgumentNullException>();
        }
    }

    public class PropertyTests
    {
        private readonly Faker faker = new();

        [Fact]
        public void FirstValue_WhenContainsSecondValue_ThrowsInvalidOperationException()
        {
            // Arrange
            var sut = new Either<int, string>(this.faker.Lorem.Word());

            // Act
            // Assert
            Should.Throw<InvalidOperationException>(() => _ = sut.FirstValue)
                .Message.ShouldBe("Either contains second value");
        }

        [Fact]
        public void SecondValue_WhenContainsFirstValue_ThrowsInvalidOperationException()
        {
            // Arrange
            var sut = new Either<int, string>(this.faker.Random.Int());

            // Act
            var action = () => _ = sut.SecondValue;

            // Assert
            action.ShouldThrow<InvalidOperationException>()
                .Message.ShouldBe("Either contains first value");
        }
    }

    public class ImplicitOperatorTests
    {
        private readonly Faker faker = new();

        [Fact]
        public void ImplicitOperator_FromFirstType_CreatesEitherWithFirstValue()
        {
            // Arrange
            int value = this.faker.Random.Int();

            // Act
            Either<int, string> sut = value;

            // Assert
            sut.IsFirst.ShouldBeTrue();
            sut.FirstValue.ShouldBe(value);
        }

        [Fact]
        public void ImplicitOperator_FromSecondType_CreatesEitherWithSecondValue()
        {
            // Arrange
            string value = this.faker.Lorem.Word();

            // Act
            Either<int, string> sut = value;

            // Assert
            sut.IsSecond.ShouldBeTrue();
            sut.SecondValue.ShouldBe(value);
        }
    }

    public class FromFactoryMethodTests
    {
        private readonly Faker faker = new();

        [Fact]
        public void FromFirst_WithValue_CreatesEitherWithFirstValue()
        {
            // Arrange
            var value = this.faker.Random.Int();

            // Act
            var sut = Either<int, string>.FromFirst(value);

            // Assert
            sut.IsFirst.ShouldBeTrue();
            sut.FirstValue.ShouldBe(value);
        }

        [Fact]
        public void FromSecond_WithValue_CreatesEitherWithSecondValue()
        {
            // Arrange
            var value = this.faker.Lorem.Word();

            // Act
            var sut = Either<int, string>.FromSecond(value);

            // Assert
            sut.IsSecond.ShouldBeTrue();
            sut.SecondValue.ShouldBe(value);
        }

        [Fact]
        public void FromNullable_WithNonNullValue_CreatesEitherWithFirstValue()
        {
            // Arrange
            var value = this.faker.Random.Int();
            var defaultSecond = this.faker.Lorem.Word();

            // Act
            var sut = Either<int, string>.FromNullable(value, defaultSecond);

            // Assert
            sut.IsFirst.ShouldBeTrue();
            sut.FirstValue.ShouldBe(value);
        }

        [Fact]
        public void FromNullable_WithNullValue_CreatesEitherWithSecondValue()
        {
            // Arrange
            string value = null;
            var defaultSecond = this.faker.Lorem.Word();

            // Act
            var sut = Either<string, string>.FromNullable(value, defaultSecond);

            // Assert
            sut.IsSecond.ShouldBeTrue();
            sut.SecondValue.ShouldBe(defaultSecond);
        }
    }

    public class MatchAndSwitchTests
    {
        public class MatchTests
        {
            private readonly Faker faker = new();

            [Fact]
            public void Match_WithFirstValue_ExecutesFirstMatchFunction()
            {
                // Arrange
                var value = this.faker.Random.Int();
                var sut = new Either<int, string>(value);
                var expectedResult = this.faker.Lorem.Word();

                // Act
                var result = sut.Match(
                    firstMatch: _ => expectedResult,
                    secondMatch: _ => this.faker.Lorem.Word());

                // Assert
                result.ShouldBe(expectedResult);
            }

            [Fact]
            public void Match_WithSecondValue_ExecutesSecondMatchFunction()
            {
                // Arrange
                var value = this.faker.Lorem.Word();
                var sut = new Either<int, string>(value);
                var expectedResult = this.faker.Random.Int();

                // Act
                var result = sut.Match(
                    firstMatch: _ => this.faker.Random.Int(),
                    secondMatch: _ => expectedResult);

                // Assert
                result.ShouldBe(expectedResult);
            }

            [Fact]
            public void Match_WithNullFirstMatchFunction_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                var action = () => sut.Match(
                    firstMatch: null,
                    secondMatch: _ => this.faker.Lorem.Word());

                // Assert
                action.ShouldThrow<ArgumentNullException>();
            }

            [Fact]
            public void Match_WithNullSecondMatchFunction_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                var action = () => sut.Match(
                    firstMatch: _ => this.faker.Lorem.Word(),
                    secondMatch: null);

                // Assert
                action.ShouldThrow<ArgumentNullException>();
            }
        }

        public class SwitchTests
        {
            private readonly Faker faker = new();

            [Fact]
            public void Switch_WithFirstValue_ExecutesFirstAction()
            {
                // Arrange
                var value = this.faker.Random.Int();
                var sut = new Either<int, string>(value);
                var firstActionExecuted = false;
                var secondActionExecuted = false;

                // Act
                sut.Switch(
                    firstAction: _ => firstActionExecuted = true,
                    secondAction: _ => secondActionExecuted = true);

                // Assert
                firstActionExecuted.ShouldBeTrue();
                secondActionExecuted.ShouldBeFalse();
            }

            [Fact]
            public void Switch_WithSecondValue_ExecutesSecondAction()
            {
                // Arrange
                var value = this.faker.Lorem.Word();
                var sut = new Either<int, string>(value);
                var firstActionExecuted = false;
                var secondActionExecuted = false;

                // Act
                sut.Switch(
                    firstAction: _ => firstActionExecuted = true,
                    secondAction: _ => secondActionExecuted = true);

                // Assert
                firstActionExecuted.ShouldBeFalse();
                secondActionExecuted.ShouldBeTrue();
            }

            [Fact]
            public void Switch_WithNullFirstAction_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                var action = () => sut.Switch(
                    firstAction: null,
                    secondAction: _ => { });

                // Assert
                action.ShouldThrow<ArgumentNullException>();
            }

            [Fact]
            public void Switch_WithNullSecondAction_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                var action = () => sut.Switch(
                    firstAction: _ => { },
                    secondAction: null);

                // Assert
                action.ShouldThrow<ArgumentNullException>();
            }
        }

        public class MatchAsyncTests
        {
            private readonly Faker faker = new();

            [Fact]
            public async Task MatchAsync_WithFirstValue_ExecutesFirstMatchFunction()
            {
                // Arrange
                var value = this.faker.Random.Int();
                var sut = new Either<int, string>(value);
                var expectedResult = this.faker.Lorem.Word();

                // Act
                var result = await sut.MatchAsync(
                    firstMatch: _ => Task.FromResult(expectedResult),
                    secondMatch: _ => Task.FromResult(this.faker.Lorem.Word()));

                // Assert
                result.ShouldBe(expectedResult);
            }

            [Fact]
            public async Task MatchAsync_WithSecondValue_ExecutesSecondMatchFunction()
            {
                // Arrange
                var value = this.faker.Lorem.Word();
                var sut = new Either<int, string>(value);
                var expectedResult = this.faker.Random.Int();

                // Act
                var result = await sut.MatchAsync(
                    firstMatch: _ => Task.FromResult(this.faker.Random.Int()),
                    secondMatch: _ => Task.FromResult(expectedResult));

                // Assert
                result.ShouldBe(expectedResult);
            }

            [Fact]
            public async Task MatchAsync_WithNullFirstMatchFunction_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                var action = () => sut.MatchAsync(
                    firstMatch: null,
                    secondMatch: _ => Task.FromResult(this.faker.Lorem.Word()));

                // Assert
                await action.ShouldThrowAsync<ArgumentNullException>();
            }

            [Fact]
            public async Task MatchAsync_WithNullSecondMatchFunction_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                var action = () => sut.MatchAsync(
                    firstMatch: _ => Task.FromResult(this.faker.Lorem.Word()),
                    secondMatch: null);

                // Assert
                await action.ShouldThrowAsync<ArgumentNullException>();
            }
        }

        public class SwitchAsyncTests
        {
            private readonly Faker faker = new();

            [Fact]
            public async Task SwitchAsync_WithFirstValue_ExecutesFirstAction()
            {
                // Arrange
                var value = this.faker.Random.Int();
                var sut = new Either<int, string>(value);
                var firstActionExecuted = false;
                var secondActionExecuted = false;

                // Act
                await sut.SwitchAsync(
                    firstAction: _ =>
                    {
                        firstActionExecuted = true;

                        return Task.CompletedTask;
                    },
                    secondAction: _ =>
                    {
                        secondActionExecuted = true;

                        return Task.CompletedTask;
                    });

                // Assert
                firstActionExecuted.ShouldBeTrue();
                secondActionExecuted.ShouldBeFalse();
            }

            [Fact]
            public async Task SwitchAsync_WithSecondValue_ExecutesSecondAction()
            {
                // Arrange
                var value = this.faker.Lorem.Word();
                var sut = new Either<int, string>(value);
                var firstActionExecuted = false;
                var secondActionExecuted = false;

                // Act
                await sut.SwitchAsync(
                    firstAction: _ =>
                    {
                        firstActionExecuted = true;

                        return Task.CompletedTask;
                    },
                    secondAction: _ =>
                    {
                        secondActionExecuted = true;

                        return Task.CompletedTask;
                    });

                // Assert
                firstActionExecuted.ShouldBeFalse();
                secondActionExecuted.ShouldBeTrue();
            }

            [Fact]
            public async Task SwitchAsync_WithNullFirstAction_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                var action = () => sut.SwitchAsync(
                    firstAction: null,
                    secondAction: _ => Task.CompletedTask);

                // Assert
                await action.ShouldThrowAsync<ArgumentNullException>();
            }

            [Fact]
            public async Task SwitchAsync_WithNullSecondAction_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                var action = () => sut.SwitchAsync(
                    firstAction: _ => Task.CompletedTask,
                    secondAction: null);

                // Assert
                await action.ShouldThrowAsync<ArgumentNullException>();
            }

            [Fact]
            public async Task SwitchAsync_WithExceptionInFirstAction_PropagatesException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());
                var expectedException = new Exception("Test exception");

                // Act
                var action = () => sut.SwitchAsync(
                    firstAction: _ => Task.FromException(expectedException),
                    secondAction: _ => Task.CompletedTask);

                // Assert
                var exception = await action.ShouldThrowAsync<Exception>();
                exception.Message.ShouldBe(expectedException.Message);
            }

            [Fact]
            public async Task SwitchAsync_WithExceptionInSecondAction_PropagatesException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Lorem.Word());
                var expectedException = new Exception("Test exception");

                // Act
                var action = () => sut.SwitchAsync(
                    firstAction: _ => Task.CompletedTask,
                    secondAction: _ => Task.FromException(expectedException));

                // Assert
                var exception = await action.ShouldThrowAsync<Exception>();
                exception.Message.ShouldBe(expectedException.Message);
            }
        }
    }

    public class MiscTests
    {
        public class TryMethodTests
        {
            private readonly Faker faker = new();

            [Fact]
            public void Try_WithSuccessfulExecution_ReturnsFirstValue()
            {
                // Arrange
                var expectedValue = this.faker.Random.Int();

                // Act
                var result = Either<int, Exception>.Try(() => expectedValue);

                // Assert
                result.IsFirst.ShouldBeTrue();
                result.FirstValue.ShouldBe(expectedValue);
            }

            [Fact]
            public void Try_WithException_ReturnsSecondValue()
            {
                // Arrange
                var expectedException = new InvalidOperationException(this.faker.Lorem.Sentence());

                // Act
                var result = Either<int, Exception>.Try(() => throw expectedException);

                // Assert
                result.IsSecond.ShouldBeTrue();
                result.SecondValue.ShouldBe(expectedException);
            }

            [Fact]
            public void Try_WithNullFunction_ThrowsArgumentNullException()
            {
                // Arrange & Act
                var action = () => Either<int, Exception>.Try(null);

                // Assert
                action.ShouldThrow<ArgumentNullException>();
            }
        }

        public class TryAsyncMethodTests
        {
            private readonly Faker faker = new();

            [Fact]
            public async Task TryAsync_WithSuccessfulExecution_ReturnsFirstValue()
            {
                // Arrange
                var expectedValue = this.faker.Random.Int();

                // Act
                var result = await Either<int, Exception>.TryAsync(
                    () => Task.FromResult(expectedValue));

                // Assert
                result.IsFirst.ShouldBeTrue();
                result.FirstValue.ShouldBe(expectedValue);
            }

            [Fact]
            public async Task TryAsync_WithException_ReturnsSecondValue()
            {
                // Arrange
                var expectedException = new InvalidOperationException(this.faker.Lorem.Sentence());

                // Act
                var result = await Either<int, Exception>.TryAsync(
                    () => Task.FromException<int>(expectedException));

                // Assert
                result.IsSecond.ShouldBeTrue();
                result.SecondValue.ShouldBe(expectedException);
            }

            [Fact]
            public async Task TryAsync_WithNullFunction_ThrowsArgumentNullException()
            {
                // Arrange & Act
                var action = () => Either<int, Exception>.TryAsync(null);

                // Assert
                await action.ShouldThrowAsync<ArgumentNullException>();
            }
        }

        public class FilterMethodTests
        {
            private readonly Faker faker = new();

            [Fact]
            public void Filter_WhenPredicateTrue_ReturnsOriginalEither()
            {
                // Arrange
                var value = this.faker.Random.Int(1, 100);
                var sut = new Either<int, string>(value);
                var defaultSecond = this.faker.Lorem.Word();

                // Act
                var result = sut.Filter(x => x > 0, defaultSecond);

                // Assert
                result.IsFirst.ShouldBeTrue();
                result.FirstValue.ShouldBe(value);
            }

            [Fact]
            public void Filter_WhenPredicateFalse_ReturnsSecondValue()
            {
                // Arrange
                var value = this.faker.Random.Int(-100, -1);
                var sut = new Either<int, string>(value);
                var defaultSecond = this.faker.Lorem.Word();

                // Act
                var result = sut.Filter(x => x > 0, defaultSecond);

                // Assert
                result.IsSecond.ShouldBeTrue();
                result.SecondValue.ShouldBe(defaultSecond);
            }

            [Fact]
            public void Filter_WithSecondValue_ReturnsOriginalEither()
            {
                // Arrange
                var value = this.faker.Lorem.Word();
                var sut = new Either<int, string>(value);
                var defaultSecond = this.faker.Lorem.Word();

                // Act
                var result = sut.Filter(x => x > 0, defaultSecond);

                // Assert
                result.IsSecond.ShouldBeTrue();
                result.SecondValue.ShouldBe(value);
            }

            [Fact]
            public void Filter_WithNullPredicate_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());
                var defaultSecond = this.faker.Lorem.Word();

                // Act
                var action = () => sut.Filter(null, defaultSecond);

                // Assert
                action.ShouldThrow<ArgumentNullException>();
            }
        }

        public class ToResultMethodTests
        {
            private readonly Faker faker = new();

            [Fact]
            public void ToResult_WithFirstValue_ReturnsSuccessResult()
            {
                // Arrange
                var value = this.faker.Random.Int();
                var sut = new Either<int, string>(value);

                // Act
                var result = sut.ToResult(
                    firstMatch: x => x.ToString(),
                    secondMatch: x => x);

                // Assert
                result.ShouldBeSuccess();
                result.Value.ShouldBe(value.ToString());
            }

            [Fact]
            public void ToResult_WithSecondValue_ReturnsFailureResult()
            {
                // Arrange
                var errorMessage = this.faker.Lorem.Sentence();
                var sut = new Either<int, string>(errorMessage);

                // Act
                var result = sut.ToResult(
                    firstMatch: x => x.ToString(),
                    errorMessage: "Error occurred");

                // Assert
                result.ShouldBeFailure();
                result.ShouldContainMessage("Error occurred");
            }

            [Fact]
            public void ToResult_WithCustomError_ReturnsFailureResultWithCustomError()
            {
                // Arrange
                var value = this.faker.Lorem.Word();
                var sut = new Either<int, string>(value);

                // Act
                var result = sut.ToResult(
                    firstMatch: x => x.ToString(),
                    secondMatch: x => x,
                    errorFactory: error => new CustomResultError(error));

                // Assert
                result.ShouldBeFailure();
                result.ShouldContainError<CustomResultError>();
                //result.Error.Message.ShouldBe(value);
            }

            [Fact]
            public void ToResult_WithNullFirstMatch_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                // Assert
                Should.Throw<ArgumentNullException>(() => sut.ToResult(
                    firstMatch: null,
                    secondMatch: x => x));
            }

            [Fact]
            public void ToResult_WithNullSecondMatch_ThrowsArgumentNullException()
            {
                // Arrange
                var sut = new Either<int, string>(this.faker.Random.Int());

                // Act
                // Assert
                Should.Throw<ArgumentNullException>(() => sut.ToResult(
                    firstMatch: x => x.ToString(),
                    secondMatch: null));
            }
        }

        public class ToStringMethodTests
        {
            private readonly Faker faker = new();

            [Fact]
            public void ToString_WithFirstValue_ReturnsFormattedString()
            {
                // Arrange
                var value = this.faker.Random.Int();
                var sut = new Either<int, string>(value);

                // Act
                var result = sut.ToString();

                // Assert
                result.ShouldBe($"Int32: {value}");
            }

            [Fact]
            public void ToString_WithSecondValue_ReturnsFormattedString()
            {
                // Arrange
                var value = this.faker.Lorem.Word();
                var sut = new Either<int, string>(value);

                // Act
                var result = sut.ToString();

                // Assert
                result.ShouldBe($"String: {value}");
            }
        }

        private class CustomResultError(string message) : IResultError
        {
            public string Message { get; } = message;

            public void Throw()
            {
                throw new NotImplementedException();
            }
        }
    }
}