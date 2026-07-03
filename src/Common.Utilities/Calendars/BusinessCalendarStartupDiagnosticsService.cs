// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Writes registered business calendar diagnostics during host startup.
/// </summary>
/// <example>
/// <code>
/// services.AddBusinessCalendars(calendars => calendars.RegisterCountry("NL", new BusinessCalendar()));
/// </code>
/// </example>
public sealed class BusinessCalendarStartupDiagnosticsService(
    IEnumerable<BusinessCalendarOptions> options,
    ILoggerFactory loggerFactory) : IHostedService
{
    private const string LogCategory = "BDK";
    private readonly ILogger logger = loggerFactory.CreateLogger(LogCategory);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var optionSummaries = options
            .Select((option, index) => new
            {
                Index = index + 1,
                Default = option.DefaultCalendarFactory is not null
                    ? "factory"
                    : option.DefaultCalendar?.GetType().FullName ?? "none",
                Registrations = option.Registrations
                    .Select(registration =>
                        $"{string.Join("|", registration.Codes)}={registration.Calendar?.GetType().FullName ?? "factory"}")
                    .OrderBy(registration => registration, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .ToArray();

        this.logger.LogDebug(
            "[BDK] business calendars registered (count={OptionsCount}, calendars={Calendars})",
            optionSummaries.Length,
            string.Join("; ", optionSummaries.Select(summary =>
                $"options#{summary.Index}[default={summary.Default}; registrations={string.Join(",", summary.Registrations)}]")));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
