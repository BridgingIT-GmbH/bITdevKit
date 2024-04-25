// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;
using EnsureThat;

public class DistinctOption<TEntity>
    where TEntity : class, IEntity
{
    public DistinctOption()
    {
    }

    public DistinctOption(
        Expression<Func<TEntity, object>> expression)
    {
        EnsureArg.IsNotNull(expression, nameof(expression));

        this.Expression = expression;
    }

    public Expression<Func<TEntity, object>> Expression { get; set; }
}