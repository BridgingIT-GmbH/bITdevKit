// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using Microsoft.EntityFrameworkCore;

// Combined maintenance always acquires this row before the operation coordination row.
// Normal operation publication never locks this row, keeping Runtime controls independent.
internal static class EntityFrameworkProfilingRuntimeGate
{
    internal static async Task<ProfilingRuntimeGateEntity> AcquireAsync<TContext>(TContext context, CancellationToken cancellationToken)
        where TContext : DbContext, IProfilingDbContext
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Runtime profiling coordination requires an owned relational transaction.");
        }

        var version = Guid.NewGuid();
        var changed = await context.ProfilingRuntimeGates.Where(gate => gate.Id == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(gate => gate.ConcurrencyVersion, version), cancellationToken).ConfigureAwait(false);
        if (changed == 0)
        {
            context.ProfilingRuntimeGates.Add(new() { Id = 1, ConcurrencyVersion = version });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var gate = await context.ProfilingRuntimeGates.SingleAsync(entity => entity.Id == 1, cancellationToken).ConfigureAwait(false);
        if (gate.DeletionRevision < 0)
        {
            throw new ArgumentException("Runtime profiling deletion state is invalid.");
        }

        return gate;
    }
}
