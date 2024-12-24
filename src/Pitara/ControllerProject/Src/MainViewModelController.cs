using CommonProject;
using CommonProject.Src;
using CommonProject.Src.Cache;
using CommonProject.Src.Queues;
using CommonProject.Src.Views;
using DynamicData;
using Lucene.Net.Documents;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ViewModelProject.Src;
using static ViewModelProject.Src.MainWIndowViewModel;

namespace ControllerProject.Src
{
    public class MainViewModelController
    {
        private MainWIndowViewModel _viewModel;
        private ILuceneService _luceneService;
        private UserSettings _userSettings;
        private OperatingSettings _operatingSettings;
        private static ILogger _logger;
        private AppSettings _appSettings;
        private License _license;
        private int _searchCount = 0;
        private int _firstPageSize = 200;// Photos.

        private PhotoInputQueue<string> _photoQueue;
        private UserPhotoInputQueue<string> _userPhotoInputQueue;
        private LucenDocumentQueue<LuceneDocumentQueueMessage> _lucenDocumentQueue;
        private MetaQueue<MetaQueueMessage> _metaQueue;

        private FileWatcher _fileWatcher;
        private IndexWatcher _indexWatcher;
        private SuggestionProcessor _searchSuggestions;

        private RecommendationView _recommendationsView;
        private LocationView _locationView;
        private MyTagView _myTagView;
        private FolderView _folderView;
        private HeightView _HeightView;
        private CameraView _cameraModelMakeView;
        private MyTagView _miscView;
        private FavoriteView _favoriteView;
        private TimeView _timeView;
        private RandomeDeckView _randomDeckView;
        // private FestivalsView _festivalsView;
        private Dictionary<string, FestivalsView> _festivalViewCollection = new Dictionary<string, FestivalsView>();

        // Timers
        private static System.Timers.Timer _oneTimeRandomSurpriseTimer;
        private static System.Timers.Timer _updateLinksTimer;
        private static System.Timers.Timer _statusBarUpdateTimer;
        private static System.Timers.Timer _promptAboutRegistrationTimer;
        private static System.Timers.Timer _diagnosisdumpTimer;
        private static System.Timers.Timer _updateKeywordCacheFromIndexTimer;
        private static System.Timers.Timer _favoriteViewUpdateTimer;
        private static System.Timers.Timer _customTagViewUpdateTimer;

        // private static readonly SemaphoreSlim _readMetaQueueAsyncLock = new SemaphoreSlim(1, 1);
        // private static readonly SemaphoreSlim _readDocumentQueueAsyncLock = new SemaphoreSlim(1, 1);
        private static ManualResetEvent _searchisGoingon = new ManualResetEvent(true);


        // private Task _renderTask = null;
        // private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        StopWatchInternal _swIndexingClock = null;
        private void StartTheInternalMainEngine()
        {
            _photoQueue = new PhotoInputQueue<string>(1000, 8, _luceneService, _userSettings, _operatingSettings, _appSettings, _searchisGoingon, _logger); // Converts to document.
            _photoQueue.Start();

            _userPhotoInputQueue = new UserPhotoInputQueue<string>(200, 5, _operatingSettings, _appSettings, _searchisGoingon, _logger); // Converts to document.
            _userPhotoInputQueue.Start();

            _lucenDocumentQueue = new LucenDocumentQueue<LuceneDocumentQueueMessage>(500, 5, _luceneService, _searchisGoingon, _logger);
            _lucenDocumentQueue.Start();

            _metaQueue = new MetaQueue<MetaQueueMessage>(500, 5, _luceneService, _appSettings, _searchisGoingon, _logger);
            _metaQueue.Start();

            _fileWatcher = new FileWatcher(2 * 1000, _logger, _userSettings, _appSettings, _searchisGoingon, _luceneService);
            _fileWatcher.Watch();
            _fileWatcher._totalPhotoEvent += _fileWatcher__totalPhotoEvent;

            _indexWatcher = new IndexWatcher(2 * 1000, _logger, _userSettings, _searchisGoingon, _luceneService, _folderView);
            _indexWatcher.Watch();

            _customTagViewUpdateTimer = new System.Timers.Timer(500);
            _customTagViewUpdateTimer.Elapsed += _customTagViewUpdateTimer_Elapsed;
            _customTagViewUpdateTimer.Start();

            _favoriteViewUpdateTimer = new System.Timers.Timer(500);
            _favoriteViewUpdateTimer.Elapsed += FavoriteViewUpdateTimer_Elapsed;
            _favoriteViewUpdateTimer.Start();

            _updateLinksTimer = new System.Timers.Timer(10 * 1000); // Wants it low freq. User experience to switch tabs will be smooth, data sync is ok to be slow.
            _updateLinksTimer.Elapsed += UpdateLinksTimer_Elapsed;
            _updateLinksTimer.Start();

            _statusBarUpdateTimer = new System.Timers.Timer(_etaSampingTime);
            _statusBarUpdateTimer.Elapsed += StatusBarUpdateTimer_Elapsed;
            _statusBarUpdateTimer.Start();

            //_promptAboutRegistrationTimer = new System.Timers.Timer(25*1000);
            //_promptAboutRegistrationTimer.Elapsed += _promptAboutRegistrationTimer_Elapsed;
            //_promptAboutRegistrationTimer.Start();

            _oneTimeRandomSurpriseTimer = new System.Timers.Timer(100);
            _oneTimeRandomSurpriseTimer.Elapsed += _oneTimeRandomSurpriseTimer_Elapsed;
            _oneTimeRandomSurpriseTimer.Start();

            _diagnosisdumpTimer = new System.Timers.Timer(1000 * 60 * 30); // 5 minute.
            _diagnosisdumpTimer.Elapsed += _diagnosisdumpTimer_Elapsed;
            _diagnosisdumpTimer.Start();

            _updateKeywordCacheFromIndexTimer = new System.Timers.Timer(1000 * 5);
            _updateKeywordCacheFromIndexTimer.Elapsed += _updateKeywordCacheFromIndexTimer_Elapsed;

            // Bad perf. Not needed, if you create TagView by quering Keywords:cute not Tag:cute.
            // _updateKeywordCacheFromIndexTimer.Start();

            _swIndexingClock = new StopWatchInternal("Indexing clock", _logger, false);

            // Launch and forget the beacon home
            Task.Run(async () =>
            {
                if(_operatingSettings.AccountedFor)
                {
                    return;
                }
                try
                {
                    var formId = "1FAIpQLSeiXJMdJ-nqjHqS1TMT-TZg-uwVZkftbxIHcJwfMRaWdCuzYQ";
                    var fields = new Dictionary<string, string>
                    {
                        { "entry.989047206", _operatingSettings.ClientId },
                    };
                    //var formId = "1FAIpQLSeiXJMdJ-nqjHqS1TMT-TZg-uwVZkftbxIHcJwfMRaWdCuzYQ";
                    var formUrl = $"https://docs.google.com/forms/d/e/{formId}/formResponse";
                    var form = new SubmitGoogleForm(formUrl);
                    form.SetFieldValues(fields);
                    var response = await form.SubmitAsync();
                    _operatingSettings.AccountedFor = true;
                    await _operatingSettings.SaveAsync();
                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"Beacon failed: {ex.Message}");
                }
            });
        }
        private void _fileWatcher__totalPhotoEvent(object sender, PitaraEventArg e)
        {
            string total = e._object.ToString();
            _viewModel.SetTotalPhotoMessage(string.Format($"Total: {total}"));
            int parsed = 0;
            if (int.TryParse(total, out parsed))
            {
                _viewModel.TotalPhotosCount = parsed;
            }
            else
            {
                _viewModel.TotalPhotosCount = parsed;
            }
        }

        //private async void _etaTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    if (_viewModel.TotalPhotosCount > 0)
        //    {
        //        int currentIndexCount = 0;

        //        currentIndexCount = await Task.Run(() => _luceneService.GetIndexedPhotoCount());

