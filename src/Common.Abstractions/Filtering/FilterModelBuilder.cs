// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq.Expressions;

/// <summary>
/// A static class that provides methods for building filter models
/// for querying collections of data.
/// </summary>
public static class FilterModelBuilder
{
    private static readonly ThreadLocal<FilterModel> FilterModel = new(() => new FilterModel());

    /// <summary>
    /// Initializes a new instance of the <see cref="Builder{T}"/> class for a specified type.
    /// This method serves as the entry point for building a filter model.
    /// </summary>
    /// <typeparam name="T">The type of objects to filter.</typeparam>
    /// <returns>A new instance of the <see cref="Builder{T}"/> class.</returns>
    /// <example>
    /// var filterBuilder = FilterModelBuilder.For{PersonStub}();
    /// </example>
    public static Builder<T> For<T>()
    {
        return new Builder<T>(FilterModel.Value);
    }

    public static Builder<T> For<T>(FilterModel filterModel)
    {
        filterModel ??= FilterModel.Value;
        return new Builder<T>(filterModel);
    }

    public class Builder<T>(FilterModel model)
    {
        internal readonly FilterModel filterModel = model;

        /// <summary>
        /// Sets the paging options for the filter model.
        /// </summary>
        /// <param name="page">The page number to retrieve.</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <example>
        /// builder.SetPaging(1, 10); // Sets to retrieve the first page with 10 items per page.
        /// </example>
        public Builder<T> SetPaging(int page, int pageSize, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            if (page < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be greater than or equal to 1.");
            }

            if (pageSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than or equal to 1.");
            }

            this.filterModel.Page = page;
            this.filterModel.PageSize = pageSize;

            return this;
        }

        /// <summary>
        /// Sets the paging options for the filter model and the first page.
        /// </summary>
        /// <param name="pageSize">The number of items per page.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <example>
        /// builder.SetPaging(1, 10); // Sets to retrieve the first page with 10 items per page.
        /// </example>
        public Builder<T> SetPaging(int pageSize, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            if (pageSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than or equal to 1.");
            }

            this.filterModel.Page = 1;
            this.filterModel.PageSize = pageSize;

            return this;
        }

        /// <summary>
        /// Sets the paging for the filter model using a standard page size.
        /// </summary>
        /// <param name="page">The page number to set.</param>
        /// <param name="standardPageSize">The standard page size to use from the <see cref="PageSize"/> enum.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <example>
        /// builder.SetPaging(1, PageSize.Medium);
        /// </example>
        public Builder<T> SetPaging(int page, PageSize standardPageSize, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            if (page < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be greater than or equal to 1.");
            }

            this.filterModel.Page = page;
            this.filterModel.PageSize = (int)standardPageSize;

            return this;
        }

        /// <summary>
        /// Sets the paging for the filter model using a standard page size and the first page.
        /// </summary>
        /// <param name="standardPageSize">The standard page size to use from the <see cref="PageSize"/> enum.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <example>
        /// builder.SetPaging(PageSize.Medium);
        /// </example>
        public Builder<T> SetPaging(PageSize standardPageSize, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            this.filterModel.Page = 1;
            this.filterModel.PageSize = (int)standardPageSize;

            return this;
        }

        /// <summary>
        /// Sets the filter model to retrieve all items without paging.
        /// </summary>
        /// <param name="condition">If specified, applies the paging configuration only when the condition is <see langword="true"/>. </param>
        /// <returns></returns>
        public Builder<T> SetNoPaging(bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            this.filterModel.Page = 0;
            this.filterModel.PageSize = 0;

            return this;
        }

        /// <summary>
        /// Configures the query to disable entity tracking in the underlying context.
        /// </summary>
        /// <remarks>Disabling tracking can improve performance for read-only queries by preventing the
        /// data context from monitoring changes to returned entities. Use this option when you do not intend to update
        /// the queried entities.</remarks>
        /// <param name="value">A value indicating whether entity tracking should be disabled. Defaults to <see langword="true"/>.</param>
        /// <param name="condition">If specified, applies the no-tracking configuration only when the condition is <see langword="true"/>. </param>
        /// <see langword="false"/>, the configuration is not changed.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance with the updated tracking configuration.</returns>
        public Builder<T> SetNoTracking(bool value = true, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            this.filterModel.NoTracking = value;

            return this;
        }

