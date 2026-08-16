// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Windows;

using System;
using System.Security.Principal;

/// <summary>
/// Defines operations for i windows authenticator.
/// </summary>
public interface IWindowsAuthenticator : IDisposable
{
    /// <summary>
    /// Executes the authenticate operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    (IntPtr Token, WindowsIdentity Identity) Authenticate();

    /// <summary>
    /// Executes the close token operation.
    /// </summary>
    /// <param name="token">The token used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    bool CloseToken(IntPtr token);
}
