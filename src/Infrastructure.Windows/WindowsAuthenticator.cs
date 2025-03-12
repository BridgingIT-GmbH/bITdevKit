// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Windows;

using System;
using System.Runtime.InteropServices;
using System.Security.Principal;

/// <summary>
/// Provides Windows authentication services.
/// </summary>
public class WindowsAuthenticator : IWindowsAuthenticator
{
    private readonly string username;
    private readonly string password;
    private readonly string domain;
    private readonly Lazy<(IntPtr Token, WindowsIdentity Identity)> lazyCredentials;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsAuthenticator"/> class.
    /// </summary>
    /// <param name="username">The username for Windows authentication.</param>
    /// <param name="password">The password for Windows authentication.</param>
    /// <param name="domain">Optional domain for Windows authentication.</param>
    public WindowsAuthenticator(string username, string password, string domain = null)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Username and password are required for Windows authentication.", nameof(username));
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows authentication is only supported on Windows.");
        }

        this.username = username;
        this.password = password;
        this.domain = domain;
        this.lazyCredentials = new Lazy<(IntPtr Token, WindowsIdentity Identity)>(this.InitializeCredentials, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public (IntPtr Token, WindowsIdentity Identity) Authenticate()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows authentication is only supported on Windows.");
        }

        return this.lazyCredentials.Value;
    }

    /// <inheritdoc />
    public bool CloseToken(IntPtr token)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        return CloseHandle(token);
    }

    private (IntPtr Token, WindowsIdentity Identity) InitializeCredentials()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows authentication is only supported on Windows.");
        }

        if (!LogonUser(this.username, this.domain ?? Environment.MachineName, this.password,
                     LOGON32_LOGON_NEW_CREDENTIALS, LOGON32_PROVIDER_DEFAULT, out var logonToken))
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new System.ComponentModel.Win32Exception(errorCode, "Failed to logon user for Windows authentication.");
        }

        var identity = new WindowsIdentity(logonToken);
        return (logonToken, identity);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">
    /// true to release both managed and unmanaged resources; false to release only unmanaged resources.
    /// </param>
    public virtual void Dispose(bool disposing)
    {
        if (this.disposed || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        if (disposing && this.lazyCredentials.IsValueCreated)
        {
            this.lazyCredentials.Value.Identity?.Dispose();
            if (this.lazyCredentials.Value.Token != IntPtr.Zero)
            {
                CloseHandle(this.lazyCredentials.Value.Token);
            }
        }

        this.disposed = true;
    }

    #region P/Invoke for Windows API
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword,
        int dwLogonType, int dwLogonProvider, out IntPtr phToken);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern bool CloseHandle(IntPtr handle);

    private const int LOGON32_LOGON_NEW_CREDENTIALS = 9; // Use new credentials for network access
    private const int LOGON32_PROVIDER_DEFAULT = 0;
    #endregion
}