        /// <summary>
        /// Adds a filter criteria based on a specified property and value.
        /// </summary>
        /// <param name="propertySelector">An expression selecting the property to filter on.</param>
        /// <param name="filterOperator">The operator to use for filtering.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown if the property selector is invalid.</exception>
        /// <example>
        /// builder.AddFilter(person => person.FirstName, FilterOperator.Equal, "John");
        /// </example>
        public Builder<T> AddFilter(Expression<Func<T, object>> propertySelector, FilterOperator filterOperator, object value, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            var memberExpression = GetMemberExpression(propertySelector);
            if (memberExpression == null)
            {
                throw new ArgumentException("Invalid property selector. Must be a member expression.");
            }

            var criteria = new FilterCriteria
            {
                Field = memberExpression.GetFullPropertyName(),
                Operator = filterOperator,
                Value = value
            };

            this.filterModel.Filters ??= [];
            this.filterModel.Filters.Add(criteria);

            return this;
        }

        /// <summary>
        /// Determines whether any filters are currently applied.
        /// </summary>
        public bool HasFilters() =>
            this.filterModel.Filters?.Any() == true;

        /// <summary>
        /// Adds a collection filter based on a property that represents a collection of objects.
        /// </summary>
        /// <typeparam name="TCollection">The type of the collection items.</typeparam>
        /// <param name="collectionSelector">An expression selecting the collection property.</param>
        /// <param name="filterOperator">The operator to use for filtering the collection.</param>
        /// <param name="configure">A configuration action to specify filters for the collection items.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <example>
        /// builder.AddCollectionFilter(person => person.Addresses, FilterOperator.Any, collectionBuilder =>
        /// {
        ///     collectionBuilder.AddFilter(address => address.City, FilterOperator.Equal, "Berlin");
        /// });
        /// </example>
        public Builder<T> AddFilter<TCollection>(
            Expression<Func<T, IEnumerable<TCollection>>> collectionSelector,
            FilterOperator filterOperator,
            Action<Builder<TCollection>> configure, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            var memberExpression = GetMemberExpression(collectionSelector);
            if (memberExpression == null)
            {
                throw new ArgumentException("Invalid collection selector. Must be a member expression.");
            }

            var criteria = new FilterCriteria
            {
                Field = memberExpression.GetFullPropertyName(),
                Operator = filterOperator,
                Filters = []
            };

            var collectionBuilder = new Builder<TCollection>(new FilterModel());
            configure(collectionBuilder);

            // Only add criteria if it has filters
            var filters = collectionBuilder.GetFilters();
            if (filters.Any()) // Check if there are any filters added
            {
                criteria.Filters.AddRange(filters);
                this.filterModel.Filters ??= [];
                this.filterModel.Filters.Add(criteria);
            }

            return this;
        }

        public Builder<T> AddFilter<T2>(
            Expression<Func<T, T2>> propertySelector,
            FilterOperator filterOperator,
            Action<Builder<T2>> configure, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            var memberExpression = GetMemberExpression(propertySelector);
            if (memberExpression == null)
            {
                throw new ArgumentException("Invalid property selector. Must be a member expression.");
            }

            var criteria = new FilterCriteria
            {
                Field = memberExpression.GetFullPropertyName(),
                Operator = filterOperator,
                Filters = []
            };

            var collectionBuilder = new Builder<T2>(new FilterModel());
            configure(collectionBuilder);

            // Only add criteria if it has filters
            var filters = collectionBuilder.GetFilters();
            if (filters.Any()) // Check if there are any filters added
            {
                criteria.Filters.AddRange(filters);
                this.filterModel.Filters ??= [];
                this.filterModel.Filters.Add(criteria);
            }

            return this;
        }

        /// <summary>
        /// Adds an ordering criterion to the filter model.
        /// </summary>
        /// <param name="orderBy">An expression selecting the property to order by.</param>
        /// <param name="direction">The direction of the ordering (ascending or descending).</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <example>
        /// builder.AddOrdering(person => person.LastName, OrderDirection.Ascending);
        /// </example>
        public Builder<T> AddOrdering(Expression<Func<T, object>> orderBy, OrderDirection direction, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            var ordering = new FilterOrderCriteria
            {
                Field = GetMemberName(orderBy),
                Direction = direction
            };

            this.filterModel.Orderings ??= [];
            this.filterModel.Orderings.Add(ordering);

            return this;
        }

