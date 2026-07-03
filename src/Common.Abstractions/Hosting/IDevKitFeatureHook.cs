namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a feature-owned hook that can participate in DevKit application builder stages.
/// </summary>
/// <example>
/// <code>
/// public sealed class MessagingFeatureHook : IDevKitFeatureHook
/// {
///     public string Name =&gt; "messaging";
///     public DevKitFeatureHookStage Stage =&gt; DevKitFeatureHookStage.ConfigureServices;
///     public void Apply(DevKitFeatureHookContext context) =&gt; context.Services.AddMessaging();
/// }
/// </code>
/// </example>
public interface IDevKitFeatureHook
{
    /// <summary>
    /// Gets the feature hook name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the stage in which the hook should run.
    /// </summary>
    DevKitFeatureHookStage Stage { get; }

    /// <summary>
    /// Applies the feature hook to the supplied context.
    /// </summary>
    /// <param name="context">The hook context.</param>
    void Apply(DevKitFeatureHookContext context);
}