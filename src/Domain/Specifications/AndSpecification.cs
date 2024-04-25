﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System;
using System.Linq;
using System.Linq.Expressions;
using EnsureThat;

public class AndSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> leftSpecification;
    private readonly ISpecification<T> rightSpecification;

    public AndSpecification(ISpecification<T> leftSpecification, ISpecification<T> rightSpecification)
    {
        EnsureArg.IsNotNull(leftSpecification);
        EnsureArg.IsNotNull(rightSpecification);

        this.rightSpecification = rightSpecification;
        this.leftSpecification = leftSpecification;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = this.leftSpecification.ToExpression();
        var rightExpression = this.rightSpecification.ToExpression();

        //var andExpression = Expression.AndAlso(leftExpression.Body, rightExpression.Body);
        var andExpression = Expression.AndAlso(
            leftExpression.Body,
            Expression.Invoke(rightExpression, leftExpression.Parameters.Single()));

        //return Expression.Lambda<Func<T, bool>>(andExpression, leftExpression.Parameters.Single());
        return Expression.Lambda<Func<T, bool>>(andExpression, leftExpression.Parameters);
    }

    public override string ToString()
    {
        return this.ToExpression()?.ToString();
    }
}