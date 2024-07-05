// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using EnsureThat;
using BridgingIT.DevKit.Infrastructure.EntityFramework;

public class PersonStub : AggregateRoot<Guid>
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }
}

public class PersonDtoStub //: AggregateRoot<string>
{
    public Guid Identifier { get; set; }

    public string Nationality { get; set; }

    public string FullName { get; set; }

    public int Age { get; set; }
}

public class PersonDomainEventStub(long ticks) : DomainEventBase
{
    public long Ticks { get; } = ticks;
}

public class StubMessage : MessageBase
{
    public string FirstName { get; set; }

    public string LastName { get; set; }
}

public class StubDbContext : DbContext, IOutboxDomainEventContext, IOutboxMessageContext
{
    public StubDbContext()
    {
    }

    public StubDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    public DbSet<OutboxMessage> OutboxMessages { get; set; }
}

public sealed class StubDbContextFixture : IDisposable
{
    public StubDbContextFixture()
    {
        var builder = new DbContextOptionsBuilder<StubDbContext>()
            .UseInMemoryDatabase($"Test_{KeyGenerator.Create(10)}");
        this.Context = new StubDbContext(builder.Options);
    }

    public StubDbContext Context { get; }

    public void Dispose()
    {
        this.Context.Dispose();
    }
}

public class MapperProfile : Profile
{
    public MapperProfile()
    {
        this.CreateMap<PersonStub, PersonDtoStub>()
            .ForMember(d => d.Identifier, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Age, o => o.MapFrom(s => s.Age))
            .ForMember(d => d.FullName, opt => opt.MapFrom(s => $"{s.FirstName} {s.LastName}"))
            .IgnoreAllUnmapped();

        this.CreateMap<PersonDtoStub, PersonStub>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Identifier))
            .ForMember(d => d.Age, o => o.MapFrom(s => s.Age))
            .ForMember(d => d.FirstName, opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None).FirstOrDefault()))
            .ForMember(d => d.LastName, opt => opt.MapFrom(s => s.FullName.Split(' ', StringSplitOptions.None).LastOrDefault()))
            .IgnoreAllUnmapped();
    }
}

public class PersonStubRepository : EntityFrameworkRepository<PersonStub>
{
    public PersonStubRepository(EntityFrameworkRepositoryOptions options)
        : base(options)
    {
        EnsureArg.IsNotNull(options.Mapper, nameof(options.Mapper));
    }

    public PersonStubRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : base(optionsBuilder)
    {
    }

    public async Task<IEnumerable<PersonStub>> FindAllAsync(IFindOptions<PersonStub> options = null,
        CancellationToken cancellationToken = default)
    {
        var query = this.Options.DbContext.Set<PersonDtoStub>().AsNoTrackingIf(options, this.Options.Mapper).TakeIf(options?.Take);
        if (this.Options?.Mapper is not null && query is not null)
        {
            return await query.Select(p => this.Options.Mapper.Map<PersonStub>(p)).ToListAsyncSafe(cancellationToken)
                .AnyContext();
        }

        return null;
    }

    public async Task<PersonStub> FindOneAsync(Guid id)
    {
        var entity = await this.Options.DbContext.Set<PersonDtoStub>().FindAsync(id).AnyContext();
        if (entity is null)
        {
            return null;
        }

        return this.Options.Mapper.Map<PersonStub>(entity);
    }

    public async Task<IEnumerable<string>> FindAllLastNames()
    {
        return await Task.FromResult(
            this.Options.DbContext.Set<PersonDtoStub>().Select(p => p.FullName)).AnyContext();

        // dapper queries, does not work with inmemory provider
        //var dbConnection = this.GetDbConnection();
        //return (await dbConnection.QueryAsync<string>("select Lastname from Persons", transaction: this.GetDbTransaction())).ToList();
    }
}