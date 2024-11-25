// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

public class EntityFrameworkRepositoryOptions : OptionsBase
{
    public EntityFrameworkRepositoryOptions() { }

    public EntityFrameworkRepositoryOptions(DbContext context, IEntityMapper mapper = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.DbContext = context;
        this.Mapper = mapper;
    }

    /// <summary>
    ///     Gets or sets the database context.
    /// </summary>
    /// <value>
    ///     The database context.
    /// </value>
    public virtual DbContext DbContext { get; set; }

    public virtual IEntityMapper Mapper { get; set; }

    public virtual Func<Guid> VersionGenerator { get; set; } = GuidGenerator.CreateSequential;

    public virtual bool Autosave { get; set; } = true;
}