namespace HeavenTool.IO;

public static class LinqUtilities
{
    /// <summary>
    /// Add an element to the list if it does not already exist in the list.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list">List that will receive the value</param>
    /// <param name="value">Value to add into the target list</param>
    public static void AddIfNotExist<T>(this List<T> list, T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(value);

        if (!list.Contains(value))
            list.Add(value);
    }

    /// <summary>
    /// Return the first element of a sequence that satisfies a specified condition or null if no such element is found.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sequence"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static T? FirstOrNull<T>(this IEnumerable<T> sequence, Func<T, bool> predicate) where T : struct
    {
        if (sequence == null || predicate == null)
            return null;

        foreach (T item in sequence.Where(predicate))
            return item;
        return null;
    }
}
