using CommonProject.Src.Cache;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public class SuggestionProcessor
    {
        private static ILogger _logger;
        private ILuceneService _luceneService;
        public SuggestionProcessor(ILogger logger, ILuceneService luceneService)
        {
            _logger = logger;
            _luceneService = luceneService;
        }
        // Take all other collection and process the in memory. No need to write anything in files.
        public async Task<List<string>> GetSearchSuggestionKeyWords(
            BaseThreadSafeFileCache<string> keywordCache,
            BaseThreadSafeFileCache<string> folderCache, 
            BaseThreadSafeFileCache<string> favoriteCache,
            TimeCache timeCache,
            LocationCache locationCache,
            BaseThreadSafeFileCache<string> miscCache,
            BaseThreadSafeFileCache<string> cameraMakeModelCache,
            BaseThreadSafeFileCache<string> heightCache)
        {
            return await Task.Run(async()=> {

                List<string> result = new List<string>();

                var keywords = keywordCache.DataKeyPairDictionary.Select(x => "tags " + x.Key).ToList();
                result = result.Concat(keywords).ToList();

                var cameraMakeModel = cameraMakeModelCache.DataKeyPairDictionary.Select(x => "camera " + x.Key).ToList();
                result = result.Concat(cameraMakeModel).ToList();

                var height = heightCache.DataKeyPairDictionary.Select(x => "at " + x.Key).ToList();
                result = result.Concat(height).ToList();

                var folders = folderCache.DataKeyPairDictionary.Select(x => "from " + x.Key).ToList();
                result = result.Concat(folders).ToList();

                var locations = await locationCache.GetAllLocationsAsync();
                result = result.Concat(locations.Select(x=> "at " + x)).ToList();

                var years = timeCache.ListAllYears();
                result = result.Concat(years.Select(x=> "at " + x)).ToList();

                var months = timeCache.ListAllMonths();
                result = result.Concat(months.Select(x => "at " + x)).ToList();

                // var misc = miscCache.DataKeyPairDictionary.Select(x => x.Key).ToList();
                // result = result.Concat(misc).ToList();

                // Keep only unique, remove empty
                result = result
                        .Distinct().ToList();

                // Make sure there are at least one result for each keyword.
                result = await FilterOutStaleTags(result);

                // AddCommon default
                var defaultKeywords = GetAllDefaultKeywords();
                result = result.Concat(defaultKeywords).ToList();

                result = result.Select(x => TagsHelper.UppercaseFirst(x)).ToList();
                
                return result;
            });
        }
        private IEnumerable<string> GetAllDefaultKeywords()
        {
            return new List<string>()
            {
            //"AND",
            //"OR",
            //"(",
            //")",
            //"#LastWeek",
            //"#LastMonth",
            //"#LastYear",
            //"#LastHalfDecade",
            //"#LastDecade",
            //"#LastTwoDecades"
            };
        }

        private async Task<List<string>> FilterOutStaleTags(List<string> words)
        {
            List<string> result = new List<string>();
            foreach (var word in words)
            {
                // Note key is the query.
                var info = await _luceneService.GetQueryInfoAsync(word, word);
                if (info.ResultCount > 0)
                {
                    result.Add(word);
                }
            }
            return result;
        }
    }
}
