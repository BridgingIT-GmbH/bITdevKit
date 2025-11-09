// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring validation behaviors in the Active Entity configuration.
/// </summary>
public static class ActiveEntityConfiguratorExtensions
{
    /// <summary>
    /// Adds a logging behavior for the entity.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <param name="configurator">The entity configurator.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// configurator.AddLoggingBehavior&lt;Customer, CustomerId&gt;();
    /// </code>
    /// </example>
    public static ActiveEntityConfigurator<TEntity, TId> AddLoggingBehavior<TEntity, TId>(
        this ActiveEntityConfigurator<TEntity, TId> configurator)
        where TEntity : class, IEntity
    {
        configurator.AddBehaviorType(typeof(ActiveEntityLoggingBehavior<TEntity>));
        return configurator;
    }

    /// <summary>
    /// Adds a domain event publishing behavior for the entity with optional configuration options.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <param name="configurator">The entity configurator.</param>
    /// <param name="options">Optional configuration options for the behavior.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// configurator.AddDomainEventPublishingBehavior&lt;Order, OrderId&gt;(
    ///     new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false });
    /// </code>
    /// </example>
    public static ActiveEntityConfigurator<TEntity, TId> AddDomainEventPublishingBehavior<TEntity, TId>(
        this ActiveEntityConfigurator<TEntity, TId> configurator,
        ActiveEntityDomainEventPublishingBehaviorOptions options = null)
        where TEntity : ActiveEntity<TEntity, TId>
    {
        configurator.AddBehaviorType(
            typeof(ActiveEntityDomainEventPublishingBehavior<TEntity, TId>), options ?? new ActiveEntityDomainEventPublishingBehaviorOptions());
        return configurator;
    }

    /// <summary>
    /// Adds an audit state behavior for the entity with optional configuration options.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <param name="configurator">The entity configurator.</param>
    /// <param name="configureOptions">Optional configuration options for the behavior.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// configurator.AddAuditStateBehavior&lt;Order, OrderId&gt;(
    ///     o => o.EnableSoftDelete(false));
    /// </code>
    /// </example>
    public static ActiveEntityConfigurator<TEntity, TId> AddAuditStateBehavior<TEntity, TId>(
        this ActiveEntityConfigurator<TEntity, TId> configurator,
        Action<ActiveEntityAuditStateBehaviorOptions> configureOptions = null)
        where TEntity : class, IEntity, IAuditable
    {
        var options = new ActiveEntityAuditStateBehaviorOptions();
        configureOptions?.Invoke(options);

        configurator.AddBehaviorType(
            typeof(ActiveEntityAuditStateBehavior<TEntity>), options);
        return configurator;
    }

