using CommonProject.Src;
using CommonProject.Src.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Policy;
using System.Windows;
using System.Windows.Threading;

namespace ViewModelProject.Src
{
    public class MainWIndowViewModel : INPCBase
    {
        public ReactiveCommand<object, object> SearchCommand { get; private set; }
        public ReactiveCommand<object, object> IndexResetCommand { get; private set; }
        public ReactiveCommand<object, object> ImFeelingLuckyCommand { get; private set; }
        public ReactiveCommand<object, object> LaunchQueryLinkCommand { get; private set; }
        public ReactiveCommand<object, object> UpdateIndexCommand { get; private set; }
        public ReactiveCommand<object, object> BulkIndexCleanup { get; private set; }
        public ReactiveCommand<object, object> StopIndexing { get; private set; }
        
        public ReactiveCommand<object, object> CopyToBucketFilesCommand { get; private set; }
        public ReactiveCommand<object, object> DeletePhotosCommand { get; private set; }
        public ReactiveCommand<object, object> ChangeCountrySelectionCommand { get; private set; }


        // private ILuceneService _luceneService;
        // private UserSettings _userSettings;
        // private AppSettings _appSettings;
        private bool isViewKeyWordButtonEnabled;
        private bool isFavoriteButtonEnabled;
        private bool isSearchBarReadOnly;
        private static ILogger _logger;
        private string tagCloudContents;
        private string _searchTerm;
        private string _searchTermCorrected = string.Empty;
        private string statusText;
        private string totalPhotoText;
        private int progress;
        private string progressText;
        private string _selectedCount;
        private string _alreadyInIndexCount;
        private string scrollViewingHeight;
        private string indexingStatus;
        private string userStatusAtTop;
        private string metaTags;
        private string moreMeta;

        public int PageNumber = 0;
        // public ScrollBarVm scrollBarVm { get; set; }

        public bool RenderingInProgress { get; private set;}

        public MainWIndowViewModel(
            // MainWindow mainWindow,
                                    ILogger logger
                                    // ILuceneService luceneService,
                                    // UserSettings userSettings,
                                   // AppSettings appSettings
            )
        {
            // _userSettings = userSettings;
            _logger = logger;
            //  = luceneService;
            // _appSettings = appSettings;
            // scrollBarVm = new ScrollBarVm();;
            RenderingInProgress = false;
            // this.mainWindow = mainWindow;
            // RawRows = new ObservableCollection<RawRowDefinitionViewModel>();
            RenderingInProgress = false;
            InitCommands();
        }

        public class LaunchQueryParam : EventArgs
        {
            public QueryInfo QueryInfo { get; set; }
            public bool BuildHistory = false;
            public bool AppendResults = false;
            // public bool BreakWords = true; // Only false in case of Camera model where we don't breal "Apple IPhone 8
        }
        private void InitCommands()
        {
            SearchCommand = new ReactiveCommand<object, object>((x) => !IsBusy);
            IndexResetCommand = new ReactiveCommand<object, object>((x) => !IsBusy);
            LaunchQueryLinkCommand = new ReactiveCommand<object, object>((x) => !IsBusy);
            ImFeelingLuckyCommand = new ReactiveCommand<object, object>((x) => !IsBusy);
            UpdateIndexCommand = new ReactiveCommand<object, object>((x) => !IsBusy);
            BulkIndexCleanup = new ReactiveCommand<object, object>((x) => !IsBusy);
            StopIndexing = new ReactiveCommand<object, object>((x) => !IsBusy);
            CopyToBucketFilesCommand = new ReactiveCommand<object, object>((x) => !IsBusy);
            DeletePhotosCommand = new ReactiveCommand<object, object>((x) => !IsBusy);
            ChangeCountrySelectionCommand = new ReactiveCommand<object, object>((x) => !IsBusy);
        }

