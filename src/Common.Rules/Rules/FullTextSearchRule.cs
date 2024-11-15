// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines a rule to support full-text search functionality.
/// This class encapsulates the criteria and behavior required
/// to perform a full-text search in a given context.
/// </summary>
public class FullTextSearchRule(
    string text,
    string searchTerms,
    StringComparison comparison = StringComparison.OrdinalIgnoreCase) : RuleBase
{
    /// <summary>
    /// Gets or sets the message associated with the object.
    /// </summary>
    /// <value>
    /// A string representing the message.
    /// </value>
    public override string Message => "Text does not match search criteria";

    /// <summary>
    /// Executes a specific rule provided as a parameter.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="context">The context in which the rule is executed.</param>
    /// <returns>True if the rule is executed successfully, otherwise false.</returns>
    protected override Result Execute()
    {
        if (string.IsNullOrWhiteSpace(searchTerms))
        {
            return Result.Success();
        }

        var sarchTerm = searchTerms.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var searchText = text ?? string.Empty;

        return Result.SuccessIf(sarchTerm.All(term => searchText.Contains(term, comparison)));
    }
}