// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>Validates the existing FilterModel into a bounded immutable provider expression.</summary>
/// <example><code>var plan = builder.Build(filter);</code></example>
public sealed class AuditSearchPlanBuilder(AuditFieldCatalog catalog, TimeProvider time, AuditQueryOptions options = null)
{
    private static readonly JsonSerializerOptions filterJson = CreateFilterJson();
    private readonly AuditQueryOptions limits = options ?? new();

    /// <summary>Compiles client selection, mandatory scope, deterministic paging and absolute time bounds.</summary>
    /// <example><code>var plan = builder.Build(filter, trustedPredicate);</code></example>
    public Result<AuditSearchPlan> Build(FilterModel filter, AuditPredicate mandatory = null, bool withRange = true, string scopeFingerprint = "unrestricted", DateTimeOffset? asOfUtc = null, bool unrestrictedScope = false)
    {
        try
        {
            this.limits.Validate();
            var copy = AuditFilterSnapshot.Copy(filter) ?? new FilterModel();
            ValidateModel(copy);
            var predicate = this.List(copy.Filters ?? [], "Record", 0);
            if (Flatten(predicate).Count(p => p.Kind is "Comparison" or "Collection") > 32) throw Invalid();
            if (!HasField(predicate, "StorageState")) predicate = And(predicate, Comparison("Record", "StorageState", FilterOperator.Equal, new AuditQueryValue { Kind = "String", Text = "Active" }));
            var orders = new List<AuditOrder>();
            foreach (var order in copy.Orderings ?? [])
            {
                if (order is null || !Enum.IsDefined(order.Direction)) throw Invalid();
                var field = this.Field("Record", order.Field);
                if (field.Kind == "Collection") throw Invalid();
                orders.Add(new AuditOrder { Field = field.Binding, Direction = order.Direction });
            }

            if (orders.Count == 0) orders.Add(new AuditOrder { Field = "StartedUtc" });
            if (!orders.Any(o => o.Field == "AuditId")) orders.Add(new AuditOrder { Field = "AuditId", Direction = OrderDirection.Descending });
            var now = (asOfUtc ?? time.GetUtcNow()).ToUniversalTime();
            DateTimeOffset? from = null, to = null;
            if (withRange && ExactAuditId(predicate) is null)
            {
                var boundaries = Boundaries(predicate);
                to = boundaries.To ?? now;
                from = boundaries.From ?? to.Value.Subtract(this.limits.DefaultRange);
                if (from > to || to.Value - from.Value > this.limits.MaxRange) throw Invalid();
                predicate = And(predicate, Comparison("Record", "StartedUtc", FilterOperator.GreaterThanOrEqual, Timestamp(from.Value)),
                    Comparison("Record", "StartedUtc", FilterOperator.LessThan, Timestamp(to.Value)));
            }

            predicate = And(mandatory ?? new AuditPredicate(), predicate);
            var plan = new AuditSearchPlan
            {
                Predicate = predicate, Orderings = orders.ToImmutableArray(), Page = copy.Page == 0 ? 1 : copy.Page,
                PageSize = copy.PageSize == 0 ? 50 : copy.PageSize, FromUtc = from, ToUtc = to, AsOfUtc = now,
                IsUnrestrictedScope = unrestrictedScope, ScopeFingerprint = scopeFingerprint, Lifecycle = Lifecycle(predicate), RequiresStableLifecycle = LifecycleDependent(predicate)
            };
            var selection = JsonSerializer.SerializeToElement(new { plan.Predicate, plan.Orderings, plan.FromUtc, plan.ToUtc, plan.Lifecycle });
            return Result<AuditSearchPlan>.Success(plan with { Fingerprint = AuditCanonicalizerV1.Hash(AuditCanonicalizerV1.Encode(selection)) });
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException or FormatException or OverflowException or NotSupportedException)
        {
            return Result<AuditSearchPlan>.Failure(new AuditError("AuditQueryInvalid"));
        }
    }

