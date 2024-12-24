using CommonProject.Src.Queues;
using ControllerProject.Src;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace CommonProject.Src
{
    public interface ILuceneService
    {
        string GetIndexVersion();
        Task<Dictionary<string, bool>> DoesPhotoExistInIndexAsync(string[] paths);
        Task AddDocumentToIndexAsync(List<Document> documents);
        Task StopCleaningupIndex();
        Task UpdateDocumentToIndexAsync(List<Document> documents);
        // Task AddtoIndex(string folder, MainWIndowViewModel viewmodel);
        Task StopIndexing();
        Task<Tuple<IList<DisplayItem>, int>> SearchAsync(string searchTerm, int startIndex, int count);
        Task UpdateCustomTagsAsync(string[] fileName);
        int GetIndexedPhotoCount();
        bool DoesIndexExists();
        Task ClearIndexAsync();
        bool IsIndexingGoingOn();
        Task<List<string>> DoesContentKeyAlreadyExistAsync(string conteKey, string filePath);
        Task<bool> DeleteDocumentFilePathAsync(string[] pathKeys);
        Task<QueryInfo> GetQueryInfoAsync(string queryName, string searchTerm, bool shortenDisplayName = true);
        Task<Dictionary<string, DisplayItem>> GetThesePhotoesFromIndex(string[] paths);
        Task<Document> ReadKeyValue(string key);
        Task<string[]> GetAllUniqueCustomTAGs();

    }

    public class LuceneService : ILuceneService
    {
        public static string UniqKeyForVersion = "af7e339b-a553-4c39-b60f-def7874530fe";

        UserSettings _userSettings;
        AppSettings _appSettings;
        public static int MaxPhotoToDisplay = 5000;
        //  4 is ideal
        //  6 - no errors at all.
        //  10 - some error.
        public static int BatchSize = 5; // standard 20 - 20 gives Random out of memory exception.
        public int PhotoCount { get; set; } = 0;
        public static Lucene.Net.Util.Version AppLuceneVersion1 { get => AppLuceneVersion; set => AppLuceneVersion = value; }
        public static Lucene.Net.Analysis.Standard.StandardAnalyzer Analyzer;

        public async Task<Document> ReadKeyValue(string key)
        {
            Document result = new Document();
            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                if (!IndexReader.IndexExists(fs))
                {
                    return result;
                }
                try
                {
                    Document doc = new Document();
                    await Task.Run(() => {
                        using (IndexSearcher searcher = new IndexSearcher(fs, true))
                        {
                            TopDocs searchResults = null;
                            {
                                searchResults = searcher.Search(new TermQuery(new Term("PathKey", Utils.GetUniquePathKey(key.ToLower()))), 1);
                                if (searchResults.TotalHits >= 1)
                                {
                                   doc = searcher.Doc(searchResults.ScoreDocs[0].Doc);
                                }
                            };
                            searcher.Dispose();
                        }
                    });
                    return doc;
                }
                catch (Exception ex)
                {
                    _logger.SendLogWithException("ReadKeyValue error:", ex);
                }
                fs.Dispose();
            }
            return result;
        }

        public bool DoesIndexExists()
        {
            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                return IndexReader.IndexExists(fs);
            }
        }
        public async Task<bool> DeleteDocumentFilePathAsync(string[] pathKeys)
        {
            return await Task.Run(async ()=> {
                try
                {
                    using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
                    {
                        if (IndexReader.IndexExists(fs))
                        {

                            await DeleteIndexEntryAsync(pathKeys.ToList(), fs);
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"DeleteDocumentByPathKeysAsync failed. Error: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task ClearIndexAsync()
        {
            // await Utils.EnsureCleanFolderAsync(_userSettings.IndexFolder);
            await Task.Run(()=> {
                try
                {
                    using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
                    {
                        if (IndexReader.IndexExists(fs))
                        {
                            using (IndexWriter writer = new IndexWriter(fs, Analyzer, false, IndexWriter.MaxFieldLength.UNLIMITED))
                            {
                                writer.DeleteAll();
                                writer.Commit();
                                writer.Dispose();
                                fs.Dispose();
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"ClearIndexAsync failed. Error: {ex.Message}");
                }
            });
        }

        public string GetIndexVersion()
        {
            try
            {
                using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
                {
                    using (IndexSearcher searcher = new IndexSearcher(fs, true))
                    {
                        Query query = new TermQuery(new Term("DocType", "VersionDocument"));
                        var filter = new QueryWrapperFilter(query);
                        var topDocs = searcher.Search(query, filter, 1);
                        if(topDocs.TotalHits ==0)
                        {
                            _logger.SendLogAsync($"No VersionDocument available");
                            return string.Empty;
                        }
                        var doc = searcher.Doc(0); // There is only doc of type VersionDocument.
                        if(doc == null)
                        {
                            _logger.SendLogAsync($"VersionDocument doc is null");
                            return string.Empty;
                        }
                        var version = doc.Get("Version").ToString().Trim();
                        _logger.SendLogAsync($"Version of index is:{version}");
                        return version;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"GetIndexedPhotoCount failed. Error: {ex.Message}, stacktrack: {ex.StackTrace}");
                return string.Empty;
            }
        }
        public int GetIndexedPhotoCount()
        {
            try
            {
                using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
                {
                    if (!IndexReader.IndexExists(fs))
                    {
                        return 0;
                    }
                    int count;
                    using (IndexReader reader = IndexReader.Open(fs, true))
                    {
                        count = reader.MaxDoc;
                        if(count == 0)
                        {
                            return 0;
                        }
                    }
                    using (IndexSearcher searcher = new IndexSearcher(fs, true))
                    {
                        Query query = new TermQuery(new Term("DocType", "PhotoDocument"));
                        var filter = new QueryWrapperFilter(query);
                        var topDocs = searcher.Search(query, filter, count);
                        return topDocs.TotalHits;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"GetIndexedPhotoCount failed. Error: {ex.Message}, stacktrack: {ex.StackTrace}");
            }
            return 0;
        }

        private static ILogger _logger;// = AsyncLog.GetGlobalLogger();
        // private Dictionary<string, int> NewTagsDiscovered = new Dictionary<string, int>();
        private static Lucene.Net.Util.Version AppLuceneVersion = Lucene.Net.Util.Version.LUCENE_30;// LuceneVersion.LUCENE_48;

        private List<char> escapeChars = new List<char>
            {
                '+',
                '-',
                '&',
                '|',
                '!',
                '(',
                ')',
                '{',
                '}',
                '[',
                ']',
                '^',
                '"',
                '~',
                '*',
                '?',
                '\\',
                '{',
                '}'
            };

        public LuceneService(ILogger logger, UserSettings userSettings,  AppSettings appSettings)
        {
            _logger = logger;
            _userSettings = userSettings;
            _appSettings = appSettings;
            InitialiseLucene();
        }

        private void InitialiseLucene()
        {
            Analyzer = new Lucene.Net.Analysis.Standard.StandardAnalyzer(AppLuceneVersion1);
        }

        private CancellationTokenSource _cancelTokenCleanupIndex = null;
        private CancellationTokenSource _cancelTokenAddToIndex = null;
        private Task _indexSynchronizationTask = null;
        private Task _indexCleanupTask = null;

        private async Task GetIndexTerms()
        {
            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                Dictionary<string, int> tagRanks = new Dictionary<string, int>();

                await Task.Run(() =>
                {
                    using (IndexReader reader = IndexReader.Open(fs, true))
                    {
                        for (int i = 0; i < reader.MaxDoc; i++)
                        {
                            if (reader.IsDeleted(i))
                            {
                                continue;
                            }

                            Document doc = reader.Document(i);
                            string tags = doc.Get("Tags");
                            string[] tagsArray = tags.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string tag in tagsArray)
                            {
                                if (tagRanks.ContainsKey(tag.ToLower().Trim()))
                                {
                                    tagRanks[tag.ToLower().Trim()]++;
                                }
                                else
                                {
                                    tagRanks[tag.ToLower().Trim()] = 1;
                                }
                            }
                        }
                        var updatedTagRanks = tagRanks
                            .Where(x => IsValid(x.Key))
                            .OrderByDescending(x => x.Value);

                        // File.WriteAllLines(_appSettings.TagCloudFileFullPath, updatedTagRanks.Select(x => x.Key + ":" + x.Value).ToArray());
                    }
                });

            }
        }

        private bool IsValid(string str)
        {
            return true;
        }
        private ConcurrentDictionary<string, string> _globalCustomMetaUpdateList = new ConcurrentDictionary<string, string>();
        public async Task<string[]> GetAllUniqueCustomTAGs()
        {
            var results = new List<string>();
            var sb = new StringBuilder();
            await Task.Run( () => 
            {
                using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
                {
                    if (IndexReader.IndexExists(fs))
                    {
                        using (IndexReader reader = IndexReader.Open(fs, true))
                        {
                            int count = reader.NumDocs();
                            if (count == 0)
                            {
                                return;
                            }
                            for (double i = 0; i < reader.MaxDoc; i++)
                            {
                                if (reader.IsDeleted((int)i))
                                {
                                    continue;
                                }
                                else
                                {
                                    Document doc = reader.Document((int)i);
                                    var version = doc.Get("Version");
                                    // Verison 0 when there was no version.
                                    if (string.IsNullOrEmpty(version))
                                    {
                                        continue;
                                    }
                                    sb.Append(" ");
                                    if (doc.Get("KeyWords") != null)
                                    {
                                        sb.Append(doc.Get("KeyWords"));
                                    }
                                }
                            }
                        }
                    }
                }
            });
            var tempArray = sb.ToString().Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var unique = tempArray
                .Select(x => x.Trim())
                .Distinct();
            return unique.ToArray();
        }
        public async Task UpdateCustomTagsAsync(string[] files)
        {
            // Insert in the queue for deletion.
            // Later these files will be inserted in queue for add.
            var messageId = Guid.NewGuid().ToString();
            await LucenDocumentQueue<LuceneDocumentQueueMessage>.EnQueueAsync(new LuceneDocumentQueueMessage()
            {
                Action = ActionType.DELETE,
                FilePathCollection = files.ToList(),
                MessageId = messageId
            });

            await LucenDocumentQueue<LuceneDocumentQueueMessage>.WaitUntilProcessed(messageId);
            await UpdateIndex(files);

            //if (!_buildingIndexEvent.WaitOne(5))
            //{
            //    _logger.SendDebugLogAsync($"Since indexing is in progress. Adding {files.Length} to the the indexing queue");
            //    // Syncing is in progress.
            //    // Let's just queue the item and leave. As Syncing thread will pick the queue.
            //    foreach (var file in files)
            //    {
            //        try
            //        {
            //            _globalCustomMetaUpdateList.TryAdd(file, file);
            //        }
            //        catch (Exception ex)
            //        {
            //            _logger.SendLogAsync($"Failed to add to custom keyword queue. Continuing to next file. Error: {ex.Message}");
            //            continue;
            //        }
            //    }
            //}
            //else
            //{
            //    UpdateIndex(files);
            //}
        }

        // Should only be called via Queue.
        private async Task DeleteIndexEntryAsync(List<string> fileNames, FSDirectory fs)
        {
            await Task.Run(()=> {
                if (fileNames.Count == 0)
                {
                    return;
                }
                using (IndexReader indexReader = IndexReader.Open(fs, false))
                {
                    try
                    {
                        int deleted = 0;
                        foreach (var item in fileNames)
                        {
                             deleted+=  indexReader.DeleteDocuments(new Term("PathKey", Utils.GetUniquePathKey(item.ToLower())));
                        }
                        indexReader.Commit();
                        indexReader.Dispose();
                        _logger.SendDebugLogAsync($"Total deleted: {deleted}");
                    }
                    catch (Exception ex)
                    {
                        _logger.SendLogAsync($"Error: DeleteIndexEntryAsync: {ex.Message}");
                    }
                }
            });
        }
        private async Task UpdateIndex(string[] files)
        {
            List<string> filesList = files.Distinct().ToList();
            foreach (var item in filesList)
            {
                await UserPhotoInputQueue<string>.EnQueueAsync(item);
            }
            _logger.SendDebugLogAsync($"Updated Index for: {files.Count()} files");
        }
        public bool IsIndexingGoingOn()
        {
            return (_indexSynchronizationTask != null)
                && (_indexSynchronizationTask.Status != TaskStatus.RanToCompletion);
        }
        public async Task StopCleaningupIndex()
        {
            if (_cancelTokenCleanupIndex != null)
            {
                _cancelTokenCleanupIndex.Cancel();
                while (_indexCleanupTask.Status != TaskStatus.RanToCompletion)
                {
                    await Task.Delay(100);
                }
            }
        }
        public async Task StopIndexing()
        {
            if (_cancelTokenAddToIndex != null)
            {
                _cancelTokenAddToIndex.Cancel();
                while (_indexSynchronizationTask.Status != TaskStatus.RanToCompletion)
                {
                    // _logger.SendLogAsync($"_indexSynchronizationTask.Status:{_indexSynchronizationTask.Status}");
                    await Task.Delay(100);
                }
                // _logger.SendLogAsync($"Out of loop - _indexSynchronizationTask.Status:{_indexSynchronizationTask.Status}");
                // await Task.Delay(500);
            }
        }
        private string _LuceneUnEscape(string str)
        {
            bool foundCharToUnEscape = false;
            foreach (char ch in escapeChars)
            {
                if (str.IndexOf("\\" + ch.ToString()) >= 0)
                {
                    // logger.SendLog(string.Format($"Found char:{ch} in path: {str}"));
                    foundCharToUnEscape = true;
                    break;
                }
            }
            if (foundCharToUnEscape == false)
            {
                return str;
            }

            foreach (char ch in escapeChars)
            {
                string findStr = "\\" + ch;
                str = str.Replace(findStr, ch.ToString());
            }
            return str;
        }
        private string _LuceneEscape(string str)
        {
            bool foundCharToEscape = false;
            foreach (char ch in escapeChars)
            {
                if (str.IndexOf(ch) >= 0)
                {
                    // logger.SendLog(string.Format($"Found char:{ch} in path: {str}"));
                    foundCharToEscape = true;
                    break;
                }
            }
            if (foundCharToEscape == false)
            {
                return str;
            }

            foreach (char ch in escapeChars)
            {
                string replacing = "\\" + ch;
                str = str.Replace(ch.ToString(), replacing);
            }
            return str;
        }

        public async Task<Dictionary<string, bool>> DoesPhotoExistInIndexAsync(string[] paths)
        {
            Object lockObject = new object();
            Dictionary<string, bool> result = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);
            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                try
                {
                    using (IndexSearcher searcher = new IndexSearcher(fs, true))
                    {
                        List<Task> taskList = new List<Task>();
                        foreach (var path in paths)
                        {
                            try
                            {
                                TopDocs searchResults = null;
                                var tempPath = path;
                                taskList.Add(Task.Run(() =>
                                {
                                    searchResults = searcher.Search(new TermQuery(new Term("PathKey", Utils.GetUniquePathKey(tempPath.ToLower()))), 1);
                                    bool found = false;
                                    if (searchResults.TotalHits >= 1)
                                    {
                                        found = true;
                                    }
                                    lock (lockObject)
                                    {
                                        result.Add(tempPath, found);
                                    }

                                }));
                            }
                            catch (Exception ex)
                            {
                                _logger.SendLogAsync($"Couldn't determind if file in Index:{path}. Error: {ex.Message}. Moving on to next file.");
                            }
                        }
                        await Task.WhenAll(taskList);
                        searcher.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.SendLogWithException("InIndex error:", ex);
                }
                fs.Dispose();
            }
            return result;
        }

        public async Task<Dictionary<string, DisplayItem>> GetThesePhotoesFromIndex(string[] paths)
        {
            Object lockObject = new object();
            Dictionary<string, DisplayItem> result = new Dictionary<string, DisplayItem>(StringComparer.InvariantCultureIgnoreCase);
            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                if (!IndexReader.IndexExists(fs))
                {
                    return result;
                }
                try
                {
                    using (IndexSearcher searcher = new IndexSearcher(fs, true))
                    {
                        List<Task> taskList = new List<Task>();
                        foreach (var path in paths)
                        {
                            try
                            {
                                TopDocs searchResults = null;
                                taskList.Add(Task.Run(() =>
                                {
                                    searchResults = searcher.Search(new TermQuery(new Term("PathKey", Utils.GetUniquePathKey(path.ToLower()))), 1);
                                    DisplayItem di = null; // null when not found.
                                    if (searchResults.TotalHits >= 1)
                                    {
                                        Document doc = searcher.Doc(searchResults.ScoreDocs[0].Doc);
                                        di = new DisplayItem(doc);
                                        lock (lockObject)
                                        {
                                            result.Add(path, di);
                                        }
                                    }

                                }));
                            }
                            catch (Exception ex)
                            {
                                _logger.SendLogAsync($"Couldn't determind if file in Index:{path}. Error: {ex.Message}. Moving on to next file.");
                            }
                        }
                        await Task.WhenAll(taskList);
                        searcher.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.SendLogWithException("InIndex error:", ex);
                }
                fs.Dispose();
            }
            return result;
        }
        public async Task UpdateDocumentToIndexAsync(List<Document> documents)
        {
            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                using (IndexWriter writer = new IndexWriter(fs, Analyzer, false, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    await Task.Run(() =>
                    {
                        foreach (var document in documents)
                        {
                            writer.UpdateDocument(new Term("PathKey", Utils.GetUniquePathKey(document.Get("PathKey"))),
                                document);
                            // _logger.SendDebugLogAsync($">> Index updated, for PathKey: {document.Get("PathKey")}");
                        }
                    });
                    writer.Commit();
                    // writer.Optimize();
                    writer.Dispose();
                }
                fs.Dispose();
            }
        }

        public async Task AddDocumentToIndexAsync(List<Document> documents)
        {
            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                bool createNew = IndexReader.IndexExists(fs)
                    ? false
                    : true;
                using (IndexWriter writer = new IndexWriter(fs, Analyzer, createNew, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    await Task.Run(() =>
                    {
                        foreach (var document in documents)
                        {
                            writer.AddDocument(document);
                        }
                    });
                    writer.Commit();
                    // writer.Optimize();
                    writer.Dispose();
                }
                fs.Dispose();
            }
        }

        public async Task<Dictionary<string, Tuple<bool, string>>> DoesContentAlreadyExistAsync(string[] paths)
        {
            Object lockObject = new object();
            Dictionary<string, Tuple<bool, string>> result = new Dictionary<string, Tuple<bool, string>>();
            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                if (!IndexReader.IndexExists(fs))
                {
                    return result;
                }
                try
                {
                    using (IndexSearcher searcher = new IndexSearcher(fs, true))
                    {
                        List<Task> taskList = new List<Task>();
                        foreach (var path in paths)
                        {
                            try
                            {
                                TopDocs searchResults = null;
                                taskList.Add(Task.Run(() =>
                                {
                                    string filePath = string.Empty;
                                    bool found = false;
                                    var contentKey = string.Empty;// await PhotoManipulation.HashOfPhotoAsync(path.ToLower());
                                    if (string.IsNullOrEmpty(contentKey))
                                    {
                                        lock (lockObject)
                                        {
                                            result.Add(path, new Tuple<bool, string>(found, filePath));
                                        }
                                    }
                                    searchResults = searcher.Search(new TermQuery(new Term("ContentKey",
                                        contentKey)), 1);
                                    if (searchResults.TotalHits >= 1)
                                    {
                                        Document doc = searcher.Doc(searchResults.ScoreDocs[0].Doc);
                                        filePath = doc.Get("PathKey");
                                        found = true;
                                    }
                                    lock (lockObject)
                                    {
                                        result.Add(path, new Tuple<bool, string>(found, filePath));
                                    }

                                }));
                            }
                            catch (Exception ex)
                            {
                                _logger.SendLogAsync($"Can't determine if content already in index: {path}. Error:{ex.Message}. Moving to next file.");
                            }
                        }
                        await Task.WhenAll(taskList);
                        searcher.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.SendLogWithException("DoesContentAlreadyExistAsync error:", ex);
                }
                fs.Dispose();
            }
            return result;
        }
        public async Task<List<string>> DoesContentKeyAlreadyExistAsync(string contentKey, string filePath)
        {
            return await Task.Run(()=> {
                Object lockObject = new object();
                List<string> result = new List<string>();
                using (FSDirectory fs = FSDirectory.Open(
                    _userSettings.IndexFolder))
                {
                    if (!IndexReader.IndexExists(fs))
                    {
                        return result;
                    }
                    try
                    {
                        using (IndexSearcher searcher = new IndexSearcher(fs, true))
                        {
                            TopDocs searchResults = null;
                            if (string.IsNullOrEmpty(contentKey))
                            {
                                return result;
                            }
                            searchResults = searcher.Search(new TermQuery(new Term("ContentKey",
                                contentKey)), 10000);
                            if (searchResults.TotalHits <= 1)
                            {
                                return result;
                            }
                            string orignalKeyFile = string.Empty;
                            foreach (ScoreDoc scoreDoc in searchResults.ScoreDocs)
                            {
                                int docId = scoreDoc.Doc;
                                Document doc = searcher.Doc(docId);
                                string fileEntry = $"{doc.Get("PathKey")}";
                                if (filePath.ToLower().Equals(doc.Get("PathKey").ToLower()))
                                {
                                    orignalKeyFile = fileEntry;
                                }
                                result.Add(fileEntry);
                            }
                            result.Remove(orignalKeyFile);
                            result.Insert(0, orignalKeyFile);
                            searcher.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.SendLogWithException("DoesContentKeyAlreadyExist error:", ex);
                    }
                    fs.Dispose();
                }
                return result;
            });
        }

        public async Task<QueryInfo> GetQueryInfoAsync(string queryName, string searchTerm, bool shortenDisplayName = true)
        {

            if (string.IsNullOrEmpty(searchTerm))
            {
                return new QueryInfo()
                {
                    QueryDisplayName = $"{queryName}",
                    QueryName = $"{queryName}",
                    QueryString = searchTerm,
                    ResultCount = 0
                };
            }
            NLPSearchProcessor nlp = new NLPSearchProcessor(searchTerm, _logger);
            string searchTermTranslated = nlp.GetTranslatedSearchTerm(searchTerm);

            // var searchTermTranslated = _appSettings.TranslateSearchTerm(searchTerm);

            QueryParser parser = new QueryParser(AppLuceneVersion1, "Tags", Analyzer);
            parser.DefaultOperator = QueryParser.Operator.AND;
            searchTermTranslated = PreProcessSearchTerm(searchTermTranslated.ToLower());
            Query query = null;
            try
            {
                query = parser.Parse(searchTermTranslated);
            }
            catch (Exception)
            {
                return new QueryInfo()
                {
                    QueryDisplayName = $"{queryName}",
                    QueryName = $"{queryName}",
                    QueryString = searchTerm,
                    ResultCount = 0
                };
            }
            // int validDocCounts = 0;
            TopDocs topDocs = null;
            try
            {
                using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
                {
                    using (IndexSearcher searcher = new IndexSearcher(fs, true))
                    {
                        String str = query.ToString();
                        await Task.Run(() =>
                        {
                            topDocs = searcher.Search(query, _appSettings.MaxResultsToFetch);
                        });

                    }
                }
            }
            catch (Exception)
            {
                return new QueryInfo()
                {
                    QueryDisplayName = $"{queryName}",
                    QueryName = $"{queryName}",
                    QueryString = searchTerm,
                    ResultCount = 0
                };
            }
            var queryDisplayName = $"{queryName}: ({topDocs.ScoreDocs.Length})";
            if (queryDisplayName.Length > 24 && shortenDisplayName)
            {
                string firstHalf = queryDisplayName.Substring(0, 10);
                string lastHalf = queryDisplayName.Substring(queryDisplayName.Length - 10);
                queryDisplayName = firstHalf + "~" + lastHalf;
            }

            return new QueryInfo()
            {
                QueryDisplayName = queryDisplayName,
                QueryName = $"{queryName}",
                QueryString = searchTerm,
                ResultCount = topDocs.ScoreDocs.Length
            };
        }

        private IEnumerable<ScoreDoc> GetAllActiveDocsHits()
        {
            List<ScoreDoc> results = new List<ScoreDoc>();
            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                if (!IndexReader.IndexExists(fs))
                {
                    return results;
                }

                int count = 0;
                using (IndexReader reader = IndexReader.Open(fs, true))
                {
                    count = reader.MaxDoc;
                }

                using (IndexSearcher searcher = new IndexSearcher(fs, true))
                {
                    var sort = new Sort(new SortField("EPOCHTIME", SortField.LONG));
                    QueryParser parser = new QueryParser(AppLuceneVersion1, "Tags", Analyzer);
                    Query query = parser.Parse("*:*");
                    var filter = new QueryWrapperFilter(query);
                    var topDocs = searcher.Search(query, filter, count, sort);
                    return topDocs.ScoreDocs;

                    //foreach (ScoreDoc scoreDoc in topDocs.ScoreDocs)
                    //{
                    //    float score = scoreDoc.Score;
                    //    int docId = scoreDoc.Doc;
                    //    Document doc = searcher.Doc(docId);
                    //    results.AddCommon(doc);
                    //}
                }
            }
        }

        public async Task<Tuple<IList<DisplayItem>, int>> SearchAsync(string searchTerm, int startIndex, int count)
        {
            IList<DisplayItem> results = new List<DisplayItem>();
            int maxAvailable = 0;

            QueryParser parser = new QueryParser(AppLuceneVersion1, "Tags", Analyzer);
            parser.DefaultOperator = QueryParser.Operator.AND;

            searchTerm = PreProcessSearchTerm(searchTerm.ToLower());

            Query query = null;
            try
            {
                query = parser.Parse(searchTerm);
            }
            catch (Exception ex)
            {
                Utils.DisplayMessageBox($"Parsing error: {ex.Message}");
                return Tuple.Create(results, maxAvailable);
            }

            try
            {
                using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
                {
                    using (IndexSearcher searcher = new IndexSearcher(fs, true))
                    {
                        // String str = query.ToString();
                        //var queryNew = new QueryParser(
                        //    Lucene.Net.Util.Version.LUCENE_30, 
                        //    "Text", 
                        //    new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30)).Parse(searchTerm);
                        var sort = new Sort(new SortField("EPOCHTIME", SortField.LONG));
                        var filter = new QueryWrapperFilter(query);
                        var topDocs = searcher.Search(query, filter, _appSettings.MaxResultsToFetch, sort);

                        if(topDocs.ScoreDocs.Count() == 0)
                        {
                            return Tuple.Create(results, maxAvailable);
                        }
                        maxAvailable = topDocs.ScoreDocs.Count();
                        if(count > maxAvailable)
                        {
                            count = maxAvailable;
                        }

                        // StringBuilder sb = new StringBuilder();
                        await Task.Run(() =>
                        {
                            int trackCount = 0;
                            // int numOfColumn = 8; // Change when you change at XAML.

                            // Insert the first photo
                            var tempDoc = searcher.Doc(topDocs.ScoreDocs[0].Doc);
                            InsertDocumentToResult(
                                results,
                                tempDoc);
                            string previousHeading = new DisplayItem(tempDoc).Heading;

                            bool skipFirstOne = true;

                            // Which one to render.
                            ScoreDoc[] toRender = new ScoreDoc[count - startIndex];
                            Array.Copy(topDocs.ScoreDocs, startIndex, toRender, 0, count - startIndex);

                            foreach (ScoreDoc scoreDoc in toRender)
                            {
                                if(skipFirstOne)
                                {
                                    skipFirstOne = false;
                                    continue;
                                }

                                float score = scoreDoc.Score;
                                int docId = scoreDoc.Doc;
                                Document doc = searcher.Doc(docId);
                                // sb.Append($", {doc.Get("EPOCHTIME")}");

                                DisplayItem dislayItem = new DisplayItem(doc);
                                if (string.IsNullOrEmpty(dislayItem.ThumbNail))
                                {
                                    continue;
                                }
                                if (MaxPhotoToDisplay > 0 && trackCount++ == MaxPhotoToDisplay)
                                {
                                    break;
                                }

                                if (dislayItem.Heading.Equals(previousHeading))
                                {
                                    previousHeading = dislayItem.Heading;
                                    dislayItem.Heading = String.Empty;
                                    results.Add(dislayItem);
                                }
                                else
                                {
                                    // See if dummys are needed after.
                                    previousHeading = dislayItem.Heading;
                                    results.Add(dislayItem);
                                }
                            }
                        });
                        // _logger.SendLogAsync($"EPOCHTIME-> {sb.ToString()}");
                        searcher.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.DisplayMessageBox(string.Format($"Index seem not ready.\nError: {ex.Message}"));
                return Tuple.Create(results, 0);
            }
            return Tuple.Create(results, maxAvailable);
        }

        private void InsertDocumentToResult(IEnumerable<DisplayItem> results, Document doc)
        {
            ((IList)results).Add(new DisplayItem(doc));
        }

        private void AddDummys(int remainingColumns, List<DisplayItem> results)
        {
            if(remainingColumns ==0)
            {
                return;
            }
            for(int i=0; i<remainingColumns; i++)
            {
                results.Add(new DisplayItem() {
                    FilePath = "",
                    Heading = "",
                     ThumbNail = ""
                });
            }
        }

        private string PreProcessSearchTerm(string searchTerm)
        {
            return searchTerm.Replace("and", "AND").Replace("or", "OR").Replace("not", "NOT");
        }
    }
}
