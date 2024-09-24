// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     Represents an interface used for entities that support concurrent access control.
///     It is used to address issues related to concurrency in data operations by incorporating
///     a version attribute that can be used for optimistic locking mechanisms.
/// </summary>
public interface IConcurrent
{
    /// <summary>
    ///     Gets or sets the version identifier for the entity.
    ///     This property is typically used for concurrency control, ensuring that updates to the entity
    ///     are only applied if the version identifier matches the expected value.
    /// </summary>
    /// <remarks>
    ///     A new version identifier is usually generated each time the entity is inserted, updated,
    ///     or marked as deleted. The version identifier is commonly represented as a GUID.
    /// </remarks>
    Guid Version { get; set; }
}