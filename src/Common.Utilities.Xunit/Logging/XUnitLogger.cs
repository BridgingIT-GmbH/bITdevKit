// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public class XunitLogger : ILogger
{
    private readonly ITestOutputHelper output;
    private readonly string categoryName;
    private readonly LoggerExternalScopeProvider scopeProvider;

    public XunitLogger(
        ITestOutputHelper output,
        LoggerExternalScopeProvider scopeProvider,
        string categoryName)
    {
        this.output = output;
        this.scopeProvider = scopeProvider;
        this.categoryName = categoryName;
    }

    public static ILogger Create(ITestOutputHelper output) =>
        new XunitLogger(output, new LoggerExternalScopeProvider(), string.Empty);

    public static ILogger<T> Create<T>(ITestOutputHelper output) =>
        new XunitLogger<T>(output, new LoggerExternalScopeProvider());

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public IDisposable BeginScope<TState>(TState state) => this.scopeProvider.Push(state);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        var sb = new StringBuilder()
            .Append(GetLogLevelString(logLevel))
            .Append(" [").Append(this.categoryName).Append("] ")
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
        }, sb);

        this.output?.WriteLine(sb.ToString());
    }

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