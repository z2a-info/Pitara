using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public enum KnobId
    {
        CLEAN_INDEX =0,
        FileWatcher =1,
        ALREADY_INDEXED =2,
        UPDATE_TABS = 3,
        UPDATE_KEYWORDCACHE_FROM_INDEX =4,
        FAVORITE_VIEW_UPDATE = 5,
        CUSTOME_TAG_UPDATE_TIMER = 6,
        BaseQueue = 7,
        PhotoInputQueue = 8,
        UserPhotoInputQueue = 9,
        LucenDocumentQueue = 10,
        IndexWatcher = 11,
        MetaQueueMessage = 12
    }
    class Knob
    {
        // public CancellationToken _cancellationToken = new CancellationToken();
        public CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();  
        public Task _task = Task.CompletedTask;
    }
    public class Spinner
    {
        // Keep static.
        static ConcurrentDictionary<KnobId, Knob> _allKnobsOfSystem = new ConcurrentDictionary<KnobId, Knob>();

        public static async void StopAll()
        {
            foreach (var item in _allKnobsOfSystem)
            {
                await Spinner.Stop(item.Key);
            }
        }
        public static async Task Stop(KnobId knobId)
        {
            if (!_allKnobsOfSystem.ContainsKey(knobId))
            {
                throw new Exception($"Knob not defined - {knobId}");
            }
            _allKnobsOfSystem[knobId]._cancellationTokenSource.Cancel();
            while(_allKnobsOfSystem[knobId]._task.Status == TaskStatus.Running)
            {
                await Task.Delay(50);
            }
        }

        string _title;
        Func<CancellationToken, Task<int>> _threadFunc = null;
        ILogger _logger;
        Knob _knob;
        KnobId _knobId;

        public Spinner(ILogger logger, KnobId knobId, Func<CancellationToken, Task<int>> threadFunc)
        {
            _title = knobId.ToString();
            _threadFunc = threadFunc;
            _logger = logger;
            _knobId = knobId;
        }
        public async Task SpinIfStopped()
        {
            if (!_allKnobsOfSystem.ContainsKey(_knobId))
            {
                if (!_allKnobsOfSystem.TryAdd(_knobId, new Knob()))
                {
                    _logger.SendDebugLogAsync($"{_title} - Already added to knob so returning...");
                    return;
                }
            }
            _knob = _allKnobsOfSystem[_knobId];
            if (_knob._task.Status == TaskStatus.Running || _knob._task.Status == TaskStatus.WaitingForActivation)
            {
                // _logger.SendDebugLogAsync($"{_title} - Already running so returning...");
                return;
            }
            _knob._task = Task.Run(async () =>
            {
                try
                {
                    // _logger.SendDebugLogAsync($"{_title} - Starting");
                    await _threadFunc(_knob._cancellationTokenSource.Token);
                    // _logger.SendDebugLogAsync($"{_title} - Done");
                }
                catch (Exception ex)
                {
                    _logger.SendLogWithException("Spinner - Spin", ex);
                    throw;
                }
            });
            await _knob._task;
        }
    }
}