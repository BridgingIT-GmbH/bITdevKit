// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using System;
using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.Extensions.Localization;

public static class Check
{
    /// <summary>
    /// Checks several entity command rules, throws when not satisfied
    /// </summary>
    public static async Task ThrowAsync<TEntity>(IEnumerable<IEntityCommandRule<TEntity>> rules, TEntity entity, IStringLocalizer localizer = null, Action satisfied = null)
        where TEntity : class, IEntity
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                await ThrowAsync(rule, entity, localizer);
            }
        }

        satisfied?.Invoke();
    }

    public static async Task<TResult> ThrowAsync<TEntity, TResult>(IEnumerable<IEntityCommandRule<TEntity>> rules, TEntity entity, IStringLocalizer localizer = null, Func<TResult> satisfied = null)
        where TEntity : class, IEntity
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                await ThrowAsync(rule, entity, localizer);
            }
        }

        return satisfied != null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks a entity command rule, throws when not satisfied
    /// </summary>
    public static async Task ThrowAsync<TEntity>(IEntityCommandRule<TEntity> rule, TEntity entity, IStringLocalizer localizer = null, Action satisfied = null)
        where TEntity : class, IEntity
    {
        if (rule is null)
        {
            return;
        }

        if (!await rule.IsSatisfiedAsync(entity))
        {
            throw new EntityCommandRuleNotSatisfied($"{rule.GetType().Name}: {(localizer != null ? localizer[rule.Message] : rule.Message)}");
        }

        satisfied?.Invoke();
    }

    /// <summary>
    /// Checks a entity command rule, throws when not satisfied
    /// </summary>
    public static async Task<TResult> ThrowAsync<TEntity, TResult>(IEntityCommandRule<TEntity> rule, TEntity entity, IStringLocalizer localizer = null, Func<TResult> satisfied = null)
        where TEntity : class, IEntity
    {
        if (rule is null)
        {
            return default;
        }

        if (!await rule.IsSatisfiedAsync(entity))
        {
            throw new EntityCommandRuleNotSatisfied($"{rule.GetType().Name}: {(localizer != null ? localizer[rule.Message] : rule.Message)}");
        }

        return satisfied != null ? satisfied.Invoke() : default;
    }

    /// <summary>
    /// Checks several entity command rules, returns satisfied or not
    /// </summary>
    public static async Task<bool> ReturnAsync<TEntity>(IEnumerable<IEntityCommandRule<TEntity>> rules, TEntity entity)
        where TEntity : class, IEntity
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                if (!await ReturnAsync(rule, entity))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Checks a entity command rule, returns satisfied or not
    /// </summary>
    public static async Task<bool> ReturnAsync<TEntity>(IEntityCommandRule<TEntity> rule, TEntity entity)
        where TEntity : class, IEntity
    {
        if (rule is null)
        {
            return true;
        }

        return await rule.IsSatisfiedAsync(entity);
    }
}