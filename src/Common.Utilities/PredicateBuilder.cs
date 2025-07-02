// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

/// <summary>
/// A fluent, EF Core–compatible builder for dynamic LINQ predicate expressions.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <example>
/// <code>
/// var builder = new PredicateBuilder&lt;Customer&gt;()
///     .Add(c => c.Age &gt; 18)
///     .Or(c => c.Name == "Alice");
/// var predicate = builder.Build();
/// var adultsOrAlice = customers.Where(predicate).ToList();
/// </code>
/// </example>
public class PredicateBuilder<T>
{
    private readonly Stack<Expression<Func<T, bool>>> expressions = [];
    private readonly Stack<Func<Expression, Expression, BinaryExpression>> groupOperators = [];
    private Expression<Func<T, bool>> current = x => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredicateBuilder{T}"/> class.
    /// </summary>
    public PredicateBuilder()
    {
        this.expressions.Push(this.current);
        this.groupOperators.Push(Expression.AndAlso);
    }

    /// <summary>
    /// ANDs the given expression with the current predicate.
    /// </summary>
    /// <param name="expr">The expression to add.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Add(c => c.Age &gt; 18);
    /// </code>
    /// </example>
    public PredicateBuilder<T> Add(Expression<Func<T, bool>> expr)
    {
        this.current = Combine(this.current, expr, Expression.AndAlso);
        return this;
    }

    /// <summary>
    /// ORs the given expression with the current predicate.
    /// </summary>
    /// <param name="expr">The expression to add.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Or(c => c.Name == "Alice");
    /// </code>
    /// </example>
    public PredicateBuilder<T> Or(Expression<Func<T, bool>> expr)
    {
        this.current = Combine(this.current, expr, Expression.OrElse);
        return this;
    }

    /// <summary>
    /// ANDs the given expression if the condition is true.
    /// </summary>
    /// <param name="condition">Whether to add the expression.</param>
    /// <param name="expr">The expression to add.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddIf(applyAgeFilter, c => c.Age &gt; 18);
    /// </code>
    /// </example>
    public PredicateBuilder<T> AddIf(bool condition, Expression<Func<T, bool>> expr)
    {
        if (condition)
        {
            this.current = Combine(this.current, expr, Expression.AndAlso);
        }
        return this;
    }

    /// <summary>
    /// ORs the given expression if the condition is true.
    /// </summary>
    /// <param name="condition">Whether to add the expression.</param>
    /// <param name="expr">The expression to add.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.OrIf(applyNameFilter, c => c.Name == "Alice");
    /// </code>
    /// </example>
    public PredicateBuilder<T> OrIf(bool condition, Expression<Func<T, bool>> expr)
    {
        if (condition)
        {
            this.current = Combine(this.current, expr, Expression.OrElse);
        }
        return this;
    }

    /// <summary>
    /// Adds one of two expressions based on a condition (ANDed).
    /// </summary>
    /// <param name="condition">The condition to choose the expression.</param>
    /// <param name="ifExpr">Expression if condition is true.</param>
    /// <param name="elseExpr">Expression if condition is false.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddIfElse(useYoung, c => c.Age &lt; 30, c => c.Age &gt;= 30);
    /// </code>
    /// </example>
    public PredicateBuilder<T> AddIfElse(
        bool condition,
        Expression<Func<T, bool>> ifExpr,
        Expression<Func<T, bool>> elseExpr)
    {
        this.current = Combine(this.current, condition ? ifExpr : elseExpr, Expression.AndAlso);
        return this;
    }

    /// <summary>
    /// Adds multiple expressions (ANDed or ORed).
    /// </summary>
    /// <param name="expressions">A collection of (condition, expression) tuples.</param>
    /// <param name="useOr">If true, combine with OR; otherwise, AND.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddRange(new[] {
    ///     (minAge != null, (Expression&lt;Func&lt;Customer, bool&gt;&gt;)(c => c.Age &gt;= minAge)),
    ///     (maxAge != null, (Expression&lt;Func&lt;Customer, bool&gt;&gt;)(c => c.Age &lt;= maxAge))
    /// });
    /// </code>
    /// </example>
    public PredicateBuilder<T> AddRange(
        IEnumerable<(bool condition, Expression<Func<T, bool>> expr)> expressions,
        bool useOr = false)
    {
        Func<Expression, Expression, BinaryExpression> op = useOr
            ? (left, right) => Expression.OrElse(left, right)
            : (left, right) => Expression.AndAlso(left, right);
        foreach (var (condition, expr) in expressions)
        {
            if (condition)
            {
                this.current = Combine(this.current, expr, op);
            }
        }
        return this;
    }

