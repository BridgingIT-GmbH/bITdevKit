// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;

/// <summary>Configures masking, hiding, references and write opt-ins for one entity type in the Entities dashboard.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <example>
/// <code>
/// services.AddEntitiesDashboard(options => options
///     .Entity&lt;Customer&gt;(e => e
///         .Mask(c => c.Email)
///         .Mask(c => c.Address.Phone, "***")
///         .Hide(c => c.TaxNumber)
///         .Reference(c => c.RegionId).To&lt;Region&gt;()));
/// </code>
/// </example>
public sealed class EntitiesDashboardEntityBuilder<TEntity>
    where TEntity : class, IEntity
{
    private readonly EntitiesDashboardEntityConfiguration configuration;

    internal EntitiesDashboardEntityBuilder(EntitiesDashboardEntityConfiguration configuration)
    {
        this.configuration = configuration;
    }

    /// <summary>Shows the property value as mask text in the list, detail panel and edit form.</summary>
    /// <param name="property">A public top-level property or one level of a value-object property.</param>
    /// <param name="maskText">The text shown instead of the value.</param>
    /// <returns>The same builder.</returns>
    /// <example>
    /// <code>
    /// options.Entity&lt;Customer&gt;(e => e.Mask(c => c.Email).Mask(c => c.Address.Phone, "***"));
    /// </code>
    /// </example>
    public EntitiesDashboardEntityBuilder<TEntity> Mask(
        Expression<Func<TEntity, object>> property,
        string maskText = "••••••")
    {
        return this.AddRule(GetPath(property), EntitiesDashboardPropertyMode.Mask, maskText);
    }

    /// <summary>Omits the property from columns, forms and payloads.</summary>
    /// <param name="property">A public top-level property or one level of a value-object property.</param>
    /// <returns>The same builder.</returns>
    /// <example>
    /// <code>
    /// options.Entity&lt;Customer&gt;(e => e.Hide(c => c.TaxNumber));
    /// </code>
    /// </example>
    public EntitiesDashboardEntityBuilder<TEntity> Hide(Expression<Func<TEntity, object>> property)
    {
        return this.AddRule(GetPath(property), EntitiesDashboardPropertyMode.Hide, null);
    }

    /// <summary>Omits the property, addressed by public property name or dotted path, from columns, forms and payloads.</summary>
    /// <param name="propertyPath">The property name or dotted path (<c>Address.Phone</c>).</param>
    /// <returns>The same builder.</returns>
    /// <example>
    /// <code>
    /// options.Entity&lt;Customer&gt;(e => e.Hide("InternalNotes"));
    /// </code>
    /// </example>
    public EntitiesDashboardEntityBuilder<TEntity> Hide(string propertyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);

        return this.AddRule(propertyPath.Trim(), EntitiesDashboardPropertyMode.Hide, null);
    }

    /// <summary>Excludes the entity type from the dashboard (same as <c>options.Exclude&lt;TEntity&gt;()</c>).</summary>
    /// <returns>The same builder.</returns>
    /// <example>
    /// <code>
    /// options.Entity&lt;AuditLogEntry&gt;(e => e.Exclude());
    /// </code>
    /// </example>
    public EntitiesDashboardEntityBuilder<TEntity> Exclude()
    {
        this.configuration.Excluded = true;

        return this;
    }

    /// <summary>Allows the dashboard to use non-public setters and a non-public parameterless constructor for this entity.</summary>
    /// <remarks>
    /// Reflection writes bypass domain invariants in setters and factory methods. Enable this only for entities
    /// where that risk is acceptable; repository or ActiveEntity behaviors still run on the save call.
    /// </remarks>
    /// <returns>The same builder.</returns>
    /// <example>
    /// <code>
    /// options.Entity&lt;Customer&gt;(e => e.AllowNonPublicSetters());
    /// </code>
    /// </example>
    public EntitiesDashboardEntityBuilder<TEntity> AllowNonPublicSetters()
    {
        this.configuration.AllowNonPublicSetters = true;

        return this;
    }

    /// <summary>Starts an explicit related-entity reference configuration for a top-level property.</summary>
    /// <param name="property">The top-level property holding the related entity id.</param>
    /// <returns>A builder to choose the target entity or suppress detection.</returns>
    /// <example>
    /// <code>
    /// options.Entity&lt;Order&gt;(e => e
    ///     .Reference(o => o.CustomerId).To&lt;Customer&gt;()
    ///     .Reference(o => o.RegionId).None());
    /// </code>
    /// </example>
    public EntitiesDashboardReferenceBuilder<TEntity> Reference(Expression<Func<TEntity, object>> property)
    {
        return new EntitiesDashboardReferenceBuilder<TEntity>(this, this.configuration, GetPath(property));
    }

    private EntitiesDashboardEntityBuilder<TEntity> AddRule(
        string path,
        EntitiesDashboardPropertyMode mode,
        string maskText)
    {
        this.configuration.Properties[path] = new EntitiesDashboardPropertyRule(
            path,
            mode,
            mode == EntitiesDashboardPropertyMode.Mask ? maskText ?? "••••••" : null);

        return this;
    }

    private static string GetPath(Expression<Func<TEntity, object>> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var body = property.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression { Member: System.Reflection.PropertyInfo } member)
        {
            if (member.Expression is ParameterExpression)
            {
                return member.Member.Name;
            }

            if (member.Expression is MemberExpression { Member: System.Reflection.PropertyInfo } parent
                && parent.Expression is ParameterExpression)
            {
                return $"{parent.Member.Name}.{member.Member.Name}";
            }
        }

        throw new ArgumentException(
            $"Expression '{property}' must select a public property (x => x.Prop) or one level of a value-object property (x => x.Prop.Member).",
            nameof(property));
    }
}

