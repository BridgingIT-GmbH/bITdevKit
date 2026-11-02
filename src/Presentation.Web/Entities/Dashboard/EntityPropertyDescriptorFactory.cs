// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Linq.Expressions;
using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>Builds the property descriptors and capabilities of an entity type.</summary>
internal static class EntityPropertyDescriptorFactory
{
    private const string DEFAULTMASK = "••••••";

    /// <summary>Builds the dashboard entity for a discovered type.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="idType">The id type.</param>
    /// <param name="configuration">The per-entity configuration, or null.</param>
    /// <param name="sources">The sources, repository first.</param>
    /// <returns>The entity.</returns>
    public static EntitiesDashboardEntity Create(
        Type entityType,
        Type idType,
        EntitiesDashboardEntityConfiguration configuration,
        IReadOnlyList<IEntitiesDashboardSource> sources)
    {
        configuration ??= new EntitiesDashboardEntityConfiguration(entityType);
        var properties = BuildProperties(entityType, configuration);
        var creator = BuildCreator(entityType, configuration);

        var canCreate = creator != null;
        var canUpdate = properties.Any(p => p.IsWritable && !p.IsHidden && !p.IsId);

        var notes = new List<string>();
        if (!canCreate)
        {
            notes.Add(entityType.IsAbstract
                ? "Create is unavailable because the type is abstract."
                : "Create is unavailable because the type has no public parameterless constructor.");
        }

        if (!canUpdate)
        {
            notes.Add("Update is unavailable because no property has a public setter.");
        }

        if ((!canCreate || !canUpdate) && !configuration.AllowNonPublicSetters)
        {
            notes.Add("Call options.Entity<T>(e => e.AllowNonPublicSetters()) to opt in to non-public setters and constructors.");
        }

        var fullName = entityType.FullName ?? entityType.Name;

        return new EntitiesDashboardEntity
        {
            Key = fullName,
            Name = entityType.Name,
            DisplayName = Shortener.Apply(fullName, 60, "."),
            EntityType = entityType,
            IdType = idType,
            IdPrimitiveType = EntitiesDashboardIdConverter.GetPrimitiveType(idType),
            Sources = sources,
            Properties = properties,
            CanCreate = canCreate,
            CanUpdate = canUpdate,
            CanDelete = true,
            CapabilityNote = notes.Count == 0 ? null : string.Join(' ', notes),
            IsAuditable = typeof(IAuditable).IsAssignableFrom(entityType),
            IsConcurrency = typeof(IConcurrency).IsAssignableFrom(entityType),
            IsSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType),
            Creator = creator
        };
    }

    /// <summary>Maps a CLR type to its editor kind.</summary>
    /// <param name="type">The property type.</param>
    /// <returns>The editor kind.</returns>
    public static EntitiesDashboardEditorKind GetEditorKind(Type type)
    {
        var under = EntitiesDashboardTypeInfo.Unwrap(type);

        if (EntitiesDashboardTypeInfo.IsTypedId(under, out var primitive))
        {
            return GetEditorKind(primitive);
        }

        if (EntitiesDashboardTypeInfo.IsEnumeration(under))
        {
            return EntitiesDashboardEditorKind.Enumeration;
        }

        if (under == typeof(string) || under == typeof(char) || under == typeof(TimeSpan)
            || under == typeof(DateOnly) || under == typeof(TimeOnly))
        {
            return EntitiesDashboardEditorKind.Text;
        }

        if (under == typeof(bool))
        {
            return EntitiesDashboardEditorKind.Boolean;
        }

        if (under == typeof(Guid))
        {
            return EntitiesDashboardEditorKind.Guid;
        }

        if (under == typeof(DateTime) || under == typeof(DateTimeOffset))
        {
            return EntitiesDashboardEditorKind.DateTime;
        }

        if (under.IsEnum)
        {
            return EntitiesDashboardEditorKind.Enum;
        }

        if (under.IsPrimitive || under == typeof(decimal))
        {
            return EntitiesDashboardEditorKind.Number;
        }

        if (under == typeof(byte[])
            || typeof(Delegate).IsAssignableFrom(under)
            || typeof(Type).IsAssignableFrom(under)
            || typeof(Stream).IsAssignableFrom(under)
            || typeof(Task).IsAssignableFrom(under)
            || under == typeof(object)
            || under.IsPointer
            || under.IsByRef)
        {
            return EntitiesDashboardEditorKind.Unsupported;
        }

        if (EntitiesDashboardTypeInfo.IsCollection(under))
        {
            var element = EntitiesDashboardTypeInfo.GetElementType(under);

            return element != null && typeof(IEntity).IsAssignableFrom(EntitiesDashboardTypeInfo.Unwrap(element))
                ? EntitiesDashboardEditorKind.Unsupported
                : EntitiesDashboardEditorKind.Json;
        }

        return typeof(IEntity).IsAssignableFrom(under)
            ? EntitiesDashboardEditorKind.Unsupported
            : EntitiesDashboardEditorKind.Json;
    }

    private static IReadOnlyList<EntityPropertyDescriptor> BuildProperties(Type entityType, EntitiesDashboardEntityConfiguration configuration)
    {
        var descriptors = new List<EntityPropertyDescriptor>();

        foreach (var property in EntitiesDashboardTypeInfo.GetPublicProperties(entityType))
        {
            if (property.PropertyType == typeof(DomainEvents))
            {
                continue;
            }

            descriptors.Add(BuildProperty(entityType, property, configuration));
        }

        return descriptors
            .OrderBy(p => p.IsId ? 0 : p.IsAudit ? 3 : p.IsConcurrency ? 2 : 1)
            .ToList();
    }

    private static EntityPropertyDescriptor BuildProperty(Type entityType, PropertyInfo property, EntitiesDashboardEntityConfiguration configuration)
    {
        var name = property.Name;
        var kind = GetEditorKind(property.PropertyType);
        var under = EntitiesDashboardTypeInfo.Unwrap(property.PropertyType);
        var isId = name == "Id";
        var isAudit = property.PropertyType == typeof(AuditState);
        var isConcurrency = name == nameof(IConcurrency.ConcurrencyVersion) && typeof(IConcurrency).IsAssignableFrom(entityType);
        var rule = FindRule(configuration, name, null);
        var setMethod = GetSetMethod(property);
        var setter = setMethod != null && (setMethod.IsPublic || configuration.AllowNonPublicSetters)
            ? BuildSetter(entityType, property, setMethod)
            : null;

        var readOnlyReason = GetReadOnlyReason(kind, isId, isAudit, isConcurrency, setter);
        var isMasked = rule?.Mode == EntitiesDashboardPropertyMode.Mask;
        var isHidden = rule?.Mode == EntitiesDashboardPropertyMode.Hide;
        var isScalar = IsScalarKind(kind);
        var members = kind == EntitiesDashboardEditorKind.Json && !EntitiesDashboardTypeInfo.IsCollection(under)
            ? BuildMembers(property, under, isAudit, configuration)
            : [];

        return new EntityPropertyDescriptor
        {
            Path = name,
            Name = name,
            ClrType = property.PropertyType,
            UnderlyingType = under,
            EditorKind = kind,
            IsNullable = IsNullable(property.PropertyType),
            IsReadOnly = readOnlyReason != null,
            ReadOnlyReason = readOnlyReason,
            IsMasked = isMasked,
            MaskText = isMasked ? rule.MaskText ?? DEFAULTMASK : null,
            IsHidden = isHidden,
            IsFilterable = isScalar && !isMasked && !isHidden,
            IsSortable = isScalar && !isMasked && !isHidden,
            IsAudit = isAudit,
            IsId = isId,
            IsTypedId = EntitiesDashboardTypeInfo.IsTypedId(under, out _),
            IsConcurrency = isConcurrency,
            Members = members,
            Getter = BuildGetter(entityType, property, false),
            Setter = setter
        };
    }

    private static List<EntityPropertyDescriptor> BuildMembers(
        PropertyInfo parent,
        Type parentType,
        bool isAudit,
        EntitiesDashboardEntityConfiguration configuration)
    {
        var members = new List<EntityPropertyDescriptor>();
        var parentRule = FindRule(configuration, parent.Name, null);

        foreach (var member in EntitiesDashboardTypeInfo.GetPublicProperties(parentType))
        {
            var kind = GetEditorKind(member.PropertyType);
            var path = $"{parent.Name}.{member.Name}";
            var rule = FindRule(configuration, parent.Name, member.Name) ?? parentRule;
            var isMasked = rule?.Mode == EntitiesDashboardPropertyMode.Mask;
            var isHidden = rule?.Mode == EntitiesDashboardPropertyMode.Hide;

            // computed audit members (no setter) cannot be translated to a query
            var translatable = !isAudit || GetSetMethod(member) != null;

            members.Add(new EntityPropertyDescriptor
            {
                Path = path,
                Name = member.Name,
                ClrType = member.PropertyType,
                UnderlyingType = EntitiesDashboardTypeInfo.Unwrap(member.PropertyType),
                EditorKind = kind,
                IsNullable = IsNullable(member.PropertyType),
                IsReadOnly = true,
                ReadOnlyReason = isAudit
                    ? "Audit state is read-only."
                    : "Value-object members are edited through the JSON of their parent property.",
                IsMasked = isMasked,
                MaskText = isMasked ? rule.MaskText ?? DEFAULTMASK : null,
                IsHidden = isHidden,
                IsFilterable = IsScalarKind(kind) && translatable && !isMasked && !isHidden,
                IsSortable = false,
                IsAudit = isAudit,
                Getter = BuildGetter(parentType, member, true)
            });
        }

        return members;
    }

    private static EntitiesDashboardPropertyRule FindRule(EntitiesDashboardEntityConfiguration configuration, string name, string member)
    {
        var path = member == null ? name : $"{name}.{member}";

        return configuration.Properties.TryGetValue(path, out var rule) ? rule : null;
    }

    private static bool IsScalarKind(EntitiesDashboardEditorKind kind) =>
        kind is EntitiesDashboardEditorKind.Text
            or EntitiesDashboardEditorKind.Number
            or EntitiesDashboardEditorKind.Guid
            or EntitiesDashboardEditorKind.Boolean
            or EntitiesDashboardEditorKind.DateTime
            or EntitiesDashboardEditorKind.Enum
            or EntitiesDashboardEditorKind.Enumeration;

    private static bool IsNullable(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

    private static string GetReadOnlyReason(EntitiesDashboardEditorKind kind, bool isId, bool isAudit, bool isConcurrency, Action<object, object> setter)
    {
        if (isId)
        {
            return "The Id cannot be changed after creation.";
        }

        if (isAudit)
        {
            return "Audit state is read-only.";
        }

        if (isConcurrency)
        {
            return "The concurrency version is managed by the store.";
        }

        if (kind == EntitiesDashboardEditorKind.Unsupported)
        {
            return "This property type is not supported for editing.";
        }

        return setter == null
            ? "The property has no public setter."
            : null;
    }

    private static MethodInfo GetSetMethod(PropertyInfo property) =>
        property.GetSetMethod(true)
        ?? property.DeclaringType?.GetProperty(property.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)?.GetSetMethod(true);

    private static Func<object, object> BuildGetter(Type ownerType, PropertyInfo property, bool nullSafe)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        Expression read = Expression.Convert(
            Expression.Property(Expression.Convert(instance, ownerType), property),
            typeof(object));

        if (nullSafe)
        {
            read = Expression.Condition(
                Expression.Equal(instance, Expression.Constant(null)),
                Expression.Constant(null, typeof(object)),
                read);
        }

        return Expression.Lambda<Func<object, object>>(read, instance).Compile();
    }

    private static Action<object, object> BuildSetter(Type ownerType, PropertyInfo property, MethodInfo setMethod)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var value = Expression.Parameter(typeof(object), "value");
        var call = Expression.Call(
            Expression.Convert(instance, ownerType),
            setMethod,
            Expression.Convert(value, property.PropertyType));

        return Expression.Lambda<Action<object, object>>(call, instance, value).Compile();
    }

    private static Func<object> BuildCreator(Type entityType, EntitiesDashboardEntityConfiguration configuration)
    {
        if (entityType.IsAbstract || entityType.IsInterface)
        {
            return null;
        }

        var flags = BindingFlags.Instance | BindingFlags.Public;
        if (configuration.AllowNonPublicSetters)
        {
            flags |= BindingFlags.NonPublic;
        }

        var constructor = entityType.GetConstructor(flags, null, Type.EmptyTypes, null);
        if (constructor == null)
        {
            return null;
        }

        return Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(constructor), typeof(object))).Compile();
    }
}
