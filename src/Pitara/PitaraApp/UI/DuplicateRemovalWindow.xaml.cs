using CommonProject.Src;
using CommonProject.Src.Cache;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Pitara
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class DuplicateRemovalWindow : Window
    {
        public UserSettings Settings;

        private ILuceneService _luceneService;
        private UserSettings _backupSettings;
        private AppSettings _appSettings;
        private ILogger _logger;

        public DuplicateRemovalWindow(UserSettings userSettings, 
            ILogger logger,
            ILuceneService luceneService,
            AppSettings appSettings)
        {
            _logger = logger;
            _backupSettings = userSettings.Clone();
            Settings = userSettings.Clone();
            DataContext = Settings;
            _luceneService = luceneService;
            _appSettings = appSettings;

            PreviewKeyDown += DuplicateRemovalWindow_PreviewKeyDown;
            Loaded += DuplicateRemovalWindow_Loaded;
            Closing += DuplicateRemovalWindow_Closing;
            InitializeComponent();
        }

        private async void DuplicateRemovalWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_searchTask != null && _searchTask.Status != TaskStatus.RanToCompletion)
            {
                var response = Utils.DisplayMessageBoxAskYesNo("Searching for duplicate is underway. Are you sure?", this);
                if (response == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
                else
                {
                    // Stop operations and close.
                    await StopSearchTaskAsync();
                    e.Cancel = false;
                }
            }
            if (_moveTask != null && _moveTask.Status != TaskStatus.RanToCompletion)
            {
                var response = Utils.DisplayMessageBoxAskYesNo("Moving duplicate file is underway. Are you sure?", this);
                if (response == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
                else
                {
                    // Stop operations and close.
                    await StopMoveTaskAsync();
                    e.Cancel = false;
                }
            }
        }

        private void DuplicateRemovalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ButtonFindDuplicate.IsEnabled = true;
            ButtonMoveDuplicate.IsEnabled = false;
            // SelectDupFolder.IsEnabled = false;
            DuplicateFolderTextBox.IsEnabled = false;
        }

        private void DuplicateRemovalWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                btnCancelData_Click(sender, e);
            }
        }


        // Cancel
        private async void btnCancelData_Click(object sender, RoutedEventArgs e)
        {
            if (_searchTask != null && _searchTask.Status != TaskStatus.RanToCompletion)
            {
                var response = Utils.DisplayMessageBoxAskYesNo("Searching for duplicate is underway. Are you sure?", this);
                if (response == MessageBoxResult.No)
                {
                    return;
                }
                else
                {
                    // Stop operations and close.
                    await StopSearchTaskAsync();
                }
            }
            if (_moveTask != null && _moveTask.Status != TaskStatus.RanToCompletion)
            {
                var response = Utils.DisplayMessageBoxAskYesNo("Moving duplicate file is underway. Are you sure?", this);
                if (response == MessageBoxResult.No)
                {
                    return;
                }
                else
                {
                    // Stop operations and close.
                    await StopMoveTaskAsync();
                }
            }
            this.DialogResult = false;
            this.Close();
        }

        // Select duplicate folder
        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    var selectedFolder = Utils.LetUserPickAFolder(Settings.DuplicateFolder, 
        //        "Select a folder to move duplicate photos.");
        //    if (string.IsNullOrEmpty(SettingsWindow.CheckIfSpecialFolder(Settings, selectedFolder, SettingsWindow.CallerType.Duplicate, this)))
        //    {
        //        return;
        //    }
        //    Settings.DuplicateFolder = selectedFolder;
        //    DuplicateFolderTextBox.Text = Settings.DuplicateFolder;
        //}
        private void DisableAll()
        {
            ButtonFindDuplicate.IsEnabled = false;
            ButtonMoveDuplicate.IsEnabled = false;
            // SelectDupFolder.IsEnabled = false;
        }
        public async Task StopMoveTaskAsync()
        {
            if (_cancellationMoveTokenSource != null && _moveTask != null)
            {
                _cancellationMoveTokenSource.Cancel();
                while (_moveTask.Status != TaskStatus.RanToCompletion)
                {
                    await Task.Delay(100);
                }
            }
        }

        public async Task StopSearchTaskAsync()
        {
            if (_cancellationSearchTokenSource != null && _searchTask!= null)
            {
                _cancellationSearchTokenSource.Cancel();
                while (_searchTask.Status != TaskStatus.RanToCompletion)
                {
                    await Task.Delay(100);
                }
            }
        }

        Task _searchTask = null;
        private CancellationTokenSource _cancellationSearchTokenSource = null;

        // Search
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (_luceneService.IsIndexingGoingOn())
            {
                Utils.DisplayMessageBox("Can not find and isolate duplicates while indexing is going on.\nPlease wait until indexing is complete then try again.", this);
                return;
            }

            DisableAll();
            int totalDuplicateFound = 0;
            using (FSDirectory fs = FSDirectory.Open(Settings.IndexFolder))
            {
                if (IndexReader.IndexExists(fs))
                {
                    using (IndexReader reader = IndexReader.Open(fs, true))
                    {
                        DuplicateCache dupCache = new DuplicateCache(
                            _appSettings.DuplicateCacheFileName, _logger, _appSettings);
                        await dupCache.Reset();
                        await dupCache.LoadAsync();

                        int count = reader.NumDocs();
                        DuplicateStatusLabel.Content = $"Finding duplicates..";
                        {
                            _cancellationSearchTokenSource = new CancellationTokenSource();
                            var cancellationToken = _cancellationSearchTokenSource.Token;
                            _searchTask = Task.Run(async ()=> {
                                for (double i = 0; i < reader.MaxDoc; i++)
                                {
                                    if (cancellationToken.IsCancellationRequested)
                                    {
                                        _logger.SendLogAsync($"Search cancelled by user.");
                                        break;
                                    }
                                    var percentage = (i / (double)reader.MaxDoc) * 100;
                                    // ProgressBar.Value = percentage;
                                    this.Dispatcher.Invoke((Action)(() =>
                                    {
                                        ProgressBar.Value = percentage;
                                        ProgressText.Text = $"{Math.Round(percentage, 2)} %";
                                    }));

                                    if (reader.IsDeleted((int)i))
                                    {
                                        continue;
                                    }
                                    Document doc = reader.Document((int)i);
                                    var contentKey = doc.Get("ContentKey");
                                    var filePath = doc.Get("FilePath");
                                    if (dupCache.DataKeyPairDictionary.ContainsKey(contentKey))
                                    {
                                        continue;
                                    }

                                    var listofDups = await _luceneService.DoesContentKeyAlreadyExistAsync(contentKey, filePath);
                                    if (listofDups.Count() > 0)
                                    {
                                        dupCache.Add(contentKey, listofDups);
                                        totalDuplicateFound += listofDups.Count - 1;
                                    }

                                    this.Dispatcher.Invoke((Action)(() =>
                                    {
                                        DuplicateStatusLabel.Content = $"Found: {totalDuplicateFound}";
                                    }));
                                }
                            });
                            await _searchTask;
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }
                            _logger.SendLogAsync($"Total: {totalDuplicateFound} duplicates found.");
                            ProgressBar.Value = 100;
                            ProgressText.Text = $"{100} %";

                            await dupCache.SaveAsync();
                        }
                        reader.Dispose();
                        fs.Dispose();
                    }
                }
            }
            if (totalDuplicateFound == 0)
            {
                Utils.DisplayMessageBox("Great NEWS!. No duplicates found!", this);
            }
            else
            {
                Utils.DisplayMessageBox($"Duplicate photos discovered.\nTotal: {totalDuplicateFound} duplicates found.\nYou can click 'Move Duplicates' button to move them over to Duplicate folder.", this);
                this.ButtonMoveDuplicate.IsEnabled = true;
                // this.SelectDupFolder.IsEnabled = true;
            }
            this.ButtonFindDuplicate.IsEnabled = true;
            ProgressBar.Value = 0;
            ProgressText.Text = $"";
        }

        Task _moveTask = null;
        private CancellationTokenSource _cancellationMoveTokenSource = null;

        // Move duplicate photos.
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            //if (_luceneService.IsIndexingGoingOn())
            //{
            //    Utils.DisplayMessageBox("Duplicates can not be moved while indexing is going on.\nPlease wait until indexing is finished then retry.", this);
            //    return;
            //}

            //DuplicateCache dupCache = new DuplicateCache(
            //    _appSettings.DuplicateCacheFileName, _logger, _appSettings);
            //await dupCache.LoadAsync();
            //if (dupCache.DataKeyPairDictionary.Count() ==0)
            //{
            //    Utils.DisplayMessageBox("No duplicates to move.", this);
            //    return;
            //}
            //if (MessageBoxResult.No == Utils.DisplayMessageBoxAskYesNo("Going to move duplicate photos to the Duplicate Folder.\nThis action can not be undone. Are you sure?",  this))
            //{
            //    return;
            //}
            //DisableAll();
            //double deleteEntryIndex = 0;
            //_cancellationMoveTokenSource = new CancellationTokenSource();
            //var cancellationToken = _cancellationMoveTokenSource.Token;
            //_moveTask = Task.Run(async ()=> {
            //    foreach (var item in dupCache.DataKeyPairDictionary)
            //    {
            //        if (cancellationToken.IsCancellationRequested)
            //        {
            //            _logger.SendLogAsync("Move: Cancellationis requested.");
            //            break;
            //        }
            //        string originalFile = item.Value[0];
            //        List<Task> taskList = new List<Task>();
            //        List<string> photoToBeRemovedFromIndex = new List<string>();
            //        int i = 0;
            //        this.Dispatcher.Invoke((Action)(() =>
            //        {
            //            DuplicateStatusLabel.Content = "Moving...";
            //        }));
            //        object lockObj = new object();
            //        foreach (var dupFile in item.Value)
            //        {
            //            if (cancellationToken.IsCancellationRequested)
            //            {
            //                _logger.SendLogAsync("Move: Cancellationis requested.");
            //                break;
            //            }

            //            // First one is original itself.
            //            if (i == 0)
            //            {
            //                i++;
            //                continue;
            //            }
            //            i++;
            //            photoToBeRemovedFromIndex.Add(dupFile);
            //            taskList.Add(Task.Run(async () => {
                            
            //                if (cancellationToken.IsCancellationRequested)
            //                {
            //                    _logger.SendLogAsync("Move: Cancellationis requested.");
            //                    return;
            //                }

            //                string srcPath = dupFile;
            //                string originalFileLocal = originalFile;
            //                string destinationFolder = "Drives\\" + Path.GetDirectoryName(srcPath).Replace(":", "") + "\\" + Path.GetFileName(srcPath);
            //                string destinationFile = Settings.DuplicateFolder + destinationFolder;
            //                _logger.SendDebugLogAsync($"Duplicate Move Log: Moving duplicate file: {srcPath} to: {destinationFile}");
            //                try
            //                {
            //                    await Utils.EnsureMoveAsync(srcPath, destinationFile, true);
            //                }
            //                catch (Exception ex)
            //                {
            //                    _logger.SendLogAsync($"Duplicate move failed: Couldn't move:{srcPath}. Error: {ex.Message}");
            //                }
            //                lock (lockObj)
            //                {
            //                    var line = $"{originalFileLocal} -> {Path.GetFileName(destinationFile)}";
            //                    File.AppendAllLines(Path.GetDirectoryName(destinationFile) + "\\OriginalLocations.txt", new string[] { line });
            //                }
            //            }));
            //        }
            //        await Task.WhenAll(taskList);
            //        if (photoToBeRemovedFromIndex.Any())
            //        {
            //            await _luceneService.DeleteDocumentFilePathAsync(photoToBeRemovedFromIndex.ToArray());
            //        }
            //        var percentage = (deleteEntryIndex / (double)dupCache.DataKeyPairDictionary.Count) * 100;
            //        deleteEntryIndex++;
            //        this.Dispatcher.Invoke((Action)(() =>
            //        {
            //            ProgressBar.Value = percentage;
            //            ProgressText.Text = $"{Math.Round(percentage, 2)} %";
            //        }));
            //    } //for
            //});
            //await _moveTask;
            //dupCache.Reset();
            //if (cancellationToken.IsCancellationRequested)
            //{
            //    return;
            //}
            //if (ProgressBar.Value > 0)
            //{
            //    ProgressBar.Value = 100;
            //}
            //this.DuplicateStatusLabel.Content = "Done...";

            //if (MessageBoxResult.Yes == Utils.DisplayMessageBoxAskYesNo("All duplicate photos habe been moved to Duplicate folder. You want to open Duplicate folder for review?",  this))
            //{
            //    Utils.ProcessStartWrapper(Settings.DuplicateFolder);
            //}
            //ButtonFindDuplicate.IsEnabled = true;
        }

        private void HowDoesitWork_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var url = @"https://getpitara.com/en/pitara-knowledge-base/how-pitara-isolates-duplicate-photos/";
            Utils.ProcessStartWrapper(url);
        }
    }
}
