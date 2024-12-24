using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonProject.Src
{
    public enum RecepientType
    {
        GPS_DATA,
        TIME_DATA,
        CAMERA_MODEL_MAKE_TAGS,
        CUSTOMEKEYWORD_DATA,
        MISC_DATA,
        FILE_FOLDER_DATA,
        HEIGHT_DATA
    }
    public class PitaraEventArg : EventArgs
    {
        public PitaraEventArg(object objectLocal) 
        {
            _object = objectLocal;
        }
        public object _object;
    };
    public delegate void PitaraEventHandler(object sender, PitaraEventArg e);
    public class AppSettings
    {
        public static int MaxHistoryBuffer = 200;
        public QueryInfo[] BrowsingHistory = new QueryInfo[MaxHistoryBuffer];
        public bool BuildHistory = true;
        public int BrowsingHistoryCursor = -1;
        public int BrowsingHistoryMaxForward = -1;
        public readonly string MessageBoxCaption;
        // public int ActualResultCount = 0;
        public readonly string GpsDBFileName;
        public readonly string TimeDBFileName;
        public readonly string OperatingSettingsDBFileName;
        public readonly string CustomKeywordsDBFileName;
        public readonly string LicenseFileDBFileName;
        public readonly string PitaraSettingFileName;
        public readonly string AppDataFolder;
        public readonly string CameraModelMakeKeywordsFileName;
        public readonly string MiscTagsFileName;
        public readonly string BadFilesName;
        public readonly string FileFolderKeywordsDBFileName;
        public readonly string HeightKeywordsDBFileName;
        public readonly string FavoritesDBFileName;
        public readonly string DuplicateCacheFileName;
        public readonly string IndianFestivalsDBFileName;
        public readonly int MaxResultsToFetch;
        private static ILogger _logger;


        public AppSettings(string appDataFolder, ILogger logger)
        {
            try
            {
                _logger = logger;
                AppDataFolder = appDataFolder;
                GpsDBFileName = appDataFolder + "GpsCache.txt";
                TimeDBFileName = appDataFolder + "TimeCache.txt";
                CustomKeywordsDBFileName = appDataFolder + "KeywordCache.txt";
                OperatingSettingsDBFileName = appDataFolder + "OperatingSettingCache.txt";
                DuplicateCacheFileName = appDataFolder + "DuplicateCache.txt";
                FileFolderKeywordsDBFileName = appDataFolder + "FileFolderKeywordCache.txt";
                HeightKeywordsDBFileName = appDataFolder + "HeightKeywordsCache.txt";
                CameraModelMakeKeywordsFileName = appDataFolder + "CameraMakeModelCache.txt";
                MiscTagsFileName = appDataFolder + "MiscKeywordsCache.txt";
                BadFilesName = appDataFolder + "BadFiles.txt";
                FavoritesDBFileName = appDataFolder + "FavoriteCache.txt";
                // IndianFestivalsDBFileName = appDataFolder + "IndianFestivals.txt";
                LicenseFileDBFileName = appDataFolder + "License.db";
                PitaraSettingFileName = appDataFolder + @"PitaraSettings.json";

                MessageBoxCaption = @"Pitara";
                MaxResultsToFetch = 5000;
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"AppSettings error:{ex.Message}");
                throw;
            }
        }
    }
}