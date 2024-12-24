using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public class UserSettings : IEquatable<UserSettings>, IDisposable
    {
        public List<string> PhotoFolders { get; set; } = new List<string>();
        public List<string> ExcludeFolders { get; set; } = new List<string>();
        public string BucketFolder { get; set; }
        public string IndexFolder { get; set; }
        // public string DeletedFolder { get; set; }
        // public string DuplicateFolder { get; set; }
        public string FestivalFolder { get; set; }
        public string SelectedCountryFile { get; set; }
        private string _settingsFilePath;
        private static ILogger _logger;
        private AppSettings _appSettings;
        public string CombinePaths(List<string> pathCollection)
        {
             return string.Join(";", pathCollection.ToArray());
        }

        [JsonIgnore]
        public string SettingsFilePath { get => _settingsFilePath; set => _settingsFilePath = value; }
        // private string appSettings.AppDataFolder;
        public void LogSettings()
        {
            _logger.SendDebugLogAsync($"Current settings: {ToString()}");
        }
        public UserSettings()
        {
        }
        public bool Init()
        {
            if (!File.Exists(_appSettings.PitaraSettingFileName))
            {
                GenerateDefaultSettingFile();
                SaveSettings();
                // Prompt For Photo Folder
                //SettingsWindow settingsWindow = new SettingsWindow(this.Clone());
                //settingsWindow.SuppressReIndexWarning = true;
                //var results = settingsWindow.ShowDialog();
                //if (results.HasValue && results == true)
                //{
                //    this.CopyPropertiesFrom(settingsWindow.Settings);
                //    SaveSettings();
                //}
                //else
                //{
                //    return false;
                //}
            }
            LoadSettings();
            return true;
        }
        public UserSettings(AppSettings appSettings, ILogger logger)
        {
            _logger = logger;
            _appSettings = appSettings;
            
            SettingsFilePath = appSettings.PitaraSettingFileName;
       }

        private void GenerateDefaultSettingFile()
        {
            BucketFolder = _appSettings.AppDataFolder + @"Export\";
            IndexFolder = _appSettings.AppDataFolder + @"Index\";
            // DeletedFolder = _appSettings.AppDataFolder + @"Deleted\";
            // DuplicateFolder = _appSettings.AppDataFolder + @"Duplicate\";
            
            // Default no photo folders. Later user will be prompted before controller is launched.
            PhotoFolders = new List<string>();
            
            var appDir = Directory.GetParent(AppContext.BaseDirectory).FullName;

            // Festival files are pre shipped with setup hence are at location by the exe.
            FestivalFolder = appDir + @"\Festivals\";
            SelectedCountryFile = FestivalFolder + "America.fes";
            
            Utils.EnsureFolderExist(FestivalFolder);
            Utils.EnsureFolderExist(IndexFolder);
            Utils.EnsureFolderExist(BucketFolder);
            // Utils.EnsureFolderExist(DeletedFolder);
            // Utils.EnsureFolderExist(DuplicateFolder);
        }

        private bool LoadSettings()
        {
            try
            {
                string fileContents = File.ReadAllText(SettingsFilePath);
                var helpText = $"\nIf you manually accidentally messed up setting file. Please delete the setting file and Pitara will recreate it for you.\nPath for setting file: {SettingsFilePath}";
                if (string.IsNullOrEmpty(fileContents))
                {
                    var message = "Setting file empty." + helpText;
                    _logger.SendLogAsync(message);
                    throw new Exception(message);
                }
                UserSettings settings = JsonConvert.DeserializeObject<UserSettings>(fileContents);
                this.BucketFolder = settings.BucketFolder;
                if (BucketFolder == null)
                {
                    var message = "Bucket folder is missing from setting file." + helpText; ;
                    _logger.SendLogAsync(message);
                    throw new Exception(message);
                }

                FestivalFolder = settings.FestivalFolder;
                if (FestivalFolder == null)
                {
                    var appDir = Directory.GetParent(AppContext.BaseDirectory).FullName;
                    FestivalFolder = appDir + @"\Festivals\";
                    var message = "Festival folder is missing from setting file. Setting default" + helpText; ;
                    _logger.SendLogAsync(message);
                    _logger.SendLogAsync($"Set Festivals folder to:{FestivalFolder}");
                }
                SelectedCountryFile = settings.SelectedCountryFile;
                if (SelectedCountryFile == null)
                {
                    SelectedCountryFile = FestivalFolder + "America.fes";
                    _logger.SendLogAsync("Earlier selection of country was missing, setting back to America");
                }

                this.IndexFolder = settings.IndexFolder;
                if (IndexFolder == null)
                {
                    var message = "Index  folder is missing from setting file." + helpText; ;
                    _logger.SendLogAsync(message);
                    throw new Exception(message);
                }

                //this.DuplicateFolder = settings.DuplicateFolder;
                //this.DeletedFolder = settings.DeletedFolder;
                //if (DeletedFolder == null)
                //{
                //    var message = "Deleted  folder is missing from setting file." + helpText; ;
                //    _logger.SendLogAsync(message);
                //    throw new Exception(message);
                //}

                this.PhotoFolders = settings.PhotoFolders;
                if (PhotoFolders == null || PhotoFolders.Count == 0)
                {
                    var message = "Photo folders to scan is missing from setting file." + helpText; ;
                    _logger.SendDebugLogAsync(message);
                    // Not an error. We will prompt user later.
                }
                 this.ExcludeFolders = settings.ExcludeFolders;
                // _logger.SendDebugLogAsync($"Settings file loaded properly.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"Couldn't load setting. Error: {ex.Message}");
                throw;
                // return false;
            }
        }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
        public void SaveSettings()
        {
            string stringContent = JsonConvert.SerializeObject(this);
            File.WriteAllText(SettingsFilePath, stringContent);
        }
        public bool IsValid()
        {
            return (PhotoFolders.Count() > 0);
        }
        public UserSettings Clone()
        {
            string serializedStr = JsonConvert.SerializeObject(this);
            var useSetting = JsonConvert.DeserializeObject<UserSettings>(serializedStr);
            useSetting.SettingsFilePath = SettingsFilePath;
            return useSetting;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as UserSettings);
        }

        public bool Equals(UserSettings other)
        {
            return other != null &&
                   PhotoFolders.SequenceEqual(other.PhotoFolders) &&
                   ExcludeFolders.SequenceEqual(other.ExcludeFolders) &&
                   BucketFolder == other.BucketFolder &&
                   IndexFolder == other.IndexFolder &&
                   // DuplicateFolder == other.DuplicateFolder &&
                   // DeletedFolder == other.DeletedFolder &&
                   SettingsFilePath == other.SettingsFilePath;
        }

        public override int GetHashCode()
        {
            var hashCode = 268168903;
            hashCode = hashCode * -1521134295 + EqualityComparer<List<string>>.Default.GetHashCode(PhotoFolders);
            hashCode = hashCode * -1521134295 + EqualityComparer<List<string>>.Default.GetHashCode(ExcludeFolders);
            // hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(DuplicateFolder);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(BucketFolder);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(IndexFolder);
            // hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(DeletedFolder);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SettingsFilePath);
            return hashCode;
        }
        public void CopyPropertiesFrom(UserSettings other)
        {
            PhotoFolders = other.PhotoFolders;
            ExcludeFolders = other.ExcludeFolders;
            BucketFolder = other.BucketFolder;
            IndexFolder = other.IndexFolder;
            // DuplicateFolder = other.DuplicateFolder;
            // DeletedFolder = other.DeletedFolder;
            SettingsFilePath = other.SettingsFilePath;
        }
        public void Dispose()
        {
            SaveSettings();
        }

        public static bool operator ==(UserSettings left, UserSettings right)
        {
            return EqualityComparer<UserSettings>.Default.Equals(left, right);
        }

        public static bool operator !=(UserSettings left, UserSettings right)
        {
            return !(left == right);
        }

        public async Task<List<string>> IsPhotoFoldersAccessibleAsync()
        {
            List<string> troubleFolders = new List<string>();

            List<Task> taskArray = new List<Task>();
            
            object lockOb = new object();

            foreach (var folder in PhotoFolders)
            {
                taskArray.Add(Task.Run(()=> {
                    try
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(folder);
                        dirInfo.GetDirectories();
                    }
                    catch (Exception ex)
                    {
                        _logger.SendLogAsync($"Folder: {folder} is inaccessible. Error:{ex.Message}");
                        lock (lockOb)
                        {
                            troubleFolders.Add(folder);
                        }
                    }
                }));
            }
            await Task.WhenAll(taskArray.ToArray());
            return troubleFolders;
        }
    }
}
