// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Text.Json;

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
    private static readonly AsyncLocal<CorrelationIdScope> currentScope = new();

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
        currentScope.Value is { } scope
            ? scope.Value
            : Activity.Current?.GetBaggageItem(ActivityBaggageName);

    /// <summary>
    /// Reads a valid correlation identifier from transport metadata.
    /// </summary>
    /// <remarks>
    /// Accepts a CLR string or a JSON string restored by a transport serializer. Other values are
    /// ignored without calling their <see cref="object.ToString"/> method.
    /// </remarks>
    /// <param name="properties">The transport properties, or <see langword="null"/>.</param>
    /// <returns>The supported correlation identifier, or <see langword="null"/> when absent or invalid.</returns>
    /// <example><code>var correlationId = CorrelationId.ReadFrom(message.Properties);</code></example>
    public static string ReadFrom(IDictionary<string, object> properties)
    {
        if (properties?.TryGetValue(HeaderName, out var value) != true)
        {
            return null;
        }

        string text;
        try
        {
            text = value switch
            {
                string identifier => identifier,
                JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
                _ => null,
            };
        }
        catch (ObjectDisposedException)
        {
            return null;
        }

        return IsValid(text) ? text : null;
    }

    /// <summary>
    /// Copies the current application correlation identifier into transport metadata before publishing.
    /// </summary>
    /// <remarks>
    /// Preserves a valid explicit transport identifier. Does not generate an identifier when the
    /// origin has none and does not replace it with a tracing identifier. JSON strings are normalized
    /// to CLR strings so native transport property collections can carry them when republishing.
    /// </remarks>
    /// <param name="properties">The mutable transport properties, or <see langword="null"/>.</param>
    /// <example><code>CorrelationId.PropagateTo(message.Properties);</code></example>
    public static void PropagateTo(IDictionary<string, object> properties)
    {
        if (properties is null)
        {
            return;
        }

        var explicitIdentifier = ReadFrom(properties);
        if (explicitIdentifier is not null)
        {
            if (properties[HeaderName] is JsonElement)
            {
                properties[HeaderName] = explicitIdentifier;
            }

            return;
        }

        var identifier = Current;
        if (IsValid(identifier))
        {
            properties[HeaderName] = identifier;
        }
    }

    /// <summary>
    /// Establishes a correlation identifier for the current asynchronous execution flow.
    /// </summary>
    /// <remarks>A null scope suppresses Activity baggage until the scope is disposed.</remarks>
    /// <param name="value">The correlation identifier, or <see langword="null"/> to clear it in the scope.</param>
    /// <returns>A scope that restores the previous ambient value when disposed.</returns>
    /// <example><code>using var scope = CorrelationId.BeginScope(CorrelationId.ReadFrom(message.Properties));</code></example>
    public static IDisposable BeginScope(string value)
    {
        var scope = new CorrelationIdScope(value, currentScope.Value);
        currentScope.Value = scope;
        return scope;
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
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':'
        );

    private sealed class CorrelationIdScope(string value, CorrelationIdScope previous) : IDisposable
    {
        private bool disposed;

        /// <summary>Gets the explicitly scoped value, including a deliberate empty correlation context.</summary>
        /// <example><code>var identifier = scope.Value;</code></example>
        public string Value { get; } = value;

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            currentScope.Value = previous;
            this.disposed = true;
        }
    }
}
