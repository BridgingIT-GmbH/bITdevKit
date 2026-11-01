// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Hosting;

/// <summary>Validates pinned formatter identities once without enabling source capture.</summary>
/// <example><code>var formatter = registry.Find("csv");</code></example>
public sealed class AuditExportRegistry
{
    private readonly IReadOnlyDictionary<string, IAuditExportFormatter> formatters;

    /// <summary>Rejects duplicate or unsafe formatter identities before serving exports.</summary>
    /// <example><code>var registry = new AuditExportRegistry([new NdjsonAuditExportFormatter(), new CsvAuditExportFormatter()]);</code></example>
    public AuditExportRegistry(IEnumerable<IAuditExportFormatter> formatters)
    {
        var values = formatters?.ToArray() ?? [];
        if (values.Length is < 1 or > 32 || values.Any(value => value is null || !Safe(value.Format, 32) || !Safe(value.Extension, 12)
            || string.IsNullOrWhiteSpace(value.Profile) || value.Profile.Length > 128 || value.Profile.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(value.MediaType) || value.MediaType.Length > 128 || value.MediaType.Any(char.IsControl)
            || value.Omissions.Length > 64 || value.Omissions.Any(item => item is null || item.Length > 512 || item.Any(char.IsControl)))
            || values.Select(value => value.Format).Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
            throw new ArgumentException("Audit export formatter identities must be unique and bounded.");
        this.formatters = values.ToDictionary(value => value.Format, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns the bounded registered output capabilities.</summary>
    /// <example><code>var profiles = registry.Formatters;</code></example>
    public IReadOnlyList<IAuditExportFormatter> Formatters => this.formatters.Values.ToArray();

    /// <summary>Resolves an explicitly requested format without fallback.</summary>
    /// <example><code>var selected = registry.Find("ndjson");</code></example>
    public Result<IAuditExportFormatter> Find(string format) => format is not null && this.formatters.TryGetValue(format, out var value)
        ? Result<IAuditExportFormatter>.Success(value) : Result<IAuditExportFormatter>.Failure(new AuditError("AuditExportFormatUnsupported"));

    private static bool Safe(string value, int maximum) => value?.Length is > 0 && value.Length <= maximum && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}

/// <summary>Resolves formatter validation during ordinary host startup, without querying or creating an audit database.</summary>
/// <example><code>await validation.StartAsync(cancellationToken);</code></example>
public sealed class AuditExportValidationService(AuditExportRegistry registry, AuditExportOptions options) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) { options.Validate(); _ = registry.Formatters; return Task.CompletedTask; }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
