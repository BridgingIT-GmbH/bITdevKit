// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

public class AuditQueryTests
{
    private static readonly DateTimeOffset now = DateTimeOffset.Parse("2026-10-30T12:00:00Z");

    [Theory]
    [InlineData("failure")]
    [InlineData("null")]
    [InlineData("throw")]
    [InlineData("invalid")]
    [InlineData("paging")]
    public async Task ReadScope_UnavailableOrInvalid_PerformsZeroProviderReads(string mode)
    {
        var spy = new ReadSpy();
        var resolutions = 0;
        using var host = Host(builder => builder.WithStorageProvider(_ => spy).WithReadScope((services, ct) =>
        {
            resolutions++;
            return mode switch
            {
                "throw" => throw new InvalidOperationException("sensitive context"),
                "failure" => Task.FromResult(Result<AuditReadScope>.Failure()),
                "null" => Task.FromResult(Result<AuditReadScope>.Success(null)),
                "invalid" => Task.FromResult(Result<AuditReadScope>.Success(new(new() { Filters = [new("Unknown", FilterOperator.Equal, "x")] }, "v1"))),
                _ => Task.FromResult(Result<AuditReadScope>.Success(new(new() { PageSize = 1, Filters = [new("Key", FilterOperator.Equal, "x")] }, "v1")))
            };
        }));
        using var scope = host.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        (await queries.SearchAsync()).IsFailure.ShouldBeTrue();
        (await queries.FindAsync(Guid.NewGuid())).IsFailure.ShouldBeTrue();
        (await queries.FindRelatedAsync(Guid.NewGuid())).IsFailure.ShouldBeTrue();
        (await queries.AggregateAsync(null, "Key")).IsFailure.ShouldBeTrue();
        (await queries.FindFacetsAsync(null, "Key")).IsFailure.ShouldBeTrue();
        (await queries.GetResourceHistoryAsync(new("Customer", "42"))).IsFailure.ShouldBeTrue();
        spy.Reads.ShouldBe(0);
        resolutions.ShouldBe(6);
    }

    [Fact]
    public async Task ReadScope_ClientOrCannotWidenAnyReadFamilyOrCounts()
    {
        var resolutions = 0;
        var trusted = new FilterModel { Filters = [new("Details.tenantId", FilterOperator.Equal, "A")] };
        var decision = new AuditReadScope(trusted, "tenant-v1");
        trusted.Filters[0].Value = "B";
        using var host = Host(builder => builder.WithSearchableDetail(new() { Name = "tenantId", ExposeValue = false })
            .WithReadScope((sp, ct) => { resolutions++; return Task.FromResult(Result<AuditReadScope>.Success(decision)); }));
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var resource = new AuditResourceReference("Customer", "42");
        var own = Record("own") with { Resources = [resource], Details = Details("tenantId", AuditValue.From("A")) };
        var other = Record("other") with { OriginAuditId = own.OriginAuditId, Resources = [resource], Details = Details("tenantId", AuditValue.From("B")) };
        await Add(provider, own, other);
        using var scope = host.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        var client = new FilterModel { Filters = [new("Key", FilterOperator.Equal, "other") { Logic = FilterLogicOperator.Or }, new("Key", FilterOperator.Equal, "own")] };
        var page = await queries.SearchAsync(client);
        page.IsSuccess.ShouldBeTrue();
        page.TotalCount.ShouldBe(1);
        page.Value.Single().AuditId.ShouldBe(own.AuditId);
        (await queries.FindAsync(other.AuditId)).IsFailure.ShouldBeTrue();
        var details = (await queries.FindAsync(own.AuditId)).Value;
        details.Record.Details["tenantId"].Marker.ShouldBe("Redacted");
        details.IsTransformed.ShouldBeTrue();
        (await provider.FindAsync(own.AuditId)).Value.Details["tenantId"].Data.GetString().ShouldBe("A");
        (await queries.AggregateAsync(client, "Key")).Value.Single().Key.ShouldBe("own");
        (await queries.FindFacetsAsync(client, "Key")).Value.ShouldBe(["own"]);
        (await queries.GetResourceHistoryAsync(resource, client)).TotalCount.ShouldBe(1);
        (await queries.FindRelatedAsync(own.AuditId)).TotalCount.ShouldBe(0);
        resolutions.ShouldBe(7);
    }

