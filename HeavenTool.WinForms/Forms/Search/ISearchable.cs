using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HeavenTool.Forms.Search
{
    public interface ISearchable
    {
        void SearchClosing();

        void Search(string search, SearchType searchType, bool searchBackwards, bool caseSensitive);
    }
}
