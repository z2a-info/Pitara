using CommonProject.Src.Cache;
using System.Threading.Tasks;

namespace CommonProject.Src.Views
{
    public class LocationView : BaseView
    {
        private static ILogger _logger;
        public LocationView(LocationCache cache, ILogger logger, ILuceneService luceneService, AppSettings appSettings)
            : base(cache, logger, nameof(LocationView), luceneService)
        {
            _logger = logger;
        }
        public override async Task ComputeRecommendations(BaseThreadSafeFileCache<string> cache)
        {
            await base.ComputeRecommendationsCommon(cache, true);
        }
    }
}