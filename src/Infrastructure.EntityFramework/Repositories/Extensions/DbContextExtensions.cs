// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using EnsureThat;
using Microsoft.EntityFrameworkCore;

public static partial class Extensions
{
    /// <summary>
    /// Führt die übergebene Operation in einer Transaktion aus.
    /// </summary>
    public static async Task ExecuteScopedAsync(this DbContext source, Func<Task> action)
    {
        EnsureArg.IsNotNull(source, nameof(source));
        EnsureArg.IsNotNull(action, nameof(action));

        await ResilientTransaction.Create(source).ExecuteAsync(action).AnyContext();
    }
}