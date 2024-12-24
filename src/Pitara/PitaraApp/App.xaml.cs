using CommonProject.Src;
using CommonProject.Src.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace Pitara
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // private UserSettings _userSettings;
        private AppSettings _appSettings;
        public App()
        {
            SetupExceptionHandling();
        }
        public IServiceProvider ServiceProvider { get; private set; }

        // Initialize global one instance of logger.
        private static ILogger _logger;// = AsyncLog.GetGlobalLogger();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        private async Task ConfigureServices(IServiceCollection serviceCollection)
        {
            var appDataFolder = await Utils.DetermineAppDataFolder();
            Utils.EnsureFolderExist(appDataFolder);
            
            bool debugMode = false;
            if (File.Exists(appDataFolder + "debug.txt"))
            {
                debugMode = true;
            }
            else
            {
                debugMode = false;
            }

            _logger = CreateLogger(appDataFolder, debugMode);

            serviceCollection.AddSingleton<ILogger>(_logger);

            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
            .AddJsonFile("appsettings.json", false)
            .Build();
            serviceCollection.AddSingleton<IConfiguration>(configuration);

            serviceCollection.AddSingleton(typeof(MainWindow));
            
            _appSettings = new AppSettings(appDataFolder, _logger);
            serviceCollection.AddSingleton(_appSettings);

            Utils.Init(_logger, _appSettings);
            PhotoManipulation.Init(_logger, _appSettings);


            // Initialize global one instance of Settings.
            var userSettings = new UserSettings(_appSettings, _logger);
            if (!userSettings.Init())
            {
                _logger.SendLogAsync("User decided not to pick Photo folder.");
                Application.Current.Shutdown();
            }
            serviceCollection.AddSingleton(userSettings);

            var operatingSettings = new OperatingSettings(
                new BaseThreadSafeFileCache<OperatingSettings>(_appSettings.OperatingSettingsDBFileName, _logger, _appSettings));
            await operatingSettings.LoadAsync();
            // await operatingSettings.SaveAsync();
            serviceCollection.AddSingleton(operatingSettings);

            var license = new License(
                new LicenseCache(_appSettings.LicenseFileDBFileName, _logger, _appSettings), operatingSettings, _logger);
            await license.ReadLicenseAsync();
            serviceCollection.AddSingleton(license);


            serviceCollection.AddSingleton<ILuceneService, LuceneService>();
        }

        private ILogger CreateLogger(string appDataFolder, bool debugMode)
        {
            try
            {
                _logger = new AsyncLog(appDataFolder, debugMode);
                return _logger;
            }
            catch (Exception)
            {
                return new DummyLog(appDataFolder);
            }

        }

        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            Dispatcher.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Dispatcher.UnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
        }
        private void LogUnhandledException(Exception exception, string source)
        {
            var logMessage =  _logger.SendLogWithException("LogUnhandledException", exception);
            // Utils.DisplayMessageBox(string.Format($"LogUnhandledException: {logMessage}"));
            Utils.DisplayMessageBox($"There is an error occurred : {exception.Message} Pitara will close\n Please check log file for more details.");
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                var serviceCollection = new ServiceCollection();
                await ConfigureServices(serviceCollection);
                ServiceProvider = serviceCollection.BuildServiceProvider();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initialization Pitara.\nError: {ex.Message}", "Pitara", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
                return;
            }
            
            _logger.SendLogAsync($">>>>>>>>>    Launching version: {CurrentVersionWrapper.GetVersion()}. Supported Index version: {CurrentVersionWrapper.GetSupportedIndexVersion()}");
            _logger.SendLogAsync($"Starting app @ {DateTime.Now}");

#if DEBUG
            try
            {
                UnitTestOneOff(_logger);
            }
            catch (Exception )
            {
                // MessageBox.Show("Comment broken unit tests");
            }
