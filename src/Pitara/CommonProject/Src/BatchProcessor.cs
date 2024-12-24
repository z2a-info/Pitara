using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    class BatchProcessor<T>
    {
        string _title;
        //Func<IEnumerable<T>, int, int, int> _func = null;
        Func<T, Task<int>> _threadFunc = null;
        IEnumerable<T> _collection = null;
        int _batchSize;
        ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public BatchProcessor(string title, ILogger logger, IEnumerable<T> collection, int batchSize, Func<T, Task<int>> threadFunc)
        {
            _title = title;
            _threadFunc = threadFunc;
            _batchSize = batchSize;
            _collection = collection;
            _logger = logger;
        }
        public void StopBatch()
        {
            _cancellationTokenSource.Cancel();
        }
        public async Task Run()
        {
            // Break into batch
            int totalCount = _collection.Count();
            if (totalCount == 0)
            {
                // _logger.SendDebugLogAsync($"BatchProcessor - Nothing to Run()");
                return;
            }
            int lb = 0;
            int ub = 0;
            if (lb + _batchSize > totalCount)
            {
                ub = totalCount;
            }
            else
            {
                ub = lb + _batchSize;
            }

            while (lb < totalCount)
            {
                if(_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }
                await Process(lb, ub, _collection);
                lb = ub;
                if (lb + _batchSize > totalCount)
                {
                    ub = totalCount;
                }
                else
                {
                    ub = lb + _batchSize;
                }
            }
        }

        private async Task Process(int lb, int ub, IEnumerable<T> collection)
        {
            List<Task> tasks = new List<Task>();
            for(int i= lb; i < ub; i++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }
                int indexLocalCopy = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        _logger.SendDebugLogAsync($"Processing batch: {_title}");
                        await _threadFunc(collection.ElementAt(indexLocalCopy));
                    }
                    catch (Exception ex)
                    {
                        _logger.SendLogWithException("BatchProcessor - Process", ex);
                        throw;
                    }

                }));
            }
            await Task.WhenAll(tasks);
        }
    }
}
