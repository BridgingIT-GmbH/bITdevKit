// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Specifications;
using Microsoft.Extensions.Localization;

public static class DomainRules
{
    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static void Apply(IDomainRule rule)
    {
        var task = Task.Run(() => ApplyAsync(rule));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static TResult Apply<TResult>(IDomainRule rule)
    {
        var task = Task.Run(() => ApplyAsync<TResult>(rule));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }

        return task.Result;
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static void Apply(IDomainRule rule, IStringLocalizer localizer)
    {
        var task = Task.Run(() => ApplyAsync(rule, localizer));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static TResult Apply<TResult>(IDomainRule rule, IStringLocalizer localizer)
    {
        var task = Task.Run(() => ApplyAsync<TResult>(rule, localizer));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }

        return task.Result;
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static void Apply(IDomainRule rule, IStringLocalizer localizer, Action satisfied)
    {
        var task = Task.Run(() => ApplyAsync(rule, localizer, satisfied));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static void Apply(IDomainRule[] rules)
    {
        var task = Task.Run(() => ApplyAsync(rules));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static TResult Apply<TResult>(IDomainRule[] rules)
    {
        var task = Task.Run(() => ApplyAsync<TResult>(rules));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }

        return task.Result;
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static void Apply(IDomainRule[] rules, IStringLocalizer localizer)
    {
        var task = Task.Run(() => ApplyAsync(rules, localizer));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static void Apply(IDomainRule[] rules, IStringLocalizer localizer, Action satisfied)
    {
        var task = Task.Run(() => ApplyAsync(rules, localizer, satisfied));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static TResult Apply<TResult>(IDomainRule[] rules, IStringLocalizer localizer)
    {
        var task = Task.Run(() => ApplyAsync<TResult>(rules, localizer));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }

        return task.Result;
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static TResult Apply<TResult>(IDomainRule rule, IStringLocalizer localizer, Func<TResult> satisfied)
    {
        var task = Task.Run(() => ApplyAsync(rule, localizer, satisfied));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }

        return task.Result;
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static TResult Apply<TResult>(IDomainRule[] rules, IStringLocalizer localizer, Func<TResult> satisfied)
    {
        var task = Task.Run(() => ApplyAsync(rules, localizer, satisfied));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }

        return task.Result;
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static void Apply<TEntity>(ISpecification<TEntity> specification, TEntity entity, Action satisfied = null)
    {
        if (specification is null)
        {
            return;
        }

        if (!specification.IsSatisfiedBy(entity))
        {
            throw new DomainRuleException(specification.GetType().Name);
        }

        satisfied?.Invoke();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static void Apply<TEntity>(ISpecification<TEntity>[] specifications, TEntity entity, Action satisfied = null)
    {
        if (specifications?.Any() == true)
        {
            foreach (var specification in specifications)
            {
                Apply(specification, entity);
            }
        }

        satisfied?.Invoke();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static TResult Apply<TEntity, TResult>(ISpecification<TEntity>[] specifications, TEntity entity, Func<TResult> satisfied = null)
    {
        if (specifications?.Any() == true)
        {
            foreach (var specification in specifications)
            {
                Apply(specification, entity);
            }
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static TResult Apply<TEntity, TResult>(ISpecification<TEntity> specification, TEntity entity, Func<TResult> satisfied = null)
    {
        if (specification is null)
        {
            return default;
        }

        if (!specification.IsSatisfiedBy(entity))
        {
            throw new DomainRuleException(specification.GetType().Name);
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task ApplyAsync(IDomainRule[] rules, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(rules, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task ApplyAsync(IDomainRule[] rules, IStringLocalizer localizer, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(rules, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task ApplyAsync(IDomainRule[] rules, IStringLocalizer localizer, Action satisfied, CancellationToken cancellationToken = default)
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                await ApplyAsync(rule, localizer, cancellationToken: cancellationToken);
            }
        }

        satisfied?.Invoke();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task ApplyAsync(IDomainRule rule, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(rule, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task ApplyAsync(IDomainRule rule, IStringLocalizer localizer, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(rule, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task ApplyAsync(IDomainRule rule, IStringLocalizer localizer, Action satisfied, CancellationToken cancellationToken = default)
    {
        if (rule is null || !await rule.IsEnabledAsync(cancellationToken))
        {
            return;
        }

        if (!await rule.ApplyAsync(cancellationToken))
        {
            throw new DomainRuleException($"{rule.GetType().Name} {(localizer is not null ? localizer[rule.Message] : rule.Message)}");
        }

        satisfied?.Invoke();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ApplyAsync<TResult>(IDomainRule rule, CancellationToken cancellationToken = default)
    {
        return await ApplyAsync<TResult>(rule, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ApplyAsync<TResult>(IDomainRule rule, IStringLocalizer localizer, CancellationToken cancellationToken = default)
    {
        return await ApplyAsync<TResult>(rule, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ApplyAsync<TResult>(IDomainRule rule, IStringLocalizer localizer, Func<TResult> satisfied, CancellationToken cancellationToken = default)
    {
        if (rule is null || !await rule.IsEnabledAsync(cancellationToken))
        {
            return default;
        }

        if (!await rule.ApplyAsync(cancellationToken))
        {
            throw new DomainRuleException($"{rule.GetType().Name} {(localizer is not null ? localizer[rule.Message] : rule.Message)}");
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ApplyAsync<TResult>(IDomainRule[] rules, CancellationToken cancellationToken = default)
    {
        return await ApplyAsync<TResult>(rules, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ApplyAsync<TResult>(IDomainRule[] rules, IStringLocalizer localizer, CancellationToken cancellationToken = default)
    {
        return await ApplyAsync<TResult>(rules, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ApplyAsync<TResult>(IDomainRule[] rules, IStringLocalizer localizer, Func<TResult> satisfied, CancellationToken cancellationToken = default)
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                await ApplyAsync(rule, localizer, cancellationToken: cancellationToken);
            }
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks a business rule, returns satisfied or not
    /// </summary>
    public static bool Return(IDomainRule rule)
    {
        var task = Task.Run(() => ReturnAsync(rule));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            throw ex.InnerException;
        }

        return task.Result;
    }

    /// <summary>
    /// Checks several business rules, returns satisfied or not
    /// </summary>
    public static bool Return(IDomainRule[] rules)
    {
        var task = Task.Run(() => ReturnAsync(rules));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            throw ex.InnerException;
        }

        return task.Result;
    }

    /// <summary>
    /// Checks a business rule, returns satisfied or not
    /// </summary>
    public static bool Return<TEntity>(ISpecification<TEntity> specification, TEntity entity)
    {
        if (specification is null)
        {
            return true;
        }

        return specification.IsSatisfiedBy(entity);
    }

    /// <summary>
    /// Checks several business rules, returns satisfied or not
    /// </summary>
    public static bool Return<TEntity>(ISpecification<TEntity>[] specifications, TEntity entity)
    {
        if (specifications?.Any() == true)
        {
            foreach (var rule in specifications)
            {
                if (!Return(rule, entity))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Checks a business rule, returns satisfied or not
    /// </summary>
    public static async Task<bool> ReturnAsync(IDomainRule rule, CancellationToken cancellationToken = default)
    {
        if (rule is null || !await rule.IsEnabledAsync(cancellationToken))
        {
            return true;
        }

        return await rule.ApplyAsync(cancellationToken);
    }

    /// <summary>
    /// Checks several business rules, returns satisfied or not
    /// </summary>
    public static async Task<bool> ReturnAsync(IDomainRule[] rules, CancellationToken cancellationToken = default)
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                if (!await rule.IsEnabledAsync(cancellationToken))
                {
                    continue;
                }

                if (!await ReturnAsync(rule, cancellationToken))
                {
                    return false;
                }
            }
        }

        return true;
    }
}