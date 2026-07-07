namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Provides the stable MCP tool catalog exposed by <c>bdk mcp</c>.
/// </summary>
/// <example>
/// <code>
/// var tools = new McpToolCatalog().Tools;
/// </code>
/// </example>
public sealed class McpToolCatalog
{
    private static readonly object EmptySchema = new { type = "object", additionalProperties = false, properties = new { } };

    /// <summary>
    /// Gets the stable tools.
    /// </summary>
    public IReadOnlyList<McpToolDefinition> Tools { get; } =
    [
        Tool("bdk_mcp_status", "Reports bdk MCP server, workspace, descriptor, runtime, and documentation status.", EmptySchema),
        Tool("bdk_mcp_self_test", "Checks runtime selection, IPC connectivity, protocol compatibility, capabilities, and documentation lookup.", EmptySchema),
        Tool("bdk_mcp_explain_setup", "Explains how to configure an MCP client for bdk.", EmptySchema),
        Tool("bdk_runtimes_list", "Lists ready local DevKit runtimes for the workspace.", EmptySchema),
        Tool("bdk_runtimes_current", "Returns the currently selected local DevKit runtime.", EmptySchema),
        Tool("bdk_runtimes_select", "Selects a local DevKit runtime for this workspace.", new { type = "object", additionalProperties = false, required = new[] { "runtimeId" }, properties = new { runtimeId = String("Runtime id to select.") } }),
        Tool("bdk_runtimes_refresh", "Refreshes runtime discovery for this workspace.", EmptySchema),
        Tool("bdk_capabilities_get", "Returns MCP capabilities from the selected runtime.", EmptySchema),
        Tool("bdk_health_snapshot", "Returns a bounded health snapshot from the selected runtime.", EmptySchema),
        Tool("bdk_metrics_snapshot", "Returns a bounded runtime metrics snapshot from the selected runtime.", EmptySchema),
        Tool("bdk_metrics_query", "Queries bounded runtime metrics from the selected runtime.", EmptySchema),
        Tool("bdk_logs_query", "Queries bounded log entries from the selected runtime.", LogQuerySchema()),
        Tool("bdk_logs_tail", "Returns recent bounded log entries from the selected runtime.", LogQuerySchema()),
        Tool("bdk_logs_purge", "Purges retained logs when the admin toolset is enabled and confirmation is supplied.", LogPurgeSchema()),
        Tool("bdk_errors_recent", "Returns recent errors from the selected runtime.", LogQuerySchema()),
        Tool("bdk_errors_details", "Returns details for a selected error.", Id64Schema("Log entry id.")),
        Tool("bdk_correlation_inspect", "Aggregates diagnostics for a correlation id.", CorrelationSchema()),
        Tool("bdk_investigate_recent_errors", "Aggregates recent error diagnostics from the selected runtime.", LogQuerySchema()),
        Tool("bdk_investigate_correlation", "Aggregates diagnostics for a correlation id.", CorrelationSchema()),
        Tool("bdk_investigate_job_run", "Aggregates diagnostics for a job run.", JobRunInvestigationSchema()),
        Tool("bdk_investigate_orchestration_instance", "Aggregates diagnostics for an orchestration instance.", InstanceIdSchema()),
        Tool("bdk_messages_summary", "Returns broker message runtime summary from the selected runtime.", EmptySchema),
        Tool("bdk_messages_subscriptions", "Lists broker message subscriptions from the selected runtime.", EmptySchema),
        Tool("bdk_messages_waiting", "Lists broker messages waiting for handlers.", TakeSchema()),
        Tool("bdk_messages_list", "Lists retained broker messages from the selected runtime.", MessageListSchema(includeQueueName: false)),
        Tool("bdk_messages_details", "Returns broker message details from the selected runtime.", GuidIdSchema()),
        Tool("bdk_messages_content", "Returns persisted broker message content from the selected runtime.", GuidIdSchema()),
        Tool("bdk_messages_retry", "Retries a broker message or handler when the operations toolset is enabled.", MessageRetrySchema()),
        Tool("bdk_messages_release_lease", "Releases a broker message lease when the operations toolset is enabled.", GuidIdSchema()),
        Tool("bdk_messages_archive", "Archives a broker message when the operations toolset is enabled.", GuidIdSchema()),
        Tool("bdk_messages_pause_type", "Pauses broker message processing for a type.", TypeSchema()),
        Tool("bdk_messages_resume_type", "Resumes broker message processing for a type.", TypeSchema()),
        Tool("bdk_messages_purge", "Purges retained broker messages when the admin toolset is enabled and confirmation is supplied.", RetainedMessagesPurgeSchema("purge messages")),
        Tool("bdk_queueing_messages", "Lists retained queue messages from the selected runtime.", MessageListSchema(includeQueueName: true)),
        Tool("bdk_queueing_summary", "Returns queue broker runtime summary from the selected runtime.", EmptySchema),
        Tool("bdk_queueing_subscriptions", "Lists queue subscriptions from the selected runtime.", EmptySchema),
        Tool("bdk_queueing_waiting", "Lists queue messages waiting for handlers.", TakeSchema()),
        Tool("bdk_queueing_message_details", "Returns queue message details from the selected runtime.", QueueMessageDetailsSchema()),
        Tool("bdk_queueing_retry", "Retries a queue message when the operations toolset is enabled.", GuidIdSchema()),
        Tool("bdk_queueing_release_lease", "Releases a queue message lease when the operations toolset is enabled.", GuidIdSchema()),
        Tool("bdk_queueing_archive", "Archives a queue message when the operations toolset is enabled.", GuidIdSchema()),
        Tool("bdk_queueing_pause_queue", "Pauses queue processing for a logical queue.", QueueNameSchema()),
        Tool("bdk_queueing_resume_queue", "Resumes queue processing for a logical queue.", QueueNameSchema()),
        Tool("bdk_queueing_pause_type", "Pauses queue processing for a message type.", TypeSchema()),
        Tool("bdk_queueing_resume_type", "Resumes queue processing for a message type.", TypeSchema()),
        Tool("bdk_queueing_purge", "Purges retained queue messages when the admin toolset is enabled and confirmation is supplied.", RetainedMessagesPurgeSchema("purge queue messages")),
        Tool("bdk_jobs_list", "Lists job definitions or recent job runs from the selected runtime.", JobsListSchema()),
        Tool("bdk_jobs_details", "Returns job details from the selected runtime.", JobNameSchema()),
        Tool("bdk_jobs_runs", "Returns job execution history from the selected runtime.", JobRunsSchema()),
        Tool("bdk_jobs_run_stats", "Returns job run statistics from the selected runtime.", JobStatsSchema()),
        Tool("bdk_jobs_trigger", "Triggers a job when the operations toolset is enabled.", JobTriggerSchema()),
        Tool("bdk_jobs_pause", "Pauses a job when the operations toolset is enabled.", JobReasonSchema()),
        Tool("bdk_jobs_resume", "Resumes a job when the operations toolset is enabled.", JobReasonSchema()),
        Tool("bdk_jobs_interrupt", "Interrupts a job when the operations toolset is enabled.", JobInterruptSchema()),
        Tool("bdk_jobs_purge_runs", "Purges retained job runs when the admin toolset is enabled and confirmation is supplied.", JobPurgeSchema()),
        Tool("bdk_orchestrations_list", "Lists orchestration instances from the selected runtime.", OrchestrationListSchema()),
        Tool("bdk_orchestrations_instances", "Lists orchestration instances from the selected runtime.", OrchestrationListSchema()),
        Tool("bdk_orchestrations_instance_details", "Returns details for an orchestration instance.", InstanceIdSchema()),
        Tool("bdk_orchestrations_history", "Returns orchestration history from the selected runtime.", InstanceIdSchema()),
        Tool("bdk_orchestrations_signals", "Returns orchestration signals from the selected runtime.", InstanceIdSchema()),
        Tool("bdk_orchestrations_timers", "Returns orchestration timers from the selected runtime.", InstanceIdSchema()),
        Tool("bdk_orchestrations_signal", "Signals an orchestration when the operations toolset is enabled.", OrchestrationSignalSchema()),
        Tool("bdk_orchestrations_pause", "Pauses an orchestration when the operations toolset is enabled.", OrchestrationReasonSchema()),
        Tool("bdk_orchestrations_resume", "Resumes an orchestration when the operations toolset is enabled.", InstanceIdSchema()),
        Tool("bdk_orchestrations_cancel", "Cancels an orchestration when the operations toolset is enabled.", OrchestrationReasonSchema()),
        Tool("bdk_orchestrations_terminate", "Terminates an orchestration when the operations toolset is enabled.", OrchestrationReasonSchema()),
        Tool("bdk_orchestrations_repair", "Runs an orchestration repair operation when the operations toolset is enabled.", OrchestrationRepairSchema()),
        Tool("bdk_orchestrations_purge", "Purges retained orchestration data when the admin toolset is enabled and confirmation is supplied.", OrchestrationPurgeSchema()),
        Tool("bdk_docs_search", "Searches bounded official DevKit documentation.", new { type = "object", additionalProperties = false, required = new[] { "query" }, properties = new { query = String("Search query."), limit = Integer("Maximum result count.") } }),
        Tool("bdk_docs_get", "Gets bounded DevKit documentation content by source path or URL.", new { type = "object", additionalProperties = false, required = new[] { "source" }, properties = new { source = String("Documentation source path or URL."), maxChars = Integer("Maximum characters to return.") } }),
        Tool("bdk_api_search", "Searches bounded official DevKit API reference symbols.", ApiSearchSchema()),
        Tool("bdk_api_get", "Gets bounded DevKit API reference content by symbol uid.", new { type = "object", additionalProperties = false, required = new[] { "uid" }, properties = new { uid = String("DocFX API reference symbol uid."), maxChars = Integer("Maximum characters to return.") } }),
        Tool("bdk_guidance_list", "Lists curated DevKit agentic coding guidance topics.", EmptySchema),
        Tool("bdk_guidance_get", "Gets curated DevKit agentic coding guidance. Use when the user asks for guidance, how to implement, add, create or build DevKit jobs, messaging, queueing, orchestration, pipelines, dashboards or project dashboard pages.", GuidanceSchema()),
        Tool("bdk_project_summary", "Summarizes the selected runtime, advertised project shape, MCP capabilities, and suggested next calls.", EmptySchema),
        Tool("bdk_project_operations", "Lists project-owned MCP operations advertised by the selected runtime.", EmptySchema),
        Tool("bdk_project_call", "Invokes a project-owned MCP operation by name.", new { type = "object", additionalProperties = false, required = new[] { "operation" }, properties = new { operation = String("Project operation name."), toolset = String("Required toolset: diagnostics, operations, or admin. Defaults to diagnostics."), arguments = new { type = "object", additionalProperties = true } } })
    ];

