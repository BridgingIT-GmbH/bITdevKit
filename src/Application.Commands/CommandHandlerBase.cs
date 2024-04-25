// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using EnsureThat;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

public abstract partial class CommandHandlerBase<TCommand>
    : IRequestHandler<TCommand, CommandResponse>, ICommandRequestHandler
    where TCommand : class, ICommandRequest<CommandResponse>
{
    private const string CommandIdKey = "CommandId";
    private const string CommandTypeKey = "CommandType";

    protected CommandHandlerBase(ILoggerFactory loggerFactory)
    {
        EnsureArg.IsNotNull(loggerFactory, nameof(loggerFactory));

        this.Logger = loggerFactory.CreateLogger(this.GetType());
    }

    protected ILogger Logger { get; }

    public virtual async Task<CommandResponse> Handle(
        TCommand command,
        CancellationToken cancellationToken)
    {
        var requestType = command.GetType().Name;
        var handlerType = this.GetType().Name;

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [CommandIdKey] = command.Id,
            [CommandTypeKey] = requestType,
        }))
        {
            try
            {
                EnsureArg.IsNotNull(command, nameof(command));

                return await Activity.Current.StartActvity(
                    $"{Constants.TraceOperationProcessName} {requestType}",
                    async (a, c) =>
                    {
                        a?.AddEvent(new($"processing (type={command.GetType().Name}, id={command.Id}, handler={handlerType})"));
                        TypedLogger.LogProcessing(this.Logger, Constants.LogKey, requestType, command.Id, handlerType);

                        this.ValidateRequest(command);
                        var watch = ValueStopwatch.StartNew();
                        var response = await this.Process(command, cancellationToken).AnyContext();

                        if (response?.Cancelled == true || cancellationToken.IsCancellationRequested)
                        {
                            this.Logger.LogWarning("{LogKey} processing cancelled (type={CommandType}, id={CommandId}, handler={CommandHandler}, reason={CommandCancelledReason})", Constants.LogKey, requestType, command.Id, handlerType, response?.CancelledReason);
                            a?.AddEvent(new($"processing cancelled (type={requestType}, id={command.Id}, handler={handlerType}, reason={response?.CancelledReason})"));

                            if (cancellationToken.IsCancellationRequested)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }

                        TypedLogger.LogProcessed(this.Logger, Constants.LogKey, requestType, command.Id, watch.GetElapsedMilliseconds());

                        return response;
                    },
                    baggages: new Dictionary<string, string> { ["command.id"] = command.Id, ["command.type"] = requestType },
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "{LogKey} processing error (type={CommandType}, id={CommandId}): {ErrorMessage}", Constants.LogKey, requestType, command.Id, ex.Message);
                throw;
            }
        }
    }

    public abstract Task<CommandResponse> Process(
        TCommand request,
        CancellationToken cancellationToken);

    private void ValidateRequest(TCommand request)
    {
        this.Logger.LogDebug("{LogKey} validating (type={CommandType}, id={CommandId})", Constants.LogKey, request.GetType().Name, request.Id);

        var validationResult = request.Validate();
        if (validationResult?.IsValid == false)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} processing (type={CommandType}, id={CommandId}, handler={CommandHandler})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string commandType, string commandId, string commandHandler);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} processed (type={CommandType}, id={CommandId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string commandType, string commandId, long timeElapsed);
    }
}

public abstract partial class CommandHandlerBase<TCommand, TResult>
    : IRequestHandler<TCommand, CommandResponse<TResult>>, ICommandRequestHandler
    where TCommand : class, ICommandRequest<CommandResponse<TResult>>
{
    private const string CommandIdKey = "CommandId";
    private const string CommandTypeKey = "CommandType";

    protected CommandHandlerBase(ILoggerFactory loggerFactory)
    {
        EnsureArg.IsNotNull(loggerFactory, nameof(loggerFactory));

        this.Logger = loggerFactory.CreateLogger(this.GetType());
    }

    protected ILogger Logger { get; }

    public virtual async Task<CommandResponse<TResult>> Handle(
        TCommand command,
        CancellationToken cancellationToken)
    {
        var requestType = command.GetType().Name;
        var handlerType = this.GetType().Name;

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [CommandIdKey] = command.Id,
            [CommandTypeKey] = requestType,
        }))
        {
            try
            {
                EnsureArg.IsNotNull(command, nameof(command));

                return await Activity.Current.StartActvity(
                    $"{Constants.TraceOperationProcessName} {requestType}",
                    async (a, c) =>
                    {
                        a?.AddEvent(new($"processing (type={command.GetType().Name}, id={command.Id}, handler={handlerType})"));
                        TypedLogger.LogProcessing(this.Logger, Constants.LogKey, command.GetType().Name, command.Id, handlerType);

                        this.ValidateRequest(command);
                        var watch = ValueStopwatch.StartNew();
                        var response = await this.Process(command, cancellationToken).AnyContext();

                        if (response?.Cancelled == true || cancellationToken.IsCancellationRequested)
                        {
                            this.Logger.LogWarning("{LogKey} processing cancelled (type={CommandType}, id={CommandId}, handler={CommandHandler}, reason={CommandCancelledReason})", Constants.LogKey, command.GetType().Name, command.Id, handlerType, response?.CancelledReason);
                            a?.AddEvent(new($"processing cancelled (type={command.GetType().Name}, id={command.Id}, handler={handlerType}, reason={response?.CancelledReason})"));

                            if (cancellationToken.IsCancellationRequested)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }

                        TypedLogger.LogProcessed(this.Logger, Constants.LogKey, command.GetType().Name, command.Id, watch.GetElapsedMilliseconds());

                        return response;
                    },
                    baggages: new Dictionary<string, string> { ["command.id"] = command.Id, ["command.type"] = requestType },
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "{LogKey} processing error (type={CommandType}, id={CommandId}): {ErrorMessage}", Constants.LogKey, command.GetType().Name, command.Id, ex.Message);
                throw;
            }
        }
    }

    public abstract Task<CommandResponse<TResult>> Process(
        TCommand request,
        CancellationToken cancellationToken);

    private void ValidateRequest(TCommand request)
    {
        this.Logger.LogDebug("{LogKey} validating (type={CommandType}, id={CommandId})", Constants.LogKey, request.GetType().Name, request.Id);

        var validationResult = request.Validate();
        if (validationResult?.IsValid == false)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} processing (type={CommandType}, id={CommandId}, handler={CommandHandler})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string commandType, string commandId, string commandHandler);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} processed (type={CommandType}, id={CommandId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string commandType, string commandId, long timeElapsed);
    }
}