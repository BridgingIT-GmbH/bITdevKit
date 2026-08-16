// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

/// <summary>
/// Defines operations for i cache invalidate command.
/// </summary>
public interface ICacheInvalidateCommand
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    CacheInvalidateCommandOptions Options { get; }
}

/// <summary>
/// Configures cache invalidate command.
/// </summary>
public class CacheInvalidateCommandOptions
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; }
}