        /// <summary>
        /// Determines whether the current filter model contains any orderings.
        /// </summary>
        public bool HasOrderings() =>
            this.filterModel.Orderings?.Any() == true;

        /// <summary>
        /// Adds an include criterion to the filter model, specifying related entities to include.
        /// </summary>
        /// <param name="includeSelector">An expression selecting the property to include.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <example>
        /// builder.AddInclude(person => person.Addresses);
        /// </example>
        public Builder<T> AddInclude(Expression<Func<T, object>> includeSelector, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            this.filterModel.Includes ??= [];
            this.filterModel.Includes.Add(GetMemberName(includeSelector));

            return this;
        }

        /// <summary>
        /// Adds an include criterion to the filter model, specifying related entities to include using a string path.
        /// </summary>
        /// <param name="path">The navigation property path to include in the query.</param>
        /// <param name="condition">If specified, applies the include only when the condition is <see langword="true"/>.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <example>
        /// builder.AddInclude("Addresses");
        /// builder.AddInclude("Addresses.City");
        /// </example>
        public Builder<T> AddInclude(string path, bool? condition = null)
        {
            EnsureArg.IsNotNullOrWhiteSpace(path, nameof(path));

            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            this.filterModel.Includes ??= [];
            this.filterModel.Includes.Add(path);

            return this;
        }

        /// <summary>
        /// Adds an include criterion with support for ThenInclude chaining, specifying related entities to include.
        /// </summary>
        /// <typeparam name="TProperty">The type of the navigation property to include.</typeparam>
        /// <param name="includeSelector">An expression selecting the property to include.</param>
        /// <returns>An <see cref="IncludeBuilder{T, TProperty}"/> instance for chaining ThenInclude calls.</returns>
        /// <example>
        /// builder.AddInclude(person => person.BillingAddress)
        ///     .ThenInclude(address => address.City);
        /// </example>
        public IncludeBuilder<T, TProperty> AddInclude<TProperty>(Expression<Func<T, TProperty>> includeSelector, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                // Return a builder that does nothing but maintains the fluent interface
                return new IncludeBuilder<T, TProperty>(this, null, false);
            }

            var memberExpression = GetMemberExpression(includeSelector);
            if (memberExpression == null)
            {
                throw new ArgumentException("Invalid include selector. Must be a member expression.");
            }

            var includePath = memberExpression.GetFullPropertyName();
            this.filterModel.Includes ??= [];
            this.filterModel.Includes.Add(includePath);

            return new IncludeBuilder<T, TProperty>(this, includePath, true);
        }

        /// <summary>
        /// Determines whether the filter model contains any included items.
        /// </summary>
        public bool HasIncludes() =>
            this.filterModel.Includes?.Any() == true;

        /// <summary>
        /// Adds an hierarchy criterion to the filter model, specifying the children to include.
        /// </summary>
        /// <param name="hierarchySelector">An expression selecting the property that contains the children.</param>
        /// <returns>The current <see cref="Builder{T}"/> instance for method chaining.</returns>
        /// <example>
        /// builder.AddHierarchy(manager => manager.Employees);
        /// </example>
        public Builder<T> AddHierarchy(Expression<Func<T, object>> hierarchySelector, int maxDepth = 5, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return this;
            }

            this.filterModel.Hierarchy = GetMemberName(hierarchySelector);
            this.filterModel.HierarchyMaxDepth = maxDepth;

