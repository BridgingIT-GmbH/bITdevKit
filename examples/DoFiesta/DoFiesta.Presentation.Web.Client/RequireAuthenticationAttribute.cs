// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Client;

/// <summary>
/// Marks a routed DoFiesta client page that must require an authenticated user in the client router.
/// </summary>
/// <remarks>
/// This keeps direct document navigation serving the SPA shell so the WebAssembly authentication flow can
/// redirect to login instead of the server returning a raw unauthorized response before the client starts.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class RequireAuthenticationAttribute : Attribute
{
}