    /// <summary>
    /// Checks whether the catalog contains a tool.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <returns><see langword="true" /> when found.</returns>
    public bool Contains(string name)
        => this.Tools.Any(tool => string.Equals(tool.Name, name, StringComparison.OrdinalIgnoreCase));

    private static McpToolDefinition Tool(string name, string description, object inputSchema)
        => new(name, description, inputSchema);

    private static object String(string description)
        => new { type = "string", description };

    private static object Bool(string description)
        => new { type = "boolean", description };

    private static object Integer(string description)
        => new { type = "integer", description, minimum = 1 };

    private static object DateTime(string description)
        => new { type = "string", format = "date-time", description };

    private static object Guid(string description)
        => new { type = "string", format = "uuid", description };

    private static object StringArray(string description)
        => new { type = "array", description, items = new { type = "string" } };

    private static object FreeObject(string description)
        => new { type = "object", description, additionalProperties = true };

    private static object Enum(string description, params string[] values)
        => new { type = "string", description, @enum = values };

    private static object TakeSchema()
        => new { type = "object", additionalProperties = false, properties = new { take = Integer("Maximum number of rows to return.") } };

    private static object GuidanceSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                topic = Enum(
                    "Optional exact guidance topic when known.",
                    "jobs",
                    "messaging",
                    "queueing",
                    "orchestration",
                    "pipelines",
                    "caching",
                    "mapping",
                    "serialization",
                    "utilities",
                    "commands_queries",
                    "application_events",
                    "activeentity",
                    "domain_events",
                    "repositories",
                    "specifications",
                    "domain",
                    "filtering",
                    "modules",
                    "requester_notifier",
                    "results",
                    "rules",
                    "startuptasks",
                    "document_storage",
                    "file_storage",
                    "monitoring",
                    "dashboard",
                    "project_dashboard_page"),
                query = String("Natural-language guidance request, for example 'how to implement a new job that triggers an orchestration'.")
            }
        };

    private static object ApiSearchSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "query" },
            properties = new
            {
                query = String("Type, member, namespace or feature keyword to search for."),
                topic = String("Optional inferred topic filter, for example results, jobs, queueing or repositories."),
                kind = String("Optional DocFX kind filter, for example Class, Interface, Method, Property, Struct or Enum."),
                @namespace = String("Optional namespace filter."),
                limit = Integer("Maximum result count.")
            }
        };

    private static object GuidIdSchema(string description = "Entity id.")
        => new { type = "object", additionalProperties = false, required = new[] { "id" }, properties = new { id = Guid(description) } };

    private static object Id64Schema(string description)
        => new { type = "object", additionalProperties = false, required = new[] { "id" }, properties = new { id = Integer(description) } };

    private static object TypeSchema()
        => new { type = "object", additionalProperties = false, required = new[] { "type" }, properties = new { type = String("Message type name.") } };

    private static object QueueNameSchema()
        => new { type = "object", additionalProperties = false, required = new[] { "queueName" }, properties = new { queueName = String("Logical queue name.") } };

    private static object CorrelationSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "correlationId" },
            properties = new
            {
                correlationId = String("Correlation id to inspect."),
                startTime = DateTime("Inclusive lower timestamp bound."),
                endTime = DateTime("Inclusive upper timestamp bound."),
                take = Integer("Maximum number of log entries to return."),
                continuationToken = String("Continuation token from a previous response.")
            }
        };

    private static object LogQuerySchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                startTime = DateTime("Inclusive lower timestamp bound."),
                endTime = DateTime("Inclusive upper timestamp bound."),
                ageSeconds = Integer("Relative lookback window in seconds."),
                level = Enum("Log level filter.", "Trace", "Debug", "Information", "Warning", "Error", "Critical"),
                traceId = String("Trace id filter."),
                spanId = String("Span id filter."),
                correlationId = String("Correlation id filter."),
                logKey = String("Log key filter."),
                moduleName = String("Module name filter."),
                shortTypeName = String("Short type name filter."),
                searchText = String("Full-text search filter."),
                afterId = Integer("Return entries after this log id."),
                take = Integer("Maximum number of log entries to return."),
                pageSize = Integer("Maximum number of log entries to return."),
                continuationToken = String("Continuation token from a previous response.")
            }
        };

    private static object LogPurgeSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "olderThan", "confirm", "confirmation" },
            properties = new
            {
                olderThan = DateTime("Purge entries older than this timestamp."),
                archive = Bool("Archive before cleanup when supported."),
                batchSize = Integer("Cleanup batch size."),
                confirm = Bool("Must be true for admin purge operations."),
                confirmation = String("Must equal 'purge logs'.")
            }
        };

    private static object MessageListSchema(bool includeQueueName)
        => includeQueueName
            ? new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    status = String("Message status filter."),
                    type = String("Message type filter."),
                    queueName = String("Logical queue name filter."),
                    messageId = String("Application message id filter."),
                    lockedBy = String("Lease owner filter."),
                    isArchived = Bool("Archive state filter."),
                    createdAfter = DateTime("Inclusive created-after timestamp."),
                    createdBefore = DateTime("Inclusive created-before timestamp."),
                    take = Integer("Maximum number of messages to return.")
                }
            }
            : new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    status = String("Message status filter."),
                    type = String("Message type filter."),
                    messageId = String("Application message id filter."),
                    lockedBy = String("Lease owner filter."),
                    isArchived = Bool("Archive state filter."),
                    createdAfter = DateTime("Inclusive created-after timestamp."),
                    createdBefore = DateTime("Inclusive created-before timestamp."),
                    includeHandlers = Bool("Include handler rows."),
                    take = Integer("Maximum number of messages to return.")
                }
            };

    private static object MessageRetrySchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "id" },
            properties = new
            {
                id = Guid("Message id."),
                handlerType = String("Optional handler type to retry instead of the whole message.")
            }
        };

    private static object QueueMessageDetailsSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "id" },
            properties = new
            {
                id = Guid("Queue message id."),
                includeContent = Bool("Include persisted queue message content.")
            }
        };

    private static object RetainedMessagesPurgeSchema(string confirmation)
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "confirm", "confirmation" },
            properties = new
            {
                olderThan = DateTime("Purge messages older than this timestamp."),
                statuses = StringArray("Message statuses to purge."),
                isArchived = Bool("Archive state filter."),
                confirm = Bool("Must be true for admin purge operations."),
                confirmation = String($"Must equal '{confirmation}'.")
            }
        };

    private static object JobsListSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                jobName = String("Job name filter."),
                name = String("Alias for jobName."),
                group = String("Job group filter."),
                module = String("Module filter."),
                enabled = Bool("Enabled state filter."),
                paused = Bool("Paused state filter."),
                take = Integer("Maximum number of jobs to return."),
                sortBy = String("Sort field."),
                sortDescending = Bool("Sort descending.")
            }
        };

    private static object JobNameSchema()
        => new { type = "object", additionalProperties = false, required = new[] { "jobName" }, properties = new { jobName = String("Job name."), take = Integer("Maximum number of recent rows to return.") } };

    private static object JobRunsSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                jobName = String("Job name filter."),
                name = String("Alias for jobName."),
                triggerName = String("Trigger name filter."),
                trigger = String("Alias for triggerName."),
                triggerType = String("Trigger type filter."),
                statuses = StringArray("Execution status filters."),
                schedulerInstanceId = String("Scheduler instance id filter."),
                correlationId = String("Correlation id filter."),
                idempotencyKey = String("Idempotency key filter."),
                startedFrom = DateTime("Inclusive start timestamp."),
                startedTo = DateTime("Inclusive end timestamp."),
                from = DateTime("Alias for startedFrom."),
                to = DateTime("Alias for startedTo."),
                completedFrom = DateTime("Inclusive completed-from timestamp."),
                completedTo = DateTime("Inclusive completed-to timestamp."),
                take = Integer("Maximum number of executions to return."),
                sortBy = String("Sort field."),
                sortDescending = Bool("Sort descending.")
            }
        };

    private static object JobStatsSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                jobName = String("Job name filter."),
                name = String("Alias for jobName."),
                triggerName = String("Trigger name filter."),
                trigger = String("Alias for triggerName."),
                triggerType = String("Trigger type filter."),
                occurrenceStatuses = StringArray("Occurrence status filters."),
                executionStatuses = StringArray("Execution status filters."),
                schedulerInstanceId = String("Scheduler instance id filter."),
                dueFrom = DateTime("Inclusive due-from timestamp."),
                dueTo = DateTime("Inclusive due-to timestamp."),
                from = DateTime("Alias for dueFrom."),
                to = DateTime("Alias for dueTo."),
                completedFrom = DateTime("Inclusive completed-from timestamp."),
                completedTo = DateTime("Inclusive completed-to timestamp.")
            }
        };

    private static object JobTriggerSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "jobName" },
            properties = new
            {
                jobName = String("Job name."),
                data = FreeObject("Optional dispatch data."),
                wait = Bool("Wait for completion."),
                timeout = Integer("Wait timeout in seconds.")
            }
        };

    private static object JobReasonSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "jobName" },
            properties = new
            {
                jobName = String("Job name."),
                reason = String("Optional operator reason.")
            }
        };

    private static object JobInterruptSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                occurrenceId = Guid("Running occurrence id. If omitted, jobName is used to locate the latest running occurrence."),
                jobName = String("Job name used when occurrenceId is omitted."),
                reason = String("Optional operator reason.")
            }
        };

    private static object JobRunInvestigationSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                occurrenceId = Guid("Occurrence id filter."),
                executionId = Guid("Execution id filter."),
                jobName = String("Job name filter."),
                name = String("Alias for jobName."),
                take = Integer("Maximum number of rows to return.")
            }
        };

    private static object JobPurgeSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "confirm", "confirmation" },
            properties = new
            {
                jobName = String("Job name filter."),
                name = String("Alias for jobName."),
                triggerName = String("Trigger name filter."),
                trigger = String("Alias for triggerName."),
                olderThan = DateTime("Purge occurrences older than this timestamp."),
                isArchived = Bool("Archive state filter."),
                statuses = StringArray("Occurrence status filters."),
                dryRun = Bool("Calculate purge report without deleting."),
                batchSize = Integer("Purge batch size."),
                confirm = Bool("Must be true for admin purge operations."),
                confirmation = String("Must equal 'purge job runs'.")
            }
        };

    private static object OrchestrationListSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                orchestrationName = String("Orchestration name filter."),
                statuses = StringArray("Runtime status filters."),
                states = StringArray("State filters."),
                correlationId = String("Correlation id filter."),
                concurrencyKey = String("Concurrency key filter."),
                startedFrom = DateTime("Inclusive started-from timestamp."),
                startedTo = DateTime("Inclusive started-to timestamp."),
                completedFrom = DateTime("Inclusive completed-from timestamp."),
                completedTo = DateTime("Inclusive completed-to timestamp."),
                skip = Integer("Rows to skip."),
                take = Integer("Maximum number of instances to return."),
                sortBy = String("Sort field."),
                sortDescending = Bool("Sort descending.")
            }
        };

    private static object InstanceIdSchema()
        => new { type = "object", additionalProperties = false, required = new[] { "instanceId" }, properties = new { instanceId = Guid("Orchestration instance id."), id = Guid("Alias for instanceId.") } };

    private static object OrchestrationSignalSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "instanceId", "signalName" },
            properties = new
            {
                instanceId = Guid("Orchestration instance id."),
                id = Guid("Alias for instanceId."),
                signalName = String("Signal name."),
                payload = FreeObject("Signal payload."),
                idempotencyKey = String("Optional idempotency key.")
            }
        };

    private static object OrchestrationReasonSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "instanceId" },
            properties = new
            {
                instanceId = Guid("Orchestration instance id."),
                id = Guid("Alias for instanceId."),
                reason = String("Optional operator reason.")
            }
        };

    private static object OrchestrationRepairSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "instanceId", "action" },
            properties = new
            {
                instanceId = Guid("Orchestration instance id."),
                id = Guid("Alias for instanceId."),
                action = Enum("Repair action.", "archive", "releaseLease", "requeueTimers")
            }
        };

    private static object OrchestrationPurgeSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "confirm", "confirmation" },
            properties = new
            {
                olderThan = DateTime("Purge orchestration data older than this timestamp."),
                statuses = StringArray("Runtime status filters."),
                isArchived = Bool("Archive state filter."),
                confirm = Bool("Must be true for admin purge operations."),
                confirmation = String("Must equal 'purge orchestrations'.")
            }
        };
}