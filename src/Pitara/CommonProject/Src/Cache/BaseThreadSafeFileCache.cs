// using Pitara.TagStuff;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CommonProject.Src.Cache
{
    public class BaseThreadSafeFileCache<T>
    {
        public string FilePath { get; set; }
        private DateTime _lastUpdateTime = DateTime.MinValue;
        public Dictionary<string, T> DataKeyPairDictionary = new Dictionary<string, T>();
        ILogger _logger;
        public BaseThreadSafeFileCache(string cacheFileName, ILogger logger, AppSettings appSettings)
        {
            FilePath = cacheFileName;
           _logger= logger;
        }
        public bool IsCacheDirty()
        {
            if(!File.Exists(this.FilePath))
            {
                return false;
            }
            if(File.GetLastWriteTime(this.FilePath) != _lastUpdateTime)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public async Task LoadAsync()
        {
                await Task.Run(() => {
                    using (var mutex = new MutexWrapper(FilePath.Replace("\\", "")))
                    {
                        try
                        {
                            if (File.Exists(FilePath))
                            {
                                if (!this.IsCacheDirty())
                                {
                                    return;
                                }
                                string fileContents = File.ReadAllText(FilePath).Trim();
                                if (!string.IsNullOrEmpty(fileContents))
                                {
                                    var settentireCache = JsonConvert.DeserializeObject<BaseThreadSafeFileCache<T>>(fileContents);
                                    this.FilePath = settentireCache.FilePath;
                                    this.DataKeyPairDictionary = settentireCache.DataKeyPairDictionary;
                                }
                                this._lastUpdateTime = File.GetLastWriteTime(FilePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.SendLogWithException("BaseThreadSafeFileCache - LoadAsync", ex);
                            throw;
                        }
                        finally
                        {
                        }
                    }
                });
        }
        public async Task TouchAsync()
        {
            await LoadAsync();
            await SaveAsync();
        }
        public async Task SaveAsync()
        {
            await Task.Run(() => {
                using (var mutex = new MutexWrapper(FilePath.Replace("\\", "")))
                {
                    try 
                    {
                        string stringContent = JsonConvert.SerializeObject(this);
                        File.WriteAllText(this.FilePath, stringContent);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally 
                    {
                    }
                }
            });
        }
        public async Task Reset()
        {
            DataKeyPairDictionary.Clear();
            // Save only if a previous load was called.
            if(!string.IsNullOrEmpty(this.FilePath))
            {
                await SaveAsync();
            }
        }

        // Only used for the cache <stting> type
        // Key - Search term Val - display
        public virtual async Task<Dictionary<string, string>> GetQueryMap()
        {
            return await Task.Run(()=> new Dictionary<string, string>());
        }
        public void AddCommon(string key, T cacheType)
        {
            if (!DataKeyPairDictionary.ContainsKey(key))
            {
                this.DataKeyPairDictionary.Add(key, cacheType);
            }
        }

    }
}
