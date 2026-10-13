namespace HeavenTool.Forms.Search
{
    public interface ISearchable
    {
        void SearchClosing();

        void Search(string search, SearchType searchType, bool searchBackwards, bool caseSensitive);
    }
}