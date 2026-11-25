// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using Application.Auditing;

/// <summary>Checks the legacy byte-limit behavior of audit display text.</summary>
/// <example><code>var label = AuditCollector.Text("Café", maxBytes: 5);</code></example>
public sealed class AuditCollectorTextTests
{
    /// <summary>Checks negative, zero and positive limits through the Auditing API.</summary>
    /// <example><code>var label = AuditCollector.Text("Café", maxBytes: -1); // ""</code></example>
    [Theory]
    [InlineData(null, -1, null)]
    [InlineData("", -1, "")]
    [InlineData("Café", -1, "")]
    [InlineData("Café", 0, "")]
    [InlineData("é😀!", 5, "é")]
    [InlineData("é😀!", 6, "é😀")]
    public void Text_ByteBudget_PreservesLegacyResult(string value, int maxBytes, string expected)
    {
        var result = AuditCollector.Text(value: value, maxBytes: maxBytes);

        result.ShouldBe(expected);
    }

    /// <summary>Checks that unpaired surrogates are preserved in fitting text and replaced when truncating.</summary>
    /// <example><code>var label = AuditCollector.Text("A\uD800!", 4); // "A\uFFFD"</code></example>
    [Fact]
    public void Text_UnpairedSurrogate_PreservesLegacyFallback()
    {
        var value = "A\uD800!";

        AuditCollector.Text(value, 5).ShouldBeSameAs(value);
        AuditCollector.Text(value, 4).ShouldBe("A\uFFFD");
    }
}
