// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.Caching;

/// <summary>Defines a bounded real-Kestrel cache transfer experiment without changing the legacy streaming runner.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Presentation.Benchmarks -- caching --output artifacts/cache-http.json --rounds 5 --iterations 100 --concurrency 8 --payload 4096</code></example>
public sealed record CacheHttpExperimentOptions
{
    /// <summary>Gets the machine-readable output outside the documentation tree.</summary>
    public string Output { get; init; } = "benchmarks/Presentation.Benchmarks/Caching/artifacts/http.json";

    /// <summary>Gets independent repeated measured rounds after warm-up.</summary>
    public int Rounds { get; init; } = 5;

    /// <summary>Gets complete transfers per caller per round.</summary>
    public int Iterations { get; init; } = 100;

    /// <summary>Gets simultaneous client workers sharing one real loopback server.</summary>
    public int Concurrency { get; init; } = 8;

    /// <summary>Gets exact uncompressed bytes per successful binary resource transfer.</summary>
    public int PayloadBytes { get; init; } = 4096;

    /// <summary>Parses explicit option/value pairs and rejects unbounded or unknown profiles.</summary>
    /// <param name="args">Arguments following the explicit caching selector.</param>
    /// <returns>The validated finite experiment profile.</returns>
    public static CacheHttpExperimentOptions Parse(string[] args)
    {
        var result = new CacheHttpExperimentOptions();
        if (args.Length % 2 != 0) { throw new ArgumentException("Cache experiment arguments must be option/value pairs.", nameof(args)); }

        for (var index = 0; index < args.Length; index += 2)
        {
            var value = args[index + 1];
            result = args[index] switch
            {
                "--output" => result with { Output = value },
                "--rounds" => result with { Rounds = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) },
                "--iterations" => result with { Iterations = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) },
                "--concurrency" => result with { Concurrency = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) },
                "--payload" => result with { PayloadBytes = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) },
                _ => throw new ArgumentException("Unknown cache experiment option: " + args[index], nameof(args))
            };
        }

        if (result.Rounds is < 5 or > 100 || result.Iterations is < 1 or > 100000 || result.Concurrency is < 1 or > 128 ||
            result.PayloadBytes is < 1 or > 1024 * 1024 || string.IsNullOrWhiteSpace(result.Output))
        {
            throw new ArgumentOutOfRangeException(nameof(args), "The cache HTTP profile exceeds its explicit finite limits.");
        }

        return result;
    }
}