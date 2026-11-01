// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Auditing;

using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Auditing;
using Infrastructure.EntityFramework.Auditing;

/// <summary>Verifies exact search keys and the canonical projection byte contract.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~AuditSearchProjectionTests</code></example>
[UnitTest("Infrastructure")]
public sealed class AuditSearchProjectionTests
{
    /// <summary>Checks decimal extremes, adjacent large integers and every supported scale.</summary>
    /// <example><code>AuditSearchEncoding.Number(decimal.MaxValue);</code></example>
    [Fact]
    public void Number_FullDecimalRangeAndEveryScale_PreservesExactOrderingAndRoundtrip()
    {
        var values = new List<decimal>
        {
            decimal.MinValue, decimal.MinValue + 1, long.MinValue - 1m, long.MinValue,
            -9_007_199_254_740_993m, -9_007_199_254_740_992m, -1m, 0m, 1m,
            9_007_199_254_740_992m, 9_007_199_254_740_993m, long.MaxValue,
            long.MaxValue + 1m, decimal.MaxValue - 1, decimal.MaxValue
        };
        for (byte scale = 0; scale <= 28; scale++)
        {
            foreach (var negative in new[] { false, true })
            {
                values.Add(new decimal(1, 0, 0, negative, scale));
                values.Add(new decimal(-1, -1, -1, negative, scale));
                values.Add(new decimal(int.MaxValue, int.MinValue, 1, negative, scale));
            }
        }

        var ordered = values.Distinct().Order().ToArray();
        var keys = ordered.Select(AuditSearchEncoding.Number).ToArray();

        keys.Distinct(StringComparer.Ordinal).Count().ShouldBe(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            keys[index].Length.ShouldBe(59);
            decimal.Parse(AuditSearchEncoding.Decode(keys[index], "Decimal"), CultureInfo.InvariantCulture).ShouldBe(ordered[index]);
            if (index > 0)
            {
                StringComparer.Ordinal.Compare(keys[index - 1], keys[index]).ShouldBeLessThan(0);
            }
        }
    }

    /// <summary>Checks equal values retain one key regardless of scale or signed zero.</summary>
    /// <example><code>AuditSearchEncoding.Number(1.00m);</code></example>
    [Fact]
    public void Number_EquivalentScalesAndSignedZero_ProducesIdenticalKeys()
    {
        for (byte scale = 0; scale <= 28; scale++)
        {
            var one = decimal.Parse("1" + (scale == 0 ? "" : "." + new string('0', scale)), CultureInfo.InvariantCulture);

            AuditSearchEncoding.Number(one).ShouldBe(AuditSearchEncoding.Number(1m));
            AuditSearchEncoding.Number(-one).ShouldBe(AuditSearchEncoding.Number(-1m));
            AuditSearchEncoding.Number(new decimal(0, 0, 0, false, scale)).ShouldBe(AuditSearchEncoding.Number(0m));
            AuditSearchEncoding.Number(new decimal(0, 0, 0, true, scale)).ShouldBe(AuditSearchEncoding.Number(0m));
        }
    }

    /// <summary>Uses the BCL comparer as the Unicode matching and ordering oracle.</summary>
    /// <example><code>AuditSearchEncoding.Text("Straße", true);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Text_UnicodeCorpus_PreservesOrdinalComparisonAndEquality(bool ignoreCase)
    {
        string[] values = ["", "a", "A", "aa", "aA", "a\0", "\0", "|", "%_\\", "é", "É", "e\u0301",
            "i", "I", "ı", "İ", "ß", "ẞ", "ss", "ſ", "S", "K", "K", "µ", "Μ", "σ", "ς", "Σ",
            "\ud801\udc28", "\ud801\udc00", "\ud83d\ude00", "\ue000", "\ud800", "\udfff"];
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        foreach (var left in values)
        {
            var leftKey = AuditSearchEncoding.Text(left, ignoreCase);
            comparer.Equals(AuditSearchEncoding.Decode(leftKey, "String"), left).ShouldBeTrue();
            foreach (var right in values)
            {
                var rightKey = AuditSearchEncoding.Text(right, ignoreCase);
                var context = $"{JsonSerializer.Serialize(left)} / {JsonSerializer.Serialize(right)}, ignoreCase={ignoreCase}";

                Math.Sign(StringComparer.Ordinal.Compare(leftKey, rightKey)).ShouldBe(Math.Sign(comparer.Compare(left, right)), context);
                (leftKey == rightKey).ShouldBe(comparer.Equals(left, right), context);
            }
        }
    }

