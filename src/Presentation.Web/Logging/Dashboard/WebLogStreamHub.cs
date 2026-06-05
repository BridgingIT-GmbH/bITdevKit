// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;

using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Application.Utilities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// SignalR hub that streams stored log entries to the dashboard terminal view.
/// </summary>
/// <example>
/// <code>
/// endpoints.MapHub&lt;WebLogStreamHub&gt;("/_bdk/dashboard/logentries/stream/hub");
/// </code>
/// </example>
public sealed class WebLogStreamHub(
    IServiceScopeFactory scopeFactory,
    ILogger<WebLogStreamHub> logger) : Hub
{
    private const int PollPageSize = 100;

    /// <summary>
    /// Streams initial log rows and then tails newer rows from the registered log entry service.
    /// </summary>
    public async IAsyncEnumerable<LogStreamMessage> StreamLogs(
        LogStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        request ??= new LogStreamRequest();

        var filter = CreateFilter(request);
        var pollingInterval = TimeSpan.FromMilliseconds(250);
        long? lastId = null;

        using (var initialScope = scopeFactory.CreateScope())
        {
            var service = initialScope.ServiceProvider.GetService<ILogEntryService>();
            if (service is null)
            {
                yield return LogStreamMessage.Error("\x1b[31mILogEntryService is not registered. Log streaming is unavailable.\x1b[0m\r\n");
                yield break;
            }

            var latestResponse = await service.QueryAsync(CreateQuery(filter, 1), cancellationToken);
            var latest = latestResponse.Items.OrderByDescending(item => item.Id).FirstOrDefault();
            if (latest is not null)
            {
                lastId = latest.Id;
            }
        }

        yield return LogStreamMessage.Status($"\x1b[90mStreaming logs every {pollingInterval.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)}s. Waiting for new entries...\x1b[0m\r\n");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(pollingInterval, cancellationToken);

            LogStreamMessage[] messages;
            var stop = false;

            using (var scope = scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<ILogEntryService>();
                if (service is null)
                {
                    messages = [LogStreamMessage.Error("\x1b[31mILogEntryService is no longer available. Stream stopped.\x1b[0m\r\n")];
                    stop = true;
                }
                else
                {
                    try
                    {
                        var response = await service.QueryAsync(CreateQuery(filter, PollPageSize, lastId), cancellationToken);
                        var items = response.Items
                            .OrderBy(item => item.Id)
                            .ToArray();

                        messages = items
                            .Select(item =>
                            {
                                lastId = Math.Max(lastId ?? 0, item.Id);
                                return LogStreamMessage.Output(LogStreamFormatter.Format(item), item.Id);
                            })
                            .ToArray();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Dashboard log stream polling failed (connectionId={ConnectionId})", this.Context.ConnectionId);
                        messages = [LogStreamMessage.Error($"\x1b[31mLog stream failed: {ex.Message}\x1b[0m\r\n")];
                    }
                }
            }

            foreach (var message in messages)
            {
                yield return message;
            }

            if (stop)
            {
                yield break;
            }
        }
    }

    private static LogEntriesDashboardFilter CreateFilter(LogStreamRequest request)
    {
        return new LogEntriesDashboardFilter
        {
            Level = ParseLevel(request.Level),
            PageSize = 1,
            LogKey = EmptyToNull(request.LogKey)
        };
    }

    private static LogEntryQueryRequest CreateQuery(LogEntriesDashboardFilter filter, int pageSize, long? afterId = null)
    {
        return new LogEntryQueryRequest
        {
            StartTime = filter.StartTime,
            Level = filter.Level,
            LogKey = filter.LogKey,
            PageSize = pageSize,
            AfterId = afterId
        };
    }

    private static string EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Microsoft.Extensions.Logging.LogLevel? ParseLevel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LogEntriesDashboard.DefaultLevel;
        }

        if (string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(value, "Verbose", StringComparison.OrdinalIgnoreCase))
        {
            return Microsoft.Extensions.Logging.LogLevel.Trace;
        }

        if (string.Equals(value, "Fatal", StringComparison.OrdinalIgnoreCase))
        {
            return Microsoft.Extensions.Logging.LogLevel.Critical;
        }

        return Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(value, true, out var parsed)
            ? parsed
            : LogEntriesDashboard.DefaultLevel;
    }
}
