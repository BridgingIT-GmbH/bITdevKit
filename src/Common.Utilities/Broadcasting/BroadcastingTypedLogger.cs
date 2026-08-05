// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>Provides source-generated, credential- and payload-safe Broadcasting logs.</summary>
/// <example><code>BroadcastingTypedLogger.LogNodeUnregistered(logger, nodeIdentity);</code></example>
public static partial class BroadcastingTypedLogger
{
    /// <summary>Logs node registration.</summary>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "[{LogKey}] broadcast node registered (nodeIdentity={NodeIdentity}, scopeCount={ScopeCount})"
    )]
    public static partial void LogNodeRegistered(
        ILogger logger,
        string logKey,
        string nodeIdentity,
        int scopeCount
    );

    /// <summary>Logs node unregistration.</summary>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "[{LogKey}] broadcast node unregistered (nodeIdentity={NodeIdentity})"
    )]
    public static partial void LogNodeUnregistered(ILogger logger, string logKey, string nodeIdentity);

    /// <summary>Logs publication completion.</summary>
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "[{LogKey}] broadcast publication completed (type={BroadcastType}, targetCount={TargetCount}, acceptedCount={AcceptedCount}, failureCount={FailureCount})"
    )]
    public static partial void LogPublicationCompleted(
        ILogger logger,
        string logKey,
        string broadcastType,
        int targetCount,
        int acceptedCount,
        int failureCount
    );

    /// <summary>Logs one immediate delivery outcome.</summary>
    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] broadcast node delivery completed (type={BroadcastType}, nodeIdentity={NodeIdentity}, outcome={Outcome}, durationMs={DurationMs})"
    )]
    public static partial void LogDeliveryCompleted(
        ILogger logger,
        string logKey,
        string broadcastType,
        string nodeIdentity,
        string outcome,
        double durationMs
    );

    /// <summary>Logs a receiver rejection without payload or credential details.</summary>
    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] broadcast receiver outcome (type={BroadcastType}, outcome={Outcome})"
    )]
    public static partial void LogReceiverOutcome(
        ILogger logger,
        string logKey,
        string broadcastType,
        string outcome
    );

    /// <summary>Logs a handler failure without payload details.</summary>
    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "[{LogKey}] broadcast handler failed (type={BroadcastType})"
    )]
    public static partial void LogHandlerFailed(
        ILogger logger,
        string logKey,
        string broadcastType,
        Exception exception
    );

    /// <summary>Logs a registry operation failure without provider exception details.</summary>
    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "[{LogKey}] broadcast registry operation failed (operation={Operation})"
    )]
    public static partial void LogRegistryFailure(
        ILogger logger,
        string logKey,
        string operation,
        Exception exception
    );

    /// <summary>Logs publication start without payload content.</summary>
    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Debug,
        Message = "[{LogKey}] broadcast publication started (type={BroadcastType}, scopeCount={ScopeCount})"
    )]
    public static partial void LogPublicationStarted(
        ILogger logger,
        string logKey,
        string broadcastType,
        int scopeCount
    );

    /// <summary>Logs lease-expiry cleanup without node identities or addresses.</summary>
    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "[{LogKey}] broadcast registration leases expired (count={Count})"
    )]
    public static partial void LogRegistrationLeasesExpired(ILogger logger, string logKey, int count);

    /// <summary>Logs a dedicated transport-authentication rejection without credential data.</summary>
    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Warning,
        Message = "[{LogKey}] broadcast receiver authentication rejected"
    )]
    public static partial void LogAuthenticationRejected(ILogger logger, string logKey);

    /// <summary>Logs a configured delay before the initial node registration.</summary>
    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Information,
        Message = "[{LogKey}] broadcast node registration startup delayed (delayMs={DelayMs})"
    )]
    public static partial void LogRegistrationStartupDelayed(ILogger logger, string logKey, double delayMs);

    /// <summary>Logs the start of an optional database-readiness wait.</summary>
    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Information,
        Message = "[{LogKey}] broadcast node registration waiting for database readiness (name={DatabaseName}, timeoutSeconds={TimeoutSeconds})"
    )]
    public static partial void LogDatabaseReadinessWaiting(
        ILogger logger,
        string logKey,
        string databaseName,
        double timeoutSeconds);

    /// <summary>Logs completion of an optional database-readiness wait.</summary>
    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Information,
        Message = "[{LogKey}] broadcast node registration database ready (name={DatabaseName})"
    )]
    public static partial void LogDatabaseReadinessSatisfied(
        ILogger logger,
        string logKey,
        string databaseName);
}