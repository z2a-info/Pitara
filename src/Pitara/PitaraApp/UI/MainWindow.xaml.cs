using CommonProject.Src;
using CommonProject.Src.Cache;
using ControllerProject.Src;
using Microsoft.Extensions.Configuration;
using PitaraLuceneSearch.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ViewModelProject.Src;
using WpfMessageBoxLibrary;
using static ViewModelProject.Src.MainWIndowViewModel;

namespace Pitara
{
    // Tool tips
    public class ImageConverterFromPath : IValueConverter
    {
        public object Convert(
            object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Photo.GetImageNewFromFile(value.ToString(), 400);
        }

        public object ConvertBack(
            object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
    public class ImageConverterFromString : IValueConverter
    {
        public object Convert(
            object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value!= null)
            {
                return Photo.FromString(value.ToString());
            }
            return null;
        }

        public object ConvertBack(
            object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public partial class MainWindow : Window
    {
        private ILuceneService _luceneService;
        private static ILogger _logger;
        static string TitleString = "Pitara - Version " + CurrentVersionWrapper.GetVersion();
        private UserSettings _userSettings;
        private AppSettings _appSettings;
        private MainWIndowViewModel _viewModel;
        private OperatingSettings _operatingSettings;
        private License _license;
        public MainWindow(IConfiguration configuration,
            ILogger logger,
            ILuceneService luceneService,
            UserSettings userSettings,
            AppSettings appSettings,
            OperatingSettings operatingSettings,
            License license)
        {
            _userSettings = userSettings;
            _luceneService = luceneService;
            _logger = logger;
            _appSettings = appSettings;
            _operatingSettings = operatingSettings;
            _logger.SendDebugLogAsync("Starting..");
            _license = license;
            InitializeComponent();
            SizeChanged += MainWindow_SizeChanged;
            Title = TitleString;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _userSettings.Dispose();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();

            if (args.Length > 1)
            {
                if (args[1].ToLower() == "version")
                {
                    try
                    {
                        System.IO.File.WriteAllText("PitaraVersion.txt", $"{CurrentVersionWrapper.GetVersion()} @<b>{DateTime.Now.ToString()}</b>");
                    }
                    catch (Exception eX)
                    {
                        _logger.SendLogWithException("Can't dump version", eX);
                    }
                    finally
                    {
                        System.Windows.Application.Current.Shutdown();
                    }
                }
                if (args[1].ToLower() == "hash")
                {
                    try
                    {
                        GenerateDownloadAndHashFile(args[2].ToLower());
                    }
                    catch (Exception eX)
                    {
                        _logger.SendLogWithException("Can't dump Hashes", eX);
                    }
                    finally
                    {
                        System.Windows.Application.Current.Shutdown();
                    }
                }
                if (args[1].ToLower() == "downloadpage")
                {
                    try
                    {
                        string versionString = $"{CurrentVersionWrapper.GetVersion()} @{DateTime.Now.ToString()}";

                        // GenerateDownloadAndHashFile(args[2].ToLower());
                        var tempalteFileName = args[2].ToLower();
                        var setupHash = GetHashFromFile($"sha256Setup.txt");
                        var zipHash = GetHashFromFile($"sha256Zip.txt");
                        var templateContent = File.ReadAllText(tempalteFileName);
                        templateContent = templateContent.Replace("SHA256 Hash for setup:{}", string.Format($"SHA256: {setupHash}"));
                        templateContent = templateContent.Replace("SHA256 Hash for portable:{}", string.Format($"SHA256: {zipHash}"));
                        templateContent = templateContent.Replace("LastUpdated version and date time{}", versionString);
                        var changelog = File.ReadAllText($"Changelog.md");
                        templateContent = templateContent.Replace("Changelog.md.contents:{}", changelog);

                        File.WriteAllText("default.md", templateContent);
                    }
                    catch (Exception eX)
                    {
                        _logger.SendLogWithException("Can't dump Download-Pitara.md", eX);
                    }
                    finally
                    {
                        System.Windows.Application.Current.Shutdown();
                    }
                }
            }
            // Initial UI state
            MetaEditBox.IsEnabled = false;
            MetaSaveButton.IsEnabled = false;
            MetaEraseButton.IsEnabled = false;
            // MultiSelectEditBox.Focus();

            // If launched first time, when no photo folders given, let's prompt user for some.
            if(!_userSettings.IsValid())
            {
                Utils.DisplayMessageBox("Let's first add your photo folders to Pitara.", null);

                SettingsWindow settingsWindow = new SettingsWindow(_userSettings.Clone());
                settingsWindow.SuppressReIndexWarning = true;
                var results = settingsWindow.ShowDialog();
                if (results.HasValue && results == true)
                {
                    _userSettings.CopyPropertiesFrom(settingsWindow.Settings);
                    _userSettings.SaveSettings();
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }

            _viewModel = new MainWIndowViewModel(_logger);//, _luceneService, _userSettings, _appSettings);
            this.DataContext = _viewModel;
            // SetSearchBoxAndFavoriteState();

            // No need to cache this. 
            var controller = await MainViewModelController.CreateController(_viewModel, _logger, _luceneService, _userSettings, _appSettings, _operatingSettings, _license);

        }

        private void GenerateDownloadAndHashFile(string templatePath)
        {
            var setupHash = GetHashFromFile($"sha256Setup.txt");
            var zipHash = GetHashFromFile($"sha256Zip.txt");
            var templateContent = File.ReadAllText(templatePath);
            templateContent = templateContent.Replace("SHA256 Hash for setup:{}", string.Format($"SHA256: {setupHash}") );
            templateContent = templateContent.Replace("SHA256 Hash for portable:{}", string.Format($"SHA256: {zipHash}"));
            File.WriteAllText("DownloadAndHash.txt", templateContent);
        }

        private string GetHashFromFile(string fileName)
        {
            var hashFileContentLines = File.ReadAllLines(fileName);
            return hashFileContentLines[1].Trim();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            int gapH = 200;
            double maxListViewWidth = (e.NewSize.Width - 280 > 0) ? e.NewSize.Width - 280 : e.NewSize.Width;
            double maxListViewHeight = (e.NewSize.Height - gapH > 0) ? e.NewSize.Height - gapH : e.NewSize.Height;
            Thumbnails.MaxWidth = maxListViewWidth;
            Thumbnails.MaxHeight = maxListViewHeight;
            Thumbnails.MinHeight = maxListViewHeight;

            var otherListHeight = e.NewSize.Height - 146;

            LocationView.MaxHeight = otherListHeight;
            LocationView.MinHeight = otherListHeight;

            YearlyListView.MaxHeight = otherListHeight;
            YearlyListView.MinHeight = otherListHeight;

            CustomKeywordView.MaxHeight = otherListHeight;
            CustomKeywordView.MinHeight = otherListHeight;

            MiscKeywordView.MaxHeight = otherListHeight;
            MiscKeywordView.MinHeight = otherListHeight;

            FolderView.MaxHeight = otherListHeight;
            FolderView.MinHeight = otherListHeight;

            HeightView.MaxHeight = otherListHeight;
            HeightView.MinHeight = otherListHeight;

            FavoriteView.MaxHeight = otherListHeight;
            FavoriteView.MinHeight = otherListHeight;

            FestivalsView.MaxHeight = otherListHeight-20;
            FestivalsView.MinHeight = otherListHeight-20;
        }

    
     
        private void Collect100_Click(object sender, RoutedEventArgs e)
        {
            CopyToBucket(100);
        }
        private void CopyToBucket(int count)
        {
            var topFiles = new List<string>();
            if (Thumbnails.Items.Count > 0)
            {
                var files = Thumbnails.Items.Cast<Photo>()
                    .ToList()
                    .Select(x=>x.FullPath);
                var topOnes = files.Take(count);
                _viewModel.CopyToBucketFilesCommand.Execute(topOnes.ToList());
            }
        }

        // Collect top 50
        private void Collect50_Click(object sender, RoutedEventArgs e)
        {
            CopyToBucket(50);
        }

        // Copy selected
        private List<string> GetSelectedFiles()
        {
            List<string> results = new List<string>();
            foreach (var item in Thumbnails.SelectedItems)
            {
                var photo = item as Photo;
                results.Add(photo.FullPath);
            }
            return results;
        }
        
        // Open file location.
        private async void FileLocation_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = GetSelectedFiles();
            if (selectedFiles.Count < 1)
            {
                return;
            }

            var firstFile = await Utils.EnsureIfFilesExist(new List<string> { selectedFiles[0]});
            if (!string.IsNullOrEmpty(firstFile.FirstOrDefault()))
            {
                Utils.ProcessStartWrapperAsync("explorer.exe", string.Format("/select,\"{0}\"", firstFile.FirstOrDefault()));
            }
        }

        // View open file.
        private async void View_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = GetSelectedFiles();
            if(selectedFiles.Count < 1)
            {
                return;
            }
            var firstFile = await Utils.EnsureIfFilesExist(new List<string> { selectedFiles[0] });
            if (!string.IsNullOrEmpty(firstFile.FirstOrDefault()))
            {
                Utils.ProcessStartWrapperAsync(firstFile.FirstOrDefault());
            }
        }

        // List<System.Windows.Controls.Image> imagesShown = new List<System.Windows.Controls.Image>();
        private void Thumbnails_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            View_Click(sender, e);
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_logger.GetLogFilePath()))
            {
                Utils.ProcessStartWrapperAsync(_logger.GetLogFilePath());
            }
            else
            {
                Utils.DisplayMessageBox($"There is no log file at: {_appSettings.AppDataFolder}");
            }
        }