#endif



            Process thisProc = Process.GetCurrentProcess();
            if (Process.GetProcessesByName(thisProc.ProcessName).Length > 1)
            {
                Utils.DisplayMessageBox("Pitara is already running on this PC.");
                // MessageBox.Show("Pitara is already running on this PC.");
                Application.Current.Shutdown();
                return;
            }
            else
            { 
                RemoteVersion remoteVersion = new RemoteVersion();
                var remoteVer = await remoteVersion.CheckCurrentVersion(_logger);
                var currentVer = CurrentVersionWrapper.GetVersion();
                if (remoteVer > currentVer)
                {
                    _logger.SendLogAsync($"You are not running latest version. Please download current released version:{remoteVer}");
                    if (
                        Utils.DisplayMessageBoxAskYesNo("Newer version of Pitara is available to download. Do you want to download newer version instead?")
                        == System.Windows.MessageBoxResult.Yes)
                    {
                        Utils.ProcessStartWrapperAsync("https://getpitara.com/en/home/download/");
                        Application.Current.Shutdown();
                    }
                }
                else
                {
                    _logger.SendLogAsync($"You are running latest version.");
                }
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }

        }
        private async Task GenerateTestPhotos(string srcFolder, ILogger logger)
        {
            logger.SendLogAsync(@"Starting Test..");
            if (!srcFolder.StartsWith(@"d:\Pitara-Test-Data"))
            {
                logger.SendLogAsync(@"Must only run for - d:\Pitara-Test-Data");
                return;
            }

            string[] files = Directory.GetFiles(srcFolder, "*.jpg", SearchOption.AllDirectories);
            int count = files.Length;
            int index = 0;
            foreach (var file in files)
            {
                logger.SendLogAsync($"{index++}) - {file}");
                await Task.Delay(10);
                var logoPath = @"D:\Workspace\InstaFind\Pitara\Pitara\brush.png";
                await PhotoManipulation.DONOTUSE__ONLY_TO_GENERATE_TEST_DATA_OverWriteImage(file, logoPath);
            }
            logger.SendLogAsync("Done it.");
        }

        // WARNING Caliing this even once will mess up UI Component error. Clean up .vs, bin, obj after.
        private void DumpAllAssemblies()
        {
            
            var assemblies = Assembly.LoadFile(
                System.Reflection.Assembly.GetExecutingAssembly().Location).GetReferencedAssemblies();
            var nameArray = assemblies.Select(x => x.FullName).ToArray();
            File.WriteAllLines("assemblies.txt", nameArray);
        }
        private void UnitTestOneOff(ILogger logger)
        {
            // DumpAllAssemblies();
            // var one = Utils.GetHigherThanNKFeetQueryFragment(5);
            // var x = 4;
            //var one = PhotoManipulation.GetHeightTag(145.623);
            //var one1 = PhotoManipulation.GetHeightTag(645.623);
            //var one2 = PhotoManipulation.GetHeightTag(1245.623);
            //var one = Utils.GetLastNMonthsQueryFragment(1);
            //var three = Utils.GetLastNMonthsQueryFragment(3);
            //var six = Utils.GetLastNMonthsQueryFragment(6);
            //var one = Utils.GetLastNYearsQueryFragment(1);
            //var five = Utils.GetLastNYearsQueryFragment(5);
            //var ten = Utils.GetLastNYearsQueryFragment(10);
            //var fifteen = Utils.GetLastNYearsQueryFragment(15);
            //var twenty = Utils.GetLastNYearsQueryFragment(20);
            //List<int> intList = new List<int>();
            //for(int i =0; i< 11; i++)
            //{
            //    intList.AddCommon(i); 
            //}

            //var bp = new BatchProcessor<int>("test-int",
            //    logger,
            //    intList,
            //    3,
            //    (int item) =>
            //    {
            //        logger.SendLogAsync($"---------------------------------------- Processing - {item}");
            //        return 0;
            //    });
            //bp.Run();
            // Application.Current.Shutdown();

            // var tags = PhotoManipulation.GetSuggestedTagsFromFolderName(@"d:\\123Name\\456\\fourFiveSiix\seven66");
        }
    }
}
