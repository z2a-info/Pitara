// using Sdl.MultiSelectComboBox.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pitara.Services
{
    internal class KeywordFilterService // : // IFilterService
    {
        private string searchText;

        public KeywordFilterService()
        {
            Filter = GetFilter;
        }

        private bool GetFilter(object obj)
        {
            if (searchText == null) { return true; }
            if (obj == null) { return false; }
            return obj.ToString().ToLower().Contains(searchText.ToLower());
        }

        public Predicate<object> Filter { get; set; }

        public void SetFilter(string criteria)
        {
            searchText = criteria;
        }
    }
}
