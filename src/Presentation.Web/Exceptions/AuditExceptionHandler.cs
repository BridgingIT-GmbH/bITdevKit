// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler that audits exceptions with user and request context information.
///     Logs detailed audit information including user identity, request details, and
///     exception context for compliance and security monitoring purposes.
/// </summary>
/// <remarks>
///     This handler should be registered with high priority to ensure it captures
///     exception context before other handlers process the exception.
///     It does not prevent exception propagation to other handlers.
///     The <see cref="ICurrentUserAccessor" /> is optional and may be null.
/// </remarks>
public class AuditExceptionHandler(
    ILogger<AuditExceptionHandler> logger,
    GlobalExceptionHandlerOptions options,
    ICurrentUserAccessor currentUserAccessor = null)
    : ExceptionHandlerBase<Exception>(logger, options)
{
    private readonly ICurrentUserAccessor currentUserAccessor = currentUserAccessor;

    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status500InternalServerError;

    /// <inheritdoc />
    protected override string Title => "An error occurred";

    /// <inheritdoc />
    protected override string GetDetail(Exception exception)
    {
        return exception.Message;
    }

    /// <summary>
    ///     Attempts to handle the exception by auditing it and then delegating to the next handler.
    /// </summary>
    public override async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Audit the exception
        this.AuditException(httpContext, exception);

        // Return false to allow other handlers to process the exception
        return false;
    }

    /// <summary>
    ///     Audits the exception with comprehensive context information.
    /// </summary>
    private void AuditException(HttpContext httpContext, Exception exception)
    {
        var userInfo = this.GetUserAuditInfo();
        var requestInfo = this.GetRequestAuditInfo(httpContext);
        var exceptionInfo = this.GetExceptionAuditInfo(exception);

        this.Logger?.LogWarning(
            exception,
            "AUDIT: Exception occurred - " +
            "TraceId: {TraceId}, " +
            "UserId: {UserId}, " +
            "UserName: {UserName}, " +
            "IsAuthenticated: {IsAuthenticated}, " +
            "Method: {Method}, " +
            "Path: {Path}, " +
            "RemoteIp: {RemoteIp}, " +
            "StatusCode: {StatusCode}, " +
            "ExceptionType: {ExceptionType}",
            httpContext.TraceIdentifier,
            userInfo.UserId,
            userInfo.UserName,
            userInfo.IsAuthenticated,
            requestInfo.Method,
            requestInfo.Path,
            requestInfo.RemoteIpAddress,
            httpContext.Response.StatusCode,
            exceptionInfo.ExceptionType);
    }

    /// <summary>
    ///     Extracts user audit information from the current user accessor.
    ///     If <see cref="ICurrentUserAccessor" /> is not registered, returns anonymous user info.
    /// </summary>
    private UserAuditInfo GetUserAuditInfo()
    {
        if (this.currentUserAccessor is null)
        {
            return new UserAuditInfo
            {
                UserId = "Unknown",
                UserName = "Unknown",
                Email = null,
                IsAuthenticated = false,
                Roles = Array.Empty<string>(),
                ClaimCount = 0,
                AccessorRegistered = false
            };
        }

        return new UserAuditInfo
        {
            UserId = this.currentUserAccessor.UserId ?? "Anonymous",
            UserName = this.currentUserAccessor.UserName ?? "Anonymous",
            Email = this.currentUserAccessor.Email,
            IsAuthenticated = this.currentUserAccessor.IsAuthenticated,
            Roles = this.currentUserAccessor.Roles ?? Array.Empty<string>(),
            ClaimCount = this.currentUserAccessor.Principal?.Claims.Count() ?? 0,
            AccessorRegistered = true
        };
    }

    /// <summary>
    ///     Extracts request audit information.
    /// </summary>
    private RequestAuditInfo GetRequestAuditInfo(HttpContext httpContext)
    {
        var request = httpContext.Request;

        return new RequestAuditInfo
        {
            Method = request.Method,
            Path = request.Path.Value,
            QueryString = request.QueryString.Value,
            Scheme = request.Scheme,
            Host = request.Host.Value,
            ContentType = request.ContentType,
            ContentLength = request.ContentLength,
            RemoteIpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = request.Headers.UserAgent.ToString(),
            Referer = request.Headers.Referer.ToString(),
            IsHttps = request.IsHttps,
            Protocol = request.Protocol
        };
    }

    /// <summary>
    ///     Extracts exception audit information.
    /// </summary>
    private ExceptionAuditInfo GetExceptionAuditInfo(Exception exception)
    {
        return new ExceptionAuditInfo
        {
            ExceptionType = exception.GetType().FullName,
            Message = exception.Message,
            Source = exception.Source,
            StackTraceLength = exception.StackTrace?.Length ?? 0,
            InnerExceptionType = exception.InnerException?.GetType().FullName,
            InnerExceptionMessage = exception.InnerException?.Message,
            HResult = exception.HResult,
            Data = this.GetExceptionData(exception)
        };
    }

    /// <summary>
    ///     Extracts custom data from the exception's Data dictionary.
    /// </summary>
    private Dictionary<string, object> GetExceptionData(Exception exception)
    {
        var data = new Dictionary<string, object>();

        if (exception.Data.Count > 0)
        {
            foreach (var key in exception.Data.Keys)
            {
                try
                {
                    var value = exception.Data[key];
                    data[key.ToString()] = value ?? "null";
                }
                catch
                {
                    data[key.ToString()] = "[Error accessing data]";
                }
            }
        }

        return data;
    }

    /// <inheritdoc />
    protected override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        Exception exception)
    {
        // Not used as we return false from TryHandleAsync,
        // but required by abstract base class
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Represents user audit information.
    /// </summary>
    private record UserAuditInfo
    {
        public string UserId { get; init; }

        public string UserName { get; init; }

        public string Email { get; init; }

        public bool IsAuthenticated { get; init; }

        public string[] Roles { get; init; }

        public int ClaimCount { get; init; }

        public bool AccessorRegistered { get; init; }
    }

    /// <summary>
    ///     Represents request audit information.
    /// </summary>
    private record RequestAuditInfo
    {
        public string Method { get; init; }

        public string Path { get; init; }

        public string QueryString { get; init; }

        public string Scheme { get; init; }

        public string Host { get; init; }

        public string ContentType { get; init; }

        public long? ContentLength { get; init; }

        public string RemoteIpAddress { get; init; }

        public string UserAgent { get; init; }

        public string Referer { get; init; }

        public bool IsHttps { get; init; }

        public string Protocol { get; init; }
    }

    /// <summary>
    ///     Represents exception audit information.
    /// </summary>
    private record ExceptionAuditInfo
    {
        public string ExceptionType { get; init; }

        public string Message { get; init; }

        public string Source { get; init; }

        public int StackTraceLength { get; init; }

        public string InnerExceptionType { get; init; }

        public string InnerExceptionMessage { get; init; }

        public int HResult { get; init; }

        public Dictionary<string, object> Data { get; init; }
    }
}