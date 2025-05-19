namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

/// <summary>
/// Provides a user context ID stored in memory or optionally in localStorage for user isolation.
/// </summary>
/// <remarks>
/// This implementation is Blazor-agnostic, using an in-memory ID by default. For Blazor apps, inject IJSRuntime to enable localStorage persistence.
/// </remarks>
/// <param name="jsRuntime">The optional JavaScript runtime for localStorage interaction (null in non-Blazor contexts).</param>
/// <param name="authStateProvider">The authentication state provider to get the current user.</param>
public class LocalStorageUserContextProvider(IJSRuntime jsRuntime = null, AuthenticationStateProvider authStateProvider = null) : IUserContextProvider
{
    private const string UserContextKey = "appstate_user_context_id";
    private const string UserIdKey = "appstate_user_id";
    private readonly IJSRuntime jsRuntime = jsRuntime;
    private string inMemoryContextId = jsRuntime == null ? Guid.NewGuid().ToString() : null;

    /// <summary>
    /// Asynchronously gets or generates a unique identifier for the current user context.
    /// </summary>
    /// <returns>A task returning a string representing the user context ID.</returns>
    /// <remarks>
    /// Uses localStorage if IJSRuntime is available (Blazor); otherwise, returns an in-memory ID.
    /// Generates a new user context ID if the current user has changed.
    /// </remarks>
    public async Task<string> GetUserContextId()
    {
        if (this.jsRuntime != null)
        {
            // Get the current user ID
            var currentUserId = await this.GetCurrentUserIdAsync();

            // Get the stored user context ID and associated user ID from localStorage
            var storedContextId = await this.jsRuntime.InvokeAsync<string>("localStorage.getItem", UserContextKey);
            var storedUserId = await this.jsRuntime.InvokeAsync<string>("localStorage.getItem", UserIdKey);

            // If there's no stored context ID, or the user has changed, generate a new context ID
            if (string.IsNullOrEmpty(storedContextId) || storedUserId != currentUserId)
            {
                var newContextId = currentUserId ?? CreateShortHash();
                await this.jsRuntime.InvokeVoidAsync("localStorage.setItem", UserContextKey, newContextId);
                await this.jsRuntime.InvokeVoidAsync("localStorage.setItem", UserIdKey, currentUserId ?? string.Empty);
                return newContextId;
            }

            return storedContextId;
        }

        // Non-Blazor fallback: use in-memory ID
        return this.inMemoryContextId ??= CreateShortHash();
    }

    /// <summary>
    /// Clears the stored user context ID and associated user ID from localStorage.
    /// </summary>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    public async Task ClearAsync()
    {
        if (this.jsRuntime != null)
        {
            await this.jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserContextKey);
            await this.jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserIdKey);
        }
        this.inMemoryContextId = null;
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        if (authStateProvider == null)
        {
            return null;
        }

        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public static string CreateShortHash()
    {
        var guid = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes(guid.ToString());
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        var truncatedHash = new byte[4];
        Array.Copy(hashBytes, truncatedHash, 4);
        var base64 = Convert.ToBase64String(truncatedHash);

        return new string([.. base64.Where(char.IsLetterOrDigit).Take(8)]).ToLower();
    }
}