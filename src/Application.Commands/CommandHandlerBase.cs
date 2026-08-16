// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

/// <summary>
/// Represents command handler base.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
[Obsolete("Use the new Requester from now on")]
public abstract partial class CommandHandlerBase<TCommand>
    : MediatR.IRequestHandler<TCommand, CommandResponse>, ICommandRequestHandler
    where TCommand : class, ICommandRequest<CommandResponse>
{
    private const string CommandIdKey = "CommandRequestId";
    private const string CommandTypeKey = "CommandType";

    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources;

    /// <summary>
    /// Initializes a new instance of the <c>CommandHandlerBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="moduleAccessors">The module accessors used by the operation.</param>
    /// <param name="activitySources">The activity sources used by the operation.</param>
    protected CommandHandlerBase(
        ILoggerFactory loggerFactory,
        IEnumerable<IModuleContextAccessor> moduleAccessors = null,
        IEnumerable<ActivitySource> activitySources = null)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());

        this.moduleAccessors = moduleAccessors;
        this.activitySources = activitySources;
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Handles .
    /// </summary>
    /// <param name="command">The command used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<CommandResponse> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var requestType = command.GetType().PrettyName();
        var handlerType = this.GetType().PrettyName();

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [CommandIdKey] = command.RequestId.ToString("N"),
            [CommandTypeKey] = requestType
        }))
        {
            try
            {
                EnsureArg.IsNotNull(command, nameof(command));

                // TODO: move the Activity Starting to the ModuleScopeCommandBehavior so the module can be added to the Activity (tracing)
                var module = this.moduleAccessors.Find(command.GetType());
                var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

                //return await this.activitySources.Find(moduleName)
                return await Activity.Current.StartActvity($"{Constants.TraceOperationHandleName} {requestType} [{moduleName}]",
                    async (a, c) =>
                    {
                        a?.AddEvent(new ActivityEvent(
                            $"processing (type={command.GetType().Name}, id={command.RequestId.ToString("N")}, handler={handlerType})"));
                        TypedLogger.LogProcessing(this.Logger, Constants.LogKey, requestType, command.RequestId.ToString("N"), handlerType, moduleName);

                        this.ValidateRequest(command);
                        var watch = ValueStopwatch.StartNew();
                        var response = await this.Process(command, cancellationToken).AnyContext();

                        if (response?.Cancelled == true || cancellationToken.IsCancellationRequested)
                        {
                            this.Logger.LogWarning("[{LogKey}] processing cancelled (type={CommandType}, id={CommandRequestId}, handler={CommandHandler}, reason={CommandCancelledReason})", Constants.LogKey, requestType, command.RequestId.ToString("N"), handlerType, response?.CancelledReason);
                            a?.AddEvent(new ActivityEvent(
                                $"processing cancelled (type={requestType}, id={command.RequestId.ToString("N")}, handler={handlerType}, reason={response?.CancelledReason})"));

                            if (cancellationToken.IsCancellationRequested)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }

                        TypedLogger.LogProcessed(this.Logger, Constants.LogKey, requestType, command.RequestId.ToString("N"), moduleName, watch.GetElapsedMilliseconds());

                        return response;
                    },
                    tags: new Dictionary<string, string>
                    {
                        ["command.module.origin"] = moduleName,
                        ["command.request_id"] = command.RequestId.ToString("N"),
                        ["command.request_type"] = requestType
                    },
                    baggages: new Dictionary<string, string>
                    {
                        ["command.id"] = command.RequestId.ToString("N"),
                        ["command.type"] = requestType,
                        [ActivityConstants.ModuleNameTagKey] = moduleName,
                    },
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "[{LogKey}] processing error (type={CommandType}, id={CommandRequestId}): {ErrorMessage}", Constants.LogKey, requestType, command.RequestId.ToString("N"), ex.Message);

                throw;
            }
        }
    }

    /// <summary>
    /// Executes the process operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task<CommandResponse> Process(TCommand request, CancellationToken cancellationToken);

    private void ValidateRequest(TCommand request)
    {
        this.Logger.LogDebug("[{LogKey}] validating (type={CommandType}, id={CommandRequestId})", Constants.LogKey, request.GetType().Name, request.RequestId);

        var validationResult = request.Validate();
        if (validationResult?.IsValid == false)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the processing operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="commandType">The command type used by the operation.</param>
        /// <param name="commandRequestId">The command request id used by the operation.</param>
        /// <param name="commandHandler">The command handler used by the operation.</param>
        /// <param name="moduleName">The module name used by the operation.</param>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] processing (type={CommandType}, id={CommandRequestId}, handler={CommandHandler}, module={ModuleName})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string commandType, string commandRequestId, string commandHandler, string moduleName);

        /// <summary>
        /// Writes a log entry for the processed operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="commandType">The command type used by the operation.</param>
        /// <param name="commandRequestId">The command request id used by the operation.</param>
        /// <param name="moduleName">The module name used by the operation.</param>
        /// <param name="timeElapsed">The time elapsed used by the operation.</param>
        [LoggerMessage(1, LogLevel.Information, "[{LogKey}] processed (type={CommandType}, id={CommandRequestId}, module={ModuleName}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string commandType, string commandRequestId, string moduleName, long timeElapsed);
    }
}

/// <summary>
/// Represents command handler base.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
/// <param name="moduleAccessors">The module accessors used by the operation.</param>
/// <param name="activitySources">The activity sources used by the operation.</param>
[Obsolete("Use the new Requester from now on")]
public abstract partial class CommandHandlerBase<TCommand, TResult>(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null,
    IEnumerable<ActivitySource> activitySources = null)
    : MediatR.IRequestHandler<TCommand, CommandResponse<TResult>>, ICommandRequestHandler
    where TCommand : class, ICommandRequest<CommandResponse<TResult>>
{
    private const string CommandIdKey = "CommandRequestId";
    private const string CommandTypeKey = "CommandType";

    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors = moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources = activitySources;

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; } = loggerFactory?.CreateLogger<CommandHandlerBase<TCommand, TResult>>() ??
        NullLoggerFactory.Instance.CreateLogger<CommandHandlerBase<TCommand, TResult>>();

    /// <summary>
    /// Handles .
    /// </summary>
    /// <param name="command">The command used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<CommandResponse<TResult>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var requestType = command.GetType().Name;
        var handlerType = this.GetType().Name;

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [CommandIdKey] = command.RequestId.ToString("N"),
            [CommandTypeKey] = requestType
        }))
        {
            try
            {
                EnsureArg.IsNotNull(command, nameof(command));

                // TODO: move the Activity Starting to the ModuleScopeCommandBehavior so the module can be added to the Activity (tracing)
                var module = this.moduleAccessors.Find(command.GetType());
                var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

                //return await this.activitySources.Find(moduleName)
                return await Activity.Current.StartActvity($"{Constants.TraceOperationHandleName} {requestType} [{moduleName}]",
                    async (a, c) =>
                    {
                        a?.AddEvent(new ActivityEvent(
                            $"processing (type={command.GetType().Name}, id={command.RequestId.ToString("N")}, handler={handlerType})"));
                        TypedLogger.LogProcessing(this.Logger, Constants.LogKey, command.GetType().Name, command.RequestId.ToString("N"), handlerType, moduleName);

                        this.ValidateRequest(command);
                        var watch = ValueStopwatch.StartNew();
                        var response = await this.Process(command, cancellationToken).AnyContext();

                        if (response?.Cancelled == true || cancellationToken.IsCancellationRequested)
                        {
                            this.Logger.LogWarning(
                                "[{LogKey}] processing cancelled (type={CommandType}, id={CommandRequestId}, handler={CommandHandler}, reason={CommandCancelledReason})",
                                Constants.LogKey,
                                command.GetType().Name,
                                command.RequestId,
                                handlerType,
                                response?.CancelledReason);
                            a?.AddEvent(new ActivityEvent(
                                $"processing cancelled (type={command.GetType().Name}, id={command.RequestId.ToString("N")}, handler={handlerType}, reason={response?.CancelledReason})"));

                            if (cancellationToken.IsCancellationRequested)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }

                        TypedLogger.LogProcessed(this.Logger,
                            Constants.LogKey,
                            command.GetType().Name,
                            command.RequestId.ToString("N"),
                            moduleName,
                            watch.GetElapsedMilliseconds());

                        return response;
                    },
                    tags: new Dictionary<string, string>
                    {
                        ["command.module.origin"] = moduleName,
                        ["command.request_id"] = command.RequestId.ToString("N"),
                        ["command.request_type"] = requestType
                    },
                    baggages: new Dictionary<string, string>
                    {
                        ["command.id"] = command.RequestId.ToString("N"),
                        ["command.type"] = requestType,
                        [ActivityConstants.ModuleNameTagKey] = moduleName,
                    },
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex,
                    "[{LogKey}] processing error (type={CommandType}, id={CommandRequestId}): {ErrorMessage}",
                    Constants.LogKey,
                    command.GetType().Name,
                    command.RequestId.ToString("N"),
                    ex.Message);

                throw;
            }
        }
    }

    /// <summary>
    /// Executes the failure operation.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="value">The value used by the operation.</param>
    /// <param name="messages">The messages used by the operation.</param>
    /// <param name="errorMessage">The error message used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    protected CommandResponse<Result<TValue>> Failure<TValue>(TValue value, string messages = null, string errorMessage = null)
    {
        return CommandResult.Failure(value);
    }

    /// <summary>
    /// Executes the process operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task<CommandResponse<TResult>> Process(TCommand request, CancellationToken cancellationToken);

    private void ValidateRequest(TCommand request)
    {
        this.Logger.LogDebug("[{LogKey}] validating (type={CommandType}, id={CommandRequestId})",
            Constants.LogKey,
            request.GetType().Name,
            request.RequestId.ToString("N"));

        var validationResult = request.Validate();
        if (validationResult?.IsValid == false)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the processing operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="commandType">The command type used by the operation.</param>
        /// <param name="commandRequestId">The command request id used by the operation.</param>
        /// <param name="commandHandler">The command handler used by the operation.</param>
        /// <param name="moduleName">The module name used by the operation.</param>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] processing (type={CommandType}, id={CommandRequestId}, handler={CommandHandler}, module={ModuleName})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string commandType, string commandRequestId, string commandHandler, string moduleName);

        /// <summary>
        /// Writes a log entry for the processed operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="commandType">The command type used by the operation.</param>
        /// <param name="commandRequestId">The command request id used by the operation.</param>
        /// <param name="moduleName">The module name used by the operation.</param>
        /// <param name="timeElapsed">The time elapsed used by the operation.</param>
        [LoggerMessage(1, LogLevel.Information, "[{LogKey}] processed (type={CommandType}, id={CommandRequestId}, module={ModuleName}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string commandType, string commandRequestId, string moduleName, long timeElapsed);
    }
}
