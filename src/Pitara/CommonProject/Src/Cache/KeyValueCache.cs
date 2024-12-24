using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CommonProject.Src.Cache
{

    public class KeyValueCache : BaseThreadSafeFileCache<string>
    {
        private static object _lock = new object();

        public KeyValueCache(string cacheFileName, ILogger logger, AppSettings appSettings)
        : base(cacheFileName, logger, appSettings)
        {
        }
        public override async Task<Dictionary<string, string>> GetQueryMap()
        {
            var queryMap = new Dictionary<string, string>();
            return await Task.Run(() => {
                foreach (var keypair in DataKeyPairDictionary)
                {
                    if(string.IsNullOrEmpty(keypair.Key))
                    {
                        continue;
                    }
                    // File Folder, Keyword case.
                    if (!queryMap.ContainsKey(keypair.Key))
                    {
                        queryMap.Add(keypair.Key, keypair.Key);
                    }
                }
                return queryMap;
            });
            // Key should be seardh term.
        }

        internal async Task Add(string message, bool breakWords = true)
        {
            if(string.IsNullOrEmpty(message))
            {
                return;
            }
            if(breakWords)
            {
                await Task.Run(() => {
                    message = message.Replace(";", " ");
                    message = message.Replace(",", " ");
                    var wordArray = message.Split(' ');
                    foreach (var word in wordArray)
                    {
                        var sanitizedWord = TagsHelper.UppercaseFirst(word.Trim());
                        if (!DataKeyPairDictionary.ContainsKey(sanitizedWord.Trim()))
                        {
                            this.DataKeyPairDictionary.Add(sanitizedWord.Trim(), "x");
                        }
                    }
                });
            }
            else
            {
                await Task.Run(() => {
                    message = TagsHelper.UppercaseFirst(message.Trim());
                    if (!DataKeyPairDictionary.ContainsKey(message))
                    {
                        this.DataKeyPairDictionary.Add(message, "x");
                    }
                });
            }
        }
    }
}
