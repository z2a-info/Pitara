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
    public class UserPhotoInputQueue<T>: BaseQueue<T>
    {
        public static ConcurrentQueue<T> _theQueue = new ConcurrentQueue<T>();
        private ILogger _logger;
        private OperatingSettings _operatingSettings;
        private AppSettings _appSettings;

        public UserPhotoInputQueue(int timerFrequency, int maxBatchSize, OperatingSettings operatingSettings, AppSettings appSettings, ManualResetEvent searchisGoingon, ILogger logger)
            : base(_theQueue, timerFrequency, maxBatchSize, KnobId.UserPhotoInputQueue, searchisGoingon, logger)
        {
            _logger = logger;
            _operatingSettings = operatingSettings;
            _appSettings = appSettings;
        }
        public static async Task EnQueueAsync(T item)
        {
            await Task.Run(() => _theQueue.Enqueue(item));
        }

        public override async Task ProcessBatch(IList<T> batch)
        {
            // Let's remove bad files.
            string[] badFiles = await Utils.ReadThreadSafe(_appSettings.BadFilesName, _logger, _appSettings);
            batch = batch.Where(x => Utils.IsValidFileToIndex(badFiles, x.ToString())).ToList();
            if (batch.Count() == 0)
            {
                return;
            }
            var stringArray = batch.Select(x => x.ToString()).ToArray();
            var docColl = await PhotoManipulation.GetDocumentsAsync(stringArray, _operatingSettings.ClientId);
            await LucenDocumentQueue<LuceneDocumentQueueMessage>.EnQueueAsync(new LuceneDocumentQueueMessage()
            {
                Action = ActionType.ADD,
                Documents = docColl
            });
        }
    }
}
