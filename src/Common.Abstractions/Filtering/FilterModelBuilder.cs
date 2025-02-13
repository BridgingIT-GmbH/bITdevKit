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
        private readonly FilterModel filterModel = model;

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
                Field = memberExpression.Member.Name,
                Operator = filterOperator,
                Value = value
            };

            this.filterModel.Filters ??= [];
            this.filterModel.Filters.Add(criteria);

            return this;
        }

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
                Field = memberExpression.Member.Name,
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
                return new CustomFilterBuilder(new(), this);
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
}