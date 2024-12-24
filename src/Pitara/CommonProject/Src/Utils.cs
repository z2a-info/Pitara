using CommonProject.Src.Cache;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CommonProject.Src
{
    public class Utils
    {
        private static ILogger _logger;
        private static AppSettings _appSettings;
        public static void Init(ILogger logger, AppSettings appSettings)
        {
            _logger = logger;
            _appSettings = appSettings;
        }
        public static async Task DelayRandome(int low, int high)
        {
            Random rnd = new Random();
            int delay = rnd.Next(low, high);
            await Task.Delay(delay);
        }
        public static string DayString(int day)
        {
            string suffix = string.Empty;
            switch (day)
            {
                case 1:
                case 21:
                case 31:
                    suffix = "st";
                    break;
                case 2:
                case 22:
                    suffix = "nd";
                    break;
                case 3:
                case 23:
                    suffix = "rd";
                    break;
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                case 13:
                case 14:
                case 15:
                case 16:
                case 17:
                case 18:
                case 19:
                case 20:
                case 24:
                case 25:
                case 26:
                case 27:
                case 28:
                case 29:
                case 30:
                    suffix = "th";
                    break;
                default:
                    System.Diagnostics.Debug.Assert(1 < 0);
                    break;
            }
            return day.ToString() + suffix;
        }

        public static string GetLastNYearsQueryFragment(int n)
        {
            if (n == 0)
            {
                return string.Empty;
            }

            DateTime date = DateTime.Now;
            var year = date.Year;
            StringBuilder sb = new StringBuilder();
            sb.Append("( ");
            sb.Append($"{year - 1}");

            for (int i = 2; i <= n; i++)
            {
                sb.Append($" or {year - i}");
            }
            sb.Append(" )");
            return sb.ToString();
        }
        public static string GetWeekNQueryFragment(int n)
        {
            if (n == 0)
            {
                return string.Empty;
            }
            switch (n)
            {
                case 1:
                    {
                        return $"(1st or 2nd or 3rd or 4th or 5th or 6th or 7th)";
                    }
                case 2:
                    {
                        return $"(8th or 9th or 10th or 11th or 12th or 13th or 14th)";
                    }
                case 3:
                    {
                        return $"(15st or 16th or 17th or 18th or 19th or 20th or 21th)";
                    }
                case 4:
                    {
                        return $"(22nd or 23rd or 24th or 25th or 26th or 27th or 28th)";
                    }
                case 5:
                    {
                        return $"(29th or 30th or 31st)";
                    }
                default:
                    return string.Empty;
            }
        }
        public static string GetLastNMonthsQueryFragment(int n)
        {
            if(n ==0)
            {
                return string.Empty;
            }

            DateTime date = DateTime.Now;
            var year = date.Year;
            StringBuilder sb = new StringBuilder();
            sb.Append("( ");
            sb.Append($"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(date.AddMonths(-1).Month)}");

            for (int i = 2; i <= n; i++)
            {
                sb.Append($" or {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(date.AddMonths(-i).Month)}");
            }
            sb.Append($" ) AND {year}");
            return sb.ToString();
        }
        public static string GetHigherThanNKFeetQueryFragment(int nKFeet)
        {
            int maxKeFeets = 100;
            if (nKFeet == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("( ");
            sb.Append($"{nKFeet}kfeet");

            for (int i = nKFeet +1; i <= maxKeFeets; i++)
            {
                sb.Append($" or {i}kfeet");
            }
            sb.Append($" ) ");
            return sb.ToString();
        }
        public static async void WriteThreadSafe(string filename, string[] lines, ILogger logger, AppSettings appSettings)
        {
            BaseThreadSafeFileCache<string> file = new BaseThreadSafeFileCache<string>(filename, logger, appSettings);
            await file.LoadAsync();
            bool addedNew = false;
            foreach(var line in lines)
            {
                addedNew = true;
                file.AddCommon(line.Trim(), "1");
            }
            if (addedNew)
            {
                await file.SaveAsync();
            }
        }
        public static async Task<string[]> ReadThreadSafe(string filename, ILogger logger, AppSettings appSettings)
        {
            BaseThreadSafeFileCache<string> file = new BaseThreadSafeFileCache<string>(filename, logger, appSettings);
            await file.LoadAsync();
            return file.DataKeyPairDictionary.Keys.ToArray();
        }
        //  This function can't use logger.
        public static async Task<string> DetermineAppDataFolder()
        {
            return await Task.Run(()=> {
                var appDir = Directory.GetParent(AppContext.BaseDirectory).FullName;
                // Try access uder here. If works then let that be appData folder.
                try
                {
                    appDir = appDir.TrimEnd('\\');
                    appDir += @"\";
                    appDir += @"Pitara";
                    appDir += @"\";
                    Utils.EnsureFolderExist(appDir);
                    return appDir;
                    // throw new Exception("Repro for C program files install.");
                }
                catch (Exception)
                {
                    appDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    appDir = appDir.TrimEnd('\\');
                    appDir += @"\";
                    appDir += @"Pitara";
                    appDir += @"\";
                    return appDir;
                }
            });
        }
        public static bool IsChildOfAnyParent(string[] parents, string child)
        {
            foreach ( string parent in parents)
            {
                if(Utils.IsChildOfParent(parent, child))
                {
                    return true;
                }
            }
            return false;
        }
        public static bool IsChildOfParent(string parent, string child)
        {
            if(child.ToLower().StartsWith(parent.ToLower()))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool IsValidFileToIndex(string[] badFiles, string file)
        {
            if (!File.Exists(file))
            {
                return false;
            }
            FileInfo fi = new FileInfo(file);
            if (fi.Length == 0)
            {
                _logger.SendDebugLogAsync($"Skipping due to zero bytes size. File: {file}");
                return false;
            }
            if (badFiles.Count() > 0)
            {
                var fileFound = Array.Find(badFiles, x => x == file);
                if (!string.IsNullOrEmpty(fileFound))
                {
                    _logger.SendDebugLogAsync($"Found the file in bad files: {fileFound}");
                    return false;
                }
            }
            return true;    
        }
        public static async Task EnsureMoveToRecycleBin(string src, bool deleteIfEmpty = false)
        {
            await Task.Run(() => {
                try
                {
                    FileSystem.DeleteFile(file: src, showUI:UIOption.AllDialogs, recycle: RecycleOption.SendToRecycleBin);
                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"Can't move file. Src :{src}. Error: {ex.Message}");
                    throw;
                }
            });
        }

        public static async Task EnsureMoveAsync(string src, string dest, bool deleteIfEmpty = false)
        {
            await Task.Run(async ()=> {
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(dest)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    }
                    string destinationPath = NextAvailableFileName(Path.GetDirectoryName(dest), Path.GetFileName(dest));
                    File.Move(src, destinationPath);
                    if(deleteIfEmpty && Utils.IsDirectoryEmpty(Path.GetDirectoryName(src)))
                    {
                        _logger.SendLogAsync($"Deleting empty folder :{Path.GetDirectoryName(src)}");
                        await Utils.EnsureDeleteFolderAsync(Path.GetDirectoryName(src));
                    }
                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"Can't move file. Src :{src}. Dst: {dest}. Error: {ex.Message}");
                    throw;
                }
            });
        }

        public static async Task<List<string>> EnsureIfFilesExist(List<string> files)
        {
            var taskArray = new List<Task>();
            object lockObj = new object();
            List<string> validFiles = new List<string>();
            bool atLeastOneMissing = false;
            foreach (var item in files)
            {
                taskArray.Add(Task.Run(() =>
                {
                    if (File.Exists(item))
                    {
                        lock (lockObj)
                        {
                            validFiles.Add(item);
                        }
                    }
                    else
                    {
                        atLeastOneMissing = true;
                        _logger.SendLogAsync($"File {item} is present in index, but moved from hard drive.");
                    }
                }));
            }
            await Task.WhenAll(taskArray.ToArray());
            if (atLeastOneMissing)
            {
                var error = $"One or more file(s) not available in the Hard Drive, though present in index. Close Pitara then re-lunch. Wait until indexing is 100% and this error will be gone.\nMissing file information is in the log file.";
                Utils.DisplayMessageBox(error);
            }
            return validFiles;
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

        private static byte[] GetHash(string inputString)
        {
            HashAlgorithm algorithm = SHA256.Create();
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }
        public static void MakeWritable(string filePath)
        {
            FileAttributes attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                attributes = attributes & ~FileAttributes.ReadOnly;
                File.SetAttributes(filePath, attributes);
            }
        }

        public static string GetUniquePathKey(string inputString)
        {
            return inputString.ToUpper().Trim();
        }
        public static string GetHashOfAnyString(string inputString)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in GetHash(inputString))
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }
        public static void EnsureFolderExist(string FolderName)
        {
            if (!Directory.Exists(FolderName))
            {
                Directory.CreateDirectory(FolderName);
            }
        }
        public static bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        public static async Task EnsureDeleteFolderAsync(string FolderName)
        {
            await Task.Run(() => {
                if (Directory.Exists(FolderName))
                {
                    Directory.Delete(FolderName.TrimEnd('\\'), true);
                    Task t = Task.Run(() =>
                    {
                        while (Directory.Exists(FolderName))
                        {
                            System.Threading.Thread.Sleep(10);
                        }
                    });

                    t.Wait();
                }
            });
        }
        public static async Task EnsureCleanFolderAsync(string FolderName)
        {
            await Task.Run(()=> {
                if (Directory.Exists(FolderName))
                {
                    Directory.Delete(FolderName.TrimEnd('\\'), true);
                    Task t = Task.Run(() =>
                    {
                        while (Directory.Exists(FolderName))
                        {
                            System.Threading.Thread.Sleep(10);
                        }
                    });

                    t.Wait();
                }
                Directory.CreateDirectory(FolderName);
            });
        }
        public static string FixSlash(string inputPath)
        {
            return inputPath.TrimEnd(new char[] { '\\' }) + "\\";
        }
        public static string GetExecutingDirectoryName()
        {
            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            
            return new FileInfo(Uri.UnescapeDataString(location.AbsolutePath)).Directory.FullName;
        }
        public static async void ProcessStartWrapperAsync(string fileName, string args = "")
        {
            try
            {
                await Task.Run(()=> 
                {

                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.CreateNoWindow = false;
                    startInfo.UseShellExecute = true;
                    startInfo.FileName = fileName;
                    startInfo.WindowStyle = ProcessWindowStyle.Normal;
                    startInfo.Arguments = args;

                    Process.Start(startInfo);
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error: {ex.Message}";
                _logger.SendLogAsync(errorMessage);
                Utils.DisplayMessageBox(errorMessage);
            }
        }
        //public static void ProcessStartWrapper(string fileName)
        //{
        //    try
        //    {
        //        Process.Start(fileName);
        //    }
        //    catch(Exception ex)
        //    {
        //        string errorMessage = $"Error: {ex.Message}";
        //        _logger.SendLogAsync(errorMessage);
        //        Utils.DisplayMessageBox(errorMessage);
        //    }
        //}
        public static bool IsEntirelyAlphabateString(string input)
        {
            return input.All(char.IsLetter);
        }
        public static bool DoesContainsNumbers(string input)
        {
            return input.Any(char.IsDigit);
        }
        public static bool IsEntirelyNumericString(string input)
        {
            return input.All(char.IsDigit);
        }
        public static void EnsureCopyToBucket(string src, string dest)
        {
            //if(File.Exists(dest))
            //{
            //    return;
            //}
            //File.Copy(src, dest);

            int attempts = 2;
            while (attempts > 0)
            {
                try
                {
                    File.Copy(src, dest);
                    attempts--;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"WARNING! Can't copy, attempting destination rename: {src} -> {dest}. Error:{ex.Message}");
                    dest = Utils.NextAvailableFileName(Path.GetDirectoryName(dest), Path.GetFileName(dest));
                }
            }
        }

        public static void DisplayMessageBox(string message, Window owner = null)
        {
            _logger.SendLogAsync(message);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (owner == null)
                {
                    if (Application.Current.MainWindow != null)
                    {
                        MessageBox.Show(
                            Application.Current.MainWindow, 
                            message, 
                            _appSettings.MessageBoxCaption, 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            message,
                            _appSettings.MessageBoxCaption,
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show(
                        owner, 
                        message, 
                        _appSettings.MessageBoxCaption, 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
            }
            );
        }
        public static string RemoveDuplicateWords(string input)
        {
            var tempArray = input.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            var unique = tempArray
                .Select(x => x.Trim())
                .Distinct().ToArray();
            return string.Join(" ", unique);
        }
        public static MessageBoxResult DisplayMessageBoxAskYesNo(string message, Window owner = null)
        {
            // Application.Current.MainWindow for main window owner.
            _logger.SendLogAsync(message);
            return Application.Current.Dispatcher.Invoke(() =>
            {
                if (owner == null)
                {
                    if (Application.Current.MainWindow != null)
                    {
                        return System.Windows.MessageBox.Show(
                            Application.Current.MainWindow,
                            message,
                            _appSettings.MessageBoxCaption,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information
                            );
                    }
                    else
                    {
                        return System.Windows.MessageBox.Show(
                            message,
                            _appSettings.MessageBoxCaption,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information
                            );
                    }
                }
                else
                {
                    return System.Windows.MessageBox.Show(
                        owner,
                        message,
                        _appSettings.MessageBoxCaption,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                        );
                }
            }
            );
        }
    }
}