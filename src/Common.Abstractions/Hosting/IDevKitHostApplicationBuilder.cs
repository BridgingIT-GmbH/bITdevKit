namespace BridgingIT.DevKit.Common;

/// <summary>
/// Marks DevKit application builders that wrap the generic host application model.
/// </summary>
/// <example>
/// <code>
/// public static TBuilder AddWorkerDefaults&lt;TBuilder&gt;(this TBuilder builder)
///     where TBuilder : IDevKitHostApplicationBuilder
/// {
///     return builder;
/// }
/// </code>
/// </example>
public interface IDevKitHostApplicationBuilder : IDevKitApplicationBuilder
{
}