/// <summary>Chooses the target of an explicit related-entity reference.</summary>
/// <typeparam name="TEntity">The entity type that owns the property.</typeparam>
/// <example>
/// <code>
/// options.Entity&lt;Order&gt;(e => e.Reference(o => o.CustomerId).To&lt;Customer&gt;());
/// </code>
/// </example>
public sealed class EntitiesDashboardReferenceBuilder<TEntity>
    where TEntity : class, IEntity
{
    private readonly EntitiesDashboardEntityBuilder<TEntity> entityBuilder;
    private readonly EntitiesDashboardEntityConfiguration configuration;
    private readonly string path;

    internal EntitiesDashboardReferenceBuilder(
        EntitiesDashboardEntityBuilder<TEntity> entityBuilder,
        EntitiesDashboardEntityConfiguration configuration,
        string path)
    {
        this.entityBuilder = entityBuilder;
        this.configuration = configuration;
        this.path = path;
    }

    /// <summary>Links the property to the given entity type.</summary>
    /// <typeparam name="TTarget">The referenced entity type.</typeparam>
    /// <returns>The entity builder.</returns>
    /// <example>
    /// <code>
    /// options.Entity&lt;Order&gt;(e => e.Reference(o => o.CustomerId).To&lt;Customer&gt;());
    /// </code>
    /// </example>
    public EntitiesDashboardEntityBuilder<TEntity> To<TTarget>()
        where TTarget : class, IEntity
    {
        this.configuration.References[this.path] = typeof(TTarget);

        return this.entityBuilder;
    }

    /// <summary>Suppresses automatic reference detection for the property.</summary>
    /// <returns>The entity builder.</returns>
    /// <example>
    /// <code>
    /// options.Entity&lt;Order&gt;(e => e.Reference(o => o.RegionId).None());
    /// </code>
    /// </example>
    public EntitiesDashboardEntityBuilder<TEntity> None()
    {
        this.configuration.References[this.path] = null;

        return this.entityBuilder;
    }
}
