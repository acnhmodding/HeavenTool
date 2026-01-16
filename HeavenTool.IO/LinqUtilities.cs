namespace HeavenTool.IO
{
    public static class LinqUtilities
    {
        public static void AddIfNotExist<T>(this List<T> list, T key) where T : class
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(key);

            if (!list.Contains(key))
                list.Add(key);
        }

        public static T? FirstOrNull<T>(this IEnumerable<T> sequence, Func<T, bool> predicate) where T : struct
        {
            if (sequence == null || predicate == null)
                return null;

            foreach (T item in sequence.Where(predicate))
                return item;
            return null;
        }
    }
}
