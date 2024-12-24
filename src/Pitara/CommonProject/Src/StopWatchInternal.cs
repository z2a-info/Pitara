using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace CommonProject.Src
{
    public class StopWatchInternal : IDisposable
    {
        private static ILogger _logger;// = AsyncLog.GetGlobalLogger();
        public Stopwatch _stopwatch = new Stopwatch();
        string _title;
        bool _debugLogOnly;
        bool _isRunning = false;
        public StopWatchInternal(string title, ILogger logger, bool debugLogOnly = true)
        {
            _title = title;
            _logger = logger;
            _debugLogOnly = debugLogOnly;
            _stopwatch.Start();
            _isRunning = true;
        }
        public string Stop()
        {
            _stopwatch.Stop();

            TimeSpan ts = _stopwatch.Elapsed;

            // Format and display the TimeSpan value.
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            if(_isRunning)
            {
                _isRunning = false;
                if (_debugLogOnly)
                {
                    _logger.SendDebugLogAsync($"Time taken: {_title} " + elapsedTime);
                }
                else
                {
                    _logger.SendLogAsync($"Time taken: {_title} " + elapsedTime);
                }
            }
            return elapsedTime;
        }
        public void Dispose()
        {
            Stop();
        }
    }
}
