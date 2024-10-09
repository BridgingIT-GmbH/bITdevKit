// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;

public abstract partial class QueryHandlerBase<TQuery, TResult>
    : IRequestHandler<TQuery, QueryResponse<TResult>>, IQueryHandler
    where TQuery : class, IQueryRequest<QueryResponse<TResult>>
{
    private const string QueryIdKey = "QueryRequestId";
    private const string QueryTypeKey = "QueryType";

    protected QueryHandlerBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    protected ILogger Logger { get; }

    public virtual async Task<QueryResponse<TResult>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        var requestType = query.GetType().PrettyName();
        var handlerType = this.GetType().PrettyName();

        using (this.Logger.BeginScope(new Dictionary<string, object>
               {
                   [QueryIdKey] = query.RequestId, [QueryTypeKey] = query.GetType().Name
               }))
        {
            try
            {
                EnsureArg.IsNotNull(query, nameof(query));

                return await Activity.Current.StartActvity($"{Constants.TraceOperationProcessName} {requestType}",
                    async (a, c) =>
                    {
                        a?.AddEvent(new ActivityEvent(
                            $"processing (type={requestType}, id={query.RequestId}, handler={handlerType})"));
                        TypedLogger.LogProcessing(this.Logger,
                            Constants.LogKey,
                            requestType,
                            query.RequestId,
                            handlerType);

                        this.ValidateRequest(query);
                        var watch = ValueStopwatch.StartNew();
                        var response = await this.Process(query, cancellationToken).AnyContext();

                        if (response?.Cancelled == true || cancellationToken.IsCancellationRequested)
                        {
                            this.Logger.LogWarning(
                                "{LogKey} processing cancelled (type={QueryType}, id={QueryRequestId}, handler={QueryHandler}, reason={QueryCancelledReason})",
                                Constants.LogKey,
                                requestType,
                                query.RequestId,
                                handlerType,
                                response?.CancelledReason);
                            a?.AddEvent(new ActivityEvent(
                                $"processing cancelled (type={requestType}, id={query.RequestId}, handler={handlerType}, reason={response?.CancelledReason})"));

                            if (cancellationToken.IsCancellationRequested)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }

                        TypedLogger.LogProcessed(this.Logger,
                            Constants.LogKey,
                            requestType,
                            query.RequestId,
                            watch.GetElapsedMilliseconds());

                        return response;
                    },
                    baggages: new Dictionary<string, string>
                    {
                        ["command.id"] = query.RequestId.ToString("N"), ["command.type"] = requestType
                    },
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex,
                    "{LogKey} processing error (type={QueryType}, id={QueryRequestId}): {ErrorMessage}",
                    Constants.LogKey,
                    requestType,
                    query.RequestId,
                    ex.Message);

                throw;
            }
        }
    }

    public abstract Task<QueryResponse<TResult>> Process(TQuery query, CancellationToken cancellationToken);

    private void ValidateRequest(TQuery request)
    {
        this.Logger.LogDebug("{LogKey} validating (type={QueryType}, id={QueryRequestId}, handler={QueryHandler})",
            Constants.LogKey,
            request.GetType().Name,
            request.RequestId,
            this.GetType().Name);

        var validationResult = request.Validate();
        if (validationResult?.IsValid == false)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0,
            LogLevel.Information,
            "{LogKey} processing (type={QueryType}, id={QueryRequestId}, handler={QueryHandler})")]
        public static partial void LogProcessing(
            ILogger logger,
            string logKey,
            string queryType,
            Guid queryRequestId,
            string queryHandler);

        [LoggerMessage(1,
            LogLevel.Information,
            "{LogKey} processed (type={QueryType}, id={QueryRequestId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(
            ILogger logger,
            string logKey,
            string queryType,
            Guid queryRequestId,
            long timeElapsed);
    }
}