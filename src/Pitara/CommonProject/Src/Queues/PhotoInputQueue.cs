using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Support;
using Microsoft.Extensions.Logging;
// using Pitara.PhotoStuff;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommonProject.Src.Queues
{
    public class PhotoInputQueue<T>: BaseQueue<T>
    {
        public static ConcurrentQueue<T> _theQueue = new ConcurrentQueue<T>();
        private ILogger _logger;
        private ILuceneService _luceneService;
        private UserSettings _userSettings;
        private OperatingSettings _operatingSettings;
        private AppSettings _appSettings;

        public PhotoInputQueue(int timerFrequency, int maxBatchSize, ILuceneService luceneService,
            UserSettings userSettings, OperatingSettings operatingSettings, AppSettings appSettings, ManualResetEvent searchisGoingon, ILogger logger)
            : base(_theQueue, timerFrequency, maxBatchSize, KnobId.PhotoInputQueue, searchisGoingon,logger)
        {
            _logger = logger;
            _luceneService = luceneService;
            _userSettings = userSettings;
            _operatingSettings = operatingSettings;
            _appSettings = appSettings;
        }
        public static async Task EnQueueAsync(T item)
        {
            await Task.Run(() => _theQueue.Enqueue(item));
        }
        public static bool ItemExist(T item)
        {
            return _theQueue.Any(x => x.Equals(item));
        }

        public override async Task ProcessBatch(IList<T> batch)
        {
            // Don't remove this delay is imp to other batch processor can pick up
            await Utils.DelayRandome(10, 100);

            // If they are removed from settings
            IList<T> updatedBatch = new List<T>();
            foreach (var item in batch)
            {
                foreach (var folder in _userSettings.PhotoFolders)
                {
                    if(item.ToString().StartsWith(folder))
                    {
                        updatedBatch.Add(item);
                        break;
                    }
                }
            }
            if (updatedBatch.Count() == 0)
            {
                return;
            }
            // Let's remove bad files.
            string[] badFiles = await Utils.ReadThreadSafe(_appSettings.BadFilesName, _logger, _appSettings);
            updatedBatch = updatedBatch.Where(x => Utils.IsValidFileToIndex(badFiles, x.ToString())).ToList();
            if (updatedBatch.Count() == 0)
            {
                return;
            }

            var stringArray = updatedBatch.Select(x => x.ToString()).ToArray();
            Dictionary<string, bool> result = new Dictionary<string, bool>();
            if (IndexReader.IndexExists(FSDirectory.Open(_userSettings.IndexFolder)))
            {
                result = await _luceneService.DoesPhotoExistInIndexAsync(stringArray);
            }
            // Those not already in index.
            stringArray = stringArray.Where(x => result.ContainsKey(x) && result[x] == false).ToArray();

            var docColl = await PhotoManipulation.GetDocumentsAsync(stringArray, _operatingSettings.ClientId);
            var messageId = System.Guid.NewGuid().ToString();
            await LucenDocumentQueue<LuceneDocumentQueueMessage>.EnQueueAsync(new LuceneDocumentQueueMessage()
            {
                Action = ActionType.ADD,
                Documents = docColl,
                MessageId = messageId
            });
            // await LucenDocumentQueue<LuceneDocumentQueueMessage>.WaitUntilProcessed(messageId);
        }
    }
}
