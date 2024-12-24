using CommonProject.Src.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommonProject.Src.Views
{
    public class BaseView
    {

#if NO_EXPIRY
        private bool _addAutoexpiry = false;
#else
        private bool _addAutoexpiry = true;
#endif
        private DateTime _expiryDate = DateTime.Now;

        private static int _maxrecommendations = 1000;
        private string _viewName;
        private BaseThreadSafeFileCache<string> _cache;
        private static ILogger _logger;
        private ILuceneService _luceneService;
        private bool _addSpacingToResults = false;
        protected Dictionary<string, QueryInfo> _recommendationList = new Dictionary<string, QueryInfo>();

        public string ViewName { get => _viewName; set => _viewName = value; }

        public BaseView(BaseThreadSafeFileCache<string> cache, 
            ILogger logger, 
            string viewName, 
            ILuceneService luceneService)
        {
            _logger = logger;
            _cache = cache;
            ViewName = viewName;
            _luceneService = luceneService;
            _addSpacingToResults = false;

            if(_addAutoexpiry)
            {
                // _logger.SendLogAsync($"Checking Auto expiry");
                if(DateTime.Now > _expiryDate)
                {
                    throw new Exception($"Trial period for Common lib expired on : {_expiryDate.ToString()}");
                }
            }
            else
            {
                // _logger.SendLogAsync($"Not Checking Auto expiry");
            }

        }
        public virtual BaseThreadSafeFileCache<string> GetCache()
        {
            return _cache;
        }

        //public async Task ResetCommon()
        //{
        //    try
        //    {
        //        _recommendationList.Clear();
        //        await _cache.Reset();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.SendLogWithException($"{ViewName} - Reset", ex);
        //    }
        //}
        public async Task Reset()
        {
            try
            {
                _recommendationList.Clear();
                await _cache.Reset();
            }
            catch (Exception ex)
            {
                _logger.SendLogWithException($"{ViewName} - Reset", ex);
            }
        }
        public virtual Task ComputeRecommendations(BaseThreadSafeFileCache<string> cache) 
        {
            return null;
        }
        static public List<QueryInfo> AddSpacing(List<QueryInfo> inCollection)
        {
            char firstLetter = ' ';
            char previousFirstLetter = firstLetter;
            List<QueryInfo> result = new List<QueryInfo>();
            foreach (var item in inCollection)
            {
                firstLetter = item.QueryDisplayName[0];
                if (firstLetter != previousFirstLetter && previousFirstLetter != ' ')
                {
                    int remainder = result.Count() % 8;

                    int extraLinks = (remainder != 0) ? 8 - remainder : 0;
                    for (int i = 0; i < extraLinks; i++)
                    {
                        QueryInfo qi = new QueryInfo()
                        {
                            QueryName = firstLetter + "_" + Guid.NewGuid().ToString(),
                            QueryDisplayName = "              ",
                            ResultCount = 0
                        };
                        result.Add(qi);
                    }
                }
                if (firstLetter != previousFirstLetter)
                {
                    string charVal = "*["+ firstLetter + "]";
                    item.QueryDisplayName = $"{charVal} {item.QueryDisplayName}";
                }
                result.Add(item);
                previousFirstLetter = firstLetter;
            }
            return result;
        }
        public async Task<Dictionary<string, string>> GetTopDozenVisits()
        {
            _recommendationList.Clear();
            await _cache.LoadAsync();
            await ComputeRecommendations(_cache);

            var sortedByCount = _recommendationList.Values.ToList()
                .OrderByDescending(x => x.ResultCount)
                .Take(12)
                .ToList();
            return sortedByCount.ToDictionary(x => x.QueryName, x => x.QueryString);
        }
        public async Task<Dictionary<string, string>> GetTopFive()
        {
            _recommendationList.Clear();
            await _cache.LoadAsync();
            await ComputeRecommendations(_cache);

            if (_recommendationList.Values.Count < 3)
            {
                return new Dictionary<string, string>();
            }
            int upperBound = (_recommendationList.Count < 9)
                ? _recommendationList.Count - 1
                : 5;
            int count = (upperBound - 3) < 0
                ? 0
                : (upperBound - 3);
            var sortedByCount = _recommendationList.Values.ToList()
                .OrderByDescending(x => x.ResultCount)
                .ToList()
                .GetRange(3, count);
            return sortedByCount.ToDictionary(x => x.QueryName, x => x.QueryString);
        }

        public async Task ComputeRecommendationsCommon(BaseThreadSafeFileCache<string> cache, bool shortNames, bool ignoreZeroResults = true)
        {
            var queryPairs = await cache.GetQueryMap();
            foreach (var query in queryPairs)
            {
                if (_recommendationList.Count == _maxrecommendations)
                {
                    break;
                }
                // Note key is the query.
                var info = await _luceneService.GetQueryInfoAsync(query.Value, query.Key, shortNames);
                if (info.ResultCount > 0)
                {
                    if (!_recommendationList.ContainsKey(info.QueryName))
                    {
                        _recommendationList.Add(info.QueryName, info);
                    }
                }
                else
                {
                    if (!ignoreZeroResults)
                    {
                        if (!_recommendationList.ContainsKey(info.QueryName))
                        {
                            _recommendationList.Add(info.QueryName, info);
                        }
                    }
                    else
                    {
                        // _logger.SendDebugLogAsync($"Query keyval Results -> {query.Key},{query.Value}, {info.ResultCount}");
                    }
                }
            }
            return;
        }
        public async Task<List<QueryInfo>> GetTopRecommendationsAsync()
        {
            try
            {
                _recommendationList.Clear();
                await _cache.LoadAsync();
                await ComputeRecommendations(_cache);
                var results = _recommendationList.Values.ToList()
                        .OrderBy(x => x.QueryName).ToList();
                if (_addSpacingToResults)
                {
                    return AddSpacing(results);
                }
                else
                {
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.SendLogWithException($"{ViewName} - GetTopRecommendationsAsync", ex);
                return _recommendationList.Values.ToList();
            }
        }
    }
}