    /// <summary>Compiles trusted constraints without injecting client time or lifecycle defaults.</summary>
    /// <example><code>var predicate = builder.CompileConstraints(scope.Constraints);</code></example>
    public Result<AuditPredicate> CompileConstraints(FilterModel constraints)
    {
        try
        {
            var copy = AuditFilterSnapshot.Copy(constraints) ?? throw Invalid();
            ValidateModel(copy);
            if (copy.Page != 0 || copy.PageSize != 0 || copy.Orderings is { Count: > 0 }) throw Invalid();
            var predicate = this.List(copy.Filters ?? [], "Record", 0);
            if (Flatten(predicate).Count(p => p.Kind is "Comparison" or "Collection") > 32) throw Invalid();
            return Result<AuditPredicate>.Success(predicate);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException or FormatException or OverflowException or NotSupportedException)
        {
            return Result<AuditPredicate>.Failure(new AuditError("AuditReadScopeInvalid"));
        }
    }

    /// <summary>Composes mandatory restrictions outside any client OR groups.</summary>
    /// <example><code>var effective = AuditSearchPlanBuilder.And(scope, resource, client);</code></example>
    public static AuditPredicate And(params AuditPredicate[] predicates)
    {
        var children = predicates.Where(p => p is not null && p.Kind != "True").ToImmutableArray();
        return children.Length == 0 ? new() : children.Length == 1 ? children[0] : new() { Kind = "And", Children = children };
    }

