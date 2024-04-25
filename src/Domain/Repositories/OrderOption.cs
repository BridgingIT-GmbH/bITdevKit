// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;
using EnsureThat;

public class OrderOption<TEntity>
    where TEntity : class, IEntity
{
    public OrderOption(
        Expression<Func<TEntity, object>> orderingExpression,
        OrderDirection direction = OrderDirection.Ascending)
    {
        EnsureArg.IsNotNull(orderingExpression, nameof(orderingExpression));

        this.Expression = orderingExpression;
        this.Direction = direction;
    }

    public OrderOption(string ordering)
    {
        EnsureArg.IsNotNull(ordering, nameof(ordering));

        this.Ordering = ordering;
    }

    public Expression<Func<TEntity, object>> Expression { get; set; }

    public string Ordering { get; } // of the form >   fieldname [ascending|descending], ...

    public OrderDirection Direction { get; set; }
}