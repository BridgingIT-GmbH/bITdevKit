// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// The RuleSettingsBuilder class is responsible for configuring and creating instances
/// of the RuleSettings class. It allows configuring various properties such as logging,
/// error handling, and rule execution behavior.
/// </summary>
public class RuleSettingsBuilder
{
    private IRuleLogger logger;
    private bool continueOnRuleFailure;
    private bool throwOnRuleFailure;
    private bool throwOnRuleException;
    private Func<IRule, RuleException> ruleFailureExceptionFactory;
    private Func<IRule, Exception, IResultError> ruleExceptionErrorFactory;

    /// <summary>
    /// The RuleSettingsBuilder class is responsible for creating instances of RuleSettings.
    /// It allows configuring the logger and the exception error factory used by the Rule class.
    /// </summary>
    public RuleSettingsBuilder()
    {
        this.logger = new RuleNullLogger();
        this.ruleFailureExceptionFactory = rule => new RuleException(rule, string.Empty);
        this.ruleExceptionErrorFactory = (rule, ex) => new RuleExceptionError(rule, ex);
    }

    /// <summary>
    /// Builds and returns a RuleSettings object.
    /// </summary>
    /// <returns>
    /// A RuleSettings object configured with the current properties of the RuleSettingsBuilder instance.
    /// </returns>
    public RuleSettings Build()
    {
        return new RuleSettings
        {
            Logger = this.logger ?? new RuleNullLogger(),
            ContinueOnRuleFailure = this.continueOnRuleFailure,
            ThrowOnRuleFailure = this.throwOnRuleFailure,
            ThrowOnRuleException = this.throwOnRuleException,
            RuleFailureExceptionFactory = this.ruleFailureExceptionFactory,
            RuleExceptionErrorFactory = this.ruleExceptionErrorFactory
        };
    }

    /// <summary>
    /// Configures whether to throw an exception on rule failure.
    /// </summary>
    /// <param name="throwOnFailure">If true, exceptions will be thrown on rule failures.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RuleSettingsBuilder ThrowOnRuleFailure(bool throwOnFailure = true)
    {
        this.throwOnRuleFailure = throwOnFailure;
        return this;
    }

    /// <summary>
    /// Configures whether to throw exceptions that occur during rule execution.
    /// </summary>
    /// <param name="throwOnException">If true, exceptions during execution will be thrown.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RuleSettingsBuilder ThrowOnRuleException(bool throwOnException = true)
    {
        this.throwOnRuleException = throwOnException;
        return this;
    }

    /// <summary>
    /// Configures whether to continue executing subsequent rules after a rule failure.
    /// </summary>
    /// <param name="continueOnFailure">If true, execution continues after rule failures.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RuleSettingsBuilder ContinueOnRuleFailure(bool continueOnFailure)
    {
        this.continueOnRuleFailure = continueOnFailure;
        return this;
    }

    /// <summary>
    /// Sets the logger for rule-related information and errors.
    /// </summary>
    /// <param name="logger">The logger implementation to use.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RuleSettingsBuilder SetLogger(IRuleLogger logger)
    {
        this.logger = logger ?? new RuleNullLogger();
        return this;
    }

    /// <summary>
    /// Sets the factory method for creating rule failure exceptions.
    /// </summary>
    /// <param name="factory">The factory function that creates RuleException instances.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RuleSettingsBuilder SetRuleFailureExceptionFactory(Func<IRule, RuleException> factory)
    {
        this.ruleFailureExceptionFactory = factory ?? (rule => new RuleException(rule, string.Empty));
        return this;
    }

    /// <summary>
    /// Sets the factory method for creating rule exception errors.
    /// </summary>
    /// <param name="factory">The factory function that creates IResultError instances.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RuleSettingsBuilder SetRuleExceptionErrorFactory(Func<IRule, Exception, IResultError> factory)
    {
        this.ruleExceptionErrorFactory = factory ?? ((rule, ex) => new RuleExceptionError(rule, ex));
        return this;
    }
}