            return this;
        }

        /// <summary>
        /// Adds a custom filter to the filter model, which allows for specialized filtering logic.
        /// </summary>
        /// <param name="customType">The type of the custom filter to add.</param>
        /// <returns>A <see cref="CustomFilterBuilder"/> instance for adding parameters to the custom filter.</returns>
        /// <example>
        /// var customFilter = builder.AddCustomFilter(FilterCustomType.FullTextSearch);
        /// customFilter.AddParameter("searchTerm", "John");
        /// </example>
        public CustomFilterBuilder AddCustomFilter(FilterCustomType customType, bool? condition = null)
        {
            if (condition.HasValue && !condition.Value)
            {
                return new CustomFilterBuilder([], this);
            }

            var customFilter = new FilterCriteria
            {
                CustomType = customType,
                CustomParameters = []
            };

            this.filterModel.Filters ??= [];
            this.filterModel.Filters.Add(customFilter);

            return new CustomFilterBuilder(customFilter.CustomParameters, this); // Pass the parent builder instance to allow `Done()` to return control.
        }

        /// <summary>
        /// Builds and returns the final <see cref="Common.FilterModel"/> instance.
        /// This resets the thread-local filter model for subsequent builds.
        /// </summary>
        /// <returns>A <see cref="Common.FilterModel"/> containing the built filter criteria, orderings, includes, and paging options.</returns>
        /// <example>
        /// var filterModel = builder.Build();
        /// </example>
        public FilterModel Build()
        {
            FilterModel.Value = new FilterModel();

            return this.filterModel;
        }

        /// <summary>
        /// A builder class for adding parameters to a custom filter.
        /// </summary>
        /// <summary>
        /// A builder class for adding parameters to a custom filter.
        /// </summary>
        public class CustomFilterBuilder
        {
            private readonly Dictionary<string, object> parameters;
            private readonly Builder<T> parentBuilder;

            internal CustomFilterBuilder(Dictionary<string, object> parameters, Builder<T> parentBuilder)
            {
                this.parameters = parameters;
                this.parentBuilder = parentBuilder;
            }

            /// <summary>
            /// Adds a parameter to the custom filter.
            /// </summary>
            public CustomFilterBuilder AddParameter(string key, object value, bool? condition = null)
            {
                if (condition.HasValue && !condition.Value)
                {
                    return this;
                }

                this.parameters.TryAdd(key, value);
                return this;
            }

            public CustomFilterBuilder AddParameter(string key, string[] values, bool? condition = null)
            {
                if (condition.HasValue && !condition.Value)
                {
                    return this;
                }

                this.parameters.TryAdd(key, values);
                return this;
            }

            /// <summary>
            /// Exits the custom filter builder and returns to the main builder.
            /// </summary>
            public Builder<T> Done()
            {
                return this.parentBuilder;
            }
        }

        private static MemberExpression GetMemberExpression(Expression<Func<T, object>> selector)
        {
            return selector.Body switch
            {
                UnaryExpression { Operand: MemberExpression member } => member,
                MemberExpression memberExpression => memberExpression,
                _ => null
            };
        }

        private static MemberExpression GetMemberExpression<T2>(Expression<Func<T, T2>> selector)
        {
            return selector.Body switch
            {
                UnaryExpression { Operand: MemberExpression member } => member,
                MemberExpression memberExpression => memberExpression,
                _ => null
            };
        }

        private static MemberExpression GetMemberExpression<TCollection>(Expression<Func<T, IEnumerable<TCollection>>> selector)
        {
            return selector.Body switch
            {
                UnaryExpression { Operand: MemberExpression member } => member,
                MemberExpression memberExpression => memberExpression,
                _ => null
            };
        }

        private static string GetMemberName(Expression<Func<T, object>> expression)
        {
            var memberExpression = GetMemberExpression(expression);

            return memberExpression?.Member.Name ?? throw new ArgumentException("Invalid expression");
        }

        private List<FilterCriteria> GetFilters()
        {
            return this.filterModel.Filters;
        }
    }

    /// <summary>
    /// A builder class for chaining ThenInclude calls on included navigation properties.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    public class IncludeBuilder<TEntity, TProperty>
    {
        internal readonly Builder<TEntity> parentBuilder;
        internal readonly string basePath;
        internal readonly bool isActive;

        internal IncludeBuilder(Builder<TEntity> parentBuilder, string basePath, bool isActive)
        {
            this.parentBuilder = parentBuilder;
            this.basePath = basePath;
            this.isActive = isActive;
        }

        /// <summary>
        /// Specifies additional related data to be further included based on a related type
        /// that was just included (for reference navigation properties).
        /// </summary>
        /// <typeparam name="TNextProperty">The type of the next navigation property to include.</typeparam>
        /// <param name="navigationPropertyPath">An expression representing the navigation property to be included.</param>
        /// <param name="condition">Optional condition to control whether this include is applied.</param>
        /// <returns>An <see cref="IncludeBuilder{TEntity, TNextProperty}"/> for further chaining.</returns>
        /// <example>
        /// builder.AddInclude(person => person.BillingAddress)
        ///     .ThenInclude(address => address.City)
        ///     .ThenInclude(city => city.Country);
        /// </example>
        public IncludeBuilder<TEntity, TNextProperty> ThenInclude<TNextProperty>(
            Expression<Func<TProperty, TNextProperty>> navigationPropertyPath,
            bool? condition = null)
        {
            if (!this.isActive || (condition.HasValue && !condition.Value))
            {
                return new IncludeBuilder<TEntity, TNextProperty>(this.parentBuilder, null, false);
            }

            var memberExpression = GetMemberExpression(navigationPropertyPath);
            if (memberExpression == null)
            {
                throw new ArgumentException("Invalid navigation property path. Must be a member expression.");
            }

            var propertyName = memberExpression.GetFullPropertyName();
            var fullPath = $"{this.basePath}.{propertyName}";

            this.parentBuilder.filterModel.Includes.Add(fullPath);

            return new IncludeBuilder<TEntity, TNextProperty>(this.parentBuilder, fullPath, true);
        }
 
        /// <summary>
        /// Returns to the main filter builder to continue building the filter model.
        /// </summary>
        /// <returns>The parent <see cref="Builder{TEntity}"/> instance.</returns>
        public Builder<TEntity> Done()
        {
            return this.parentBuilder;
        }

        private static MemberExpression GetMemberExpression<TSource, TResult>(Expression<Func<TSource, TResult>> selector)
        {
            return selector.Body switch
            {
                UnaryExpression { Operand: MemberExpression member } => member,
                MemberExpression memberExpression => memberExpression,
                _ => null
            };
        }
    }
}

