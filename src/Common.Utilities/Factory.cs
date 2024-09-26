// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Factory class for creating instances of type <typeparamref name="T" />.
/// </summary>
/// <typeparam name="T">The target type which should have a parameterless constructor.</typeparam>
public static class Factory<T>
    where T : class
{
    /// <summary>
    ///     Compiled lambda expression to create an instance of type <typeparamref name="T" />.
    /// </summary>
    private static readonly Func<T> CreateFunc = Expression.Lambda<Func<T>>(Expression.New(typeof(T))).Compile();

    /// <summary>
    ///     Creates an instance of type <typeparamref name="T" /> by calling its parameterless constructor.
    /// </summary>
    /// <returns>An instance of type <typeparamref name="T" />.</returns>
    public static T Create()
    {
        return CreateFunc();
        // without ctor, fast
    }

    /// <summary>
    ///     Creates an instance of type <typeparamref name="T" /> by setting its properties using the provided dictionary.
    /// </summary>
    /// <param name="propertyItems">
    ///     A dictionary containing property names and their corresponding values to set on the created
    ///     instance.
    /// </param>
    /// <returns>An instance of type <typeparamref name="T" />.</returns>
    public static T Create(IDictionary<string, object> propertyItems)
    {
        var instance = CreateFunc(); // without ctor, fast
        ReflectionHelper.SetProperties(instance, propertyItems);

        return instance;
    }

    /// <summary>
    ///     Creates an instance of the specified type <typeparamref name="T" /> using the constructor that best matches
    ///     the specified parameters.
    /// </summary>
    /// <param name="parameters">The constructor parameters.</param>
    /// <returns>An instance of type <typeparamref name="T" /> or default if the constructor is not found.</returns>
    public static T Create(params object[] parameters)
    {
        try
        {
            return Activator.CreateInstance(typeof(T), parameters) as T;
        }
        catch (MissingMethodException)
        {
            return default;
        }
    }

    /// <summary>
    ///     Creates an instance of type <typeparamref name="T" /> by calling it's parameterless constructor.
    /// </summary>
    /// <returns>An instance of type <typeparamref name="T" />.</returns>
    public static T Create(IDictionary<string, object> propertyItems, params object[] parameters)
    {
        try
        {
            var instance = Activator.CreateInstance(typeof(T), parameters) as T;
            ReflectionHelper.SetProperties(instance, propertyItems);

            return instance;
        }
        catch (MissingMethodException)
        {
            return default;
        }
    }

    /// <summary>
    ///     Creates an instance of type <typeparamref name="T" /> by calling its parameterless constructor.
    /// </summary>
    /// <returns>An instance of type <typeparamref name="T" />.</returns>
    public static T Create(IServiceProvider serviceProvider)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        return ActivatorUtilities.CreateInstance<T>(serviceProvider);
    }

    /// <summary>
    ///     Creates an instance of type <typeparamref name="T" /> using its parameterless constructor.
    /// </summary>
    /// <returns>An instance of type <typeparamref name="T" />.</returns>
    public static T Create(IServiceProvider serviceProvider, params object[] parameters)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        return ActivatorUtilities.CreateInstance<T>(serviceProvider, parameters);
    }

    /// <summary>
    ///     Creates an instance of type <typeparamref name="T" /> and sets its properties based on the provided dictionary of
    ///     property items.
    /// </summary>
    /// <param name="propertyItems">A dictionary of property names and values to set on the created instance.</param>
    /// <param name="serviceProvider">The service provider used to resolve dependencies for the instance creation.</param>
    /// <returns>An instance of type <typeparamref name="T" /> with its properties set as specified.</returns>
    public static T Create(IDictionary<string, object> propertyItems, IServiceProvider serviceProvider)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        var instance = ActivatorUtilities.CreateInstance<T>(serviceProvider);
        ReflectionHelper.SetProperties(instance, propertyItems);

        return instance;
    }

    /// <summary>
    ///     Creates an instance of type <typeparamref name="T" /> by calling its parameterless constructor.
    /// </summary>
    /// <returns>An instance of type <typeparamref name="T" />.</returns>
    public static T Create(
        IDictionary<string, object> propertyItems,
        IServiceProvider serviceProvider,
        params object[] parameters)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        var instance = ActivatorUtilities.CreateInstance<T>(serviceProvider, parameters);
        ReflectionHelper.SetProperties(instance, propertyItems);

        return instance;
    }
}

