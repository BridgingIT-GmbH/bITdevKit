// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

using Shouldly;

[UnitTest("Common")]
public class LinqFluentExtensionsTests
{
    #region Test Models

    private class TestEntity(int id = 1, string name = "Test", int value = 100, bool isActive = true)
    {
        public int Id { get; set; } = id;

        public string Name { get; set; } = name;

        public int Value { get; set; } = value;

        public bool IsActive { get; set; } = isActive;
    }

    private struct TestValue(int id = 1, bool isActive = true)
    {
        public int Id { get; set; } = id;

        public bool IsActive { get; set; } = isActive;
    }

    #endregion

    #region Find Tests

    [Fact]
    public void Find_WithMatchingPredicate_ReturnsFirstMatch()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new(1, "Alice"),
            new(2, "Bob"),
            new(3, "Charlie")
        };

        // Act
        var result = entities.Find(e => e.Id == 2);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(2);
        result.Name.ShouldBe("Bob");
    }

    [Fact]
    public void Find_WithNoMatchingPredicate_ReturnsNull()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new(1, "Alice"),
            new(2, "Bob")
        };

        // Act
        var result = entities.Find(e => e.Id == 99);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Find_WithNullSource_ThrowsNullReferenceException()
    {
        // Arrange
        List<TestEntity> entities = null;

        // Act & Assert - Extension method on null will throw
        Should.Throw<NullReferenceException>(() => entities.Find(e => e.Id == 1));
    }

    [Fact]
    public void Find_WithNullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var entities = new List<TestEntity> { new(1, "Alice") };

        // Act & Assert - Calling List.Find with null predicate throws
        Should.Throw<ArgumentNullException>(() => entities.Find(null));
    }

    [Fact]
    public void FindValue_WithMatchingPredicate_ReturnsFirstMatch()
    {
        // Arrange
        var values = new List<TestValue>
        {
            new(1, true),
            new(2, false),
            new(3, true)
        };

        // Act
        var result = values.FindValue(v => v.Id == 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Id.ShouldBe(2);
    }

    [Fact]
    public void FindValue_WithNoMatchingPredicate_ReturnsNull()
    {
        // Arrange
        var values = new List<TestValue> { new(1, true) };

        // Act
        var result = values.FindValue(v => v.Id == 99);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindValue_WithNullSource_ReturnsNull()
    {
        // Arrange
        List<TestValue> values = null;

        // Act
        var result = values.FindValue(v => v.Id == 1);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindValue_WithNullPredicate_ReturnsNull()
    {
        // Arrange
        var values = new List<TestValue> { new(1, true) };

        // Act
        var result = values.FindValue(null);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region WhenNotNull / WhenNull Tests

    [Fact]
    public void WhenNotNull_WithNotNullReferenceType_ExecutesActionAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");
        var actionExecuted = false;

        // Act
        var result = entity.WhenNotNull(_ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(entity);
    }

    [Fact]
    public void WhenNotNull_WithNullReferenceType_DoesNotExecuteActionAndReturnsNull()
    {
        // Arrange
        TestEntity entity = null;
        var actionExecuted = false;

        // Act
        var result = entity.WhenNotNull(_ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void WhenNotNull_WithNullAction_DoesNotThrowAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity();

        // Act
        var result = entity.WhenNotNull(null);

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public void WhenNotNull_WithValueType_ExecutesActionWhenHasValue()
    {
        // Arrange
        int? value = 42;
        var capturedValue = 0;

        // Act
        var result = value.WhenNotNull(v => capturedValue = v);

        // Assert
        capturedValue.ShouldBe(42);
        result.ShouldBe(value);
    }

    [Fact]
    public void WhenNotNull_WithNullableValueType_DoesNotExecuteWhenNoValue()
    {
        // Arrange
        int? value = null;
        var actionExecuted = false;

        // Act
        var result = value.WhenNotNull(_ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void WhenNull_WithNullReferenceType_ExecutesActionAndReturnsNull()
    {
        // Arrange
        TestEntity entity = null;
        var actionExecuted = false;

        // Act
        var result = entity.WhenNull(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeNull();
    }

    [Fact]
    public void WhenNull_WithNotNullReferenceType_DoesNotExecuteActionAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity();
        var actionExecuted = false;

        // Act
        var result = entity.WhenNull(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(entity);
    }

    [Fact]
    public void WhenNull_WithNullableValueType_ExecutesWhenNoValue()
    {
        // Arrange
        int? value = null;
        var actionExecuted = false;

        // Act
        var result = value.WhenNull(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeNull();
    }

    [Fact]
    public void WhenNull_WithValueTypeHasValue_DoesNotExecute()
    {
        // Arrange
        int? value = 42;
        var actionExecuted = false;

        // Act
        var result = value.WhenNull(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(value);
    }

    #endregion

    #region String-specific Tests

    [Fact]
    public void WhenNotNullOrEmpty_WithValidString_ExecutesAction()
    {
        // Arrange
        var str = "test value";
        var capturedValue = "";

        // Act
        var result = str.WhenNotNullOrEmpty(s => capturedValue = s);

        // Assert
        capturedValue.ShouldBe("test value");
        result.ShouldBe(str);
    }

    [Fact]
    public void WhenNotNullOrEmpty_WithEmptyString_DoesNotExecuteAction()
    {
        // Arrange
        var str = "";
        var actionExecuted = false;

        // Act
        var result = str.WhenNotNullOrEmpty(_ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe("");
    }

    [Fact]
    public void WhenNotNullOrEmpty_WithNullString_DoesNotExecuteAction()
    {
        // Arrange
        string str = null;
        var actionExecuted = false;

        // Act
        var result = str.WhenNotNullOrEmpty(_ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void WhenNotNullOrWhiteSpace_WithValidString_ExecutesAction()
    {
        // Arrange
        var str = "meaningful text";
        var actionExecuted = false;

        // Act
        var result = str.WhenNotNullOrWhiteSpace(_ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(str);
    }

    [Fact]
    public void WhenNotNullOrWhiteSpace_WithWhitespaceString_DoesNotExecuteAction()
    {
        // Arrange
        var str = "   ";
        var actionExecuted = false;

        // Act
        var result = str.WhenNotNullOrWhiteSpace(_ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(str);
    }

    [Fact]
    public void WhenNullOrEmpty_WithNullString_ExecutesAction()
    {
        // Arrange
        string str = null;
        var actionExecuted = false;

        // Act
        var result = str.WhenNullOrEmpty(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeNull();
    }

    [Fact]
    public void WhenNullOrEmpty_WithEmptyString_ExecutesAction()
    {
        // Arrange
        var str = "";
        var actionExecuted = false;

        // Act
        var result = str.WhenNullOrEmpty(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe("");
    }

    [Fact]
    public void WhenNullOrEmpty_WithValidString_DoesNotExecuteAction()
    {
        // Arrange
        var str = "text";
        var actionExecuted = false;

        // Act
        var result = str.WhenNullOrEmpty(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(str);
    }

    [Fact]
    public void WhenNullOrWhiteSpace_WithWhitespaceString_ExecutesAction()
    {
        // Arrange
        var str = "  \t\n  ";
        var actionExecuted = false;

        // Act
        var result = str.WhenNullOrWhiteSpace(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(str);
    }

    [Fact]
    public void WhenNullOrWhiteSpace_WithValidString_DoesNotExecuteAction()
    {
        // Arrange
        var str = "content";
        var actionExecuted = false;

        // Act
        var result = str.WhenNullOrWhiteSpace(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(str);
    }

    #endregion

    #region When / Unless Side Effect Tests

    [Fact]
    public void When_WithTruePredicateSideEffect_ExecutesActionAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = true };
        var actionExecuted = false;

        // Act
        var result = entity.When(e => e.IsActive, _ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(entity);
    }

    [Fact]
    public void When_WithFalsePredicateSideEffect_DoesNotExecuteActionAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };
        var actionExecuted = false;

        // Act
        var result = entity.When(e => e.IsActive, _ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(entity);
    }

    [Fact]
    public void When_WithNullValueSideEffect_DoesNotExecuteAction()
    {
        // Arrange
        TestEntity entity = null;
        var actionExecuted = false;

        // Act
        var result = entity.When(e => e.IsActive, _ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void When_WithNullPredicateSideEffect_DoesNotExecuteAction()
    {
        // Arrange
        var entity = new TestEntity();

        // Act
        var result = entity.When(null, _ => { });

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public void When_WithValueTypeSideEffect_ExecutesActionWhenPredicateTrue()
    {
        // Arrange
        int? value = 42;
        var actionExecuted = false;

        // Act
        var result = value.When(v => v > 10, _ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(value);
    }

    [Fact]
    public void When_WithValueTypeNoValueSideEffect_DoesNotExecuteAction()
    {
        // Arrange
        int? value = null;
        var actionExecuted = false;

        // Act
        var result = value.When(v => v > 10, _ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void Unless_WithTruePredicateSideEffect_DoesNotExecuteActionAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = true };
        var actionExecuted = false;

        // Act
        var result = entity.Unless(e => e.IsActive, _ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(entity);
    }

    [Fact]
    public void Unless_WithFalsePredicateSideEffect_ExecutesActionAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };
        var actionExecuted = false;

        // Act
        var result = entity.Unless(e => e.IsActive, _ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(entity);
    }

    [Fact]
    public void Unless_WithValueTypeSideEffect_ExecutesActionWhenPredicateFalse()
    {
        // Arrange
        int? value = 5;
        var actionExecuted = false;

        // Act
        var result = value.Unless(v => v > 10, _ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(value);
    }

    #endregion

    #region When / Unless Transformation Tests

    [Fact]
    public void When_WithTruePredicateTransform_ReturnsTransformedValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { Value = 100, IsActive = true };

        // Act
        var result = entity.When(e => e.IsActive, e => new TestEntity(e.Id, e.Name.ToUpper()));

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("TEST");
        // New instance created with constructor will have Value = 100 from constructor default
        result.Id.ShouldBe(1);
    }

    [Fact]
    public void When_WithFalsePredicateTransform_ReturnsOriginalValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };

        // Act
        var result = entity.When(e => e.IsActive, e => new TestEntity(e.Id, "TRANSFORMED"));

        // Assert
        result.ShouldBe(entity);
        result.Name.ShouldBe("Test");
    }

    [Fact]
    public void When_WithValueTypeTransform_ReturnsTransformedValue()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = value.When(v => v > 10, v => v * 2);

        // Assert
        result.ShouldBe(84);
    }

    [Fact]
    public void When_WithValueTypeTransformFalsePredicate_ReturnsOriginalValue()
    {
        // Arrange
        int? value = 5;

        // Act
        var result = value.When(v => v > 10, v => v * 2);

        // Assert
        result.ShouldBe(5);
    }

    [Fact]
    public void Unless_WithFalsePredicateTransform_ReturnsTransformedValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };

        // Act
        var result = entity.Unless(e => e.IsActive, e => new TestEntity(e.Id, "UNLESS_TRANSFORM"));

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("UNLESS_TRANSFORM");
    }

    [Fact]
    public void Unless_WithTruePredicateTransform_ReturnsOriginalValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = true };

        // Act
        var result = entity.Unless(e => e.IsActive, e => new TestEntity(e.Id, "TRANSFORMED"));

        // Assert
        result.ShouldBe(entity);
        result.Name.ShouldBe("Test");
    }

    [Fact]
    public void Unless_WithValueTypeTransform_ReturnsTransformedValue()
    {
        // Arrange
        int? value = 5;

        // Act
        var result = value.Unless(v => v > 10, v => v + 10);

        // Assert
        result.ShouldBe(15);
    }

    #endregion

    #region When / Unless Then-Else Tests

    [Fact]
    public void When_WithThenElse_ThenPredicateTrue_ReturnsThenResult()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = true };

        // Act
        var result = entity.When(
            e => e.IsActive,
            e => "Active",
            e => "Inactive");

        // Assert
        result.ShouldBe("Active");
    }

    [Fact]
    public void When_WithThenElse_PredicateFalse_ReturnsElseResult()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };

        // Act
        var result = entity.When(
            e => e.IsActive,
            e => "Active",
            e => "Inactive");

        // Assert
        result.ShouldBe("Inactive");
    }

    [Fact]
    public void When_WithThenElse_NullValue_ReturnsDefault()
    {
        // Arrange
        TestEntity entity = null;

        // Act
        var result = entity.When(
            e => e.IsActive,
            e => "Active",
            e => "Inactive");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void When_WithThenElse_NullPredicate_ReturnsDefault()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = entity.When(
            null,
            e => "Active",
            e => "Inactive");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void When_WithThenElse_ValueType_ThenPredicateTrue_ReturnsThenResult()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = value.When(
            v => v > 10,
            v => "Large",
            v => "Small");

        // Assert
        result.ShouldBe("Large");
    }

    [Fact]
    public void When_WithThenElse_ValueType_PredicateFalse_ReturnsElseResult()
    {
        // Arrange
        int? value = 5;

        // Act
        var result = value.When(
            v => v > 10,
            v => "Large",
            v => "Small");

        // Assert
        result.ShouldBe("Small");
    }

    [Fact]
    public void When_WithThenElse_ValueType_NoValue_ReturnsDefault()
    {
        // Arrange
        int? value = null;

        // Act
        var result = value.When(
            v => v > 10,
            v => "Large",
            v => "Small");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Unless_WithThenElse_PredicateFalse_ReturnsThenResult()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };

        // Act
        var result = entity.Unless(
            e => e.IsActive,
            e => "Inactive",
            e => "Active");

        // Assert
        result.ShouldBe("Inactive");
    }

    [Fact]
    public void Unless_WithThenElse_PredicateTrue_ReturnsElseResult()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = true };

        // Act
        var result = entity.Unless(
            e => e.IsActive,
            e => "Inactive",
            e => "Active");

        // Assert
        result.ShouldBe("Active");
    }

    [Fact]
    public void Unless_WithThenElse_ValueType_PredicateFalse_ReturnsThenResult()
    {
        // Arrange
        int? value = 5;

        // Act
        var result = value.Unless(
            v => v > 10,
            v => "Small",
            v => "Large");

        // Assert
        result.ShouldBe("Small");
    }

    #endregion

    #region Bool Condition Tests

    [Fact]
    public void When_WithBoolTrue_ExecutesAction()
    {
        // Arrange
        var condition = true;
        var actionExecuted = false;

        // Act
        var result = condition.When(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeTrue();
    }

    [Fact]
    public void When_WithBoolFalse_DoesNotExecuteAction()
    {
        // Arrange
        var condition = false;
        var actionExecuted = false;

        // Act
        var result = condition.When(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeFalse();
    }

    [Fact]
    public void When_WithBoolAndNullAction_DoesNotThrow()
    {
        // Arrange
        var condition = true;

        // Act
        var result = condition.When(null);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Otherwise_WithBoolFalse_ExecutesAction()
    {
        // Arrange
        var condition = false;
        var actionExecuted = false;

        // Act
        var result = condition.Otherwise(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeFalse();
    }

    [Fact]
    public void Otherwise_WithBoolTrue_DoesNotExecuteAction()
    {
        // Arrange
        var condition = true;
        var actionExecuted = false;

        // Act
        var result = condition.Otherwise(() => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeTrue();
    }

    [Fact]
    public void BoolConditionChain_WhenAndOtherwise_ExecutesCorrectAction()
    {
        // Arrange
        var condition = true;
        var whenExecuted = false;
        var otherwiseExecuted = false;

        // Act
        condition
            .When(() => whenExecuted = true)
            .Otherwise(() => otherwiseExecuted = true);

        // Assert
        whenExecuted.ShouldBeTrue();
        otherwiseExecuted.ShouldBeFalse();
    }

    #endregion

    #region Do Side Effect Tests

    [Fact]
    public void Do_WithReferenceType_ExecutesActionAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");
        var capturedId = 0;

        // Act
        var result = entity.Do(e => capturedId = e.Id);

        // Assert
        capturedId.ShouldBe(1);
        result.ShouldBe(entity);
    }

    [Fact]
    public void Do_WithNullReferenceType_DoesNotExecuteAction()
    {
        // Arrange
        TestEntity entity = null;
        var actionExecuted = false;

        // Act
        var result = entity.Do(_ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void Do_WithNullAction_DoesNotThrowAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity();

        // Act
        var result = entity.Do(null);

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public void Do_WithValueType_ExecutesActionAndReturnsValue()
    {
        // Arrange
        int? value = 42;
        var capturedValue = 0;

        // Act
        var result = value.Do(v => capturedValue = v);

        // Assert
        capturedValue.ShouldBe(42);
        result.ShouldBe(value);
    }

    [Fact]
    public void Do_WithNullValueType_DoesNotExecuteAction()
    {
        // Arrange
        int? value = null;
        var actionExecuted = false;

        // Act
        var result = value.Do(_ => actionExecuted = true);

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void Do_MultipleChained_ExecutesAllActions()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");
        var firstExecuted = false;
        var secondExecuted = false;

        // Act
        var result = entity
            .Do(_ => firstExecuted = true)
            .Do(_ => secondExecuted = true);

        // Assert
        firstExecuted.ShouldBeTrue();
        secondExecuted.ShouldBeTrue();
        result.ShouldBe(entity);
    }

    #endregion

    #region Throw / ThrowWhen Tests

    [Fact]
    public void Throw_WithNullValue_ThrowsException()
    {
        // Arrange
        TestEntity entity = null;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            entity.Throw(() => new InvalidOperationException("Entity not found")));
    }

    [Fact]
    public void Throw_WithNotNullValue_DoesNotThrowAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = entity.Throw(() => new InvalidOperationException("Entity not found"));

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public void Throw_WithNullValueAndNullFactory_DoesNotThrow()
    {
        // Arrange
        TestEntity entity = null;

        // Act
        var result = entity.Throw(null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Throw_WithValueType_ThrowsWhenNoValue()
    {
        // Arrange
        int? value = null;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            value.Throw(() => new InvalidOperationException("Value required")));
    }

    [Fact]
    public void Throw_WithValueType_DoesNotThrowWhenHasValue()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = value.Throw(() => new InvalidOperationException("Value required"));

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void ThrowWhen_WithTrueCondition_ThrowsException()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            entity.ThrowWhen(e => !e.IsActive, e => new InvalidOperationException("Inactive")));
    }

    [Fact]
    public void ThrowWhen_WithFalseCondition_DoesNotThrowAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = true };

        // Act
        var result = entity.ThrowWhen(e => !e.IsActive, e => new InvalidOperationException("Inactive"));

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public void ThrowWhen_WithNullValue_DoesNotThrow()
    {
        // Arrange
        TestEntity entity = null;

        // Act
        var result = entity.ThrowWhen(e => e.IsActive, e => new InvalidOperationException("Active"));

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ThrowWhen_WithValueType_ThrowsWhenConditionTrue()
    {
        // Arrange
        int? value = 5;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            value.ThrowWhen(v => v < 10, v => new InvalidOperationException("Value too small")));
    }

    [Fact]
    public void ThrowWhen_WithValueType_DoesNotThrowWhenConditionFalse()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = value.ThrowWhen(v => v < 10, v => new InvalidOperationException("Value too small"));

        // Assert
        result.ShouldBe(42);
    }

    #endregion

    #region Match Pattern Matching Tests

    [Fact]
    public void Match_WithNotNullValue_ExecutesSomeFunction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = entity.Match(
            some: e => $"Found: {e.Name}",
            none: () => "Not found");

        // Assert
        result.ShouldBe("Found: Test");
    }

    [Fact]
    public void Match_WithNullValue_ExecutesNoneFunction()
    {
        // Arrange
        TestEntity entity = null;

        // Act
        var result = entity.Match(
            some: e => $"Found: {e.Name}",
            none: () => "Not found");

        // Assert
        result.ShouldBe("Not found");
    }

    [Fact]
    public void Match_WithNullSomeFunction_ReturnsDefaultWhenValueExists()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = entity.Match(
            some: null,
            none: () => "Not found");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Match_WithNullNoneFunction_ReturnsDefaultWhenValueNull()
    {
        // Arrange
        TestEntity entity = null;

        // Act
        var result = entity.Match(
            some: e => "Found",
            none: null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Match_WithValueType_ExecutesSomeWhenHasValue()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = value.Match(
            some: v => $"Value: {v}",
            none: () => "No value");

        // Assert
        result.ShouldBe("Value: 42");
    }

    [Fact]
    public void Match_WithValueType_ExecutesNoneWhenNoValue()
    {
        // Arrange
        int? value = null;

        // Act
        var result = value.Match(
            some: v => $"Value: {v}",
            none: () => "No value");

        // Assert
        result.ShouldBe("No value");
    }

    #endregion

    #region OrElse Fallback Tests

    [Fact]
    public void OrElse_WithNotNullValue_ReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = entity.OrElse(() => new TestEntity(999, "Fallback"));

        // Assert
        result.ShouldBe(entity);
        result.Id.ShouldBe(1);
    }

    [Fact]
    public void OrElse_WithNullValue_ReturnsFallback()
    {
        // Arrange
        TestEntity entity = null;
        var fallback = new TestEntity(999, "Fallback");

        // Act
        var result = entity.OrElse(() => fallback);

        // Assert
        result.ShouldBe(fallback);
        result.Id.ShouldBe(999);
    }

    [Fact]
    public void OrElse_WithNullValueAndNullFactory_ReturnsNull()
    {
        // Arrange
        TestEntity entity = null;

        // Act
        var result = entity.OrElse(null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void OrElse_WithValueType_ReturnsValueWhenHasValue()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = value.OrElse(() => 999);

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void OrElse_WithValueType_ReturnsFallbackWhenNoValue()
    {
        // Arrange
        int? value = null;

        // Act
        var result = value.OrElse(() => 999);

        // Assert
        result.ShouldBe(999);
    }

    [Fact]
    public void OrElse_Chained_UsesFirstNonNullValue()
    {
        // Arrange
        TestEntity first = null;
        TestEntity second = null;
        var third = new TestEntity(3, "Third");

        // Act
        var result = first
            .OrElse(() => second)
            .OrElse(() => third);

        // Assert
        result.ShouldBe(third);
    }

    [Fact]
    public void OrElse_WithValueType_Chained_UsesFirstNonNullValue()
    {
        // Arrange
        int? first = null;
        int? second = 42;

        // Act
        var result = first.OrElse(() => second.Value);

        // Assert
        result.ShouldBe(42);
    }

    #endregion

    #region Async Find Tests

    [Fact]
    public async Task FindAsync_WithMatchingAsyncPredicate_ReturnsFirstMatch()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new(1, "Alice"),
            new(2, "Bob"),
            new(3, "Charlie")
        };

        // Act
        var result = await entities.FindAsync(async (e, ct) =>
        {
            await Task.Delay(1, ct);
            return e.Id == 2;
        });

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(2);
    }

    [Fact]
    public async Task FindAsync_WithoutCancellationTokenParam_ReturnsFirstMatch()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new(1, "Alice"),
            new(2, "Bob")
        };

        // Act
        var result = await entities.FindAsync(async e =>
        {
            await Task.Delay(1);
            return e.Id == 2;
        });

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(2);
    }

    [Fact]
    public async Task FindAsync_WithNoMatch_ReturnsNull()
    {
        // Arrange
        var entities = new List<TestEntity> { new(1, "Alice") };

        // Act
        var result = await entities.FindAsync(async (e, ct) =>
        {
            await Task.Delay(1, ct);
            return e.Id == 99;
        });

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_WithNullSource_ReturnsNull()
    {
        // Arrange
        List<TestEntity> entities = null;

        // Act
        var result = await entities.FindAsync(async (e, ct) => await Task.FromResult(true), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_WithNullPredicate_ReturnsNull()
    {
        // Arrange
        var entities = new List<TestEntity> { new(1, "Alice") };

        // Act
        var result = await entities.FindAsync((Func<TestEntity, CancellationToken, Task<bool>>)null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindValueAsync_WithMatchingAsyncPredicate_ReturnsFirstMatch()
    {
        // Arrange
        var values = new List<TestValue>
        {
            new(1, true),
            new(2, false),
            new(3, true)
        };

        // Act
        var result = await values.FindValueAsync(async (v, ct) =>
        {
            await Task.Delay(1, ct);
            return v.Id == 2;
        });

        // Assert
        result.ShouldNotBeNull();
        result.Value.Id.ShouldBe(2);
    }

    [Fact]
    public async Task FindValueAsync_WithoutCancellationTokenParam_ReturnsFirstMatch()
    {
        // Arrange
        var values = new List<TestValue> { new(1, true), new(2, false) };

        // Act
        var result = await values.FindValueAsync(async v =>
        {
            await Task.Delay(1);
            return v.Id == 2;
        });

        // Assert
        result.ShouldNotBeNull();
        result.Value.Id.ShouldBe(2);
    }

    [Fact]
    public async Task FindValueAsync_WithNoMatch_ReturnsNull()
    {
        // Arrange
        var values = new List<TestValue> { new(1, true) };

        // Act
        var result = await values.FindValueAsync(async (v, ct) => await Task.FromResult(false), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region Async WhenNotNull / WhenNull Tests

    [Fact]
    public async Task WhenNotNullAsync_WithNotNullReferenceType_ExecutesAsyncAction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");
        var actionExecuted = false;

        // Act
        var result = await entity.WhenNotNullAsync(async (e, ct) =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task WhenNotNullAsync_WithoutCancellationTokenParam_ExecutesAsyncAction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");
        var actionExecuted = false;

        // Act
        var result = await entity.WhenNotNullAsync(async e =>
        {
            await Task.Delay(1);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task WhenNotNullAsync_WithNullReferenceType_DoesNotExecuteAction()
    {
        // Arrange
        TestEntity entity = null;
        var actionExecuted = false;

        // Act
        var result = await entity.WhenNotNullAsync(async (e, ct) =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeNull();
    }



    [Fact]
    public async Task WhenNullAsync_WithNullReferenceType_ExecutesAsyncAction()
    {
        // Arrange
        TestEntity entity = null;
        var actionExecuted = false;

        // Act
        var result = await entity.WhenNullAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeNull();
    }

    [Fact]
    public async Task WhenNullAsync_WithNotNull_DoesNotExecuteAction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");
        var actionExecuted = false;

        // Act
        var result = await entity.WhenNullAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task WhenNullAsync_OnTask_WithNull_ExecutesAction()
    {
        // Arrange
        var task = Task.FromResult((TestEntity)null);
        var actionExecuted = false;

        // Act
        var result = await task.WhenNullAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeNull();
    }

    #endregion

    #region Async String-Specific Tests

    [Fact]
    public async Task WhenNotNullOrEmptyAsync_WithValidString_ExecutesAction()
    {
        // Arrange
        var str = "test value";
        var capturedValue = "";

        // Act
        var result = await str.WhenNotNullOrEmptyAsync(async (s, ct) =>
        {
            await Task.Delay(1, ct);
            capturedValue = s;
        });

        // Assert
        capturedValue.ShouldBe("test value");
        result.ShouldBe(str);
    }

    [Fact]
    public async Task WhenNotNullOrEmptyAsync_WithEmptyString_DoesNotExecuteAction()
    {
        // Arrange
        var str = "";
        var actionExecuted = false;

        // Act
        var result = await str.WhenNotNullOrEmptyAsync(async (s, ct) =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe("");
    }

    [Fact]
    public async Task WhenNotNullOrEmptyAsync_OnTask_WithValidString_ExecutesAction()
    {
        // Arrange
        var task = Task.FromResult("test string");
        var actionExecuted = false;

        // Act
        var result = await task.WhenNotNullOrEmptyAsync(async (s, ct) =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe("test string");
    }

    [Fact]
    public async Task WhenNotNullOrWhiteSpaceAsync_WithValidString_ExecutesAction()
    {
        // Arrange
        var str = "content";
        var actionExecuted = false;

        // Act
        var result = await str.WhenNotNullOrWhiteSpaceAsync(async (s, ct) =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(str);
    }

    [Fact]
    public async Task WhenNotNullOrWhiteSpaceAsync_WithWhitespace_DoesNotExecuteAction()
    {
        // Arrange
        var str = "   ";
        var actionExecuted = false;

        // Act
        var result = await str.WhenNotNullOrWhiteSpaceAsync(async (s, ct) =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(str);
    }

    [Fact]
    public async Task WhenNotNullOrWhiteSpaceAsync_OnTask_WithWhitespace_DoesNotExecuteAction()
    {
        // Arrange
        var task = Task.FromResult("   ");
        var actionExecuted = false;

        // Act
        var result = await task.WhenNotNullOrWhiteSpaceAsync(async (s, ct) =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe("   ");
    }

    [Fact]
    public async Task WhenNullOrEmptyAsync_WithNullString_ExecutesAction()
    {
        // Arrange
        string str = null;
        var actionExecuted = false;

        // Act
        var result = await str.WhenNullOrEmptyAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeNull();
    }

    [Fact]
    public async Task WhenNullOrEmptyAsync_OnTask_WithEmptyString_ExecutesAction()
    {
        // Arrange
        var task = Task.FromResult("");
        var actionExecuted = false;

        // Act
        var result = await task.WhenNullOrEmptyAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe("");
    }

    [Fact]
    public async Task WhenNullOrWhiteSpaceAsync_WithWhitespace_ExecutesAction()
    {
        // Arrange
        var str = "   ";
        var actionExecuted = false;

        // Act
        var result = await str.WhenNullOrWhiteSpaceAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(str);
    }

    [Fact]
    public async Task WhenNullOrWhiteSpaceAsync_OnTask_WithValidString_DoesNotExecuteAction()
    {
        // Arrange
        var task = Task.FromResult("content");
        var actionExecuted = false;

        // Act
        var result = await task.WhenNullOrWhiteSpaceAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe("content");
    }

    #endregion

    #region Async When / Unless Tests

    [Fact]
    public async Task WhenAsync_WithTruePredicate_ExecutesAsyncAction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = true };
        var actionExecuted = false;

        // Act
        var result = await entity.WhenAsync(
            async (e, ct) => await Task.FromResult(e.IsActive),
            async (e, ct) =>
            {
                await Task.Delay(1, ct);
                actionExecuted = true;
            });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task WhenAsync_WithFalsePredicate_DoesNotExecuteAction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };
        var actionExecuted = false;

        // Act
        var result = await entity.WhenAsync(
            async (e, ct) => await Task.FromResult(e.IsActive),
            async (e, ct) =>
            {
                await Task.Delay(1, ct);
                actionExecuted = true;
            });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task WhenAsync_OnTask_WithTruePredicate_ExecutesAsyncAction()
    {
        // Arrange
        var task = Task.FromResult(new TestEntity(1, "Test") { IsActive = true });
        var actionExecuted = false;

        // Act
        var result = await task.WhenAsync(
            async (e, ct) => await Task.FromResult(e.IsActive),
            async (e, ct) =>
            {
                await Task.Delay(1, ct);
                actionExecuted = true;
            });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task UnlessAsync_WithFalsePredicate_ExecutesAsyncAction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };
        var actionExecuted = false;

        // Act
        var result = await entity.UnlessAsync(
            async (e, ct) => await Task.FromResult(e.IsActive),
            async (e, ct) =>
            {
                await Task.Delay(1, ct);
                actionExecuted = true;
            });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task UnlessAsync_WithTruePredicate_DoesNotExecuteAction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = true };
        var actionExecuted = false;

        // Act
        var result = await entity.UnlessAsync(
            async (e, ct) => await Task.FromResult(e.IsActive),
            async (e, ct) =>
            {
                await Task.Delay(1, ct);
                actionExecuted = true;
            });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task UnlessAsync_OnTask_WithFalsePredicate_ExecutesAsyncAction()
    {
        // Arrange
        var task = Task.FromResult(new TestEntity(1, "Test") { IsActive = false });
        var actionExecuted = false;

        // Act
        var result = await task.UnlessAsync(
            async (e, ct) => await Task.FromResult(e.IsActive),
            async (e, ct) =>
            {
                await Task.Delay(1, ct);
                actionExecuted = true;
            });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldNotBeNull();
    }

    #endregion

    #region Async Bool Condition Tests

    [Fact]
    public async Task WhenAsync_WithBoolTrue_ExecutesAsyncAction()
    {
        // Arrange
        var condition = true;
        var actionExecuted = false;

        // Act
        var result = await condition.WhenAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenAsync_WithBoolFalse_DoesNotExecuteAction()
    {
        // Arrange
        var condition = false;
        var actionExecuted = false;

        // Act
        var result = await condition.WhenAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenAsync_OnTask_WithBoolTrue_ExecutesAsyncAction()
    {
        // Arrange
        var task = Task.FromResult(true);
        var actionExecuted = false;

        // Act
        var result = await task.WhenAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task OtherwiseAsync_WithBoolFalse_ExecutesAsyncAction()
    {
        // Arrange
        var condition = false;
        var actionExecuted = false;

        // Act
        var result = await condition.OtherwiseAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task OtherwiseAsync_WithBoolTrue_DoesNotExecuteAction()
    {
        // Arrange
        var condition = true;
        var actionExecuted = false;

        // Act
        var result = await condition.OtherwiseAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeFalse();
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task OtherwiseAsync_OnTask_WithBoolFalse_ExecutesAsyncAction()
    {
        // Arrange
        var task = Task.FromResult(false);
        var actionExecuted = false;

        // Act
        var result = await task.OtherwiseAsync(async ct =>
        {
            await Task.Delay(1, ct);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBeFalse();
    }

    #endregion

    #region Async Do Tests

    [Fact]
    public async Task DoAsync_WithReferenceType_ExecutesAsyncActionAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");
        var capturedId = 0;

        // Act
        var result = await entity.DoAsync(async (e, ct) =>
        {
            await Task.Delay(1, ct);
            capturedId = e.Id;
        });

        // Assert
        capturedId.ShouldBe(1);
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task DoAsync_WithoutCancellationTokenParam_ExecutesAsyncAction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");
        var actionExecuted = false;

        // Act
        var result = await entity.DoAsync(async e =>
        {
            await Task.Delay(1);
            actionExecuted = true;
        });

        // Assert
        actionExecuted.ShouldBeTrue();
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task DoAsync_WithValueType_ExecutesAsyncActionAndReturnsValue()
    {
        // Arrange
        int? value = 42;
        var capturedValue = 0;

        // Act
        var result = await value.DoAsync(async (v, ct) =>
        {
            await Task.Delay(1, ct);
            capturedValue = v;
        });

        // Assert
        capturedValue.ShouldBe(42);
        result.ShouldBe(42);
    }

    [Fact]
    public async Task DoAsync_WithNullAction_DoesNotThrowAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = await entity.DoAsync((Func<TestEntity, CancellationToken, Task>)null);

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task DoAsync_WithValueType_WithNullAction_DoesNotThrow()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = await value.DoAsync((Func<int, CancellationToken, Task>)null);

        // Assert
        result.ShouldBe(42);
    }

    #endregion

    #region Async Throw / ThrowWhen Tests

    [Fact]
    public async Task ThrowAsync_WithNullValue_ThrowsException()
    {
        // Arrange
        TestEntity entity = null;

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await entity.ThrowAsync(async ct => new InvalidOperationException("Entity not found")));
    }

    [Fact]
    public async Task ThrowAsync_WithNotNullValue_DoesNotThrowAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = await entity.ThrowAsync(async ct => new InvalidOperationException("Entity not found"));

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task ThrowWhenAsync_WithTrueCondition_ThrowsException()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = false };

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await entity.ThrowWhenAsync(
                async (e, ct) => await Task.FromResult(!e.IsActive),
                async (e, ct) => new InvalidOperationException("Inactive")));
    }

    [Fact]
    public async Task ThrowWhenAsync_WithFalseCondition_DoesNotThrowAndReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test") { IsActive = true };

        // Act
        var result = await entity.ThrowWhenAsync(
            async (e, ct) => await Task.FromResult(!e.IsActive),
            async (e, ct) => new InvalidOperationException("Inactive"));

        // Assert
        result.ShouldBe(entity);
    }

    [Fact]
    public async Task ThrowWhenAsync_OnTask_WithTrueCondition_ThrowsException()
    {
        // Arrange
        var task = Task.FromResult(new TestEntity(1, "Test") { IsActive = false });

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await task.ThrowWhenAsync(
                async (e, ct) => await Task.FromResult(!e.IsActive),
                async (e, ct) => new InvalidOperationException("Inactive")));
    }

    #endregion

    #region Async Match Tests

    [Fact]
    public async Task MatchAsync_WithNotNullValue_ExecutesSomeFunction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = await entity.MatchAsync(
            some: async (e, ct) =>
            {
                await Task.Delay(1, ct);
                return $"Found: {e.Name}";
            },
            none: async ct =>
            {
                await Task.Delay(1, ct);
                return "Not found";
            });

        // Assert
        result.ShouldBe("Found: Test");
    }

    [Fact]
    public async Task MatchAsync_WithoutCancellationTokenParam_ExecutesSomeFunction()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = await entity.MatchAsync(
            some: async e =>
            {
                await Task.Delay(1);
                return $"Found: {e.Name}";
            },
            none: async () =>
            {
                await Task.Delay(1);
                return "Not found";
            });

        // Assert
        result.ShouldBe("Found: Test");
    }

    [Fact]
    public async Task MatchAsync_WithNullValue_ExecutesNoneFunction()
    {
        // Arrange
        TestEntity entity = null;

        // Act
        var result = await entity.MatchAsync(
            some: async (e, ct) => await Task.FromResult($"Found: {e.Name}"),
            none: async ct => await Task.FromResult("Not found"));

        // Assert
        result.ShouldBe("Not found");
    }

    [Fact]
    public async Task MatchAsync_OnTask_WithNotNull_ExecutesSomeFunction()
    {
        // Arrange
        var task = Task.FromResult(new TestEntity(1, "Test"));

        // Act
        var result = await task.MatchAsync(
            some: async (e, ct) => await Task.FromResult($"Found: {e.Name}"),
            none: async ct => await Task.FromResult("Not found"));

        // Assert
        result.ShouldBe("Found: Test");
    }

    [Fact]
    public async Task MatchAsync_OnTask_WithNull_ExecutesNoneFunction()
    {
        // Arrange
        var task = Task.FromResult((TestEntity)null);

        // Act
        var result = await task.MatchAsync(
            some: async (e, ct) => await Task.FromResult($"Found: {e.Name}"),
            none: async ct => await Task.FromResult("Not found"));

        // Assert
        result.ShouldBe("Not found");
    }

    [Fact]
    public async Task MatchAsync_WithValueType_ExecutesSomeWhenHasValue()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = await value.MatchAsync(
            some: async (v, ct) => await Task.FromResult($"Value: {v}"),
            none: async ct => await Task.FromResult("No value"));

        // Assert
        result.ShouldBe("Value: 42");
    }

    [Fact]
    public async Task MatchAsync_WithValueType_ExecutesNoneWhenNoValue()
    {
        // Arrange
        int? value = null;

        // Act
        var result = await value.MatchAsync(
            some: async (v, ct) => await Task.FromResult($"Value: {v}"),
            none: async ct => await Task.FromResult("No value"));

        // Assert
        result.ShouldBe("No value");
    }

    #endregion

    #region Async OrElse Tests

    [Fact]
    public async Task OrElseAsync_WithNotNullValue_ReturnsValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = await entity.OrElseAsync(async ct =>
        {
            await Task.Delay(1, ct);
            return new TestEntity(999, "Fallback");
        });

        // Assert
        result.ShouldBe(entity);
        result.Id.ShouldBe(1);
    }

    [Fact]
    public async Task OrElseAsync_WithNullValue_ReturnsFallback()
    {
        // Arrange
        TestEntity entity = null;
        var fallback = new TestEntity(999, "Fallback");

        // Act
        var result = await entity.OrElseAsync(async ct =>
        {
            await Task.Delay(1, ct);
            return fallback;
        });

        // Assert
        result.ShouldBe(fallback);
        result.Id.ShouldBe(999);
    }

    [Fact]
    public async Task OrElseAsync_WithoutCancellationTokenParam_ReturnsFallback()
    {
        // Arrange
        TestEntity entity = null;
        var fallback = new TestEntity(999, "Fallback");

        // Act
        var result = await entity.OrElseAsync(async () =>
        {
            await Task.Delay(1);
            return fallback;
        });

        // Assert
        result.ShouldBe(fallback);
    }

    [Fact]
    public async Task OrElseAsync_WithValueType_ReturnsValueWhenHasValue()
    {
        // Arrange
        int? value = 42;

        // Act
        var result = await value.OrElseAsync(async ct =>
        {
            await Task.Delay(1, ct);
            return 999;
        });

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public async Task OrElseAsync_WithValueType_ReturnsFallbackWhenNoValue()
    {
        // Arrange
        int? value = null;

        // Act
        var result = await value.OrElseAsync(async ct =>
        {
            await Task.Delay(1, ct);
            return 999;
        });

        // Assert
        result.ShouldBe(999);
    }

    [Fact]
    public async Task OrElseAsync_OnTask_WithNotNull_ReturnsValue()
    {
        // Arrange
        var task = Task.FromResult(new TestEntity(1, "Test"));

        // Act
        var result = await task.OrElseAsync(async ct =>
        {
            await Task.Delay(1, ct);
            return new TestEntity(999, "Fallback");
        });

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(1);
    }

    [Fact]
    public async Task OrElseAsync_OnTask_WithNull_ReturnsFallback()
    {
        // Arrange
        var task = Task.FromResult((TestEntity)null);

        // Act
        var result = await task.OrElseAsync(async ct =>
        {
            await Task.Delay(1, ct);
            return new TestEntity(999, "Fallback");
        });

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(999);
    }

    [Fact]
    public async Task OrElseAsync_OnTask_WithoutCancellationTokenParam_ReturnsFallback()
    {
        // Arrange
        var task = Task.FromResult((TestEntity)null);

        // Act
        var result = await task.OrElseAsync(async () =>
        {
            await Task.Delay(1);
            return new TestEntity(999, "Fallback");
        });

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(999);
    }

    #endregion

    #region Async Select Tests

    [Fact]
    public async Task SelectAsync_OnTask_WithSyncSelector_ReturnsTransformedValue()
    {
        // Arrange
        var task = Task.FromResult(new TestEntity(1, "Test"));

        // Act
        var result = await task.Select(e => e.Name);

        // Assert
        result.ShouldBe("Test");
    }

    [Fact]
    public async Task SelectAsync_WithAsyncSelector_ReturnsTransformedValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = await entity.SelectAsync(async (e, ct) =>
        {
            await Task.Delay(1, ct);
            return new { e.Id, e.Name };
        });

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(1);
        result.Name.ShouldBe("Test");
    }

    [Fact]
    public async Task SelectAsync_WithoutCancellationTokenParam_ReturnsTransformedValue()
    {
        // Arrange
        var entity = new TestEntity(1, "Test");

        // Act
        var result = await entity.SelectAsync(async e =>
        {
            await Task.Delay(1);
            return e.Name.ToUpper();
        });

        // Assert
        result.ShouldBe("TEST");
    }

    [Fact]
    public async Task SelectAsync_OnTask_WithAsyncSelector_ReturnsTransformedValue()
    {
        // Arrange
        var task = Task.FromResult(new TestEntity(1, "Test"));

        // Act
        var result = await task.SelectAsync(async (e, ct) =>
        {
            await Task.Delay(1, ct);
            return e.Name.ToUpper();
        });

        // Assert
        result.ShouldBe("TEST");
    }

    [Fact]
    public async Task SelectAsync_OnTask_WithoutCancellationTokenParam_ReturnsTransformedValue()
    {
        // Arrange
        var task = Task.FromResult(new TestEntity(1, "Test"));

        // Act
        var result = await task.SelectAsync(async e =>
        {
            await Task.Delay(1);
            return e.Name;
        });

        // Assert
        result.ShouldBe("Test");
    }

    #endregion
}