/// <summary>
/// Extension methods for IncludeBuilder to support collection navigation properties.
/// </summary>
public static class IncludeBuilderExtensions
{
    /// <summary>
    /// Specifies additional related data to be further included based on a collection navigation property.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TElement">The element type of the collection.</typeparam>
    /// <typeparam name="TNextProperty">The type of the next navigation property to include.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="navigationPropertyPath">An expression representing the navigation property to be included.</param>
    /// <param name="condition">Optional condition to control whether this include is applied.</param>
    /// <returns>An <see cref="FilterModelBuilder.IncludeBuilder{TEntity, TNextProperty}"/> for further chaining.</returns>
    /// <example>
    /// builder.AddInclude(person => person.Addresses)
    ///     .ThenInclude(address => address.City);
    /// </example>
    public static FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty> ThenInclude<TEntity, TElement, TNextProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, IEnumerable<TElement>> source,
        Expression<Func<TElement, TNextProperty>> navigationPropertyPath,
        bool? condition = null)
    {
        if (!source.isActive || (condition.HasValue && !condition.Value))
        {
            return new FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty>(
                source.parentBuilder,
                null,
                false);
        }

        var memberExpression = GetMemberExpression(navigationPropertyPath);
        if (memberExpression == null)
        {
            throw new ArgumentException("Invalid navigation property path. Must be a member expression.");
        }

        var propertyName = memberExpression.GetFullPropertyName();
        var fullPath = $"{source.basePath}.{propertyName}";

        source.parentBuilder.filterModel.Includes.Add(fullPath);

        return new FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty>(
            source.parentBuilder,
            fullPath,
            true);
    }

    /// <summary>
    /// Specifies additional related data to be further included based on a collection navigation property (IReadOnlyList).
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TElement">The element type of the collection.</typeparam>
    /// <typeparam name="TNextProperty">The type of the next navigation property to include.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="navigationPropertyPath">An expression representing the navigation property to be included.</param>
    /// <param name="condition">Optional condition to control whether this include is applied.</param>
    /// <returns>An <see cref="FilterModelBuilder.IncludeBuilder{TEntity, TNextProperty}"/> for further chaining.</returns>
    public static FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty> ThenInclude<TEntity, TElement, TNextProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, IReadOnlyList<TElement>> source,
        Expression<Func<TElement, TNextProperty>> navigationPropertyPath,
        bool? condition = null)
    {
        if (!source.isActive || (condition.HasValue && !condition.Value))
        {
            return new FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty>(
                source.parentBuilder,
                null,
                false);
        }

        var memberExpression = GetMemberExpression(navigationPropertyPath);
        if (memberExpression == null)
        {
            throw new ArgumentException("Invalid navigation property path. Must be a member expression.");
        }

        var propertyName = memberExpression.GetFullPropertyName();
        var fullPath = $"{source.basePath}.{propertyName}";

        source.parentBuilder.filterModel.Includes.Add(fullPath);

        return new FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty>(
            source.parentBuilder,
            fullPath,
            true);
    }

    /// <summary>
    /// Specifies additional related data to be further included based on a collection navigation property (ICollection).
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TElement">The element type of the collection.</typeparam>
    /// <typeparam name="TNextProperty">The type of the next navigation property to include.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="navigationPropertyPath">An expression representing the navigation property to be included.</param>
    /// <param name="condition">Optional condition to control whether this include is applied.</param>
    /// <returns>An <see cref="FilterModelBuilder.IncludeBuilder{TEntity, TNextProperty}"/> for further chaining.</returns>
    public static FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty> ThenInclude<TEntity, TElement, TNextProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, ICollection<TElement>> source,
        Expression<Func<TElement, TNextProperty>> navigationPropertyPath,
        bool? condition = null)
    {
        if (!source.isActive || (condition.HasValue && !condition.Value))
        {
            return new FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty>(
                source.parentBuilder,
                null,
                false);
        }

        var memberExpression = GetMemberExpression(navigationPropertyPath);
        if (memberExpression == null)
        {
            throw new ArgumentException("Invalid navigation property path. Must be a member expression.");
        }

        var propertyName = memberExpression.GetFullPropertyName();
        var fullPath = $"{source.basePath}.{propertyName}";

        source.parentBuilder.filterModel.Includes.Add(fullPath);

        return new FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty>(
            source.parentBuilder,
            fullPath,
            true);
    }

    /// <summary>
    /// Builds and returns the final <see cref="FilterModel"/> instance.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <returns>A <see cref="FilterModel"/> containing the built filter criteria.</returns>
    public static FilterModel Build<TEntity, TProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source)
    {
        return source.parentBuilder.Build();
    }

    /// <summary>
    /// Adds an ordering criterion to the filter model.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="orderBy">An expression selecting the property to order by.</param>
    /// <param name="direction">The direction of the ordering.</param>
    /// <param name="condition">Optional condition to control whether this ordering is applied.</param>
    /// <returns>The parent <see cref="FilterModelBuilder.Builder{TEntity}"/> instance for method chaining.</returns>
    public static FilterModelBuilder.Builder<TEntity> AddOrdering<TEntity, TProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        Expression<Func<TEntity, object>> orderBy,
        OrderDirection direction,
        bool? condition = null)
    {
        return source.parentBuilder.AddOrdering(orderBy, direction, condition);
    }

    /// <summary>
    /// Adds a filter criteria to the filter model.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="propertySelector">An expression selecting the property to filter on.</param>
    /// <param name="filterOperator">The operator to use for filtering.</param>
    /// <param name="value">The value to compare against.</param>
    /// <param name="condition">Optional condition to control whether this filter is applied.</param>
    /// <returns>The parent <see cref="FilterModelBuilder.Builder{TEntity}"/> instance for method chaining.</returns>
    public static FilterModelBuilder.Builder<TEntity> AddFilter<TEntity, TProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        Expression<Func<TEntity, object>> propertySelector,
        FilterOperator filterOperator,
        object value,
        bool? condition = null)
    {
        return source.parentBuilder.AddFilter(propertySelector, filterOperator, value, condition);
    }

    /// <summary>
    /// Adds a collection filter to the filter model.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <typeparam name="TCollection">The type of the collection items.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="collectionSelector">An expression selecting the collection property.</param>
    /// <param name="filterOperator">The operator to use for filtering the collection.</param>
    /// <param name="configure">A configuration action to specify filters for the collection items.</param>
    /// <param name="condition">Optional condition to control whether this filter is applied.</param>
    /// <returns>The parent <see cref="FilterModelBuilder.Builder{TEntity}"/> instance for method chaining.</returns>
    public static FilterModelBuilder.Builder<TEntity> AddFilter<TEntity, TProperty, TCollection>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        Expression<Func<TEntity, IEnumerable<TCollection>>> collectionSelector,
        FilterOperator filterOperator,
        Action<FilterModelBuilder.Builder<TCollection>> configure,
        bool? condition = null)
    {
        return source.parentBuilder.AddFilter(collectionSelector, filterOperator, configure, condition);
    }

    /// <summary>
    /// Adds an include criterion to the filter model.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="includeSelector">An expression selecting the property to include.</param>
    /// <param name="condition">Optional condition to control whether this include is applied.</param>
    /// <returns>The parent <see cref="FilterModelBuilder.Builder{TEntity}"/> instance for method chaining.</returns>
    public static FilterModelBuilder.Builder<TEntity> AddInclude<TEntity, TProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        Expression<Func<TEntity, object>> includeSelector,
        bool? condition = null)
    {
        return source.parentBuilder.AddInclude(includeSelector, condition);
    }

    /// <summary>
    /// Adds an include criterion to the filter model using a string path.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="path">The navigation property path to include in the query.</param>
    /// <param name="condition">Optional condition to control whether this include is applied.</param>
    /// <returns>The parent <see cref="FilterModelBuilder.Builder{TEntity}"/> instance for method chaining.</returns>
    public static FilterModelBuilder.Builder<TEntity> AddInclude<TEntity, TProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        string path,
        bool? condition = null)
    {
        return source.parentBuilder.AddInclude(path, condition);
    }

    /// <summary>
    /// Adds an include criterion with support for ThenInclude chaining.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the current navigation property.</typeparam>
    /// <typeparam name="TNextProperty">The type of the next navigation property to include.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="includeSelector">An expression selecting the property to include.</param>
    /// <param name="condition">Optional condition to control whether this include is applied.</param>
    /// <returns>An <see cref="FilterModelBuilder.IncludeBuilder{TEntity, TNextProperty}"/> for chaining ThenInclude calls.</returns>
    public static FilterModelBuilder.IncludeBuilder<TEntity, TNextProperty> AddInclude<TEntity, TProperty, TNextProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        Expression<Func<TEntity, TNextProperty>> includeSelector,
        bool? condition = null)
    {
        return source.parentBuilder.AddInclude(includeSelector, condition);
    }

    /// <summary>
    /// Sets the paging options for the filter model.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="page">The page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="condition">Optional condition to control whether paging is applied.</param>
    /// <returns>The parent <see cref="FilterModelBuilder.Builder{TEntity}"/> instance for method chaining.</returns>
    public static FilterModelBuilder.Builder<TEntity> SetPaging<TEntity, TProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        int page,
        int pageSize,
        bool? condition = null)
    {
        return source.parentBuilder.SetPaging(page, pageSize, condition);
    }

    /// <summary>
    /// Sets the paging options for the filter model using a standard page size.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="page">The page number to retrieve.</param>
    /// <param name="standardPageSize">The standard page size to use.</param>
    /// <param name="condition">Optional condition to control whether paging is applied.</param>
    /// <returns>The parent <see cref="FilterModelBuilder.Builder{TEntity}"/> instance for method chaining.</returns>
    public static FilterModelBuilder.Builder<TEntity> SetPaging<TEntity, TProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        int page,
        PageSize standardPageSize,
        bool? condition = null)
    {
        return source.parentBuilder.SetPaging(page, standardPageSize, condition);
    }

    /// <summary>
    /// Configures the query to disable entity tracking.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="value">A value indicating whether entity tracking should be disabled.</param>
    /// <param name="condition">Optional condition to control whether no-tracking is applied.</param>
    /// <returns>The parent <see cref="FilterModelBuilder.Builder{TEntity}"/> instance for method chaining.</returns>
    public static FilterModelBuilder.Builder<TEntity> SetNoTracking<TEntity, TProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        bool value = true,
        bool? condition = null)
    {
        return source.parentBuilder.SetNoTracking(value, condition);
    }

    /// <summary>
    /// Adds a custom filter to the filter model.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
    /// <param name="source">The source include builder.</param>
    /// <param name="customType">The type of the custom filter to add.</param>
    /// <param name="condition">Optional condition to control whether the custom filter is applied.</param>
    /// <returns>A <see cref="FilterModelBuilder.Builder{TEntity}.CustomFilterBuilder"/> for adding parameters.</returns>
    public static FilterModelBuilder.Builder<TEntity>.CustomFilterBuilder AddCustomFilter<TEntity, TProperty>(
        this FilterModelBuilder.IncludeBuilder<TEntity, TProperty> source,
        FilterCustomType customType,
        bool? condition = null)
    {
        return source.parentBuilder.AddCustomFilter(customType, condition);
    }

    private static MemberExpression GetMemberExpression<TSource, TResult>(Expression<Func<TSource, TResult>> selector)
    {
        return selector.Body switch
        {
            UnaryExpression { Operand: MemberExpression member } => member,
            MemberExpression memberExpression => memberExpression,
            _ => null
        };
    }
}
