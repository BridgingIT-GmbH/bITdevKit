namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.Extensions.Configuration;

/// <summary>
/// The in-memory configuration source that carries the managed values of an active development instance.
/// </summary>
/// <remarks>
/// The source is appended by <c>AddDevAppInstance</c> and moved to the end of the source list during <c>Build()</c>.
/// Provider registrations such as <c>AddSqlServerDbContext</c> can add managed values later through <see cref="Set"/>.
/// </remarks>
/// <example>
/// <code>
/// var source = builder.Configuration.Sources.OfType&lt;DevAppInstanceConfigurationSource&gt;().SingleOrDefault();
/// </code>
/// </example>
public sealed class DevAppInstanceConfigurationSource : IConfigurationSource
{
    private readonly object sync = new();
    private readonly Dictionary<string, string> values;
    private Provider provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevAppInstanceConfigurationSource"/> class.
    /// </summary>
    /// <param name="values">The managed values keyed by configuration key.</param>
    public DevAppInstanceConfigurationSource(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        this.values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a snapshot of the managed values keyed by configuration key. Values may contain secrets and are never logged.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values
    {
        get
        {
            lock (this.sync)
            {
                return new Dictionary<string, string>(this.values, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        lock (this.sync)
        {
            return this.provider = new Provider(this);
        }
    }

    /// <summary>
    /// Adds or replaces managed values. The values are visible to configuration reads at once.
    /// </summary>
    /// <param name="managedValues">The managed values keyed by configuration key.</param>
    /// <example>
    /// <code>
    /// source.Set(new Dictionary&lt;string, string&gt; { ["ConnectionStrings:Default"] = instanceConnectionString });
    /// </code>
    /// </example>
    public void Set(IReadOnlyDictionary<string, string> managedValues)
    {
        ArgumentNullException.ThrowIfNull(managedValues);

        lock (this.sync)
        {
            foreach (var (key, value) in managedValues)
            {
                this.values[key] = value;
                this.provider?.Set(key, value);
            }
        }
    }

    private sealed class Provider(DevAppInstanceConfigurationSource source) : ConfigurationProvider
    {
        public override void Load()
        {
            lock (source.sync)
            {
                this.Data = new Dictionary<string, string>(source.values, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
