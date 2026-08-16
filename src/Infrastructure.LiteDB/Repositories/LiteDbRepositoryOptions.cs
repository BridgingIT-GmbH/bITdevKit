// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using Common;
using Domain.Repositories;

/// <summary>
/// Configures lite db repository.
/// </summary>
public class LiteDbRepositoryOptions : OptionsBase, ILiteDbRepositoryOptions
{
    /// <summary>
    /// Initializes a new instance of the <c>LiteDbRepositoryOptions</c> class.
    /// </summary>
    public LiteDbRepositoryOptions() { }

    /// <summary>
    /// Initializes a new instance of the <c>LiteDbRepositoryOptions</c> class.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="mapper">The mapper used to transform values.</param>
    public LiteDbRepositoryOptions(ILiteDbContext context, IEntityMapper mapper)
    {
        this.DbContext = context;
        this.Mapper = mapper;
    }

    /// <summary>
    ///     Gets or sets the database context.
    /// </summary>
    /// <value>
    ///     The database context.
    /// </value>
    public ILiteDbContext DbContext { get; set; }

    /// <summary>
    /// Gets or sets the mapper.
    /// </summary>
    public IEntityMapper Mapper { get; set; }

    /// <summary>
    /// Gets or sets the autosave.
    /// </summary>
    public bool Autosave { get; set; } = true;
}
