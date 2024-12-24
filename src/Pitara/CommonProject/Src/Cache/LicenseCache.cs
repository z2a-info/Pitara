// using Pitara.TagStuff;
// using PitaraNameSpace.Infrastructure;

using Microsoft.Extensions.Logging;

namespace CommonProject.Src.Cache
{
    public class LicenseCache : BaseThreadSafeFileCache<License>
    {

        public LicenseCache(string cacheFileName, ILogger logger, AppSettings appSettings)
        : base(cacheFileName, logger, appSettings)
        {
        }
        public void Add(string key, License license)
        { 
            if(!DataKeyPairDictionary.ContainsKey(key))
            {
                DataKeyPairDictionary.Add(key, license);
            }
        }
    }
}
