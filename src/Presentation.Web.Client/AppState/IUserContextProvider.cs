namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines the contract for user context providers, generating unique identifiers for state isolation.
/// </summary>
public interface IUserContextProvider
{
    /// <summary>
    /// Gets or generates a unique identifier for the current user context.
    /// </summary>
    /// <returns>A string representing the user context ID.</returns>
    Task<string> GetUserContextId();

    /// <summary>
    /// Clears the stored user context ID, if any.
    /// </summary>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    Task ClearAsync();
}
