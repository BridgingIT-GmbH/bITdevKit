// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
/// Provides ambient access to the application correlation identifier for the current execution flow.
/// </summary>
/// <remarks>
/// The correlation identifier is independent from the distributed tracing
/// <see cref="ActivityTraceId"/>. An explicitly established scope takes precedence over the
/// correlation value stored in the current activity baggage.
/// </remarks>
/// <example>
/// <code>
/// using (CorrelationId.BeginScope("order-123"))
/// {
///     logger.LogInformation("Processing correlation {CorrelationId}", CorrelationId.Current);
/// }
/// </code>
/// </example>
public static class CorrelationId
{
    private static readonly AsyncLocal<string> current = new();

    /// <summary>
    /// Gets the maximum supported correlation identifier length.
    /// </summary>
    /// <example><code>var maximumLength = CorrelationId.MaximumLength;</code></example>
    public const int MaximumLength = 128;

    /// <summary>
    /// Gets the conventional HTTP header, query, and context-item name for a correlation identifier.
    /// </summary>
    /// <example><code>request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, value);</code></example>
    public const string HeaderName = "CorrelationId";

    /// <summary>
    /// Gets the OpenTelemetry activity baggage name for a correlation identifier.
    /// </summary>
    /// <example><code>activity.SetBaggage(CorrelationId.ActivityBaggageName, value);</code></example>
    public const string ActivityBaggageName = "correlation_id";

    /// <summary>
    /// Gets the correlation identifier for the current asynchronous execution flow.
    /// </summary>
    /// <value>
    /// The explicitly scoped value, the current activity baggage value, or <see langword="null"/>
    /// when no correlation identifier is available.
    /// </value>
    /// <example><code>var correlationId = CorrelationId.Current;</code></example>
    public static string Current =>
        current.Value ?? Activity.Current?.GetBaggageItem(ActivityBaggageName);

    /// <summary>
    /// Establishes a correlation identifier for the current asynchronous execution flow.
    /// </summary>
    /// <param name="value">The correlation identifier, or <see langword="null"/> to clear it in the scope.</param>
    /// <returns>A scope that restores the previous ambient value when disposed.</returns>
    /// <example><code>using var scope = CorrelationId.BeginScope(message.CorrelationId);</code></example>
    public static IDisposable BeginScope(string value)
    {
        var previous = current.Value;
        current.Value = value;
        return new CorrelationIdScope(previous);
    }

    /// <summary>
    /// Determines whether a value is a supported correlation identifier.
    /// </summary>
    /// <remarks>
    /// A valid value contains between 1 and 128 ASCII letters, digits, hyphens, underscores,
    /// periods, or colons.
    /// </remarks>
    /// <param name="value">The value to validate.</param>
    /// <returns><see langword="true"/> when the value is a supported correlation identifier.</returns>
    /// <example><code>var valid = CorrelationId.IsValid("order-123");</code></example>
    public static bool IsValid(string value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaximumLength
        && value.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.' or ':');

    private sealed class CorrelationIdScope(string previous) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            current.Value = previous;
            this.disposed = true;
        }
    }
}
