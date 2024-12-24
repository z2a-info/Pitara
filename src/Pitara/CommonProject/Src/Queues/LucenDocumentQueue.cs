using Lucene.Net.Documents;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommonProject.Src.Queues
{
    public enum ActionType
    {
        ADD,
        UPDATE,
        DELETE,
        DONE
    }
    public class LuceneDocumentQueueMessage
    {
        public ActionType Action { get; set; }
        // For add only
        public List<Document> Documents { get; set; }

        // For delete only.
        public List<string> FilePathCollection { get; set; }
        // Sender can optionally set, to confirm if messag was processed.
        public string MessageId { get; set; }
    }

    public class LucenDocumentQueue<T>: BaseQueue<T>
    {
        public static ConcurrentQueue<T> _theQueue = new ConcurrentQueue<T>();
        private ILogger _logger;
        private ILuceneService _luceneService;

        public LucenDocumentQueue(int timerFrequency, int maxBatchSize, ILuceneService luceneService, ManualResetEvent searchisGoingon, ILogger logger)
            : base(_theQueue, timerFrequency, maxBatchSize, KnobId.LucenDocumentQueue, searchisGoingon, logger)
        {
            _logger = logger;
            _luceneService = luceneService;
        }
        public static async Task EnQueueAsync(T item)
        {
            await Task.Run(()=>_theQueue.Enqueue(item));
        }

        public static async Task WaitUntilProcessed(string messageId)
        {
            var tempQueue = _theQueue.Select(x => x as LuceneDocumentQueueMessage);
            while (tempQueue.Any(x => x.MessageId == messageId))
            {
                await Task.Delay(50);
                tempQueue = _theQueue.Select(x => x as LuceneDocumentQueueMessage);
            }
        }

        public override async Task ProcessBatch(IList<T> batch)
        {
            var bp = new BatchProcessor<T>("LucenDocumentQueue - ProcessBatch",
                    _logger,
                    batch,
                    1, // Has to be one, Lucene not thread safe for writing.
                    async (item) =>
                    {
                        var message = item as LuceneDocumentQueueMessage;
                        if(message?.Documents?.Count() == 0)
                        {
                            _logger.SendDebugLogAsync("No documents supplied to process in the back. Fix this.");
                            return 0;
                        }

                        switch (message.Action)
                        {
                            case ActionType.ADD:
                                {
                                    await _luceneService.AddDocumentToIndexAsync(message.Documents);
                                    _logger.SendDebugLogAsync($"Added: {message.Documents.Count()} files to index");
                                    break;
                                }
                            case ActionType.UPDATE:
                                {
                                    await _luceneService.UpdateDocumentToIndexAsync(message.Documents);
                                    _logger.SendDebugLogAsync($"Updated: {message.Documents.Count()} files to index");
                                    break;
                                }
                            case ActionType.DELETE:
                                {
                                    await _luceneService.DeleteDocumentFilePathAsync(message.FilePathCollection.ToArray());
                                    _logger.SendDebugLogAsync($"Deleted: {message.FilePathCollection.Count()} files from index");
                                    break;
                                }
                            default:
                                {
                                    _logger.SendLogAsync($"Undefined ActionType: {message.Action}");
                                    break;
                                }
                        }
                        return 0;
                    });
            await bp.Run();
        }
    }
}
