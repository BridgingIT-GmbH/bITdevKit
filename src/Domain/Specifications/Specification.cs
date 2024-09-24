// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System.Linq.Expressions;

public class Specification<T> : ISpecification<T>
{
    private readonly Expression<Func<T, bool>> expression;

    public Specification(Expression<Func<T, bool>> expression)
    {
        this.expression = expression;
    }

    protected Specification() { }

    public virtual Expression<Func<T, bool>> ToExpression()
    {
        return this.expression;
    }

    public Func<T, bool> ToPredicate()
    {
        return this.ToExpression()?.Compile();
    }

    public bool IsSatisfiedBy(T entity)
    {
        if (entity is null)
        {
            return false;
        }

        var predicate = this.ToPredicate();
        return predicate(entity);
    }

    public ISpecification<T> And(ISpecification<T> specification)
    {
        //if (this == All)
        //{
        //    return specification;
        //}

        //if (specification == All)
        //{
        //    return this;
        //}

        return new AndSpecification<T>(this, specification);
    }

    public ISpecification<T> Or(ISpecification<T> specification)
    {
        //if (this == All || specification == All)
        //{
        //    return All;
        //}

        return new OrSpecification<T>(this, specification);
    }

    public ISpecification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
}