// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides extension methods for the rule builder.
/// </summary>
public static class RuleBuilderItemExtensions
{
    /// <summary>
    /// Adds an asynchronous predicate-based rule to the builder.
    /// </summary>
    /// <param name="builder">The rule builder.</param>
    /// <param name="predicate">The asynchronous predicate to evaluate.</param>
    /// <param name="message">The optional message to use if the rule fails.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = await Rule.Add()
    ///     .Add(async token => await Task.FromResult(user.IsActive), "User must be active")
    ///     .FilterAsync(users);
    /// </code>
    /// </example>
    public static RuleBuilder Add(this RuleBuilder builder, Func<CancellationToken, Task<bool>> predicate, string message = null)
    {
        return builder.Add(new AsyncFuncRule(predicate, message));
    }

    /// <summary>
    /// Adds a rule for a specific type using a rule factory.
    /// </summary>
    /// <typeparam name="T">The type of the item to validate.</typeparam>
    /// <param name="builder">The rule builder.</param>
    /// <param name="ruleFactory">The factory function to create the rule.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.Add()
    ///     .Add&lt;PersonStub&gt;(p => RuleSet.IsNotEmpty(p.FirstName))
    ///     .Filter(persons);
    /// </code>
    /// </example>
    public static RuleBuilder Add<T>(this RuleBuilder builder, Func<T, IRule> ruleFactory)
    {
        return builder.Add(new ItemRule<T>(ruleFactory));
    }

    /// <summary>
    /// Adds a rule for a specific type using a rule factory.
    /// </summary>
    /// <typeparam name="T">The type of the item to validate.</typeparam>
    /// <param name="builder">The rule builder.</param>
    /// <param name="ruleFactory">The factory function to create the rule.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// /// <code>
    /// var result = Rule.Add()
    ///     .Add&lt;PersonStub&gt;((p, token) => RuleSet.Contains(p.Email.Value, "@"))
    ///     .Filter(persons);
    /// </code>
    /// </example>
    public static RuleBuilder Add<T>(this RuleBuilder builder, Func<T, CancellationToken, IRule> ruleFactory)
    {
        return builder.Add(new AsyncItemRule<T>((item, token) => Task.FromResult(ruleFactory(item, token))));
    }

    /// <summary>
    /// Adds an asynchronous rule for a specific type using a rule factory.
    /// </summary>
    /// <typeparam name="T">The type of the item to validate.</typeparam>
    /// <param name="builder">The rule builder.</param>
    /// <param name="ruleFactory">The factory function to create the asynchronous rule.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = await Rule.Add()
    ///     .Add&lt;PersonStub&gt;(async (p, token) => await RuleSet.ContainsAsync(p.Email.Value, "@"))
    ///     .FilterAsync(persons);
    /// </code>
    /// </example>
    public static RuleBuilder Add<T>(this RuleBuilder builder, Func<T, CancellationToken, Task<IRule>> ruleFactory)
    {
        return builder.Add(new AsyncItemRule<T>(ruleFactory));
    }

    // // Overload to add ready-made rules from the RuleSet
    // public static RuleBuilder Add<T>(this RuleBuilder builder, Func<T, CancellationToken, IRule> ruleFactory)
    // {
    //     return builder.Add(new AsyncItemRule<T>((item, token) => Task.FromResult(ruleFactory(item, token))));
    // }

    // // Overload to add ready-made asynchronous rules from the RuleSet
    // public static RuleBuilder Add<T>(this RuleBuilder builder, Func<T, CancellationToken, Task<IRule>> ruleFactory)
    // {
    //     return builder.Add(new AsyncItemRule<T>(ruleFactory));
    // }

    /// <summary>
    /// Adds a rule for a specific type using a rule factory.
    /// </summary>
    /// <typeparam name="T">The type of the item to validate.</typeparam>
    /// <param name="builder">The rule builder.</param>
    /// <param name="ruleFactory">The factory function to create the rule.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.Add()
    ///     .And&lt;PersonStub&gt;(p => RuleSet.IsNotEmpty(p.FirstName))
    ///     .Filter(persons);
    /// </code>
    /// </example>
    public static RuleBuilder And<T>(this RuleBuilder builder, Func<T, IRule> ruleFactory)
    {
        return builder.Add(ruleFactory);
    }

    /// <summary>
    /// Adds a predicate-based rule for a specific type.
    /// </summary>
    /// <typeparam name="T">The type of the item to validate.</typeparam>
    /// <param name="builder">The rule builder.</param>
    /// <param name="predicate">The predicate to evaluate.</param>
    /// <param name="message">The optional message to use if the rule fails.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.Add()
    ///     .Add&lt;PersonStub&gt;(p => !string.IsNullOrEmpty(p.LastName), "Last name is required")
    ///     .Filter(persons);
    /// </code>
    /// </example>
    public static RuleBuilder Add<T>(this RuleBuilder builder, Func<T, bool> predicate, string message = null)
    {
        return builder.Add(new ItemRule<T>(item =>
            new FuncRule(() => predicate(item), message)));
    }

    /// <summary>
    /// Adds an asynchronous predicate-based rule for a specific type.
    /// </summary>
    /// <typeparam name="T">The type of the item to validate.</typeparam>
    /// <param name="builder">The rule builder.</param>
    /// <param name="predicate">The asynchronous predicate to evaluate.</param>
    /// <param name="message">The optional message to use if the rule fails.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = await Rule.Add()
    ///     .Add&lt;PersonStub&gt;(async (p, token) => await Task.FromResult(!string.IsNullOrEmpty(p.LastName)), "Last name is required")
    ///     .FilterAsync(persons);
    /// </code>
    /// </example>
    public static RuleBuilder Add<T>(this RuleBuilder builder, Func<T, CancellationToken, Task<bool>> predicate, string message = null)
    {
        return builder.Add(new AsyncItemRule<T>((item, token) =>
            Task.FromResult<IRule>(new AsyncFuncRule(async ct => await predicate(item, ct), message))));
    }
}