    /// <summary>Checks encoded substring boundaries and escaped-looking user text.</summary>
    /// <example><code>AuditSearchEncoding.Text("%_", false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Text_EncodedSubstrings_MatchesOnlyCompleteUtf16Units(bool ignoreCase)
    {
        string[] values = ["", "A", "\u1234\u5678", "before%_\\|after", "Straße Σς", "\ud801\udc28-end", "éclair"];
        string[] needles = ["", "A", "a", "\u2345", "\u3456", "\u1234", "\u5678", "%_\\|", "|", "Σ", "σ", "ς", "SS", "é", "É", "\ud801\udc00", "end"];
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var value in values)
        {
            foreach (var needle in needles)
            {
                var encoded = AuditSearchEncoding.Text(value, ignoreCase);
                var key = AuditSearchEncoding.Text(needle, ignoreCase);

                encoded.Contains(key, StringComparison.Ordinal).ShouldBe(value.Contains(needle, comparison));
                encoded.StartsWith(key, StringComparison.Ordinal).ShouldBe(value.StartsWith(needle, comparison));
                encoded.EndsWith(key, StringComparison.Ordinal).ShouldBe(value.EndsWith(needle, comparison));
            }
        }
    }

    /// <summary>Checks every owned projection domain against canonical bytes and independent SHA-256.</summary>
    /// <example><code>AuditSearchProjection.Build(record, "Record");</code></example>
    [Theory]
    [InlineData("Record")]
    [InlineData("Action")]
    [InlineData("Change")]
    [InlineData("Resource")]
    [InlineData("Error")]
    [InlineData("Property")]
    public async Task Build_AllProjectionDomains_ProducesCanonicalBytesAndMatchingStreamedHash(string domain)
    {
        var record = Record();
        object original = domain switch
        {
            "Record" => record,
            "Action" => record.Actions[0],
            "Change" => record.EntityChanges[0],
            "Resource" => record.Resources[0],
            "Error" => record.Errors[0],
            _ => record.EntityChanges[0].Properties[0]
        };

        var json = AuditSearchProjection.Build(original, domain);
        var bytes = Encoding.UTF8.GetBytes(json);
        using var document = JsonDocument.Parse(bytes);
        using var reader = new ChunkedTextReader(json, 7);
        var storedHash = await AuditSearchProjection.HashStoredAsync(reader, bytes.Length);

        bytes.ShouldBe(AuditCanonicalizerV1.Encode(document.RootElement));
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        AuditSearchProjection.ComputeHash(original, domain).ShouldBe(expectedHash);
        storedHash.ShouldBe(expectedHash);
        json.All(character => character <= 127).ShouldBeTrue();
    }

    /// <summary>Checks projection construction does not expose source property values.</summary>
    /// <example><code>AuditSearchProjection.Build(change, "Change");</code></example>
    [Fact]
    public void Build_ChangeEvidence_IndexesPropertyMetadataWithoutOldOrNewValues()
    {
        var change = Record().EntityChanges[0];

        var json = AuditSearchProjection.Build(change, "Change");

        json.ShouldContain(AuditSearchEncoding.Text("Name", false));
        json.ShouldNotContain(AuditSearchEncoding.Text("private-before", false));
        json.ShouldNotContain(AuditSearchEncoding.Text("private-after", false));
        json.ShouldNotContain("private-before");
        json.ShouldNotContain("private-after");
    }

    /// <summary>Checks the byte limit over multiple buffered reads.</summary>
    /// <example><code>await AuditSearchProjection.HashStoredAsync(reader, maximumBytes);</code></example>
    [Fact]
    public async Task HashStoredAsync_ExactLimitAndOneByteOverflow_AcceptsBoundaryAndRejectsOverflow()
    {
        var json = AuditSearchProjection.Build(Record(), "Record");
        json.Length.ShouldBeGreaterThan(2_048);
        using var exactReader = new ChunkedTextReader(json, 31);
        using var oversizedReader = new ChunkedTextReader(json, 31);

        var exact = await AuditSearchProjection.HashStoredAsync(exactReader, json.Length);
        var error = await Should.ThrowAsync<ArgumentException>(() => AuditSearchProjection.HashStoredAsync(oversizedReader, json.Length - 1));

        exact.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))));
        error.Message.ShouldBe("AuditSearchProjectionTooLarge");
    }

    /// <summary>Checks non-ASCII data is rejected after earlier valid chunks.</summary>
    /// <example><code>await AuditSearchProjection.HashStoredAsync(reader, 4096);</code></example>
    [Theory]
    [InlineData("é")]
    [InlineData("\ud800")]
    public async Task HashStoredAsync_NonAsciiText_RejectsInvalidProfile(string suffix)
    {
        using var reader = new ChunkedTextReader(new string(' ', 2_048) + suffix, 7);

        var error = await Should.ThrowAsync<ArgumentException>(() => AuditSearchProjection.HashStoredAsync(reader, 4_096));

        error.Message.ShouldBe("AuditSearchProjectionInvalid");
    }

    /// <summary>Checks cancellation reaches a streaming text reader.</summary>
    /// <example><code>await AuditSearchProjection.HashStoredAsync(reader, 1024, token);</code></example>
    [Fact]
    public async Task HashStoredAsync_CancelledRead_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var reader = new StringReader("{}");

        await Should.ThrowAsync<OperationCanceledException>(() => AuditSearchProjection.HashStoredAsync(reader, 2, cancellation.Token));
    }

    private static AuditRecord Record()
    {
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        return new()
        {
            AuditId = Guid.Parse("641b9509-989a-40f8-ad1e-60162aebaf83"), Key = "orders.approve", StartedUtc = timestamp, CompletedUtc = timestamp.AddTicks(1),
            Details = ImmutableDictionary<string, AuditValue>.Empty
                .Add("quantity", AuditValue.From(79228162514264337593543950335m))
                .Add("description", AuditValue.From(new string('é', 500)))
                .Add("hidden", AuditValue.Policy("Redacted")),
            Resources = [new("Order", "O-123", "訂單")], Errors = [new() { Code = "Recovered", ExceptionType = "TransientFailure" }],
            Actions = [new() { Id = Guid.Parse("f8f0f274-ccf7-4fd9-a5d6-c80d3fd2a40d"), Key = "orders.check", Sequence = 2, StartedUtc = timestamp, CompletedUtc = timestamp }],
            EntityChanges = [new()
            {
                EntityType = "Order", EntityId = "O-123", Timestamp = timestamp,
                Properties = [new() { Path = "Name", Operation = "Replace", OldValue = AuditValue.From("private-before"), NewValue = AuditValue.From("private-after") }]
            }]
        };
    }

    private sealed class ChunkedTextReader(string text, int maximumChunk) : StringReader(text)
    {
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumChunk)], cancellationToken);
    }
}
