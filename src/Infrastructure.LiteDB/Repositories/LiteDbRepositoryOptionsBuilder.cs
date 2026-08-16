// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using Common;
using Domain.Repositories;

/// <summary>
/// Builds lite db repository options configuration.
/// </summary>
public class LiteDbRepositoryOptionsBuilder
    : OptionsBuilderBase<LiteDbRepositoryOptions, LiteDbRepositoryOptionsBuilder>
{
    /// <summary>
    /// Executes the db context operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <returns>The result of the operation.</returns>
    public LiteDbRepositoryOptionsBuilder DbContext(ILiteDbContext context)
    {
        this.Target.DbContext = context;

        return this;
    }

    /// <summary>
    /// Executes the mapper operation.
    /// </summary>
    /// <param name="mapper">The mapper used to transform values.</param>
    /// <returns>The result of the operation.</returns>
    public LiteDbRepositoryOptionsBuilder Mapper(IEntityMapper mapper)
    {
        this.Target.Mapper = mapper;

        return this;
    }
}
