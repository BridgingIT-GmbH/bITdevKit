// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Translates only catalog-validated immutable predicates into parameterized relational and JSON expressions.</summary>
/// <example><code>var sql = new AuditQuerySql(dialect, catalog); var where = sql.Predicate(plan.Predicate);</code></example>
public sealed class AuditQuerySql(AuditSqlDialect dialect, AuditFieldCatalog catalog)
{
    private readonly List<object> values = [];
    private int aliases;

    /// <summary>The ordered immutable-value parameter snapshot for the generated command.</summary>
    /// <example><code>await using var command = dialect.Command(text, sql.Values);</code></example>
    public IReadOnlyList<object> Values => this.values.AsReadOnly();

    /// <summary>Adds an opaque value as a provider-created parameter, never as SQL syntax.</summary>
    /// <example><code>var id = sql.Parameter(auditId);</code></example>
    public string Parameter(object value) { this.values.Add(value); return "@p" + (this.values.Count - 1); }

    /// <summary>Compiles complete criteria with same-child aliases and mandatory predicates already composed outside client OR.</summary>
    /// <example><code>var condition = sql.Predicate(plan.Predicate);</code></example>
    public string Predicate(AuditPredicate predicate, string rootAlias = "r") => this.Compile(predicate, new("Record", this.SearchJson("Record", rootAlias), rootAlias, rootAlias), 0);

    /// <summary>Produces explicit null ordering and deterministic approved root ordering.</summary>
    /// <example><code>var order = sql.Order(plan);</code></example>
    public string Order(AuditSearchPlan plan, string rootAlias = "r")
    {
        var terms = new List<string>();
        foreach (var order in plan.Orderings)
        {
            var field = this.Field("Record", order.Field);
            var scalar = this.Scalar(field, this.SearchJson("Record", rootAlias), rootAlias);
            var direction = order.Direction == OrderDirection.Descending ? "DESC" : "ASC";
            if (!Enum.IsDefined(order.Direction) || field.Kind == "Collection") throw Invalid();
            terms.Add("CASE WHEN " + scalar.Comparable + " THEN 1 ELSE 0 END " + direction);
            terms.Add(scalar.Selected + " " + direction);
        }

        if (terms.Count == 0) throw Invalid();
        return string.Join(",", terms);
    }

    /// <summary>Resolves a trusted scalar expression for bounded group/facet operations.</summary>
    /// <example><code>var scalar = sql.RootField("Key");</code></example>
    public AuditSqlField RootField(string name, string rootAlias = "r") => this.Scalar(this.Field("Record", name), this.SearchJson("Record", rootAlias), rootAlias);

    /// <summary>Resolves one catalog field on a trusted relational owned-row alias.</summary>
    /// <example><code>var label = sql.OwnedField("Action", "DisplayName", "a");</code></example>
    public AuditSqlField OwnedField(string domain, string name, string alias, string rootAlias = "r") => this.Scalar(this.Field(domain, name), this.SearchJson(domain, alias), rootAlias);

    /// <summary>Compiles a complete same-action predicate for an owned action query.</summary>
    /// <example><code>var condition = sql.ActionPredicate(resourcePredicate);</code></example>
    public string ActionPredicate(AuditPredicate predicate, string alias = "a", string rootAlias = "r") => this.Compile(predicate, new("Action", this.SearchJson("Action", alias), alias, rootAlias), 0);

    /// <summary>Applies backend-native bounded offset paging after filtering and ordering.</summary>
    /// <example><code>var paging = sql.Page(plan);</code></example>
    public string Page(AuditSearchPlan plan)
    {
        if (plan.Page < 1 || plan.PageSize is < 1 or > AuditQueryOptions.MAX_PAGE_SIZE) throw Invalid();
        var offset = ((long)plan.Page - 1) * plan.PageSize;
        return dialect.Backend == "SqlServer" ? " OFFSET " + this.Parameter(offset) + " ROWS FETCH NEXT " + this.Parameter(plan.PageSize) + " ROWS ONLY"
            : " LIMIT " + this.Parameter(plan.PageSize) + " OFFSET " + this.Parameter(offset);
    }

