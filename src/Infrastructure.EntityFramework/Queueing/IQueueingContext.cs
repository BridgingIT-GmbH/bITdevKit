namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Defines the Entity Framework capability contract required by the SQL-backed queue broker.
/// </summary>
/// <remarks>
/// A host <see cref="DbContext"/> opts into durable queueing by implementing this interface and exposing the queue message set.
/// </remarks>
/// <example>
/// <code>
/// public class AppDbContext : DbContext, IQueueingContext
/// {
///     public DbSet&lt;QueueMessage&gt; QueueMessages { get; set; }
/// }
/// </code>
/// </example>
public interface IQueueingContext
{
    /// <summary>
    /// Gets or sets the durable queue messages persisted for broker processing.
    /// </summary>
    DbSet<QueueMessage> QueueMessages { get; set; }
}