    [Fact]
    public async Task Search_CountsBeforePagingAndAddsStableIdentityOrder()
    {
        using var host = Host();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var ids = Enumerable.Range(1, 7).Select(i => Guid.Parse($"00000000-0000-0000-0000-{i:D12}")).ToArray();
        await Add(provider, ids.Reverse().Select(id => Record("same") with { AuditId = id }).ToArray());
        using var scope = host.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        var page = await queries.SearchAsync(new() { Page = 2, PageSize = 2, Orderings = [new() { Field = "Key", Direction = OrderDirection.Ascending }] });
        page.TotalCount.ShouldBe(7);
        page.CurrentPage.ShouldBe(2);
        page.Value.Select(r => r.AuditId).ShouldBe([ids[4], ids[3]]);
        page.Value.All(r => r.IntegrityStatus == "NotChecked").ShouldBeTrue();
    }

    [Theory]
    [InlineData(250)]
    [InlineData(500)]
    public async Task SearchAndHistory_LargePages_PreserveCountsAndPaging(int pageSize)
    {
        using var host = Host();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var resource = new AuditResourceReference("Customer", "42");
        await Add(provider, Enumerable.Range(0, pageSize + 1).Select(index => Record($"execution-{index}") with { Resources = [resource] }).ToArray());
        using var scope = host.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        var filter = new FilterModel { PageSize = pageSize };

        var search = await queries.SearchAsync(filter);
        var history = await queries.GetResourceHistoryAsync(resource, filter);
        filter.Page = 2;
        var nextSearch = await queries.SearchAsync(filter);
        var nextHistory = await queries.GetResourceHistoryAsync(resource, filter);

        search.IsSuccess.ShouldBeTrue();
        search.TotalCount.ShouldBe(pageSize + 1);
        search.Value.Count().ShouldBe(pageSize);
        history.IsSuccess.ShouldBeTrue();
        history.TotalCount.ShouldBe(pageSize + 1);
        history.Value.Count().ShouldBe(pageSize);
        nextSearch.IsSuccess.ShouldBeTrue();
        nextSearch.Value.ShouldHaveSingleItem();
        nextHistory.IsSuccess.ShouldBeTrue();
        nextHistory.Value.ShouldHaveSingleItem();
        search.Value.Select(item => item.AuditId).ShouldNotContain(nextSearch.Value.Single().AuditId);
        history.Value.Select(item => item.Record.AuditId).ShouldNotContain(nextHistory.Value.Single().Record.AuditId);
    }

    [Fact]
    public async Task Search_ExclusiveUpperAndNestedHistoricalRangeArePreserved()
    {
        using var host = Host();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var earlier = Record("earlier") with { StartedUtc = now.AddDays(-10), CompletedUtc = now.AddDays(-10).AddMinutes(1) };
        await Add(provider, earlier, Record("at-boundary") with { StartedUtc = now });
        using var scope = host.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        (await queries.SearchAsync()).TotalCount.ShouldBe(0);
        var filter = new FilterModel { Filters = [new() { Filters = [new("StartedUtc", FilterOperator.GreaterThanOrEqual, now.AddDays(-11)), new("StartedUtc", FilterOperator.LessThan, now.AddDays(-9))] }] };
        var page = await queries.SearchAsync(filter);
        page.IsSuccess.ShouldBeTrue();
        page.Value.Single().AuditId.ShouldBe(earlier.AuditId);
    }

