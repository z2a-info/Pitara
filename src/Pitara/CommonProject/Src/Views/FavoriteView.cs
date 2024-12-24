using CommonProject.Src.Cache;
using System.Threading.Tasks;

namespace CommonProject.Src.Views
{
    public class FavoriteView: BaseView
    {
        private static ILogger _logger;
        public FavoriteView(FavoriteCache cache, ILogger logger, ILuceneService luceneService, AppSettings appSettings)
            :base(cache, logger, nameof(FavoriteView), luceneService)
        {
            _logger = logger;
        }
        public override async Task ComputeRecommendations(BaseThreadSafeFileCache<string> cache) 
        {
            // Don't ignore zero result queries in favorite view.
            await base.ComputeRecommendationsCommon(cache, false, false);
        }
    }
}
