// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static class ConditionalLinqExtensions
{
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

    public static TSource ElementAtOrDefaultIf<TSource>(this IEnumerable<TSource> source, int index, bool condition)
    {
        if (source == null)
        {
            return default;
        }

        return condition ? source.ElementAtOrDefault(index) : default;
    }

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

    public static int CountIf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, bool condition)
    {
        if (source == null)
        {
            return 0;
        }

        return condition ? source.Count(predicate) : source.Count();
    }

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

    public static IEnumerable<TSource> DistinctIf<TSource>(this IEnumerable<TSource> source, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Distinct() : source;
    }

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

    public static IEnumerable<TSource> SkipIf<TSource>(this IEnumerable<TSource> source, int count, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Skip(count) : source;
    }

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

    public static IEnumerable<TSource> TakeIf<TSource>(this IEnumerable<TSource> source, int count, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Take(count) : source;
    }

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

    public static IEnumerable<TSource> ReverseIf<TSource>(this IEnumerable<TSource> source, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.Reverse() : source;
    }

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

    public static bool AnyIf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, bool condition)
    {
        if (source == null)
        {
            return false;
        }

        return condition ? source.Any(predicate) : source.Any();
    }

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

    public static bool AllIf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, bool condition)
    {
        if (source == null)
        {
            return true;
        }

        return !condition || source.All(predicate);
    }

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

    public static List<TSource> ToListIf<TSource>(this IEnumerable<TSource> source, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.ToList() : [];
    }

    public static TSource[] ToArrayIf<TSource>(this IEnumerable<TSource> source, bool condition)
    {
        if (source == null)
        {
            return null;
        }

        return condition ? source.ToArray() : [];
    }
}