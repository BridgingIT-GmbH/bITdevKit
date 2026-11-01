// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Extends review suppression through deferred response rendering and streaming.</summary>
/// <example><code>return new AuditSuppressedResult(Results.Ok(model));</code></example>
public sealed class AuditSuppressedResult(IResult inner) : IResult
{
    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext context)
    {
        using var suppressed = context.RequestServices.GetRequiredService<IAuditContext>().Suppress();
        await inner.ExecuteAsync(context).ConfigureAwait(false);
    }
}