    private string Compile(AuditPredicate predicate, Source source, int depth)
    {
        if (predicate is null || depth > 16) throw Invalid();
        return predicate.Kind switch
        {
            "True" => "1=1", "False" => "1=0",
            "And" => predicate.Children.Length == 0 ? "1=1" : "(" + string.Join(" AND ", predicate.Children.Select(child => this.Compile(child, source, depth + 1))) + ")",
            "Or" => predicate.Children.Length == 0 ? "1=0" : "(" + string.Join(" OR ", predicate.Children.Select(child => this.Compile(child, source, depth + 1))) + ")",
            "Comparison" => this.Comparison(predicate, source),
            "Collection" => this.Collection(predicate, source, depth),
            "ResourceMatch" when source.Domain == "Record" => this.Compile(Resource(predicate.Resource), source, depth + 1),
            _ => throw Invalid()
        };
    }
    private string Comparison(AuditPredicate predicate, Source source)
    {
        if (predicate.Domain != source.Domain || !Enum.IsDefined(predicate.Operator)) throw Invalid();
        var field = this.Field(source.Domain, predicate.Field);
        if (field.Kind == "Collection") throw Invalid();
        var actual = this.Scalar(field, source.Json, source.RootAlias);
        var op = predicate.Operator;
        var expected = predicate.Value ?? throw Invalid();
        if (field.Binding == "StorageState") return this.StateComparison(actual.Selected, op, expected);
        if (op == FilterOperator.IsNull) return actual.Kind + "='Null'";
        if (op == FilterOperator.IsNotNull) return actual.Comparable;
        if (op is FilterOperator.IsEmpty or FilterOperator.IsNotEmpty)
            return Wrap(actual.Kind + "='String' AND " + actual.Selected + (op == FilterOperator.IsEmpty ? "=" : "<>") + this.Parameter(AuditSearchEncoding.Text("", field.IgnoreCase)));
        if (expected.Kind == "Null") return op switch
        {
            FilterOperator.Equal => actual.Kind + "='Null'",
            FilterOperator.NotEqual => actual.Comparable,
            _ => throw Invalid()
        };
        if (expected.Kind != field.Kind || AuditSearchEncoding.Scalar(expected, field.IgnoreCase) is not string encoded) throw Invalid();
        if (source.Domain == "Record" && field.Binding is "StartedUtc" or "CompletedUtc" && op is FilterOperator.Equal or FilterOperator.NotEqual or FilterOperator.GreaterThan or FilterOperator.GreaterThanOrEqual or FilterOperator.LessThan or FilterOperator.LessThanOrEqual)
        {
            var column = dialect.Column<AuditRecordEntity>(source.RootAlias, field.Binding + "Ticks");
            return column + Operator(op) + this.Parameter(expected.Ticks.GetValueOrDefault());
        }

        var parameter = this.Parameter(encoded);
        var expression = op switch
        {
            FilterOperator.Equal or FilterOperator.NotEqual or FilterOperator.GreaterThan or FilterOperator.GreaterThanOrEqual or FilterOperator.LessThan or FilterOperator.LessThanOrEqual => actual.Selected + Operator(op) + parameter,
            FilterOperator.Contains or FilterOperator.StartsWith or FilterOperator.EndsWith or FilterOperator.DoesNotContain or FilterOperator.DoesNotStartWith or FilterOperator.DoesNotEndWith when field.Kind == "String" => this.Like(actual.Selected, op, parameter),
            _ => throw Invalid()
        };
        return Wrap((op == FilterOperator.NotEqual ? actual.Kind + "='Null' OR " : "") + "(" + actual.Comparable + " AND " + expression + ")");
    }
    private string StateComparison(string column, FilterOperator op, AuditQueryValue expected)
    {
        if (op == FilterOperator.IsNull) return "1=0";
        if (op == FilterOperator.IsNotNull) return "1=1";
        if (expected.Kind == "Null") return op == FilterOperator.NotEqual ? "1=1" : op == FilterOperator.Equal ? "1=0" : throw Invalid();
        var state = expected.Text switch { "Active" => 0, "Archived" => 1, _ => throw Invalid() };
        if (op is not (FilterOperator.Equal or FilterOperator.NotEqual)) throw Invalid();
        return column + Operator(op) + this.Parameter(state);
    }
    private string Collection(AuditPredicate predicate, Source source, int depth)
    {
        if (predicate.Domain != source.Domain || predicate.Children.Length != 1) throw Invalid();
        var field = this.Field(source.Domain, predicate.Collection);
        if (field.Kind != "Collection") throw Invalid();
        var alias = "a" + this.aliases++;
        string table, join;
        Source child;
        if (source.Domain == "Record" && field.Binding is "Actions" or "EntityChanges")
        {
            var action = field.Binding == "Actions";
            table = (action ? dialect.Table<AuditActionEntity>() : dialect.Table<AuditEntityChangeEntity>()) + " " + alias;
            join = (action ? dialect.Column<AuditActionEntity>(alias, nameof(AuditActionEntity.AuditId)) : dialect.Column<AuditEntityChangeEntity>(alias, nameof(AuditEntityChangeEntity.AuditId))) + "=" + dialect.Column<AuditRecordEntity>(source.RootAlias, nameof(AuditRecordEntity.AuditId));
            child = new(field.TargetDomain, this.SearchJson(field.TargetDomain, alias), alias, source.RootAlias);
        }
        else
        {
            table = dialect.Backend switch
            {
                "SqlServer" => "OPENJSON(" + source.Json + ", '$." + field.Binding + "') " + alias,
                "Postgres" => "jsonb_array_elements(COALESCE((" + source.Json + ")::jsonb -> '" + field.Binding + "', '[]'::jsonb)) AS " + alias + "(value)",
                _ => "json_each(" + source.Json + ", '$." + field.Binding + "') " + alias
            };
            join = "1=1";
            child = new(field.TargetDomain, alias + "." + dialect.Quote("value"), null, source.RootAlias);
        }

        var condition = this.Compile(predicate.Children[0], child, depth + 1);
        if (predicate.Operator == FilterOperator.All) condition = "NOT (" + condition + ")";
        else if (predicate.Operator is not (FilterOperator.Any or FilterOperator.None)) throw Invalid();
        return (predicate.Operator is FilterOperator.All or FilterOperator.None ? "NOT " : "") + "EXISTS (SELECT 1 FROM " + table + " WHERE " + join + " AND (" + condition + "))";
    }
    private AuditSqlField Scalar(AuditQueryField field, string json, string rootAlias)
    {
        if (field.Binding == "StorageState") return new(field, "'String'", dialect.Column<AuditRecordEntity>(rootAlias, nameof(AuditRecordEntity.StorageState)), "1=1", true);
        var path = "$.fields." + AuditSearchEncoding.FieldKey(field.Binding) + "." + field.Kind;
        string Read(string member) => dialect.Backend switch
        {
            "SqlServer" => "(SELECT v FROM OPENJSON(" + json + ", '" + path + "') WITH (v nvarchar(max) '$." + member + "'))",
            "Postgres" => "((" + json + ")::jsonb #>> '{fields," + AuditSearchEncoding.FieldKey(field.Binding) + "," + field.Kind + "," + member + "}')",
            _ => "json_extract(" + json + ", '" + path + "." + member + "')"
        };
        var kind = "COALESCE(" + Read("kind") + ", '" + (field.IsMarker ? "String" : "Missing") + "')";
        var selected = Read(field.IgnoreCase ? "folded" : "exact");
        var exact = Read("exact");
        if (field.IsMarker)
        {
            selected = "COALESCE(" + selected + ", '" + AuditSearchEncoding.Text("Missing", field.IgnoreCase) + "')";
            exact = "COALESCE(" + exact + ", '" + AuditSearchEncoding.Text("Missing", false) + "')";
        }

        if (field.Domain == "Record" && field.Binding == "HasTruncation")
        {
            // Version-one indexes counted only omitted actions/details. Derive the
            // missing evidence from the frozen header so historical queries remain correct.
            var root = dialect.Column<AuditRecordEntity>(rootAlias, nameof(AuditRecordEntity.RootJson));
            var completeness = dialect.Backend switch
            {
                "SqlServer" => "JSON_VALUE(" + root + ", '$.completeness')",
                "Postgres" => "((" + root + ")::jsonb ->> 'completeness')",
                _ => "json_extract(" + root + ", '$.completeness')"
            };
            var notes = dialect.Backend switch
            {
                "SqlServer" => "OPENJSON(" + root + ", '$.notes') WITH (code nvarchar(64) '$.code') n",
                "Postgres" => "jsonb_array_elements(COALESCE((" + root + ")::jsonb -> 'notes', '[]'::jsonb)) n(value)",
                _ => "json_each(" + root + ", '$.notes') n"
            };
            var code = dialect.Backend switch
            {
                "SqlServer" => "n.code",
                "Postgres" => "(n.value ->> 'code')",
                _ => "json_extract(n.value, '$.code')"
            };
            var incomplete = "'AuditRootIncomplete','AuditActionIncomplete','AuditActiveChildrenAtClosure','AuditOutOfOrderClosure','AuditCaptureExpired','AuditRuntimeStopped'";
            var truncated = "CASE WHEN " + selected + "='1' OR " + dialect.Ordinal(completeness) + "='Truncated' OR EXISTS (SELECT 1 FROM " + notes + " WHERE " + dialect.Ordinal(code) + " NOT IN (" + incomplete + ")) THEN '1' ELSE '0' END";
            return new(field, "'Boolean'", truncated, "1=1", false) { Exact = truncated };
        }

        var comparable = kind + " IN ('String','Guid','Integer','Decimal','Timestamp','Boolean')";
        return new(field, kind, dialect.Ordinal(selected), comparable, false) { Exact = dialect.Ordinal(exact) };
    }
    private string Like(string column, FilterOperator op, string parameter)
    {
        var prefix = op is FilterOperator.Contains or FilterOperator.DoesNotContain or FilterOperator.EndsWith or FilterOperator.DoesNotEndWith;
        var suffix = op is FilterOperator.Contains or FilterOperator.DoesNotContain or FilterOperator.StartsWith or FilterOperator.DoesNotStartWith;
        var concatenate = dialect.Backend == "SqlServer" ? "+" : "||";
        var pattern = (prefix ? "'%'" + concatenate : "") + parameter + (suffix ? concatenate + "'%'" : "");
        return column + (op is FilterOperator.DoesNotContain or FilterOperator.DoesNotStartWith or FilterOperator.DoesNotEndWith ? " NOT LIKE " : " LIKE ") + "(" + pattern + ")";
    }
    private string SearchJson(string domain, string alias) => domain switch
    {
        "Record" => dialect.Column<AuditRecordEntity>(alias, nameof(AuditRecordEntity.SearchJson)),
        "Action" => dialect.Column<AuditActionEntity>(alias, nameof(AuditActionEntity.SearchJson)),
        "Change" => dialect.Column<AuditEntityChangeEntity>(alias, nameof(AuditEntityChangeEntity.SearchJson)),
        _ => throw Invalid()
    };
    private AuditQueryField Field(string domain, string name)
    {
        var found = catalog.Find(domain, name);
        return found.IsSuccess ? found.Value : throw Invalid();
    }
    private static AuditPredicate Resource(AuditResourceReference resource)
    {
        if (resource is null) throw Invalid();
        var match = AuditSearchPlanBuilder.And(
            new() { Kind = "Comparison", Domain = "Resource", Field = "Type", Value = new() { Kind = "String", Text = resource.Type } },
            new() { Kind = "Comparison", Domain = "Resource", Field = "Id", Value = new() { Kind = "String", Text = resource.Id } });
        var root = new AuditPredicate { Kind = "Collection", Collection = "Resources", Operator = FilterOperator.Any, Children = [match] };
        var action = root with { Domain = "Action" };
        return new() { Kind = "Or", Children = [root, new() { Kind = "Collection", Collection = "Actions", Operator = FilterOperator.Any, Children = [action] }] };
    }
    private static string Operator(FilterOperator op) => op switch { FilterOperator.Equal => "=", FilterOperator.NotEqual => "<>", FilterOperator.GreaterThan => ">", FilterOperator.GreaterThanOrEqual => ">=", FilterOperator.LessThan => "<", FilterOperator.LessThanOrEqual => "<=", _ => throw Invalid() };
    private static string Wrap(string expression) => "(CASE WHEN (" + expression + ") THEN 1 ELSE 0 END=1)";
    private static ArgumentException Invalid() => new("AuditQueryInvalid");
    private sealed record Source(string Domain, string Json, string RowAlias, string RootAlias);
}

/// <summary>Trusted SQL expressions for one catalog field; these are never sent to clients.</summary>
/// <example><code>var grouped = sql.RootField("Outcome");</code></example>
public sealed record AuditSqlField(AuditQueryField Field, string Kind, string Selected, string Comparable, bool IsStorageState)
{
    /// <summary>Exact normalized representative, independent of the selected field's case-folding policy.</summary>
    /// <example><code>var representative = scalar.Exact;</code></example>
    public string Exact { get; init; } = Selected;
}
