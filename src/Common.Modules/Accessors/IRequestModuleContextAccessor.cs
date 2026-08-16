// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Http;

/// <summary>
///     Resolves a module from an HTTP request.
/// </summary>
public interface IRequestModuleContextAccessor
{
    /// <summary>
    ///     Finds the module associated with an HTTP request.
    /// </summary>
    /// <param name="request">The request to inspect.</param>
    /// <returns>The matching module, or <see langword="null"/> when none is found.</returns>
    IModule Find(HttpRequest request);
}
