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
    extension<TSource>(IEnumerable<TSource> source)
    {
        /// <summary>Applies a predicate when the condition is true; otherwise returns the source unchanged.</summary>
        public IEnumerable<TSource> WhereIf(
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
        public IEnumerable<TSource> WhereIfElse(
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
        public IEnumerable<TResult> SelectIf<TResult>(
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
        public IEnumerable<TResult> SelectIfElse<TResult>(
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
        public IOrderedEnumerable<TSource> OrderByIf<TKey>(
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
        public IOrderedEnumerable<TSource> OrderByIfElse<TKey>(
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
        public IOrderedEnumerable<TSource> OrderByDescendingIf<TKey>(
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
        public IOrderedEnumerable<TSource> OrderByDescendingIfElse<TKey>(
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
    }

    extension<TSource>(IOrderedEnumerable<TSource> source)
    {
        /// <summary>Adds an ascending secondary ordering when the condition is true.</summary>
        public IOrderedEnumerable<TSource> ThenByIf<TKey>(
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
        public IOrderedEnumerable<TSource> ThenByIfElse<TKey>(
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
        public IOrderedEnumerable<TSource> ThenByDescendingIf<TKey>(
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
        public IOrderedEnumerable<TSource> ThenByDescendingIfElse<TKey>(
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
    }

    extension<TSource>(IEnumerable<TSource> source)
    {
        /// <summary>Returns the first matching item when enabled, or the first item without filtering otherwise.</summary>
        public TSource FirstOrDefaultIf(
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
        public TSource FirstOrDefaultIfElse(
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
        public TSource LastOrDefaultIf(
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
        public TSource LastOrDefaultIfElse(
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
        public TSource SingleOrDefaultIf(
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
        public TSource SingleOrDefaultIfElse(
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
        public TSource ElementAtOrDefaultIf(int index, bool condition)
        {
            if (source == null)
            {
                return default;
            }

            return condition ? source.ElementAtOrDefault(index) : default;
        }

        /// <summary>Returns the item at the index selected by the condition, or the default value when that index is absent.</summary>
        public TSource ElementAtOrDefaultIfElse(
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
        public int CountIf(Func<TSource, bool> predicate, bool condition)
        {
            if (source == null)
            {
                return 0;
            }

            return condition ? source.Count(predicate) : source.Count();
        }

        /// <summary>Counts items accepted by the predicate selected by the condition.</summary>
        public int CountIfElse(
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
        public double SumIf(
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
        public double SumIfElse(
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
        public double AverageIf(
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
        public double AverageIfElse(
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
        public TResult MaxIf<TResult>(
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
        public TResult MaxIfElse<TResult>(
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
        public TResult MinIf<TResult>(
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
        public TResult MinIfElse<TResult>(
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
        public IEnumerable<TSource> DistinctIf(bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Distinct() : source;
        }

        /// <summary>Removes duplicate values using the equality comparer selected by the condition.</summary>
        public IEnumerable<TSource> DistinctIfElse(
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
        public IEnumerable<TSource> UnionIf(
            IEnumerable<TSource> second,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Union(second) : source;
        }

        /// <summary>Returns the set union with the second sequence selected by the condition.</summary>
        public IEnumerable<TSource> UnionIfElse(
            IEnumerable<TSource> secondIf,
            IEnumerable<TSource> secondElse,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Union(secondIf) : source.Union(secondElse);
        }

        /// <summary>Returns the set intersection with a second sequence when enabled; otherwise returns the first sequence unchanged.</summary>
        public IEnumerable<TSource> IntersectIf(
            IEnumerable<TSource> second,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Intersect(second) : source;
        }

        /// <summary>Returns the set intersection with the second sequence selected by the condition.</summary>
        public IEnumerable<TSource> IntersectIfElse(
            IEnumerable<TSource> secondIf,
            IEnumerable<TSource> secondElse,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Intersect(secondIf) : source.Intersect(secondElse);
        }

        /// <summary>Removes values found in a second sequence when enabled; otherwise returns the first sequence unchanged.</summary>
        public IEnumerable<TSource> ExceptIf(
            IEnumerable<TSource> second,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Except(second) : source;
        }

        /// <summary>Removes values found in the second sequence selected by the condition.</summary>
        public IEnumerable<TSource> ExceptIfElse(
            IEnumerable<TSource> secondIf,
            IEnumerable<TSource> secondElse,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Except(secondIf) : source.Except(secondElse);
        }

        /// <summary>Skips the requested leading items when enabled; otherwise returns the source unchanged.</summary>
        public IEnumerable<TSource> SkipIf(int count, bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Skip(count) : source;
        }

        /// <summary>Skips the number of leading items selected by the condition.</summary>
        public IEnumerable<TSource> SkipIfElse(
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
        public IEnumerable<TSource> TakeIf(int count, bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Take(count) : source;
        }

        /// <summary>Takes the number of leading items selected by the condition.</summary>
        public IEnumerable<TSource> TakeIfElse(
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
        public IEnumerable<TResult> ZipIf<TSecond, TResult>(
            IEnumerable<TSecond> second,
            Func<TSource, TSecond, TResult> resultSelector,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Zip(second, resultSelector) : [];
        }

        /// <summary>Zips the first sequence with the second sequence and result selector selected by the condition.</summary>
        public IEnumerable<TResult> ZipIfElse<TSecond, TResult>(
            IEnumerable<TSecond> secondIf,
            IEnumerable<TSecond> secondElse,
            Func<TSource, TSecond, TResult> resultSelectorIf,
            Func<TSource, TSecond, TResult> resultSelectorElse,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Zip(secondIf, resultSelectorIf) : source.Zip(secondElse, resultSelectorElse);
        }

        /// <summary>Joins matching keys from two sequences when enabled; otherwise returns an empty result sequence.</summary>
        public IEnumerable<TResult> JoinIf<TInner, TKey, TResult>(
            IEnumerable<TInner> inner,
            Func<TSource, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TSource, TInner, TResult> resultSelector,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Join(inner, outerKeySelector, innerKeySelector, resultSelector) : [];
        }

        /// <summary>Joins the outer sequence using the inner sequence, key selectors, and result selector selected by the condition.</summary>
        public IEnumerable<TResult> JoinIfElse<TInner, TKey, TResult>(
            IEnumerable<TInner> innerIf,
            IEnumerable<TInner> innerElse,
            Func<TSource, TKey> outerKeySelectorIf,
            Func<TSource, TKey> outerKeySelectorElse,
            Func<TInner, TKey> innerKeySelectorIf,
            Func<TInner, TKey> innerKeySelectorElse,
            Func<TSource, TInner, TResult> resultSelectorIf,
            Func<TSource, TInner, TResult> resultSelectorElse,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition
                ? source.Join(innerIf, outerKeySelectorIf, innerKeySelectorIf, resultSelectorIf)
                : source.Join(innerElse, outerKeySelectorElse, innerKeySelectorElse, resultSelectorElse);
        }

        /// <summary>Group-joins matching keys from two sequences when enabled; otherwise returns an empty result sequence.</summary>
        public IEnumerable<TResult> GroupJoinIf<TInner, TKey, TResult>(
            IEnumerable<TInner> inner,
            Func<TSource, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TSource, IEnumerable<TInner>, TResult> resultSelector,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.GroupJoin(inner, outerKeySelector, innerKeySelector, resultSelector) : [];
        }

        /// <summary>Group-joins the outer sequence using the inner sequence, key selectors, and result selector selected by the condition.</summary>
        public IEnumerable<TResult> GroupJoinIfElse<TInner, TKey, TResult>(
            IEnumerable<TInner> innerIf,
            IEnumerable<TInner> innerElse,
            Func<TSource, TKey> outerKeySelectorIf,
            Func<TSource, TKey> outerKeySelectorElse,
            Func<TInner, TKey> innerKeySelectorIf,
            Func<TInner, TKey> innerKeySelectorElse,
            Func<TSource, IEnumerable<TInner>, TResult> resultSelectorIf,
            Func<TSource, IEnumerable<TInner>, TResult> resultSelectorElse,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition
                ? source.GroupJoin(innerIf, outerKeySelectorIf, innerKeySelectorIf, resultSelectorIf)
                : source.GroupJoin(innerElse, outerKeySelectorElse, innerKeySelectorElse, resultSelectorElse);
        }

        /// <summary>Reverses the source order when enabled; otherwise returns the source unchanged.</summary>
        public IEnumerable<TSource> ReverseIf(bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Reverse() : source;
        }

        /// <summary>Appends a second sequence when enabled; otherwise returns the first sequence unchanged.</summary>
        public IEnumerable<TSource> ConcatIf(
            IEnumerable<TSource> second,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Concat(second) : source;
        }

        /// <summary>Appends the second sequence selected by the condition.</summary>
        public IEnumerable<TSource> ConcatIfElse(
            IEnumerable<TSource> secondIf,
            IEnumerable<TSource> secondElse,
            bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.Concat(secondIf) : source.Concat(secondElse);
        }

        /// <summary>Tests for a matching item when enabled, or for any item without filtering otherwise.</summary>
        public bool AnyIf(Func<TSource, bool> predicate, bool condition)
        {
            if (source == null)
            {
                return false;
            }

            return condition ? source.Any(predicate) : source.Any();
        }

        /// <summary>Tests whether any item satisfies the predicate selected by the condition.</summary>
        public bool AnyIfElse(
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
        public bool AllIf(Func<TSource, bool> predicate, bool condition)
        {
            if (source == null)
            {
                return true;
            }

            return !condition || source.All(predicate);
        }

        /// <summary>Tests whether all items satisfy the predicate selected by the condition.</summary>
        public bool AllIfElse(
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
        public List<TSource> ToListIf(bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.ToList() : [];
        }

        /// <summary>Materializes the source into an array when enabled; otherwise returns an empty array.</summary>
        public TSource[] ToArrayIf(bool condition)
        {
            if (source == null)
            {
                return null;
            }

            return condition ? source.ToArray() : [];
        }
    }
}
