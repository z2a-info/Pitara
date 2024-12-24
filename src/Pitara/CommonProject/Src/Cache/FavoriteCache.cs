// using Pitara.TagStuff;
// using PitaraNameSpace.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;


namespace CommonProject.Src.Cache
{
    public class FavoriteCache : BaseThreadSafeFileCache<string>
    {

        public FavoriteCache(string cacheFileName, ILogger logger, AppSettings appSettings)
        : base(cacheFileName, logger, appSettings)
        {
        }

        public void AddFavorite(string query, string queryDescription)
        {
            if(!DataKeyPairDictionary.ContainsKey(query))
            {
                DataKeyPairDictionary.Add(query, queryDescription);
            }
        }
        public override async Task<Dictionary<string, string>> GetQueryMap()
        {
            var queryMap = new Dictionary<string, string>();
            return await Task.Run(() => 
            {
                foreach (var keypair in DataKeyPairDictionary)
                {
                    if (!queryMap.ContainsKey(keypair.Key))
                    {
                        {
                            queryMap.Add(keypair.Key, keypair.Value.ToString());
                        }
                    }
                }
                return queryMap;
            });
            // Key should be seardh term.
        }

    }
}
