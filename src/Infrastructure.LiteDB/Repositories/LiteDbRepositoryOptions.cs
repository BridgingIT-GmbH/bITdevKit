// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using Common;
using Domain.Repositories;

public class LiteDbRepositoryOptions : OptionsBase, ILiteDbRepositoryOptions
{
    public LiteDbRepositoryOptions() { }

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

    public IEntityMapper Mapper { get; set; }

    public bool Autosave { get; set; } = true;
}