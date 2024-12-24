// using Pitara.TagStuff;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace CommonProject.Src.Cache
{
    public class LocationCache : BaseThreadSafeFileCache<string>
    {
        ILogger _logger; 

        public LocationCache(string cacheFileName, ILogger logger, AppSettings appSettings)
        : base(cacheFileName, logger, appSettings)
        {
            _logger = logger;
        }

        private async Task<List<string>> GetASegment(int segmentNumber)
        {
            return await Task.Run(
                 () => {
                     var results = DataKeyPairDictionary
                         .Select(x =>
                         {
                             string[] parts = x.Value.Split(new char[] { ',' });
                             if (segmentNumber < parts.Length)
                             {
                                 return parts[segmentNumber].Trim();
                             }
                             else
                             { 
                                 return string.Empty;
                             }

                         })
                         .Distinct();
                     return results.ToList();
                 });

        }

        internal async Task<List<string>> GetAllCountries()
        {
            return await GetASegment(1);
         }
        internal async Task<List<string>> GetAllStates()
        {
            return await GetASegment(2);
        }
        internal async Task<List<string>> GetAllCities()
        {
            return await GetASegment(0);
        }

        internal async Task<List<string>> GetAllLocationsAsync()
        {
            var results = await GetAllCities();
            // results = results.Concat(await GetAllStates()).ToList();
            // results = results.Concat(await GetAllCountries()).ToList();

            results = results.Where(x => !string.IsNullOrEmpty(x)).ToList();
            
            results.Sort();
            return results;
        }
        // Key - Search term Val - display
        public override async Task<Dictionary<string, string>> GetQueryMap()
        {
            var queryMap = new Dictionary<string, string>();
            var allLocations = await GetAllLocationsAsync();
            foreach (var locationName in allLocations)
            {
                var key = locationName.Trim();
                if(string.IsNullOrEmpty(key))
                {
                    continue;
                }
                if (key.IndexOf("-") > 0)
                {
                    key = key.Replace("-", " ");
                }
                if (!queryMap.ContainsKey(key))
                {
                    queryMap.Add(key, locationName);
                }
            }
            return queryMap;
        }

        public string GetValue(string gpsKey)
        {
            if (DataKeyPairDictionary.ContainsKey(gpsKey))
            {
                return DataKeyPairDictionary[gpsKey];
            }
            return string.Empty;
        }

        internal void Add(string coordinate, string location)
        {
            if (!DataKeyPairDictionary.ContainsKey(coordinate))
            {
                DataKeyPairDictionary.Add(coordinate, location);
            }
        }
    }
}
