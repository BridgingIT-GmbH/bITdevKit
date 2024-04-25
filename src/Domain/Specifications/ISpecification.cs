// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System;
using System.Linq.Expressions;

public interface ISpecification<T>
{
    Expression<Func<T, bool>> ToExpression();

    Func<T, bool> ToPredicate();

    bool IsSatisfiedBy(T entity);

    ISpecification<T> Or(ISpecification<T> specification);

    ISpecification<T> And(ISpecification<T> specification);

    ISpecification<T> Not();
}