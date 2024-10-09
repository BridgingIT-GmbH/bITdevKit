// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using Microsoft.Extensions.Localization;

/// <summary>
///     The DomainRules class provides various methods to apply and evaluate domain rules and specifications.
///     These methods can be used synchronously or asynchronously, with or without localization support,
///     and offer different return types based on the use case.
/// </summary>
public static class DomainRules
{
    /// <summary>
    ///     Checks a business rule, throws when not satisfied
    /// </summary>
    /// <param name="rule">The business rule to be checked.</param>
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
    ///     Applies a business rule and returns a result. Waits for the rule to be applied asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned after applying the rule.</typeparam>
    /// <param name="rule">The business rule to apply.</param>
    /// <returns>The result of the applied business rule.</returns>
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
    ///     Checks a business rule, throws when not satisfied
    /// </summary>
    /// <param name="rule">The business rule to check</param>
    /// <param name="localizer">The localizer for localized messages</param>
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
    ///     Checks a business rule, throws when not satisfied
    /// </summary>
    /// <typeparam name="TResult">The result type returned if the rule is satisfied.</typeparam>
    /// <param name="rule">The business rule to be checked.</param>
    /// <param name="localizer">The localizer for translating any error messages.</param>
    /// <returns>The result of type TResult if the rule is satisfied.</returns>
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
    ///     Checks a business rule with localization support, throws when not satisfied
    /// </summary>
    /// <param name="rule">The domain rule to be checked.</param>
    /// <param name="localizer">The localizer for localization of the rule checks' output.</param>
    /// <param name="satisfied">The action to be executed if the rule is satisfied.</param>
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
    ///     Checks several business rules, throws when not satisfied
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
    ///     Checks a business rule, throws when not satisfied
    /// </summary>
    /// <param name="rules">The domain rules to be applied.</param>
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
    ///     Checks several business rules and throws an exception if any rule is not satisfied
    /// </summary>
    /// <param name="rules">An array of rules to be checked</param>
    /// <param name="localizer">The localizer to provide localized messages</param>
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
    ///     Applies the specified domain rules with localization and an optional action when the rules are satisfied.
    /// </summary>
    /// <param name="rules">The array of domain rules to apply.</param>
    /// <param name="localizer">The localizer used for providing localized error messages.</param>
    /// <param name="satisfied">The action to be executed if the rules are satisfied.</param>
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
    ///     Applies the specified domain rules and returns the result.
    /// </summary>
    /// <param name="rules">The domain rules to be checked.</param>
    /// <param name="localizer">The string localizer used for localization.</param>
    /// <returns>The result of applying the domain rules.</returns>
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
    ///     Checks a business rule asynchronously, throws when not satisfied
    /// </summary>
    /// <param name="rule">The domain rule to be checked</param>
    /// <param name="localizer">The string localizer for localization support</param>
    /// <param name="satisfied">The function to invoke if the rule is satisfied</param>
    /// <returns>The result of the satisfied function</returns>
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
    ///     Applies the given business rule and checks for its satisfaction.
    /// </summary>
    /// <param name="rules">The domain rules to be applied.</param>
    /// <param name="localizer">The string localizer for localization tasks.</param>
    /// <param name="satisfied">The callback function to be executed if the rule is satisfied.</param>
    /// <returns>The result of the satisfied function.</returns>
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
    ///     Applies a specification to an entity and executes the satisfied action if specified.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="specification">The specification to be applied.</param>
    /// <param name="entity">The entity to be validated against the specification.</param>
    /// <param name="satisfied">The action to execute when the specification is satisfied.</param>
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
    ///     Checks several business rules through specified specifications applied to an entity.
    ///     Throws when any specification is not satisfied.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to which the specifications are applied.</typeparam>
    /// <param name="specifications">An array of specifications to be checked.</param>
    /// <param name="entity">The entity against which the specifications are validated.</param>
    /// <param name="satisfied">An optional action to be executed if all specifications are satisfied.</param>
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
    ///     Checks a specification against an entity, throws when not satisfied.
    /// </summary>
    /// <param name="specifications">An array of specifications to be applied to the entity.</param>
    /// <param name="entity">The entity to validate against the specifications.</param>
    /// <param name="satisfied">An optional function to be executed if all specifications are satisfied.</param>
    /// <returns>A result of type TResult if the specifications are satisfied; otherwise, the default value of TResult.</returns>
    public static TResult Apply<TEntity, TResult>(
        ISpecification<TEntity>[] specifications,
        TEntity entity,
        Func<TResult> satisfied = null)
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
    ///     Checks a business rule, throws when not satisfied
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="specification">The specification to be checked.</param>
    /// <param name="entity">The entity to be evaluated against the specification.</param>
    /// <param name="satisfied">The function to execute if the rule is satisfied, returning a result of type TResult.</param>
    /// <returns>The result of the satisfied function if the rule is satisfied; otherwise, default value of TResult.</returns>
    public static TResult Apply<TEntity, TResult>(
        ISpecification<TEntity> specification,
        TEntity entity,
        Func<TResult> satisfied = null)
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
    ///     Checks several business rules asynchronously, throws when not satisfied.
    /// </summary>
    /// <param name="rules">The array of domain rules to be validated.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ApplyAsync(IDomainRule[] rules, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(rules, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Asynchronously checks several business rules, throws when not satisfied
    /// </summary>
    /// <param name="rules">An array of rules to be applied.</param>
    /// <param name="localizer">A string localizer for localization purposes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task ApplyAsync(
        IDomainRule[] rules,
        IStringLocalizer localizer,
        CancellationToken cancellationToken = default)
    {
        await ApplyAsync(rules, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Asynchronously checks several business rules, throws when not satisfied
    /// </summary>
    /// <param name="rules">The array of business rules to check</param>
    /// <param name="localizer">The string localizer for localization</param>
    /// <param name="satisfied">The action to invoke when all rules are satisfied</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task ApplyAsync(
        IDomainRule[] rules,
        IStringLocalizer localizer,
        Action satisfied,
        CancellationToken cancellationToken = default)
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                await ApplyAsync(rule, localizer, cancellationToken);
            }
        }

        satisfied?.Invoke();
    }

    /// <summary>
    ///     Asynchronously checks a business rule and throws an exception if it is not satisfied.
    /// </summary>
    /// <param name="rule">The business rule to be checked.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ApplyAsync(IDomainRule rule, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(rule, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Asynchronously applies a domain rule and localizes the result.
    /// </summary>
    /// <param name="rule">The domain rule to be applied.</param>
    /// <param name="localizer">The string localizer used for localization.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task ApplyAsync(
        IDomainRule rule,
        IStringLocalizer localizer,
        CancellationToken cancellationToken = default)
    {
        await ApplyAsync(rule, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Asynchronously checks a specified business rule and throws an exception if the rule is not satisfied.
    /// </summary>
    /// <param name="rule">The business rule to check.</param>
    /// <param name="localizer">The localizer for translating rule messages.</param>
    /// <param name="satisfied">The action to invoke if the rule is satisfied.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="DomainRuleException">Thrown when the business rule is not satisfied.</exception>
    public static async Task ApplyAsync(
        IDomainRule rule,
        IStringLocalizer localizer,
        Action satisfied,
        CancellationToken cancellationToken = default)
    {
        if (rule is null || !await rule.IsEnabledAsync(cancellationToken))
        {
            return;
        }

        if (!await rule.ApplyAsync(cancellationToken))
        {
            throw new DomainRuleException(
                $"{rule.GetType().Name} {(localizer is not null ? localizer[rule.Message] : rule.Message)}");
        }

        satisfied?.Invoke();
    }

    /// <summary>
    ///     Asynchronously checks a business rule and throws an exception if not satisfied.
    /// </summary>
    /// <param name="rule">The business rule to be checked.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <typeparam name="TResult">The type of result the rule returns if satisfied.</typeparam>
    /// <returns>The result of the business rule if it is satisfied.</returns>
    public static async Task<TResult> ApplyAsync<TResult>(
        IDomainRule rule,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAsync<TResult>(rule, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Checks a business rule asynchronously, throws when not satisfied
    /// </summary>
    /// <param name="rule">The business rule to be checked</param>
    /// <param name="localizer">The string localizer for localization messages</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation</param>
    /// <returns>A task representing the result of the business rule check</returns>
    public static async Task<TResult> ApplyAsync<TResult>(
        IDomainRule rule,
        IStringLocalizer localizer,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAsync<TResult>(rule, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Asynchronously checks a business rule using a localizer and a satisfaction function. Throws an exception when not
    ///     satisfied.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned if the rule is satisfied.</typeparam>
    /// <param name="rule">The business rule to be checked.</param>
    /// <param name="localizer">The localizer for obtaining localized messages.</param>
    /// <param name="satisfied">The function to be executed if the rule is satisfied.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     The result of the <paramref name="satisfied" /> function if the rule is satisfied; otherwise, the default
    ///     value of <typeparamref name="TResult" />.
    /// </returns>
    public static async Task<TResult> ApplyAsync<TResult>(
        IDomainRule rule,
        IStringLocalizer localizer,
        Func<TResult> satisfied,
        CancellationToken cancellationToken = default)
    {
        if (rule is null || !await rule.IsEnabledAsync(cancellationToken))
        {
            return default;
        }

        if (!await rule.ApplyAsync(cancellationToken))
        {
            throw new DomainRuleException(
                $"{rule.GetType().Name} {(localizer is not null ? localizer[rule.Message] : rule.Message)}");
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    ///     Checks several business rules asynchronously and throws if any are not satisfied.
    /// </summary>
    /// <param name="rules">An array of domain rules to apply.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains the value obtained after applying the
    ///     rules.
    /// </returns>
    public static async Task<TResult> ApplyAsync<TResult>(
        IDomainRule[] rules,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAsync<TResult>(rules, null, null, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Checks several business rules asynchronously, throws when not satisfied
    /// </summary>
    /// <param name="rules">The business rules to be checked</param>
    /// <param name="localizer">The string localizer instance for localization</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>A task that represents the asynchronous operation, containing the result of the rule checks</returns>
    public static async Task<TResult> ApplyAsync<TResult>(
        IDomainRule[] rules,
        IStringLocalizer localizer,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAsync<TResult>(rules, localizer, null, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Checks several business rules asynchronously and returns the result of the satisfied function if all rules are
    ///     satisfied.
    /// </summary>
    /// <param name="rules">An array of domain rules to be checked.</param>
    /// <param name="localizer">An optional string localizer used for localization purposes.</param>
    /// <param name="satisfied">A function to be invoked and return a result if all rules are satisfied.</param>
    /// <param name="cancellationToken">An optional cancellation token to cancel the asynchronous operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing the result of the satisfied function if all
    ///     rules are satisfied.
    /// </returns>
    public static async Task<TResult> ApplyAsync<TResult>(
        IDomainRule[] rules,
        IStringLocalizer localizer,
        Func<TResult> satisfied,
        CancellationToken cancellationToken = default)
    {
        if (rules?.Any() == true)
        {
            foreach (var rule in rules)
            {
                await ApplyAsync(rule, localizer, cancellationToken);
            }
        }

        return satisfied is not null ? satisfied.Invoke() : default;
    }

    /// <summary>
    ///     Checks a business rule, returns whether it is satisfied or not.
    /// </summary>
    /// <param name="rule">The domain rule to be checked.</param>
    /// <returns>True if the rule is satisfied, otherwise false.</returns>
    public static bool Return(IDomainRule rule)
    {
        var task = Task.Run(() => ReturnAsync(rule));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        return task.Result;
    }

    /// <summary>
    ///     Checks several business rules, returning whether they are all satisfied.
    /// </summary>
    /// <param name="rules">An array of business rules to be checked.</param>
    /// <returns>True if all rules are satisfied, otherwise false.</returns>
    public static bool Return(IDomainRule[] rules)
    {
        var task = Task.Run(() => ReturnAsync(rules));

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        return task.Result;
    }

    /// <summary>
    ///     Checks a specification and returns if it is satisfied by the given entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity being checked.</typeparam>
    /// <param name="specification">The specification to check against.</param>
    /// <param name="entity">The entity to check.</param>
    /// <return>true if the entity satisfies the specification; otherwise, false.</return>
    public static bool Return<TEntity>(ISpecification<TEntity> specification, TEntity entity)
    {
        if (specification is null)
        {
            return true;
        }

        return specification.IsSatisfiedBy(entity);
    }

    /// <summary>
    ///     Checks several business rules, returns satisfied or not
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="specifications">An array of specifications to be applied to the entity.</param>
    /// <param name="entity">The entity to which the specifications will be applied.</param>
    /// <returns>A boolean indicating whether all the specifications are satisfied.</returns>
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
    ///     Checks a business rule asynchronously and returns whether it is satisfied.
    /// </summary>
    /// <param name="rule">The domain rule to be checked.</param>
    /// <param name="cancellationToken">An optional token to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean indicating whether the
    ///     rule is satisfied or not.
    /// </returns>
    public static async Task<bool> ReturnAsync(IDomainRule rule, CancellationToken cancellationToken = default)
    {
        if (rule is null || !await rule.IsEnabledAsync(cancellationToken))
        {
            return true;
        }

        return await rule.ApplyAsync(cancellationToken);
    }

    /// <summary>
    ///     Asynchronously checks multiple business rules and returns whether all of them are satisfied.
    /// </summary>
    /// <param name="rules">An array of business rules to be checked.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean indicating whether all
    ///     rules are satisfied.
    /// </returns>
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