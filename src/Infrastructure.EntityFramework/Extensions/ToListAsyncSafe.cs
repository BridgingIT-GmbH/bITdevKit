// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public static partial class Extensions
{
    public static Task<List<TSource>> ToListAsyncSafe<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(source, nameof(source));

        if (source is not IAsyncEnumerable<TSource>)
        {
            return Task.FromResult(source.ToList());
        }

        return source.ToListAsync(cancellationToken);
    }
}