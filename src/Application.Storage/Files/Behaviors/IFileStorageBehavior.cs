// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines a behavior/decorator contract for extending IFileStorageProvider functionality,
/// such as logging, caching, or retrying operations, while preserving the Result pattern.
/// </summary>
public interface IFileStorageBehavior : IFileStorageProvider
{
    /// <summary>
    /// Gets the inner IFileStorageProvider wrapped by this behavior.
    /// </summary>
    IFileStorageProvider InnerProvider { get; }
}