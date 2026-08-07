// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;

using System.ComponentModel;
using System.Reflection;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Invokes registered ChangeHistory services for dashboard pages without issuing HTTP calls to the same application.
/// </summary>
/// <example>
/// <code>
/// var result = await ChangeHistoryDashboardInvoker.FindAllAsync(context, descriptor, query, cancellationToken);
/// </code>
/// </example>
public static class ChangeHistoryDashboardInvoker
{
    private const string FindAllMethodName = "FindAllAsync";
    private const string FindAllChangeSetsMethodName = "FindAllChangeSetsAsync";
    private const string FindOneChangeSetMethodName = "FindOneChangeSetAsync";
    private const string RestoreMethodName = "RestoreAsync";

    /// <summary>
    /// Queries ChangeHistory rows for the selected dashboard descriptor.
    /// </summary>
    /// <param name="services">The scoped service provider.</param>
    /// <param name="descriptor">The ChangeHistory registration descriptor.</param>
    /// <param name="query">The query filters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A ChangeHistory row query result.</returns>
    public static Task<Result<ChangeHistoryFindAllResult>> FindAllAsync(
        IServiceProvider services,
        ChangeHistoryDashboardDescriptor descriptor,
        ChangeHistoryFindAllQuery query,
        CancellationToken cancellationToken = default)
        => InvokeAsync<ChangeHistoryFindAllResult>(
            services,
            descriptor,
            FindAllMethodName,
            query,
            cancellationToken);

    /// <summary>
    /// Queries grouped ChangeHistory change sets for the selected dashboard descriptor.
    /// </summary>
    /// <param name="services">The scoped service provider.</param>
    /// <param name="descriptor">The ChangeHistory registration descriptor.</param>
    /// <param name="query">The query filters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A grouped ChangeHistory query result.</returns>
    public static Task<Result<ChangeHistoryFindAllChangeSetsResult>> FindAllChangeSetsAsync(
        IServiceProvider services,
        ChangeHistoryDashboardDescriptor descriptor,
        ChangeHistoryFindAllQuery query,
        CancellationToken cancellationToken = default)
        => InvokeAsync<ChangeHistoryFindAllChangeSetsResult>(
            services,
            descriptor,
            FindAllChangeSetsMethodName,
            query,
            cancellationToken);

    /// <summary>
    /// Queries one grouped ChangeHistory change set for the selected dashboard descriptor.
    /// </summary>
    /// <param name="services">The scoped service provider.</param>
    /// <param name="descriptor">The ChangeHistory registration descriptor.</param>
    /// <param name="query">The change set query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A grouped ChangeHistory change set result.</returns>
    public static Task<Result<ChangeHistoryChangeSetRecord>> FindOneChangeSetAsync(
        IServiceProvider services,
        ChangeHistoryDashboardDescriptor descriptor,
        ChangeHistoryFindOneChangeSetQuery query,
        CancellationToken cancellationToken = default)
        => InvokeAsync<ChangeHistoryChangeSetRecord>(
            services,
            descriptor,
            FindOneChangeSetMethodName,
            query,
            cancellationToken);

    /// <summary>
    /// Restores one ChangeHistory change set for the selected dashboard descriptor.
    /// </summary>
    /// <param name="services">The scoped service provider.</param>
    /// <param name="descriptor">The ChangeHistory registration descriptor.</param>
    /// <param name="entityId">The entity id as entered in the dashboard.</param>
    /// <param name="changeSetId">The change set id to restore.</param>
    /// <param name="reason">The restore reason.</param>
    /// <param name="expectedConcurrencyVersion">The optional expected concurrency version.</param>
    /// <param name="restoreMode">The restore mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A ChangeHistory restore result.</returns>
    public static Task<Result<ChangeHistoryRestoreResult>> RestoreAsync(
        IServiceProvider services,
        ChangeHistoryDashboardDescriptor descriptor,
        string entityId,
        Guid changeSetId,
        string reason,
        Guid? expectedConcurrencyVersion,
        ChangeHistoryRestoreMode restoreMode,
        CancellationToken cancellationToken = default)
    {
        var commandType = typeof(ChangeHistoryRestoreCommand<>).MakeGenericType(descriptor.EntityType);
        var command = Activator.CreateInstance(
            commandType,
            ConvertEntityId(descriptor.EntityType, entityId),
            changeSetId,
            reason,
            expectedConcurrencyVersion,
            restoreMode);

        return InvokeAsync<ChangeHistoryRestoreResult>(
            services,
            descriptor,
            RestoreMethodName,
            command,
            cancellationToken);
    }

    private static async Task<Result<TResult>> InvokeAsync<TResult>(
        IServiceProvider services,
        ChangeHistoryDashboardDescriptor descriptor,
        string methodName,
        object request,
        CancellationToken cancellationToken)
    {
        if (descriptor is null)
        {
            return Result<TResult>.Failure().WithError(new ValidationError("ChangeHistory registration is required."));
        }

        var serviceType = typeof(IChangeHistoryService<,>).MakeGenericType(descriptor.EntityType, descriptor.ContextType);
        var service = services.GetService(serviceType);
        if (service is null)
        {
            return Result<TResult>.Failure().WithError(new Error($"ChangeHistory service for {descriptor.EntityTypeName} is not registered."));
        }

        var method = serviceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (method is null)
        {
            return Result<TResult>.Failure().WithError(new Error($"ChangeHistory service method '{methodName}' was not found."));
        }

        var task = method.Invoke(service, [request, cancellationToken]);
        if (task is null)
        {
            return Result<TResult>.Failure().WithError(new Error($"ChangeHistory service method '{methodName}' did not return a result."));
        }

        return await (Task<Result<TResult>>)task;
    }

    private static object ConvertEntityId(Type entityType, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Entity id is required.", nameof(value));
        }

        var idType = entityType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntity<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault() ?? typeof(string);

        if (idType == typeof(string))
        {
            return value;
        }

        if (idType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        var createStringMethod = idType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        if (createStringMethod is not null)
        {
            return createStringMethod.Invoke(null, [value]);
        }

        var createGuidMethod = idType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [typeof(Guid)]);
        if (createGuidMethod is not null)
        {
            return createGuidMethod.Invoke(null, [Guid.Parse(value)]);
        }

        var parseMethod = idType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        if (parseMethod is not null)
        {
            return parseMethod.Invoke(null, [value]);
        }

        var converter = TypeDescriptor.GetConverter(idType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(value);
        }

        return Convert.ChangeType(value, idType);
    }
}