/// <summary>
///     Provides methods to create instances of specified types using reflection.
/// </summary>
public static class Factory
{
    /// <summary>
    ///     Creates an instance of the specified <paramref name="type" /> using the constructor that best matches
    ///     the specified <paramref name="parameters" />.
    /// </summary>
    /// <param name="type">The type of object to create.</param>
    /// <param name="parameters">An array of arguments that match the parameters of the constructor to invoke.</param>
    /// <returns>An instance of the specified type, or <c>null</c> if no matching constructor is found.</returns>
    public static object Create(Type type, params object[] parameters)
    {
        EnsureArg.IsNotNull(type, nameof(type));

        try
        {
            return Activator.CreateInstance(type, parameters);
        }
        catch (MissingMethodException)
        {
            return default;
        }
    }

    /// <summary>
    ///     Creates an instance of type <typeparamref name="T" /> by calling its parameterless constructor.
    /// </summary>
    /// <returns>An instance of type <typeparamref name="T" />.</returns>
    public static T Create<T>(Type type, params object[] parameters)
        where T : class
    {
        return Create(type, parameters) as T;
    }

    /// <summary>
    ///     Creates an instance of the specified type using the constructor that best matches
    ///     the specified parameters.
    /// </summary>
    /// <param name="parameters">An array of arguments that match the parameters of the constructor to invoke.</param>
    /// <returns>An instance of the specified type, or <c>null</c> if no matching constructor is found.</returns>
    public static T Create<T>(params object[] parameters)
        where T : class
    {
        return Create(typeof(T), parameters) as T;
    }

    /// <summary>
    ///     Creates an instance of the specified generic <paramref name="type" /> using the specified
    ///     <paramref name="genericType" /> and constructor parameters.
    /// </summary>
    /// <param name="type">The type of the object to create.</param>
    /// <param name="genericType">The generic type to be used for creating the instance.</param>
    /// <param name="parameters">An array of arguments that match the parameters of the constructor to invoke.</param>
    /// <returns>An instance of the specified generic type, or <c>null</c> if no matching constructor is found.</returns>
    public static object Create(Type type, Type genericType, params object[] parameters)
    {
        EnsureArg.IsNotNull(type, nameof(type));
        EnsureArg.IsNotNull(genericType, nameof(genericType));

        try
        {
            return Activator.CreateInstance(type.MakeGenericType(genericType), parameters);
        }
        catch (MissingMethodException)
        {
            return default;
        }
    }

    /// <summary>
    ///     Creates an instance of the specified generic type using the provided constructor parameters.
    /// </summary>
    /// <param name="type">The generic type definition of the object to create.</param>
    /// <param name="genericType">The specific type to use as the generic argument.</param>
    /// <param name="parameters">The parameters to pass to the constructor.</param>
    /// <returns>An instance of the specified generic type.</returns>
    public static T Create<T>(Type type, Type genericType, params object[] parameters)
        where T : class
    {
        return Create(type, genericType, parameters) as T;
    }

    /// <summary>
    ///     Creates an instance of the specified type <paramref name="type" /> using the constructor that best matches
    ///     the specified parameters and sets the properties defined in <paramref name="propertyItems" />.
    /// </summary>
    /// <param name="type">The type of the object to create.</param>
    /// <param name="propertyItems">A dictionary containing property names and values to be set on the created instance.</param>
    /// <param name="parameters">An array of parameters to pass to the constructor of the type.</param>
    /// <returns>An instance of the specified type, or <c>null</c> if the creation fails.</returns>
    public static object Create(Type type, IDictionary<string, object> propertyItems, params object[] parameters)
    {
        EnsureArg.IsNotNull(type, nameof(type));

