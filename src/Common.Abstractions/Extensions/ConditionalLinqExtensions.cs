// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides LINQ operators whose behavior is selected by a Boolean condition.
/// </summary>
/// <remarks>
/// Methods return null or the type's default value for a null source. An <c>If</c> method applies its optional
/// operation only when the condition is true; an <c>IfElse</c> method selects between its <c>If</c> and <c>Else</c> inputs.
/// </remarks>
public static class ConditionalLinqExtensions
{
    /// <summary>Applies a predicate when the condition is true; otherwise returns the source unchanged.</summary>
    public static IEnumerable<TSource> WhereIf<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicate,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Where(predicate) : source;
    }

    /// <summary>Filters the source with the predicate selected by the condition.</summary>
    public static IEnumerable<TSource> WhereIfElse<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicateIf,
        Func<TSource, bool> predicateElse,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Where(predicateIf) : source.Where(predicateElse);
    }

    /// <summary>Projects the source when the condition is true; otherwise casts each source item to the result type.</summary>
    public static IEnumerable<TResult> SelectIf<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, TResult> selector,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Select(selector) : source.Cast<TResult>();
    }

    /// <summary>Projects the source with the selector selected by the condition.</summary>
    public static IEnumerable<TResult> SelectIfElse<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, TResult> selectorIf,
        Func<TSource, TResult> selectorElse,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Select(selectorIf) : source.Select(selectorElse);
    }

    /// <summary>Orders by the supplied key when enabled; otherwise creates a stable ordering with a constant key.</summary>
    public static IOrderedEnumerable<TSource> OrderByIf<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.OrderBy(keySelector) : source.OrderBy(_ => default(TKey));
    }

    /// <summary>Orders the source in ascending order using the key selector selected by the condition.</summary>
    public static IOrderedEnumerable<TSource> OrderByIfElse<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelectorIf,
        Func<TSource, TKey> keySelectorElse,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.OrderBy(keySelectorIf) : source.OrderBy(keySelectorElse);
    }

    /// <summary>Orders by the supplied key in descending order when enabled; otherwise uses a constant-key ordering.</summary>
    public static IOrderedEnumerable<TSource> OrderByDescendingIf<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.OrderByDescending(keySelector) : source.OrderBy(_ => default(TKey));
    }

    /// <summary>Orders the source in descending order using the key selector selected by the condition.</summary>
    public static IOrderedEnumerable<TSource> OrderByDescendingIfElse<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelectorIf,
        Func<TSource, TKey> keySelectorElse,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.OrderByDescending(keySelectorIf) : source.OrderByDescending(keySelectorElse);
    }

    /// <summary>Adds an ascending secondary ordering when the condition is true.</summary>
    public static IOrderedEnumerable<TSource> ThenByIf<TSource, TKey>(
        this IOrderedEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.ThenBy(keySelector) : source;
    }

    /// <summary>Adds an ascending secondary ordering using the key selector selected by the condition.</summary>
    public static IOrderedEnumerable<TSource> ThenByIfElse<TSource, TKey>(
        this IOrderedEnumerable<TSource> source,
        Func<TSource, TKey> keySelectorIf,
        Func<TSource, TKey> keySelectorElse,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.ThenBy(keySelectorIf) : source.ThenBy(keySelectorElse);
    }

    /// <summary>Adds a descending secondary ordering when the condition is true.</summary>
    public static IOrderedEnumerable<TSource> ThenByDescendingIf<TSource, TKey>(
        this IOrderedEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.ThenByDescending(keySelector) : source;
    }

    /// <summary>Adds a descending secondary ordering using the key selector selected by the condition.</summary>
    public static IOrderedEnumerable<TSource> ThenByDescendingIfElse<TSource, TKey>(
        this IOrderedEnumerable<TSource> source,
        Func<TSource, TKey> keySelectorIf,
        Func<TSource, TKey> keySelectorElse,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.ThenByDescending(keySelectorIf) : source.ThenByDescending(keySelectorElse);
    }

    /// <summary>Returns the first matching item when enabled, or the first item without filtering otherwise.</summary>
    public static TSource FirstOrDefaultIf<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicate,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.FirstOrDefault(predicate) : source.FirstOrDefault();
    }

    /// <summary>Returns the first item matching the predicate selected by the condition.</summary>
    public static TSource FirstOrDefaultIfElse<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicateIf,
        Func<TSource, bool> predicateElse,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.FirstOrDefault(predicateIf) : source.FirstOrDefault(predicateElse);
    }

    /// <summary>Returns the last matching item when enabled, or the last item without filtering otherwise.</summary>
    public static TSource LastOrDefaultIf<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicate,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.LastOrDefault(predicate) : source.LastOrDefault();
    }

    /// <summary>Returns the last item matching the predicate selected by the condition.</summary>
    public static TSource LastOrDefaultIfElse<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicateIf,
        Func<TSource, bool> predicateElse,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.LastOrDefault(predicateIf) : source.LastOrDefault(predicateElse);
    }

    /// <summary>Returns the single matching item when enabled, or the unfiltered single item otherwise.</summary>
    public static TSource SingleOrDefaultIf<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicate,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.SingleOrDefault(predicate) : source.SingleOrDefault();
    }

    /// <summary>Returns the single item matching the predicate selected by the condition.</summary>
    public static TSource SingleOrDefaultIfElse<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicateIf,
        Func<TSource, bool> predicateElse,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.SingleOrDefault(predicateIf) : source.SingleOrDefault(predicateElse);
    }

    /// <summary>Returns the item at the requested index when enabled; otherwise returns the default value.</summary>
    public static TSource ElementAtOrDefaultIf<TSource>(this IEnumerable<TSource> source, int index, bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.ElementAtOrDefault(index) : default;
    }

    /// <summary>Returns the item at the index selected by the condition, or the default value when that index is absent.</summary>
    public static TSource ElementAtOrDefaultIfElse<TSource>(
        this IEnumerable<TSource> source,
        int indexIf,
        int indexElse,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.ElementAtOrDefault(indexIf) : source.ElementAtOrDefault(indexElse);
    }

    /// <summary>Counts matching items when enabled, or all source items otherwise.</summary>
    public static int CountIf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, bool condition)
    {
        if (source == null)
        {
            return 0;
        }

        return condition ? source.Count(predicate) : source.Count();
    }

    /// <summary>Counts items accepted by the predicate selected by the condition.</summary>
    public static int CountIfElse<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicateIf,
        Func<TSource, bool> predicateElse,
        bool condition)
    {
        if (source == null)
        {
            return 0;
        }

        return condition ? source.Count(predicateIf) : source.Count(predicateElse);
    }

    /// <summary>Sums projected values when enabled; otherwise returns zero without enumerating the source.</summary>
    public static double SumIf<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, double> selector,
        bool condition)
    {
        if (source == null)
        {
            return 0;
        }

        return condition ? source.Sum(selector) : 0;
    }

    /// <summary>Sums values produced by the selector selected by the condition.</summary>
    public static double SumIfElse<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, double> selectorIf,
        Func<TSource, double> selectorElse,
        bool condition)
    {
        if (source == null)
        {
            return 0;
        }

        return condition ? source.Sum(selectorIf) : source.Sum(selectorElse);
    }

    /// <summary>Averages projected values when enabled; otherwise returns zero without enumerating the source.</summary>
    public static double AverageIf<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, double> selector,
        bool condition)
    {
        if (source == null)
        {
            return 0;
        }

        return condition ? source.Average(selector) : 0;
    }

    /// <summary>Averages values produced by the selector selected by the condition.</summary>
    public static double AverageIfElse<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, double> selectorIf,
        Func<TSource, double> selectorElse,
        bool condition)
    {
        if (source == null)
        {
            return 0;
        }

        return condition ? source.Average(selectorIf) : source.Average(selectorElse);
    }

    /// <summary>Returns the maximum projected value when enabled; otherwise returns the default result value.</summary>
    public static TResult MaxIf<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, TResult> selector,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.Max(selector) : default;
    }

    /// <summary>Returns the maximum value produced by the selector selected by the condition.</summary>
    public static TResult MaxIfElse<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, TResult> selectorIf,
        Func<TSource, TResult> selectorElse,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.Max(selectorIf) : source.Max(selectorElse);
    }

    /// <summary>Returns the minimum projected value when enabled; otherwise returns the default result value.</summary>
    public static TResult MinIf<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, TResult> selector,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.Min(selector) : default;
    }

    /// <summary>Returns the minimum value produced by the selector selected by the condition.</summary>
    public static TResult MinIfElse<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, TResult> selectorIf,
        Func<TSource, TResult> selectorElse,
        bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.Min(selectorIf) : source.Min(selectorElse);
    }

    /// <summary>Removes duplicate values when enabled; otherwise returns the source unchanged.</summary>
    public static IEnumerable<TSource> DistinctIf<TSource>(this IEnumerable<TSource> source, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Distinct() : source;
    }

    /// <summary>Removes duplicate values using the equality comparer selected by the condition.</summary>
    public static IEnumerable<TSource> DistinctIfElse<TSource>(
        this IEnumerable<TSource> source,
        IEqualityComparer<TSource> comparerIf,
        IEqualityComparer<TSource> comparerElse,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Distinct(comparerIf) : source.Distinct(comparerElse);
    }

    /// <summary>Returns the set union with a second sequence when enabled; otherwise returns the first sequence unchanged.</summary>
    public static IEnumerable<TSource> UnionIf<TSource>(
        this IEnumerable<TSource> first,
        IEnumerable<TSource> second,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Union(second) : first;
    }

    /// <summary>Returns the set union with the second sequence selected by the condition.</summary>
    public static IEnumerable<TSource> UnionIfElse<TSource>(
        this IEnumerable<TSource> first,
        IEnumerable<TSource> secondIf,
        IEnumerable<TSource> secondElse,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Union(secondIf) : first.Union(secondElse);
    }

    /// <summary>Returns the set intersection with a second sequence when enabled; otherwise returns the first sequence unchanged.</summary>
    public static IEnumerable<TSource> IntersectIf<TSource>(
        this IEnumerable<TSource> first,
        IEnumerable<TSource> second,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Intersect(second) : first;
    }

    /// <summary>Returns the set intersection with the second sequence selected by the condition.</summary>
    public static IEnumerable<TSource> IntersectIfElse<TSource>(
        this IEnumerable<TSource> first,
        IEnumerable<TSource> secondIf,
        IEnumerable<TSource> secondElse,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Intersect(secondIf) : first.Intersect(secondElse);
    }

    /// <summary>Removes values found in a second sequence when enabled; otherwise returns the first sequence unchanged.</summary>
    public static IEnumerable<TSource> ExceptIf<TSource>(
        this IEnumerable<TSource> first,
        IEnumerable<TSource> second,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Except(second) : first;
    }

    /// <summary>Removes values found in the second sequence selected by the condition.</summary>
    public static IEnumerable<TSource> ExceptIfElse<TSource>(
        this IEnumerable<TSource> first,
        IEnumerable<TSource> secondIf,
        IEnumerable<TSource> secondElse,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Except(secondIf) : first.Except(secondElse);
    }

    /// <summary>Skips the requested leading items when enabled; otherwise returns the source unchanged.</summary>
    public static IEnumerable<TSource> SkipIf<TSource>(this IEnumerable<TSource> source, int count, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Skip(count) : source;
    }

    /// <summary>Skips the number of leading items selected by the condition.</summary>
    public static IEnumerable<TSource> SkipIfElse<TSource>(
        this IEnumerable<TSource> source,
        int countIf,
        int countElse,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Skip(countIf) : source.Skip(countElse);
    }

    /// <summary>Takes the requested number of leading items when enabled; otherwise returns the source unchanged.</summary>
    public static IEnumerable<TSource> TakeIf<TSource>(this IEnumerable<TSource> source, int count, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Take(count) : source;
    }

    /// <summary>Takes the number of leading items selected by the condition.</summary>
    public static IEnumerable<TSource> TakeIfElse<TSource>(
        this IEnumerable<TSource> source,
        int countIf,
        int countElse,
        bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Take(countIf) : source.Take(countElse);
    }

    /// <summary>Zips two sequences when enabled; otherwise returns an empty result sequence.</summary>
    public static IEnumerable<TResult> ZipIf<TFirst, TSecond, TResult>(
        this IEnumerable<TFirst> first,
        IEnumerable<TSecond> second,
        Func<TFirst, TSecond, TResult> resultSelector,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Zip(second, resultSelector) : [];
    }

    /// <summary>Zips the first sequence with the second sequence and result selector selected by the condition.</summary>
    public static IEnumerable<TResult> ZipIfElse<TFirst, TSecond, TResult>(
        this IEnumerable<TFirst> first,
        IEnumerable<TSecond> secondIf,
        IEnumerable<TSecond> secondElse,
        Func<TFirst, TSecond, TResult> resultSelectorIf,
        Func<TFirst, TSecond, TResult> resultSelectorElse,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Zip(secondIf, resultSelectorIf) : first.Zip(secondElse, resultSelectorElse);
    }

    /// <summary>Joins matching keys from two sequences when enabled; otherwise returns an empty result sequence.</summary>
    public static IEnumerable<TResult> JoinIf<TOuter, TInner, TKey, TResult>(
        this IEnumerable<TOuter> outer,
        IEnumerable<TInner> inner,
        Func<TOuter, TKey> outerKeySelector,
        Func<TInner, TKey> innerKeySelector,
        Func<TOuter, TInner, TResult> resultSelector,
        bool condition)
    {
        if (outer == null)
        {
            return null;
        }

        return condition ? outer.Join(inner, outerKeySelector, innerKeySelector, resultSelector) : [];
    }

    /// <summary>Joins the outer sequence using the inner sequence, key selectors, and result selector selected by the condition.</summary>
    public static IEnumerable<TResult> JoinIfElse<TOuter, TInner, TKey, TResult>(
        this IEnumerable<TOuter> outer,
        IEnumerable<TInner> innerIf,
        IEnumerable<TInner> innerElse,
        Func<TOuter, TKey> outerKeySelectorIf,
        Func<TOuter, TKey> outerKeySelectorElse,
        Func<TInner, TKey> innerKeySelectorIf,
        Func<TInner, TKey> innerKeySelectorElse,
        Func<TOuter, TInner, TResult> resultSelectorIf,
        Func<TOuter, TInner, TResult> resultSelectorElse,
        bool condition)
    {
        if (outer == null)
        {
            return null;
        }

        return condition
            ? outer.Join(innerIf, outerKeySelectorIf, innerKeySelectorIf, resultSelectorIf)
            : outer.Join(innerElse, outerKeySelectorElse, innerKeySelectorElse, resultSelectorElse);
    }

    /// <summary>Group-joins matching keys from two sequences when enabled; otherwise returns an empty result sequence.</summary>
    public static IEnumerable<TResult> GroupJoinIf<TOuter, TInner, TKey, TResult>(
        this IEnumerable<TOuter> outer,
        IEnumerable<TInner> inner,
        Func<TOuter, TKey> outerKeySelector,
        Func<TInner, TKey> innerKeySelector,
        Func<TOuter, IEnumerable<TInner>, TResult> resultSelector,
        bool condition)
    {
        if (outer == null)
        {
            return null;
        }

        return condition ? outer.GroupJoin(inner, outerKeySelector, innerKeySelector, resultSelector) : [];
    }

    /// <summary>Group-joins the outer sequence using the inner sequence, key selectors, and result selector selected by the condition.</summary>
    public static IEnumerable<TResult> GroupJoinIfElse<TOuter, TInner, TKey, TResult>(
        this IEnumerable<TOuter> outer,
        IEnumerable<TInner> innerIf,
        IEnumerable<TInner> innerElse,
        Func<TOuter, TKey> outerKeySelectorIf,
        Func<TOuter, TKey> outerKeySelectorElse,
        Func<TInner, TKey> innerKeySelectorIf,
        Func<TInner, TKey> innerKeySelectorElse,
        Func<TOuter, IEnumerable<TInner>, TResult> resultSelectorIf,
        Func<TOuter, IEnumerable<TInner>, TResult> resultSelectorElse,
        bool condition)
    {
        if (outer == null)
        {
            return null;
        }

        return condition
            ? outer.GroupJoin(innerIf, outerKeySelectorIf, innerKeySelectorIf, resultSelectorIf)
            : outer.GroupJoin(innerElse, outerKeySelectorElse, innerKeySelectorElse, resultSelectorElse);
    }

    /// <summary>Reverses the source order when enabled; otherwise returns the source unchanged.</summary>
    public static IEnumerable<TSource> ReverseIf<TSource>(this IEnumerable<TSource> source, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Reverse() : source;
    }

    /// <summary>Appends a second sequence when enabled; otherwise returns the first sequence unchanged.</summary>
    public static IEnumerable<TSource> ConcatIf<TSource>(
        this IEnumerable<TSource> first,
        IEnumerable<TSource> second,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Concat(second) : first;
    }

    /// <summary>Appends the second sequence selected by the condition.</summary>
    public static IEnumerable<TSource> ConcatIfElse<TSource>(
        this IEnumerable<TSource> first,
        IEnumerable<TSource> secondIf,
        IEnumerable<TSource> secondElse,
        bool condition)
    {
        if (first == null)
        {
            return null;
        }

        return condition ? first.Concat(secondIf) : first.Concat(secondElse);
    }

    /// <summary>Tests for a matching item when enabled, or for any item without filtering otherwise.</summary>
    public static bool AnyIf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, bool condition)
    {
        if (source == null)
        {
            return false;
        }

        return condition ? source.Any(predicate) : source.Any();
    }

    /// <summary>Tests whether any item satisfies the predicate selected by the condition.</summary>
    public static bool AnyIfElse<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicateIf,
        Func<TSource, bool> predicateElse,
        bool condition)
    {
        if (source == null)
        {
            return false;
        }

        return condition ? source.Any(predicateIf) : source.Any(predicateElse);
    }

    /// <summary>Tests all items against a predicate when enabled; otherwise returns <see langword="true"/> without enumeration.</summary>
    public static bool AllIf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, bool condition)
    {
        if (source == null)
        {
            return true;
        }

        return !condition || source.All(predicate);
    }

    /// <summary>Tests whether all items satisfy the predicate selected by the condition.</summary>
    public static bool AllIfElse<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicateIf,
        Func<TSource, bool> predicateElse,
        bool condition)
    {
        if (source == null)
        {
            return true;
        }

        return condition ? source.All(predicateIf) : source.All(predicateElse);
    }

    /// <summary>Materializes the source into a list when enabled; otherwise returns an empty list.</summary>
    public static List<TSource> ToListIf<TSource>(this IEnumerable<TSource> source, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.ToList() : [];
    }

    /// <summary>Materializes the source into an array when enabled; otherwise returns an empty array.</summary>
    public static TSource[] ToArrayIf<TSource>(this IEnumerable<TSource> source, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.ToArray() : [];
    }
}
