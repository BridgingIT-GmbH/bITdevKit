// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Defines various configuration options for repository behavior, such as entity mapping and autosave settings.
/// </summary>
public interface IRepositoryOptions
{
    /// <summary>
    ///     Gets or sets the entity mapper.
    ///     Provides functionality to map entities to different representations or types.
    /// </summary>
    IEntityMapper Mapper { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether changes are automatically saved.
    ///     When set to <c>true</c>, any changes made to the repository will be automatically persisted.
    /// </summary>
    bool Autosave { get; set; }
}