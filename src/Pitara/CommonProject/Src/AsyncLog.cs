using Lucene.Net.Support;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
// using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace CommonProject.Src
{
    public class DummyLog : ILogger
    {
        public string LogFileName;

        public DummyLog(string appDataFolder)
        {
            LogFileName = appDataFolder + "PitaraLogs.txt";
        }
        public void DumpMemUsageIfHigherThenBefore()
        {
        }

        public string GetLogFilePath()
        {
            return LogFileName;
        }
        public void SendDebugLogAsync(string str)
        {
        }
        public void SendLogAsync(string str)
        {
        }
        public string SendLogWithException(string str, Exception ex)
        {
            return string.Empty;
        }
    }
    public class AsyncLog: ILogger
    {
        private static Object logLock = new Object();
        private bool debugEnabled = false;
        public string LogFileName;
        double LogFileRoundoffSize = 5 * 1024 * 1024; // 5 MB
        private ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private Timer _timer = new Timer(100);
        private long _highestMemory = 0;

        // private static AsyncLog globalLogger = null;
        public string GetLogFilePath()
        {
            return LogFileName;
        }
        public void DumpMemUsageIfHigherThenBefore()
        {
            if (_highestMemory == 0)
            {
                SendDebugLogAsync($">> Starting memory usage.");
            }
            var proc = Process.GetCurrentProcess();
            var currentMemUsage = proc.PrivateMemorySize64;
            if (currentMemUsage > _highestMemory)
            {
                _highestMemory = currentMemUsage;
            }
            SendDebugLogAsync($"Memory Usage - Current: {BytesToString(currentMemUsage)} Max:{BytesToString(_highestMemory)}");
        }
        private static String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        public AsyncLog(string appDataFolder, bool ifDebugMode)
        {
            LogFileName = appDataFolder + "PitaraLogs.txt";
            if (ifDebugMode)
            {
                this.debugEnabled = true;
                SendLogAsync("DEBUG mode enabled..");
            }
            if(!DoWeHaveWriteAccess(appDataFolder))
            {
                throw new Exception("No write access");
            }
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            string message;
            while (_queue.TryDequeue(out message))
            {
                SendLogInternal(message);
            }
        }

        public bool DoWeHaveWriteAccess(string appDataFolder)
        {
            try
            {
                using (StreamWriter SW = File.AppendText(this.LogFileName))
                {
                    SW.WriteLine(". ");
                    SW.Flush();
                    SW.Close();
                    return true;
                }
            }
            catch (Exception)
            {
                MessageBox.Show(
                "Pitara doesn't have permission to write to the log file. Logs won't be available.",
                    "Pitara",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Utils.Init(new DummyLog(appDataFolder), null);
                return false;
            }
        }

        public void SendDebugLogAsync(string str)
        {
            if (this.debugEnabled)
            {
                SendLogAsync("DBG: "+str);
            }
        }
        public async void SendLogAsync(string str) 
        {
            // await Task.Run(() => SendLogInternal(str));
            await Task.Run(() => _queue.Enqueue(str));
        }


        public string SendLogWithException(string str, Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Error - {str}").Append("\n").Append(ex.Message).Append("\n").Append(ex.StackTrace).Append("\n");
            if (ex.InnerException != null)
            {
                sb.Append("Innser Exception - \n");
                sb.Append(ex.InnerException.Message).Append("\n").Append(ex.InnerException.StackTrace);
            }
            SendLogAsync(sb.ToString());
            return sb.ToString();
        }

        private void SendLogInternal(string str)
        {
            string strmsg = string.Format("{0}: {1}{2}", DateTime.Now.ToString(), str, Environment.NewLine);
            try
            {
                lock (logLock)
                {
                    if (File.Exists(this.LogFileName))
                    {
                        FileInfo fi = new FileInfo(this.LogFileName);
                        if (fi.Length >= LogFileRoundoffSize)
                        {
                            File.Move(this.LogFileName, NextAvailableFileName(Path.GetDirectoryName(this.LogFileName), Path.GetFileName(this.LogFileName)));
                        }
                    }
                    using (StreamWriter SW = File.AppendText(this.LogFileName))
                    {
                        SW.WriteLine(strmsg);
                        SW.Flush();
                        SW.Close();
                    }
                }
            }
            catch(Exception ex)
            {
                Utils.DisplayMessageBox(ex.Message);
                throw;
            }
        }
        public static string NextAvailableFileName(string folderName, string fileName)
        {
            int i = 0;
            string fileNameCopy = folderName + @"\" + fileName;

            while (File.Exists(fileNameCopy) == true)
            {
                fileNameCopy = folderName + @"\" + Path.GetFileNameWithoutExtension(fileName) + @"__dup__" + string.Format("{0:d}", i++) + Path.GetExtension(fileName);
            }

            return fileNameCopy;
        }


    }
}
