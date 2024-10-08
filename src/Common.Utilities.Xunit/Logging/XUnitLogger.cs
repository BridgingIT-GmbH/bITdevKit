﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public class XunitLogger(
    ITestOutputHelper output,
    LoggerExternalScopeProvider scopeProvider,
    string categoryName) : ILogger
{
    private readonly string categoryName = categoryName;
    private readonly ITestOutputHelper output = output;
    private readonly LoggerExternalScopeProvider scopeProvider = scopeProvider;

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return this.scopeProvider.Push(state);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        var sb = new StringBuilder().Append(GetLogLevelString(logLevel))
            .Append(" [")
            .Append(this.categoryName)
            .Append("] ")
            .Append(formatter(state, exception));

        if (exception is not null)
        {
            sb.Append('\n').Append(exception);
        }

        // Append scopes
        this.scopeProvider.ForEachScope((scope, state) =>
            {
                state.Append("\n => ");
                state.Append(scope);
            },
            sb);

        this.output?.WriteLine(sb.ToString());
    }

#pragma warning disable SA1204
    public static ILogger Create(ITestOutputHelper output)
    {
        return new XunitLogger(output, new LoggerExternalScopeProvider(), string.Empty);
    }

    public static ILogger<T> Create<T>(ITestOutputHelper output)
    {
        return new XunitLogger<T>(output, new LoggerExternalScopeProvider());
    }
#pragma warning restore SA1204

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
        };
    }
}