// using Pitara.PhotoStuff;
// using Pitara.ViewModel;
using CommonProject.Src.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommonProject.Src.Views
{
    public class FestivalsView
    {
        private TimeCache _cache;// = new TimeCache(AppSettings.GetGlobalSettings().TimeDBFileName, _logger);
        private static int _maxrecommendations = 1000;
        private static ILogger _logger;// = AsyncLog.GetGlobalLogger();
        private ILuceneService _luceneService;// = new LuceneService(null);
        private List<YearlyRow> _recommendation = new List<YearlyRow>();
        private IEnumerable<HeaderInfo> _headerInfo = new List<HeaderInfo>();
        private AppSettings _appSettings;

        public List<YearlyRow> Recommendation 
        { 
            get => _recommendation; 
            set => _recommendation = value; 
        }

        public IEnumerable<HeaderInfo> Headers 
        { 
            get => _headerInfo; 
            set => _headerInfo = value;
        }

        public FestivalsView(TimeCache cache, ILogger logger, ILuceneService luceneService, AppSettings appSettings)
        {
            _appSettings = appSettings;
            _cache = cache; 
            _logger = logger;
            _luceneService = luceneService;
        }
        private List<string> GetFestivalNames()
        {
            var months = _cache.DataKeyPairDictionary
                .Select(x => x.Value)
                .Select(x => x.Name)
                .Distinct()
                .OrderBy(x => x);
            return months.ToList();
        }
        private async Task ComputeRecommendations()
        {
            Recommendation.Clear();
            TimeCache timeCache = new TimeCache(_appSettings.TimeDBFileName, _logger, _appSettings);
            await timeCache.LoadAsync();
            var yearsUserHavePhotos = timeCache.ListAllYears();

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
                // List only years that's in time cache.
                if (!yearsUserHavePhotos.Contains(year))
                {
                    continue;
                }
                Dictionary<string, string> sampleQueries = new Dictionary<string, string>();
                var festivales = GetFestivalNames();
                foreach (var festival in festivales)
                {
                    var dateQuery = GetFestivalDate(year, festival);
                    //if(!string.IsNullOrEmpty(dateQuery))
                    {
                        if (sampleQueries.ContainsKey(festival))
                        {
                            _logger.SendLogAsync($"Query Name should be unique. Already contains:{festival}");
                        }
                        else
                        {
                            sampleQueries.Add(festival, dateQuery);
                        }
                    }
                }
                var monthQueryDict = await TransformToRecommendationsAsync(sampleQueries);
                // If at least one month has result count > 0 then only add the year
                var count = monthQueryDict.Where(x => x.Value.ResultCount > 0);

                if(count.Count() > 0)
                {
                    Recommendation.Add(
                        new YearlyRow()
                        {
                            ColumnWidth = "450",
                            Name = year,
                            MonthsQueryInfo = monthQueryDict.Values.Select(x => x).ToList()
                        });
                }
            }
        }

        private string GetFestivalDate(string year, string festival)
        {
            try
            {
                var yearDict = _cache.DataKeyPairDictionary
                    .Select(x => x.Value)
                    .Where(x => x.Name.Equals(festival))
                    .Select(x => x.Photos).ToList().FirstOrDefault();

                if (yearDict != null && yearDict.ContainsKey(year))
                {
                    // String is in the form of March 21
                    string[] parts = yearDict[year].Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                    int day = Int32.Parse(parts[1].Trim());
                    string dayString = Utils.DayString(day); 
                    return $"{year} {parts[0].Trim()} {dayString}";
                }
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"Parsing error at festival file:{_cache.FilePath}. Error:{ex.Message}");
            }
            return string.Empty;
        }

        //private async Task<YearlyRow> ComputeYearlyRowAsync(string year, List<string> festivals, string date)
        //{
        //    Dictionary<string, string> sampleQueries = new Dictionary<string, string>();
        //    foreach (var festival in festivals)
        //    {
        //        sampleQueries.AddCommon(festival, $"{year} {festival}");
        //    }
        //    var monthQueryDict = await TransformToRecommendationsAsync(sampleQueries);
        //    return new YearlyRow()
        //    {
        //        ColumnWidth = "300",
        //        Name = year,
        //        MonthsQueryInfo = monthQueryDict.Values.Select(x => x).ToList()
        //    };
        //}

        public async Task<Dictionary<string, QueryInfo>> TransformToRecommendationsAsync(Dictionary<string, string> sampleQueries)
        {
            Dictionary<string, QueryInfo> results = new Dictionary<string, QueryInfo>();
            foreach (var query in sampleQueries)
            {
                var info = await _luceneService.GetQueryInfoAsync(query.Key, query.Value);
                
                info.QueryDisplayName =
                    $"{query.Key}: ({info.ResultCount})";
                if (info.ResultCount > 0)
                {
                    results.Add(query.Key, info);
                }
                else
                {
                    info.QueryDisplayName = string.Empty;
                    results.Add(query.Key, info);
                }
                if (results.Count == _maxrecommendations)
                {
                    break;
                }
                // monthIndex++;
            }
            return results;
        }

        internal async Task Reset()
        {
            Recommendation.Clear();
            await _cache.Reset();
        }

        public async Task<List<YearlyRow>> GetTopRecommendationsYearAsync()
        {
            try
            {
                await ComputeRecommendations();
                return Recommendation;
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"YearlyView GetTopRecommendationsYearAsync. Error:{ex.Message}");
                return Recommendation;
            }
        }

        public IEnumerable<HeaderInfo> GetHeaderInfo()
        {
            var festivals = GetFestivalNames();
            var headerInfo = festivals.Select(x => new HeaderInfo() { Title = x, Width = (x.Length+5)*9 } );
            // For now 24 headers/festivals max per year. Suffix with non visible ones if not 24.
            int headerCount = headerInfo.Count();
            int needToSuffix = 12 - headerCount;

            var list = headerInfo.ToList();
            for (int i = 0; i < needToSuffix; i++)
            {
                list.Add(new HeaderInfo()
                {
                    Title = string.Empty,
                    Width = 0
                });
            }
            _headerInfo = list;
            return list;
        }

        public TimeCache GetCache()
        {
            return  _cache;
        }
    }
}