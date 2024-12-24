using CommonProject.Src.Queues;
using CommonProject.Src.Views;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CommonProject.Src
{
    public class FileWatcher
    {
        public event PitaraEventHandler _totalPhotoEvent;
        private static int _timerFrequency;
        private System.Timers.Timer _theTimer;
        private ILogger _logger;
        private ILuceneService _luceneService;
        private UserSettings _userSettings;
        private AppSettings _appSettings;
        ManualResetEvent _searchisGoingon;
        private BatchProcessor<string> _batchProcessor;
        public FileWatcher(int timerFrequency, ILogger logger, UserSettings userSettings, AppSettings appSettings, ManualResetEvent searchisGoingon, ILuceneService luceneService)
        {
            _timerFrequency = timerFrequency;
            _logger = logger;
            _userSettings = userSettings;
            _luceneService = luceneService;
            _searchisGoingon = searchisGoingon;
            _appSettings = appSettings;
        }
        public void Watch()
        {
            _theTimer = new System.Timers.Timer(_timerFrequency);
            _theTimer.Elapsed += Timer_Elapsed;
            _theTimer.Start();
        }
        public void Stop()
        {
            if (_batchProcessor != null)
            {
                _batchProcessor.StopBatch();
            }
        }
        private async void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Just do it once at launch.
            _theTimer?.Stop();

            using (SingleThreaded singleThreaded = new SingleThreaded())
            {
                if(!singleThreaded.IsSafeToProceed()) 
                {
                    return;
                }
                _searchisGoingon.WaitOne();
                CancellationTokenSource cts = new CancellationTokenSource();
                CancellationToken cancellationToken = cts.Token;
                using (StopWatchInternal swFileWatcher = new StopWatchInternal("FileWatcher", _logger))
                {
                    _userSettings.LogSettings();
                    DateTime startTime = DateTime.Now;
                    List<string> photoCollection = new List<string>();
                    _totalPhotoEvent.Invoke(this, new PitaraEventArg("Counting.."));

                    using (StopWatchInternal sw = new StopWatchInternal("Collecting photos.", _logger))
                    {
                        IEnumerable<string> scanPathCollection = await GetScanPathCollection(cancellationToken);
                        photoCollection = await ListPhotosFromPaths(cancellationToken, scanPathCollection);

                        // If subfolders are added separatedly, just need to have unique ones.
                        photoCollection = photoCollection.Distinct().ToList();

                        // Let's remove bad files.
                        string[] badFiles = await Utils.ReadThreadSafe(_appSettings.BadFilesName, _logger, _appSettings);

                        photoCollection = photoCollection.Where(x => Utils.IsValidFileToIndex(badFiles, x)).ToList();

                        _totalPhotoEvent.Invoke(this, new PitaraEventArg(photoCollection.Count()));

                        photoCollection = ShuffleIndexedFilesatBack(photoCollection);
                    }

                    _totalPhotoEvent.Invoke(this, new PitaraEventArg(photoCollection.Count()));
                    object lockObj = new object();
                    List<string> listofFilesToAdd = new List<string>();

                    _batchProcessor = new BatchProcessor<string>("File Watcher",
                                        _logger,
                                        photoCollection,
                                        5,
                                        async (string item) =>
                                        {
                                            _searchisGoingon.WaitOne();

                                            // To keep UI smooth.

                                            await Utils.DelayRandome(20, 200);
                                            if (PhotoInputQueue<string>.ItemExist(item))
                                            {
                                                return 0;
                                            }
                                            if (!IndexReader.IndexExists(FSDirectory.Open(_userSettings.IndexFolder)))
                                            {
                                                return 0;
                                            }
                                            var result = await _luceneService.DoesPhotoExistInIndexAsync(new string[] { item });
                                            if (result.ContainsKey(item) && result[item] == true)
                                            {
                                                return 0;
                                            }
                                            await PhotoInputQueue<string>.EnQueueAsync(item);
                                            lock (lockObj)
                                            {
                                                listofFilesToAdd.Add(item);
                                            }
                                            return 0;
                                        });
                    await _batchProcessor.Run();
                    if (listofFilesToAdd.Count > 0)
                    {
                        _logger.SendLogAsync($"File watcher, total added to the queue: {listofFilesToAdd.Count()}");
                    }
                }
            }
            //Spinner spinner = new Spinner(_logger,
            //            KnobId.FileWatcher,
            //            async (CancellationToken cancellationToken) =>
            //            {
            //                _searchisGoingon.WaitOne();
            //                using (StopWatchInternal swFileWatcher = new StopWatchInternal("FileWatcher", _logger))
            //                {
            //                    _userSettings.LogSettings();
            //                    DateTime startTime = DateTime.Now;
            //                    List<string> photoCollection = new List<string>();
            //                    _totalPhotoEvent.Invoke(this, new PitaraEventArg("Counting.."));

            //                    using (StopWatchInternal sw = new StopWatchInternal("Collecting photos.", _logger))
            //                    {
            //                        IEnumerable<string> scanPathCollection = await GetScanPathCollection(cancellationToken);
            //                        photoCollection = await ListPhotosFromPaths(cancellationToken, scanPathCollection);

            //                        // Let's remove bad files.
            //                        string[] badFiles = await Utils.ReadThreadSafe(_appSettings.BadFilesName, _logger, _appSettings);

            //                        photoCollection = photoCollection.Where(x => Utils.IsValidFileToIndex(badFiles, x)).ToList();
                                    
            //                        _totalPhotoEvent.Invoke(this, new PitaraEventArg(photoCollection.Count()));

            //                        photoCollection = ShuffleIndexedFilesatBack(photoCollection);
            //                    }

            //                    _totalPhotoEvent.Invoke(this, new PitaraEventArg(photoCollection.Count()));
            //                    object lockObj = new object();
            //                    List<string> listofFilesToAdd = new List<string>();

            //                    _batchProcessor = new BatchProcessor<string>("File Watcher",
            //                                        _logger,
            //                                        photoCollection,
            //                                        1,
            //                                        async (string item) =>
            //                                        {
            //                                            _searchisGoingon.WaitOne();
            //                                            // To keep UI smooth.
            //                                            await Utils.DelayRandome(50, 300);
            //                                            if (!IndexReader.IndexExists(FSDirectory.Open(_userSettings.IndexFolder)))
            //                                            {
            //                                                return 0;
            //                                            }
            //                                            var result = await _luceneService.DoesPhotoExistInIndexAsync(new string[] { item });
            //                                            if (result.ContainsKey(item) && result[item] == true)
            //                                            {
            //                                                return 0;
            //                                            }
            //                                            await PhotoInputQueue<string>.EnQueueAsync(item);
            //                                            lock (lockObj)
            //                                            {
            //                                                listofFilesToAdd.Add(item);
            //                                            }
            //                                            return 0;
            //                                        });
            //                    await _batchProcessor.Run();
            //                    if (listofFilesToAdd.Count > 0)
            //                    {
            //                        _logger.SendLogAsync($"File watcher, total added to the queue: {listofFilesToAdd.Count()}");
            //                    }
            //                }
            //                return 0;
            //            });
            //await spinner.SpinIfStopped();
        }

        private List<string> ShuffleIndexedFilesatBack(List<string> photoCollection)
        {
            int alreadyIndexedCount = _luceneService.GetIndexedPhotoCount();
            if (alreadyIndexedCount == photoCollection.Count)
            {
                return photoCollection;
            }
            var indexedFiles = photoCollection.Take(alreadyIndexedCount).ToList();
            List<string> results = new List<string>();

            results = photoCollection.Skip(alreadyIndexedCount).ToList();
            // _logger.SendLogAsync($"New files: {string.Join(";",results.ToArray())}");
            results = results.Concat(indexedFiles).ToList();
            return results;
        }

        private async Task<IEnumerable<string>> GetScanPathCollection(CancellationToken cancellationToken)
        {
            var path = _userSettings.CombinePaths(_userSettings.PhotoFolders);
            if (path == "*")
            {
                return await Task.Run(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new List<string>();
                    }
                    return DriveInfo.GetDrives()
                        .Where(x => x.IsReady)
                        .Select(x => x.RootDirectory.FullName).ToList<string>();
                });
            }
            else
            {
                return path.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList<string>()
                    .Select(x => x.Trim());
            }
        }
        private async Task<List<string>> ListPhotosFromPaths(CancellationToken cancellationToken, IEnumerable<string> scanPathCollection /*, MainWIndowViewModel viewmodel*/)
        {
            List<string> resultPhotoCollection = new List<string>();
            object lockObject = new object();
            IEnumerable<string> exclusionList = PrepareExclusionList();
            var bp = new BatchProcessor<string>("ListPhotosFromPaths",
            _logger,
            scanPathCollection,
            1,
            async (string item) =>
            {
                try
                {
                    IEnumerable<string> collectedFiles = await GetFilesInternal(cancellationToken, item, new string[] { "*.jpg", "*.jpeg" }, exclusionList);
                    lock (lockObject)
                    {
                        resultPhotoCollection = resultPhotoCollection.Concat(collectedFiles).ToList();
                        _logger.SendDebugLogAsync($"Gathered {collectedFiles.Count()} files from: {item}");
                        // _totalPhotoEvent.Invoke(this, new PitaraEventArg(resultPhotoCollection.Count()));
                    }
                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"Couldn't scan folder: {item}. Error: {ex.Message}."); // Log it and move on
                    _logger.SendLogWithException($"Couldn't scan folder: {item}.", ex);
                }
                return 0;
            });
            await bp.Run();
            _logger.SendDebugLogAsync($"Total potential files to index: {resultPhotoCollection.Count()}");
            return resultPhotoCollection;
        }

        public static int GetTotalFilesCount(UserSettings settings)
        {
            // settings.LoadSettings();
            int result = 0;
            foreach(var path in settings.PhotoFolders)
            {
                List<string> files = new List<string>();
                result += DirectorySearch(path, files).Count;
            }
            return result;
        }
        public static List<string> DirectorySearch(string dir, List<string> files)
        {
            try
            {
                foreach (string f in System.IO.Directory.GetFiles(dir, "*.jpg"))
                {
                    // Console.WriteLine(Path.GetFileName(f));
                    files.Add(f);
                }
                foreach (string d in System.IO.Directory.GetDirectories(dir))
                {
                    // Console.WriteLine(Path.GetFileName(d));
                    DirectorySearch(d, files);
                }
                return files;
            }
            catch (System.Exception ex)
            {
                return files;
            }
        }

        public async Task<IEnumerable<string>> GetFilesInternal(CancellationToken cancellationToken, string path, string[] patternCollection, IEnumerable<string> exclusionList /*, /*MainWIndowViewModel viewmodel*/)
        {
            int maxStatusLen = 40;
            int printLen = (path.Length < maxStatusLen) ? path.Length : maxStatusLen;

            // Don't walk folders that are in exclusion list.
            foreach (string exclude in exclusionList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new List<string>();
                }
                if (path.ToLower().StartsWith(exclude.ToLower()))
                {
                    return new List<string>();
                }
                if (path.ToLower().Substring(3).StartsWith("$RECYCLE.BIN".ToLower()))
                {
                    return new List<string>();
                }
            }
            var files = new List<string>();
            object lockObject = new object();
            try
            {
                // await Utils.DelayRandome(5, 15);
                var bp = new BatchProcessor<string>("GetFilesInternal",
                _logger,
                patternCollection, // for each pattern
                1,
                async (string item) =>
                {
                    var list = System.IO.Directory.GetFiles(path, item, SearchOption.TopDirectoryOnly);
                    // list = list.Where(x => Utils.IsValidFileToIndex(x)).ToArray();
                    if (list.Count() > 0)
                    {
                        lock (lockObject)
                        {
                            files.AddRange(list);
                        }
                    }
                    // viewmodel.SetTotalPhotoMessage($"Photos: \\");
                    return await Task.Run(() => { return 0; });
                });
                await bp.Run();

                var bp1 = new BatchProcessor<string>("GetFilesInternal-dir",
                _logger,
                System.IO.Directory.EnumerateDirectories(path),
                2,
                async (string directory) =>
                {
                    var list = await GetFilesInternal(cancellationToken, directory, patternCollection, exclusionList);
                    if (list.Count() > 0)
                    {
                        lock (lockObject)
                        {
                            files.AddRange(list);
                        }
                    }
                    // viewmodel.SetTotalPhotoMessage($"Photos: /");
                    return 0;
                });
                await bp1.Run();

            }
            // For directory that don't have access.
            catch (UnauthorizedAccessException)
            {
            }
            catch (Exception ex)
            {
                _logger.SendLogWithException($"GetFiles error:{ex.Message}, path:{path}", ex);
                throw;
            }
            return files;
        }
        private IEnumerable<string> PrepareExclusionList()
        {
            List<string> result = new List<string>();
            if (_userSettings.ExcludeFolders != null)
            {
                result = _userSettings.ExcludeFolders
                .Select(x => x.Trim())
                .ToList<string>();
            }

            // AddCommon Bucket.
            if (_userSettings.BucketFolder != null)
            {
                result.Add(_userSettings.BucketFolder);
            }

            // Exclude index folder.
            if (_userSettings.IndexFolder != null)
            {
                result.Add(_userSettings.IndexFolder);
            }

            //// Exclude Deleted folder.
            //if (_userSettings.DeletedFolder != null)
            //{
            //    result.AddCommon(_userSettings.DeletedFolder);
            //}

            // Exclude Duplicate folder.
            //if (_userSettings.DuplicateFolder != null)
            //{
            //    result.AddCommon(_userSettings.DuplicateFolder);
            //}

            // Temp folder
            if (System.IO.Path.GetTempPath() != null)
            {
                result.Add(System.IO.Path.GetTempPath());
            }

            // Download folder.
            //if (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) != null)
            //{
            //    result.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            //}
            return result.Select(x => x.TrimEnd(new char[] { '\\' }));
        }
    }
}