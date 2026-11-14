// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>Provides bounded metadata inspection and cache operations through the common authorized requester boundary.</summary>
/// <param name="configuration">The validated independent Console exposure policy.</param>
/// <remarks>Output is structured JSON. Keys and dimensions remain redacted unless the current host explicitly grants sensitive inspection.</remarks>
/// <example><code>
/// application-caching collections
/// application-caching entries --collection cards --take 50
/// application-caching preview --collection cards --prefix region:
/// application-caching clear --collection cards --background
/// application-caching operation-status --collection cards --id 102030405060708090a0b0c0d0e0f000
/// </code></example>
public sealed class ApplicationCachingConsoleCommand(CacheManagementConfiguration configuration) : ConsoleCommandBase("application-caching", "Advanced cache metadata and operational controls", "caching")
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Gets or sets the finite management operation; help is available without management authorization.</summary>
    [ConsoleCommandArgument(0, Required = false, Description = "help, collections, inspect, entries, status, diagnostics, preview, remove, invalidate, clear, pause, resume, operation-status, operation-resume, operation-cancel")]
    public string Operation { get; set; } = "help";

    /// <summary>Gets or sets one explicitly selected declared collection.</summary>
    [ConsoleCommandOption("collection", Alias = "c", Description = "One declared collection")]
    public string Collection { get; set; }

    /// <summary>Gets or sets at most 32 comma-separated explicit invalidation or clear targets.</summary>
    [ConsoleCommandOption("collections", Description = "Explicit comma-separated invalidation or clear targets")]
    public string Collections { get; set; }

    /// <summary>Gets or sets the exact key selector or removal target.</summary>
    [ConsoleCommandOption("key", Alias = "k", Description = "Exact key; wildcard characters remain literal")]
    public string Key { get; set; }

    /// <summary>Gets or sets a literal prefix selector.</summary>
    [ConsoleCommandOption("prefix", Description = "Literal key prefix selector")]
    public string Prefix { get; set; }

    /// <summary>Gets or sets an explicit portable whole-key glob selector.</summary>
    [ConsoleCommandOption("pattern", Description = "Explicit portable key glob selector")]
    public string Pattern { get; set; }

    /// <summary>Gets or sets at most 64 KiB of structured portable selection JSON.</summary>
    [ConsoleCommandOption("selection", Description = "Bounded structured CacheSelection JSON")]
    public string SelectionJson { get; set; }

    /// <summary>Gets or sets metadata page size, from one through 1,000.</summary>
    [ConsoleCommandOption("take", Description = "Metadata page size, at most 1000")]
    public int Take { get; set; } = 100;

    /// <summary>Gets or sets an opaque continuation for the same bounded selection.</summary>
    [ConsoleCommandOption("continuation", Description = "Opaque metadata continuation")]
    public string Continuation { get; set; }

    /// <summary>Gets or sets the original round-trip UTC cutoff returned with a metadata continuation.</summary>
    [ConsoleCommandOption("cutoff", Description = "Original page cutoff returned with --continuation")]
    public string Cutoff { get; set; }

    /// <summary>Gets or sets a retained opaque operation identifier.</summary>
    [ConsoleCommandOption("id", Description = "Retained opaque operation identifier")]
    public string OperationId { get; set; }

    /// <summary>Gets or sets whether invalidation or clear returns accepted status before execution.</summary>
    [ConsoleCommandOption("background", Description = "Accept tracked invalidation before maintenance executes it")]
    public bool Background { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(this.Operation) || this.Operation.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            console.WriteLine("application-caching " + string.Join(" | ", CacheManagementOperations.Names));
            console.WriteLine("Select --collection, or explicit --collections for invalidation/clear. Use one of --key, --prefix, --pattern, --selection.");
            console.WriteLine("Use --take/--continuation for bounded metadata, --background for accepted invalidation, and --id for operation controls.");
            return;
        }

        if (!configuration.ConsoleEnabled)
        {
            console.WriteLine(JsonSerializer.Serialize(new { Success = false, Code = "FeatureUnavailable" }, Json));
            return;
        }

        if (this.Collections?.Length > 32 * 129)
        {
            console.WriteLine(JsonSerializer.Serialize(new { Success = false, Code = "Validation", Message = "Explicit collection targets exceed their finite bound." }, Json));
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetService<CacheManagementDispatcher>();
        if (dispatcher is null)
        {
            console.WriteLine(JsonSerializer.Serialize(new { Success = false, Code = "FeatureUnavailable" }, Json));
            return;
        }

        DateTimeOffset? cutoff = null;
        if (this.Cutoff is not null)
        {
            if (!DateTimeOffset.TryParse(this.Cutoff, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
            {
                console.WriteLine(JsonSerializer.Serialize(new { Success = false, Code = "Validation", Message = "The original page cutoff must be a round-trip timestamp." }, Json));
                return;
            }

            cutoff = value;
        }

        var selected = this.Collections?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var result = await dispatcher.ExecuteAsync("ConsoleCommands", this.Operation, new(this.Collection, selected, this.Key, this.Prefix,
            this.Pattern, this.SelectionJson, this.Take, this.Continuation, this.OperationId, this.Background, cutoff), cancellationToken).ConfigureAwait(false);
        console.WriteLine(JsonSerializer.Serialize(new
        {
            Success = result.IsSuccess, Data = result.IsSuccess ? result.Value : null,
            Errors = result.Errors.Select(error => new { Code = error.GetType().Name, error.Message }).ToArray()
        }, Json));
    }
}