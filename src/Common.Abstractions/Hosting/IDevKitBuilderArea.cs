namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a typed configuration area that is backed by the root DevKit application builder.
/// </summary>
/// <example>
/// <code>
/// public sealed class MessagingDevKitBuilder(IDevKitApplicationBuilder application) : IDevKitBuilderArea
/// {
///     public IDevKitApplicationBuilder Application { get; } = application;
/// }
/// </code>
/// </example>
public interface IDevKitBuilderArea
{
    /// <summary>
    /// Gets the root DevKit application builder.
    /// </summary>
    IDevKitApplicationBuilder Application { get; }
}