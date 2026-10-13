// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

internal sealed partial class ProfilingLifecycleLogger(ILogger operationLogger, ILogger segmentLogger, OperationProfilingCaptureCounters counters)
{
    public void StartOperation(Guid id, string key, string kind, string correlationId)
    {
        try
        {
            if (operationLogger?.IsEnabled(LogLevel.Trace) == true)
            {
                OperationStarted(operationLogger, id, key, kind, correlationId);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref counters.LoggingFaults);
        }
    }

    public void StopOperation(OperationProfilingRecord record)
    {
        try
        {
            if (operationLogger?.IsEnabled(LogLevel.Trace) == true)
            {
                OperationStopped(operationLogger, record.Id, record.Key, record.Kind, record.Outcome.ToString(), record.Duration.TotalMilliseconds,
                    record.Failure?.Code, record.Failure?.ExceptionType);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref counters.LoggingFaults);
        }
    }

    public void StartSegment(Guid id, ProfilingSegmentPath path, long sequence)
    {
        try
        {
            if (segmentLogger?.IsEnabled(LogLevel.Trace) == true)
            {
                SegmentStarted(segmentLogger, id, FormatPath(path), sequence);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref counters.LoggingFaults);
        }
    }

    public void StopSegment(Guid id, ProfilingSegmentPath path, long sequence, ProfilingSegmentOutcome outcome, TimeSpan duration, ProfilingFailureDescriptor failure)
    {
        try
        {
            if (segmentLogger?.IsEnabled(LogLevel.Trace) == true)
            {
                SegmentStopped(segmentLogger, id, FormatPath(path), sequence, outcome.ToString(), duration.TotalMilliseconds, failure?.Code, failure?.ExceptionType);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref counters.LoggingFaults);
        }
    }

    private static string FormatPath(ProfilingSegmentPath path) => System.Text.Json.JsonSerializer.Serialize(path.Components);

    [LoggerMessage(EventId = 6101, EventName = "ProfilingOperationStarted", Level = LogLevel.Trace, SkipEnabledCheck = true,
        Message = "[Profiling] operation started (operationId={OperationId}, key={Key}, kind={Kind}, correlationId={CorrelationId})")]
    private static partial void OperationStarted(ILogger logger, Guid operationId, string key, string kind, string correlationId);

    [LoggerMessage(EventId = 6102, EventName = "ProfilingOperationStopped", Level = LogLevel.Trace, SkipEnabledCheck = true,
        Message = "[Profiling] operation stopped (operationId={OperationId}, key={Key}, kind={Kind}, outcome={Outcome}, durationMs={DurationMs}, code={Code}, type={Type})")]
    private static partial void OperationStopped(ILogger logger, Guid operationId, string key, string kind, string outcome, double durationMs, string code, string type);

    [LoggerMessage(EventId = 6103, EventName = "ProfilingSegmentStarted", Level = LogLevel.Trace, SkipEnabledCheck = true,
        Message = "[Profiling] segment started (operationId={OperationId}, path={Path}, invocationSequence={InvocationSequence})")]
    private static partial void SegmentStarted(ILogger logger, Guid operationId, string path, long invocationSequence);

    [LoggerMessage(EventId = 6104, EventName = "ProfilingSegmentStopped", Level = LogLevel.Trace, SkipEnabledCheck = true,
        Message = "[Profiling] segment stopped (operationId={OperationId}, path={Path}, invocationSequence={InvocationSequence}, outcome={Outcome}, durationMs={DurationMs}, code={Code}, type={Type})")]
    private static partial void SegmentStopped(ILogger logger, Guid operationId, string path, long invocationSequence, string outcome, double durationMs, string code, string type);
}
