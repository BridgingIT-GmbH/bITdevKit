// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.EntityFrameworkCore.Database.Command;

using System.Data.Common;
using Diagnostics;
using Constants = BridgingIT.DevKit.Infrastructure.EntityFramework.Constants;

/// <summary>
/// Represents command logger interceptor.
/// </summary>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public partial class CommandLoggerInterceptor(ILoggerFactory loggerFactory) : DbCommandInterceptor
{
    private readonly ILogger<CommandLoggerInterceptor> logger =
        loggerFactory?.CreateLogger<CommandLoggerInterceptor>() ??
        NullLoggerFactory.Instance.CreateLogger<CommandLoggerInterceptor>();

    /// <inheritdoc/>
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        this.LogExecuting(command, eventData);

        return base.NonQueryExecuting(command, eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuting(command, eventData);

        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuted(command, eventData);

        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        this.LogExecuted(command, eventData);

        return base.NonQueryExecuted(command, eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuting(command, eventData);

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        this.LogExecuting(command, eventData);

        return base.ReaderExecuting(command, eventData, result);
    }

    /// <inheritdoc/>
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        this.LogExecuted(command, eventData);

        return base.ReaderExecuted(command, eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuted(command, eventData);

        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        this.LogExecuting(command, eventData);

        return base.ScalarExecuting(command, eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        this.LogExecuting(command, eventData);

        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override object ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object result)
    {
        this.LogExecuted(command, eventData);

        return base.ScalarExecuted(command, eventData, result);
    }

    /// <inheritdoc/>
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

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the command executing operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="dbCommandId">The db command id used by the operation.</param>
        /// <param name="dbContextName">The db context name used by the operation.</param>
        /// <param name="dbCommandCommandText">The db command command text used by the operation.</param>
        [LoggerMessage(0,
            LogLevel.Debug,
            "[{LogKey}] database command executing (id={DbCommandId}, context={DbContextName}) {DbCommandCommandText}")]
        public static partial void LogCommandExecuting(
            ILogger logger,
            string logKey,
            string dbCommandId,
            string dbContextName,
            string dbCommandCommandText);

        /// <summary>
        /// Writes a log entry for the command executed operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="dbCommandId">The db command id used by the operation.</param>
        /// <param name="dbContextName">The db context name used by the operation.</param>
        /// <param name="dbCommandTimeElapsed">The db command time elapsed used by the operation.</param>
        [LoggerMessage(1,
            LogLevel.Debug,
            "[{LogKey}] database command executed (id={DbCommandId}, context={DbContextName}) -> took {DbCommandTimeElapsed:0.0000} ms")]
        public static partial void LogCommandExecuted(
            ILogger logger,
            string logKey,
            string dbCommandId,
            string dbContextName,
            double dbCommandTimeElapsed);
    }
}
