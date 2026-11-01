// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Pure logical query evaluation used by the memory provider and backend parity vectors.</summary>
/// <example><code>var matches = evaluator.Matches(plan.Predicate, record, "Active");</code></example>
public sealed class AuditQueryEvaluator(AuditFieldCatalog catalog)
{
    /// <summary>Evaluates an approved complete predicate before count, ordering or paging.</summary>
    /// <example><code>var included = evaluator.Matches(predicate, record);</code></example>
    public bool Matches(AuditPredicate predicate, AuditRecord record, string storageState = "Active") => this.Evaluate(predicate, record, storageState);

    /// <summary>Compares approved root values for stable provider-side ordering.</summary>
    /// <example><code>var comparison = evaluator.CompareRecords(left, "Active", right, "Archived", order);</code></example>
    public int CompareRecords(AuditRecord left, string leftState, AuditRecord right, string rightState, AuditOrder order)
    {
        var field = this.Field("Record", order.Field);
        var result = Compare(AuditFieldReader.Read(field, left, leftState), AuditFieldReader.Read(field, right, rightState), field.IgnoreCase);
        return order.Direction == OrderDirection.Descending ? -Math.Sign(result) : result;
    }

    /// <summary>Tests exact typed resource identity across root and action references.</summary>
    /// <example><code>var found = AuditQueryEvaluator.HasResource(record, resource);</code></example>
    public static bool HasResource(AuditRecord record, AuditResourceReference resource) => record.Resources.Any(r => ResourceEquals(r, resource)) || record.Actions.Any(a => a.Resources.Any(r => ResourceEquals(r, resource)));

    /// <summary>Compares type aliases case-insensitively and opaque IDs ordinally.</summary>
    /// <example><code>var same = AuditQueryEvaluator.ResourceEquals(left, right);</code></example>
    public static bool ResourceEquals(AuditResourceReference left, AuditResourceReference right) => left is not null && right is not null && StringComparer.OrdinalIgnoreCase.Equals(left.Type, right.Type) && StringComparer.Ordinal.Equals(left.Id, right.Id);

    /// <summary>Reads one registered root field for grouping/facets without arbitrary object reflection.</summary>
    /// <example><code>var value = evaluator.Read(record, "Key", "Active");</code></example>
    public AuditQueryValue Read(AuditRecord record, string field, string storageState) => AuditFieldReader.Read(this.Field("Record", field), record, storageState);

