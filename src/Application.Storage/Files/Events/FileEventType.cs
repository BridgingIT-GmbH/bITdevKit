// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents the type of file event that occurred in the monitored storage location.
/// Used by processors to determine the nature of the change (e.g., Added, Changed, Deleted).
/// </summary>
public enum FileEventType
{
    /// <summary>
    /// Indicates a file was newly created or added to the monitored location.
    /// </summary>
    Added,

    /// <summary>
    /// Indicates an existing file was modified (e.g., content or metadata changed).
    /// </summary>
    Changed,

    /// <summary>
    /// Indicates a file was removed from the monitored location.
    /// </summary>
    Deleted
}