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

public static class Check
{
    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static void Throw(IBusinessRule rule)
    {
        var task = Task.Run(() => ThrowAsync(rule));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if(ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static TResult Throw<TResult>(IBusinessRule rule)
    {
        var task = Task.Run(() => ThrowAsync<TResult>(rule));

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
    public static void Throw(IBusinessRule rule, IStringLocalizer localizer)
    {
        var task = Task.Run(() => ThrowAsync(rule, localizer));

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
    public static TResult Throw<TResult>(IBusinessRule rule, IStringLocalizer localizer)
    {
        var task = Task.Run(() => ThrowAsync<TResult>(rule, localizer));

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
    public static void Throw(IBusinessRule rule, IStringLocalizer localizer, Action satisfied)
    {
        var task = Task.Run(() => ThrowAsync(rule, localizer, satisfied));

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
    public static void Throw(IEnumerable<IBusinessRule> rules)
    {
        var task = Task.Run(() => ThrowAsync(rules));

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
    public static TResult Throw<TResult>(IEnumerable<IBusinessRule> rules)
    {
        var task = Task.Run(() => ThrowAsync<TResult>(rules));

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
    public static void Throw(IEnumerable<IBusinessRule> rules, IStringLocalizer localizer)
    {
        var task = Task.Run(() => ThrowAsync(rules, localizer));

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
    public static void Throw(IEnumerable<IBusinessRule> rules, IStringLocalizer localizer, Action satisfied)
    {
        var task = Task.Run(() => ThrowAsync(rules, localizer, satisfied));

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
    public static TResult Throw<TResult>(IEnumerable<IBusinessRule> rules, IStringLocalizer localizer)
    {
        var task = Task.Run(() => ThrowAsync<TResult>(rules, localizer));

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
    public static TResult Throw<TResult>(IBusinessRule rule, IStringLocalizer localizer, Func<TResult> satisfied)
    {
        var task = Task.Run(() => ThrowAsync(rule, localizer, satisfied));

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
    public static TResult Throw<TResult>(IEnumerable<IBusinessRule> rules, IStringLocalizer localizer, Func<TResult> satisfied)
    {
        var task = Task.Run(() => ThrowAsync(rules, localizer, satisfied));

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
    public static void Throw<TEntity>(ISpecification<TEntity> specification, TEntity entity, Action satisfied = null)
    {
        if (specification is null)
        {
            return;
        }

        if (!specification.IsSatisfiedBy(entity))
        {
            throw new BusinessRuleNotSatisfiedException($"{specification.GetType().Name}");
        }

        satisfied?.Invoke();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static void Throw<TEntity>(IEnumerable<ISpecification<TEntity>> specifications, TEntity entity, Action satisfied = null)
    {
        if (specifications?.Any() == true)
        {
            foreach (var specification in specifications)
            {
                Throw(specification, entity);
            }
        }

        satisfied?.Invoke();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static TResult Throw<TEntity, TResult>(IEnumerable<ISpecification<TEntity>> specifications, TEntity entity, Func<TResult> satisfied = null)
    {
        if (specifications?.Any() == true)
        {
            foreach (var specification in specifications)
            {
                Throw(specification, entity);
            }
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static TResult Throw<TEntity, TResult>(ISpecification<TEntity> specification, TEntity entity, Func<TResult> satisfied = null)
    {
        if (specification is null)
        {
            return default;
        }

        if (!specification.IsSatisfiedBy(entity))
        {
            throw new BusinessRuleNotSatisfiedException($"{specification.GetType().Name}");
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task ThrowAsync(IEnumerable<IBusinessRule> rules, CancellationToken cancellationToken = default)
    {
        await ThrowAsync(rules, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task ThrowAsync(IEnumerable<IBusinessRule> rules, IStringLocalizer localizer, CancellationToken cancellationToken = default)
    {
        await ThrowAsync(rules, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task ThrowAsync(IEnumerable<IBusinessRule> rules, IStringLocalizer localizer, Action satisfied, CancellationToken cancellationToken = default)
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                await ThrowAsync(rule, localizer, cancellationToken: cancellationToken);
            }
        }

        satisfied?.Invoke();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task ThrowAsync(IBusinessRule rule, CancellationToken cancellationToken = default)
    {
        await ThrowAsync(rule, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task ThrowAsync(IBusinessRule rule, IStringLocalizer localizer, CancellationToken cancellationToken = default)
    {
        await ThrowAsync(rule, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task ThrowAsync(IBusinessRule rule, IStringLocalizer localizer, Action satisfied, CancellationToken cancellationToken = default)
    {
        if (rule is null)
        {
            return;
        }

        if (!await rule.IsSatisfiedAsync(cancellationToken))
        {
            throw new BusinessRuleNotSatisfiedException($"{rule.GetType().Name}: {(localizer is not null ? localizer[rule.Message] : rule.Message)}");
        }

        satisfied?.Invoke();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ThrowAsync<TResult>(IBusinessRule rule, CancellationToken cancellationToken = default)
    {
        return await ThrowAsync<TResult>(rule, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ThrowAsync<TResult>(IBusinessRule rule, IStringLocalizer localizer, CancellationToken cancellationToken = default)
    {
        return await ThrowAsync<TResult>(rule, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks a business rule, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ThrowAsync<TResult>(IBusinessRule rule, IStringLocalizer localizer, Func<TResult> satisfied, CancellationToken cancellationToken = default)
    {
        if (rule is null)
        {
            return default;
        }

        if (!await rule.IsSatisfiedAsync(cancellationToken))
        {
            throw new BusinessRuleNotSatisfiedException($"{rule.GetType().Name}: {(localizer is not null ? localizer[rule.Message] : rule.Message)}");
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ThrowAsync<TResult>(IEnumerable<IBusinessRule> rules, CancellationToken cancellationToken = default)
    {
        return await ThrowAsync<TResult>(rules, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ThrowAsync<TResult>(IEnumerable<IBusinessRule> rules, IStringLocalizer localizer, CancellationToken cancellationToken = default)
    {
        return await ThrowAsync<TResult>(rules, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks several business rules, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ThrowAsync<TResult>(IEnumerable<IBusinessRule> rules, IStringLocalizer localizer, Func<TResult> satisfied, CancellationToken cancellationToken = default)
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                await ThrowAsync(rule, localizer, cancellationToken: cancellationToken);
            }
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks a business rule, returns satisfied or not
    /// </summary>
    public static bool Return(IBusinessRule rule)
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
    public static bool Return(IEnumerable<IBusinessRule> rules)
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
    public static bool Return<TEntity>(IEnumerable<ISpecification<TEntity>> specifications, TEntity entity)
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
    public static async Task<bool> ReturnAsync(IBusinessRule rule, CancellationToken cancellationToken = default)
    {
        if (rule is null)
        {
            return true;
        }

        return await rule.IsSatisfiedAsync(cancellationToken);
    }

    /// <summary>
    /// Checks several business rules, returns satisfied or not
    /// </summary>
    public static async Task<bool> ReturnAsync(IEnumerable<IBusinessRule> rules, CancellationToken cancellationToken = default)
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                if (!await ReturnAsync(rule, cancellationToken))
                {
                    return false;
                }
            }
        }

        return true;
    }
}