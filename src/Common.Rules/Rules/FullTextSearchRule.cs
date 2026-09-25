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
    /// Checks whether the text contains every whitespace-delimited search term.
    /// </summary>
    /// <returns>A successful result when no search terms are supplied or every term is found; otherwise, a failure result.</returns>
    public override Result Execute()
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