using CommonProject.Src.Cache;
using System.Threading.Tasks;

namespace CommonProject.Src.Views
{
    public class MyTagView: BaseView
    {
        private static ILogger _logger;
        public MyTagView(KeyValueCache cache, ILogger logger, ILuceneService luceneService, AppSettings appSettings)
            : base(cache, logger, nameof(MyTagView), luceneService)
        {
            _logger = logger;
        }
        public override async Task ComputeRecommendations(BaseThreadSafeFileCache<string> cache)
        {
            await base.ComputeRecommendationsCommon(cache, true);
        }
    }
}