        public void SetStatusBarMessage(string message)
        {
            if (Application.Current == null)
            {
                return;
            }
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                          new Action(() => {
                                              this.StatusText = message;
                                          }));
        }
        public void SetTotalPhotoMessage(string message)
        {
            if (Application.Current == null)
            {
                return;
            }
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                          new Action(() => {
                                              this.TotalPhotoText = message;
                                          }));
        }

        public string GetUserMessage()
        {
            return this.UserStatusAtTop;
        }
        public void SetUserMessage(string message)
        {
            if(Application.Current?.Dispatcher == null)
            {
                return;
            }
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                          new Action(() => 
                                          {
                                            this.UserStatusAtTop= message;
                                          }));
        }
        
        public bool IsBusy { get; set; }
       //  public ObservableCollection<RawRowDefinitionViewModel> RawRows { get; private set; }

        private ObservableCollection<QueryInfo> customeKeyWordSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> CustomeKeyWordSource
        {
            get
            {
                return customeKeyWordSource;
            }
            set
            {
                if (this.customeKeyWordSource != value)
                {
                    this.customeKeyWordSource = value;
                    base.NotifyChanged("CustomeKeyWordSource");
                }
            }
        }

        private bool isFrontButtonEnabled = false;
        public bool IsFrontButtonEnabled
        {
            get
            {
                return isFrontButtonEnabled;
            }
            set
            {
                if (this.isFrontButtonEnabled != value)
                {
                    this.isFrontButtonEnabled = value;
                    base.NotifyChanged("IsFrontButtonEnabled");
                }
            }
        }
        //private Photo selectedItem = null;
        //public Photo SelectedItem
        //{
        //    get
        //    {
        //        return selectedItem;
        //    }
        //    set
        //    {
        //        if (this.selectedItem != value)
        //        {
        //            this.selectedItem = value;
        //            base.NotifyChanged("SelectedItem");
        //        }
        //    }
        //}
        private bool isBackButtonEnabled = false;
        public bool IsBackButtonEnabled
        {
            get
            {
                return isBackButtonEnabled;
            }
            set
            {
                if (this.isBackButtonEnabled != value)
                {
                    this.isBackButtonEnabled = value;
                    base.NotifyChanged("IsBackButtonEnabled");
                }
            }
        }
        
        private ObservableCollection<QueryInfo> miscKeywordSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> MiscKeywordSource
        {
            get
            {
                return miscKeywordSource;
            }
            set
            {
                if (this.miscKeywordSource != value)
                {
                    this.miscKeywordSource = value;
                    base.NotifyChanged("MiscKeywordSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> heightSource = new ObservableCollection<QueryInfo>()
        {
            new QueryInfo() {QueryDisplayName="5kfeet"},
        };
      
        public ObservableCollection<QueryInfo> HeightSource
        {
            get
            {
                return heightSource;
            }
            set
            {
                if (this.heightSource != value)
                {
                    this.heightSource = value;
                    base.NotifyChanged("HeightSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> folderFileNameSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> FolderFileNameSource
        {
            get
            {
                return folderFileNameSource;
            }
            set
            {
                if (this.folderFileNameSource != value)
                {
                    this.folderFileNameSource = value;
                    base.NotifyChanged("FolderFileNameSource");
                }
            }
        }
        
        private CountryDetails selectedCountry = new CountryDetails();
        public CountryDetails SelectedCountry
        {
            get
            {
                return selectedCountry;
            }
            set
            {
                if (this.selectedCountry != value)
                {
                    this.selectedCountry = value;
                    base.NotifyChanged("SelectedCountry");
                }
            }
        }

        private ObservableCollection<CountryDetails> countryNameSource = new ObservableCollection<CountryDetails>();
        public ObservableCollection<CountryDetails> CountryNameSource
        {
            get
            {
                return countryNameSource;
            }
            set
            {
                if (this.countryNameSource != value)
                {
                    this.countryNameSource = value;
                    base.NotifyChanged("CountryNameSource");
                }
            }
        }

        private ObservableCollection<string> searchSuggestions = new ObservableCollection<string>();
        public ObservableCollection<string> SearchSuggestions
        {
            get
            {
                return searchSuggestions;
            }
            set
            {
                if (this.searchSuggestions != value)
                {
                    this.searchSuggestions = value;
                    base.NotifyChanged("SearchSuggestions");
                }
            }
        }



        private ObservableCollection<QueryInfo> favoriteViewSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> FavoriteViewSource
        {
            get
            {
                return favoriteViewSource;
            }
            set
            {
                if (this.favoriteViewSource != value)
                {
                    this.favoriteViewSource = value;
                    base.NotifyChanged("FavoriteViewSource");
                }
            }
        }


        private ObservableCollection<YearlyRow> yearlyViewSource = new ObservableCollection<YearlyRow>();
        public ObservableCollection<YearlyRow> YearlyViewSource
        {
            get
            {
                return yearlyViewSource;
            }
            set
            {
                if (this.yearlyViewSource != value)
                {
                    this.yearlyViewSource = value;
                    base.NotifyChanged("YearlyViewSource");
                }
            }
        }
        private ObservableCollection<HeaderInfo> headerInfo = new ObservableCollection<HeaderInfo>();
        public ObservableCollection<HeaderInfo> HeaderInfo
        {
            get
            {
                return headerInfo;
            }
            set
            {
                if (this.headerInfo != value)
                {
                    this.headerInfo = value;
                    base.NotifyChanged("HeaderInfo");
                }
            }
        }
        private ObservableCollection<YearlyRow> festivalViewSource = new ObservableCollection<YearlyRow>();
        public ObservableCollection<YearlyRow> FestivalViewSource
        {
            get
            {
                return festivalViewSource;
            }
            set
            {
                if (this.festivalViewSource != value)
                {
                    this.festivalViewSource = value;
                    base.NotifyChanged("FestivalViewSource");
                }
            }
        }

        private ObservableCollection<QueryInfo> locationSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> LocationSource
        {
            get
            {
                return locationSource;
            }
            set
            {
                if (this.locationSource != value)
                {
                    this.locationSource = value;
                    base.NotifyChanged("LocationSource");
                }
            }
        }

        private ObservableCollection<QueryInfo> decembertimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> DecemberTimeSource
        {
            get
            {
                return decembertimeSource;
            }
            set
            {
                if (this.decembertimeSource != value)
                {
                    this.decembertimeSource = value;
                    base.NotifyChanged("DecemberTimeSource");
                }
            }
        }

        private QueryInfo imFeelingLucky = new QueryInfo();
        public QueryInfo ImFeelingLucky
        {
            get
            {
                return imFeelingLucky;
            }
            set
            {
                if (this.imFeelingLucky != value)
                {
                    this.imFeelingLucky = value;
                    base.NotifyChanged("ImFeelingLucky");
                }
            }
        }

        private QueryInfo moreResults = new QueryInfo();
        public QueryInfo MoreResults
        {
            get
            {
                return moreResults;
            }
            set
            {
                if (this.moreResults != value)
                {
                    this.moreResults = value;
                    base.NotifyChanged("MoreResults");
                }
            }
        }


        private QueryInfo aroundThisDayQuery = new QueryInfo();
        public QueryInfo AroundThisDayQuery
        {
            get
            {
                return aroundThisDayQuery;
            }
            set
            {
                if (this.aroundThisDayQuery != value)
                {
                    this.aroundThisDayQuery = value;
                    base.NotifyChanged("AroundThisDayQuery");
                }
            }
        }

        private QueryInfo aroundThisHourQuery = new QueryInfo();
        public QueryInfo AroundThisHourQuery
        {
            get
            {
                return aroundThisHourQuery;
            }
            set
            {
                if (this.aroundThisHourQuery != value)
                {
                    this.aroundThisHourQuery = value;
                    base.NotifyChanged("AroundThisHourQuery");
                }
            }
        }

        private ObservableCollection<QueryInfo> novembertimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> NovemberTimeSource
        {
            get
            {
                return novembertimeSource;
            }
            set
            {
                if (this.novembertimeSource != value)
                {
                    this.novembertimeSource = value;
                    base.NotifyChanged("NovemberTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> octobertimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> OctoberTimeSource
        {
            get
            {
                return octobertimeSource;
            }
            set
            {
                if (this.octobertimeSource != value)
                {
                    this.octobertimeSource = value;
                    base.NotifyChanged("OctoberTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> sepetembertimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> SeptemberTimeSource
        {
            get
            {
                return sepetembertimeSource;
            }
            set
            {
                if (this.sepetembertimeSource != value)
                {
                    this.sepetembertimeSource = value;
                    base.NotifyChanged("SeptemberTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> augustimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> AugustTimeSource
        {
            get
            {
                return augustimeSource;
            }
            set
            {
                if (this.augustimeSource != value)
                {
                    this.augustimeSource = value;
                    base.NotifyChanged("AugustTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> julytimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> JulyTimeSource
        {
            get
            {
                return julytimeSource;
            }
            set
            {
                if (this.julytimeSource != value)
                {
                    this.julytimeSource = value;
                    base.NotifyChanged("JulyTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> junetimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> JuneTimeSource
        {
            get
            {
                return junetimeSource;
            }
            set
            {
                if (this.junetimeSource != value)
                {
                    this.junetimeSource = value;
                    base.NotifyChanged("JuneTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> maytimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> MayTimeSource
        {
            get
            {
                return maytimeSource;
            }
            set
            {
                if (this.maytimeSource != value)
                {
                    this.maytimeSource = value;
                    base.NotifyChanged("MayTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> apriltimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> AprilTimeSource
        {
            get
            {
                return apriltimeSource;
            }
            set
            {
                if (this.apriltimeSource != value)
                {
                    this.apriltimeSource = value;
                    base.NotifyChanged("AprilTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> marchtimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> MarchTimeSource
        {
            get
            {
                return marchtimeSource;
            }
            set
            {
                if (this.marchtimeSource != value)
                {
                    this.marchtimeSource = value;
                    base.NotifyChanged("MarchTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> februarytimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> FebruaryTimeSource
        {
            get
            {
                return februarytimeSource;
            }
            set
            {
                if (this.februarytimeSource != value)
                {
                    this.februarytimeSource = value;
                    base.NotifyChanged("FebruaryTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> januarytimeSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> JanuaryTimeSource
        {
            get
            {
                return januarytimeSource;
            }
            set
            {
                if (this.januarytimeSource != value)
                {
                    this.januarytimeSource = value;
                    base.NotifyChanged("JanuaryTimeSource");
                }
            }
        }
        private ObservableCollection<QueryInfo> recommendationsSource = new ObservableCollection<QueryInfo>();
        public ObservableCollection<QueryInfo> RecommendationsSource
        {
            get
            {
                return recommendationsSource;
            }
            set
            {
                if (this.recommendationsSource != value)
                {
                    this.recommendationsSource = value;
                    base.NotifyChanged("RecommendationsSource");
                }
            }
        }

        private ObservableCollection<string> keywordHistory = new ObservableCollection<string>();

        public ObservableCollection<string> KeywordHistory
        {
            get
            {
                return keywordHistory;
            }
            set
            {
                if (this.keywordHistory != value)
                {
                    this.keywordHistory = value;
                    base.NotifyChanged("KeywordHistory");
                }
            }
        }


        private ObservableCollection<Photo> photos = new ObservableCollection<Photo>();

        public ObservableCollection<Photo> Photos
        {
            get {
                return photos; 
            }
            set
            {
                if (this.photos != value)
                {
                    this.photos = value;
                    base.NotifyChanged("Photos");
                }
            }
        }

        private string _tabHint = "You can also use these keywords in search";
        public string TabHint
        {
            get { return _tabHint; }
            set 
            {
                if(this._tabHint != value)
                {
                    _tabHint = value;
                    base.NotifyChanged("TabHint");
                }
            }
        }
        public string MoreMeta
        {
            get { return moreMeta; }
            set
            {
                if (this.moreMeta != value)
                {
                    this.moreMeta = value;
                    base.NotifyChanged("MoreMeta");
                }
            }
        }
        public string MetaTags
        {
            get { return metaTags; }
            set
            {
                if (this.metaTags != value)
                {
                    this.metaTags = value;
                    base.NotifyChanged("MetaTags");
                }
            }
        }
        public string UserStatusAtTop
        {
            get { return userStatusAtTop; }
            set
            {
                if (this.userStatusAtTop != value)
                {
                    this.userStatusAtTop = value;
                    base.NotifyChanged("UserStatusAtTop");
                }
            }
        }
        public string IndexingStatus
        {
            get { return indexingStatus; }
            set
            {
                if (this.indexingStatus != value)
                {
                    this.indexingStatus = value;
                    base.NotifyChanged("IndexingStatus");
                }
            }
        }
        public string SelectedCount
        {
            get { return _selectedCount; }
            set
            {
                if (this._selectedCount != value)
                {
                    this._selectedCount = value;
                    base.NotifyChanged("SelectedCount");
                }
            }
        }
        public string AlreadyInIndexCount
        {
            get { return _alreadyInIndexCount; }
            set
            {
                if (this._alreadyInIndexCount != value)
                {
                    this._alreadyInIndexCount = value;
                    base.NotifyChanged("AlreadyInIndexCount");
                }
            }
        }
        
        public string ProgressText
        {
            get { return progressText; }
            set
            {
                if (this.progressText != value)
                {
                    this.progressText = value;
                    base.NotifyChanged("ProgressText");
                }
            }
        }
        public int Progress
        {
            get { return progress; }
            set
            {
                if (this.progress != value)
                {
                    this.progress = value;
                    base.NotifyChanged("Progress");
                }
            }
        }
        public string ScrollViewingHeight
        {
            get { return scrollViewingHeight; }
            set
            {
                if (this.scrollViewingHeight != value)
                {
                    this.scrollViewingHeight = value;
                    base.NotifyChanged("ScrollViewingHeight");
                }
            }
        }
        public string TagCloudContents
        {
            get { return tagCloudContents; }
            set
            {
                if (this.tagCloudContents != value)
                {
                    this.tagCloudContents = value;
                    base.NotifyChanged("TagCloudContents");
                }
            }
        }
        public string StatusText
        {
            get { return statusText; }
            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;
                    base.NotifyChanged("StatusText");
                }
            }
        }

        // Don't use this property. Use Set function instead.
        public string TotalPhotoText
        {
            get { return totalPhotoText; }
            set
            {
                if (this.totalPhotoText != value)
                {
                    this.totalPhotoText = value;
                    base.NotifyChanged("TotalPhotoText");
                }
            }
        }
        private string _testVersion;
        public string TestVersion
        {
            get { return _testVersion; }
            set
            {
                if (this._testVersion != value)
                {
                    this._testVersion = value;
                    base.NotifyChanged("TestVersion");
                }
            }
        }

        public bool IsViewKeyWordButtonEnabled
        {
            get { return isViewKeyWordButtonEnabled; }
            set
            {
                if (this.isViewKeyWordButtonEnabled != value)
                {
                    this.isViewKeyWordButtonEnabled = value;
                    base.NotifyChanged("IsViewKeyWordButtonEnabled");
                }
            }
        }
        public bool IsSearchBarReadOnly
        {
            get
            {
                return isSearchBarReadOnly;
            }
            set
            {
                if (this.isSearchBarReadOnly != value)
                {
                    this.isSearchBarReadOnly = value;
                    base.NotifyChanged("IsSearchBarReadOnly");
                }
            }
        }
        public bool IsFavoriteButtonEnabled
        {
            get 
            { 
                return isFavoriteButtonEnabled; 
            }
            set
            {
                if (this.isFavoriteButtonEnabled != value)
                {
                    this.isFavoriteButtonEnabled = value;
                    base.NotifyChanged("IsFavoriteButtonEnabled");
                }
            }
        }

        public string SearchTerm
        {
            get { return _searchTerm; }
            set
            {
                if (_searchTerm != value)
                {
                    _searchTerm = value;
                    base.NotifyChanged("SearchTerm");
                }
            }
        }
        public string SearchTermAutoCorrected
        {
            get { return _searchTermCorrected; }
            set
            {
                if (_searchTermCorrected != value)
                {
                    _searchTermCorrected = value;
                    base.NotifyChanged("SearchTermAutoCorrected");
                }
            }
        }
        private License licenseDetails;

        public License LicenseDetails
        {
            get { return licenseDetails; }
            set
            {
                if (licenseDetails != value)
                {
                    licenseDetails = value;
                    base.NotifyChanged("LicenseDetails");
                }
            }
        }

        public int TotalPhotosCount { get;  set; }
    }
}