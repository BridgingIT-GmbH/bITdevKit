// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

/// <summary>Loads the curated task-title catalog through individually cached reference entries.</summary>
/// <param name="cache">The application-owned cache entry point.</param>
/// <remarks>The static source contains fictional reference suggestions and no task or authorization data.</remarks>
/// <example><code>var suggestions = await catalog.GetAllAsync(cancellationToken);</code></example>
public sealed class TaskLookupCatalog(IApplicationCache cache)
{
    /// <summary>Identifies the declared collection containing one entry per task-title suggestion.</summary>
    /// <example><code>var collection = cache.GetCollection&lt;TaskLookupModel&gt;(TaskLookupCatalog.CollectionName);</code></example>
    public const string CollectionName = "task-lookups";

    private static readonly IReadOnlyList<TaskLookupModel> Entries = Array.AsReadOnly<TaskLookupModel>(
    [
        new("Implement API endpoint", "Development", "ASP.NET Core", "en"),
        new("Review pull request", "Development", "General", "en"),
        new("Refactor application service", "Development", ".NET", "en"),
        new("Fix input validation", "Development", ".NET", "en"),
        new("Add database migration", "Development", "Entity Framework Core", "en"),
        new("Implement domain rule", "Development", ".NET", "en"),
        new("Build form component", "Development", "Blazor", "en"),
        new("Add pagination to endpoint", "Development", "ASP.NET Core", "en"),
        new("Handle cancellation correctly", "Development", ".NET", "en"),
        new("Improve error response", "Development", "ASP.NET Core", "en"),
        new("Implement background job", "Development", ".NET", "en"),
        new("Add keyboard navigation", "Development", "Blazor", "en"),
        new("Add unit tests", "Testing", ".NET", "en"),
        new("Write integration tests", "Testing", "ASP.NET Core", "en"),
        new("Reproduce reported bug", "Testing", "General", "en"),
        new("Test authorization boundary", "Testing", "ASP.NET Core", "en"),
        new("Cover cancellation scenario", "Testing", ".NET", "en"),
        new("Verify database constraints", "Testing", "SQL", "en"),
        new("Test file upload limits", "Testing", "ASP.NET Core", "en"),
        new("Add browser regression test", "Testing", "Playwright", "en"),
        new("Check mobile layout", "Testing", "Blazor", "en"),
        new("Validate API contract", "Testing", "OpenAPI", "en"),
        new("Test cache invalidation", "Testing", ".NET", "en"),
        new("Review flaky test", "Testing", "General", "en"),
        new("Update CI pipeline", "DevOps", "GitHub Actions", "en"),
        new("Configure deployment workflow", "DevOps", "GitHub Actions", "en"),
        new("Upgrade application dependencies", "DevOps", ".NET", "en"),
        new("Update container base image", "DevOps", "Docker", "en"),
        new("Configure health checks", "DevOps", "ASP.NET Core", "en"),
        new("Set resource limits", "DevOps", "Kubernetes", "en"),
        new("Configure structured logging", "DevOps", ".NET", "en"),
        new("Add deployment smoke test", "DevOps", "General", "en"),
        new("Review database backup schedule", "DevOps", "SQL", "en"),
        new("Configure monitoring alerts", "DevOps", "General", "en"),
        new("Verify rollback procedure", "DevOps", "General", "en"),
        new("Automate release packaging", "DevOps", ".NET", "en"),
        new("Review endpoint permissions", "Security", "ASP.NET Core", "en"),
        new("Rotate service certificates", "Security", "General", "en"),
        new("Audit dependency vulnerabilities", "Security", ".NET", "en"),
        new("Review authentication configuration", "Security", "OpenID Connect", "en"),
        new("Validate file upload content", "Security", "ASP.NET Core", "en"),
        new("Remove sensitive log fields", "Security", ".NET", "en"),
        new("Review secret storage", "Security", "General", "en"),
        new("Configure request rate limits", "Security", "ASP.NET Core", "en"),
        new("Check cross-origin policy", "Security", "ASP.NET Core", "en"),
        new("Verify tenant isolation", "Security", "General", "en"),
        new("Review database access grants", "Security", "SQL", "en"),
        new("Check security response headers", "Security", "ASP.NET Core", "en"),
        new("Optimize database query", "Performance", "SQL", "en"),
        new("Investigate memory usage", "Performance", ".NET", "en"),
        new("Measure endpoint latency", "Performance", "ASP.NET Core", "en"),
        new("Reduce serialization allocations", "Performance", ".NET", "en"),
        new("Review database indexes", "Performance", "SQL", "en"),
        new("Profile slow page rendering", "Performance", "Blazor", "en"),
        new("Eliminate repeated database reads", "Performance", "Entity Framework Core", "en"),
        new("Tune cache capacity", "Performance", ".NET", "en"),
        new("Benchmark batch processing", "Performance", ".NET", "en"),
        new("Investigate connection pool pressure", "Performance", "SQL", "en"),
        new("Reduce frontend bundle size", "Performance", "Blazor", "en"),
        new("Review background job concurrency", "Performance", ".NET", "en"),
        new("Update API documentation", "Documentation", "OpenAPI", "en"),
        new("Write local setup guide", "Documentation", "General", "en"),
        new("Document configuration options", "Documentation", ".NET", "en"),
        new("Add code usage examples", "Documentation", ".NET", "en"),
        new("Describe deployment procedure", "Documentation", "Docker", "en"),
        new("Write troubleshooting guide", "Documentation", "General", "en"),
        new("Document database recovery", "Documentation", "SQL", "en"),
        new("Explain authorization policies", "Documentation", "ASP.NET Core", "en"),
        new("Update release notes", "Documentation", "General", "en"),
        new("Document cache refresh behavior", "Documentation", ".NET", "en"),
        new("Review public XML comments", "Documentation", ".NET", "en"),
        new("Record architecture decision", "Documentation", "General", "en")
    ]);

    /// <summary>Returns all suggestions, reloading individual missing or expired entries from the static catalog.</summary>
    /// <param name="cancellationToken">Cancels this caller's cache waits.</param>
    /// <returns>A read-only catalog or the first explicit cache/source failure.</returns>
    /// <example><code>var result = await catalog.GetAllAsync(cancellationToken);</code></example>
    public async ValueTask<Result<IReadOnlyList<TaskLookupModel>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var collection = cache.GetCollection<TaskLookupModel>(CollectionName);
        var suggestions = new List<TaskLookupModel>(Entries.Count);

        foreach (var entry in Entries)
        {
            var item = new CacheItem($"task-title:{entry.Language}:{entry.Category}:{entry.Title.Replace(' ', '-')}")
                .WithScalarDimension("category", entry.Category)
                .WithScalarDimension("technology", entry.Technology)
                .WithScalarDimension("language", entry.Language);
            var result = await collection.GetOrLoadAsync(item,
                (_, _) =>
                    ValueTask.FromResult<Result<CacheLoadResult<TaskLookupModel>>>(new CacheLoadResult<TaskLookupModel>(entry)),
                cancellationToken);
            if (result.IsFailure)
            {
                return Result<IReadOnlyList<TaskLookupModel>>.Failure(result);
            }

            if (!result.Value.Found || result.Value.Value is null)
            {
                return Result<IReadOnlyList<TaskLookupModel>>.Failure(new Error("A task-title suggestion was unavailable."));
            }

            suggestions.Add(result.Value.Value);
        }

        return Result<IReadOnlyList<TaskLookupModel>>.Success(suggestions.AsReadOnly());
    }
}