        private void MenuItem_Click_3(object sender, RoutedEventArgs e)
        {
            Utils.DisplayMessageBox($"You are running Pitra version: {CurrentVersionWrapper.GetVersion()}. \nSupported index version: {CurrentVersionWrapper.GetSupportedIndexVersion()}\n\n\nYour unique client id:{_operatingSettings.ClientId}");
        }

        // Settings
        private void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow(_userSettings.Clone());
            settingsWindow.Owner = Window.GetWindow(this);
            var results = settingsWindow.ShowDialog();
            if (results.HasValue && results == true)
            {
                bool needToStopIndexing = false;
                var currentExcludedFolderList = _userSettings.ExcludeFolders;
                var updatedExcludedFolderList = settingsWindow.Settings.ExcludeFolders;
                var addedToExcludedFolderList = updatedExcludedFolderList.Except(currentExcludedFolderList).ToList();
                // var removedExcludedFolderList = currentExcludedFolderList.Except(updatedExcludedFolderList).ToList();
                if(!currentExcludedFolderList.Equals(updatedExcludedFolderList))
                {
                    needToStopIndexing = true;
                    // _viewModel.StopIndexing.Execute(null);
                }
                if(addedToExcludedFolderList.Count()>0)
                {
                    _viewModel.BulkIndexCleanup.Execute(addedToExcludedFolderList);
                }

                var currentFolderList = _userSettings.PhotoFolders;
                var updatedFolderList = settingsWindow.Settings.PhotoFolders;
                var removedFolderList = currentFolderList.Except(updatedFolderList).ToList();
                // var addedFolderList = updatedFolderList.Except(currentFolderList).ToList();
                if(!currentFolderList.Equals(updatedFolderList))
                {
                    needToStopIndexing = true;
                    // _viewModel.StopIndexing.Execute(null);
                }
                if(removedFolderList.Count()>0)
                {
                    _viewModel.BulkIndexCleanup.Execute(removedFolderList);
                }
                _userSettings.CopyPropertiesFrom(settingsWindow.Settings);
                _userSettings.SaveSettings();
                if(needToStopIndexing)
                {
                     _viewModel.StopIndexing.Execute(null);
                }
            }
        }

        // Help
        private void MenuItem_Help(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStartWrapperAsync("https://getpitara.com/en/pitara-user-guide");
        }

        private void ListView_Loaded(object sender, RoutedEventArgs e)
        {
            //if (VisualTreeHelper.GetChildrenCount(Thumbnails) != 0)
            //{
            //    Decorator border = VisualTreeHelper.GetChild(Thumbnails, 0) as Decorator;
            //    ScrollViewer sv = border.Child as ScrollViewer;
            //    sv.ScrollChanged += Sv_ScrollChanged;
            //}
        }
        private async void Thumbnails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(Thumbnails.Items.Count == 0)
            {
                // Index was cleared as result photos are deleted from colleciton
                MetaEditBox.IsEnabled = false;
                MetaSaveButton.IsEnabled = false;
                MetaEraseButton.IsEnabled = false;

                AroundThisHourTextBlock.Visibility = Visibility.Hidden;
                AroundThisDayTextBlock.Visibility = Visibility.Hidden;

                return;
            }

            _viewModel.SelectedCount = $"Selected: {Thumbnails.SelectedItems.Count}";
            if (Thumbnails.SelectedItems.Count > 0)
            {
                MetaEditBox.IsEnabled = true;
                //MetaSaveButton.IsEnabled = true;
                //MetaEraseButton.IsEnabled = true;
            }
            else
            {
                MetaEditBox.IsEnabled = false;
                MetaSaveButton.IsEnabled = false;
                MetaEraseButton.IsEnabled = false;
            }

            if (Thumbnails.SelectedItems.Count == 1)
            {
                AroundThisHourTextBlock.Visibility = Visibility.Visible;
                AroundThisDayTextBlock.Visibility = Visibility.Visible;

                var photo = Thumbnails.SelectedItems[0] as Photo;
                if (!File.Exists(photo.FullPath))
                {
                    return;
                }
                var answer = await _luceneService.GetThesePhotoesFromIndex(new string[] { photo.FullPath });
                if(!answer.ContainsKey(photo.FullPath))
                {
                    return;
                }
                _viewModel.MoreMeta = answer[photo.FullPath]?.Tags;
                _viewModel.MetaTags = answer[photo.FullPath]?.KeyWords;

                var aroundThisHourQueryString = "From " + answer[photo.FullPath]?.DateTimeKeywords;// + " at " + answer[photo.FullPath]?.Location;
                var aroundThisDayQueryString = "From " + answer[photo.FullPath]?.DateTimeKeywords;// + " at " + answer[photo.FullPath]?.Location;
                if (!string.IsNullOrEmpty(answer[photo.FullPath]?.Location))
                {
                    aroundThisHourQueryString = "From " + answer[photo.FullPath]?.DateTimeKeywords + " at " + answer[photo.FullPath]?.Location;
                    aroundThisDayQueryString = "From " + answer[photo.FullPath]?.DateTimeKeywords + " at " + answer[photo.FullPath]?.Location;
                }
                var excludeDaySegment = PhotoManipulation.GatherAllTimeWordsToExclude();
                excludeDaySegment = excludeDaySegment.OrderByDescending(c => c.Length).ToArray();
                
                foreach (var excludeDay in excludeDaySegment)
                {
                    aroundThisDayQueryString =
                                Regex.Replace(aroundThisDayQueryString, excludeDay, "", RegexOptions.IgnoreCase);
                    aroundThisHourQueryString =
                                Regex.Replace(aroundThisHourQueryString, excludeDay, "", RegexOptions.IgnoreCase);
                }
                string[] excludeHouyrs = new string[]
        {
                    "12am",
                    "1am",
                    "2am",
                    "3am",
                    "4am",
                    "5am",
                    "6am",
                    "7am",
                    "8am",
                    "9am",
                    "10am",
                    "11am",
                    "12pm",
                    "1pm",
                    "2pm",
                    "3pm",
                    "4pm",
                    "5pm",
                    "6pm",
                    "7pm",
                    "8pm",
                    "9pm",
                    "10pm",
                    "11pm"
        };
                excludeHouyrs = excludeHouyrs.OrderByDescending(c => c.Length).ToArray();

                foreach (var hour in excludeHouyrs)
                {
                    aroundThisDayQueryString =
                                Regex.Replace(aroundThisDayQueryString, hour, "", RegexOptions.IgnoreCase);
                }

                aroundThisDayQueryString = TagsHelper.SanitizeSearchTerm(aroundThisDayQueryString);
                aroundThisHourQueryString = TagsHelper.SanitizeSearchTerm(aroundThisHourQueryString);

                var infoAroundThisHour = await _luceneService.GetQueryInfoAsync("On this Hour", aroundThisHourQueryString.ToLower().Trim());
                var infoAroundThisDay = await _luceneService.GetQueryInfoAsync("On this Day", aroundThisDayQueryString.ToLower().Trim());

                if (infoAroundThisHour.ResultCount > 0)
                {
                    AroundThisHour.IsEnabled = true;
                }
                else
                {
                    AroundThisHour.IsEnabled = false;
                    infoAroundThisHour.QueryDisplayName = "On this Hour (unavailable)";
                }
                _viewModel.AroundThisHourQuery = infoAroundThisHour;
                
                if (infoAroundThisDay.ResultCount > 0)
                {
                    AroundThisDay.IsEnabled = true;
                }
                else
                {
                    AroundThisDay.IsEnabled = false;
                    infoAroundThisDay.QueryDisplayName = "On this Day (unavailable)";
                }
                _viewModel.AroundThisDayQuery = infoAroundThisDay;
            }
            else
            {
                _viewModel.MetaTags = "";
                _viewModel.MoreMeta = "";
                _viewModel.AroundThisDayQuery = new QueryInfo()
                {
                    QueryName = "On this Day",
                    QueryDisplayName = "On this Day",
                    QueryString = "",
                    ResultCount = 0
                };
                AroundThisDay.IsEnabled = false;

                _viewModel.AroundThisHourQuery = new QueryInfo()
                {
                    QueryName = "On this Hour",
                    QueryDisplayName = "On this Hour",
                    QueryString = "",
                    ResultCount = 0
                };
                AroundThisHour.IsEnabled = false;
            }
        }
        private async Task AlterCustomTAGs(bool append)
        {
            string userProvidedKeywords = _viewModel.MetaTags;

            MetaEditBox.IsEnabled = false;
            MetaSaveButton.IsEnabled = false;
            MetaEraseButton.IsEnabled = false;
            WaitCursor wc = new WaitCursor();
            List<string> files = new List<string>();
            Object lockObject = new object();
            try
            {
                List<object> copy = new List<object>();
                foreach (var item in Thumbnails.SelectedItems)
                {
                    copy.Add(item);
                }

                List<object> filesList = copy.Distinct().ToList();
                while (filesList.Any())
                {
                    List<Task> taskArray = new List<Task>();
                    // Set on batch of two.
                    var batchOfPhotos = filesList.Take(2).ToList();
                    filesList.RemoveAll(x => batchOfPhotos.Contains(x));

                    foreach (var item in batchOfPhotos)
                    {
                        taskArray.Add(Task.Run(async () =>
                        {
                            var photo = item as Photo;
                            
                            var localCopyUserProvidedKeyWords = TagsHelper.NormalizeTAGs(userProvidedKeywords, false, true, 2, true, true, -1).ToLower();
                            
                            bool ifProcessed = false;
                            
                            var meta = await PhotoManipulation.GetPhotoMetaAsync(photo.FullPath, false);
                            string dstTags = meta.CustomeKeyWords;
                            string srcTags = localCopyUserProvidedKeyWords;
                            
                            if (append)
                            {

                                bool atLeastOneMissing = TagsHelper.IsAnyMissingInTarget(dstTags, srcTags);
                                if(atLeastOneMissing)
                                {
                                    await PhotoManipulation.AppendExifWrapperAsync(photo.FullPath, string.Empty, localCopyUserProvidedKeyWords);
                                    ifProcessed = true;
                                }
                            }
                            else
                            {
                                bool atLeastOnePresent = TagsHelper.IsAnyPresentInTarget(dstTags, srcTags);
                                if (atLeastOnePresent)
                                {
                                    await PhotoManipulation.RemovExifWrapperAsync(photo.FullPath, string.Empty, localCopyUserProvidedKeyWords);
                                    ifProcessed = true;
                                }
                            }
                            if(ifProcessed)
                            {
                                lock (lockObject)
                                {
                                    files.Add(photo.FullPath);
                                }
                            }
                        }));
                    }
                    await Task.WhenAll(taskArray);
                }
                if(append)
                {
                    _logger.SendLogAsync($"To appended tags: {userProvidedKeywords} to: {files.Count()} files.");
                }
                else
                {
                    _logger.SendLogAsync($"To removed tags: {userProvidedKeywords} to: {files.Count()} files.");
                }
                if(files.Count()>0)
                {
                    _viewModel.UpdateIndexCommand.Execute(files);
                }
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"Error: Couldn't write custom keywords to photo error: {ex.Message}");
            }
            MetaEditBox.IsEnabled = true;
            MetaSaveButton.IsEnabled = true;
            MetaEraseButton.IsEnabled = true;
            wc.Stop();
        }

        // AddCommon custom tags
        private async void Button_AddCustomTag(object sender, RoutedEventArgs e)
        {
            string userProvidedKeywords = _viewModel.MetaTags;

            StringBuilder sb = new StringBuilder();
            string[] words = userProvidedKeywords.Split(new char[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            words = words.Distinct().ToArray();
            var message = string.Join(",", words);
            //foreach (var word in words)
            //{
            //    if (NLPSearchProcessor.HelperWordsMap.ContainsKey(word))
            //    {
            //        sb.Append(word);
            //        sb.Append(",");
            //    }
            //}
            //if (!string.IsNullOrEmpty(sb.ToString()))
            //{
            //    var tags = sb.ToString().TrimEnd(',');
            //    Utils.DisplayMessageBox($"Following TAGs are reserved, you can not use for your TAGs.\nTags: {tags}");
            //    MetaEditBox.IsEnabled = true;
            //    MetaSaveButton.IsEnabled = true;
            //    MetaEraseButton.IsEnabled = true;
            //    // wc.StopBatch();
            //    return;
            //}
            if (!_operatingSettings.DoNotShowAddTagsWarning)
            {
                var msgProperties = new WpfMessageBoxProperties()
                {
                    Button = MessageBoxButton.OKCancel,
                    ButtonOkText = "AddCommon Tags",
                    CheckBoxText = "Don't ask again.",
                    Image = MessageBoxImage.Exclamation,
                    Header = "AddCommon TAGs to photo headers?",
                    IsCheckBoxChecked = _operatingSettings.DoNotShowAddTagsWarning,
                    IsCheckBoxVisible = true,
                    IsTextBoxVisible = false,
                    Text = $"\nFollowing TAGs will be added to: {Thumbnails.SelectedItems.Count} selected photos headers?\n\nTAGs: {message}",
                    Title = "Pitara",
                };

                MessageBoxResult result = WpfMessageBox.Show(this, ref msgProperties);
                bool checkBoxChecked = msgProperties.IsCheckBoxChecked;
                if (checkBoxChecked != _operatingSettings.DoNotShowAddTagsWarning)
                {
                    _operatingSettings.DoNotShowAddTagsWarning = checkBoxChecked;
                    await _operatingSettings.SaveAsync();
                }
                if (result != MessageBoxResult.OK)
                {
                    return;
                }
            }
            await AlterCustomTAGs(true);
        }

        private void MetaEditBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                _viewModel.MetaTags = MetaEditBox.Text;
                Button_AddCustomTag(sender, e);
                e.Handled = true;
            }
        }

        // Launch query
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            _viewModel.LicenseDetails.LicensedVersion = true;

            if (_viewModel.LicenseDetails.LicensedVersion)
            {
                Dispatcher.BeginInvoke((Action)(() => MainTabControl.SelectedIndex = 0));
                string linkParam = ((System.Windows.Documents.Hyperlink)sender).NavigateUri.ToString();

                if (linkParam.ToLower().StartsWith("imfeelinglucky:"))
                {
                    _viewModel.ImFeelingLuckyCommand.Execute(linkParam);
                    return;
                }


                bool appendResults = false;
                if (linkParam.ToLower().StartsWith("appendresults:"))
                {
                    linkParam = linkParam.ToLower().Replace("appendresults:", "").Trim();
                    appendResults = true;
                }

                _viewModel.LaunchQueryLinkCommand.Execute(
                            new LaunchQueryParam()
                            {
                                QueryInfo = new QueryInfo()
                                {
                                    QueryName = "",
                                    QueryString = linkParam.ToLower()
                                },
                                BuildHistory = true,
                                AppendResults = appendResults,
                                // BreakWords = breakWords
                            });

            }
            else
            {
                Utils.DisplayMessageBox("Links don't function after trial period end.\n\nWe will appreciate if you purchase a license. It supports ongoing effort with bug fixes and improvements.");
            }
        }

        // Remove tags
        private async void MetaEraseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Thumbnails.SelectedItems.Count > 0)
            {
                if (MessageBoxResult.No ==
                    Utils.DisplayMessageBoxAskYesNo
                    ($"Removing following tags from: {Thumbnails.SelectedItems.Count} photo(s). Are you sure?\nTAGs: {_viewModel.MetaTags}"))
                {
                    return;
                }
            }
            await AlterCustomTAGs(false);
            _viewModel.MetaTags = string.Empty;
        }

        // Delete
        private void MenuItem_Click_Delete(object sender, RoutedEventArgs e)
        {
            if (Thumbnails.SelectedItems.Count > 0)
            {
                if (MessageBoxResult.No == 
                    Utils.DisplayMessageBoxAskYesNo
                    ($"Are you sure you want move {Thumbnails.SelectedItems.Count} photo(s) to Recycle bin?"))
                {
                    return;
                }
            }
            WaitCursor wc = new WaitCursor();
            var selectedFiles = GetSelectedFiles();
            _logger.SendLogAsync($"Total: {selectedFiles.Count} files selected for deletion.");

            List<object> items = new List<object>();
            foreach (var item in Thumbnails.SelectedItems)
            {
                items.Add(item);
            }
            foreach (var item in items)
            {
                _viewModel.Photos.Remove(item as Photo);
            }
            if (_viewModel.Photos.Count > 0)
            {
                // _viewModel.SelectedItem = _viewModel.Photos[_viewModel.Photos.Count - 1];
            }
            _viewModel.DeletePhotosCommand.Execute(selectedFiles);
            wc.Stop();
        }

        // Duplicate
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            //DuplicateRemovalWindow duplicateWindow = new DuplicateRemovalWindow(_userSettings.Clone(), _logger, _luceneService, _appSettings);
            //duplicateWindow.Owner = Window.GetWindow(this);
            //var results = duplicateWindow.ShowDialog();
            //// Doesn't matter if true or false.
            //if (results.HasValue && !_userSettings.Equals(duplicateWindow.Settings))
            //{
            //    _userSettings.CopyPropertiesFrom(duplicateWindow.Settings);
            //}
        }

        // Reset Index
        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            if (MessageBoxResult.Yes ==
                Utils.DisplayMessageBoxAskYesNo($"This will clear the entire index and rebuild a new index from the scratch. Are you sure?"))
            {
                _viewModel.IndexResetCommand.Execute(null);

            }
        }

        // Back
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (_appSettings.BrowsingHistoryCursor - 1 < 0)
            {
                Utils.DisplayMessageBox("No backword history.");
                return;
            }
            _viewModel.IsFrontButtonEnabled = true;
            _appSettings.BrowsingHistoryCursor--;
            var queryInfo = _appSettings.BrowsingHistory[_appSettings.BrowsingHistoryCursor];

            Dispatcher.BeginInvoke((Action)(() => MainTabControl.SelectedIndex = 0));
            _viewModel.LaunchQueryLinkCommand.Execute(
                new LaunchQueryParam()
                {
                    QueryInfo = new QueryInfo()
                    {
                        QueryName = "",
                        QueryString = queryInfo.QueryString.ToLower()
                    },
                    BuildHistory = false
                });
            if (_appSettings.BrowsingHistoryCursor - 1 < 0)
            {
                _viewModel.IsBackButtonEnabled = false;
            }
        }


        // Forward
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (_appSettings.BrowsingHistoryCursor + 1 > _appSettings.BrowsingHistoryMaxForward)
            {
                // ForwardButton.IsEnabled = false;
                Utils.DisplayMessageBox("No forward history.");
                return;
            }
            _viewModel.IsBackButtonEnabled = true;
            _appSettings.BrowsingHistoryCursor++;
            var queryInfo = _appSettings.BrowsingHistory[_appSettings.BrowsingHistoryCursor];
            Dispatcher.BeginInvoke((Action)(() => MainTabControl.SelectedIndex = 0));
            //_viewModel
            //    .LaunchQueryWrapped(new PitaraLuceneSearch.Infrastructure.QueryInfo() { QueryName = "", QueryString = queryInfo.QueryString.ToLower() }, false);
            _viewModel.LaunchQueryLinkCommand.Execute(
                        new LaunchQueryParam()
                        {
                            QueryInfo = new QueryInfo()
                            {
                                QueryName = "",
                                QueryString = queryInfo.QueryString.ToLower()
                            },
                            BuildHistory = false
                        });

            if (_appSettings.BrowsingHistoryCursor + 1 > _appSettings.BrowsingHistoryMaxForward)
            {
                _viewModel.IsFrontButtonEnabled = false;
            }
        }

        private async void AddToFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            FavoriteCache favCache = new FavoriteCache(_appSettings.FavoritesDBFileName,
                _logger, _appSettings);
            await favCache.LoadAsync();
            string queryDiscription = string.Empty;
            if (favCache.DataKeyPairDictionary.ContainsKey(_viewModel.SearchTerm.Trim()))
            {
                queryDiscription = favCache.DataKeyPairDictionary[_viewModel.SearchTerm.Trim()];
            }

            FavoriteQuery favoriteQuery = new FavoriteQuery()
            {
                QueryDescription = queryDiscription,
                Query = _viewModel.SearchTerm.Trim()
            };
            AddToFAvoriteWindow window = new AddToFAvoriteWindow(_logger, favoriteQuery);
            window.Owner = Window.GetWindow(this);
            var results = window.ShowDialog();
            if (results.HasValue && results == true)
            {
                favCache.AddFavorite(window.FavoriteString.Query,
                    window.FavoriteString.QueryDescription);
                await favCache.SaveAsync();
                // Just to trigger update if indexing was stopped.
                // TBD: Should be done in better way.
                _viewModel.UpdateIndexCommand.Execute(new string[] { });
            }
        }

       

     
        private void addItemNew(string text, Canvas border)
        {
            _viewModel.KeywordHistory.Add(text);
        }
     

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            //if (string.IsNullOrWhiteSpace(_viewModel.SearchTerm))
            //{
            //    return;
            //}
            DoSearchUI();
        }
        private void DoSearchUI()
        {
            _viewModel.SearchCommand.Execute(null);
            Thumbnails.Focus();
        }

        private void CountrySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WaitCursor wc = new WaitCursor();
            CountryDetails selectedCountry = CountrySelector.SelectedItem as CountryDetails;
            _viewModel.ChangeCountrySelectionCommand.Execute(selectedCountry);
            wc.Stop();
        }

        // Purchase
        private void MenuItem_Click_5(object sender, RoutedEventArgs e)
        {
            var url = "https://getpitara.com/en/purchase-license/";
            Utils.ProcessStartWrapperAsync(url);
        }

        // Register
        private async void MenuItem_Click_6(object sender, RoutedEventArgs e)
        {
            RegisterWindow registerWindow = new RegisterWindow(_userSettings, _logger, _appSettings, _viewModel.LicenseDetails);
            registerWindow.Owner = Window.GetWindow(this);
            var results = registerWindow.ShowDialog();
            if (results.HasValue && results == true)
            {
                if (await _viewModel.LicenseDetails.ActivateAsync())
                {
                    await _viewModel.LicenseDetails.WriteLicenseAsync();
                }
            }
        }

       

        private void SearchEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _viewModel.SearchTerm = SearchEditBox.Text;
                DoSearchUI();
            }
        }
 
        private void MetaEditBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox objTextBox = this.MetaEditBox;
            string theText = objTextBox.Text;
            if (!string.IsNullOrEmpty(theText))
            {
                this.MetaSaveButton.IsEnabled = true;
                this.MetaEraseButton.IsEnabled = true;
            }
            else
            {
                this.MetaSaveButton.IsEnabled = false;
                this.MetaEraseButton.IsEnabled = false;
            }

        }

        private void KeyWordAutoComplete_KeyUp(object sender, KeyEventArgs e)
        {

        }

        private void CopyCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedFiles = GetSelectedFiles();
            if (selectedFiles.Count < 1)
            {
                return;
            }
            _viewModel.CopyToBucketFilesCommand.Execute(selectedFiles);
        }

        private void DeleteConmmandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MenuItem_Click_Delete(null, null);
        }
        private void RemoveFromFavorite_Clicked(object sender, RoutedEventArgs e)
        {
            var queryInfo = (QueryInfo)((Button)sender).DataContext;
            MessageBox.Show(queryInfo.QueryName);

        }

        private void SearchEditBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SearchEditBox.Text) )
            {
                _viewModel.IsFavoriteButtonEnabled = true;
            }
            else
            {
                _viewModel.IsFavoriteButtonEnabled = false;
            }

        }
    }
}
