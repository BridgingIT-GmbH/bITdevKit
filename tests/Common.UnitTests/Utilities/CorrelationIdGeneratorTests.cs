// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

/// <summary>Verifies the shared format used when starting an application correlation flow.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CorrelationIdGeneratorTests</code></example>
[UnitTest("Common")]
public class CorrelationIdGeneratorTests
{
    /// <summary>Checks newly generated identifiers have the same format as request identifiers.</summary>
    /// <example><code>test.Create_NewFlow_ReturnsShortLowercaseAlphanumericIdentifier();</code></example>
    [Fact]
    public void Create_NewFlow_ReturnsShortLowercaseAlphanumericIdentifier()
    {
        // Act
        var identifier = CorrelationIdGenerator.Create();

        // Assert
        CorrelationIdGenerator.GeneratedLength.ShouldBe(12);
        identifier.Length.ShouldBe(CorrelationIdGenerator.GeneratedLength);
        identifier
            .All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9')
            .ShouldBeTrue();
        CorrelationId.IsValid(identifier).ShouldBeTrue();
        Guid.TryParse(identifier, out _).ShouldBeFalse();
    }

    /// <summary>Checks concurrent callers receive independently generated identifiers.</summary>
    /// <example><code>test.Create_ConcurrentFlows_GeneratesIndependentIdentifiers();</code></example>
    [Fact]
    public void Create_ConcurrentFlows_GeneratesIndependentIdentifiers()
    {
        // Act
        var identifiers = Enumerable
            .Range(0, 256)
            .AsParallel()
            .Select(_ => CorrelationIdGenerator.Create())
            .ToArray();

        // Assert
        identifiers.Distinct(StringComparer.Ordinal).Count().ShouldBe(identifiers.Length);
        identifiers.ShouldAllBe(identifier =>
            identifier.Length == 12 && CorrelationId.IsValid(identifier)
        );
    }

    /// <summary>Checks publishers preserve explicit metadata and otherwise continue their caller's scope.</summary>
    /// <example><code>test.GetOrCreate_Publisher_PreservesOrigin(null, "origin");</code></example>
    [Theory]
    [InlineData(null, "origin")]
    [InlineData("explicit", "explicit")]
    [InlineData("invalid\r\nvalue", "origin")]
    public void GetOrCreate_Publisher_PreservesOrigin(string supplied, string expected)
    {
        using var scope = CorrelationId.BeginScope("origin");
        var properties = new Dictionary<string, object>();
        if (supplied is not null)
        {
            properties[CorrelationId.HeaderName] = supplied;
        }

        var identifier = CorrelationIdGenerator.GetOrCreate(properties);

        identifier.ShouldBe(expected);
        properties[CorrelationId.HeaderName].ShouldBe(expected);
    }

    /// <summary>Checks new origins and legacy consumers receive one stored ID independent of worker scopes.</summary>
    /// <example><code>test.GetOrCreate_NewBoundary_CreatesAndReusesShortIdentifier(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetOrCreate_NewBoundary_CreatesAndReusesShortIdentifier(bool consumer)
    {
        using var scope = CorrelationId.BeginScope(consumer ? "worker" : null);
        var properties = new Dictionary<string, object>();

        var identifier = CorrelationIdGenerator.GetOrCreate(properties, useAmbient: !consumer);

        identifier.Length.ShouldBe(12);
        identifier.ShouldNotBe("worker");
        CorrelationId.IsValid(identifier).ShouldBeTrue();
        properties[CorrelationId.HeaderName].ShouldBe(identifier);
        CorrelationIdGenerator.GetOrCreate(properties, useAmbient: !consumer).ShouldBe(identifier);
    }
}