        //        int sampleCountsForETA = currentIndexCount - _prevousIndexCount;
        //        if (sampleCountsForETA > 0 && _prevousIndexCount > 0)
        //        {
        //            double elapsed = _etaSampingTime;// _stopWatch.Elapsed.TotalMilliseconds;
        //            double timeNeededToprocessOne = elapsed / sampleCountsForETA;
        //            double eta = timeNeededToprocessOne * (_viewModel.TotalPhotosCount - currentIndexCount);
        //            TimeSpan t = TimeSpan.FromMilliseconds(eta);
        //            if (eta > 1000)
        //            {
        //                var etaString = string.Format("ETA - {0:D2}:{1:D2}:{2:D2}",
        //                                        t.Hours,
        //                                        t.Minutes,
        //                                        t.Seconds);
        //                _viewModel.SetStatusBarMessage(etaString);
        //            }
        //        }
        //        else
        //        {
        //            AnimateSyncing();
        //        }
        //        _prevousIndexCount = currentIndexCount;
        //    }
        //}
        //private void AnimateSyncing()
        //{
        //    Random rand = new Random();
        //    int random = rand.Next(1, 200);
        //    switch (random % 3)
        //    {
        //        case 0:
        //            {
        //                _viewModel.SetStatusBarMessage(".");
        //                _viewModel.SetStatusBarMessage("..");
        //                break;
        //            }
        //        case 1:
        //            {
        //                _viewModel.SetStatusBarMessage("..");
        //                _viewModel.SetStatusBarMessage("...");
        //                break;
        //            }
        //        case 2:
        //            {
        //                _viewModel.SetStatusBarMessage("...");
        //                _viewModel.SetStatusBarMessage(".");
        //                break;
        //            }
        //        case 3:
        //            {
        //                _viewModel.SetStatusBarMessage(".");
        //                _viewModel.SetStatusBarMessage("..");
        //                break;
        //            }
        //    }
        //}


        public static async Task<MainViewModelController> CreateController(MainWIndowViewModel viewModel,
            ILogger logger,
            ILuceneService luceneService,
            UserSettings userSettings,
            AppSettings appSettings,
            OperatingSettings operatingSettings,
            License license)
        {
            MainViewModelController controller = null;
            await Task.Run(async () => {
                controller = new MainViewModelController(viewModel,
                logger,
                luceneService,
                userSettings,
                appSettings,
                operatingSettings,
                license);

                await controller.StartAsync();
            });
            return controller;

        }
        private MainViewModelController(MainWIndowViewModel viewModel,
            ILogger logger,
            ILuceneService luceneService,
            UserSettings userSettings,
            AppSettings appSettings,
            OperatingSettings operatingSettings,
            License license)
        {
            _viewModel = viewModel;
            _logger = logger;
            _luceneService = luceneService;
            _userSettings = userSettings;
            _appSettings = appSettings;
            _license = license;
            _operatingSettings = operatingSettings;


            _viewModel.LicenseDetails = license;

            _recommendationsView = new RecommendationView(new LocationCache(_appSettings.GpsDBFileName, _logger, _appSettings),
                _logger, _luceneService, _appSettings);

            _locationView = new LocationView(new LocationCache(_appSettings.GpsDBFileName, _logger, _appSettings),
                _logger, _luceneService, _appSettings);

            _myTagView = new MyTagView(new KeyValueCache(_appSettings.CustomKeywordsDBFileName, _logger, _appSettings),
                _logger, _luceneService, _appSettings);

            _folderView = new FolderView(new KeyValueCache(_appSettings.FileFolderKeywordsDBFileName, _logger, _appSettings),
                _logger, _luceneService, _appSettings);

            _HeightView = new HeightView(new KeyValueCache(_appSettings.HeightKeywordsDBFileName, _logger, _appSettings),
                _logger, _luceneService, _appSettings);

            _cameraModelMakeView = new CameraView(new KeyValueCache(_appSettings.CameraModelMakeKeywordsFileName, _logger, _appSettings),
                _logger, _luceneService, _appSettings);

            _miscView = new MyTagView(new KeyValueCache(_appSettings.MiscTagsFileName, _logger, _appSettings),
                _logger, _luceneService, _appSettings);

            _favoriteView = new FavoriteView(new FavoriteCache(_appSettings.FavoritesDBFileName, _logger, _appSettings),
                _logger, _luceneService, _appSettings);

            _searchSuggestions = new SuggestionProcessor(_logger, _luceneService);

            _timeView = new TimeView(_logger, _luceneService, _appSettings);

            _randomDeckView = new RandomeDeckView(_logger, _luceneService, _userSettings);

            GatherFestivalCountries();

            foreach (var item in _viewModel.CountryNameSource)
            {
                _festivalViewCollection.Add(item.Name,
                                new FestivalsView(new TimeCache(item.FilePath, _logger, _appSettings),
                                _logger, _luceneService, _appSettings));
            }
        }

        private async void FavoriteViewUpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                using(SingleThreaded singleThreaded = new SingleThreaded()) 
                {
                    if (!_luceneService.DoesIndexExists())
                    {
                        return;
                    }
                    var col = await UpdateIfNeeded(_favoriteView);
                    if (col != null)
                    {
                        _viewModel.FavoriteViewSource = col;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.SendLogWithException($"UpdateLinksTimer_Elapsed", ex);
            }
        }

        private async void _customTagViewUpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                using (SingleThreaded singleThreaded = new SingleThreaded()) 
                {
                    if(!singleThreaded.IsSafeToProceed()) 
                    {
                        return;
                    }
                    if (!_luceneService.DoesIndexExists())
                    {
                        return;
                    }
                    var col = await UpdateIfNeeded(_myTagView);
                    if (col != null)
                    {
                        _viewModel.CustomeKeyWordSource = col;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.SendLogWithException($"UpdateLinksTimer_Elapsed", ex);
            }
        }

        //private async void _photoQueueTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    Spinner spinner = new Spinner(_logger,
        //    KnobId.UPDATE_TOTAL_PHOTO_COUNT,
        //    async (CancellationToken cancellationToken) =>
        //    {
        //        await Task.Run(() =>
        //        {
        //            IEnumerable<string> message;
        //            while (_appSettings.PhotoFileQueue.TryDequeue(out message))
        //            {
        //                if (message.Count() == 0)
        //                {
        //                    continue;
        //                }
        //                // _totalPhotosCountedSofar += message.Count();
        //                _viewModel.SetTotalPhotoMessage($"Photos: {100}");
        //            } // while

        //        });
        //        return 0;
        //    });
        //    await spinner.SpinIfStopped();
        //}

        private async void _updateKeywordCacheFromIndexTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            using (SingleThreaded singleThreaded = new SingleThreaded())
            {
                if(!singleThreaded.IsSafeToProceed())
                {
                    return;
                }
                await UpdateKeywordCacheFromIndexNew();
            }
        }

        private void _diagnosisdumpTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _logger.DumpMemUsageIfHigherThenBefore();
        }

        private void WireupViewModelStreams()
        {
            _viewModel.AddDisposable(_viewModel.SearchCommand.CommandExecutedStream.Subscribe(_ => DoSearchCommand()));
            _viewModel.AddDisposable(_viewModel.LaunchQueryLinkCommand.CommandExecutedStream.Subscribe(x => DoLaunchQueryLinkCommand(x)));
            _viewModel.AddDisposable(_viewModel.ImFeelingLuckyCommand.CommandExecutedStream.Subscribe(x => DoImFeelingLuckyCommand(x)));
            
            _viewModel.AddDisposable(_viewModel.UpdateIndexCommand.CommandExecutedStream.Subscribe(x => DoUpdateIndexCommand(x)));
            _viewModel.AddDisposable(_viewModel.BulkIndexCleanup.CommandExecutedStream.Subscribe(x => DoBulkIndexCleanup(x)));
            _viewModel.AddDisposable(_viewModel.StopIndexing.CommandExecutedStream.Subscribe(x => DoStopIndexing(x)));
            
            _viewModel.AddDisposable(_viewModel.CopyToBucketFilesCommand.CommandExecutedStream.Subscribe(x => DoCopyToBucketFilesCommand(x)));
            _viewModel.AddDisposable(_viewModel.DeletePhotosCommand.CommandExecutedStream.Subscribe(x => DoDeletePhotosCommand(x)));
            _viewModel.AddDisposable(_viewModel.ChangeCountrySelectionCommand.CommandExecutedStream.Subscribe(x => DoCountrySelectionChangeCommand(x)));
        }

        private async Task DoImFeelingLuckyCommand(object param)
        {
            using (var singleThreaded = new SingleThreaded())
            {
                if(!singleThreaded.IsSafeToProceed())
                {
                    return;
                }
                _searchisGoingon.Reset();
                _viewModel.SearchTerm = string.Empty;
                var photos = await _randomDeckView.GetTopRecommendationsYearAsync();
                if (photos.Count() > 0)
                {
                    // More results shouldn't show at randome deck
                    _viewModel.MoreResults = new QueryInfo()
                    {
                        IsVisible = false
                    };
                    _viewModel.SetUserMessage(string.Format("Random mix"));
                    await System.Windows.Application.Current.Dispatcher.BeginInvoke(
                           DispatcherPriority.Background,
                           new Action(() =>
                           {
                               WaitCursor wc = new WaitCursor();
                               _viewModel.Photos.Clear();
                               foreach (var photo in photos)
                               {
                                   _viewModel.Photos.Add(photo);
                               }
                               wc.Stop();
                           }));
                    _searchisGoingon.Set();
                    return;
                }
                _searchisGoingon.Set();
            }
        }

        private void DoCountrySelectionChangeCommand(object param)
        {
            _viewModel.SelectedCountry = param as CountryDetails;
            var currentSelectedFestivals = _festivalViewCollection[_viewModel.SelectedCountry.Name];
            if(currentSelectedFestivals.Recommendation == null)
            {
                return;
            }
            _viewModel.FestivalViewSource = new ObservableCollection<YearlyRow>(currentSelectedFestivals.Recommendation);
            _viewModel.HeaderInfo = new ObservableCollection<HeaderInfo>(currentSelectedFestivals.Headers);
            
            // Save current selection.
            _userSettings.SelectedCountryFile = _viewModel.SelectedCountry.FilePath;
            _userSettings.SaveSettings();
            return;
        }

        private async void DoDeletePhotosCommand(object param)
        {
            List<string> files = param as List<string>;
            if (files == null)
            {
                return;
            }
            // Utils.EnsureFolderExist(_userSettings.DeletedFolder);
            await LucenDocumentQueue<LuceneDocumentQueueMessage>.EnQueueAsync(new LuceneDocumentQueueMessage()
            {
                Action = ActionType.DELETE,
                FilePathCollection = files.ToList()
            });
            List<Task> taskArray = new List<Task>();
            foreach (var file in files)
            {
                taskArray.Add(Utils.EnsureMoveToRecycleBin(file));
            }
            await Task.WhenAll(taskArray);
            int remainingCount = _viewModel.Photos.Count;
            _viewModel.SetUserMessage($"Displaying: {remainingCount} of {remainingCount} results..");
            await this._myTagView.GetCache().TouchAsync();
        }

        private void DoStopIndexing(object param)
        {
            _fileWatcher.Stop();
            return;
        }
        private async void DoBulkIndexCleanup(object param)
        {
            var listofFoldersToRemove = param as List<string>;
            var listofFiles = new List<string>();
            foreach(var folder in listofFoldersToRemove)
            {
                listofFiles = listofFiles.Concat(FileWatcher.DirectorySearch(folder, listofFiles).ToList()).ToList();
            }
            var messageId = Guid.NewGuid().ToString();
            await LucenDocumentQueue<LuceneDocumentQueueMessage>.EnQueueAsync(new LuceneDocumentQueueMessage()
            {
                Action = ActionType.DELETE,
                FilePathCollection = listofFiles,
                MessageId = messageId
            });
            await LucenDocumentQueue<LuceneDocumentQueueMessage>.WaitUntilProcessed(messageId);
            await RefreshAllViews();
        }

        private async Task RefreshAllViews()
        {
            // Refresh all views
            await _myTagView.GetCache().TouchAsync();
            await _locationView.GetCache().TouchAsync();
            await _folderView.GetCache().TouchAsync();
            await _HeightView.GetCache().TouchAsync();
            await _cameraModelMakeView.GetCache().TouchAsync();
            await _miscView.GetCache().TouchAsync();
            await _favoriteView.GetCache().TouchAsync();
            await _timeView.GetCache().TouchAsync();

            //await Task.Run(async ()=>{
            //    await UpdatePhotoView();
            //});
        }

        //private async Task UpdatePhotoView()
        //{
        //    await System.Windows.Application.Current.Dispatcher.BeginInvoke(
        //                                   DispatcherPriority.Background,
        //                                   new Action(async () =>
        //                                   {
        //                                       if (string.IsNullOrEmpty(_viewModel.SearchTerm))
        //                                       {
        //                                           var photos = await _randomDeckView.GetTopRecommendationsYearAsync();
        //                                           if (photos.Count() > 0)
        //                                           {
        //                                               // _viewModel.SetUserMessage(string.Format("Pitara is ready for search. Meanwhile enjoy today's deck of random mix."));
        //                                               _viewModel.Photos = new ObservableCollection<Photo>(photos);
        //                                           }
        //                                       }
        //                                       else
        //                                       {
        //                                           _searchisGoingon.Reset();
        //                                           DoSearchCommand();
        //                                           _searchisGoingon.Set();
        //                                       }
        //                                   }));

        //}


        private async void DoCopyToBucketFilesCommand(object param)
        {
            List<string> files = param as List<string>;
            if (files == null)
            {
                return;
            }
            // Ensure dir exsist.
            Utils.EnsureFolderExist(_userSettings.BucketFolder);
            var validFiles = await Utils.EnsureIfFilesExist(files);

            //// Launch folder in file explorer.
            Utils.ProcessStartWrapperAsync(_userSettings.BucketFolder);

            List<Task> taskArray = new List<Task>();

            // int i = 0;
            _logger.SendLogAsync($"Coping:{validFiles.Count()} files, to bucket.");

            //var givenFiles = validFiles.Select(x => System.IO.Path.GetFileName(x)).Distinct();

            foreach (var item in validFiles)
            {
                // int localCount = i;
                string localItem = item;
                taskArray.Add(Task.Run(() =>
                {
                    // _logger.SendLogAsync($" Trying {localItem}");
                    Utils.EnsureCopyToBucket(localItem, _userSettings.BucketFolder + System.IO.Path.GetFileName(localItem));
                }));
                // i++;
            }
            await Task.WhenAll(taskArray.ToArray());
        }

        // Processing of docs should be inside Lucene 
        //private async void DocumentQueueReaderTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    await _readDocumentQueueAsyncLock.WaitAsync();
        //    try
        //    {
        //        LuceneDocumentQueueMessage message;
        //        if (!_appSettings.DocumentQueue.TryPeek(out message))
        //        {
        //            return;
        //        }

        //        while (_appSettings.DocumentQueue.TryPeek(out message))
        //        {
        //            switch (message.Action)
        //            {
        //                case ActionType.ADD:
        //                    {
        //                        await _luceneService.AddDocumentToIndexAsync(message.Documents);
        //                        LuceneDocumentQueueMessage msg;
        //                        if (!_appSettings.DocumentQueue.TryDequeue(out msg))
        //                        {
        //                            _logger.SendLogAsync($"Couldn't dequqe after ADD");
        //                        }
        //                        break;
        //                    }
        //                case ActionType.UPDATE:
        //                    {
        //                        await _luceneService.UpdateDocumentToIndexAsync(message.Documents);
        //                        LuceneDocumentQueueMessage msg;
        //                        if (!_appSettings.DocumentQueue.TryDequeue(out msg))
        //                        {
        //                            _logger.SendLogAsync($"Couldn't dequqe after UPDATE");
        //                        }
        //                        break;
        //                    }
        //                case ActionType.DELETE:
        //                    {
        //                        if(message.FilePathCollection.Count() > 0 )
        //                        {
        //                            _logger.SendDebugLogAsync($"Removing {message.FilePathCollection.Count()} files from index.");
        //                            await _luceneService.DeleteDocumentFilePathAsync(message.FilePathCollection.ToArray());
        //                        }
        //                        LuceneDocumentQueueMessage msg;
        //                        if (!_appSettings.DocumentQueue.TryDequeue(out msg))
        //                        {
        //                            _logger.SendLogAsync($"Couldn't dequqe after DELETE");
        //                        }
        //                        break;
        //                    }
        //                default:
        //                    {
        //                        _logger.SendLogAsync($"Undefined ActionType: {message.Action}");
        //                        break;
        //                    }
        //            }
        //        } // while
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.SendLogAsync($"Error processing Meta queue. Errr:{ex.Message}");
        //        throw;
        //    }
        //    finally
        //    {
        //        _readDocumentQueueAsyncLock.Release();
        //    }
        //}

        private async void _oneTimeRandomSurpriseTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_viewModel.Photos == null)
            {
                return;
            }
            if (_viewModel.Photos.Count == 0 &&
                _luceneService.GetIndexedPhotoCount() >= 50 &&
                string.IsNullOrEmpty(_viewModel.SearchTerm))
            {
                try
                {
                    _oneTimeRandomSurpriseTimer.Stop();
                    await DoImFeelingLuckyCommand(null);
                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"One time randome deck not possible. Error: {ex.Message}");
                }
                finally
                {
                    _oneTimeRandomSurpriseTimer.Stop();
                }
            }
        }

        private void _promptAboutRegistrationTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _promptAboutRegistrationTimer.Stop();
            _license.PromptForRegistrationIfNecessary();
        }

        private void GatherFestivalCountries()
        {
            _viewModel.CountryNameSource = new ObservableCollection<CountryDetails>();
            string[] festivalFiles = System.IO.Directory.GetFiles(_userSettings.FestivalFolder, "*.fes", SearchOption.TopDirectoryOnly);

            List<CountryDetails> countries = new List<CountryDetails>();
            foreach (var file in festivalFiles)
            {
                countries.Add(new CountryDetails()
                {
                    FilePath = file,
                    Name = Path.GetFileNameWithoutExtension(file)
                });
                //_viewModel.CountryNameSource.AddCommon(new CountryDetails()
                //{
                //    FilePath = file,
                //    Name = Path.GetFileNameWithoutExtension(file)
                //});
            }
            _viewModel.CountryNameSource = new ObservableCollection<CountryDetails>(countries);
            CountryDetails selectd = new CountryDetails()
            {
                FilePath = _userSettings.SelectedCountryFile,
                Name = Path.GetFileNameWithoutExtension(_userSettings.SelectedCountryFile)
            };
            _logger.SendDebugLogAsync($"selectd.FilePath: {selectd.FilePath}");

            int index = 0;
            bool found = false;
            foreach (var item in _viewModel.CountryNameSource)
            {
                if (item.FilePath.Equals(selectd.FilePath))
                {
                    found = true;
                    break;
                }
                index++;
            }

            if (_viewModel.CountryNameSource.Count > 0 && found)
            {
                _viewModel.SelectedCountry = _viewModel.CountryNameSource[index];
            }
        }

        //private async void MetaQueueReaderTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    await _readMetaQueueAsyncLock.WaitAsync();

        //    try
        //    {
        //        MetaQueueMessage message;
        //        if (!_appSettings.MetaQueue.TryPeek(out message))
        //        {
        //            return;
        //        }

        //        LocationCache  gpsCache = new LocationCache(_appSettings.GpsDBFileName, _logger, _appSettings);
        //        await gpsCache.LoadAsync();

        //        TimeCache dbTime = new TimeCache(_appSettings.TimeDBFileName, _logger, _appSettings);
        //        await dbTime.LoadAsync();

        //        KeywordCache dbCust = new KeywordCache(
        //            _appSettings.CustomKeywordsDBFileName, _logger, _appSettings);
        //        await dbCust.LoadAsync();

        //        KeywordCache dbFileFolder = new KeywordCache(
        //            _appSettings.FileFolderKeywordsDBFileName, _logger, _appSettings);
        //        await dbFileFolder.LoadAsync();

        //        KeywordCache cameraModelMake = new KeywordCache(
        //            _appSettings.CameraModelMakeKeywordsFileName, _logger, _appSettings);
        //        await cameraModelMake.LoadAsync();

        //        KeywordCache misc = new KeywordCache(
        //            _appSettings.MiscTagsFileName, _logger, _appSettings);
        //        await misc.LoadAsync();
        //        while (_appSettings.MetaQueue.TryDequeue(out message))
        //        {
        //            if (string.IsNullOrEmpty(message.Message.Trim()))
        //            {
        //                continue;
        //            }
        //            switch (message.Recepient)
        //            {
        //                case RecepientType.GPS_DATA:
        //                    {
        //                        string[] parts = message.Message.Split(new char[] { ':' });
        //                        gpsCache.AddCommon(parts[0].Trim(), parts[1].Trim());
        //                        break;
        //                    }
        //                case RecepientType.TIME_DATA:
        //                    {
        //                        string[] parts = message.Message.Split(new char[] { ':' });
        //                        dbTime.AddCommon(parts[0].Trim(), parts[1].Trim());
        //                        break;
        //                    }
        //                case RecepientType.CUSTOMEKEYWORD_DATA:
        //                    {
        //                        await dbCust.AddCommon(message.Message);
        //                        break; 
        //                    }
        //                case RecepientType.FILE_FOLDER_DATA:
        //                    {
        //                        await dbFileFolder.AddCommon(message.Message);
        //                        break;
        //                    }
        //                case RecepientType.CAMERA_MODEL_MAKE_TAGS:
        //                    {
        //                        await cameraModelMake.AddCommon(message.Message, false);
        //                        break;
        //                    }
        //                case RecepientType.MISC_DATA:
        //                    {
        //                        await misc.AddCommon(message.Message);
        //                        break;
        //                    }
        //                default:
        //                    {
        //                        _logger.SendLogAsync($"Undefined RecepientType: {message.Recepient}");
        //                        break;
        //                    }
        //            }
        //        } // while
        //        await dbTime.SaveAsync();
        //        await dbCust.SaveAsync();
        //        await gpsCache.SaveAsync();
        //        await dbFileFolder.SaveAsync();
        //        await cameraModelMake.SaveAsync();
        //        await misc.SaveAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.SendLogAsync($"Error processing Meta queue. Errr:{ex.Message}");
        //        throw;
        //    }
        //    finally
        //    {
        //        _readMetaQueueAsyncLock.Release();
        //    }

        //}
        private void AnimateWarmingUp(bool addMessage)
        {
            if (!addMessage)
            {
                _viewModel.SetStatusBarMessage("");
                return;
            }
            Random rand = new Random();
            int random = rand.Next(1, 200);
            switch (random % 3)
            {
                case 0:
                    {
                        _viewModel.SetStatusBarMessage(StringConstants.WarmingUp);
                        break;
                    }
                case 1:
                    {
                        _viewModel.SetStatusBarMessage(StringConstants.WarmingUpMore);
                        break;
                    }
                case 2:
                    {
                        _viewModel.SetStatusBarMessage(StringConstants.WarmingUpEvenMore);
                        break;
                    }
                case 3:
                    {
                        _viewModel.SetStatusBarMessage(StringConstants.WarmingUpEvenMore);
                        break;
                    }
            }
        }
        private int _timerCount = 0;
        private async void StatusBarUpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            using(SingleThreaded singleThreaded = new SingleThreaded())
            {
                if(!singleThreaded.IsSafeToProceed())
                {
                    return;
                }
                int currentIndexCount = await Task.Run(() => _luceneService.GetIndexedPhotoCount());
                _viewModel.AlreadyInIndexCount = $"Indexed: {currentIndexCount}";

                if (_viewModel.TotalPhotosCount > 0)
                {
                    double percent = (double)currentIndexCount / (double)_viewModel.TotalPhotosCount * 100;
                    percent = percent > 100 ? 100 : percent;
                    _viewModel.Progress = (int)percent;
                    _viewModel.ProgressText = $"Indexed: {((int)percent).ToString()}%";
                    if ((int)percent == 100)
                    {
                        _viewModel.Progress = 0;
                        _viewModel.SetStatusBarMessage(string.Empty);
                        _swIndexingClock.Stop();
                    }
                    _timerCount++;
                    int factor = 40; // 20 seconds 500*40 = 20000
                    if((_prevousIndexCount ==0) && ((int)percent!= 100))
                    {
                        _viewModel.SetStatusBarMessage($"ETA - Calculating..");
                    }
                    if (_timerCount == factor)
                    {
                        _timerCount = 0;
                        int sampleCountsForETA = currentIndexCount - _prevousIndexCount;
                        if (sampleCountsForETA > 0 && _prevousIndexCount > 0)
                        {
                            double elapsed = _etaSampingTime * factor;// _stopWatch.Elapsed.TotalMilliseconds;
                            double photosProcessedInMiliseconds = (sampleCountsForETA / elapsed);
                            var hourlyRate = photosProcessedInMiliseconds * 1000 * 60 * 60;
                            double timeNeededToprocessOne = elapsed / sampleCountsForETA;
                            double eta = timeNeededToprocessOne * (_viewModel.TotalPhotosCount - currentIndexCount);
                            TimeSpan t = TimeSpan.FromMilliseconds(eta);
                            // _logger.SendLogAsync($"elapsed:{elapsed}, timeNeededToprocessOne:{timeNeededToprocessOne}, sampleCountsForETA: {sampleCountsForETA}, timeNeededToprocessOne:{timeNeededToprocessOne},  _viewModel.TotalPhotosCount: {_viewModel.TotalPhotosCount},  currentIndexCount: {currentIndexCount},  eta:{eta}");
                            if (eta > 1000)
                            {
                                // var warningMessage = " - (Pitara will be slow until indexing is complete.)";
                                var etaString = string.Format("ETA - {0:D2}:{1:D2}:{2:D2}",
                                                        t.Hours,
                                                        t.Minutes,
                                                        t.Seconds);
                                _viewModel.SetStatusBarMessage(etaString);
                            }
                        }
                        _prevousIndexCount = currentIndexCount;
                    }
                }

                if (currentIndexCount > 0)
                {
                    _viewModel.ImFeelingLucky = new QueryInfo()
                    {
                        QueryString = "ImFeelinglucky:",
                        QueryDisplayName = $"I'm Feeling Lucky",
                        ResultCount = 1, // more than zero to enable link.
                        IsVisible = true
                    };
                    _viewModel.IsSearchBarReadOnly = false;
                    var currentUserMsg = _viewModel.GetUserMessage();
                    if (!string.IsNullOrEmpty(currentUserMsg) && currentUserMsg.Equals(StringConstants.NotReady))
                    {
                        _viewModel.SetUserMessage(StringConstants.Ready);
                    }
                }
                else
                {
                    _viewModel.IsSearchBarReadOnly = true;
                    _viewModel.SetUserMessage(StringConstants.NotReady);
                    // AnimateWarmingUp(true);
                }
            }
        }

        private async Task DoIndexResetCommandNew()
        {
            await Task.Run(async () => {
                await _recommendationsView.Reset();
                await _locationView.Reset();
                await _myTagView.Reset();
                await _folderView.Reset();
                await _HeightView.Reset();
                await _timeView.Reset();
                await _cameraModelMakeView.Reset();
                await _randomDeckView.Reset();
                await _miscView.Reset();
                // Festival files are pre shipped with setup and not required to delete.
                // await _festivalsView.Reset();


                // Never clear Favorite view.
                _logger.SendDebugLogAsync($"All viewws cleared...");

                // TODO : Clear UI collections. This will be needed if Index Reset is called from in between.

                int retryCount = 2;
                bool cleared = false;
                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        await _luceneService.ClearIndexAsync();
                        // int count = _luceneService.GetIndexedPhotoCount();
                        cleared = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.SendLogAsync($"Clear index failed. Retrying. Error:{ex.Message}");
                        await Task.Delay(250);
                        continue;
                    }
                }
                if (!cleared)
                {
                    var message = "Index couldn't be cleared!";
                    _logger.SendLogAsync(message);
                    throw new Exception(message);
                }
                _logger.SendLogAsync($"Index cleared...");
            });

        }
        // Command handlers 
        private async void _DEL_DoIndexResetCommand()
        {
            try
            {
                _logger.SendLogAsync($"Index Reset started...");
                // _documentQueueReaderTimer.StopBatch();

                await _luceneService.StopIndexing();

                //_metaQueueReaderTimer.StopBatch();
                _logger.SendLogAsync($"Indexing task stopped...");
                await System.Windows.Application.Current.Dispatcher.BeginInvoke(
                                                            DispatcherPriority.Background,
                                                            new Action(() =>
                                                            {
                                                                _viewModel.SetUserMessage(string.Format(string.Empty));
                                                                _viewModel.Photos.Clear();
                                                                _viewModel.SetTotalPhotoMessage($"Total Photoes: {0}");
                                                            }));
                _logger.SendLogAsync($"Photo cachs cleared...");
                await Task.Run(async () => {
                    //_recommendationsView.Reset();
                    //_locationView.Reset();
                    //_myTagView.Reset();
                    //_folderView.Reset();
                    //await _timeView.Reset();
                    //_surpriseView.Reset();
                    //_miscView.Reset();
                    _logger.SendLogAsync($"All viewws cleared...");
                    int retryCount = 2;
                    bool cleared = false;
                    for (int i = 0; i < retryCount; i++)
                    {
                        try
                        {
                            await _luceneService.ClearIndexAsync();
                            int count = _luceneService.GetIndexedPhotoCount();
                            cleared = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.SendLogAsync($"Clear index failed. Retrying. Error:{ex.Message}");
                            await Task.Delay(250);
                            continue;
                        }
                    }
                    if (!cleared)
                    {
                        var message = "Index couldn't be cleared!";
                        _logger.SendLogAsync(message);
                        throw new Exception(message);
                    }
                    _logger.SendLogAsync($"Index cleared...");
                });

                _logger.SendLogAsync($"Starting fresh indexing...");
                Utils.DisplayMessageBox($"Index reset successful!\nPressing OK will start fresh indexing.");
                //_metaQueueReaderTimer.Start();
                //_documentQueueReaderTimer.Start();
            }
            catch (Exception ex)
            {
                var message = $"Index can't be cleared automatically.\nPlease close Pitara, manually delete index folder then relaunch.\nCheck logs for more details.\nError:{ex.Message}";
                _logger.SendLogAsync(message);
                Utils.DisplayMessageBox(message);
            }
        }
        //private void LaunchUpdateLinkIfStopped()
        //{
        //    // If indexing was stopped. Set three attempts to update link.
        //    _runCountAfterSyncingIsDone = 3;
        //}
        private async void DoUpdateIndexCommand(object e)
        {
            List<string> param = e as List<string>;
            if (param != null)
            {
                await _luceneService.UpdateCustomTagsAsync(param.ToArray());
            }

            // Artificial save without any update. So that this will trigger an update link call for all views.
            await this._myTagView.GetCache().TouchAsync();
        }
        private bool DoesThisFieldValueExist(string field, string value)
        {
            QueryParser parser = new QueryParser(LuceneService.AppLuceneVersion1, field, LuceneService.Analyzer);
            parser.DefaultOperator = QueryParser.Operator.AND;

            Query query = null;
            query = parser.Parse(value);

            using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
            {
                using (IndexSearcher searcher = new IndexSearcher(fs, true))
                {
                    var sort = new Sort(new SortField("EPOCHTIME", SortField.LONG));
                    var filter = new QueryWrapperFilter(query);
                    var topDocs = searcher.Search(query, filter, 1, sort);
                    if (topDocs.ScoreDocs.Count() > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }
        private async Task UpdateKeywordCacheFromIndexNew()
        {
            {
                KeyValueCache dbCust = new KeyValueCache(
                                     _appSettings.CustomKeywordsDBFileName, _logger, _appSettings);
                await dbCust.LoadAsync();
                var copyOfCollection = new List<KeyValuePair<string, string>>(dbCust.DataKeyPairDictionary);
                bool removed = false;
                StringBuilder sb = new StringBuilder();
                foreach (var item in copyOfCollection)
                {
                    _searchisGoingon.WaitOne();
                    if (string.IsNullOrEmpty(item.Key.Trim()))
                    {
                        continue;
                    }
                    if (!DoesThisFieldValueExist("KeyWords", item.Key.ToLower().Trim()))
                    {
                        sb.Append(";");
                        sb.Append(item.Key);
                        sb.Append(" ");
                        dbCust.DataKeyPairDictionary.Remove(item.Key);
                        removed = true;
                    }
                }
                if (removed)
                {
                    _logger.SendLogAsync($"Removed some keywords from keyword cache: {sb.ToString()}");
                    await dbCust.SaveAsync();
                }
            }
        }

        private void DoLaunchQueryLinkCommand(object e)
        {
            if (e == null)
            {
                return;
            }
            LaunchQueryParam param = (LaunchQueryParam)e;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                                                        DispatcherPriority.Background,
                                                        new Action(() =>
                                                        {
                                                            param.QueryInfo.QueryString = param.QueryInfo.QueryString.ToLower();
                                                            _viewModel.SearchTerm = param.QueryInfo.QueryString;
                                                            _appSettings.BuildHistory = param.BuildHistory;
                                                            DoLuceneSearchCore(param.AppendResults);
                                                        }));
        }
        public static void SetSearchHistory(MainWIndowViewModel mainWIndowViewModel, string queryInfo)
        {
            string[] data = null;
            mainWIndowViewModel.KeywordHistory.Clear();// = new ObservableCollection<string>();
            queryInfo = queryInfo.Replace("(", " ( ");
            queryInfo = queryInfo.Replace(")", " ) ");

            data = queryInfo.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var itemsToSelect = mainWIndowViewModel.SearchSuggestions.Where(x => data.Contains(x.ToLower()));

            if (itemsToSelect.Count() == data.Count())
            {
                List<string> orderedSelect = new List<string>();
                foreach (var item in data)
                {
                    if (itemsToSelect.Any(x => x.ToLower() == item.ToLower()))
                    {
                        var picked = itemsToSelect.First(x => x.ToLower() == item.ToLower());
                        orderedSelect.Add(picked);
                    }
                }
                mainWIndowViewModel.KeywordHistory.AddRange(orderedSelect);
            }
            else
            {
                mainWIndowViewModel.KeywordHistory.AddRange(data);
            }
        }
        // Return null if no need to update.
        private async Task<ObservableCollection<QueryInfo>> UpdateIfNeeded(BaseView view)
        {
            if (view.GetCache().IsCacheDirty())
            {
                _searchisGoingon.WaitOne();
                return new ObservableCollection<QueryInfo>(await view.GetTopRecommendationsAsync());
            }
            else
            {
                return null;
            }
        }
        private async Task UpdateTabLinks()
        {
            using (StopWatchInternal sw = new StopWatchInternal("Updating tabs", _logger))
            {
                ObservableCollection<YearlyRow> yearly = null;
                ObservableCollection<YearlyRow> festivals = null;
                ObservableCollection<HeaderInfo> headerInfo = null;

                List<string> searchSuggestions = new List<string>();

                // CountryDetails currentSelectedCountry = _viewModel.SelectedCountry;


                await Task.Run(async () => {
                    var col = await UpdateIfNeeded(_recommendationsView);
                    if (col != null)
                    {
                        _viewModel.RecommendationsSource = col;
                    }
                });

                await (Task.Run(async () => {
                    var col = await UpdateIfNeeded(_locationView);
                    if (col != null)
                    {
                        _viewModel.LocationSource = col;
                    }
                }));
                await (Task.Run(async () => {
                    var col = await UpdateIfNeeded(_folderView);
                    if (col != null)
                    {
                        _viewModel.FolderFileNameSource = col;
                    }
                }));
                await (Task.Run(async () => {
                    var col = await UpdateIfNeeded(_HeightView);
                    if (col != null)
                    {
                        _viewModel.HeightSource = col;
                    }
                }));
                await (Task.Run(async () => {
                    var col = await UpdateIfNeeded(_cameraModelMakeView);
                    if (col != null)
                    {
                        // _viewModel.MiscKeywordSource = AlterLinksForCameraModel(col);
                        _viewModel.MiscKeywordSource = col;
                    }
                }));

                // Misc keywords like 9th 19th etc, they are not for view. But only for suggestion
                await (Task.Run(async () => {
                    var col = await UpdateIfNeeded(_miscView);
                    if (col != null)
                    {
                        //_miscView.MiscKeywordSource = col;
                    }
                }));
                await (Task.Run(async () =>
                {
                    {
                        if (_timeView.GetCache().IsCacheDirty())
                        {
                            _searchisGoingon.WaitOne();
                            yearly = new ObservableCollection<YearlyRow>(await _timeView.GetTopRecommendationsYearAsync());
                            _viewModel.YearlyViewSource = yearly;
                        }
                    }
                }));
                await (Task.Run(async () =>
                {
                    foreach (var festival in _festivalViewCollection.Values)
                    {
                        //if (festival.GetCache().IsCacheDirty())
                        {
                            _searchisGoingon.WaitOne();
                            await festival.GetTopRecommendationsYearAsync();
                            festival.GetHeaderInfo();
                            // _logger.SendLogAsync($"Loaded: Country festival: {festival.Headers.ToString()}");
                        }
                    }

                    // Set the selection
                    var currentSelectedFestivals= _festivalViewCollection[_viewModel.SelectedCountry.Name];
                    if(currentSelectedFestivals.Recommendation!= null)
                    {
                        _viewModel.FestivalViewSource = new ObservableCollection<YearlyRow>(currentSelectedFestivals.Recommendation);
                        _viewModel.HeaderInfo = new ObservableCollection<HeaderInfo>(currentSelectedFestivals.Headers);
                        // _logger.SendLogAsync($"Filled the festivale: {_viewModel.SelectedCountry.Name}");
                    }
                    else
                    {
                        // _logger.SendLogAsync($"currentSelectedFestivals.Recommendation is null");
                    }

                }));

                //    if (_festivalsView.GetCache().IsCacheDirty())
                //        {
                //            _searchisGoingon.WaitOne();
                //            festivals = new ObservableCollection<YearlyRow>(await _festivalsView.GetTopRecommendationsYearAsync());
                //            headerInfo = new ObservableCollection<HeaderInfo>(_festivalsView.GetHeaderInfo());

                //            // Case of first time launch.
                //            if (_viewModel.FestivalViewSource.Count == 0 )
                //            {
                //                _viewModel.FestivalViewSource = festivals;
                //                _viewModel.HeaderInfo = headerInfo;
                //            }
                //            // If user haven't changed from drop down then update. Else selection change of 
                //            // Drop down will update anyway.
                //            if (currentSelectedCountry == _viewModel.SelectedCountry)
                //            {
                //                _viewModel.FestivalViewSource = festivals;
                //                _viewModel.HeaderInfo = headerInfo;
                //            }
                //        }
                //}));
                await Task.Run(async () => {
                    {
                        if (searchSuggestions.Count() == 0 || this.IsAnyCacheDirtyForAutoUpdateLinks() == true)
                        {
                            _searchisGoingon.WaitOne();
                            _logger.SendDebugLogAsync($"Refreshing suggestions total:{_viewModel.SearchSuggestions.Count()}");

                            searchSuggestions = await _searchSuggestions.GetSearchSuggestionKeyWords(_myTagView.GetCache(),
                                _folderView.GetCache(),
                                _favoriteView.GetCache(),
                                _timeView.GetCache(),
                                (LocationCache)_locationView.GetCache(),
                                _miscView.GetCache(),
                                _cameraModelMakeView.GetCache(),
                                _HeightView.GetCache()
                                );
                            searchSuggestions.AddRange(new string[] { "(", ")", "AND", "OR" });
                            _viewModel.SearchSuggestions = new ObservableCollection<string>(searchSuggestions);
                            _logger.SendDebugLogAsync($"Refreshing suggestions total:{_viewModel.SearchSuggestions.Count()}");
                        }
                        else
                        {
                            _logger.SendDebugLogAsync($"Refreshing suggestion not needed..");
                        }
                    }
                });
            }
        }

        private ObservableCollection<QueryInfo> AlterLinksForCameraModel(ObservableCollection<QueryInfo> col)
        {
            var altered = col.Select(x => {
                x.QueryString = $"cameramodel: {x.QueryString}";
                return x;
            });
            return new ObservableCollection<QueryInfo>(altered);
        }

        private async Task SaveToLitUp()
        {
            List<Document> documentList = new List<Document>();

            if (_viewModel.YearlyViewSource != null)
            {
                documentList.Add(CreateDocumentFromViewForTime(_viewModel.YearlyViewSource, "YearlyViewSource"));
            }
            if (_viewModel.RecommendationsSource != null)
            {
                documentList.Add(CreateDocumentFromView(_viewModel.RecommendationsSource, "RecommendationsSource"));
            }
            if (_viewModel.LocationSource != null)
            {
                documentList.Add(CreateDocumentFromView(_viewModel.LocationSource, "LocationSource"));
            }
            if (_viewModel.CustomeKeyWordSource != null)
            {
                documentList.Add(CreateDocumentFromView(_viewModel.CustomeKeyWordSource, "CustomeKeyWordSource"));
            }
            if (_viewModel.FolderFileNameSource != null)
            {
                documentList.Add(CreateDocumentFromView(_viewModel.FolderFileNameSource, "FolderFileNameSource"));
            }
            if (_viewModel.MiscKeywordSource != null)
            {
                documentList.Add(CreateDocumentFromView(_viewModel.MiscKeywordSource, "MiscKeywordSource"));
            }
            if (_viewModel.FavoriteViewSource != null)
            {
                documentList.Add(CreateDocumentFromView(_viewModel.FavoriteViewSource, "FavoriteViewSource"));
            }
            if (_viewModel.HeightSource != null)
            {
                documentList.Add(CreateDocumentFromView(_viewModel.HeightSource, "HeightSource"));
            }
            if (_viewModel.SearchSuggestions != null)
            {
                documentList.Add(CreateDocumentFromSuggestion(_viewModel.SearchSuggestions, "SearchSuggestions"));
            }
            foreach( var festivalSrc in _festivalViewCollection ) 
            {
                if(festivalSrc.Value.Recommendation== null)
                {
                    continue;
                }
                documentList.Add(CreateDocumentFromViewForTime(
                    new ObservableCollection<YearlyRow>(festivalSrc.Value.Recommendation), festivalSrc.Key));

                documentList.Add(CreateDocumentFromFestivalHeaders(
                    new ObservableCollection<HeaderInfo>(festivalSrc.Value.Headers), festivalSrc.Key+ "Headers"));
                
            }

            await LucenDocumentQueue<LuceneDocumentQueueMessage>.EnQueueAsync(new LuceneDocumentQueueMessage()
            {
                Action = ActionType.UPDATE,
                Documents = documentList
            });
        }
        private async Task LitUp()
        {
            await Task.Run(async () => {
                _viewModel.YearlyViewSource = await CreateViewFromDocumentTime("YearlyViewSource");

                foreach (var festivalSrc in _festivalViewCollection)
                {
                    festivalSrc.Value.Recommendation =  CreateViewFromDocumentTime(festivalSrc.Key).Result?.ToList();
                    festivalSrc.Value.Headers = CreateViewFromDocumentFetivalHeader(festivalSrc.Key+"Headers").Result?.ToList();
                    if (festivalSrc.Key == _viewModel.SelectedCountry.Name && festivalSrc.Value.Recommendation!= null)
                    {
                        _viewModel.FestivalViewSource = new ObservableCollection<YearlyRow>(festivalSrc.Value.Recommendation);
                        _viewModel.HeaderInfo = new ObservableCollection<HeaderInfo>(festivalSrc.Value.Headers);
                    }
                }


                _viewModel.SearchSuggestions = new ObservableCollection<string>(await CreateSuggestionsFromDocument("SearchSuggestions"));

                _viewModel.RecommendationsSource = await CreateViewFromDocument("RecommendationsSource");
                _viewModel.LocationSource = await CreateViewFromDocument("LocationSource");
                _viewModel.CustomeKeyWordSource = await CreateViewFromDocument("CustomeKeyWordSource");
                _viewModel.FolderFileNameSource = await CreateViewFromDocument("FolderFileNameSource");
                _viewModel.HeightSource = await CreateViewFromDocument("HeightSource");
                _viewModel.MiscKeywordSource = await CreateViewFromDocument("MiscKeywordSource");
                _viewModel.FavoriteViewSource = await CreateViewFromDocument("FavoriteViewSource");

            });
        }

        private async Task<List<string>> CreateSuggestionsFromDocument(string key)
        {
            {
                var doc = await _luceneService.ReadKeyValue(key);
                if (doc != null)
                {
                    var content = doc.Get("ContentKey");
                    if (!string.IsNullOrEmpty(content))
                    {
                        var result = JsonConvert.DeserializeObject<List<string>>(content);
                        if (result != null)
                        {
                            // _logger.SendDebugLogAsync($"Key - {key} . Total read:{result.Count()}");
                        }
                        return result;
                    }
                }
                _logger.SendDebugLogAsync($"Key - {key} Non found in cache.");
                return new List<string>();
            }

        }

        private Document CreateDocumentFromFestivalHeaders(ObservableCollection<HeaderInfo> headers, string key)
        {
            var stringContent = JsonConvert.SerializeObject(headers);
            var doc = PhotoManipulation.CreateViewDocument(key, stringContent);
            return doc;
        }
        private Document CreateDocumentFromViewForTime(ObservableCollection<YearlyRow> yearlyViewSource, string key)
        {
            var stringContent = JsonConvert.SerializeObject(yearlyViewSource);
            var doc = PhotoManipulation.CreateViewDocument(key, stringContent);
            return doc;
        }


        private async Task<ObservableCollection<YearlyRow>> CreateViewFromDocumentTime(string key)
        {
            {
                var doc = await _luceneService.ReadKeyValue(key);
                if (doc != null)
                {
                    var content = doc.Get("ContentKey");
                    if (!string.IsNullOrEmpty(content))
                    {
                        var result = JsonConvert.DeserializeObject<ObservableCollection<YearlyRow>>(content);
                        if (result != null)
                        {
                            // _logger.SendDebugLogAsync($"Key - {key} . Total read:{result.Count()}");
                        }
                        return result;
                    }
                }
                _logger.SendDebugLogAsync($"Key - {key} Non found in cache.");
                return new ObservableCollection<YearlyRow>();
            }
        }
        private async Task<ObservableCollection<HeaderInfo>> CreateViewFromDocumentFetivalHeader(string key)
        {
            {
                var doc = await _luceneService.ReadKeyValue(key);
                if (doc != null)
                {
                    var content = doc.Get("ContentKey");
                    if (!string.IsNullOrEmpty(content))
                    {
                        var result = JsonConvert.DeserializeObject<ObservableCollection<HeaderInfo>>(content);
                        if (result != null)
                        {
                            // _logger.SendDebugLogAsync($"Key - {key} . Total read:{result.Count()}");
                        }
                        return result;
                    }
                }
                _logger.SendDebugLogAsync($"Key - {key} Non found in cache.");
                return new ObservableCollection<HeaderInfo>();
            }
        }
        private async Task<ObservableCollection<QueryInfo>> CreateViewFromDocument(string key)
        {
            {
                var doc = await _luceneService.ReadKeyValue(key);
                if (doc != null)
                {
                    var content = doc.Get("ContentKey");
                    if (!string.IsNullOrEmpty(content))
                    {
                        var result = JsonConvert.DeserializeObject<ObservableCollection<QueryInfo>>(content);
                        if (result != null)
                        {
                            // _logger.SendDebugLogAsync($"Key - {key} . Total read:{result.Count()}");
                        }
                        return result;
                    }
                }
                _logger.SendDebugLogAsync($"Key - {key} Non found in cache.");
                return null;
            }
        }

        private Document CreateDocumentFromSuggestion(ObservableCollection<string> suggestions, string key)
        {
            var stringContent = JsonConvert.SerializeObject(suggestions);
            var doc = PhotoManipulation.CreateViewDocument(key, stringContent);
            return doc;
        }
        private Document CreateDocumentFromView(ObservableCollection<QueryInfo> locationSource, string key)
        {
            var stringContent = JsonConvert.SerializeObject(locationSource);
            var doc = PhotoManipulation.CreateViewDocument(key, stringContent);
            return doc;
        }

        /*
                await _recommendationsView.Reset();
                await _locationView.Reset();
                await _myTagView.Reset();
                await _folderView.Reset();
                await _timeView.Reset();
                await _cameraModelMakeView.Reset();
                await _randomDeckView.Reset();
                await _miscView.Reset();
                await _festivalsView.Reset();

         */

        private bool IsAnyCacheDirtyForAutoUpdateLinks()
        {
            // Recommendation view have no cache, Favorite view & Custom TaG view refresh is checked seperately.
            if (
                 _folderView.GetCache().IsCacheDirty()
                || _locationView.GetCache().IsCacheDirty()
                || _miscView.GetCache().IsCacheDirty()
                || _cameraModelMakeView.GetCache().IsCacheDirty()
                || _timeView.GetCache().IsCacheDirty()
//                || _festivalsView.GetCache().IsCacheDirty()
                )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private async void UpdateLinksTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                using(SingleThreaded singleThreaded = new SingleThreaded())
                {
                    if(!singleThreaded.IsSafeToProceed()) 
                    {
                        return;
                    }
                    if (!_luceneService.DoesIndexExists())
                    {
                        return;
                    }

                    // Check if any of the underlying cache is updated. If so then updaet else not.
                    if (!IsAnyCacheDirtyForAutoUpdateLinks())
                    {
                        _logger.SendDebugLogAsync("Cache not dirty, not updating tabs");
                        return;
                    }
                    _logger.SendDebugLogAsync("Cache dirty, updating tabs");
                    await UpdateTabLinks();
                    await SaveToLitUp();
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.SendLogWithException($"UpdateLinksTimer_Elapsed", ex);
            }
        }

        //private void ViewModel_LaunchQuery(object sender, LaunchQueryParam e)
        //{
        //    System.Windows.Application.Current.Dispatcher.BeginInvoke(
        //                                                DispatcherPriority.Background,
        //                                                new Action(() =>
        //                                                {
        //                                                    _viewModel.SearchTerm = e.QueryInfo.QueryString;
        //                                                    _appSettings.BuildHistory = e.BuildHistory;
        //                                                    DoLuceneSearchCore();
        //                                                }));
        //}

        public async Task StartAsync()
        {
            WireupViewModelStreams();
            await UpdateTitleIfTestVersion();
            if (!_luceneService.DoesIndexExists())
            {
                StartTheInternalMainEngine();
                await InitIndexWithProperVersion();
            }
            else
            {
                if (string.Compare(_luceneService.GetIndexVersion(), CurrentVersionWrapper.GetSupportedIndexVersion()) != 0)
                {
                    Utils.DisplayMessageBox("Index version is updated. There are new features added to the index. Photos will be re-indexed.");
                    await DoIndexResetCommandNew();
                    StartTheInternalMainEngine();
                    await InitIndexWithProperVersion();
                }
                else
                {
                    //  If index exists, then first fill in all tabs, then resume indexing.
                    if (_luceneService.GetIndexedPhotoCount() > 0)
                    {
                        await LitUp();
                    }
                    StartTheInternalMainEngine();
                }
            }


            // Photo folders inaccessible case.
            var inaccessibleFolder = await _userSettings.IsPhotoFoldersAccessibleAsync();
            if (inaccessibleFolder.Count() > 0)
            {
                var folders = string.Join("\n", inaccessibleFolder.ToArray());
                _oneTimeRandomSurpriseTimer.Stop();

                var message = $"Folder inaccessible. If it's an external hard drive? Make sure USB cable connected correctly, then close and re-launch Pitara. Or remove the troublesome folder from the settings.\nTroublesom folder(s):\n{folders}";
                Utils.DisplayMessageBox(message);
                _viewModel.SetUserMessage("Error: One or more photo folders are not accessible. Check log file for details.");
                return;
            }

            // Special case for helper keywords that needs to be inside misc cache.
            // Initialize misc cache with helper keywords.
            //var keys = NLPSearchProcessor.HelperWordsMap.Keys.ToArray();
            //await MetaQueue<MetaQueueMessage>.EnQueueAsync(new MetaQueueMessage()
            //{
            //    Recepient = RecepientType.MISC_DATA,
            //    Message = string.Join(" ", keys)
            //});
            // Initialize Height cache with special feetplus keywords.
            //var heightKeys = NLPSearchProcessor.HelperWordsMap.Keys
            //    .Where(x => x.Contains("feets"))
            //    .ToArray();
            //await MetaQueue<MetaQueueMessage>.EnQueueAsync(new MetaQueueMessage()
            //{
            //    Recepient = RecepientType.HEIGHT_DATA,
            //    Message = string.Join(" ", heightKeys)
            //});




        }

        private async Task InitIndexWithProperVersion()
        {
            //Document doc = new Document();
            //doc.AddCommon(new Field("Version", CurrentVersionWrapper.GetSupportedIndexVersion().ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            //doc.AddCommon(new Field("PathKey", Utils.GetUniquePathKey(_uniqKeyForVersion), Field.Store.YES, Field.Index.NOT_ANALYZED));
            var doc = PhotoManipulation.CreateVersionDocument(LuceneService.UniqKeyForVersion, CurrentVersionWrapper.GetSupportedIndexVersion().ToString());
            var messageId = Guid.NewGuid().ToString();
            await LucenDocumentQueue<LuceneDocumentQueueMessage>.EnQueueAsync(new LuceneDocumentQueueMessage()
            {
                Documents = new List<Document> { doc },
                MessageId = messageId
            });
            await LucenDocumentQueue<LuceneDocumentQueueMessage>.WaitUntilProcessed(messageId);
        }

        private async Task UpdateTitleIfTestVersion()
        {
            RemoteVersion remoteTestVersion = new RemoteVersion();
            var remoteTestVer = await remoteTestVersion.CheckCurrentTestVersion(_logger);
            var currentVer = CurrentVersionWrapper.GetVersion();
            if (remoteTestVer == currentVer)
            {
                _viewModel.TestVersion = "*WARNING TEST VERSION*";
                _logger.SendLogAsync($"You are running test version.");
            }
            else
            {
                _viewModel.TestVersion = string.Empty;
            }
        }

        Stopwatch _swEntireSearch = null;
        private string _searchFinishedMessage;
        // private int _totalPhotosCount;
        private int _prevousIndexCount = 0;
        private double _etaSampingTime = 500;

        private void DisplayErrorMessageBox()
        {
            Utils.DisplayMessageBox("Some of the sample searches you can try:\n"
                + "\n1) 2002 July on weekends"
                + "\n2) From ten years Summer saturday night"
                + "\n3) From 6 months Wednesday afternoon"
                + "\n4) Wedding"
                + "\n5) IPhone from 3 years at 7:20pm"
                + "\n6) Summer saturday afternoon from 10 years"

                + "\n\n\nPlease refer help section for several more possibilities."
                );
        }

        // do search
        private async void DoLuceneSearchCore(bool appendResults = false)
        {
            if (string.IsNullOrEmpty(_viewModel.SearchTerm?.Trim()))
            {
                DisplayErrorMessageBox();
                return;
            }
            NLPSearchProcessor nlp = new NLPSearchProcessor(_viewModel.SearchTerm?.Trim(), _logger);
            var corrected = nlp.GetAutoCorrectedSearchTerm();
            if (corrected.ToString() != _viewModel.SearchTerm?.Trim())
            {
                _viewModel.SearchTermAutoCorrected = "Showing for: " + nlp.GetAutoCorrectedSearchTerm();
            }

            string searchTermTranslated = nlp.GetTranslatedSearchTerm(corrected);
            // _logger.SendLogAsync($"Translated query: {searchTermTranslated}");

            if (string.IsNullOrEmpty(searchTermTranslated))
            {
                DisplayErrorMessageBox();
                return;
            }
            //_appSettings.TranslateSearchTerm(_viewModel.SearchTerm);

            _swEntireSearch = new Stopwatch();
            _swEntireSearch.Start();

            _searchisGoingon.Reset();

            _viewModel.MoreResults = new QueryInfo()
            {
                IsVisible = false
            };

            _viewModel.SetUserMessage($"Fetching..");
            _viewModel.SelectedCount = string.Empty;

            _searchCount++;
            if (!_viewModel.LicenseDetails.LicensedVersion)
            {
                if (_searchCount >= 10)
                {
                    Utils.DisplayMessageBox("Pitara's trial period is over.\nAfter 10 searches, you will need to close and restart Pitara.\nPlease buy a license it help with ongoing effort of bug fixes & new features.");
                    return;
                }
            }
            if (_appSettings.BuildHistory)
            {
                if (_appSettings.BrowsingHistoryCursor + 1 == AppSettings.MaxHistoryBuffer)
                {
                    _logger.SendLogAsync($"Max history buffer of: {AppSettings.MaxHistoryBuffer} reached. Resetting browsing history.");
                    _appSettings.BrowsingHistoryCursor = -1;
                    _appSettings.BrowsingHistoryMaxForward = -1;
                    _viewModel.IsBackButtonEnabled = false;
                }
                if (_appSettings.BrowsingHistoryCursor > -1)
                {
                    var queryInfo = _appSettings.BrowsingHistory[_appSettings.BrowsingHistoryCursor];
                    if (!queryInfo.QueryString.Equals(corrected))
                    {
                        _appSettings.BrowsingHistoryCursor++;
                    }
                }
                else
                {
                    _appSettings.BrowsingHistoryCursor++;
                }
                _appSettings.BrowsingHistory[_appSettings.BrowsingHistoryCursor] = new QueryInfo()
                {
                    QueryString = corrected
                };
                _appSettings.BrowsingHistoryMaxForward = _appSettings.BrowsingHistoryCursor;

                _viewModel.IsFrontButtonEnabled = false;
                if (_appSettings.BrowsingHistoryCursor == 1)
                {
                    _viewModel.IsBackButtonEnabled = true;
                }
            }

                if (!appendResults)
                {
                    _viewModel.Photos.Clear();
                }

                _viewModel.PageNumber = 0;

                if (!_luceneService.DoesIndexExists())
                {
                    Utils.DisplayMessageBox($"This is your first time run. \nIndex not ready yet, please retry after one minute.");
                    return;
                }

                int startIndex = 0;
                int count = _firstPageSize;
                if (appendResults)
                {
                    startIndex = _firstPageSize;
                    count = _appSettings.MaxResultsToFetch;
                }
                var result = await this._luceneService.SearchAsync(searchTermTranslated, startIndex, count);
                IEnumerable<DisplayItem> searchResults = result.Item1;
                int maxAvailable = result.Item2;

                if (maxAvailable > _firstPageSize && !appendResults)
                {
                    _viewModel.MoreResults = new QueryInfo()
                    {
                        QueryString = "AppendResults:" + corrected,
                        QueryDisplayName = $"Show all {(maxAvailable)}",
                        ResultCount = maxAvailable,
                        IsVisible = true
                    };
                    _searchFinishedMessage = $"First {_firstPageSize} photos";
                }
                else
                {
                    _viewModel.MoreResults = new QueryInfo()
                    {
                        QueryString = "AppendResults:" + corrected,
                        QueryDisplayName = $"Show all{(0)}",
                        ResultCount = 0,
                        IsVisible = false
                    };
                    _searchFinishedMessage = $"{maxAvailable} photos";
                }

                if (searchResults == null)
                {
                    return;
                }
                if (searchResults.Any())
                {
                    try
                    {
                        int photIndex = 0;
                       //  System.Windows.Media.Color background = System.Windows.Media.Color.FromRgb(0, 255, 0);
                        foreach (var item in searchResults)
                        {
                            string path = item.FilePath;
                            string toolTips = PhotoManipulation.FormatToolTip(item.KeyWords, item.Location);
                            string thumbNail = item.ThumbNail;
                            _viewModel.Photos.Add(new Photo()
                            {
                                ThumbNail = thumbNail,
                                FullPath = path,
                                ToolTips = toolTips,
                                Heading = item.Heading.ToString(),
                                HeaderBackground = DisplayItem.Background
                            });
                            //if (photIndex == 0)
                            //{
                            //    _viewModel.SelectedItem = _viewModel.Photos[0];
                            //}
                            photIndex++;
                        } // for.
                        var task = Task.Run(async () =>
                        {
                            await System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(RenderingDone), System.Windows.Threading.DispatcherPriority.ContextIdle, null);
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.SendLogAsync($"Issue scheduling rendering task. Error:{ex.Message}");
                    }
                    finally
                    {
                        //Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                        // Thread.CurrentThread.Priority = ThreadPriority.Normal;
                    }
                }
                else
                {
                    var message = "No photos found with the query!";
                    _viewModel.SetUserMessage(message);
                    _logger.SendLogAsync($"Query: {_viewModel.SearchTerm} >> {message}");
                }

            }

            //New
            private void RenderingDone()
            {
                _swEntireSearch.Stop();
                TimeSpan ts = _swEntireSearch.Elapsed;
                string elapsedTime = String.Format("{0:00}.{1:00}",
                    ts.Seconds,
                    ts.Milliseconds / 10);
                var message = _searchFinishedMessage + $" ({elapsedTime} seconds)";
                _logger.SendLogAsync($"Query: {_viewModel.SearchTerm} >> {message}");
                _viewModel.SetUserMessage(message);
                _searchisGoingon.Set();
            }
            private void DoSearchCommand()
            {
                _appSettings.BuildHistory = true;
                DoLuceneSearchCore();
            }
        }
    }