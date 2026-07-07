namespace BridgingIT.DevKit.Cli;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides curated DevKit implementation guidance to MCP clients.
/// </summary>
/// <example>
/// <code>
/// var response = new McpGuidanceTools().Get(arguments);
/// </code>
/// </example>
public sealed class McpGuidanceTools
{
    private static readonly IReadOnlyDictionary<string, GuidanceTopic> Topics =
        new Dictionary<string, GuidanceTopic>(StringComparer.OrdinalIgnoreCase)
        {
            ["jobs"] = new(
                "jobs",
                "Implement durable background jobs with definitions, triggers, history and runtime verification.",
                "docs/features-jobs.md",
                [
                    "Read the Jobs documentation before editing.",
                    "Locate existing job definitions, handlers and module registration in the project.",
                    "Add the job definition and handler in the owning module/application boundary.",
                    "Register triggers or scheduling in the same feature setup path used by existing jobs.",
                    "Add focused tests for handler behavior and registration.",
                    "Run the app and verify with bdk_jobs_list and bdk_jobs_runs."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary",
                    "bdk_jobs_list",
                    "bdk_jobs_runs"
                ],
                "Use bdk_docs_search for 'jobs', inspect existing jobs, implement the new job, then verify with bdk_jobs_list."),
            ["messaging"] = new(
                "messaging",
                "Implement asynchronous broker messages with retained-message diagnostics and safe operations.",
                "docs/features-messaging.md",
                [
                    "Read the Messaging documentation before changing message flow.",
                    "Find existing message contracts, handlers, publisher usage and module registration.",
                    "Keep message contracts small and version-tolerant.",
                    "Add handler tests around idempotency, failure behavior and side effects.",
                    "Run the app and inspect bdk_messages_summary, bdk_messages_subscriptions and retained messages."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary",
                    "bdk_messages_summary",
                    "bdk_messages_subscriptions"
                ],
                "Use the docs to match message/handler conventions, then verify the runtime advertises messaging subscriptions."),
            ["queueing"] = new(
                "queueing",
                "Implement single-consumer queued work with retained queue diagnostics and retry/archive workflows.",
                "docs/features-queueing.md",
                [
                    "Read the Queueing documentation before adding queue processors.",
                    "Find existing queue message contracts, handlers and queue registration.",
                    "Decide the logical queue name and message type deliberately.",
                    "Add tests for handler success, failure and retry-safe behavior.",
                    "Run the app and inspect bdk_queueing_summary, bdk_queueing_subscriptions and bdk_queueing_waiting."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary",
                    "bdk_queueing_summary",
                    "bdk_queueing_subscriptions"
                ],
                "Use docs first, align queue naming with the project, then verify queue subscriptions at runtime."),
            ["orchestration"] = new(
                "orchestration",
                "Implement long-running stateful workflows with durable state, signals, timers and investigation support.",
                "docs/features-orchestrations.md",
                [
                    "Read the Orchestrations documentation before adding workflow state.",
                    "Find existing orchestration definitions, states, activities, signals and timers.",
                    "Model explicit state transitions and idempotent activity behavior.",
                    "Add tests for signals, timers, transitions and failure paths.",
                    "Run the app and inspect bdk_orchestrations_list and orchestration details."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary",
                    "bdk_orchestrations_list",
                    "bdk_orchestrations_instance_details"
                ],
                "Use docs to model the workflow shape, then verify active/runtime instances with orchestration tools."),
            ["pipelines"] = new(
                "pipelines",
                "Implement structured multi-step in-process workflows with observable steps and focused tests.",
                "docs/features-pipelines.md",
                [
                    "Read the Pipelines documentation before adding steps.",
                    "Find existing pipeline context, step and registration patterns.",
                    "Keep each step focused, deterministic and independently testable.",
                    "Add tests for step ordering, context changes and error handling.",
                    "Use logs and project diagnostics to verify runtime behavior when the pipeline runs."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary",
                    "bdk_logs_query",
                    "bdk_errors_recent"
                ],
                "Use docs to match the pipeline structure, then verify execution through logs or project-owned diagnostics."),
            ["caching"] = new(
                "caching",
                "Use the DevKit caching abstraction for bounded in-process cache access without leaking implementation details.",
                "docs/common-caching.md",
                [
                    "Read the Caching documentation before adding cache usage.",
                    "Locate existing cache registrations and cache key conventions in the project.",
                    "Keep cached values bounded, serializable when needed and safe to evict.",
                    "Put cache access at application or infrastructure boundaries, not inside domain models.",
                    "Add tests for cache hit, miss and invalidation behavior where the cache changes observable behavior."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use bdk_docs_search for 'caching', inspect existing cache conventions, then add focused cache behavior tests."),
            ["mapping"] = new(
                "mapping",
                "Map between domain, application and presentation models explicitly using the DevKit mapping conventions.",
                "docs/common-mapping.md",
                [
                    "Read the Mapping documentation before adding model conversions.",
                    "Find existing mapper registrations and mapping profile patterns.",
                    "Keep mapping at boundaries and avoid putting mapping concerns into domain types.",
                    "Prefer explicit tests for non-trivial mappings, especially enum, id and nested value conversions.",
                    "Run the relevant unit tests after adding or changing mapping behavior."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the mapping docs to match boundary mapping conventions before adding DTO or API model conversions."),
            ["serialization"] = new(
                "serialization",
                "Use shared serialization abstractions and JSON conventions consistently across application boundaries.",
                "docs/common-serialization.md",
                [
                    "Read the Serialization documentation before adding serializer usage.",
                    "Locate existing serializer registrations and configured JSON options.",
                    "Keep serialized contracts stable and avoid persistence-breaking shape changes.",
                    "Use shared serializer abstractions instead of ad hoc JsonSerializer calls when the codebase already uses them.",
                    "Add tests for compatibility-sensitive payloads and polymorphic or value-object serialization."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the serialization docs to align JSON contracts and serializer abstractions before editing payload code."),
            ["utilities"] = new(
                "utilities",
                "Reuse shared DevKit utility building blocks for clocks, ids, hashing, cloning, resilience and activity helpers.",
                "docs/common-utilities.md",
                [
                    "Read the Utilities documentation before creating a new helper.",
                    "Search for an existing utility that already covers the behavior.",
                    "Keep utilities small, deterministic and independent of feature-specific application logic.",
                    "Prefer dependency-injected abstractions for time, ids and environment-sensitive behavior.",
                    "Add focused tests for edge cases because utilities tend to become widely reused."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the utilities docs before adding helpers, and prefer existing utilities over local one-off implementations."),
            ["commands_queries"] = new(
                "commands_queries",
                "Implement application use cases as focused commands, queries and handlers with clear boundaries.",
                "docs/features-application-commands-queries.md",
                [
                    "Read the Commands and Queries documentation before adding an application use case.",
                    "Decide whether the use case changes state, reads state or coordinates both through separate handlers.",
                    "Keep domain rules in the domain and orchestration/application decisions in the handler.",
                    "Use requester pipeline behaviors for validation, logging or cross-cutting concerns.",
                    "Add handler tests that cover success, validation failure and important side effects."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the commands/queries docs to shape the handler, then verify tests around the use-case boundary."),
            ["application_events"] = new(
                "application_events",
                "Publish and handle application-layer events through notifier flows with explicit outcomes.",
                "docs/features-application-events.md",
                [
                    "Read the Application Events documentation before adding event publication.",
                    "Use application events for application-level integration or coordination, not domain invariants.",
                    "Keep event payloads stable and small.",
                    "Handle idempotency and failure behavior explicitly in event handlers.",
                    "Add tests for event publication and handler behavior where the event changes observable state."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the application events docs to choose event shape, handler registration and notifier behavior."),
            ["activeentity"] = new(
                "activeentity",
                "Use ActiveEntity for entity-centric persistence convenience while preserving Result-driven outcomes.",
                "docs/features-domain-activeentity.md",
                [
                    "Read the ActiveEntity documentation before adding entity-centric persistence.",
                    "Confirm ActiveEntity is the right fit instead of a repository or use-case handler.",
                    "Keep business invariants in the domain model and persistence concerns behind the provider.",
                    "Use Result outcomes for recoverable failures.",
                    "Add tests for persistence-facing behavior and domain state transitions."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the ActiveEntity docs to decide whether this pattern fits before adding persistence behavior."),
            ["domain_events"] = new(
                "domain_events",
                "Capture business-significant domain events inside aggregates and publish side effects outside the model.",
                "docs/features-domain-events.md",
                [
                    "Read the Domain Events documentation before adding event emission.",
                    "Raise events only for facts that happened in the domain.",
                    "Keep event payloads meaningful and stable.",
                    "Do not execute infrastructure side effects from the domain model.",
                    "Add domain tests that assert events are raised for the right state transitions."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the domain events docs to model business facts and keep side effects outside aggregates."),
            ["repositories"] = new(
                "repositories",
                "Access aggregates through repository abstractions with specifications, paging and loading options.",
                "docs/features-domain-repositories.md",
                [
                    "Read the Repository documentation before adding persistence access.",
                    "Keep repository usage outside the domain layer.",
                    "Use specifications for reusable query criteria instead of duplicating predicates.",
                    "Choose loading and paging options deliberately to avoid hidden N+1 behavior.",
                    "Add tests around query semantics or use-case behavior rather than repository plumbing alone."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the repository docs to align aggregate access with specifications and existing persistence conventions."),
            ["specifications"] = new(
                "specifications",
                "Model reusable query and business criteria as composable domain specifications.",
                "docs/features-domain-specifications.md",
                [
                    "Read the Specifications documentation before adding reusable criteria.",
                    "Name specifications after the business criterion they represent.",
                    "Keep specifications composable and side-effect free.",
                    "Use them in repositories or in-memory evaluation where the codebase already supports it.",
                    "Add tests for boundary conditions and composition behavior."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the specifications docs to express reusable criteria before adding repository query logic."),
            ["domain"] = new(
                "domain",
                "Build domain models with aggregates, entities, value objects, typed ids, invariants and policies.",
                "docs/features-domain.md",
                [
                    "Read the Domain documentation before changing core model behavior.",
                    "Identify aggregate boundaries, invariants and value objects before adding services or persistence.",
                    "Keep domain logic independent of application, infrastructure and presentation concerns.",
                    "Use Result or rules for recoverable business failures where the existing pattern does so.",
                    "Add domain tests for invariants, state transitions and emitted domain events."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the domain docs to model invariants first, then implement application and persistence code around them."),
            ["filtering"] = new(
                "filtering",
                "Use DevKit filtering to express complex query filters consistently across application boundaries.",
                "docs/features-filtering.md",
                [
                    "Read the Filtering documentation before adding query filter behavior.",
                    "Locate existing filter models and filter-to-query mapping patterns.",
                    "Keep filter inputs bounded and validate ambiguous or expensive filters.",
                    "Prefer the shared filtering abstraction over ad hoc query parameter parsing.",
                    "Add tests for important filter combinations and empty/default filter behavior."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the filtering docs to align query input models and repository/application query behavior."),
            ["modules"] = new(
                "modules",
                "Structure modular monolith features as independently configurable modules with clear registration.",
                "docs/features-modules.md",
                [
                    "Read the Modules documentation before adding or moving feature registration.",
                    "Place services, endpoints, jobs and handlers in the owning module boundary.",
                    "Keep module dependencies explicit and avoid cross-module service leakage.",
                    "Use existing module registration conventions in the host builder.",
                    "Verify the app starts and the selected runtime advertises the expected capabilities."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary",
                    "bdk_capabilities_get"
                ],
                "Use the modules docs to place registrations in the owning module and verify with bdk_project_summary."),
            ["requester_notifier"] = new(
                "requester_notifier",
                "Dispatch application requests and notifications through requester/notifier pipelines.",
                "docs/features-requester-notifier.md",
                [
                    "Read the Requester and Notifier documentation before adding dispatch behavior.",
                    "Use requester for request/response use cases and notifier for fan-out notifications.",
                    "Keep pipeline behaviors focused on cross-cutting concerns.",
                    "Avoid service locator patterns in handlers; depend on explicit abstractions.",
                    "Add tests for handler behavior and pipeline effects that change outcomes."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the requester/notifier docs to choose request or notification dispatch and handler registration."),
            ["results"] = new(
                "results",
                "Represent success, failure, validation messages and recoverable errors explicitly with Result types.",
                "docs/features-results.md",
                [
                    "Read the Results documentation before changing error handling.",
                    "Use Result for expected business or validation failures.",
                    "Reserve exceptions for exceptional infrastructure or programming failures.",
                    "Propagate messages and errors without losing useful context.",
                    "Add tests for both success and failure outcomes."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the results docs to model recoverable failures explicitly before adding handler or domain logic."),
            ["rules"] = new(
                "rules",
                "Express reusable business rules as composable validations with consistent Result outcomes.",
                "docs/features-rules.md",
                [
                    "Read the Rules documentation before adding validation logic.",
                    "Put invariant rules close to the domain concept they protect.",
                    "Keep rules focused, named and independently testable.",
                    "Compose rules instead of duplicating validation logic across handlers.",
                    "Add tests for pass, fail and edge conditions."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the rules docs to express business checks as reusable rule objects with Result outcomes."),
            ["startuptasks"] = new(
                "startuptasks",
                "Run application startup work through structured, observable and dependency-aware startup tasks.",
                "docs/features-startuptasks.md",
                [
                    "Read the StartupTasks documentation before adding startup work.",
                    "Use startup tasks for bounded initialization, not long-running background loops.",
                    "Make ordering and dependencies explicit where needed.",
                    "Keep startup work idempotent so restarts are safe.",
                    "Verify startup behavior with logs and focused tests around task registration or execution."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary",
                    "bdk_logs_query",
                    "bdk_errors_recent"
                ],
                "Use the startup tasks docs to add idempotent initialization and verify startup logs."),
            ["document_storage"] = new(
                "document_storage",
                "Store and query JSON-like documents through the provider-agnostic DocumentStorage abstraction.",
                "docs/features-storage-documents.md",
                [
                    "Read the DocumentStorage documentation before adding document persistence.",
                    "Choose document ids, partitions and query shape deliberately.",
                    "Keep document contracts version-tolerant and serialization-safe.",
                    "Use the application storage abstraction instead of provider-specific APIs in application code.",
                    "Add tests for save, load, query and missing-document behavior."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the document storage docs to align document contracts and provider-independent access."),
            ["file_storage"] = new(
                "file_storage",
                "Read, write, move and monitor files through extensible FileStorage providers and behaviors.",
                "docs/features-storage-files.md",
                [
                    "Read the FileStorage documentation before adding file operations.",
                    "Keep application code provider-agnostic and use configured storage abstractions.",
                    "Validate paths, names and content boundaries carefully.",
                    "Model overwrite, missing-file and concurrency behavior explicitly.",
                    "Add tests for success, missing file and failure paths."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use the file storage docs to keep file operations provider-independent and test edge cases."),
            ["monitoring"] = new(
                "monitoring",
                "Detect storage changes and process file events through configurable monitoring pipelines.",
                "docs/features-storage-monitoring.md",
                [
                    "Read the Storage Monitoring documentation before adding watchers or event processors.",
                    "Choose watched locations, filters and polling/event behavior deliberately.",
                    "Keep event processing idempotent because file systems can emit duplicate or reordered events.",
                    "Use logs and runtime diagnostics to verify monitoring behavior.",
                    "Add tests around event handling logic and configuration shape where practical."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary",
                    "bdk_logs_query",
                    "bdk_errors_recent"
                ],
                "Use the storage monitoring docs to configure watchers and verify file event processing through logs."),
            ["dashboard"] = new(
                "dashboard",
                "Add developer dashboard pages with server-rendered RazorSlice content and dashboard navigation metadata.",
                "docs/features-presentation-dashboard.md",
                [
                    "Read the Dashboard documentation before adding a page.",
                    "Choose whether the page belongs to a DevKit package or a project module.",
                    "Use a dashboard page provider or page set matching existing project conventions.",
                    "Keep page data bounded and read through in-process services, not ad hoc HTTP calls.",
                    "Verify the page is visible only when the owning feature is enabled."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary",
                    "bdk_capabilities_get"
                ],
                "Use docs to add the dashboard page, then verify the dashboard and MCP summary expose the expected feature."),
            ["project_dashboard_page"] = new(
                "project_dashboard_page",
                "Add a project-owned dashboard page for module-specific local diagnostics.",
                "docs/features-presentation-dashboard.md",
                [
                    "Read the Dashboard documentation before adding project UI.",
                    "Place the page in the owning presentation module.",
                    "Prefer a module dashboard page set when the module owns multiple pages.",
                    "Use module/application services for bounded read models.",
                    "Add tests for page provider metadata and content model behavior when practical."
                ],
                [
                    "bdk_docs_search",
                    "bdk_docs_get",
                    "bdk_project_summary"
                ],
                "Use docs to follow dashboard conventions and keep the page scoped to project diagnostics.")
        };

    private static readonly IReadOnlyList<GuidanceTopicAlias> TopicAliases =
    [
        new("jobs", ["job", "jobs", "background job", "scheduled job", "recurring job"]),
        new("messaging", ["message", "messages", "messaging", "broker", "publisher", "subscriber", "subscription"]),
        new("queueing", ["queue", "queues", "queued", "queueing", "queue message", "queue processor"]),
        new("orchestration", ["orchestration", "orchestrations", "orchestrator", "workflow", "signal", "timer"]),
        new("pipelines", ["pipeline", "pipelines", "step", "steps"]),
        new("caching", ["cache", "caching", "memory cache"]),
        new("mapping", ["map", "maps", "mapping", "mapper", "mapster", "dto conversion"]),
        new("serialization", ["serialization", "serializer", "serializing", "json", "payload"]),
        new("utilities", ["utility", "utilities", "helper", "helpers", "time provider", "clock", "ids", "hashing", "cloning"]),
        new("commands_queries", ["command", "commands", "query", "queries", "handler", "handlers", "cqrs", "use case", "request handler"]),
        new("application_events", ["application event", "application events", "notification event"]),
        new("activeentity", ["activeentity", "active entity", "active record"]),
        new("domain_events", ["domain event", "domain events"]),
        new("repositories", ["repository", "repositories", "repo"]),
        new("specifications", ["specification", "specifications", "criteria", "predicate"]),
        new("domain", ["domain", "aggregate", "aggregates", "entity", "entities", "value object", "value objects", "typed id", "invariant", "ddd"]),
        new("filtering", ["filter", "filters", "filtering"]),
        new("modules", ["module", "modules", "modular"]),
        new("requester_notifier", ["requester", "notifier", "mediator", "request pipeline", "notification pipeline"]),
        new("results", ["result", "results", "error handling", "recoverable failure"]),
        new("rules", ["rule", "rules", "business rule", "business rules", "validation"]),
        new("startuptasks", ["startup task", "startup tasks", "startuptask", "initialization", "startup work"]),
        new("document_storage", ["document storage", "documentstore", "document store", "documents"]),
        new("file_storage", ["file storage", "filestorage", "file store", "files"]),
        new("monitoring", ["monitoring", "storage monitoring", "file monitoring", "watcher", "watchers", "file watcher"]),
        new("project_dashboard_page", ["project dashboard", "project dashboard page", "module dashboard"]),
        new("dashboard", ["dashboard", "dashboard page", "razor dashboard", "developer dashboard"])
    ];

    private static readonly ISet<string> ApiReferenceTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "results",
        "rules",
        "repositories",
        "specifications",
        "domain",
        "domain_events",
        "requester_notifier",
        "commands_queries",
        "application_events",
        "jobs",
        "queueing",
        "messaging",
        "orchestration",
        "document_storage",
        "file_storage",
        "monitoring",
        "caching"
    };

    /// <summary>
    /// Lists available guidance topics.
    /// </summary>
    /// <param name="arguments">The ignored tool arguments.</param>
    /// <returns>The response.</returns>
    public McpResponse List(JsonElement arguments)
        => McpResponse.Success(
            $"Found {Topics.Count} DevKit guidance topic(s).",
            new
            {
                topics = Topics.Values
                    .OrderBy(topic => topic.Topic, StringComparer.OrdinalIgnoreCase)
                    .Select(topic => new
                    {
                        topic = topic.Topic,
                        summary = topic.Summary,
                        docs = topic.Docs
                    })
                    .ToArray()
            });

    /// <summary>
    /// Gets guidance for a topic.
    /// </summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <returns>The response.</returns>
    public McpResponse Get(JsonElement arguments)
    {
        var topic = McpJson.GetString(arguments, "topic")?.Trim();
        var query = McpJson.GetString(arguments, "query")?.Trim();
        if (!string.IsNullOrWhiteSpace(topic))
        {
            if (!Topics.TryGetValue(topic, out var guidance))
            {
                return McpResponse.Unavailable(
                    McpErrorCode.FeatureUnavailable,
                    $"Guidance topic '{topic}' is not available.",
                    "Call bdk_guidance_list to inspect available guidance topics, or call bdk_guidance_get with a natural-language query.",
                    [
                        new McpNextCall("bdk_guidance_list", new { }),
                        new McpNextCall("bdk_guidance_get", new { query = topic })
                    ]);
            }

            return CreateGuidanceResponse([guidance], query);
        }

        var inferredGuidance = InferTopics(query);
        if (inferredGuidance.Count > 0)
        {
            return CreateGuidanceResponse(inferredGuidance, query);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return this.List(arguments);
        }

        return McpResponse.Unavailable(
            McpErrorCode.FeatureUnavailable,
            "No matching DevKit guidance topic could be inferred.",
            "Call bdk_guidance_list to inspect available topics, or retry bdk_guidance_get with a query that mentions a DevKit feature area such as jobs, repositories, specifications, storage, modules, rules or dashboard work.",
            [
                new McpNextCall("bdk_guidance_list", new { }),
                new McpNextCall("bdk_guidance_get", new { query })
            ]);
    }

    private static McpResponse CreateGuidanceResponse(IReadOnlyList<GuidanceTopic> guidanceTopics, string query)
    {
        if (guidanceTopics.Count == 0)
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                "No guidance topic was selected.",
                "Call bdk_guidance_list to inspect available guidance topics.",
                [new McpNextCall("bdk_guidance_list", new { })]);
        }

        if (guidanceTopics.Count == 1)
        {
            var guidance = guidanceTopics[0];
            return McpResponse.Success(
                $"Returned DevKit guidance for {guidance.Topic}.",
                new
                {
                    guidance.Topic,
                    guidance.Summary,
                    guidance.Docs,
                    query,
                    steps = guidance.Steps,
                    recommendedTools = GetRecommendedTools(guidance),
                    prompt = guidance.Prompt,
                    workflow = GetWorkflow(guidance)
                },
                next: GetNextCalls(guidance));
        }

        return McpResponse.Success(
            $"Returned DevKit guidance for {string.Join(", ", guidanceTopics.Select(guidance => guidance.Topic))}.",
            new
            {
                query,
                topics = guidanceTopics.Select(guidance => new
                {
                    guidance.Topic,
                    guidance.Summary,
                    guidance.Docs,
                    steps = guidance.Steps,
                    recommendedTools = GetRecommendedTools(guidance),
                    prompt = guidance.Prompt
                }).ToArray(),
                workflow = guidanceTopics.Any(UsesApiReference)
                    ? new[] { "guidance", "docs", "api reference", "code", "runtime verification" }
                    : new[] { "guidance", "docs", "code", "runtime verification" }
            },
            next:
            guidanceTopics
                .SelectMany(GetNextCalls)
                .Append(new McpNextCall("bdk_project_summary", new { }))
                .DistinctBy(call => call.Tool + JsonSerializer.Serialize(call.Arguments, CliJson.Options))
                .ToArray());
    }

    private static IReadOnlyList<string> GetRecommendedTools(GuidanceTopic guidance)
        => UsesApiReference(guidance) && !guidance.RecommendedTools.Contains("bdk_api_search", StringComparer.OrdinalIgnoreCase)
            ? guidance.RecommendedTools.Concat(["bdk_api_search"]).ToArray()
            : guidance.RecommendedTools;

    private static IReadOnlyList<string> GetWorkflow(GuidanceTopic guidance)
        => UsesApiReference(guidance)
            ? ["guidance", "docs", "api reference", "code", "runtime verification"]
            : ["guidance", "docs", "code", "runtime verification"];

    private static IReadOnlyList<McpNextCall> GetNextCalls(GuidanceTopic guidance)
    {
        var calls = new List<McpNextCall>
        {
            new("bdk_docs_search", new { query = guidance.Topic }),
            new("bdk_docs_get", new { source = guidance.Docs })
        };

        if (UsesApiReference(guidance))
        {
            calls.Add(new McpNextCall("bdk_api_search", new { query = guidance.Topic, topic = guidance.Topic }));
        }

        calls.Add(new McpNextCall("bdk_project_summary", new { }));

        return calls;
    }

    private static bool UsesApiReference(GuidanceTopic guidance)
        => ApiReferenceTopics.Contains(guidance.Topic);

    private static IReadOnlyList<GuidanceTopic> InferTopics(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        if (Topics.TryGetValue(query.Trim(), out var exactGuidance))
        {
            return [exactGuidance];
        }

        var normalizedQuery = Normalize(query);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<GuidanceTopic>();

        foreach (var alias in TopicAliases)
        {
            if (alias.Terms.Any(term => ContainsTerm(normalizedQuery, term)) &&
                seen.Add(alias.Topic) &&
                Topics.TryGetValue(alias.Topic, out var guidance))
            {
                result.Add(guidance);
            }
        }

        return result;
    }

    private static bool ContainsTerm(string normalizedQuery, string term)
        => normalizedQuery.Contains($" {Normalize(term).Trim()} ", StringComparison.Ordinal);

    private static string Normalize(string value)
    {
        var characters = value.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();

        return $" {new string(characters)} ";
    }

    private sealed record GuidanceTopic(
        string Topic,
        string Summary,
        string Docs,
        IReadOnlyList<string> Steps,
        IReadOnlyList<string> RecommendedTools,
        string Prompt);

    private sealed record GuidanceTopicAlias(string Topic, IReadOnlyList<string> Terms);
}