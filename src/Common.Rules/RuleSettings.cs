// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Contains settings for configuring logging and error handling in rule implementations.
/// </summary>
public class RuleSettings
{
    /// <summary>
    /// Gets or sets the Logger instance that will be used for logging operations.
    /// Implements the IResultLogger interface.
    /// </summary>
    public IRuleLogger Logger { get; set; }

    public bool ContinueOnRuleFailure { get; set; }

    public bool ThrowOnRuleFailure { get; set; }

    /// <summary>
    /// Gets or sets the factory function that creates a RuleException instance.
    /// The factory is invoked when a rule failure occurs and ThrownOnRuleFailure is set to true.
    /// </summary>
    public Func<IRule, RuleException> RuleFailureExceptionFactory { get; set; }

    public bool ThrowOnRuleException { get; set; }

    /// <summary>
    /// Gets or sets the factory function that generates a IResultError instance.
    /// The factory is invoked when a rule exeption occurs and ThrownOnRuleException is set to false.
    /// </summary>
    public Func<IRule, Exception, IResultError> RuleExceptionErrorFactory { get; set; }
}