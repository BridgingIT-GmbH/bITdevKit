// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;
using EnsureThat;

public class IncludeOption<TEntity>
    where TEntity : class, IEntity
{
    public IncludeOption(
        Expression<Func<TEntity, object>> expression)
    {
        EnsureArg.IsNotNull(expression, nameof(expression));

        this.Expression = expression;
    }

    public IncludeOption(string path)
    {
        EnsureArg.IsNotNull(path, nameof(path));

        this.Path = path;
    }

    public Expression<Func<TEntity, object>> Expression { get; }

    public string Path { get; }
}