    [Fact]
    public void CollectionConditions_BindAllConditionsToOneOccurrence()
    {
        var record = Record("root") with { Actions = [Action("rename", AuditOutcome.Failed), Action("create", AuditOutcome.Succeeded)] };
        var filter = new FilterModel { Filters = [new("Actions", FilterOperator.Any, null) { Filters = [new("Key", FilterOperator.Equal, "rename"), new("Outcome", FilterOperator.Equal, "Succeeded")] }] };
        Matches(filter, record).ShouldBeFalse();
        var sameOccurrence = record with { Actions = record.Actions.Add(Action("rename", AuditOutcome.Succeeded)) };
        Matches(filter, sameOccurrence).ShouldBeTrue();
        var named = new FilterModel { Filters = [new("audit.action-match", [new FilterCriteria("Key", FilterOperator.Equal, "rename"), new FilterCriteria("Outcome", FilterOperator.Equal, "Succeeded")])] };
        Matches(named, record).ShouldBeFalse();
        Matches(named, sameOccurrence).ShouldBeTrue();
    }

    [Fact]
    public void CommittedChangeDefaultAndPropertyBindingDoNotMixEvidence()
    {
        var record = Record("root") with
        {
            EntityChanges = [new() { Id = Guid.NewGuid(), EntityType = "Customer", EntityId = "1", PersistenceState = "Unknown", Properties = [new() { Path = "Name", Operation = "Replace" }] },
                new() { Id = Guid.NewGuid(), EntityType = "Customer", EntityId = "2", PersistenceState = "Committed", Properties = [new() { Path = "Address", Operation = "Replace" }] }]
        };
        var filter = new FilterModel { Filters = [new("EntityChanges", FilterOperator.Any, null) { Filters = [new("EntityType", FilterOperator.Equal, "Customer"), new("Properties", FilterOperator.Any, null) { Filters = [new("Path", FilterOperator.Equal, "Name")] }] }] };
        Matches(filter, record).ShouldBeFalse();
        filter.Filters[0].Filters.Add(new("PersistenceState", FilterOperator.Equal, "Unknown"));
        Matches(filter, record).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Ä", "ä")]
    [InlineData("Σ", "ς")]
    [InlineData("İ", "i")]
    [InlineData("𐐀", "𐐨")]
    public void StringMatching_UsesOrdinalIgnoreCaseWhileOpaqueIdsStayExact(string stored, string query)
    {
        var record = Record(stored) with { CorrelationId = stored };
        Matches(new() { Filters = [new("Key", FilterOperator.Equal, query)] }, record).ShouldBe(StringComparer.OrdinalIgnoreCase.Equals(stored, query));
        Matches(new() { Filters = [new("CorrelationId", FilterOperator.Equal, query)] }, record).ShouldBe(StringComparer.Ordinal.Equals(stored, query));
    }

    [Fact]
    public void Details_MissingNullAndPolicyMarkersStayDistinctIncludingNegativeFilters()
    {
        var catalog = new AuditFieldCatalog([new() { Name = "count", Kind = "Integer" }]);
        var records = new[] { Record("missing"), Record("null") with { Details = Details("count", AuditValue.Json(JsonDocument.Parse("null").RootElement)) },
            Record("redacted") with { Details = Details("count", AuditValue.Policy("Redacted")) }, Record("value") with { Details = Details("count", AuditValue.From(5L)) } };
        var nullFilter = new FilterModel { Filters = [new("Details.count", FilterOperator.IsNull, null)] };
        records.Where(r => Matches(nullFilter, r, catalog)).Select(r => r.Key).ShouldBe(["null"]);
        var negative = new FilterModel { Filters = [new("Details.count", FilterOperator.NotEqual, 5)] };
        records.Where(r => Matches(negative, r, catalog)).Select(r => r.Key).ShouldBe(["null"]);
        var marker = new FilterModel { Filters = [new("Details.count.Marker", FilterOperator.Equal, "Redacted")] };
        records.Where(r => Matches(marker, r, catalog)).Select(r => r.Key).ShouldBe(["redacted"]);
        Matches(new() { Filters = [new("DisplayName", FilterOperator.IsEmpty, null)] }, Record("empty") with { DisplayName = "" }).ShouldBeTrue();
    }

    [Theory]
    [InlineData(FilterCustomType.NumericRange)]
    [InlineData(FilterCustomType.NumericNotIn)]
    [InlineData(FilterCustomType.IsNull)]
    public void CustomFilters_CannotBypassRegisteredOperatorRestrictions(FilterCustomType customType)
    {
        var catalog = new AuditFieldCatalog([new() { Name = "count", Kind = "Integer", Operators = [FilterOperator.Equal] }]);
        var filter = new FilterModel { Filters = [new(customType, new() { ["field"] = "Details.count", ["min"] = 1, ["max"] = 10, ["values"] = new[] { 1, 2 } })] };
        new AuditSearchPlanBuilder(catalog, new FakeTimeProvider(now)).Build(filter).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("{\"filters\":[{\"field\":\"Unknown\",\"operator\":\"eq\",\"value\":1}]}")]
    [InlineData("{\"filters\":[{\"field\":\"storageState\",\"operator\":\"eq\",\"value\":\"Active\"}]}")]
    [InlineData("{\"includes\":[\"Actions\"]}")]
    [InlineData("{\"hierarchy\":\"Children\"}")]
    [InlineData("{\"pageSize\":501}")]
    [InlineData("{\"filters\":[{\"field\":\"Key\",\"operator\":\"eq\",\"value\":{\"$type\":\"System.String\",\"Id\":1}}]}")]
    public void InvalidClientInput_IsNeverSilentlyIgnored(string json)
    {
        var parsed = AuditFilterJson.Parse(Encoding.UTF8.GetBytes(json));
        (parsed.IsFailure || new AuditSearchPlanBuilder(new(), new FakeTimeProvider(now)).Build(parsed.Value).IsFailure).ShouldBeTrue();
    }

    [Fact]
    public void NamedJsonValues_DoNotInvokeClientSelectedClrEnumerationConversion()
    {
        var json = JsonSerializer.Serialize(new { filters = new[] { new { specificationName = "audit.action-match", specificationArguments = new[] { new { field = "Key", @operator = "eq", value = new Dictionary<string, object> { ["$type"] = typeof(CallbackEnumeration).AssemblyQualifiedName, ["Id"] = 1 } } } } } });
        CallbackEnumeration.Calls = 0;
        var parsed = AuditFilterJson.Parse(Encoding.UTF8.GetBytes(json));
        parsed.IsSuccess.ShouldBeTrue();
        new AuditSearchPlanBuilder(new(), new FakeTimeProvider(now)).Build(parsed.Value).IsFailure.ShouldBeTrue();
        CallbackEnumeration.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ResourceHistory_IsMandatoryDistinctAndRetainsTwentyMatchingActionsWithCount()
    {
        using var host = Host();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var resource = new AuditResourceReference("Customer", "CaseSensitive");
        var matching = Record("many") with { Actions = Enumerable.Range(0, 25).Select(i => Action("rename", AuditOutcome.Succeeded) with { Resources = [resource] }).ToImmutableArray() };
        await Add(provider, matching, Record("other") with { Resources = [new("Customer", "casesensitive")] });
        using var scope = host.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        var page = await queries.GetResourceHistoryAsync(new("CUSTOMER", "CaseSensitive"), new() { Filters = [new("Key", FilterOperator.Equal, "many") { Logic = FilterLogicOperator.Or }, new("Key", FilterOperator.Equal, "other")] });
        page.TotalCount.ShouldBe(1);
        page.Value.Single().MatchingActionCount.ShouldBe(25);
        page.Value.Single().MatchingActions.Length.ShouldBe(20);
    }

    [Fact]
    public async Task FacetPrefix_IsAppliedBeforeLimitOverTheWholeScopedSelection()
    {
        using var host = Host();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        await Add(provider, Enumerable.Range(0, 105).Select(i => Record($"key-{i:D3}")).ToArray());
        using var scope = host.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        (await queries.FindFacetsAsync(null, "Key", "key-104")).Value.ShouldBe(["key-104"]);
    }

    [Fact]
    public void Snapshot_RejectsCyclesAndRetainsIndependentArgumentsAndNullMembers()
    {
        var criteria = new FilterCriteria("Key", FilterOperator.Equal, "before");
        var source = new FilterModel { Filters = [new("audit.action-match", [criteria]), null] };
        var copy = AuditFilterSnapshot.Copy(source);
        criteria.Value = "after";
        ((FilterCriteria)copy.Filters[0].SpecificationArguments[0]).Value.ShouldBe("before");
        copy.Filters[1].ShouldBeNull();
        var cycle = new FilterCriteria();
        cycle.Filters = [cycle];
        Should.Throw<ArgumentException>(() => AuditFilterSnapshot.Copy(new() { Filters = [cycle] }));
    }

    [Fact]
    public void Registration_ExplicitProviderChoicesConflictAndNoSourceIsImplicit()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        builder.WithStorageProvider<ReadSpy>();
        Should.Throw<InvalidOperationException>(() => builder.WithStorageProvider<InMemoryAuditStorageProvider>());
        services.Any(s => s.ServiceType == typeof(IAuditBoundaryRegistrationSource)).ShouldBeFalse();
    }

    [Fact]
    public async Task CurrentCapturePolicy_CannotBeBypassedByHistoricalFacetsOrAggregates()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
        services.AddAuditing(options => options.Configure(o => o with { CapturePolicy = new() { RedactNames = ["businessCode"] } }))
            .WithSearchableDetail(new() { Name = "businessCode" });
        using var host = services.BuildServiceProvider();
        var original = Record("root") with { Details = Details("businessCode", AuditValue.From("historical-sensitive-value")) };
        await Add(host.GetRequiredService<IAuditingStorageProvider>(), original);
        using var scope = host.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        (await queries.FindAsync(original.AuditId)).Value.Record.Details["businessCode"].Marker.ShouldBe("Redacted");
        (await queries.FindFacetsAsync(null, "Details.businessCode")).IsFailure.ShouldBeTrue();
        (await queries.AggregateAsync(null, "Details.businessCode")).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void CompositeWireGrammar_RoundTripsWithoutArbitraryTypeConversion()
    {
        var source = new FilterModel { Filters = [new() { CustomType = FilterCustomType.CompositeSpecification, CompositeSpecification = new()
        {
            Nodes = [new SpecificationGroup { Logic = FilterLogicOperator.Or, Nodes = [new SpecificationLeaf { Name = "audit.action-match", Arguments = [new FilterCriteria("Key", FilterOperator.Equal, "rename")] }] }]
        } }] };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(source, AuditFilterJson.CreateOptions());
        var parsed = AuditFilterJson.Parse(bytes);
        parsed.IsSuccess.ShouldBeTrue();
        Matches(parsed.Value, Record("root") with { Actions = [Action("rename", AuditOutcome.Succeeded)] }).ShouldBeTrue();
    }

    [Fact]
    public void FullText_IsRestrictedToApprovedLabelsTypesAndSafeErrors()
    {
        var record = Record("root") with { Actions = [Action("rename", AuditOutcome.Succeeded) with { HandlerType = "CustomerRenameHandler" }] };
        var filter = new FilterModel { Filters = [new(FilterCustomType.FullTextSearch, new() { ["searchTerm"] = "customer", ["fields"] = new[] { "Key", "Actions.HandlerType" } })] };
        Matches(filter, record).ShouldBeTrue();
        filter.Filters[0].CustomParameters["fields"] = new[] { "EntityChanges.Properties.NewValue" };
        new AuditSearchPlanBuilder(new(), new FakeTimeProvider(now)).Build(filter).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadProjection_NeverExposesUnknownChangeValuesEvenWithValueExposure()
    {
        using var host = Host(builder => builder.WithExposure(new() { ExposeChangeValues = true }));
        var record = Record("root") with { EntityChanges = [new() { Id = Guid.NewGuid(), PersistenceState = "Unknown", Properties = [new() { Path = "Name", NewValue = AuditValue.From("unconfirmed") }] }] };
        await Add(host.GetRequiredService<IAuditingStorageProvider>(), record);
        using var scope = host.CreateScope();
        var details = await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().FindAsync(record.AuditId);
        details.Value.Record.EntityChanges.Single().Properties.Single().NewValue.Marker.ShouldBe("Redacted");
        details.Value.IsTransformed.ShouldBeTrue();
    }

    [Fact]
    public async Task ProtectedOriginal_WithoutVerifierFailsBeforeReturningBusinessValues()
    {
        using var host = Host();
        var record = Record("root") with { IntegrityProtection = new() { KeyId = "external", Tag = Convert.ToBase64String(new byte[32]) } };
        await Add(host.GetRequiredService<IAuditingStorageProvider>(), record);
        using var scope = host.CreateScope();
        var details = await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().FindAsync(record.AuditId);
        details.IsFailure.ShouldBeTrue();
        details.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditIntegrityUnavailable");
    }

    [Fact]
    public void QueryConfiguration_ChangesRangeAndRejectsInvalidLimits()
    {
        var builder = new AuditSearchPlanBuilder(new(), new FakeTimeProvider(now), new() { DefaultRange = TimeSpan.FromDays(2), MaxRange = TimeSpan.FromDays(3) });
        builder.Build(null).Value.FromUtc.ShouldBe(now.AddDays(-2));
        builder.Build(new() { Filters = [new("StartedUtc", FilterOperator.GreaterThanOrEqual, now.AddDays(-4))] }).IsFailure.ShouldBeTrue();
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddAuditing().WithQueries(new() { Timeout = TimeSpan.Zero }));
    }

    [Fact]
    public void ExpandedNamedJsonCriteria_CannotBypassTheLogicalPredicateLimit()
    {
        var children = Enumerable.Range(0, 33).Select(i => JsonSerializer.SerializeToElement(new { field = "Key", @operator = "eq", value = "rename" })).Cast<object>().ToArray();
        var filter = new FilterModel { Filters = [new("audit.action-match", children)] };
        new AuditSearchPlanBuilder(new(), new FakeTimeProvider(now)).Build(filter).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryDeadline_CoversAnUncooperativeHostResolverBeforeProviderReads()
    {
        var pending = new TaskCompletionSource<Result<AuditReadScope>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var spy = new ReadSpy();
        using var host = Host(builder => builder.WithStorageProvider(_ => spy).WithQueries(new() { Timeout = TimeSpan.FromMilliseconds(30) }).WithReadScope((sp, ct) => pending.Task));
        using var scope = host.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().SearchAsync();
        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditQueryTimeout");
        spy.Reads.ShouldBe(0);
        pending.SetResult(Result<AuditReadScope>.Failure());
    }

    [Theory]
    [InlineData("oversized", true)]
    [InlineData("label", true)]
    [InlineData("incomplete-and-oversized", true)]
    [InlineData("incomplete-only", false)]
    public async Task Search_TruncationWithoutOmissionCounts_FindsActualCapture(string scenario, bool expected)
    {
        using var host = Host();
        using var scope = host.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var recording = audit.BeginScope(builder => builder.Key("capture").DisplayName(scenario == "label" ? new string('x', 1025) : "Capture"));
        if (scenario.Contains("oversized")) recording.SetDetail("large", AuditValue.From(new string('x', 5000)));
        if (!scenario.StartsWith("incomplete")) recording.Complete();
        await recording.DisposeAsync();
        ((FakeTimeProvider)host.GetRequiredService<TimeProvider>()).Advance(TimeSpan.FromSeconds(1));

        var stored = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(recording.AuditId)).Value;
        stored.OmittedActionCount.ShouldBe(0);
        stored.OmittedDetailCount.ShouldBe(0);
        var result = await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().SearchAsync(new()
        {
            Filters = [new("HasTruncation", FilterOperator.Equal, true)]
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(expected ? 1 : 0);
    }

    private static ServiceProvider Host(Action<AuditingBuilder> configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
        configure?.Invoke(services.AddAuditing());
        if (configure is null) services.AddAuditing();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static AuditRecord Record(string key)
    {
        var id = Guid.NewGuid();
        return new() { AuditId = id, OriginAuditId = id, Key = key, StartedUtc = now.AddHours(-1), CompletedUtc = now.AddMinutes(-30) };
    }

    private static AuditAction Action(string key, AuditOutcome outcome) => new() { Id = Guid.NewGuid(), Key = key, Outcome = outcome, StartedUtc = now.AddMinutes(-55), CompletedUtc = now.AddMinutes(-50) };
    private static ImmutableDictionary<string, AuditValue> Details(string name, AuditValue value) => ImmutableDictionary.Create<string, AuditValue>(StringComparer.OrdinalIgnoreCase).Add(name, value);
    private static bool Matches(FilterModel filter, AuditRecord record, AuditFieldCatalog catalog = null)
    {
        catalog ??= new();
        var plan = new AuditSearchPlanBuilder(catalog, new FakeTimeProvider(now)).Build(filter);
        plan.IsSuccess.ShouldBeTrue();
        return new AuditQueryEvaluator(catalog).Matches(plan.Value.Predicate, record);
    }
    private static async Task Add(IAuditingStorageProvider provider, params AuditRecord[] records)
    {
        foreach (var record in records) (await provider.AppendAsync(record)).IsSuccess.ShouldBeTrue();
    }

    public class ReadSpy : IAuditingStorageProvider
    {
        public int Reads { get; private set; }
        public Task<Result<AuditAppendReceipt>> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditAppendReceipt>.Failure());
        public Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditFlushResult>.Failure());
        public Task<Result<AuditRecord>> FindAsync(Guid id, CancellationToken cancellationToken = default) { this.Reads++; return Task.FromResult(Result<AuditRecord>.Failure()); }
        public Task<Result<AuditExtractionItem>> FindStoredAsync(Guid id, AuditPredicate scope, CancellationToken cancellationToken = default) { this.Reads++; return Task.FromResult(Result<AuditExtractionItem>.Failure()); }
        public Task<ResultPaged<AuditRecordSummary>> SearchAsync(AuditSearchPlan plan, CancellationToken cancellationToken = default) { this.Reads++; return Task.FromResult(ResultPaged<AuditRecordSummary>.Failure()); }
        public Task<Result<ImmutableArray<AuditAggregateItem>>> AggregateAsync(AuditAggregateQuery query, CancellationToken cancellationToken = default) { this.Reads++; return Task.FromResult(Result<ImmutableArray<AuditAggregateItem>>.Failure()); }
        public Task<Result<ImmutableArray<string>>> FindFacetsAsync(AuditFacetQuery query, CancellationToken cancellationToken = default) { this.Reads++; return Task.FromResult(Result<ImmutableArray<string>>.Failure()); }
        public Task<ResultPaged<AuditResourceHistoryItem>> GetResourceHistoryAsync(AuditSearchPlan query, AuditResourceReference resource, CancellationToken cancellationToken = default) { this.Reads++; return Task.FromResult(ResultPaged<AuditResourceHistoryItem>.Failure()); }
    }

    public sealed class CallbackEnumeration : Enumeration
    {
        public static int Calls;
        public CallbackEnumeration() : base(1, "probe") { }
        public static CallbackEnumeration FromId(int id) { Calls++; return new(); }
    }
}
