using CommonProject.Src.Queues;
using CommonProject.Src.Views;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public class IndexWatcher
    {
        private static int _timerFrequency;
        private System.Timers.Timer _theTimer;
        private ILogger _logger;
        private ILuceneService _luceneService;
        private UserSettings _userSettings;
        ManualResetEvent _searchisGoingon;
        FolderView _folderView;


        public IndexWatcher(int timerFrequency, ILogger logger, UserSettings userSettings, ManualResetEvent searchisGoingon, ILuceneService luceneService, FolderView folderFileView)
        {
            _timerFrequency = timerFrequency;
            _logger = logger;
            _userSettings = userSettings;
            _luceneService = luceneService;
            _searchisGoingon = searchisGoingon;
            _folderView = folderFileView;
        }
        public void Watch()
        {
            _theTimer = new System.Timers.Timer(_timerFrequency);
            _theTimer.Elapsed += Timer_Elapsed;
            _theTimer.Start();
        }

        private async void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _theTimer.Stop();
            using (SingleThreaded singleThreaded = new SingleThreaded()) 
            {
                if(!singleThreaded.IsSafeToProceed())
                {
                    return;
                }
                _searchisGoingon.WaitOne();
                try
                {
                    using (StopWatchInternal sw = new StopWatchInternal("IndexWatcher", _logger))
                    {
                        ScoreDoc[] scoreDocs = null;
                        using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
                        {
                            if (!IndexReader.IndexExists(fs))
                            {
                                return;
                            }
                            int count = 0;
                            using (IndexReader reader = IndexReader.Open(fs, true))
                            {
                                count = reader.MaxDoc;
                                if (count == 0)
                                {
                                    return;
                                }
                            }
                            using (IndexSearcher searcher = new IndexSearcher(fs, true))
                            {
                                Query query = new TermQuery(new Term("DocType", "PhotoDocument"));
                                var filter = new QueryWrapperFilter(query);
                                var topDocs = searcher.Search(query, filter, count);
                                scoreDocs = topDocs.ScoreDocs;

                                List<string> listofFilesToremove = new List<string>();
                                object lockObj = new object();
                                {
                                    var bp = new BatchProcessor<ScoreDoc>("CleanupIndexInternal",
                                        _logger,
                                        scoreDocs,
                                        1, // one at a time.
                                        async (ScoreDoc item) =>
                                        {
                                            _searchisGoingon.WaitOne();
                                            // Don't remove this delay is imp to other batch processor can pick up
                                            await Utils.DelayRandome(20, 200);
                                            float score = item.Score;
                                            int docId = item.Doc;
                                            Document doc = searcher.Doc(docId);
                                            var filePath = doc.Get("PathKey").ToString();
                                            if (filePath.Equals(LuceneService.UniqKeyForVersion))
                                            {
                                                _logger.SendLogAsync("Was about to remove unique key");
                                                return 0;
                                            }
                                            if (!Utils.IsChildOfAnyParent(_userSettings.PhotoFolders.ToArray(), filePath))
                                            {
                                                lock (lockObj)
                                                {
                                                    listofFilesToremove.Add(filePath);
                                                }
                                                return 0;
                                            }
                                            if (!System.IO.File.Exists(filePath))
                                            {
                                                lock (lockObj)
                                                {
                                                    listofFilesToremove.Add(filePath);
                                                }
                                                return 0;
                                            }
                                            return 0;
                                        });
                                    await bp.Run();

                                    if (listofFilesToremove.Count() > 0)
                                    {
                                        var messageId = Guid.NewGuid().ToString();
                                        await LucenDocumentQueue<LuceneDocumentQueueMessage>.EnQueueAsync(new LuceneDocumentQueueMessage()
                                        {
                                            Action = ActionType.DELETE,
                                            FilePathCollection = listofFilesToremove,
                                            MessageId = messageId
                                        });
                                        await LucenDocumentQueue<LuceneDocumentQueueMessage>.WaitUntilProcessed(messageId);
                                        await Task.Delay(100); // Time for lucene to update.
                                    }
                                    if (listofFilesToremove.Count > 0)
                                    {
                                        _logger.SendLogAsync($"Index watcher, total checked: {scoreDocs.Count()}. Cleaned up: {listofFilesToremove.Count}");
                                        await _folderView.GetCache().TouchAsync();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.SendLogWithException("Index Watcher, error", ex);
                    throw;
                }
            }
        }
    }
}