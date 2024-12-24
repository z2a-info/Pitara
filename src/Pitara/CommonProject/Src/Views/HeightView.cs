﻿using CommonProject.Src.Cache;
using System.Threading.Tasks;

namespace CommonProject.Src.Views
{
    public class HeightView: BaseView
    {
        private static ILogger _logger;
        public HeightView(KeyValueCache cache, ILogger logger, ILuceneService luceneService, AppSettings appSettings)
            : base(cache, logger, nameof(FolderView), luceneService)
        {
            _logger = logger;
        }
        public override async Task ComputeRecommendations(BaseThreadSafeFileCache<string> cache)
        {
            await base.ComputeRecommendationsCommon(cache, true);
        }
    }
}