    /// <summary>
    /// Negates the given expression and ANDs it if the condition is true.
    /// </summary>
    /// <param name="condition">Whether to add the negated expression.</param>
    /// <param name="expr">The expression to negate and add.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.NotIf(excludeInactive, c => c.IsActive);
    /// </code>
    /// </example>
    public PredicateBuilder<T> NotIf(bool condition, Expression<Func<T, bool>> expr)
    {
        if (condition)
        {
            var notExpr = Expression.Lambda<Func<T, bool>>(
                Expression.Not(expr.Body), expr.Parameters);
            this.current = Combine(this.current, notExpr, Expression.AndAlso);
        }
        return this;
    }

    /// <summary>
    /// Combines the current expression with the given one using a custom binary operator.
    /// </summary>
    /// <param name="condition">Whether to add the expression.</param>
    /// <param name="expr">The expression to add.</param>
    /// <param name="combinator">The binary operator (e.g., Expression.AndAlso).</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Custom(true, c => c.IsActive, Expression.OrElse);
    /// </code>
    /// </example>
    public PredicateBuilder<T> Custom(
        bool condition,
        Expression<Func<T, bool>> expr,
        Func<Expression, Expression, BinaryExpression> combinator)
    {
        if (condition)
        {
            this.current = Combine(this.current, expr, combinator);
        }
        return this;
    }

    /// <summary>
    /// Begins a new group of expressions (default AND).
    /// </summary>
    /// <param name="useOr">If true, group will be ORed; otherwise, ANDed.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.BeginGroup()
    ///        .Add(c => c.Name == "Alice")
    ///        .Or(c => c.Name == "Bob")
    ///        .EndGroup();
    /// </code>
    /// </example>
    public PredicateBuilder<T> BeginGroup(bool useOr = false)
    {
        this.expressions.Push(this.current);
        this.groupOperators.Push(useOr ? Expression.OrElse : Expression.AndAlso);
        this.current = x => true;
        return this;
    }

    /// <summary>
    /// Ends the current group and combines it with the previous expression.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.BeginGroup()
    ///        .Add(c => c.Name == "Alice")
    ///        .Or(c => c.Name == "Bob")
    ///        .EndGroup();
    /// </code>
    /// </example>
    public PredicateBuilder<T> EndGroup()
    {
        if (this.expressions.Count > 1)
        {
            var groupExpr = this.current;
            this.current = this.expressions.Pop();
            var op = this.groupOperators.Pop();
            this.current = Combine(this.current, groupExpr, op);
        }
        return this;
    }

    /// <summary>
    /// For more readable chaining (no-op).
    /// </summary>
    public PredicateBuilder<T> Then() => this;

    /// <summary>
    /// For more readable chaining (no-op).
    /// </summary>
    public PredicateBuilder<T> Else() => this;

    /// <summary>
    /// Builds and compiles the predicate as a <see cref="Func{T, bool}"/>.
    /// </summary>
    /// <returns>The compiled predicate.</returns>
    /// <example>
    /// <code>
    /// var predicate = builder.Build();
    /// var filtered = customers.Where(predicate).ToList();
    /// </code>
    /// </example>
    public Func<T, bool> Build() => this.current.Compile();

    /// <summary>
    /// Builds the predicate as an <see cref="Expression{Func{T, bool}}"/>.
    /// </summary>
    /// <returns>The predicate expression.</returns>
    /// <example>
    /// <code>
    /// var expr = builder.BuildExpression();
    /// var filtered = dbContext.Customers.Where(expr);
    /// </code>
    /// </example>
    public Expression<Func<T, bool>> BuildExpression() => this.current;

    private static Expression<Func<T, bool>> Combine(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var param = left.Parameters[0];
        var visitor = new ReplaceParameterVisitor(right.Parameters[0], param);
        var rightBody = visitor.Visit(right.Body);
        var body = merge(left.Body, rightBody);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    private class ReplaceParameterVisitor(ParameterExpression oldParam, ParameterExpression newParam) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == oldParam ? newParam : base.VisitParameter(node);
    }
}