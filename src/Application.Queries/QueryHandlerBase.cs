// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Represents query handler base.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
[Obsolete("Use the new Requester from now on")]
public abstract partial class QueryHandlerBase<TQuery, TResult>
    : MediatR.IRequestHandler<TQuery, QueryResponse<TResult>>, IQueryHandler
    where TQuery : class, IQueryRequest<QueryResponse<TResult>>
{
    private const string QueryIdKey = "QueryRequestId";
    private const string QueryTypeKey = "QueryType";

    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources;

    /// <summary>
    /// Initializes a new instance of the <c>QueryHandlerBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="moduleAccessors">The module accessors used by the operation.</param>
    /// <param name="activitySources">The activity sources used by the operation.</param>
    protected QueryHandlerBase(
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
    /// <param name="query">The query used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<QueryResponse<TResult>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        var requestType = query.GetType().PrettyName();
        var handlerType = this.GetType().PrettyName();

        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            [QueryIdKey] = query.RequestId.ToString("N"),
            [QueryTypeKey] = requestType
        }))
        {
            try
            {
                EnsureArg.IsNotNull(query, nameof(query));

                // TODO: move the Activity Starting to the ModuleScopeQueryBehavior so the module can be added to the Activity (tracing)
                var module = this.moduleAccessors.Find(query.GetType());
                var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

                //return await this.activitySources.Find(moduleName)
                return await Activity.Current.StartActvity($"{Constants.TraceOperationHandleName} {requestType} [{moduleName}]",
                    async (a, c) =>
                    {
                        a?.AddEvent(new ActivityEvent(
                            $"processing (type={requestType}, id={query.RequestId.ToString("N")}, handler={handlerType})"));
                        TypedLogger.LogProcessing(this.Logger, Constants.LogKey, requestType, query.RequestId.ToString("N"), handlerType, moduleName);

                        this.ValidateRequest(query);
                        var watch = ValueStopwatch.StartNew();
                        var response = await this.Process(query, cancellationToken).AnyContext();

                        if (response?.Cancelled == true || cancellationToken.IsCancellationRequested)
                        {
                            this.Logger.LogWarning(
                                "[{LogKey}] processing cancelled (type={QueryType}, id={QueryRequestId}, handler={QueryHandler}, reason={QueryCancelledReason})",
                                Constants.LogKey,
                                requestType,
                                query.RequestId.ToString("N"),
                                handlerType,
                                response?.CancelledReason);
                            a?.AddEvent(new ActivityEvent(
                                $"processing cancelled (type={requestType}, id={query.RequestId.ToString("N")}, handler={handlerType}, reason={response?.CancelledReason})"));

                            if (cancellationToken.IsCancellationRequested)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }

                        TypedLogger.LogProcessed(this.Logger, Constants.LogKey, requestType, query.RequestId.ToString("N"), moduleName, watch.GetElapsedMilliseconds());

                        return response;
                    },
                    tags: new Dictionary<string, string>
                    {
                        ["query.module.origin"] = moduleName,
                        ["query.request_id"] = query.RequestId.ToString("N"),
                        ["query.request_type"] = requestType
                    },
                    baggages: new Dictionary<string, string>
                    {
                        ["query.id"] = query.RequestId.ToString("N"),
                        ["query.type"] = requestType,
                        [ActivityConstants.ModuleNameTagKey] = moduleName,
                    },
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex,
                    "[{LogKey}] processing error (type={QueryType}, id={QueryRequestId}): {ErrorMessage}",
                    Constants.LogKey,
                    requestType,
                    query.RequestId.ToString("N"),
                    ex.Message);

                throw;
            }
        }
    }

    /// <summary>
    /// Executes the process operation.
    /// </summary>
    /// <param name="query">The query used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task<QueryResponse<TResult>> Process(TQuery query, CancellationToken cancellationToken);

    private void ValidateRequest(TQuery request)
    {
        // TODO: use typed logger
        this.Logger.LogDebug("[{LogKey}] validating (type={QueryType}, id={QueryRequestId}, handler={QueryHandler})",
            Constants.LogKey,
            request.GetType().Name,
            request.RequestId.ToString("N"),
            this.GetType().Name);

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
        /// <param name="queryType">The query type used by the operation.</param>
        /// <param name="queryRequestId">The query request id used by the operation.</param>
        /// <param name="queryHandler">The query handler used by the operation.</param>
        /// <param name="moduleName">The module name used by the operation.</param>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] processing (type={QueryType}, id={QueryRequestId}, handler={QueryHandler}, module={ModuleName})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string queryType, string queryRequestId, string queryHandler, string moduleName);

        /// <summary>
        /// Writes a log entry for the processed operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="queryType">The query type used by the operation.</param>
        /// <param name="queryRequestId">The query request id used by the operation.</param>
        /// <param name="moduleName">The module name used by the operation.</param>
        /// <param name="timeElapsed">The time elapsed used by the operation.</param>
        [LoggerMessage(1,
            LogLevel.Information,
            "[{LogKey}] processed (type={QueryType}, id={QueryRequestId}, module={ModuleName}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string queryType, string queryRequestId, string moduleName, long timeElapsed);
    }
}
