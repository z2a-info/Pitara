using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public class QueryInfo
    {
        public string QueryDisplayName { get; set; }
        public string QueryName { get; set; }
        public string QueryString { get; set; }
        public int ResultCount { get; set; }
        public bool IsEnabled { get { return ResultCount > 0; } }
        public bool IsVisible { get; set; } = true;
        public int Height
        {
            get
            {
                return ExtractHeight();
            }
        }
        public int ExtractHeight()
        {
            if (string.IsNullOrEmpty(QueryString))
            {
                return 0;
            }
            // string[] parts = QueryString.Split(' ');
            var queryString = QueryString;// parts[0];

            string numberStr = "";
            foreach (var item in queryString)
            {
                if (char.IsDigit(item))
                    numberStr += item;
                else
                    break;
            }
            if (string.IsNullOrEmpty(numberStr))
            {
                numberStr = "0";
            }
            return int.Parse(numberStr);

        }
    }
}
