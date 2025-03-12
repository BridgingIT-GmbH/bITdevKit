// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Windows;

using System;
using System.Runtime.InteropServices;
using System.Security.Principal;

/// <summary>
/// Implementation of <see cref="IWindowsImpersonationService"/> that uses Windows identity impersonation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WindowsImpersonationService"/> class.
/// </remarks>
/// <param name="authenticator">The Windows authenticator to use for identity impersonation.</param>
public class WindowsImpersonationService(IWindowsAuthenticator authenticator) : IWindowsImpersonationService
{
    private readonly IWindowsAuthenticator authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsImpersonationService"/> class with a new authenticator.
    /// </summary>
    /// <param name="username">The username for Windows authentication.</param>
    /// <param name="password">The password for Windows authentication.</param>
    /// <param name="domain">Optional domain for Windows authentication.</param>
    public WindowsImpersonationService(string username, string password, string domain = null)
        : this(new WindowsAuthenticator(username, password, domain))
    {
    }

    /// <inheritdoc />
    public async Task<T> ExecuteImpersonatedAsync<T>(Func<Task<T>> operation)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows impersonation is only supported on Windows.");
        }

        var credentials = this.authenticator.Authenticate();
        return await WindowsIdentity.RunImpersonated(credentials.Identity.AccessToken, operation);
    }

    /// <inheritdoc />
    public async Task ExecuteImpersonatedAsync(Func<Task> operation)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows impersonation is only supported on Windows.");
        }

        var credentials = this.authenticator.Authenticate();
        await WindowsIdentity.RunImpersonated(credentials.Identity.AccessToken, operation);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the service.
    /// </summary>
    /// <param name="disposing">
    /// true to release both managed and unmanaged resources; false to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            this.authenticator.Dispose();
        }

        this.disposed = true;
    }
}