    /// <summary>
    /// Registers a FluentValidation validator for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <typeparam name="TValidator">The validator type implementing <see cref="IValidator{TEntity}"/>.</typeparam>
    /// <param name="configurator">The entity configurator instance.</param>
    /// <param name="configureOptions">Optional action to configure validation behavior options.</param>
    /// <returns>The configurator instance for fluent configuration.</returns>
    /// <example>
    /// <code>
    /// services.AddActiveEntity(cfg =>
    /// {
    ///     cfg.For&lt;Customer, CustomerId>()
    ///         .UseEntityFrameworkProvider(b => b.Context&lt;ActiveEntityDbContext>())
    ///         .AddValidationBehavior()
    ///         .AddValidatorBehavior&lt;Customer, CustomerId, BasicCustomerValidator>(o => o.OnInsert()) // Validate required fields on insert
    ///         .AddValidatorBehavior&lt;Customer, CustomerId, BusinessCustomerValidator>(o => o.OnUpdate()) // Business rules on update
    ///         .AddValidatorBehavior&lt;Customer, CustomerId, DeleteCustomerValidator>(o => o.OnDelete()); // Deletion rules
    /// });
    /// </code>
    /// <para>
    /// Example validator for insert:
    /// <code>
    /// public class BasicCustomerValidator : AbstractValidator&lt;Customer>
    /// {
    ///     public BasicCustomerValidator()
    ///     {
    ///         RuleFor(c => c.FirstName).NotEmpty().MaximumLength(50);
    ///         RuleFor(c => c.LastName).NotEmpty().MaximumLength(50);
    ///         RuleFor(c => c.Email.Value).EmailAddress();
    ///     }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Example validator for update:
    /// <code>
    /// public class BusinessCustomerValidator : AbstractValidator&lt;Customer>
    /// {
    ///     public BusinessCustomerValidator()
    ///     {
    ///         RuleFor(c => c.LastName).NotEmpty().MaximumLength(100);
    ///     }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Example validator with async rule for delete:
    /// <code>
    /// public class DeleteCustomerValidator : AbstractValidator&lt;Customer>
    /// {
    ///     public DeleteCustomerValidator()
    ///     {
    ///         RuleFor(c => c.Id).MustAsync(async (id, ct) =>
    ///         {
    ///             var hasOrders = await Order.ExistsAsync(o => o.CustomerId == id && o.Status != OrderStatus.Completed, ct);
    ///             return !hasOrders.Value;
    ///         }).WithMessage("Cannot delete customer with active orders.");
    ///     }
    /// }
    /// </code>
    /// </para>
    public static ActiveEntityConfigurator<TEntity, TId> AddValidatorBehavior<TEntity, TId, TValidator>(
        this ActiveEntityConfigurator<TEntity, TId> configurator,
        Action<ActiveEntityValidatorBehaviorOptions> configureOptions = null)
        where TEntity : class, IEntity
        where TValidator : class, IValidator<TEntity>
    {
        var options = new ActiveEntityValidatorBehaviorOptions();
        configureOptions?.Invoke(options);
        configurator.Services.AddSingleton(sp =>
            new ValidatorRegistration<TEntity>(
                sp.GetService<TValidator>() ?? ActivatorUtilities.CreateInstance<TValidator>(sp),
                options.ApplyOn));

        if (!configurator.HasBehaviorType(typeof(ActiveEntityValidationBehavior<TEntity, TId>)))
        {
            configurator.AddBehaviorType(typeof(ActiveEntityValidationBehavior<TEntity, TId>));
        }

        return configurator;
    }

    /// <summary>
    /// Adds an annotations-based validation behavior for the specified entity type, using DataAnnotations attributes.
    /// This method should be called once per entity type to register the annotations validation behavior in the pipeline.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <param name="configurator">The entity configurator instance.</param>
    /// <param name="configureOptions">Optional action to configure validation behavior options.</param>
    /// <returns>The configurator instance for fluent configuration.</returns>
    /// <example>
    /// <code>
    /// services.AddActiveEntity(cfg =>
    /// {
    ///     cfg.For&lt;Customer, CustomerId>()
    ///         .UseEntityFrameworkProvider(b => b.Context&lt;ActiveEntityDbContext>())
    ///         .AddValidationBehavior()
    ///         .AddValidatorBehavior&lt;Customer, CustomerId, BusinessCustomerValidator>(o => o.OnUpdate())
    ///         .AddAnnotationsValidator(o => o.OnInsert()); // Annotations validator for insert
    /// });
    /// </code>
    /// <para>
    /// Example entity with DataAnnotations:
    /// <code>
    /// public class Customer : ActiveEntity&lt;Customer, CustomerId>
    /// {
    ///     [Required]
    ///     [StringLength(50)]
    ///     public string FirstName { get; set; }
    ///
    ///     [Required]
    ///     [StringLength(50)]
    ///     public string LastName { get; set; }
    ///
    ///     [Required]
    ///     [EmailAddress]
    ///     public EmailAddressStub Email { get; set; }
    ///
    ///     [Compare("Email")]
    ///     public EmailAddressStub ConfirmEmail { get; set; }
    ///
    ///     public string Title { get; set; }
    /// }
    /// </code>
    /// </para>
    public static ActiveEntityConfigurator<TEntity, TId> AddAnnotationsValidator<TEntity, TId>(
        this ActiveEntityConfigurator<TEntity, TId> configurator,
        Action<ActiveEntityValidatorBehaviorOptions> configureOptions = null)
        where TEntity : ActiveEntity<TEntity, TId>
    {
        var options = new ActiveEntityValidatorBehaviorOptions();
        configureOptions?.Invoke(options);
        configurator.AddBehaviorType(typeof(ActiveEntityAnnotationsValidationBehavior<TEntity, TId>), options);
        return configurator;
    }
}