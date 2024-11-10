// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides configurable settings for handling logging, error management, and rule execution behavior.
/// </summary>
public class RuleSettings
{
    /// <summary>
    /// Gets or sets the logger instance responsible for logging rule execution results and errors.
    /// </summary>
    public IRuleLogger Logger { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to continue executing subsequent rules when a rule fails.
    /// If set to true, the rule processing will not be halted on rule failure, and subsequent rules will still be executed.
    /// </summary>
    public bool ContinueOnRuleFailure { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an exception should be thrown if a rule fails.
    /// When set to true, any rule failure will result in a thrown exception.
    /// </summary>
    public bool ThrowOnRuleFailure { get; set; }

    /// <summary>
    /// Gets or sets a factory function that generates a RuleException when a rule fails.
    /// This factory is used to create exceptions that encapsulate rule violations,
    /// potentially including custom messages and inner exceptions.
    /// </summary>
    public Func<IRule, RuleException> RuleFailureExceptionFactory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether exceptions thrown by a rule should cause the rule's execution to be terminated.
    /// If set to true, exceptions will be propagated, causing the rule to fail fast.
    /// </summary>
    public bool ThrowOnRuleException { get; set; }

    /// <summary>
    /// Gets or sets the factory function that creates an instance of IResultError for a given IRule and Exception.
    /// Used to handle errors arising from rule execution exceptions.
    /// </summary>
    public Func<IRule, Exception, IResultError> RuleExceptionErrorFactory { get; set; }
}