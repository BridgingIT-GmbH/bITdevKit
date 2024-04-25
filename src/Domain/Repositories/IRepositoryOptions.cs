// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Various options for the <see cref="IGenericRepositoryGenericRepository{TEntity}"/>.
/// </summary>
public interface IRepositoryOptions
{
    IEntityMapper Mapper { get; set; }

    bool Autosave { get; set; }
}