// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Security.Claims;
using System.Text;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

/// <summary>Provides stable application-qualified anonymous or authenticated preference partitions.</summary>
/// <example>
/// <code><![CDATA[
/// var context = new LocalStorageUserContextProvider(jsRuntime, null, "catalog-console");
/// var first = await context.GetUserContextId();
/// Debug.Assert(first == await context.GetUserContextId());
/// ]]></code>
/// </example>
public class LocalStorageUserContextProvider : IUserContextProvider
{
    private readonly IJSRuntime jsRuntime;
    private readonly AuthenticationStateProvider authStateProvider;
    private readonly string key;
    private readonly SemaphoreSlim gate = new(1, 1);
    private string inMemoryContextId = "anonymous:" + Guid.NewGuid().ToString("N");

    /// <summary>Preserves the original optional-browser constructor.</summary>
    /// <param name="jsRuntime">The optional originating browser runtime.</param>
    /// <param name="authStateProvider">The optional native authentication integration.</param>
    /// <example>
    /// <code><![CDATA[
    /// var context = new LocalStorageUserContextProvider(jsRuntime);
    /// var partition = await context.GetUserContextId();
    /// Debug.Assert(!string.IsNullOrEmpty(partition));
    /// ]]></code>
    /// </example>
    public LocalStorageUserContextProvider(IJSRuntime jsRuntime = null, AuthenticationStateProvider authStateProvider = null)
        : this(jsRuntime, authStateProvider, "default") { }

    /// <summary>Creates an application-qualified partition provider without browser I/O.</summary>
    /// <param name="jsRuntime">The optional browser runtime.</param>
    /// <param name="authStateProvider">The optional authentication integration.</param>
    /// <param name="applicationId">The stable application namespace.</param>
    /// <example>
    /// <code><![CDATA[
    /// var context = new LocalStorageUserContextProvider(jsRuntime, null, "catalog-console");
    /// var partition = await context.GetUserContextId();
    /// Debug.Assert(!string.IsNullOrEmpty(partition));
    /// ]]></code>
    /// </example>
    public LocalStorageUserContextProvider(IJSRuntime jsRuntime, AuthenticationStateProvider authStateProvider, string applicationId)
    {
        this.jsRuntime = jsRuntime;
        this.authStateProvider = authStateProvider;
        this.key = "bdk:appstate:anonymous:" + Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(applicationId));
        this.IsAvailable = jsRuntime != null;
    }

    /// <summary>Gets whether the most recent browser operation was available.</summary>
    public bool IsAvailable { get; private set; }

    /// <inheritdoc/>
    public async Task<string> GetUserContextId()
    {
        var user = this.authStateProvider != null ? (await this.authStateProvider.GetAuthenticationStateAsync()).User : null;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId)) { return "user:" + userId; }

        if (this.jsRuntime == null) { return this.inMemoryContextId; }

        await this.gate.WaitAsync();
        try
        {
            var stored = await this.jsRuntime.InvokeAsync<string>("localStorage.getItem", this.key);
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith("anonymous:", StringComparison.Ordinal)
                || !Guid.TryParseExact(stored[10..], "N", out _))
            {
                stored = "anonymous:" + Guid.NewGuid().ToString("N");
                await this.jsRuntime.InvokeVoidAsync("localStorage.setItem", this.key, stored);
            }

            this.inMemoryContextId = stored;
            this.IsAvailable = true;
            return stored;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException)
        {
            this.IsAvailable = false;
            return this.inMemoryContextId;
        }
        finally { this.gate.Release(); }
    }

    /// <inheritdoc/>
    public async Task Clear()
    {
        await this.gate.WaitAsync();
        try
        {
            if (this.jsRuntime != null) { await this.jsRuntime.InvokeVoidAsync("localStorage.removeItem", this.key); }

            this.inMemoryContextId = "anonymous:" + Guid.NewGuid().ToString("N");
        }
        finally { this.gate.Release(); }
    }

    /// <summary>Creates a nondurable opaque identifier for legacy callers.</summary>
    /// <returns>A full random GUID, avoiding a small truncated partition space.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var opaqueId = LocalStorageUserContextProvider.CreateShortHash();
    /// Debug.Assert(Guid.TryParseExact(opaqueId, "N", out _));
    /// ]]></code>
    /// </example>
    public static string CreateShortHash() => Guid.NewGuid().ToString("N");
}
