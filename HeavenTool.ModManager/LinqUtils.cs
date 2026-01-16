namespace HeavenTool.ModManager
{
    public static class LinqUtils
    {
        public static T FirstOrNull<T>(this IEnumerable<T> source, Func<T, bool> predicate) where T : class
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);
            

            foreach (T element in source)
                if (predicate(element))
                    return element;

            return null;
        }

        public static T? FirstOrNullStruct<T>(this IEnumerable<T> source, Func<T, bool> predicate) where T : struct
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);


            foreach (T element in source)
                if (predicate(element))
                    return element;

            return null;
        }
    }
}
