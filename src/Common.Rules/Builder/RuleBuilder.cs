// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a fluent interface for building and executing collections of rules.
/// <para>
/// Usage example:
/// <code>
///     return Rule.For()
///         // Basic validation using ValueRules
///         .Add(Rules.IsNotEmpty(product.Name))
///         .Add(Rules.StringLength(product.Name, 3, 100))
///         .Add(Rules.NumericRange(product.Price, 0.01m, 999.99m))
///
///         // Conditional validation with When
///         .When(!product.IsDigital, builder => builder
///             .Add(Rules.IsNotEmpty(product.ShippingAddress))
///             .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
///
///         // Using Unless (inverse of When)
///         .Unless(product.IsDigital,
///             Rules.GreaterThan(product.Price, 10m))
///
///         // Combining multiple conditions
///         .WhenAll(new[]
///         {
///             product.Price > 100,
///             product.IsDigital,
///             product.Categories?.Count > 2
///         }, Rules.IsNotEmpty(product.ShippingAddress))
///         .Apply();
/// </code>
/// </para>
/// </summary>
public class RuleBuilder
{
    private readonly List<IRule> rules = [];
    private bool continueOnRuleFailure = Rule.Settings.ContinueOnRuleFailure;
    private bool throwOnFailure = Rule.Settings.ThrowOnRuleFailure;

    private bool throwOnException = Rule.Settings.ThrowOnRuleException;

    /// <summary>
    /// Adds a rule to the builder.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .Add(Rules.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Add(IRule rule)
    {
        if (rule is not null)
        {
            this.rules.Add(rule);
        }

        return this;
    }

