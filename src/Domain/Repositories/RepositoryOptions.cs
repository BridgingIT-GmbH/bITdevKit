// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Provides configuration options for the repository.
/// </summary>
public class RepositoryOptions(IEntityMapper mapper) : IRepositoryOptions
{
    /// <summary>
    ///     Gets or sets the entity mapper used to map objects.
    /// </summary>
    public IEntityMapper Mapper { get; set; } = mapper;

    /// <summary>
    ///     Indicates whether the repository should automatically persist changes to the data store.
    ///     When set to true, changes are automatically saved without requiring an explicit save command.
    /// </summary>
    public bool Autosave { get; set; } = true;
}