        try
        {
            var instance = Activator.CreateInstance(type, parameters);
            ReflectionHelper.SetProperties(instance, propertyItems);

            return instance;
        }
        catch (MissingMethodException)
        {
            return default;
        }
    }

    /// <summary>
    ///     Creates an instance of the specified <paramref name="type" /> using the constructor that best matches
    ///     the specified <paramref name="parameters" /> and initializes it with the values specified in
    ///     <paramref name="propertyItems" />.
    /// </summary>
    /// <typeparam name="T">The type of object to create. Must be a class.</typeparam>
    /// <param name="type">The type of object to create.</param>
    /// <param name="propertyItems">A dictionary containing property names and values to set on the created instance.</param>
    /// <param name="parameters">An array of arguments that match the parameters of the constructor to invoke.</param>
    /// <returns>
    ///     An instance of type <typeparamref name="T" /> initialized with the specified property values, or <c>null</c>
    ///     if no matching constructor is found.
    /// </returns>
    public static T Create<T>(Type type, IDictionary<string, object> propertyItems, params object[] parameters)
        where T : class
    {
        return Create(type, propertyItems, parameters) as T;
    }

    /// <summary>
    ///     Creates an instance of the specified type using the service provider to
    ///     get instances for the constructor.
    /// </summary>
    /// <param name="type">The type of the instance to create.</param>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <returns>An object of the specified type.</returns>
    public static object Create(Type type, IServiceProvider serviceProvider)
    {
        EnsureArg.IsNotNull(type, nameof(type));
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        return ActivatorUtilities.CreateInstance(serviceProvider, type);
    }

    /// <summary>
    ///     Creates an instance of the specified <paramref name="type" /> using the service provider to
    ///     get instances for the constructor.
    /// </summary>
    /// <typeparam name="T">The type of object to create.</typeparam>
    /// <param name="type">The type of the instance to create.</param>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <returns>An instance of the specified type, or <c>null</c> if the instance could not be created.</returns>
    public static T Create<T>(Type type, IServiceProvider serviceProvider)
        where T : class
    {
        return Create(type, serviceProvider) as T;
    }

    /// <summary>
    ///     Creates an instance of the specified type using the provided service provider and sets the properties from the
    ///     given dictionary.
    /// </summary>
    /// <param name="type">The type of the object to create.</param>
    /// <param name="propertyItems">A dictionary containing property names and their values to set on the created instance.</param>
    /// <param name="serviceProvider">The service provider used to resolve dependencies for the constructor.</param>
    /// <returns>An instance of the specified type with properties set.</returns>
    public static object Create(Type type, IDictionary<string, object> propertyItems, IServiceProvider serviceProvider)
    {
        EnsureArg.IsNotNull(type, nameof(type));
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        var instance = ActivatorUtilities.CreateInstance(serviceProvider, type);
        ReflectionHelper.SetProperties(instance, propertyItems);

        return instance;
    }

    /// <summary>
    ///     Creates an instance of the specified <typeparamref name="T" /> using the provided property items and service
    ///     provider.
    /// </summary>
    /// <param name="type">The type of object to create.</param>
    /// <param name="propertyItems">
    ///     A dictionary containing property names and their corresponding values to set on the created
    ///     instance.
    /// </param>
    /// <param name="serviceProvider">The service provider to use for retrieving dependencies needed by the constructor.</param>
    /// <returns>An instance of the specified type <typeparamref name="T" />, or <c>null</c> if creation fails.</returns>
    public static T Create<T>(Type type, IDictionary<string, object> propertyItems, IServiceProvider serviceProvider)
        where T : class
    {
        return Create(type, propertyItems, serviceProvider) as T;
    }
}