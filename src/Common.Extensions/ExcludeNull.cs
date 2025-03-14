namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using System.Collections.ObjectModel;

public static partial class Extensions
{
    /// <summary>
    ///     Removes null values from any collection implementing IEnumerable and returns non-nullable type.
    ///     Returns empty enumerable if source is null.
    /// </summary>
    public static IEnumerable<T> ExcludeNull<T>(this IEnumerable<T> source)
        where T : class
    {
        return source.IsNullOrEmpty()
            ? []
            : source.Where(x => x != null);
    }

    /// <summary>
    ///     Removes null values from any collection of nullable value types.
    ///     Returns empty enumerable if source is null.
    /// </summary>
    public static IEnumerable<T> ExcludeNull<T>(this IEnumerable<T?> source) where T : struct
    {
        return source.IsNullOrEmpty()
            ? []
            : source.Where(x => x.HasValue).Select(x => x.Value);
    }

    /// <summary>
    ///     Removes null values and returns a new List.
    ///     Returns empty list if source is null.
    /// </summary>
    public static List<T> ExcludeNullAsList<T>(this IEnumerable<T> source) where T : class
    {
        return source.IsNullOrEmpty()
            ? []
            : source.Where(x => x != null).ToList();
    }

    /// <summary>
    ///     Removes entries with null values from a dictionary and returns a new dictionary.
    ///     Returns empty dictionary if source is null.
    /// </summary>
    public static IDictionary<TKey, TValue> ExcludeNull<TKey, TValue>(this IDictionary<TKey, TValue> source)
        where TKey : notnull
        where TValue : class
    {
        return source.IsNullOrEmpty()
            ? []
            : source.Where(x => x.Value != null)
                .ToDictionary(x => x.Key, x => x.Value);
    }

    ///// <summary>
    /////     Removes null values from a Stack and returns a new Stack.
    /////     Returns empty stack if source is null.
    ///// </summary>
    //public static Stack<T> ExcludeNull<T>(this Stack<T> source) where T : class
    //{
    //    return source == null
    //        ? []
    //        : [.. source.Where(x => x != null).Reverse()];
    //}

    ///// <summary>
    /////     Removes null values from a Queue and returns a new Queue.
    /////     Returns empty queue if source is null.
    ///// </summary>
    //public static Queue<T> ExcludeNull<T>(this Queue<T> source) where T : class
    //{
    //    return source.IsNullOrEmpty()
    //        ? []
    //        : [.. source.Where(x => x != null)];
    //}

    /// <summary>
    ///     Removes null values from a LinkedList and returns a new LinkedList.
    ///     Returns empty linked list if source is null.
    /// </summary>
    public static LinkedList<T> ExcludeNull<T>(this LinkedList<T> source) where T : class
    {
        return source.IsNullOrEmpty()
            ? []
            : [.. source.Where(x => x != null)];
    }

    /// <summary>
    ///     Removes null values from an array and returns a new array.
    ///     Returns empty array if source is null.
    /// </summary>
    public static T[] ExcludeNull<T>(this T[] source) where T : class
    {
        return source.IsNullOrEmpty()
            ? []
            : source.Where(x => x != null).ToArray();
    }

    /// <summary>
    ///     Removes null values from a HashSet and returns a new HashSet.
    ///     Returns empty hash set if source is null.
    /// </summary>
    public static HashSet<T> ExcludeNull<T>(this HashSet<T> source) where T : class
    {
        return source.IsNullOrEmpty()
            ? []
            : [.. source.Where(x => x != null)];
    }

    /// <summary>
    ///     Removes null values from a SortedSet and returns a new SortedSet.
    ///     Returns empty sorted set if source is null.
    /// </summary>
    public static SortedSet<T> ExcludeNull<T>(this SortedSet<T> source) where T : class
    {
        return source.IsNullOrEmpty()
            ? []
            : [.. source.Where(x => x != null)];
    }

    /// <summary>
    ///     Removes null values from an ObservableCollection and returns a new ObservableCollection.
    ///     Returns empty observable collection if source is null.
    /// </summary>
    public static ObservableCollection<T> ExcludeNull<T>(this ObservableCollection<T> source) where T : class
    {
        return source.IsNullOrEmpty()
            ? []
            : [.. source.Where(x => x != null)];
    }

    /// <summary>
    ///     Removes null values from a ConcurrentBag and returns a new ConcurrentBag.
    ///     Returns empty concurrent bag if source is null.
    /// </summary>
    public static ConcurrentBag<T> ExcludeNull<T>(this ConcurrentBag<T> source) where T : class
    {
        return source.IsNullOrEmpty()
            ? []
            : [.. source.Where(x => x != null)];
    }
}