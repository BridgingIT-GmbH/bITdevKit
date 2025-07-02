namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Enumerates the possible persistence types for a app state.
/// </summary>
public enum AppStatePersistenceType
{
    /// <summary>
    /// State is scoped to the component lifetime, lost on navigation or refresh.
    /// </summary>
    ComponentScoped,

    /// <summary>
    /// State is shared within a user session (in-memory), lost on refresh or app restart.
    /// </summary>
    SessionScoped,

    /// <summary>
    /// State is persisted in browser localStorage, survives refreshes.
    /// </summary>
    LocalStorage,

    /// <summary>
    /// State is persisted using a custom provider (e.g., server-side storage).
    /// </summary>
    CustomStorage
}