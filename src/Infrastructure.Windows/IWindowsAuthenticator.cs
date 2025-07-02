// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Windows;

using System;
using System.Security.Principal;

public interface IWindowsAuthenticator : IDisposable
{
    (IntPtr Token, WindowsIdentity Identity) Authenticate();

    bool CloseToken(IntPtr token);
}
