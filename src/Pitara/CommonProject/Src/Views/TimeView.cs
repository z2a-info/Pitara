using CommonProject.Src.Cache;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CommonProject.Src.Views
{
    public class YearlyRow
    {
        public string Name { get; set; }
        public List<QueryInfo> MonthsQueryInfo { get; set; } = new List<QueryInfo>();
        public string ColumnWidth { get; set; }
    }

    public class TimeView
    {
        private TimeCache _cache;// = new TimeCache(AppSettings.GetGlobalSettings().TimeDBFileName, _logger);
        private static int _maxrecommendations = 1000;
        private static ILogger _logger;// = AsyncLog.GetGlobalLogger();
        private ILuceneService _luceneService;// = new LuceneService(null);
        private List<YearlyRow> _recommendation = new List<YearlyRow>();
        private AppSettings _appSettings;
        public TimeView(ILogger logger, ILuceneService luceneService, AppSettings appSettings)
        {
            _appSettings = appSettings;
            _cache = new TimeCache(_appSettings.TimeDBFileName, _logger, _appSettings);
            _logger = logger;
            _luceneService = luceneService;
        }
        public TimeCache GetCache()
        {
            return _cache;
        }

        private async Task ComputeRecommendations()
        {
            _recommendation.Clear();
            await _cache.LoadAsync();
            var years = _cache.DataKeyPairDictionary
                .Select(x => x.Value)
                .Select(x => x.Photos)
                .Select(x => x.Keys.ToList())
                .SelectMany(x => x)
                .Distinct()
                .OrderByDescending(x => x);

            foreach (var year in years)
            {
                var yearRow = await ComputeYearlyRowAsync(year);
                if(yearRow!= null)
                {
                    _recommendation.Add(yearRow);
                }
            }
        }

        private async Task<YearlyRow> ComputeYearlyRowAsync(string year)
        {
            Dictionary<string, string> sampleQueries = new Dictionary<string, string>();
            for (int i = 1; i <= 12; i++)
            {
                string monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i);
                sampleQueries.Add(monthName, $"{year} {monthName}");
            }
            var monthQueryDict = await TransformToRecommendationsAsync(sampleQueries);
            if(monthQueryDict.Count > 0 )
            {
                return new YearlyRow()
                {
                    ColumnWidth = "300",
                    Name = year,
                    MonthsQueryInfo = monthQueryDict.Values.Select(x => x).ToList()
                };
            }
            else
            {
                return null;
            }
        }

        public async Task<Dictionary<string, QueryInfo>> TransformToRecommendationsAsync(Dictionary<string, string> sampleQueries)
        {
            Dictionary<string, QueryInfo> results = new Dictionary<string, QueryInfo>();
            int monthIndex = 1;
            foreach (var query in sampleQueries)
            {
                var info = await _luceneService.GetQueryInfoAsync(query.Key, query.Value);
                info.QueryDisplayName =
                    $"{CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(monthIndex)}: ({info.ResultCount})";

                if (info.ResultCount > 0)
                {
                    results.Add(info.QueryName, info);
                }
                else
                {
                    info.QueryDisplayName = string.Empty;
                    results.Add(info.QueryName, info);
                }
                if (results.Count == _maxrecommendations)
                {
                    break;
                }
                monthIndex++;
            }
            return results;
        }

        public async Task Reset()
        {
            _recommendation.Clear();
            await _cache.Reset();
        }

        public async Task<List<YearlyRow>> GetTopRecommendationsYearAsync()
        {
            try
            {
                await ComputeRecommendations();
                return _recommendation;
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"YearlyView GetTopRecommendationsYearAsync. Error:{ex.Message}");
                return _recommendation;
            }
        }
    }
}