// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class RuleSettingsBuilder
{
    /// <summary>
    /// Gets or sets the logger to be used for logging rule-related information and errors.
    /// </summary>
    /// <remarks>
    /// The logger must implement the <see cref="IRuleLogger"/> interface. By default,
    /// it is set to an instance of <see cref="NullRuleLogger"/> if not specified.
    /// </remarks>
    public IRuleLogger Logger { get; set; }

    public bool ContinueOnRuleFailure { get; set; } // breaks the rule execution chain when a rule has a failure result.

    public bool ThrowOnRuleFailure { get; set; } // any rule that fails will throw an exception ()

    public bool ThrowOnRuleException { get; set; } // any exceptions during rule execution will be thrown instead of being errors in the final result

    /// <summary>
    /// Gets or sets the delegate function responsible for creating <see cref="ExceptionError"/> instances.
    /// </summary>
    /// <value>
    /// A <see cref="Func{T1, T2, TRule}"/> that takes a string message and an <see cref="Exception"/>,
    /// and returns an <see cref="ExceptionError"/>. By default, it is set to a function that creates a new
    /// <see cref="ExceptionError"/> instance with the provided exception and message.
    /// </value>
    public Func<IRule, RuleException> RuleFailureExceptionFactory { get; set; }

    /// <summary>
    /// Gets or sets the delegate function responsible for creating <see cref="ExceptionError"/> instances.
    /// </summary>
    /// <value>
    /// A <see cref="Func{T1, T2, TRule}"/> that takes a string message and an <see cref="Exception"/>,
    /// and returns an <see cref="ExceptionError"/>. By default, it is set to a function that creates a new
    /// <see cref="ExceptionError"/> instance with the provided exception and message.
    /// </value>
    public Func<IRule, Exception, IResultError> RuleExceptionErrorFactory { get; set; }

    /// <summary>
    /// The RuleSettingsBuilder class is responsible for creating instances of RuleSettings.
    /// It allows configuring the logger and the exception error factory used by the Rule class.
    /// </summary>
    public RuleSettingsBuilder()
    {
        this.Logger = new NullRuleLogger();
        this.RuleFailureExceptionFactory = rule => new RuleException(rule, string.Empty);
        this.RuleExceptionErrorFactory = (rule, ex) => new RuleExceptionError(rule, ex);
    }

    /// Builds and returns a RuleSettings object.
    /// <return>
    /// A RuleSettings object configured with the current properties of the RuleSettingsBuilder instance.
    /// </return>
    public RuleSettings Build()
    {
        return new RuleSettings
        {
            Logger = this.Logger ?? new NullRuleLogger(),
            ContinueOnRuleFailure = this.ContinueOnRuleFailure,
            ThrowOnRuleFailure = this.ThrowOnRuleFailure,
            ThrowOnRuleException = this.ThrowOnRuleException,
            RuleFailureExceptionFactory = this.RuleFailureExceptionFactory,
            RuleExceptionErrorFactory = this.RuleExceptionErrorFactory
        };
    }
}