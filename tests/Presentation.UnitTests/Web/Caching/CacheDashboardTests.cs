// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web.Caching;
using BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CachePageProvider = BridgingIT.DevKit.Presentation.Web.Caching.Dashboard.DashboardPageProvider;

/// <summary>Checks the public cache presentation behavior.</summary>
public class CacheDashboardTests
{
    /// <summary>A feature opt-in resolves safely when the host omits or disables its Dashboard, without publishing pages or granting scope.</summary>
    /// <param name="configureDisabledHost">Whether to register an explicitly disabled host instead of omitting it.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DashboardOptIn_WithoutEnabledHost_StartsWithoutRoutesOrAccess(bool configureDisabledHost)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Host.UseDefaultServiceProvider(options => { options.ValidateOnBuild = true; options.ValidateScopes = true; });
        builder.Services.AddAuthorization();
        if (configureDisabledHost) { builder.Services.AddDashboard(options => options.Enabled(false)); }

        builder.Services.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("items")
            .WithDashboard(options => options.WithSensitiveMetadata());
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        using var sut = app.GetTestClient();

        using var response = await sut.GetAsync("/_bdk/dashboard/caching");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "operator")], "test"))
        };
        scope.ServiceProvider.GetRequiredService<CacheCatalog>().Integrations.ContainsKey("Dashboard").ShouldBeTrue();
        var pages = scope.ServiceProvider.GetServices<IDashboardPageProvider>().OfType<CachePageProvider>().Single();
        pages.GetPages(context).ShouldBeEmpty();
        var access = scope.ServiceProvider.GetRequiredService<ICacheDashboardAccessContextFactory>().Create(context, CacheAccessActions.Read);
        access.SubjectId.ShouldBeEmpty();
        access.Actions.ShouldBe(CacheAccessActions.None);
        access.RevealSensitiveMetadata.ShouldBeFalse();
    }

    /// <summary>Verifies dashboard opt in registers only its own routes and remembered views.</summary>
    [Fact]
    public async Task DashboardOptIn_RegistersOnlyItsOwnRoutesAndRememberedViews()
    {
        await using var fixture = await Fixture.CreateAsync();

        foreach (var tab in CacheDashboardRoutes.GlobalTabs)
        {
            var response = await fixture.Client.GetAsync(CacheDashboardRoutes.Global(fixture.Options, tab.Key));
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).ShouldContain("data-dashboard-view");
        }

        foreach (var tab in CacheDashboardRoutes.CollectionTabs)
        {
            using var response = await fixture.Client.GetAsync(CacheDashboardRoutes.Collection(fixture.Options, "items", tab.Key));
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            if (tab.Key == "operations")
            {
                var html = await response.Content.ReadAsStringAsync();
                html.ShouldNotContain("Automatic admission not paused");
                html.ShouldNotContain("Refresh configured:");
                html.ShouldNotContain("Warm-up configured:");
            }
        }

        (await fixture.Client.GetAsync("/api/caches/collections")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var scope = fixture.App.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var provider = scope.ServiceProvider.GetServices<IDashboardPageProvider>().OfType<CachePageProvider>().ShouldHaveSingleItem();
        var page = provider.GetPages(context).Single(item => item.Key == "application.caching");
        page.Url.ShouldBe("/ops/caching");
        page.RememberedViews["collection:items:entries"].Equals("/ops/caching/collections/items/entries", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        page.RememberedViews["diagnostics"].ShouldBe("/ops/caching/diagnostics");
    }

    /// <summary>Verifies referencing core and dashboard host without feature opt in maps no cache pages.</summary>
    [Fact]
    public async Task ReferencingCoreAndDashboardHost_WithoutFeatureOptInMapsNoCachePages()
    {
        await using var fixture = await Fixture.CreateAsync(enabled: false);

        (await fixture.Client.GetAsync("/ops/caching")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Verifies collection named like atab keeps its overview and configured base path.</summary>
    [Fact]
    public async Task CollectionNamedLikeATab_KeepsItsOverviewAndConfiguredBasePath()
    {
        await using var fixture = await Fixture.CreateAsync();

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/entries");

        html.ShouldContain("data-cache-view=\"overview\"");
        html.ShouldContain("/ops/caching/collections/entries/settings");
        html.ShouldContain("aria-label=\"Cache collection views\"");
    }

    /// <summary>Verifies settings redact warmup keys and extension values and remain read only.</summary>
    [Fact]
    public async Task Settings_RedactWarmupKeysAndExtensionValuesAndRemainReadOnly()
    {
        await using var fixture = await Fixture.CreateAsync(collection => collection
            .With(x => x.Warmup.Items, new CacheItem[] { new("private-seed-key") })
            .WithExtension("Dashboard", "ApiToken", "private-extension-secret"));

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/settings");

        html.ShouldContain("Read only");
        html.ShouldContain("[redacted]");
        html.ShouldNotContain("private-seed-key");
        html.ShouldNotContain("private-extension-secret");
        html.ShouldContain("Not exposed");
        html.ShouldContain("Collection behaviors");
    }

    /// <summary>Codec and compression status are directly readable in dedicated settings cards.</summary>
    [Fact]
    public async Task Settings_ShowsCodecAndDisabledCompressionOutsideTheConfigurationDisclosure()
    {
        await using var fixture = await Fixture.CreateAsync();

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/settings");

        ElementContents(html, "section", "data-cache-codec-settings").ToLowerInvariant().ShouldContain("json");
        ElementContents(html, "section", "data-cache-compression-settings").ShouldContain("Off");
    }

    /// <summary>Enabled compression shows its actual configured algorithm, level, threshold, and saving requirement.</summary>
    [Fact]
    public async Task Settings_ShowsConfiguredCompressionDetails()
    {
        await using var fixture = await Fixture.CreateAsync(collection => collection
            .With(value => value.Compression.Enabled, true)
            .With(value => value.Compression.Algorithm, CacheCompressionAlgorithm.Brotli)
            .With(value => value.Compression.Level, System.IO.Compression.CompressionLevel.Optimal)
            .With(value => value.Compression.ThresholdBytes, 1024L)
            .With(value => value.Compression.MinimumSavingRatio, 0.1));

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/settings");

        var compression = ElementContents(html, "section", "data-cache-compression-settings");
        compression.ShouldContain("Brotli");
        compression.ShouldContain("Optimal");
        compression.ShouldContain("1 KiB");
        compression.ShouldContain("10%");
    }

    /// <summary>Stored scalar, set, interval, and null dimensions render as metadata with markup safely encoded.</summary>
    [Fact]
    public async Task Entries_ShowsStoredDimensionsWithoutRenderingTheirMarkup()
    {
        var region = CacheDimension.Scalar("region", "<b>west</b>&");
        await using var fixture = await Fixture.CreateAsync(collection => collection.With(value => value.Dimensions,
            new CacheDimensionSchema[]
            {
                new("region", CacheDimensionKind.String), new("labels", CacheDimensionKind.String, CacheDimensionForm.Set),
                new("stock", CacheDimensionKind.Integer, CacheDimensionForm.Interval), new("optional", CacheDimensionKind.String, AllowNull: true)
            }), sensitiveMetadata: true);
        var item = new CacheItem("item", [region, CacheDimension.Set("labels", CacheDimensionKind.String, "east", "west"),
            CacheDimension.Interval("stock", 10L, 20L), CacheDimension.Scalar("optional", null, CacheDimensionKind.String)]);
        (await fixture.Collection.SetAsync(item, "value")).IsSuccess.ShouldBeTrue();

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries");

        var dimensions = ElementContents(html, "td", "data-cache-entry-dimensions");
        dimensions.ShouldContain("REGION");
        dimensions.ShouldContain("LABELS");
        dimensions.ShouldContain("STOCK");
        dimensions.ShouldContain("OPTIONAL");
        dimensions.ShouldContain(WebUtility.HtmlEncode(region.Values[0].CanonicalValue));
        dimensions.ShouldNotContain(region.Values[0].CanonicalValue);
        dimensions.ShouldContain("[10, 20)");
        dimensions.ShouldContain("null");
        WebUtility.HtmlDecode(dimensions).ShouldContain("{EAST, WEST}");
    }

    /// <summary>Hidden dimensions remain distinguishable from an entry that has no dimensions.</summary>
    /// <param name="revealMetadata">Whether the host permits dimension disclosure.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Entries_DimensionDisclosure_RespectsTheCurrentAccessContext(bool revealMetadata)
    {
        await using var fixture = await Fixture.CreateAsync(collection => collection.With(value => value.Dimensions,
            new[] { new CacheDimensionSchema("region", CacheDimensionKind.String) }), sensitiveMetadata: revealMetadata);
        (await fixture.Collection.SetAsync(new CacheItem("item").WithScalarDimension("region", "private-region"), "value")).IsSuccess.ShouldBeTrue();

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries");

        var dimensions = ElementContents(html, "td", "data-cache-entry-dimensions");
        if (revealMetadata) { dimensions.ShouldContain("PRIVATE-REGION"); }
        else
        {
            dimensions.ShouldContain("Hidden");
            html.ShouldNotContain("PRIVATE-REGION");
        }
    }

    /// <summary>An untagged entry shows an explicit empty-dimension state when metadata disclosure is allowed.</summary>
    [Fact]
    public async Task Entries_WithoutDimensions_ShowsNone()
    {
        await using var fixture = await Fixture.CreateAsync(sensitiveMetadata: true);
        (await fixture.Collection.SetAsync(new("item"), "value")).IsSuccess.ShouldBeTrue();

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries");

        ElementContents(html, "td", "data-cache-entry-dimensions").ShouldContain("None");
    }

    /// <summary>Dimension equality and a literal key prefix combine rather than broadening each other's result set.</summary>
    [Fact]
    public async Task Entries_DimensionEqualityAndKeyPrefix_ReturnOnlyTheirIntersection()
    {
        await using var fixture = await Fixture.CreateAsync(collection => collection.With(value => value.Dimensions,
            new[] { new CacheDimensionSchema("region", CacheDimensionKind.String) }), sensitiveMetadata: true);
        (await fixture.Collection.SetAsync(new CacheItem("item:west").WithScalarDimension("region", "west"), "value")).IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new CacheItem("item:east").WithScalarDimension("region", "east"), "value")).IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new CacheItem("other:west").WithScalarDimension("region", "west"), "value")).IsSuccess.ShouldBeTrue();

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries?prefix=item:&dimension=region&dimensionOperator=equal&dimensionValue=west");

        Regex.Matches(html, "data-dashboard-row-id=").Count.ShouldBe(1);
        html.ShouldContain("item:west");
        html.ShouldNotContain("item:east");
        html.ShouldNotContain("other:west");
    }

    /// <summary>Typed set-membership and half-open interval predicates execute through the Entries route.</summary>
    /// <param name="dimension">The declared set or interval dimension.</param>
    /// <param name="predicate">The compatible selection operator.</param>
    /// <param name="value">The member or inclusive lower bound.</param>
    /// <param name="upper">The exclusive upper bound, or null for membership.</param>
    /// <param name="expectedKey">The sole matching entry.</param>
    [Theory]
    [InlineData("labels", "contains", "west", null, "member-west")]
    [InlineData("stock", "overlap", "10", "20", "range-overlap")]
    public async Task Entries_TypedDimensionPredicate_FiltersStoredMetadata(string dimension, string predicate, string value,
        string upper, string expectedKey)
    {
        await using var fixture = await Fixture.CreateAsync(collection => collection.With(option => option.Dimensions,
            new[] { new CacheDimensionSchema("labels", CacheDimensionKind.String, CacheDimensionForm.Set),
                new CacheDimensionSchema("stock", CacheDimensionKind.Integer, CacheDimensionForm.Interval) }), sensitiveMetadata: true);
        (await fixture.Collection.SetAsync(new CacheItem("member-west").WithDimension(CacheDimension.Set("labels", CacheDimensionKind.String, "west")), "value")).IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new CacheItem("member-east").WithDimension(CacheDimension.Set("labels", CacheDimensionKind.String, "east")), "value")).IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new CacheItem("range-overlap").WithDimension(CacheDimension.Interval("stock", 15L, 25L)), "value")).IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new CacheItem("range-touching").WithDimension(CacheDimension.Interval("stock", 20L, 30L)), "value")).IsSuccess.ShouldBeTrue();
        var path = "/ops/caching/collections/items/entries?dimension=" + dimension + "&dimensionOperator=" + predicate +
            "&dimensionValue=" + value + (upper is null ? "" : "&dimensionUpper=" + upper);

        var html = await fixture.Client.GetStringAsync(path);

        Regex.Matches(html, "data-dashboard-row-id=").Count.ShouldBe(1);
        html.ShouldContain(expectedKey);
    }

    /// <summary>Opaque continuation remains bound to the dimension predicate and preserves it through later loads.</summary>
    [Fact]
    public async Task Entries_DimensionSelection_PreservesPagingAndRejectsAChangedPredicate()
    {
        await using var fixture = await Fixture.CreateAsync(collection => collection.With(value => value.Dimensions,
            new[] { new CacheDimensionSchema("region", CacheDimensionKind.String) }), sensitiveMetadata: true);
        foreach (var key in new[] { "west:one", "west:two" })
        {
            (await fixture.Collection.SetAsync(new CacheItem(key).WithScalarDimension("region", "west"), "value")).IsSuccess.ShouldBeTrue();
        }

        (await fixture.Collection.SetAsync(new CacheItem("east:one").WithScalarDimension("region", "east"), "value")).IsSuccess.ShouldBeTrue();
        var query = "pageSize=1&dimension=region&dimensionOperator=equal&dimensionValue=west";
        var first = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries?" + query);
        var cursor = WebUtility.HtmlDecode(Regex.Match(first, "data-next-cursor=\"([^\"]+)\"").Groups[1].Value);
        var boundary = Regex.Match(first, "data-boundary=\"([0-9]+)\"").Groups[1].Value;
        cursor.ShouldNotBeNullOrEmpty();
        boundary.ShouldNotBeNullOrEmpty();
        var continuation = "&continuation=" + Uri.EscapeDataString(cursor) + "&listBoundary=" + boundary;

        var next = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries/content?" + query + continuation);
        var changed = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries/content?" +
            query.Replace("dimensionValue=west", "dimensionValue=east", StringComparison.Ordinal) + continuation);

        Regex.Matches(first, "data-dashboard-row-id=").Count.ShouldBe(1);
        Regex.Matches(next, "data-dashboard-row-id=").Count.ShouldBe(1);
        next.ShouldNotContain("east:one");
        next.ShouldNotContain("data-dashboard-query-error");
        changed.ShouldContain("data-dashboard-query-error");
        Regex.Matches(changed, "data-dashboard-row-id=").Count.ShouldBe(0);
    }

    /// <summary>A malformed typed dimension query shows its error rather than returning unfiltered entries.</summary>
    [Fact]
    public async Task Entries_InvalidDimensionValue_DoesNotFallBackToAnUnfilteredList()
    {
        await using var fixture = await Fixture.CreateAsync(collection => collection.With(value => value.Dimensions,
            new[] { new CacheDimensionSchema("stock", CacheDimensionKind.Integer) }), sensitiveMetadata: true);
        (await fixture.Collection.SetAsync(new CacheItem("retained").WithScalarDimension("stock", 42L), "value")).IsSuccess.ShouldBeTrue();

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries?dimension=stock&dimensionOperator=equal&dimensionValue=not-an-integer");

        html.ShouldContain("data-dashboard-query-error");
        Regex.Matches(html, "data-dashboard-row-id=").Count.ShouldBe(0);
        (await fixture.Collection.GetAsync("retained")).Value.Found.ShouldBeTrue();
    }

    /// <summary>Verifies entries preserve their fixed cutoff and opaque continuation across pages.</summary>
    [Fact]
    public async Task Entries_PreserveTheirFixedCutoffAndOpaqueContinuationAcrossPages()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Collection.SetAsync(new("item:a"), "first");
        await fixture.Collection.SetAsync(new("item:b"), "second");
        var first = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries?pageSize=1");
        var cursor = WebUtility.HtmlDecode(Regex.Match(first, "data-next-cursor=\"([^\"]+)\"").Groups[1].Value);
        var boundary = Regex.Match(first, "data-boundary=\"([0-9]+)\"").Groups[1].Value;
        cursor.ShouldNotBeNullOrEmpty();
        boundary.ShouldNotBeNullOrEmpty();

        var next = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries/content?pageSize=1&continuation=" +
            Uri.EscapeDataString(cursor) + "&listBoundary=" + boundary);

        Regex.Matches(first, "data-dashboard-row-id=").Count.ShouldBe(1);
        Regex.Matches(next, "data-dashboard-row-id=").Count.ShouldBe(1);
        next.ShouldNotContain("data-dashboard-query-error");
        next.ShouldContain("data-boundary=\"" + boundary + "\"");
        first.ShouldContain("Load more");
        first.ShouldContain("Rows per load");
    }

    /// <summary>Verifies entry metadata default redaction and long names stay within responsive containers.</summary>
    [Fact]
    public async Task EntryMetadata_DefaultRedactionAndLongNamesStayWithinResponsiveContainers()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Collection.SetAsync(new(new string('x', 1000)), "value");

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries");

        html.ShouldContain("[redacted]");
        html.ShouldNotContain(new string('x', 1000));
        html.ShouldContain("table-responsive");
        html.ShouldContain("text-break");
        html.ShouldContain("aria-label=\"Rows per load\"");
    }

    /// <summary>Authorized metadata display retains the actual key while encoding markup in entry labels and tooltips.</summary>
    [Fact]
    public async Task EntryMetadata_ExplicitAccess_RevealsKeyWithoutRenderingMarkup()
    {
        await using var fixture = await Fixture.CreateAsync(admin: true, sensitiveMetadata: true);
        var key = "invoice<42>&B";
        (await fixture.Collection.SetAsync(new(key), "value")).IsSuccess.ShouldBeTrue();

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries");

        html.ShouldContain(WebUtility.HtmlEncode(key));
        html.ShouldNotContain(key);
        html.ShouldNotContain("[redacted]");
    }

    /// <summary>Long opaque and slash-delimited keys preserve their exact ending and keep the complete hover title.</summary>
    /// <param name="pathLike">Whether the key contains parent segments that could otherwise be abbreviated.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EntryMetadata_LongKeys_AreLeftTruncatedWithTheirFullTitle(bool pathLike)
    {
        await using var fixture = await Fixture.CreateAsync(admin: true, sensitiveMetadata: true);
        var key = pathLike ? string.Join("/", Enumerable.Repeat("parent-segment", 12)) + "/final-identifier" :
            new string('a', 140) + ":final-identifier";
        (await fixture.Collection.SetAsync(new(key), "value")).IsSuccess.ShouldBeTrue();

        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries");

        var rendered = Regex.Match(html, "<tr data-dashboard-row-id=\"[^\"]+\">\\s*<td[^>]*><code>(?<key>.*?)</code>", RegexOptions.Singleline);
        rendered.Success.ShouldBeTrue();
        WebUtility.HtmlDecode(rendered.Groups["key"].Value).ShouldBe("..." + key[^77..]);
        html.ShouldContain("title=\"" + WebUtility.HtmlEncode(key) + "\"");
    }

    /// <summary>Diagnostics starts with focused overview charts and keeps the full families in a closed disclosure.</summary>
    /// <param name="path">The global or collection diagnostics route.</param>
    [Theory]
    [InlineData("/ops/caching/diagnostics")]
    [InlineData("/ops/caching/collections/items/diagnostics")]
    public async Task Diagnostics_RendersOverviewAndInitiallyCollapsedAdvancedCharts(string path)
    {
        await using var fixture = await Fixture.CreateAsync();

        var html = await fixture.Client.GetStringAsync(path);

        Regex.Matches(html, "data-cache-chart-level=\"overview\"").Count.ShouldBe(4);
        var advanced = Regex.Match(html, "<details(?<attributes>[^>]*data-cache-advanced[^>]*)>(?<content>.*?)</details>", RegexOptions.Singleline);
        advanced.Success.ShouldBeTrue();
        Regex.IsMatch(advanced.Groups["attributes"].Value, @"\bopen(?:\s|=|$)").ShouldBeFalse();
        Regex.Matches(advanced.Groups["content"].Value, "data-cache-chart-level=\"advanced\"").Count.ShouldBe(6);
        foreach (var family in CacheDiagnosticsCharts.OverviewFamilies)
        {
            var detailed = CacheDiagnosticsCharts.Families.Single(value => value.Key == family.Key);
            family.Metrics.ShouldAllBe(metric => detailed.Metrics.Contains(metric));
            html.ShouldContain("data-cache-chart-metrics=\"" + string.Join(",", family.Metrics) + "\"");
        }
    }

    /// <summary>Memory explanations identify node-wide accounting without becoming inline card prose.</summary>
    /// <param name="path">A global or collection page, or its refreshed fragment.</param>
    [Theory]
    [InlineData("/ops/caching")]
    [InlineData("/ops/caching/collections/items")]
    [InlineData("/ops/caching/diagnostics/content")]
    [InlineData("/ops/caching/collections/items/diagnostics/content")]
    public async Task MemoryCards_ExplainAccountingThroughAccessibleHelpOnly(string path)
    {
        await using var fixture = await Fixture.CreateAsync();

        var html = await fixture.Client.GetStringAsync(path);

        var card = ElementContents(html, "section", "data-cache-footprint=\"resident\"");
        var buttons = HelpButtons(card);
        buttons.Count.ShouldBe(1);
        var description = WebUtility.HtmlDecode(Regex.Match(buttons[0].Groups["attributes"].Value, "title=\"([^\"]+)\"").Groups[1].Value);
        description.ShouldContain("across all collections on this app node");
        description.ShouldContain("size estimates");
        description.ShouldContain("not total process RAM");
        card.Replace(buttons[0].Value, string.Empty).ShouldNotContain("Memory currently reserved");
        var hot = ElementContents(html, "section", "data-cache-footprint=\"hot\"");
        hot.ShouldContain("Already included in Owned resident RAM");
        hot.ShouldNotContain("Hot memory (included)");
        AssertAccessibleHelp(html);
    }

    /// <summary>Collection inspection uses focusable non-submit help controls in both full pages and refreshed fragments.</summary>
    /// <param name="path">The inspection route.</param>
    [Theory]
    [InlineData("/ops/caching/collections")]
    [InlineData("/ops/caching/collections/items/entries")]
    [InlineData("/ops/caching/collections/items/entries/content")]
    [InlineData("/ops/caching/shared-loads")]
    [InlineData("/ops/caching/collections/items/settings")]
    public async Task InspectionViews_ExposeExplainersAsAccessibleTooltipControls(string path)
    {
        await using var fixture = await Fixture.CreateAsync();

        var html = await fixture.Client.GetStringAsync(path);

        AssertAccessibleHelp(html);
    }

    /// <summary>Verifies history returns six bounded families and explicit disabled availability.</summary>
    [Fact]
    public async Task History_ReturnsSixBoundedFamiliesAndExplicitDisabledAvailability()
    {
        await using var fixture = await Fixture.CreateAsync();

        var response = await fixture.Client.GetAsync("/ops/caching/history");
        var json = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var charts = document.RootElement.GetProperty("charts");
        charts.GetArrayLength().ShouldBe(6);
        document.RootElement.GetProperty("scope").GetString().ShouldBe("NodeLocal");
        document.RootElement.GetProperty("availableFrom").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        charts.EnumerateArray().SelectMany(chart => chart.GetProperty("series").EnumerateArray())
            .ShouldAllBe(series => series.GetProperty("points").GetArrayLength() == 0);
        foreach (var chart in charts.EnumerateArray())
        {
            chart.GetProperty("family").TryGetProperty("description", out _).ShouldBeFalse();
        }

        response.Headers.CacheControl.NoStore.ShouldBeTrue();
    }

    /// <summary>Verifies anonymous dashboard read does not grant mutation authority.</summary>
    [Fact]
    public async Task AnonymousDashboardRead_DoesNotGrantMutationAuthority()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Collection.SetAsync(new("item"), "retained");

        var response = await fixture.Client.PostAsync("/ops/caching/actions/remove",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["collections"] = "items", ["key"] = "item" }));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await fixture.Collection.GetAsync("item")).Value.Value.ShouldBe("retained");
    }

    /// <summary>Verifies authorized mutation requires antiforgery and then uses the common operation.</summary>
    [Fact]
    public async Task AuthorizedMutation_RequiresAntiforgeryAndThenUsesTheCommonOperation()
    {
        await using var fixture = await Fixture.CreateAsync(admin: true);
        await fixture.Collection.SetAsync(new("item"), "retained");
        var denied = await fixture.Client.PostAsync("/ops/caching/actions/remove",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["collections"] = "items", ["key"] = "item" }));
        denied.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var form = await fixture.FormAsync();
        form["collections"] = "items";
        form["key"] = "item";

        var response = await fixture.Client.PostAsync("/ops/caching/actions/remove", new FormUrlEncodedContent(form));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fixture.Collection.GetAsync("item")).Value.Found.ShouldBeFalse();
    }

    /// <summary>Verifies background clear reports accepted state and direct tracked status link.</summary>
    [Fact]
    public async Task BackgroundClear_ReportsAcceptedStateAndDirectTrackedStatusLink()
    {
        await using var fixture = await Fixture.CreateAsync(admin: true);
        await fixture.Collection.SetAsync(new("item"), "retained");
        var form = await fixture.FormAsync();
        form["collections"] = "items";
        form["background"] = "true";

        var response = await fixture.Client.PostAsync("/ops/caching/actions/clear", new FormUrlEncodedContent(form));
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = document.RootElement.GetProperty("items")[0];
        item.GetProperty("accepted").GetBoolean().ShouldBeTrue();
        item.GetProperty("completed").GetBoolean().ShouldBeFalse();
        item.GetProperty("statusUrl").GetString().StartsWith("/ops/caching/collections/items/operations?operationId=", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    /// <summary>A blank or omitted prefix previews and invalidates every current entry in the selected collection only.</summary>
    /// <param name="selectionKind">The prefix selector, or an omitted selector.</param>
    /// <param name="selection">An empty prefix, or an omitted value.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("prefix", null)]
    [InlineData("prefix", "")]
    public async Task Operations_BlankPrefix_SelectsAllEntriesWithinTheExplicitCollection(string selectionKind, string selection)
    {
        await using var fixture = await Fixture.CreateAsync(admin: true);
        (await fixture.Collection.SetAsync(new("first"), "value")).IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new("second"), "value")).IsSuccess.ShouldBeTrue();
        var other = fixture.App.Services.GetRequiredService<IApplicationCache>().GetCollection<string>("entries");
        (await other.SetAsync(new("outside"), "value")).IsSuccess.ShouldBeTrue();
        var form = await fixture.FormAsync();
        form["collections"] = "items";
        if (selectionKind is not null) { form["selectionKind"] = selectionKind; }

        if (selection is not null) { form["selection"] = selection; }

        using var preview = await fixture.Client.PostAsync("/ops/caching/actions/preview", new FormUrlEncodedContent(form));
        using var previewBody = System.Text.Json.JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        using var invalidated = await fixture.Client.PostAsync("/ops/caching/actions/invalidate", new FormUrlEncodedContent(form));
        using var invalidationBody = System.Text.Json.JsonDocument.Parse(await invalidated.Content.ReadAsStringAsync());

        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        previewBody.RootElement.GetProperty("items")[0].GetProperty("count").GetInt64().ShouldBe(2);
        invalidated.StatusCode.ShouldBe(HttpStatusCode.OK);
        invalidationBody.RootElement.GetProperty("items")[0].GetProperty("count").GetInt64().ShouldBe(2);
        (await fixture.Collection.GetAsync("first")).Value.Found.ShouldBeFalse();
        (await fixture.Collection.GetAsync("second")).Value.Found.ShouldBeFalse();
        (await other.GetAsync("outside")).Value.Value.ShouldBe("value");
    }

    /// <summary>Nonempty prefixes retain literal wildcard characters and leave all nonmatching entries in place.</summary>
    [Fact]
    public async Task Operations_NonemptyPrefix_RemainsLiteralAndBounded()
    {
        await using var fixture = await Fixture.CreateAsync(admin: true);
        (await fixture.Collection.SetAsync(new("literal:*match"), "value")).IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new("literal:other"), "value")).IsSuccess.ShouldBeTrue();
        var form = await fixture.FormAsync();
        form["collections"] = "items";
        form["selectionKind"] = "prefix";
        form["selection"] = "literal:*";

        using var preview = await fixture.Client.PostAsync("/ops/caching/actions/preview", new FormUrlEncodedContent(form));
        using var previewBody = System.Text.Json.JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        using var invalidated = await fixture.Client.PostAsync("/ops/caching/actions/invalidate", new FormUrlEncodedContent(form));

        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        previewBody.RootElement.GetProperty("items")[0].GetProperty("count").GetInt64().ShouldBe(1);
        invalidated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fixture.Collection.GetAsync("literal:*match")).Value.Found.ShouldBeFalse();
        (await fixture.Collection.GetAsync("literal:other")).Value.Found.ShouldBeTrue();
    }

    /// <summary>An empty exact-key input remains invalid for preview and invalidation without affecting retained entries.</summary>
    /// <param name="action">The current preview or invalidation action.</param>
    [Theory]
    [InlineData("preview")]
    [InlineData("invalidate")]
    public async Task Operations_EmptyExactKey_IsRejectedWithoutBroadeningTheSelection(string action)
    {
        await using var fixture = await Fixture.CreateAsync(admin: true);
        (await fixture.Collection.SetAsync(new("retained"), "value")).IsSuccess.ShouldBeTrue();
        var form = await fixture.FormAsync();
        form["collections"] = "items";
        form["selectionKind"] = "key";
        form["selection"] = "";

        using var response = await fixture.Client.PostAsync("/ops/caching/actions/" + action, new FormUrlEncodedContent(form));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await fixture.Collection.GetAsync("retained")).Value.Found.ShouldBeTrue();
    }

    /// <summary>Explicit clear is independent of the currently empty exact-key selector.</summary>
    [Fact]
    public async Task Operations_ExplicitClear_DoesNotRequireASelectionValue()
    {
        await using var fixture = await Fixture.CreateAsync(admin: true);
        (await fixture.Collection.SetAsync(new("retained"), "value")).IsSuccess.ShouldBeTrue();
        var form = await fixture.FormAsync();
        form["collections"] = "items";
        form["selectionKind"] = "key";
        form["selection"] = "";

        using var response = await fixture.Client.PostAsync("/ops/caching/actions/clear", new FormUrlEncodedContent(form));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fixture.Collection.GetAsync("retained")).Value.Found.ShouldBeFalse();
    }

    /// <summary>Shows each collection's own retained count, including an empty collection sharing its provider.</summary>
    [Fact]
    public async Task Collections_SharedProvider_ShowsIndividualEntryCounts()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Collection.SetAsync(new("first"), "value")).IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new("second"), "value")).IsSuccess.ShouldBeTrue();
        var other = fixture.App.Services.GetRequiredService<IApplicationCache>().GetCollection<string>("entries");

        // Act
        var empty = await fixture.Client.GetStringAsync("/ops/caching/collections");
        (await other.SetAsync(new("other"), "value")).IsSuccess.ShouldBeTrue();
        var populated = await fixture.Client.GetStringAsync("/ops/caching/collections/content");

        // Assert
        CollectionCount(empty, "items").ShouldBe("2");
        CollectionCount(empty, "entries").ShouldBe("0");
        CollectionCount(populated, "items").ShouldBe("2");
        CollectionCount(populated, "entries").ShouldBe("1");
        (await fixture.Collection.GetAsync("first")).Value.Value.ShouldBe("value");
        (await other.GetAsync("other")).Value.Value.ShouldBe("value");
    }

    /// <summary>Marks a bounded incomplete count as observed rather than claiming the collection contains only that many entries.</summary>
    [Fact]
    public async Task Collections_CandidateBudgetReached_MarksCountAsObserved()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(collection => collection.With(value => value.Limits.MaxQueryCandidates, 1));
        (await fixture.Collection.SetAsync(new("first"), "value")).IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new("second"), "value")).IsSuccess.ShouldBeTrue();

        // Act
        var html = await fixture.Client.GetStringAsync("/ops/caching/collections");

        // Assert
        CollectionCount(html, "items").ShouldBe("1 (observed)");
        CollectionCount(html, "entries").ShouldBe("0");
    }

    /// <summary>A disabled collection does not present an uninspected store as an exact zero.</summary>
    [Fact]
    public async Task Collections_DisabledCollection_ShowsUnavailableCount()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(collection => collection.With(value => value.Enabled, false));

        // Act
        var html = await fixture.Client.GetStringAsync("/ops/caching/collections");

        // Assert
        CollectionCount(html, "items").ShouldBe("Unavailable");
        CollectionCount(html, "entries").ShouldBe("0");
    }

    /// <summary>Forms from initial and refreshed entry rows preserve exact keys, enforce antiforgery and remove only the selected entry.</summary>
    /// <param name="suffix">The full Entries page or its refresh fragment.</param>
    [Theory]
    [InlineData("")]
    [InlineData("/content")]
    public async Task Entries_InvalidateRowForm_RemovesOnlySelectedEntry(string suffix)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(admin: true, sensitiveMetadata: true);
        const string Key = "product:<script>&\"lookup";
        var stored = await fixture.Collection.SetAsync(new(Key), "selected");
        stored.IsSuccess.ShouldBeTrue();
        (await fixture.Collection.SetAsync(new("untouched"), "retained")).IsSuccess.ShouldBeTrue();
        using var page = await fixture.Client.GetAsync("/ops/caching/collections/items/entries" + suffix);
        var html = await page.Content.ReadAsStringAsync();
        var row = EntryActionForm(html, Key);
        var fields = FormFields(row.Value);
        SetAntiforgeryCookie(fixture.Client, page);

        // Act
        using var rejected = await fixture.Client.PostAsync(FormAction(row.Value), new FormUrlEncodedContent(fields
            .Where(field => field.Key != "__RequestVerificationToken")));
        using var response = await fixture.Client.PostAsync(FormAction(row.Value), new FormUrlEncodedContent(fields));

        // Assert
        page.StatusCode.ShouldBe(HttpStatusCode.OK, html);
        fields["__RequestVerificationToken"].ShouldNotBeNullOrWhiteSpace();
        fields["version"].ShouldBe(stored.Value.Version.Value.Value);
        row.Value.ShouldContain("aria-label=\"Invalidate entry\"");
        row.Value.ShouldContain("title=\"Invalidate entry\"");
        row.Value.ShouldContain("aria-hidden=\"true\"");
        html.ShouldNotContain("product:<script>");
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await fixture.Collection.GetAsync(Key)).Value.Found.ShouldBeFalse();
        (await fixture.Collection.GetAsync("untouched")).Value.Value.ShouldBe("retained");
    }

    /// <summary>Invalidating an old observed row preserves the newer value published under the same key.</summary>
    [Fact]
    public async Task Entries_StaleInvalidateForm_RejectsChangedVersion()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(admin: true, sensitiveMetadata: true);
        (await fixture.Collection.SetAsync(new("changing"), "old")).IsSuccess.ShouldBeTrue();
        using var page = await fixture.Client.GetAsync("/ops/caching/collections/items/entries");
        var row = EntryActionForm(await page.Content.ReadAsStringAsync(), "changing");
        var fields = FormFields(row.Value);
        SetAntiforgeryCookie(fixture.Client, page);
        (await fixture.Collection.SetAsync(new("changing"), "new")).IsSuccess.ShouldBeTrue();

        // Act
        using var response = await fixture.Client.PostAsync(FormAction(row.Value), new FormUrlEncodedContent(fields));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed, await response.Content.ReadAsStringAsync());
        (await fixture.Collection.GetAsync("changing")).Value.Value.ShouldBe("new");
    }

    /// <summary>A valid anonymous read token does not grant authority to invalidate an entry.</summary>
    [Fact]
    public async Task Entries_AnonymousRowForm_DoesNotGrantMutationAuthority()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(sensitiveMetadata: true);
        (await fixture.Collection.SetAsync(new("retained"), "value")).IsSuccess.ShouldBeTrue();
        using var page = await fixture.Client.GetAsync("/ops/caching/collections/items/entries");
        var row = EntryActionForm(await page.Content.ReadAsStringAsync(), "retained");
        var fields = FormFields(row.Value);
        SetAntiforgeryCookie(fixture.Client, page);

        // Act
        using var response = await fixture.Client.PostAsync(FormAction(row.Value), new FormUrlEncodedContent(fields));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await fixture.Collection.GetAsync("retained")).Value.Value.ShouldBe("value");
    }

    /// <summary>A row action never discloses an entry key hidden by the metadata policy.</summary>
    [Fact]
    public async Task Entries_HiddenKeys_DisablesRowActionWithoutLeakingTheKey()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(admin: true);
        const string Key = "private-invalidation-target";
        (await fixture.Collection.SetAsync(new(Key), "value")).IsSuccess.ShouldBeTrue();

        // Act
        var html = await fixture.Client.GetStringAsync("/ops/caching/collections/items/entries");

        // Assert
        var row = Regex.Match(html, @"<form\b[^>]*\bdata-cache-entry-action\b[^>]*>.*?</form>", RegexOptions.Singleline);
        row.Success.ShouldBeTrue();
        Regex.IsMatch(row.Value, @"<button\b[^>]*\bdisabled(?:\s|=|>)").ShouldBeTrue();
        html.ShouldNotContain(Key);
        html.ShouldNotContain(Key.ToUpperInvariant());
        (await fixture.Collection.GetAsync(Key)).Value.Value.ShouldBe("value");
    }

    private static string CollectionCount(string html, string collection)
    {
        var cell = Regex.Match(html, "<td\\b[^>]*\\bdata-cache-collection-count=\"" + Regex.Escape(collection)
            + "\"[^>]*>(?<content>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        cell.Success.ShouldBeTrue();
        return Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(cell.Groups["content"].Value, "<[^>]+>", "")), @"\s+", " ").Trim();
    }

    private static Match EntryActionForm(string html, string key)
    {
        var forms = Regex.Matches(html, @"<form\b[^>]*\bdata-cache-entry-action\b[^>]*>.*?</form>", RegexOptions.Singleline);
        var form = forms.Cast<Match>().SingleOrDefault(match => FormFields(match.Value).TryGetValue("key", out var value)
            && value.Equals(key, StringComparison.OrdinalIgnoreCase));
        form.ShouldNotBeNull();
        return form;
    }

    private static Dictionary<string, string> FormFields(string form)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in Regex.Matches(form, @"<input\b[^>]*>").Cast<Match>())
        {
            var name = Regex.Match(input.Value, "\\bname=\"([^\"]+)\"").Groups[1].Value;
            var value = Regex.Match(input.Value, "\\bvalue=\"([^\"]*)\"").Groups[1].Value;
            if (name.Length != 0) { fields[WebUtility.HtmlDecode(name)] = WebUtility.HtmlDecode(value); }
        }

        return fields;
    }

    private static string FormAction(string form)
    {
        var action = WebUtility.HtmlDecode(Regex.Match(form, "\\baction=\"([^\"]+)\"").Groups[1].Value);
        action.ShouldBe("/ops/caching/actions/remove");
        return action;
    }

    private static void SetAntiforgeryCookie(HttpClient client, HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.Contains("Antiforgery", StringComparison.Ordinal)).Split(';')[0];
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookie);
    }

    private static MatchCollection HelpButtons(string html) => Regex.Matches(html,
        "<button\\b(?<attributes>[^>]*\\bdata-dashboard-help\\b[^>]*)>(?<content>.*?)</button>", RegexOptions.Singleline);

    private static void AssertAccessibleHelp(string html)
    {
        var buttons = HelpButtons(html);
        buttons.Count.ShouldBeGreaterThan(0);
        foreach (var button in buttons.Cast<Match>())
        {
            var attributes = button.Groups["attributes"].Value;
            var title = WebUtility.HtmlDecode(Regex.Match(attributes, "title=\"([^\"]+)\"").Groups[1].Value);
            var label = WebUtility.HtmlDecode(Regex.Match(attributes, "aria-label=\"([^\"]+)\"").Groups[1].Value);
            title.ShouldNotBeNullOrWhiteSpace();
            label.ShouldBe(title);
            attributes.ShouldContain("type=\"button\"");
            attributes.ShouldContain("data-bs-trigger=\"hover focus\"");
            attributes.ShouldContain("data-bs-html=\"false\"");
            attributes.ShouldNotContain("tabindex=\"-1\"");
            button.Groups["content"].Value.ShouldContain("bi-info-circle");
            button.Groups["content"].Value.ShouldContain("aria-hidden=\"true\"");
        }
    }

    private static string ElementContents(string html, string tag, string attribute)
    {
        var element = Regex.Match(html, "<" + tag + @"\b[^>]*\b" + attribute + "[^>]*>(?<content>.*?)</" + tag + ">", RegexOptions.Singleline);
        element.Success.ShouldBeTrue();
        return element.Groups["content"].Value;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(WebApplication app)
        {
            this.App = app;
            this.Options = app.Services.GetRequiredService<DashboardEndpointsOptions>();
            this.Collection = app.Services.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        }

        internal WebApplication App { get; }

        internal DashboardEndpointsOptions Options { get; }

        internal ICacheCollection<string> Collection { get; }

        internal HttpClient Client { get; private set; }

        internal static async Task<Fixture> CreateAsync(Action<CacheCollectionBuilder> collection = null, bool enabled = true, bool admin = false,
            bool sensitiveMetadata = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddLogging();
            builder.Services.AddAuthorization(options => options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireClaim("cache-admin", "yes").Build());
            builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops"));
            var cache = builder.Services.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("items", collection)
                .WithCollection<string>("entries");
            if (enabled) { cache.WithDashboard(options => options.WithSensitiveMetadata(sensitiveMetadata)); }

            var app = builder.Build();
            var fixture = new Fixture(app);
            app.Use(async (context, next) =>
            {
                if (admin)
                {
                    context.User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "operator"), new("cache-admin", "yes")], "test"));
                }

                await next(context);
            });
            app.UseAuthorization();
            app.MapEndpoints();
            await app.StartAsync();
            fixture.Client = app.GetTestClient();
            return fixture;
        }

        internal async Task<Dictionary<string, string>> FormAsync()
        {
            var response = await this.Client.GetAsync("/ops/caching/collections/items/operations");
            var html = await response.Content.ReadAsStringAsync();
            var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value);
            token.ShouldNotBeNullOrEmpty();
            var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.Contains("Antiforgery", StringComparison.Ordinal)).Split(';')[0];
            this.Client.DefaultRequestHeaders.Remove("Cookie");
            this.Client.DefaultRequestHeaders.Add("Cookie", cookie);
            return new() { ["__RequestVerificationToken"] = token };
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.Client.Dispose();
            await this.App.DisposeAsync();
        }
    }
}