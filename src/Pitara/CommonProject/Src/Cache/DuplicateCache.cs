// using Pitara.TagStuff;
// using PitaraNameSpace.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;


namespace CommonProject.Src.Cache
{
    public class DuplicateCache : BaseThreadSafeFileCache<List<string>>
    {

        public DuplicateCache(string cacheFileName, ILogger logger, AppSettings appSettings)
        : base(cacheFileName, logger, appSettings)
        {
        }

        internal void Add(string contentKey, List<string> listofDups)
        {
            if (!DataKeyPairDictionary.ContainsKey(contentKey))
            {
                DataKeyPairDictionary.Add(contentKey, listofDups);
            }
        }
    }
}
