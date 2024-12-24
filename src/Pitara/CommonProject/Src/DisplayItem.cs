using Lucene.Net.Documents;
using System;
using System.Globalization;

namespace CommonProject.Src
{
    public class DisplayItem
    {
        public DisplayItem(Document doc)
        {
            Version = (doc.Get("Version") != null) ? doc.Get("Version") : string.Empty;
            FilePath = (doc.Get("PathKey") != null) ? doc.Get("PathKey") : string.Empty;
            Tags = (doc.Get("Tags") != null) ? doc.Get("Tags") : string.Empty;
            EpochTime = (doc.Get("EPOCHTIME") != null) ? doc.Get("EPOCHTIME") : string.Empty; 
            KeyWords = (doc.Get("KeyWords") != null) ? doc.Get("KeyWords") : string.Empty; 
            Location = (doc.Get("Location") != null) ? doc.Get("Location") : string.Empty;
            DateTimeKeywords = (doc.Get("DateTimeKeywords") != null) ? doc.Get("DateTimeKeywords") : string.Empty;
            ThumbNail = (doc.Get("ThumbNail") != null) ? doc.Get("ThumbNail") : string.Empty;
            if (!string.IsNullOrEmpty(EpochTime))
            {
                Heading = " " + FromEpochTime(long.Parse(EpochTime));
            }
            else
            {
                Heading = " ";
            }
        }
        public DisplayItem()
        {

        }

        private static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private string FromEpochTime(long epochTime)
        {
            if(epochTime == 0)
            {
                return "Date unavailable";
            }
            //  Wed, Nov 12, 2004
            DateTime timeClicked = _epoch.AddSeconds(epochTime);
            var montth = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(timeClicked.Month);
            var final = timeClicked.ToString("ddd") + ", " + montth + " " + timeClicked.Day + ", " + timeClicked.Year;
            return final;
        }

        public string Heading { get; set; }
        public string FilePath { get; set; }
        public string Tags { get; set; }
        public string DateTimeKeywords { get; set; }
        public string KeyWords { get; set; }
        public string Version { get; set; }

        public string ThumbNail { get; set; }
        public string Location { get; set; }
        public string EpochTime { get; set; }
        public static System.Windows.Media.Color Background = System.Windows.Media.Color.FromRgb(0, 255, 0);
    }
}
