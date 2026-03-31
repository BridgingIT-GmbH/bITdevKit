// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Emits FluentValidation rule chains from normalized validation-rule models.
/// </summary>
public static class ValidationSourceEmitter
{
    /// <summary>
    /// Emits generated FluentValidation rules for the specified property-rule models.
    /// </summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="rules">The rules to emit.</param>
    /// <param name="indent">The line prefix used for each emitted line.</param>
    public static void EmitRules(StringBuilder builder, ImmutableArray<ValidationPropertyRuleModel> rules, string indent)
    {
        foreach (var rule in rules)
        {
            var propertyName = ValidationGeneratorSymbolHelper.EscapeIdentifier(rule.PropertySymbol.Name);
            var ruleForMethod = rule.TargetKind == ValidationRuleTargetKind.Property ? "RuleFor" : "RuleForEach";
            var ruleBuilderExpression = $"this.{ruleForMethod}(x => x.{propertyName})";

            if (rule.Kind is ValidationRuleKind.NotEmptyGuid or ValidationRuleKind.NotDefaultOrEmptyGuid or ValidationRuleKind.ValidGuid or ValidationRuleKind.EmptyGuid or ValidationRuleKind.DefaultOrEmptyGuid or ValidationRuleKind.GuidFormat)
            {
                builder.Append(indent)
                    .Append(GetGuidValidationInvocation(rule, ruleBuilderExpression));
            }
            else
            {
                builder.Append(indent)
                    .Append(ruleBuilderExpression)
                    .AppendLine();

                builder.Append(indent)
                    .Append("    .")
                    .Append(GetMethodName(rule.Kind))
                    .Append('(')
                    .Append(string.Join(", ", GetArguments(rule)))
                    .Append(')');
            }

            if (!string.IsNullOrWhiteSpace(rule.Message))
            {
                builder.AppendLine()
                    .Append(indent)
                    .Append("    .WithMessage(")
                    .Append(SymbolDisplay.FormatLiteral(rule.Message, quote: true))
                    .Append(')');
            }

            builder.AppendLine(";");
            builder.AppendLine();
        }
    }

    private static string GetMethodName(ValidationRuleKind kind)
    {
        return kind switch
        {
            ValidationRuleKind.NotNull => "NotNull",
            ValidationRuleKind.NotEmpty => "NotEmpty",
            ValidationRuleKind.Empty => "Empty",
            ValidationRuleKind.Length => "Length",
            ValidationRuleKind.MinLength => "MinimumLength",
            ValidationRuleKind.MaxLength => "MaximumLength",
            ValidationRuleKind.GreaterThan => "GreaterThan",
            ValidationRuleKind.GreaterThanOrEqualTo => "GreaterThanOrEqualTo",
            ValidationRuleKind.LessThan => "LessThan",
            ValidationRuleKind.LessThanOrEqualTo => "LessThanOrEqualTo",
            ValidationRuleKind.Equal => "Equal",
            ValidationRuleKind.NotEqual => "NotEqual",
            ValidationRuleKind.InclusiveBetween => "InclusiveBetween",
            ValidationRuleKind.ExclusiveBetween => "ExclusiveBetween",
            ValidationRuleKind.NotEmptyGuid => "MustNotBeEmptyGuid",
            ValidationRuleKind.NotDefaultOrEmptyGuid => "MustNotBeDefaultOrEmptyGuid",
            ValidationRuleKind.ValidGuid => "MustBeValidGuid",
            ValidationRuleKind.EmptyGuid => "MustBeEmptyGuid",
            ValidationRuleKind.DefaultOrEmptyGuid => "MustBeDefaultOrEmptyGuid",
            ValidationRuleKind.GuidFormat => "MustBeInGuidFormat",
            ValidationRuleKind.Email => "EmailAddress",
            ValidationRuleKind.Matches => "Matches",
            _ => throw new InvalidOperationException($"Unsupported rule kind '{kind}'."),
        };
    }

    private static string GetGuidValidationInvocation(ValidationPropertyRuleModel rule, string ruleBuilderExpression)
    {
        var methodName = rule.Kind switch
        {
            ValidationRuleKind.NotEmptyGuid => "MustNotBeEmptyGuid",
            ValidationRuleKind.NotDefaultOrEmptyGuid => "MustNotBeDefaultOrEmptyGuid",
            ValidationRuleKind.ValidGuid => "MustBeValidGuid",
            ValidationRuleKind.EmptyGuid => "MustBeEmptyGuid",
            ValidationRuleKind.DefaultOrEmptyGuid => "MustBeDefaultOrEmptyGuid",
            ValidationRuleKind.GuidFormat => "MustBeInGuidFormat",
            _ => throw new InvalidOperationException($"Unsupported GUID validation rule kind '{rule.Kind}'."),
        };

        return $"global::BridgingIT.DevKit.Common.GuidValidationExtensions.{methodName}({ruleBuilderExpression})";
    }

    private static IEnumerable<string> GetArguments(ValidationPropertyRuleModel rule)
    {
        return rule.Kind switch
        {
            ValidationRuleKind.NotNull or ValidationRuleKind.NotEmpty or ValidationRuleKind.Empty or ValidationRuleKind.NotEmptyGuid or ValidationRuleKind.NotDefaultOrEmptyGuid or ValidationRuleKind.ValidGuid or ValidationRuleKind.EmptyGuid or ValidationRuleKind.DefaultOrEmptyGuid or ValidationRuleKind.GuidFormat or ValidationRuleKind.Email
                => [],
            ValidationRuleKind.Length or ValidationRuleKind.MinLength or ValidationRuleKind.MaxLength or ValidationRuleKind.Matches
                => rule.Arguments
                    .Take(rule.Message is null ? rule.Arguments.Length : rule.Arguments.Length - 1)
                    .Select(static argument => SymbolDisplay.FormatLiteral(argument, quote: true)),
            ValidationRuleKind.GreaterThan or ValidationRuleKind.GreaterThanOrEqualTo or ValidationRuleKind.LessThan or ValidationRuleKind.LessThanOrEqualTo or ValidationRuleKind.Equal or ValidationRuleKind.NotEqual
                => [RenderParsedValue(rule.ValidatedType, rule.Arguments[0])],
            ValidationRuleKind.InclusiveBetween or ValidationRuleKind.ExclusiveBetween
                => [RenderParsedValue(rule.ValidatedType, rule.Arguments[0]), RenderParsedValue(rule.ValidatedType, rule.Arguments[1])],
            _ => throw new InvalidOperationException($"Unsupported rule kind '{rule.Kind}'."),
        };
    }

    private static string RenderParsedValue(ITypeSymbol type, string rawValue)
    {
        return "global::BridgingIT.DevKit.Common.ValidationAttributeValueParser.Parse<" +
            ValidationGeneratorSymbolHelper.GetTypeName(type) +
            ">(" +
            SymbolDisplay.FormatLiteral(rawValue, quote: true) +
            ")";
    }
}
