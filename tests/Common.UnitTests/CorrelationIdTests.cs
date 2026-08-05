// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System.Diagnostics;

public class CorrelationIdTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("order-123")]
    [InlineData("ORDER_123.trace:value")]
    public void IsValid_WithSupportedValue_ReturnsTrue(string value)
    {
        // Act
        var result = CorrelationId.IsValid(value);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contains whitespace")]
    [InlineData("slash/value")]
    [InlineData("ümlaut")]
    public void IsValid_WithUnsupportedValue_ReturnsFalse(string value)
    {
        // Act
        var result = CorrelationId.IsValid(value);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithMaximumAndOversizedValues_UsesFixedMaximumLength()
    {
        // Arrange
        var maximum = new string('a', CorrelationId.MaximumLength);
        var oversized = maximum + "a";

        // Act & Assert
        CorrelationId.IsValid(maximum).ShouldBeTrue();
        CorrelationId.IsValid(oversized).ShouldBeFalse();
    }

    [Fact]
    public void Current_WithActivityBaggage_ReturnsCorrelationIdInsteadOfTraceId()
    {
        // Arrange
        using var activity = new Activity("correlation-test").Start();
        activity.SetBaggage(CorrelationId.ActivityBaggageName, "correlation-123");

        // Act
        var result = CorrelationId.Current;

        // Assert
        result.ShouldBe("correlation-123");
        result.ShouldNotBe(activity.TraceId.ToString());
    }

    [Fact]
    public void Current_InChildActivity_InheritsParentCorrelationBaggage()
    {
        // Arrange
        using var parent = new Activity("correlation-parent").Start();
        parent.SetBaggage(CorrelationId.ActivityBaggageName, "correlation-parent-123");

        // Act
        using var child = new Activity("correlation-child").Start();
        var inheritedBaggage = child.GetBaggageItem(CorrelationId.ActivityBaggageName);
        var currentCorrelationId = CorrelationId.Current;

        // Assert
        child.ParentId.ShouldBe(parent.Id);
        inheritedBaggage.ShouldBe("correlation-parent-123");
        currentCorrelationId.ShouldBe("correlation-parent-123");
    }

    [Fact]
    public async Task Current_InAsyncChildAndGrandchildActivities_InheritsCorrelationBaggage()
    {
        // Arrange
        using var parent = new Activity("correlation-parent").Start();
        parent.SetBaggage(CorrelationId.ActivityBaggageName, "correlation-parent-123");

        // Act
        var captured = await Task.Run(() =>
        {
            using var child = new Activity("correlation-child").Start();
            var childCorrelationId = CorrelationId.Current;
            using var grandchild = new Activity("correlation-grandchild").Start();

            return (
                ChildId: child.Id,
                ChildParentId: child.ParentId,
                ChildCorrelationId: childCorrelationId,
                GrandchildParentId: grandchild.ParentId,
                GrandchildBaggage: grandchild.GetBaggageItem(
                    CorrelationId.ActivityBaggageName
                ),
                GrandchildCorrelationId: CorrelationId.Current
            );
        });

        // Assert
        captured.ChildParentId.ShouldBe(parent.Id);
        captured.ChildCorrelationId.ShouldBe("correlation-parent-123");
        captured.GrandchildParentId.ShouldBe(captured.ChildId);
        captured.GrandchildBaggage.ShouldBe("correlation-parent-123");
        captured.GrandchildCorrelationId.ShouldBe("correlation-parent-123");
    }

    [Fact]
    public async Task BeginScope_AcrossAwait_ProvidesValueAndRestoresPreviousValue()
    {
        // Arrange
        using var outerScope = CorrelationId.BeginScope("outer");
        string captured;

        // Act
        using (CorrelationId.BeginScope("inner"))
        {
            await Task.Yield();
            captured = CorrelationId.Current;
        }

        // Assert
        captured.ShouldBe("inner");
        CorrelationId.Current.ShouldBe("outer");
    }
}
