// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

internal static partial class PipelineTypedLogger
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] pipeline started (pipeline={PipelineName}, executionId={ExecutionId}, correlationId={CorrelationId})")]
    public static partial void LogPipelineStarted(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        string correlationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] pipeline finished (pipeline={PipelineName}, executionId={ExecutionId}, status={Status}) -> took {DurationMs}ms")]
    public static partial void LogPipelineFinished(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        PipelineExecutionStatus status,
        double durationMs);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] step started (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId})")]
    public static partial void LogStepStarted(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        Guid executionId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] step finished (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId}, outcome={Outcome}) -> took {DurationMs}ms")]
    public static partial void LogStepFinished(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        Guid executionId,
        PipelineControlOutcome outcome,
        double durationMs);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "[{LogKey}] step retry requested (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId}, attempt={Attempt}, maxAttempts={MaxAttempts}, message={Message})")]
    public static partial void LogStepRetrying(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        Guid executionId,
        int attempt,
        int maxAttempts,
        string message);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "[{LogKey}] step exception (pipeline={PipelineName}, step={StepName}, executionId={ExecutionId})")]
    public static partial void LogStepException(
        ILogger logger,
        string logKey,
        string pipelineName,
        string stepName,
        Guid executionId,
        Exception exception);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "[{LogKey}] pipeline exception (pipeline={PipelineName}, executionId={ExecutionId})")]
    public static partial void LogPipelineException(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        Exception exception);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "[{LogKey}] hook failure ignored (pipeline={PipelineName}, executionId={ExecutionId})")]
    public static partial void LogHookFailure(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        Exception exception);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Error,
        Message = "[{LogKey}] completion callback failed (pipeline={PipelineName}, executionId={ExecutionId})")]
    public static partial void LogCompletionCallbackFailed(
        ILogger logger,
        string logKey,
        string pipelineName,
        Guid executionId,
        Exception exception);
}
