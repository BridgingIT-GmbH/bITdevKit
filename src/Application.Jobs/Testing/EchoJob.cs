// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides a small reusable test job that echoes typed input into the execution context.
/// </summary>
/// <example>
/// <code>
/// var result = await JobTestHarness.Create&lt;EchoJob, EchoJobData&gt;()
///     .WithData(new EchoJobData { Message = "hello" })
///     .ExecuteAsync();
/// </code>
/// </example>
public sealed class EchoJob : JobBase<EchoJobData>
{
    /// <summary>
    /// Executes the echo job and records the message in the execution context.
    /// </summary>
    /// <param name="context">The typed execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The successful execution result.</returns>
    public override Task<Result> ExecuteAsync(IJobExecutionContext<EchoJobData> context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = string.IsNullOrWhiteSpace(context.Data?.Message)
            ? "echo"
            : context.Data.Message.Trim();

        context.Messages.Add(message);
        context.Items["echo.message"] = message;
        context.Items["echo.correlationId"] = context.CorrelationId;

        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// Represents the typed payload for <see cref="EchoJob"/>.
/// </summary>
/// <example>
/// <code>
/// var data = new EchoJobData
/// {
///     Message = "hello"
/// };
/// </code>
/// </example>
public sealed class EchoJobData
{
    /// <summary>
    /// Gets or sets the message to echo into the execution context.
    /// </summary>
    public string Message { get; set; }
}