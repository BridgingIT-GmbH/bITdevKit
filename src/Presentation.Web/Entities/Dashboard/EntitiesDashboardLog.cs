// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using Microsoft.Extensions.Logging;

/// <summary>Structured log messages of the Entities dashboard. Property values are never logged.</summary>
internal static partial class EntitiesDashboardLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[EntitiesDashboard] entity created (entity={EntityType}, id={EntityId}, user={UserName})")]
    public static partial void Created(ILogger logger, string entityType, string entityId, string userName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "[EntitiesDashboard] entity updated (entity={EntityType}, id={EntityId}, user={UserName})")]
    public static partial void Updated(ILogger logger, string entityType, string entityId, string userName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "[EntitiesDashboard] entity deleted (entity={EntityType}, id={EntityId}, user={UserName})")]
    public static partial void Deleted(ILogger logger, string entityType, string entityId, string userName);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "[EntitiesDashboard] bulk delete completed (entity={EntityType}, deleted={Deleted}, failed={Failed}, user={UserName})")]
    public static partial void BulkDeleted(ILogger logger, string entityType, int deleted, int failed, string userName);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "[EntitiesDashboard] export completed (entity={EntityType}, format={Format}, rows={Rows}, truncated={Truncated}, user={UserName})")]
    public static partial void Exported(ILogger logger, string entityType, string format, int rows, bool truncated, string userName);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "[EntitiesDashboard] operation failed (entity={EntityType}, operation={Operation}, correlationId={CorrelationId})")]
    public static partial void Failed(ILogger logger, Exception exception, string entityType, string operation, string correlationId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "[EntitiesDashboard] operation failed (entity={EntityType}, operation={Operation},     correlationId={CorrelationId}, error={ErrorType}, message={ErrorMessage})")]
        public static partial void FailedResult(ILogger logger, string entityType, string operation, string correlationId, string errorType, string errorMessage);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "[EntitiesDashboard] edit denied (path={Path}, user={UserName})")]
    public static partial void EditDenied(ILogger logger, string path, string userName);
}
