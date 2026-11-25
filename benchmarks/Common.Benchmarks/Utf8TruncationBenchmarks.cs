// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using System.Text;
using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Common;

/// <summary>Measures UTF-8 truncation with both fitting and over-budget inputs.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter '*Utf8TruncationBenchmarks*'</code></example>
[MemoryDiagnoser]
[WarmupCount(3)]
[IterationCount(5)]
public class Utf8TruncationBenchmarks
{
    private string input;
    private int budget;

    /// <summary>Gets or sets the input and byte-budget combination to measure.</summary>
    /// <example><code>benchmark.Scenario = "LongAscii";</code></example>
    [Params("LongAscii", "FittingAscii", "FittingMultibyte", "TruncatedMultibyte")]
    public string Scenario { get; set; }

    /// <summary>Prepares an input outside measurement and verifies both implementations agree.</summary>
    /// <example><code>benchmark.Setup();</code></example>
    [GlobalSetup]
    public void Setup()
    {
        (this.input, this.budget) = this.Scenario switch
        {
            "LongAscii" => (new string('a', 1_048_576), 32),
            "FittingAscii" => (new string('a', 32), 32),
            "FittingMultibyte" => (new string('é', 32), 64),
            "TruncatedMultibyte" => (new string('é', 32), 32),
            _ => throw new ArgumentOutOfRangeException(nameof(this.Scenario))
        };

        if (!string.Equals(this.Before(), this.After(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The truncation results differ.");
        }
    }

    /// <summary>Measures the original full-input byte-count preflight.</summary>
    /// <example><code>var prefix = benchmark.Before();</code></example>
    [Benchmark(Baseline = true)]
    public string Before() => BaselineTruncateUtf8(this.input, this.budget);

    /// <summary>Measures truncation with the UTF-16 length guard.</summary>
    /// <example><code>var prefix = benchmark.After();</code></example>
    [Benchmark]
    public string After() => this.input.TruncateUtf8(this.budget);

    // Snapshot of TruncateUtf8 from d535627ae.
    private static string BaselineTruncateUtf8(string source, int maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        if (source is null || Encoding.UTF8.GetByteCount(source) <= maximumBytes)
        {
            return source;
        }

        var builder = new StringBuilder();
        Span<char> encodedRune = stackalloc char[2];
        var remainingBytes = maximumBytes;
        foreach (var rune in source.EnumerateRunes())
        {
            if (rune.Utf8SequenceLength > remainingBytes)
            {
                break;
            }

            builder.Append(encodedRune[..rune.EncodeToUtf16(encodedRune)]);
            remainingBytes -= rune.Utf8SequenceLength;
        }

        return builder.ToString();
    }
}
