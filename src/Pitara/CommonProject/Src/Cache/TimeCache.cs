// using Pitara.TagStuff;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommonProject.Src.Cache
{
    public class Month
    {
        public string Name;
        public Dictionary<string, string> Photos = new Dictionary<string, string>();
    }

    public class TimeCache : BaseThreadSafeFileCache<Month>
    {
        private ILogger _logger;

        public TimeCache(string cacheFileName, ILogger logger, AppSettings appSettings)
        :base(cacheFileName, logger, appSettings)
        {
            _logger = logger;
        }

        internal IEnumerable<string> ListAllYears()
        {
            var years = DataKeyPairDictionary
                .Select(x => x.Value)
                .Select(x => x.Photos)
                .Select(x => x.Keys.ToList())
                .SelectMany(x => x)
                .Distinct()
                .OrderBy(x => x);
            return years;
        }
        internal IEnumerable<string> ListAllMonths()
        {
            var months = DataKeyPairDictionary
                .Select(x => x.Key)
                .Distinct()
                .OrderBy(x => x);
            return months;
        }

        internal void Add(string month, string year)
        {
            // await Task.Run(() => {
                try
                {
                    if (this.DataKeyPairDictionary.ContainsKey(month))
                    {
                        var monthTemp = this.DataKeyPairDictionary[month];
                        if (!monthTemp.Photos.ContainsKey(year))
                        {
                            monthTemp.Photos.Add(year, "x");
                        }
                    }
                    else
                    {
                        var monthLocal = new Month();
                        monthLocal.Name = month;
                        monthLocal.Photos.Add(year, "x");
                        this.DataKeyPairDictionary.Add(month, monthLocal);
                    }

                }
                catch (Exception ex)
                {

                    _logger.SendLogAsync("Time add key isssue: " + ex.Message);
                _logger.SendLogWithException("Time add key isssue: ", ex);


                    throw;
                }
            // });
        }
    }
}
