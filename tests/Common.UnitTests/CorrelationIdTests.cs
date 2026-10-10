// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System.Diagnostics;
using System.Text.Json;

public class CorrelationIdTests
{
    /// <summary>Checks CLR and serialized strings retain the publisher identifier.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadFrom_StringMetadata_RetainsValidCorrelation(bool serialized)
    {
        var properties = new Dictionary<string, object>
        {
            [CorrelationId.HeaderName] = "publisher-123",
        };
        if (serialized)
        {
            properties = JsonSerializer.Deserialize<Dictionary<string, object>>(
                JsonSerializer.Serialize(properties)
            );
        }

        CorrelationId.ReadFrom(properties).ShouldBe("publisher-123");
    }

    /// <summary>Checks JSON values that are not valid correlation strings are ignored.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"invalid value\"")]
    public void ReadFrom_UnsupportedJsonValue_DoesNotStringify(string json)
    {
        var properties = new Dictionary<string, object>
        {
            [CorrelationId.HeaderName] = JsonSerializer.Deserialize<JsonElement>(json),
        };

        CorrelationId.ReadFrom(properties).ShouldBeNull();
    }

    /// <summary>Checks absent, oversized, arbitrary, and disposed metadata does not produce an identifier.</summary>
    [Fact]
    public void ReadFrom_UnusableMetadata_ReturnsNullWithoutCallingToString()
    {
        CorrelationId.ReadFrom(null).ShouldBeNull();
        CorrelationId.ReadFrom(new Dictionary<string, object>()).ShouldBeNull();
        CorrelationId
            .ReadFrom(
                new Dictionary<string, object>
                {
                    [CorrelationId.HeaderName] = new UnprintableValue(),
                }
            )
            .ShouldBeNull();
        CorrelationId
            .ReadFrom(
                new Dictionary<string, object> { [CorrelationId.HeaderName] = new string('x', 129) }
            )
            .ShouldBeNull();
        using var document = JsonDocument.Parse("\"publisher\"");
        var properties = new Dictionary<string, object>
        {
            [CorrelationId.HeaderName] = document.RootElement,
        };
        document.Dispose();
        CorrelationId.ReadFrom(properties).ShouldBeNull();
    }

    /// <summary>Checks the application scope takes precedence over baggage while an explicit message identifier remains unchanged.</summary>
    [Fact]
    public void PropagateTo_WithScopeAndActivity_UsesScopeAndPreservesExplicitIdentifier()
    {
        using var activity = new Activity("unrelated").Start();
        activity.SetBaggage(CorrelationId.ActivityBaggageName, "activity-correlation");
        using var scope = CorrelationId.BeginScope("publisher-correlation");
        var properties = new Dictionary<string, object>();
        CorrelationId.PropagateTo(properties);
        properties[CorrelationId.HeaderName].ShouldBe("publisher-correlation");

        properties[CorrelationId.HeaderName] = "explicit-correlation";
        CorrelationId.PropagateTo(properties);
        properties[CorrelationId.HeaderName].ShouldBe("explicit-correlation");
        CorrelationId.PropagateTo(null);
    }

    /// <summary>Checks a null metadata placeholder is filled from the publisher context.</summary>
    [Fact]
    public void PropagateTo_NullMetadataValue_UsesPublisherIdentifier()
    {
        using var scope = CorrelationId.BeginScope("publisher-correlation");
        var properties = new Dictionary<string, object> { [CorrelationId.HeaderName] = null };
        CorrelationId.PropagateTo(properties);
        CorrelationId.ReadFrom(properties).ShouldBe("publisher-correlation");
    }

    /// <summary>Checks republished JSON metadata retains the explicit ID as a supported native transport string.</summary>
    [Fact]
    public void PropagateTo_SerializedExplicitIdentifier_NormalizesStringWithoutReplacingOrigin()
    {
        using var scope = CorrelationId.BeginScope("unrelated-publisher");
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(
            "{\"CorrelationId\":\"original-publisher\"}"
        );
        CorrelationId.PropagateTo(properties);
        properties[CorrelationId.HeaderName]
            .ShouldBeOfType<string>()
            .ShouldBe("original-publisher");
    }

    /// <summary>Checks uncorrelated processing suppresses unrelated worker baggage and restores the worker afterward.</summary>
    [Fact]
    public void BeginScope_NullValue_ClearsWorkerBaggageAndRestoresPreviousContext()
    {
        using var activity = new Activity("worker").Start();
        activity.SetBaggage(CorrelationId.ActivityBaggageName, "worker-correlation");
        using (CorrelationId.BeginScope(null))
        {
            CorrelationId.Current.ShouldBeNull();
            var properties = new Dictionary<string, object>();
            CorrelationId.PropagateTo(properties);
            properties.ShouldBeEmpty();
        }

        CorrelationId.Current.ShouldBe("worker-correlation");
    }

    private sealed class UnprintableValue
    {
        public override string ToString() =>
            throw new InvalidOperationException("Must not stringify metadata");
    }

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
                GrandchildBaggage: grandchild.GetBaggageItem(CorrelationId.ActivityBaggageName),
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