    private static Guid? ExactAuditId(AuditPredicate predicate)
    {
        if (predicate.Kind == "Comparison" && predicate.Domain == "Record" && predicate.Field == "AuditId" && predicate.Operator == FilterOperator.Equal
            && Guid.TryParse(predicate.Value.Text, out var id) && id != Guid.Empty) return id;
        if (predicate.Kind is not ("And" or "Or")) return null;
        var identities = predicate.Children.Select(ExactAuditId).ToArray();
        if (predicate.Kind == "Or" && identities.Any(value => value is null)) return null;
        var present = identities.Where(value => value.HasValue).Distinct().ToArray();
        return present.Length == 1 ? present[0] : null;
    }
    private AuditPredicate List(IReadOnlyList<FilterCriteria> filters, string domain, int depth)
    {
        if (depth > 8) throw Invalid();
        if (filters.Count == 0) return new();
        var groups = new List<AuditPredicate>();
        var alternatives = new List<AuditPredicate>();
        for (var i = 0; i < filters.Count; i++)
        {
            var item = filters[i] ?? throw Invalid();
            alternatives.Add(this.Criterion(item, domain, depth + 1));
            if (item.Logic == FilterLogicOperator.And || i == filters.Count - 1)
            {
                groups.Add(alternatives.Count == 1 ? alternatives[0] : new AuditPredicate { Kind = "Or", Children = alternatives.ToImmutableArray() });
                alternatives.Clear();
            }
        }

        return And(groups.ToArray());
    }
    private AuditPredicate Criterion(FilterCriteria criterion, string domain, int depth)
    {
        if (depth > 8 || !Enum.IsDefined(criterion.Operator) || !Enum.IsDefined(criterion.Logic) || !Enum.IsDefined(criterion.CustomType)) throw Invalid();
        if (criterion.SpecificationName is not null || criterion.CustomType == FilterCustomType.NamedSpecification)
            return this.Named(criterion.SpecificationName, criterion.SpecificationArguments, domain, depth);
        if (criterion.CustomType == FilterCustomType.CompositeSpecification)
        {
            if (criterion.CompositeSpecification?.Nodes is not { Count: > 0 } nodes) throw Invalid();
            return And(nodes.Select(node => this.Specification(node, domain, depth + 1)).ToArray());
        }

        if (criterion.CustomType != FilterCustomType.None) return this.Custom(criterion, domain, depth);
        if (string.IsNullOrWhiteSpace(criterion.Field))
        {
            if (criterion.Filters is not { Count: > 0 }) throw Invalid();
            return this.List(criterion.Filters, domain, depth);
        }

        var field = this.Field(domain, criterion.Field);
        if (field.Kind == "Collection")
        {
            if (criterion.Operator is not (FilterOperator.Any or FilterOperator.All or FilterOperator.None)) throw Invalid();
            var children = criterion.Filters is { Count: > 0 } ? criterion.Filters : criterion.Value is FilterCriteria child ? [child] : throw Invalid();
            var inner = this.List(children, field.TargetDomain, depth);
            if (field.TargetDomain == "Change" && !HasField(inner, "PersistenceState"))
                inner = And(inner, Comparison("Change", "PersistenceState", FilterOperator.Equal, new AuditQueryValue { Kind = "String", Text = "Committed" }));
            return new AuditPredicate { Kind = "Collection", Domain = domain, Collection = field.Binding, Operator = criterion.Operator, Children = [inner] };
        }

        if (criterion.Filters is { Count: > 0 }) throw Invalid();
        if (!Supports(field, criterion.Operator)) throw Invalid();
        var converted = AuditQueryValues.Convert(criterion.Value, field.Kind);
        if (converted.IsFailure && criterion.Operator is not (FilterOperator.IsNull or FilterOperator.IsNotNull or FilterOperator.IsEmpty or FilterOperator.IsNotEmpty)) throw Invalid();
        var value = converted.IsSuccess ? converted.Value : new AuditQueryValue();
        ValidateEnum(field, value);
        return Comparison(domain, field.Binding, criterion.Operator, value);
    }
    private AuditPredicate Named(string name, object[] args, string domain, int depth)
    {
        if (domain != "Record" || name is not ("audit.action-match" or "audit.change-match") || args is not { Length: > 0 and <= 32 }) throw Invalid();
        var children = new List<FilterCriteria>();
        foreach (var arg in args)
        {
            if (arg is FilterCriteria criterion) children.Add(criterion);
            else if (arg is JsonElement { ValueKind: JsonValueKind.Object } json) children.Add(json.Deserialize<FilterCriteria>(filterJson) ?? throw Invalid());
            else if (arg is JsonElement { ValueKind: JsonValueKind.Array } array)
                foreach (var item in array.EnumerateArray()) children.Add(item.Deserialize<FilterCriteria>(filterJson) ?? throw Invalid());
            else throw Invalid();
        }

        return this.Criterion(new FilterCriteria(name == "audit.action-match" ? "Actions" : "EntityChanges", FilterOperator.Any, null) { Filters = children }, domain, depth + 1);
    }
    private AuditPredicate Specification(SpecificationNode node, string domain, int depth)
    {
        if (depth > 8) throw Invalid();
        if (node is SpecificationLeaf leaf) return this.Named(leaf.Name, leaf.Arguments, domain, depth);
        if (node is not SpecificationGroup { Nodes.Count: > 0 } group || !Enum.IsDefined(group.Logic)) throw Invalid();
        var children = group.Nodes.Select(n => this.Specification(n, domain, depth + 1)).ToArray();
        return group.Logic == FilterLogicOperator.And ? And(children) : new AuditPredicate { Kind = "Or", Children = children.ToImmutableArray() };
    }
    private AuditPredicate Custom(FilterCriteria criterion, string domain, int depth)
    {
        var parameters = criterion.CustomParameters ?? throw Invalid();
        object Parameter(string name) => parameters.FirstOrDefault(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
        if (criterion.CustomType == FilterCustomType.FullTextSearch) return this.FullText(Parameter("searchTerm"), Parameter("fields"), domain);
        var name = ScalarText(Parameter("field")) ?? throw Invalid();
        var field = this.Field(domain, name);
        void Allow(params FilterOperator[] operators)
        {
            if (operators.Any(op => !Supports(field, op))) throw Invalid();
        }

        if (criterion.CustomType == FilterCustomType.DateRange)
        {
            if (field.Kind != "Timestamp") throw Invalid();
            var start = AuditQueryValues.Convert(Parameter("startDate"), "Timestamp");
            var end = AuditQueryValues.Convert(Parameter("endDate"), "Timestamp");
            if (start.IsFailure || end.IsFailure || start.Value.Kind == "Null" || end.Value.Kind == "Null" || start.Value.Ticks > end.Value.Ticks) throw Invalid();
            var inclusive = Parameter("inclusive") is null ? Result<AuditQueryValue>.Success(new() { Kind = "Boolean", Boolean = true }) : AuditQueryValues.Convert(Parameter("inclusive"), "Boolean");
            if (inclusive.IsFailure || inclusive.Value.Boolean is not bool include) throw Invalid();
            Allow(include ? FilterOperator.GreaterThanOrEqual : FilterOperator.GreaterThan, include ? FilterOperator.LessThanOrEqual : FilterOperator.LessThan);
            return And(Comparison(domain, field.Binding, include ? FilterOperator.GreaterThanOrEqual : FilterOperator.GreaterThan, start.Value), Comparison(domain, field.Binding, include ? FilterOperator.LessThanOrEqual : FilterOperator.LessThan, end.Value));
        }

        if (criterion.CustomType is FilterCustomType.IsNull or FilterCustomType.IsNotNull)
        {
            Allow(criterion.CustomType == FilterCustomType.IsNull ? FilterOperator.IsNull : FilterOperator.IsNotNull);
            return Comparison(domain, field.Binding, criterion.CustomType == FilterCustomType.IsNull ? FilterOperator.IsNull : FilterOperator.IsNotNull, new());
        }

        if (criterion.CustomType is FilterCustomType.NumericRange)
        {
            if (field.Kind is not ("Integer" or "Decimal")) throw Invalid();
            Allow(FilterOperator.GreaterThanOrEqual, FilterOperator.LessThanOrEqual);
            var min = AuditQueryValues.Convert(Parameter("min"), field.Kind);
            var max = AuditQueryValues.Convert(Parameter("max"), field.Kind);
            if (min.IsFailure || max.IsFailure || min.Value.Kind == "Null" || max.Value.Kind == "Null" || AuditQueryEvaluator.Compare(min.Value, max.Value, false) > 0) throw Invalid();
            return And(Comparison(domain, field.Binding, FilterOperator.GreaterThanOrEqual, min.Value), Comparison(domain, field.Binding, FilterOperator.LessThanOrEqual, max.Value));
        }

        if (criterion.CustomType is not (FilterCustomType.EnumValues or FilterCustomType.TextIn or FilterCustomType.TextNotIn or FilterCustomType.NumericIn or FilterCustomType.NumericNotIn)) throw Invalid();
        if (criterion.CustomType is FilterCustomType.NumericIn or FilterCustomType.NumericNotIn && field.Kind is not ("Integer" or "Decimal")) throw Invalid();
        if (criterion.CustomType is FilterCustomType.TextIn or FilterCustomType.TextNotIn && field.Kind is not ("String" or "Guid")) throw Invalid();
        var raw = Parameter("values");
        var values = raw switch
        {
            string text => text.Split(';', StringSplitOptions.TrimEntries).Cast<object>().ToArray(),
            JsonElement { ValueKind: JsonValueKind.String } json => (json.GetString() ?? "").Split(';', StringSplitOptions.TrimEntries).Cast<object>().ToArray(),
            JsonElement { ValueKind: JsonValueKind.Array } json => json.EnumerateArray().Select(e => (object)e.Clone()).ToArray(),
            System.Collections.IEnumerable sequence when raw is not string => sequence.Cast<object>().ToArray(),
            _ => throw Invalid()
        };
        if (values.Length is 0 or > 32 || field.Kind == "Collection") throw Invalid();
        var negative = criterion.CustomType is FilterCustomType.TextNotIn or FilterCustomType.NumericNotIn;
        Allow(negative ? FilterOperator.NotEqual : FilterOperator.Equal);
        var expressions = values.Select(value =>
        {
            var converted = AuditQueryValues.Convert(value, field.Kind);
            if (converted.IsFailure) throw Invalid();
            ValidateEnum(field, converted.Value);
            return Comparison(domain, field.Binding, negative ? FilterOperator.NotEqual : FilterOperator.Equal, converted.Value);
        }).ToImmutableArray();
        return negative ? And(expressions.ToArray()) : new AuditPredicate { Kind = "Or", Children = expressions };
    }
    private AuditPredicate FullText(object term, object rawFields, string domain)
    {
        if (domain != "Record") throw Invalid();
        var converted = AuditQueryValues.Convert(term, "String");
        if (converted.IsFailure || string.IsNullOrWhiteSpace(converted.Value.Text) || converted.Value.Text.Length > 256) throw Invalid();
        var fields = rawFields switch
        {
            string[] names => names,
            object[] names => names.Select(value => ScalarText(value) ?? throw Invalid()).ToArray(),
            JsonElement { ValueKind: JsonValueKind.Array } json => json.EnumerateArray().Select(value => ScalarText(value) ?? throw Invalid()).ToArray(),
            _ => throw Invalid()
        };
        if (fields.Length is < 1 or > 16) throw Invalid();
        AuditPredicate Child(string owner, string collection, AuditPredicate child) => new() { Kind = "Collection", Domain = owner, Collection = collection, Operator = FilterOperator.Any, Children = [child] };
        AuditPredicate Match(string owner, string name) => Comparison(owner, this.Field(owner, name).Binding, FilterOperator.Contains, converted.Value);
        AuditPredicate Build(string path) => path.ToUpperInvariant() switch
        {
            "KEY" => Match("Record", "Key"), "DISPLAYNAME" => Match("Record", "DisplayName"),
            "ACTIONS.KEY" => Child("Record", "Actions", Match("Action", "Key")),
            "ACTIONS.DISPLAYNAME" => Child("Record", "Actions", Match("Action", "DisplayName")),
            "ACTIONS.REQUESTTYPE" => Child("Record", "Actions", Match("Action", "RequestType")),
            "ACTIONS.HANDLERTYPE" => Child("Record", "Actions", Match("Action", "HandlerType")),
            "RESOURCES.TYPE" => Child("Record", "Resources", Match("Resource", "Type")),
            "RESOURCES.DISPLAYNAME" => Child("Record", "Resources", Match("Resource", "DisplayName")),
            "ACTIONS.RESOURCES.TYPE" => Child("Record", "Actions", Child("Action", "Resources", Match("Resource", "Type"))),
            "ACTIONS.RESOURCES.DISPLAYNAME" => Child("Record", "Actions", Child("Action", "Resources", Match("Resource", "DisplayName"))),
            "ERRORS.CODE" => Child("Record", "Errors", Match("Error", "Code")),
            "ERRORS.EXCEPTIONTYPE" => Child("Record", "Errors", Match("Error", "ExceptionType")),
            "ACTIONS.ERRORS.CODE" => Child("Record", "Actions", Child("Action", "Errors", Match("Error", "Code"))),
            _ => throw Invalid()
        };
        return new AuditPredicate { Kind = "Or", Children = fields.Select(Build).ToImmutableArray() };
    }
    private AuditQueryField Field(string domain, string name)
    {
        var result = catalog.Find(domain, name);
        return result.IsSuccess ? result.Value : throw Invalid();
    }
    private static bool Supports(AuditQueryField field, FilterOperator op)
    {
        if (field.Detail is { Operators.Count: > 0 } detail && !field.IsMarker && !detail.Operators.Contains(op)) return false;
        if (field.Binding == "StorageState" && op is not (FilterOperator.Equal or FilterOperator.NotEqual or FilterOperator.IsNull or FilterOperator.IsNotNull)) return false;
        if (op is FilterOperator.Any or FilterOperator.All or FilterOperator.None) return false;
        if (field.Kind == "String") return true;
        if (op is FilterOperator.Equal or FilterOperator.NotEqual or FilterOperator.IsNull or FilterOperator.IsNotNull) return true;
        return field.Kind is "Integer" or "Decimal" or "Timestamp" && op is FilterOperator.GreaterThan or FilterOperator.GreaterThanOrEqual or FilterOperator.LessThan or FilterOperator.LessThanOrEqual;
    }
    private static void ValidateEnum(AuditQueryField field, AuditQueryValue value)
    {
        if (value.Kind == "Null") return;
        var allowed = field.Binding switch
        {
            "StorageState" => new[] { "Active", "Archived" },
            "Outcome" => Enum.GetNames<AuditOutcome>(),
            "Completeness" => Enum.GetNames<AuditCompleteness>(),
            "PersistenceState" => ["Committed", "RolledBack", "Unknown"],
            "ExecutionStatus" => ["Invoked", "ShortCircuited", "Skipped", "Incomplete"],
            _ => null
        };
        if (allowed is not null && !allowed.Contains(value.Text, field.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)) throw Invalid();
    }
    private static AuditPredicate Comparison(string domain, string field, FilterOperator op, AuditQueryValue value) => new() { Kind = "Comparison", Domain = domain, Field = field, Operator = op, Value = value };
    private static AuditQueryValue Timestamp(DateTimeOffset value) => new() { Kind = "Timestamp", Ticks = value.UtcTicks };
    private static bool HasField(AuditPredicate node, string field) => node.Field?.Equals(field, StringComparison.OrdinalIgnoreCase) == true || node.Children.Any(child => HasField(child, field));
    private static bool LifecycleDependent(AuditPredicate node)
    {
        static AuditPredicate Simplify(AuditPredicate current, string state)
        {
            if (current.Kind == "Comparison" && current.Field == "StorageState")
            {
                var matches = current.Operator switch
                {
                    FilterOperator.Equal => current.Value.Text == state,
                    FilterOperator.NotEqual => current.Value.Text != state,
                    FilterOperator.IsNull => false,
                    FilterOperator.IsNotNull => true,
                    _ => throw Invalid()
                };
                return new AuditPredicate { Kind = matches ? "True" : "False" };
            }

            if (current.Kind is not ("And" or "Or")) return current;
            var children = current.Children.Select(c => Simplify(c, state)).ToImmutableArray();
            if (current.Kind == "And")
            {
                if (children.Any(c => c.Kind == "False")) return new AuditPredicate { Kind = "False" };
                return And(children.ToArray());
            }

            if (children.Any(c => c.Kind == "True")) return new AuditPredicate();
            children = children.Where(c => c.Kind != "False").ToImmutableArray();
            return children.Length == 0 ? new AuditPredicate { Kind = "False" } : children.Length == 1 ? children[0] : current with { Children = children };
        }

        var active = JsonSerializer.SerializeToElement(Simplify(node, "Active"));
        var archived = JsonSerializer.SerializeToElement(Simplify(node, "Archived"));
        return !AuditCanonicalizerV1.Encode(active).AsSpan().SequenceEqual(AuditCanonicalizerV1.Encode(archived));
    }
    private static string Lifecycle(AuditPredicate node)
    {
        var states = Flatten(node).Where(p => p.Field == "StorageState" && p.Operator == FilterOperator.Equal).Select(p => p.Value.Text).Distinct(StringComparer.Ordinal).ToArray();
        return states.Length == 1 ? states[0] : "All";
    }
    private static IEnumerable<AuditPredicate> Flatten(AuditPredicate node) { yield return node; foreach (var child in node.Children) foreach (var item in Flatten(child)) yield return item; }
    private static (DateTimeOffset? From, DateTimeOffset? To) Boundaries(AuditPredicate predicate)
    {
        if (predicate.Kind == "Comparison" && predicate.Domain == "Record" && predicate.Field == "StartedUtc" && predicate.Value.Ticks is long ticks)
        {
            var timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            return predicate.Operator switch
            {
                FilterOperator.Equal => (timestamp, timestamp.AddTicks(1)),
                FilterOperator.GreaterThanOrEqual => (timestamp, null),
                FilterOperator.GreaterThan => (timestamp.AddTicks(1), null),
                FilterOperator.LessThan => (null, timestamp),
                FilterOperator.LessThanOrEqual => (null, timestamp.AddTicks(1)),
                _ => (null, null)
            };
        }

        if (predicate.Kind is not ("And" or "Or") || predicate.Children.Length == 0) return (null, null);
        var children = predicate.Children.Select(Boundaries).ToArray();
        var starts = children.Where(c => c.From.HasValue).Select(c => c.From.Value).ToArray();
        var ends = children.Where(c => c.To.HasValue).Select(c => c.To.Value).ToArray();
        if (predicate.Kind == "And") return (starts.Length == 0 ? null : starts.Max(), ends.Length == 0 ? null : ends.Min());
        return (starts.Length != children.Length ? null : starts.Min(), ends.Length != children.Length ? null : ends.Max());
    }
    private static void ValidateModel(FilterModel model)
    {
        if (model.Page < 0 || model.PageSize < 0 || model.PageSize > AuditQueryOptions.MAX_PAGE_SIZE || model.Orderings?.Count > 4 || model.Includes is { Count: > 0 } || !model.NoTracking || model.HierarchyMaxDepth != 5 || !string.IsNullOrWhiteSpace(model.Hierarchy)) throw Invalid();
    }
    private static string ScalarText(object value) => value switch { string text => text, JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(), _ => null };
    private static ArgumentException Invalid() => new("AuditQueryInvalid");
    private static JsonSerializerOptions CreateFilterJson() => AuditFilterJson.CreateOptions();
}