    /// <summary>
    /// Adds multiple rules to the builder.
    /// </summary>
    /// <param name="rules">The rules to add.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name), Rules.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Add(params IRule[] rules)
    {
        foreach (var rule in rules)
        {
            this.Add(rule);
        }

        return this;
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate to the builder.
    /// </summary>
    /// <param name="predicate">The boolean predicate to evaluate.</param>
    /// <param name="message"></param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(() => user.IsActive)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Add(Func<bool> predicate, string message = null)
    {
        return this.Add(new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions to the builder.
    /// </summary>
    /// <param name="expressions">The boolean expressions to evaluate.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(() => user.IsActive, () => user.HasValidSubscription)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Add(params Func<bool>[] expressions)
    {
        foreach (var predicate in expressions)
        {
            this.Add(new FuncRule(predicate));
        }

        return this;
    }

    /// <summary>
    /// Creates an empty rule builder.
    /// </summary>
    /// <returns>A new empty rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(Rules.IsNotEmpty(user.Name))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder For()
    {
        return new RuleBuilder();
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rule.
    /// </summary>
    /// <param name="rule">The initial rule.</param>
    /// <returns>A new rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For(Rules.IsNotEmpty(user.Email))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder For(IRule rule)
    {
        return new RuleBuilder().Add(rule);
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rules.
    /// </summary>
    /// <param name="rules">The initial rules.</param>
    /// <returns>A new rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For(
    ///     Rules.IsNotEmpty(order.Id),
    ///     Rules.NumericRange(order.Amount, 1, 1000))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder For(params IRule[] rules)
    {
        var builder = new RuleBuilder();
        foreach (var rule in rules)
        {
            builder.Add(rule);
        }

        return builder;
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified boolean predicate.
    /// </summary>
    /// <param name="predicate">The boolean predicate to evaluate.</param>
    /// <param name="message"></param>
    /// <returns>A new rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For(() => user.IsActive)
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder For(Func<bool> predicate, string message = null)
    {
        return new RuleBuilder().Add(new FuncRule(predicate, message));
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified boolean expressions.
    /// </summary>
    /// <param name="expressions">The boolean expressions to evaluate.</param>
    /// <returns>A new rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For(
    ///     () => user.IsActive,
    ///     () => user.HasValidSubscription)
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder For(params Func<bool>[] expressions)
    {
        var builder = new RuleBuilder();
        foreach (var predicate in expressions)
        {
            builder.Add(new FuncRule(predicate));
        }
        return builder;
    }

    /// <summary>
    /// Enables error aggregation, collecting all rule failures instead of stopping at the first failure.
    /// </summary>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .ContinueOnFailure()
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder ContinueOnFailure()
    {
        this.continueOnRuleFailure = true;

        return this;
    }

    /// <summary>
    /// Sets whether to throw an exception if a rule fails.
    /// </summary>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception on rule failure.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .ThrowOnFailure(true)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder ThrowOnFailure(bool throwOnRuleFailure = true)
    {
        this.throwOnFailure = throwOnRuleFailure;

        return this;
    }

    /// <summary>
    /// Sets whether to throw an exception if a rule throws an exception.
    /// </summary>
    /// <param name="throwOnRuleException">Indicates whether to throw an exception on rule exception.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .ThrowOnException(true)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder ThrowOnException(bool throwOnRuleException = true)
    {
        this.throwOnException = throwOnRuleException;

        return this;
    }

    // Grouping When overloads together
    /// <summary>
    /// Adds a rule when the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(product.IsDigital, Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder When(bool condition, IRule rule)
    {
        return condition ? this.Add(rule) : this;
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(product.RequiresShipping, builder => builder
    ///         .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///         .Add(Rules.IsValidPostalCode(product.PostalCode)))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder When(bool condition, Action<RuleBuilder> addRules)
    {
        if (condition)
        {
            addRules(this);
        }

        return this;
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(product.IsDigital, () => product.HasValidDownloadUrl)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder When(bool condition, Func<bool> predicate, string message = null)
    {
        return this.When(condition, new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions when the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="expressions">The boolean expressions to evaluate if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(product.RequiresVerification, () => product.HasVerifiedEmail, () => product.HasVerifiedPhone)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder When(bool condition, params Func<bool>[] expressions)
    {
        if (condition)
        {
            foreach (var predicate in expressions)
            {
                this.Add(new FuncRule(predicate));
            }
        }

        return this;
    }

    // Grouping WhenAll overloads together
    /// <summary>
    /// Adds a rule when all of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAll(new[] { user.IsActive, user.HasPremium, !user.IsBlocked },
    ///         Rules.IsNotEmpty(user.PaymentMethod))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAll(IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.All(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when all of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAll(new[] { user.IsActive, user.HasPremium, !user.IsBlocked },
    ///         builder => builder
    ///             .Add(Rules.IsNotEmpty(user.PaymentMethod))
    ///             .Add(Rules.IsValidCreditCard(user.CardNumber)))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAll(IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return this.When(conditions.All(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when all specified conditions are met.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAll(new[] { user.IsActive, user.HasSubscription }, () => user.HasAccess)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAll(IEnumerable<bool> conditions, Func<bool> predicate, string message = null)
    {
        return this.WhenAll(conditions, new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions when all specified conditions are met.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="expressions">The boolean expressions to evaluate if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAll(new[] { order.IsPaid, order.IsShipped }, () => order.HasTrackingNumber, () => order.HasDeliveryDate)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAll(IEnumerable<bool> conditions, params Func<bool>[] expressions)
    {
        if (conditions.All(c => c))
        {
            foreach (var predicate in expressions)
            {
                this.Add(new FuncRule(predicate));
            }
        }

        return this;
    }

    // Grouping WhenAny overloads together
    /// <summary>
    /// Adds a rule when any of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAny(new[] { order.IsRush, order.IsInternational, order.Value > 1000 },
    ///         Rules.IsNotEmpty(order.SpecialInstructions))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAny(IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.Any(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when any of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAny(new[] { order.IsRush, order.IsInternational, order.Value > 1000 },
    ///         builder => builder
    ///             .Add(Rules.IsNotEmpty(order.SpecialInstructions))
    ///             .Add(Rules.MinLength(order.Notes, 50)))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAny(IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return this.When(conditions.Any(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when any of the specified conditions are met.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAny(new[] { user.IsAdmin, user.IsModerator }, () => user.HasFullAccess)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAny(IEnumerable<bool> conditions, Func<bool> predicate, string message = null)
    {
        return this.WhenAny(conditions, new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions when any of the specified conditions are met.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="expressions">The boolean expressions to evaluate if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAny(new[] { order.IsInternational, order.IsExpress }, () => order.RequiresCustomHandling, () => order.HasSpecialInstructions)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAny(IEnumerable<bool> conditions, params Func<bool>[] expressions)
    {
        if (conditions.Any(c => c))
        {
            foreach (var predicate in expressions)
            {
                this.Add(new FuncRule(predicate));
            }
        }

        return this;
    }

    // Grouping WhenNone overloads together
    /// <summary>
    /// Adds a rule when none of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if no conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenNone(new[] { user.IsAdmin, user.IsModerator, user.HasSpecialAccess },
    ///         Rules.LessThan(order.Amount, 1000))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenNone(IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(!conditions.Any(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when none of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param rules="addRules">Action to add rules if no conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenNone(new[] { user.IsAdmin, user.IsModerator, user.HasSpecialAccess },
    ///         builder => builder
    ///             .Add(Rules.LessThan(order.Amount, 1000))
    ///             .Add(Rules.IsNotEmpty(order.SpecialInstructions)))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenNone(IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return this.When(!conditions.Any(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when none of the specified conditions are met.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if none of the conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenNone(new[] { user.HasPremium, user.HasGoldMembership }, () => user.HasBasicAccess)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenNone(IEnumerable<bool> conditions, Func<bool> predicate, string message = null)
    {
        return this.WhenNone(conditions, new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions when none of the specified conditions are met.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="expressions">The boolean expressions to evaluate if none of the conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenNone(new[] { product.IsOnSale, product.IsClearance }, () => product.HasStandardPricing, () => product.HasRegularWarranty)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenNone(IEnumerable<bool> conditions, params Func<bool>[] expressions)
    {
        if (!conditions.Any(c => c))
        {
            foreach (var predicate in expressions)
            {
                this.Add(new FuncRule(predicate));
            }
        }

        return this;
    }

    // Grouping WhenExactly overloads together
    /// <summary>
    /// Adds a rule when the exact number of conditions are true.
    /// </summary>
    /// <param name="count">The expected number of true conditions.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the exact count matches.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenExactly(2, new[] {
    ///         subscription.HasBasicPlan,
    ///         subscription.HasPremiumFeature,
    ///         subscription.HasEnterpriseSupport
    ///     }, Rules.IsNotEmpty(subscription.BillingPlan))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenExactly(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.Count(c => c) == count, rule);
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when exactly a specified number of conditions are met.
    /// </summary>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the exact count is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenExactly(2, new[] { user.HasVerifiedEmail, user.HasVerifiedPhone, user.HasVerifiedAddress }, () => user.IsFullyVerified)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenExactly(int count, IEnumerable<bool> conditions, Func<bool> predicate, string message = null)
    {
        return this.WhenExactly(count, conditions, new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions when exactly a specified number of conditions are met.
    /// </summary>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="expressions">The boolean expressions to evaluate if the exact count is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenExactly(3, new[] { order.HasGiftWrapping, order.HasInsurance, order.HasPriorityShipping }, () => order.IsPremium)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenExactly(int count, IEnumerable<bool> conditions, params Func<bool>[] expressions)
    {
        if (conditions.Count(c => c) == count)
        {
            foreach (var predicate in expressions)
            {
                this.Add(new FuncRule(predicate));
            }
        }

        return this;
    }

    // Grouping WhenAtLeast overloads together
    /// <summary>
    /// Adds a rule when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The minimum number of true conditions required.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the minimum count is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAtLeast(2, new[] {
    ///         document.HasSignature,
    ///         document.HasTimestamp,
    ///         document.HasWatermark,
    ///         document.HasDigitalSeal
    ///     }, Rules.IsNotEmpty(document.ValidatedBy))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAtLeast(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.Count(c => c) >= count, rule);
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when at least a specified number of conditions are met.
    /// </summary>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the minimum count is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAtLeast(2, new[] { subscription.HasBasicPlan, subscription.HasPremiumFeature }, () => subscription.HasAdvancedOptions)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAtLeast(int count, IEnumerable<bool> conditions, Func<bool> predicate, string message = null)
    {
        return this.WhenAtLeast(count, conditions, new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions when at least a specified number of conditions are met.
    /// </summary>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="expressions">The boolean expressions to evaluate if the minimum count is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAtLeast(2, new[] { user.HasAdminRole, user.HasModeratorRole, user.HasEditorRole }, () => user.HasExtendedPermissions, () => user.HasAccessToSensitiveData)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAtLeast(int count, IEnumerable<bool> conditions, params Func<bool>[] expressions)
    {
        if (conditions.Count(c => c) >= count)
        {
            foreach (var predicate in expressions)
            {
                this.Add(new FuncRule(predicate));
            }
        }

        return this;
    }

    // Grouping WhenAtMost overloads together
    /// <summary>
    /// Adds a rule when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The maximum number of true conditions allowed.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the count doesn't exceed the maximum.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAtMost(2, new[] {
    ///         user.HasBasicRole,
    ///         user.HasModeratorRole,
    ///         user.HasAdminRole
    ///     }, Rules.LessThan(transaction.Amount, 5000))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAtMost(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.Count(c => c) <= count, rule);
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when at most a specified number of conditions are met.
    /// </summary>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the maximum count is not exceeded.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAtMost(1, new[] { user.HasBasicAccess, user.HasLimitedAccess }, () => user.HasGuestAccess)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAtMost(int count, IEnumerable<bool> conditions, Func<bool> predicate, string message = null)
    {
        return this.WhenAtMost(count, conditions, new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions when at most a specified number of conditions are met.
    /// </summary>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="expressions">The boolean expressions to evaluate if the maximum count is not exceeded.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAtMost(2, new[] { document.HasSignature, document.HasTimestamp, document.HasWatermark }, () => document.IsVerified, () => document.IsEncrypted)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenAtMost(int count, IEnumerable<bool> conditions, params Func<bool>[] expressions)
    {
        if (conditions.Count(c => c) <= count)
        {
            foreach (var predicate in expressions)
            {
                this.Add(new FuncRule(predicate));
            }
        }

        return this;
    }

    // Grouping WhenBetween overloads together
    /// <summary>
    /// Adds a rule when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="min">The minimum number of true conditions required.</param>
    /// <param name="max">The maximum number of true conditions allowed.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the count falls within range.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenBetween(2, 4, new[] {
    ///         product.HasWarranty,
    ///         product.IsInsured,
    ///         product.HasExtendedSupport,
    ///         product.HasPriorityShipping,
    ///         product.HasGiftWrapping
    ///     }, Rules.GreaterThan(product.Price, 100))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenBetween(int min, int max, IEnumerable<bool> conditions, IRule rule)
    {
        var trueCount = conditions.Count(c => c);

        return this.When(trueCount >= min && trueCount <= max, rule);
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when the number of true conditions falls within a specified range.
    /// </summary>
    /// <param name="min">The minimum number of conditions that must be true.</param>
    /// <param name="max">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the count falls within the range.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenBetween(1, 3, new[] { user.HasBasicRole, user.HasModeratorRole, user.HasAdminRole, user.HasSuperUserRole }, () => user.HasFullAccess)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenBetween(int min, int max, IEnumerable<bool> conditions, Func<bool> predicate, string message = null)
    {
        var trueCount = conditions.Count(c => c);
        return this.When(trueCount >= min && trueCount <= max, new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions when the number of true conditions falls within a specified range.
    /// </summary>
    /// <param name="min">The minimum number of conditions that must be true.</param>
    /// <param name="max">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="expressions">The boolean expressions to evaluate if the count falls within the range.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenBetween(2, 4, new[] { product.HasWarranty, product.IsInsured, product.HasExtendedSupport, product.HasPriorityShipping }, () => product.IsPremiumProduct, () => product.HasExclusiveFeatures)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder WhenBetween(int min, int max, IEnumerable<bool> conditions, params Func<bool>[] expressions)
    {
        var trueCount = conditions.Count(c => c);
        if (trueCount >= min && trueCount <= max)
        {
            foreach (var predicate in expressions)
            {
                this.Add(new FuncRule(predicate));
            }
        }

        return this;
    }

    /// <summary>
    /// Adds a rule with an asynchronous condition.
    /// </summary>
    /// <param name="condition">An async function that determines if the rule should be executed.</param>
    /// <param name="rule">The rule to execute if the condition is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAsync(async token => await IsCustomerActive(customerId, token),
    ///         Rules.IsNotEmpty(order.BillingAddress))
    ///     .ApplyAsync();
    /// </code>
    /// </example>
    public RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, IRule rule)
    {
        return this.Add(new AsyncConditionalRule(condition, rule));
    }

    /// <summary>
    /// Adds multiple rules with an asynchronous condition.
    /// </summary>
    /// <param name="condition">An async function that determines if the rules should be executed.</param>
    /// <param name="addRules">Action to add rules if the condition is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAsync(async token => await HasActiveSubscription(userId, token),
    ///         builder => builder
    ///             .Add(Rules.IsNotEmpty(order.PaymentMethod))
    ///             .Add(Rules.IsValidCreditCard(order.CardNumber)))
    ///     .ApplyAsync();
    /// </code>
    /// </example>
    public RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, Action<RuleBuilder> addRules)
    {
        var builder = new RuleBuilder();
        addRules(builder);

        foreach (var rule in builder.rules)
        {
            this.Add(new AsyncConditionalRule(condition, rule));
        }

        return this;
    }

    /// <summary>
    /// Adds a rule when the specified condition is false.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .Unless(product.IsDigital, Rules.IsEmpty(product.DownloadUrl))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Unless(bool condition, IRule rule)
    {
        if (!condition)
        {
            this.Add(rule);
        }

        return this;
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is false.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .Unless(product.IsDigital, builder => builder
    ///         .Add(Rules.IsEmpty(product.ShippingAddress))
    ///         .Add(Rules.IsValidPostalCode(product.PostalCode)))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Unless(bool condition, Action<RuleBuilder> addRules)
    {
        if (!condition)
        {
            addRules(this);
        }
        return this;
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when the specified condition is false.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the condition is false.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .Unless(product.IsDigital, () => string.IsNullOrEmpty(product.DownloadUrl), "Download URL must be empty for non-digital products.")
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Unless(bool condition, Func<bool> predicate, string message = null)
    {
        if (!condition)
        {
            this.Add(new FuncRule(() => predicate(), message));
        }
        return this;
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions when the specified condition is false.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="expressions">The boolean expressions to evaluate if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .Unless(product.IsDigital, () => string.IsNullOrEmpty(product.DownloadUrl), () => product.HasValidShippingAddress)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Unless(bool condition, params Func<bool>[] expressions)
    {
        if (!condition)
        {
            foreach (var expr in expressions)
            {
                this.Add(new FuncRule(expr));
            }
        }
        return this;
    }

    /// <summary>
    /// Applies the rules synchronously to validate a single object or state.
    /// If continueOnFailure is disabled (default), stops at the first rule failure.
    /// If continueOnFailure is enabled, collects all rule failures.
    /// </summary>
    /// <returns>A Result object indicating the success or failure of applying the rules.</returns>
    /// <example>
    /// <code>
    /// var result = Rules.For()
    ///     .Add(RuleSet.IsNotEmpty(product.Name))
    ///     .Add(RuleSet.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .When(!product.IsDigital, builder => builder
    ///         .Add(RuleSet.IsNotEmpty(product.ShippingAddress))
    ///         .Add(RuleSet.HasStringLength(product.ShippingAddress, 10, 200)))
    ///     .ContinueOnFailure()
    ///     .Apply();
    /// </code>
    /// </example>
    public Result Apply()
    {
        if (!this.rules.Any())
        {
            //logger.LogDebug("{LogKey} rules - no rules defined", Constants.LogKey);

            return Result.Success();
        }

        if (!this.continueOnRuleFailure)
        {
            foreach (var rule in this.rules)
            {
                var result = Rule.Apply(rule, this.throwOnFailure, this.throwOnException);
                if (result.IsFailure)
                {
                    //logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                    return result;
                }

                //logger.LogDebug("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
            }

            return Result.Success();
        }

        var errors = new List<IResultError>();
        var hasFailures = false;

        foreach (var rule in this.rules)
        {
            var result = Rule.Apply(rule, false);
            if (!result.IsFailure)
            {
                //logger.LogDebug("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                continue;
            }

            //logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

            hasFailures = true;
            errors.AddRange(result.Errors);
        }

        return hasFailures
            ? Result.Failure().WithErrors(errors)
            : Result.Success();
    }

    /// <summary>
    /// Applies the rules asynchronously to validate a single object or state.
    /// If continueOnFailure is disabled (default), stops at the first rule failure.
    /// If continueOnFailure is enabled, collects all rule failures.
    /// Supports async conditions and rules.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result object indicating the success or failure of applying the rules.</returns>
    /// <example>
    /// <code>
    /// var result = await Rules.For()
    ///     .Add(RuleSet.IsNotEmpty(order.CustomerId))
    ///     .Add(RuleSet.All(order.Items, item =>
    ///         RuleSet.GreaterThan(item.Quantity, 0)))
    ///     .WhenAsync(
    ///         async (token) => await IsCustomerActive(order.CustomerId, token),
    ///         RuleSet.IsNotNull(order.ShippingAddress))
    ///     .ContinueOnFailure()
    ///     .ApplyAsync(cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (!this.rules.Any())
        {
            // logger.LogDebug("{LogKey} rules - no rules defined", Constants.LogKey);

            return Result.Success();
        }

        if (!this.continueOnRuleFailure)
        {
            foreach (var rule in this.rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await Rule.ApplyAsync(rule, this.throwOnFailure, this.throwOnException, cancellationToken).AnyContext();
                if (result.IsFailure)
                {
                    // logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                    return result;
                }

                // logger.LogDebug("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
            }

            return Result.Success();
        }

        var errors = new List<IResultError>();
        var hasFailures = false;

        foreach (var rule in this.rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await Rule.ApplyAsync(rule, this.throwOnFailure, this.throwOnException, cancellationToken).AnyContext();
            if (!result.IsFailure)
            {
                // logger.LogDebug("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                continue;
            }

            // logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
            hasFailures = true;
            errors.AddRange(result.Errors);
        }

        return hasFailures
            ? Result.Failure().WithErrors(errors)
            : Result.Success();
    }

    /// <summary>
    /// Filters a collection of items based on the defined rules synchronously.
    /// Items are excluded from the result if they fail any rule validation.
    /// </summary>
    /// <typeparam name="T">The type of items to filter.</typeparam>
    /// <param name="items">The collection of items to filter.</param>
    /// <returns>A Result containing the filtered collection.</returns>
    /// <example>
    /// <code>
    /// var result = Rules.For()
    ///     .Add(Product p => RuleSet.IsNotEmpty(p.Name))
    ///     .Add(Product p => RuleSet.NumericRange(p.Price, 0.01m, 999.99m))
    ///     .When(!isDigital, builder => builder
    ///         .Add(Product p => RuleSet.IsNotEmpty(p.ShippingAddress)))
    ///     .Filter(products);
    /// </code>
    /// </example>
    public Result<IEnumerable<T>> Filter<T>(IEnumerable<T> items)
    {
        if (!this.rules.Any())
        {
            return HandleEmptyInput();
        }

        var filteredItems = new List<T>();

        foreach (var item in items)
        {
            var result = EvaluateRules(item);
            if (result.IsSuccess)
            {
                filteredItems.Add(item);
            }
        }

        return Result<IEnumerable<T>>.Success(filteredItems);

        Result<IEnumerable<T>> HandleEmptyInput()
        {
            return items is null
                ? Result<IEnumerable<T>>.Success(Array.Empty<T>())
                : Result<IEnumerable<T>>.Success(items);
        }

        Result EvaluateRules(T item)
        {
            foreach (var rule in this.rules)
            {
                if (rule is ItemRule<T> itemRule)
                {
                    itemRule.SetItem(item);
                }

                var result = Rule.Apply(rule);
                if (result.IsFailure)
                {
                    return Result.Failure();
                }
            }

            return Result.Success();
        }
    }

    /// <summary>
    /// Filters a collection of items based on the defined rules asynchronously.
    /// Items are excluded from the result if they fail any rule validation.
    /// </summary>
    /// <typeparam name="T">The type of items to filter.</typeparam>
    /// <param name="items">The collection of items to filter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result containing the filtered collection.</returns>
    /// <example>
    /// <code>
    /// var result = await Rules.For()
    ///     .Add(User u => RuleSet.IsNotEmpty(u.Email))
    ///     .Add(User u => RuleSet.IsValidEmail(u.Email))
    ///     .When(isActive, builder => builder
    ///         .Add(User u => RuleSet.IsNotNull(u.LastLoginDate)))
    ///     .FilterAsync(users, cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<T>>> FilterAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        if (!this.rules.Any())
        {
            return HandleEmptyInput();
        }

        var filteredItems = new List<T>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await EvaluateRules(item);
            if (result.IsSuccess)
            {
                filteredItems.Add(item);
            }
        }

        return Result<IEnumerable<T>>.Success(filteredItems);

        Result<IEnumerable<T>> HandleEmptyInput()
        {
            return items is null
                ? Result<IEnumerable<T>>.Success(Array.Empty<T>())
                : Result<IEnumerable<T>>.Success(items);
        }

        async Task<Result> EvaluateRules(T item)
        {
            foreach (var rule in this.rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (rule is ItemRule<T> itemRule)
                {
                    itemRule.SetItem(item);
                }

                var result = await Rule.ApplyAsync(rule, false, false, cancellationToken).AnyContext();
                if (result.IsFailure)
                {
                    return Result.Failure();
                }
            }

            return Result.Success();
        }
    }

    /// <summary>
    /// Applies the rules with configurable failure handling.
    /// </summary>
    /// <param name="throwOnRuleFailure">When true, failures throw exceptions. When false, failures return Result.Failure</param>
    /// <returns>A Result indicating success or failure</returns>
    /// <exception>When validation fails and throwOnRuleFailure is true
    ///     <cref>RuleValidationException</cref>
    /// </exception>
    /// <exception cref="RuleException">When rule execution throws an error</exception>
    /// <example>
    /// <code>
    /// var result = Rule.For()
    ///     .Add(RuleSet.IsNotEmpty(product.Name))
    ///     .Add(RuleSet.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .When(!product.IsDigital, builder => builder
    ///         .Add(RuleSet.IsNotEmpty(product.ShippingAddress)))
    ///     .Throw(); // Throws if validation fails
    /// </code>
    /// </example>
    public Result Throw(bool throwOnRuleFailure = true)
    {
        return this
            .ThrowOnFailure(throwOnRuleFailure)
            .ThrowOnException()
            .Apply();
    }

    /// <summary>
    /// Asynchronously applies the rules with configurable failure handling.
    /// </summary>
    /// <param name="throwOnRuleFailure">When true, failures throw exceptions. When false, failures return Result.Failure</param>
    /// <returns>A Task containing a Result indicating success or failure</returns>
    /// <exception>When validation fails and throwOnRuleFailure is true</exception>
    /// <exception cref="RuleException">When rule execution throws an error</exception>
    /// <example>
    /// <code>
    /// var result = await Rule.For()
    ///     .Add(RuleSet.IsNotEmpty(order.CustomerId))
    ///     .Add(RuleSet.All(order.Items, item =>
    ///         RuleSet.GreaterThan(item.Quantity, 0)))
    ///     .WhenAsync(
    ///         async (token) => await IsCustomerActive(order.CustomerId, token),
    ///         RuleSet.IsNotNull(order.ShippingAddress))
    ///     .ThrowAsync(); // Throws if validation fails
    /// </code>
    /// </example>
    public async Task<Result> ThrowAsync(bool throwOnRuleFailure = true)
    {
        return await this
            .ThrowOnFailure(throwOnRuleFailure)
            .ThrowOnException()
            .ApplyAsync().AnyContext();
    }
}