// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.EntityFrameworkCore.Database.Command;

using System.Data.Common;
using Diagnostics;
using Constants = BridgingIT.DevKit.Infrastructure.EntityFramework.Constants;

public partial class CommandLoggerInterceptor(ILoggerFactory loggerFactory) : DbCommandInterceptor
{
    private readonly ILogger<CommandLoggerInterceptor> logger =
        loggerFactory?.CreateLogger<CommandLoggerInterceptor>() ??
        NullLoggerFactory.Instance.CreateLogger<CommandLoggerInterceptor>();

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        this.LogExecuting(command, eventData);

        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuting(command, eventData);

        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuted(command, eventData);

        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        this.LogExecuted(command, eventData);

        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuting(command, eventData);

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        this.LogExecuting(command, eventData);

        return base.ReaderExecuting(command, eventData, result);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        this.LogExecuted(command, eventData);

        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuted(command, eventData);

        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        this.LogExecuting(command, eventData);

        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuting(command, eventData);

        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override object ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object result)
    {
        this.LogExecuted(command, eventData);

        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuted(command, eventData);

        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void LogExecuting(DbCommand command, CommandEventData eventData)
    {
        TypedLogger.LogCommandExecuting(this.logger,
            Constants.LogKey,
            eventData.CommandId.ToString(),
            eventData.Context.GetType().Name,
            command.CommandText.Replace('\n', ' '));
    }

    private void LogExecuted(DbCommand command, CommandExecutedEventData eventData)
    {
        TypedLogger.LogCommandExecuted(this.logger,
            Constants.LogKey,
            eventData.CommandId.ToString(),
            eventData.Context.GetType().Name,
            eventData.Duration.TotalMilliseconds);
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0,
            LogLevel.Debug,
            "{LogKey} database command executing (id={DbCommandId}, context={DbContextName}) {DbCommandCommandText}")]
        public static partial void LogCommandExecuting(
            ILogger logger,
            string logKey,
            string dbCommandId,
            string dbContextName,
            string dbCommandCommandText);

        [LoggerMessage(1,
            LogLevel.Debug,
            "{LogKey} database command executed (id={DbCommandId}, context={DbContextName}) -> took {DbCommandTimeElapsed:0.0000} ms")]
        public static partial void LogCommandExecuted(
            ILogger logger,
            string logKey,
            string dbCommandId,
            string dbContextName,
            double dbCommandTimeElapsed);
    }
}