    /// <summary>Compares validated scalar values using the declared invariant semantics.</summary>
    /// <example><code>var result = AuditQueryEvaluator.Compare(left, right, ignoreCase: false);</code></example>
    public static int Compare(AuditQueryValue left, AuditQueryValue right, bool ignoreCase)
    {
        var l = Available(left) && left.Kind != "Null";
        var r = Available(right) && right.Kind != "Null";
        if (!l || !r) return l == r ? 0 : l ? 1 : -1;
        return left.Kind switch
        {
            "String" or "Guid" => (ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Compare(left.Text, right.Text),
            "Integer" or "Decimal" => (left.Number ?? left.Ticks).GetValueOrDefault().CompareTo((right.Number ?? right.Ticks).GetValueOrDefault()),
            "Timestamp" => left.Ticks.GetValueOrDefault().CompareTo(right.Ticks.GetValueOrDefault()),
            "Boolean" => left.Boolean.GetValueOrDefault().CompareTo(right.Boolean.GetValueOrDefault()),
            _ => throw Invalid()
        };
    }

    private bool Evaluate(AuditPredicate predicate, object value, string state)
    {
        return predicate.Kind switch
        {
            "True" => true,
            "False" => false,
            "And" => predicate.Children.All(child => this.Evaluate(child, value, state)),
            "Or" => predicate.Children.Any(child => this.Evaluate(child, value, state)),
            "ResourceMatch" => value is AuditRecord record && HasResource(record, predicate.Resource),
            "Comparison" => this.Test(this.Field(predicate.Domain, predicate.Field), value, state, predicate.Operator, predicate.Value),
            "Collection" => this.Collection(predicate, value, state),
            _ => throw Invalid()
        };
    }
    private bool Collection(AuditPredicate predicate, object value, string state)
    {
        if (predicate.Children.Length != 1) throw Invalid();
        var field = this.Field(predicate.Domain, predicate.Collection);
        if (field.Kind != "Collection") throw Invalid();
        var children = (value, field.Binding) switch
        {
            (AuditRecord record, "Actions") => record.Actions.Cast<object>(),
            (AuditRecord record, "EntityChanges") => record.EntityChanges.Cast<object>(),
            (AuditRecord record, "Resources") => record.Resources.Cast<object>(),
            (AuditRecord record, "Errors") => record.Errors.Cast<object>(),
            (AuditAction action, "Errors") => action.Errors.Cast<object>(),
            (AuditAction action, "Resources") => action.Resources.Cast<object>(),
            (AuditChangeEvidence change, "Properties") => change.Properties.Cast<object>(),
            _ => throw Invalid()
        };
        return predicate.Operator switch
        {
            FilterOperator.Any => children.Any(child => this.Evaluate(predicate.Children[0], child, state)),
            FilterOperator.All => children.All(child => this.Evaluate(predicate.Children[0], child, state)),
            FilterOperator.None => !children.Any(child => this.Evaluate(predicate.Children[0], child, state)),
            _ => throw Invalid()
        };
    }
    private bool Test(AuditQueryField field, object source, string state, FilterOperator op, AuditQueryValue expected)
    {
        var actual = AuditFieldReader.Read(field, source, state);
        if (!Available(actual)) return false;
        if (op == FilterOperator.IsNull) return actual.Kind == "Null";
        if (op == FilterOperator.IsNotNull) return actual.Kind != "Null";
        if (op is FilterOperator.IsEmpty or FilterOperator.IsNotEmpty)
            return actual.Kind == "String" && (op == FilterOperator.IsEmpty ? actual.Text?.Length == 0 : actual.Text?.Length > 0);
        if (actual.Kind == "Null" || expected.Kind == "Null") return op switch
        {
            FilterOperator.Equal => actual.Kind == expected.Kind,
            FilterOperator.NotEqual => actual.Kind != expected.Kind,
            _ => false
        };
        var comparison = Compare(actual, expected, field.IgnoreCase);
        var stringComparison = field.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return op switch
        {
            FilterOperator.Equal => comparison == 0,
            FilterOperator.NotEqual => comparison != 0,
            FilterOperator.GreaterThan => comparison > 0,
            FilterOperator.GreaterThanOrEqual => comparison >= 0,
            FilterOperator.LessThan => comparison < 0,
            FilterOperator.LessThanOrEqual => comparison <= 0,
            FilterOperator.Contains => actual.Text.Contains(expected.Text, stringComparison),
            FilterOperator.DoesNotContain => !actual.Text.Contains(expected.Text, stringComparison),
            FilterOperator.StartsWith => actual.Text.StartsWith(expected.Text, stringComparison),
            FilterOperator.DoesNotStartWith => !actual.Text.StartsWith(expected.Text, stringComparison),
            FilterOperator.EndsWith => actual.Text.EndsWith(expected.Text, stringComparison),
            FilterOperator.DoesNotEndWith => !actual.Text.EndsWith(expected.Text, stringComparison),
            _ => throw Invalid()
        };
    }
    private AuditQueryField Field(string domain, string name)
    {
        var field = catalog.Find(domain, name);
        return field.IsSuccess ? field.Value : throw Invalid();
    }
    private static bool Available(AuditQueryValue value) => value.Kind is "String" or "Guid" or "Integer" or "Decimal" or "Timestamp" or "Boolean" or "Null";
    private static ArgumentException Invalid() => new("AuditQueryInvalid");
}
