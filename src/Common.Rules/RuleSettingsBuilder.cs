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
    /// <summary>
    /// The RuleSettingsBuilder class is responsible for creating instances of RuleSettings.
    /// It allows configuring the logger and the exception error factory used by the Rule class.
    /// </summary>
    public RuleSettingsBuilder()
    {
        this.Logger = new RuleNullLogger();
        this.RuleFailureExceptionFactory = rule => new RuleException(rule, string.Empty);
        this.RuleExceptionErrorFactory = (rule, ex) => new RuleExceptionError(rule, ex);
    }

    /// <summary>
    /// Gets or sets the logger to be used for logging rule-related information and errors.
    /// </summary>
    /// <remarks>
    /// The logger must implement the <see cref="IRuleLogger"/> interface.
    /// </remarks>
    public IRuleLogger Logger { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to continue executing subsequent rules if a rule failure occurs.
    /// </summary>
    /// <remarks>
    /// If set to <c>true</c>, the execution will proceed to the next rule even if the current one fails.
    /// If set to <c>false</c>, the rule execution chain will stop when a rule failure is encountered.
    /// </remarks>
    public bool ContinueOnRuleFailure { get; set; } // breaks the rule execution chain when a rule has a failure result.

    /// <summary>
    /// Gets or sets a value indicating whether to throw an exception on rule failure.
    /// </summary>
    /// <remarks>
    /// When set to true, any rule that fails will throw an exception.
    /// When set to false, rule failures will not cause exceptions to be thrown.
    /// </remarks>
    public bool ThrowOnRuleFailure { get; set; } // any rule that fails will throw an exception

    /// <summary>
    /// Gets or sets a value indicating whether an exception should be thrown when a rule exception occurs.
    /// </summary>
    /// <remarks>
    /// If set to true, any rule that encounters an exception during its execution will throw an exception.
    /// If set to false, the exception will be handled according to the defined error handling strategy.
    /// </remarks>
    public bool ThrowOnRuleException { get; set; } // any exceptions during rule execution will be thrown instead of being errors in the final result

    /// <summary>
    /// Gets or sets the factory method to create exceptions when a rule fails.
    /// </summary>
    /// <remarks>
    /// This property allows customization of the exception to be thrown when a rule failure occurs. The factory method should accept an <see cref="IRule"/> instance and return a <see cref="RuleException"/>.
    /// </remarks>
    public Func<IRule, RuleException> RuleFailureExceptionFactory { get; set; }

    /// <summary>
    /// Gets or sets the factory function used to generate a custom error when a rule exception occurs.
    /// </summary>
    /// <remarks>
    /// This factory function takes an <see cref="IRule"/> instance and an <see cref="Exception"/> as parameters
    /// and returns an <see cref="IResultError"/> which encapsulates the error information.
    /// It can be used to create specific error types or to customize the handling of exceptions when rules are processed.
    /// </remarks>
    public Func<IRule, Exception, IResultError> RuleExceptionErrorFactory { get; set; }

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
            Logger = this.Logger ?? new RuleNullLogger(),
            ContinueOnRuleFailure = this.ContinueOnRuleFailure,
            ThrowOnRuleFailure = this.ThrowOnRuleFailure,
            ThrowOnRuleException = this.ThrowOnRuleException,
            RuleFailureExceptionFactory = this.RuleFailureExceptionFactory,
            RuleExceptionErrorFactory = this.RuleExceptionErrorFactory
        };
    }
}