using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommonProject.Src.Queues
{
    public class BaseQueue<T> 
    {
        public ConcurrentQueue<T> _childClassQueue;
        private static int _timerFrequency;
        private System.Timers.Timer _watchMessagesTimer;
        private int _maxBatchSize;
        private ILogger _logger;
        private KnobId _knobId;
        private ManualResetEvent _searchisGoingon;

        public BaseQueue(ConcurrentQueue<T> childClassQueue, int timerFrequency, int maxBatchSize, KnobId knobId, ManualResetEvent searchisGoingon, ILogger logger)
        {
            _childClassQueue = childClassQueue;
            _timerFrequency = timerFrequency;
            _maxBatchSize = maxBatchSize;
            _searchisGoingon = searchisGoingon;
            _logger = logger;
            _knobId = knobId;
        }
        public void Start()
        {
            _watchMessagesTimer = new System.Timers.Timer(_timerFrequency);
            _watchMessagesTimer.Elapsed += _watchMessagesTimer_Elapsed;
            _watchMessagesTimer.Start();
        }
        private async void _watchMessagesTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Spinner spinner = new Spinner(_logger,
                _knobId,
                async (CancellationToken cancellationToken) =>
                {
                    int batchSize = 0;
                    if (_childClassQueue.IsEmpty)
                    {
                        return 0;
                    }
                    IList<T> batch = new List<T>();
                    while (!_childClassQueue.IsEmpty && batchSize < _maxBatchSize)
                    {
                        T item;
                        if (_childClassQueue.TryDequeue(out item))
                        {
                            batch.Add(item);
                            batchSize++;
                        }
                    }
                    await Task.Run(() => ProcessBatchInternal(batch));
                    return 0;
                });
            await spinner.SpinIfStopped();
        }
        private async Task ProcessBatchInternal(IList<T> batch)
        {
            if (_searchisGoingon.WaitOne())
            {
                _logger.SendDebugLogAsync($"Processing Queue:{_knobId.ToString()}");
                // This will call override from children's implementation.
                await ProcessBatch(batch);
            }
            return;
        }
        public virtual Task ProcessBatch(IList<T> batch) 
        {
            if (_searchisGoingon.WaitOne())
            {
                _logger.SendDebugLogAsync($"Processing batch:{_knobId.ToString()}");
            }

            return Task.Run(() => { return 0; });
        }
    }
}
