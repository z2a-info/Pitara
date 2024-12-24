using CommonProject.Src;
using DynamicData;
using Pitara;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace PitaraLuceneSearch.UI
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public UserSettings Settings;
        public bool SuppressReIndexWarning = false;
        // public bool IfReIndexingRequired = false;

        private UserSettings _originalSettings;
        private ObservableCollection<string> _photoFoldersInternal;
        private ObservableCollection<string> _excludeFoldersInternal;

        private int _shrinkHeight = 264;//258
        private int _originalHeight = 450; // Also update at XAML

        public SettingsWindow(UserSettings setting)
        {
            InitializeComponent();
            
            _originalSettings = setting.Clone();
            Settings = setting.Clone();
            DataContext = Settings;

            Loaded += SettingsWindow_Loaded;
            PreviewKeyDown += SettingsWindow_PreviewKeyDown;


            _photoFoldersInternal = new ObservableCollection<string>(Settings.PhotoFolders);
            PhotoFoldersListBox.ItemsSource = _photoFoldersInternal;

            _excludeFoldersInternal = new ObservableCollection<string>(Settings.ExcludeFolders);
            ExcludeFoldersListBox.ItemsSource = _excludeFoldersInternal;
        }

        private void EnableDisableSaveButtonIfNecessary()
        {
            if (_originalSettings.Equals(Settings))
            {
                btnSaveData.IsEnabled = false;
            }
            else
            {
                btnSaveData.IsEnabled = true;
            }
        }
        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EnableDisableSaveButtonIfNecessary();
            MoreScroll.Visibility = Visibility.Collapsed;
            MinHeight = _shrinkHeight;
            MaxHeight = _shrinkHeight;
            RemovePhotoFolderButton.IsEnabled = false;
            RemoveFromExcludeFolder.IsEnabled = false;

            EntirePCRadioButton.IsChecked = false;
            SelectedFolderRadioButton.IsChecked = true;
            EnableDisableAddPhotoFolderControls();
        }

        private void SettingsWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                btnCancelData_Click(sender, e);
            }
        }

        private void EnableDisableAddPhotoFolderControls()
        {
            if(EntirePCRadioButton.IsChecked?? true)
            {
                PhotoFoldersListBox.IsEnabled = false;
                AddPhotoFolderButton.IsEnabled = false;
                RemovePhotoFolderButton.IsEnabled = false;
            }
            else
            {
                PhotoFoldersListBox.IsEnabled = true;
                AddPhotoFolderButton.IsEnabled = true;
            }
            EnableDisableSaveButtonIfNecessary();
        }
        // Save
        private void btnSaveData_Click(object sender, RoutedEventArgs e)
        {
            if (PhotoFoldersListBox.Items.Count == 0 )
            {
                Utils.DisplayMessageBox($"You must select at least one photo folder to index",  this);
                return;
            }
            this.DialogResult = true;
            this.Close();
        }

        // Cancel
        private void btnCancelData_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // Entire PC
        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            EnableDisableAddPhotoFolderControls();
        }

        // photo folders
        private void RadioButton_Click_1(object sender, RoutedEventArgs e)
        {
            EnableDisableAddPhotoFolderControls();
        }

        // Index Folder
        //private void Unused_Button_Click(object sender, RoutedEventArgs e)
        //{
        //    var selectedFolder = UtilUI.LetUserPickAFolder(Settings.IndexFolder, 
        //        "Select a folder to store Index");
        //    if (string.IsNullOrEmpty(CheckIfSpecialFolder(Settings, selectedFolder, CallerType.Index, this)))
        //    {
        //        EnableDisableSaveButtonIfNecessary();
        //        return;
        //    }
        //    if ((selectedFolder.TrimEnd('\\') != Settings.IndexFolder.TrimEnd('\\'))
        //        && (!Utils.IsDirectoryEmpty(selectedFolder))
        //        && (selectedFolder.TrimEnd('\\') != _originalSettings.IndexFolder.TrimEnd('\\')))
        //    {
        //        Utils.DisplayMessageBox("Index folder is not empty.\nPlease select a separate dedicated folder for storing index.",  this);
        //        return;
        //    }

        //    Settings.IndexFolder = selectedFolder;
        //    // IndexFolderTextBox.Text = Settings.IndexFolder;
        //    EnableDisableSaveButtonIfNecessary();
        //}

        // Select Export folder.
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var selectedFolder = UtilUI.LetUserPickAFolder(Settings.BucketFolder,
                "Select an export folder");
            if (string.IsNullOrEmpty(CheckIfSpecialFolder(Settings, selectedFolder, CallerType.Bucket, this)))
            {
                EnableDisableSaveButtonIfNecessary();
                return;
            }
            // Is selected folder a child of any of photo folders then don't allow.
            if(Utils.IsChildOfAnyParent(Settings.PhotoFolders.ToArray(), selectedFolder))
            {
                Utils.DisplayMessageBox($"{selectedFolder}\nYou have selected this as photo folder, it can not be set as export folder", this);
                EnableDisableSaveButtonIfNecessary();
                return;
            }
            Settings.BucketFolder = selectedFolder;
            BucketFolderTextBox.Text = Settings.BucketFolder;
            EnableDisableSaveButtonIfNecessary();
        }
        public enum CallerType 
        {
            Index =0,
            Bucket =1,
            Deleted =2,
            Duplicate =3,
            AddFolder =4
        }
        public static string CheckIfSpecialFolder(UserSettings userSettings, string selectedFolder, CallerType type, Window parent)
        {
            if (string.IsNullOrEmpty(selectedFolder))
            {
                return string.Empty;
            }
            var reservedFolderName = string.Empty;
            if (type!= CallerType.Index && selectedFolder.StartsWith(userSettings.IndexFolder))
            {
                reservedFolderName = "Index";
            }
            if (type != CallerType.Bucket && selectedFolder.StartsWith(userSettings.BucketFolder))
            {
                reservedFolderName = "Export";
            }
            //if (type != CallerType.Deleted && selectedFolder.StartsWith(userSettings.DeletedFolder))
            //{
            //    reservedFolderName = "Deleted";
            //}
            //if (type != CallerType.Duplicate && selectedFolder.StartsWith(userSettings.DuplicateFolder))
            //{
            //    reservedFolderName = "Duplicate";
            //}
            if (!string.IsNullOrEmpty(reservedFolderName))
            {
                Utils.DisplayMessageBox($"{reservedFolderName} folder can not be selected.", parent);
                return string.Empty;
            }
            return selectedFolder;
        }
        // AddCommon photo folders.
        private void AddPhotoFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = UtilUI.LetUserPickAFolder(string.Empty,
                    "Select a folder to index.");
            if(string.IsNullOrEmpty(
                CheckIfSpecialFolder(Settings, selectedFolder, CallerType.AddFolder, this)))
            {
                EnableDisableSaveButtonIfNecessary();
                return;
            }
            if(Settings.PhotoFolders.Contains(selectedFolder))
            {
                Utils.DisplayMessageBox($"Folder: {selectedFolder} is already added to Photo Folders.", this);
                EnableDisableSaveButtonIfNecessary();
                return;
            }
            Settings.PhotoFolders.Add(selectedFolder);
            _photoFoldersInternal.Add(selectedFolder);
            EnableDisableSaveButtonIfNecessary();
        }

        // AddCommon exclusion folders.
        private void AddExclusionFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = UtilUI.LetUserPickAFolder(string.Empty,
                    "Select a folder to not include in indexing.");
            if (string.IsNullOrEmpty(selectedFolder))
            {
                return;
            }
            if (Settings.ExcludeFolders.Contains(selectedFolder))
            {
                Utils.DisplayMessageBox($"Folder: {selectedFolder} is already added to exclusion list.", this);
                EnableDisableSaveButtonIfNecessary();
                return;
            }

            Settings.ExcludeFolders.Add(selectedFolder);
            _excludeFoldersInternal.Add(selectedFolder);
            EnableDisableSaveButtonIfNecessary();
        }

        // Remove photo folders.
        private void RemovePhotoFolderButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> myList = new List<string>();

            foreach (var item in PhotoFoldersListBox.SelectedItems)
            {
                myList.Add(item.ToString());
            }

            foreach (var path in myList)
            {
                _photoFoldersInternal.Remove(path);
                Settings.PhotoFolders.Remove(path);
            }
            EnableDisableSaveButtonIfNecessary();

            //if (PhotoFoldersListBox.SelectedItems.Count>0)
            //{
            //    string selectedItem = PhotoFoldersListBox.SelectedItems[0].ToString();
            //    _photoFoldersInternal.Remove(selectedItem);
            //    Settings.PhotoFolders.Remove(selectedItem);
            //    EnableDisableSaveButtonIfNecessary();
            //}
        }

        // Remove exclusion folder.
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (ExcludeFoldersListBox.SelectedItems.Count > 0)
            {
                string selectedItem = ExcludeFoldersListBox.SelectedItems[0].ToString();
                _excludeFoldersInternal.Remove(selectedItem);
                Settings.ExcludeFolders.Remove(selectedItem);
                EnableDisableSaveButtonIfNecessary();
            }
        }

        // Deleted folder
        //private void Button_Click_3(object sender, RoutedEventArgs e)
        //{
        //    var selectedFolder = Utils.LetUserPickAFolder(Settings.DeletedFolder,
        //        "Select Deleted folder");
        //    if (string.IsNullOrEmpty(CheckIfSpecialFolder(Settings, selectedFolder, CallerType.Deleted, this)))
        //    {
        //        EnableDisableSaveButtonIfNecessary();
        //        return;
        //    }
        //    Settings.DeletedFolder = selectedFolder;
        //    DeleteFolderTextBox.Text = Settings.DeletedFolder;
        //    EnableDisableSaveButtonIfNecessary();
        //}
        private void ToggerHeight()
        {
            if (MoreScroll.Visibility == Visibility.Collapsed)
            {
                //  Expand
                MoreScroll.Visibility = Visibility.Visible;
                ButtonMore.Content = "- Advance";
                MinHeight = _originalHeight;
                MaxHeight = _originalHeight;
            }
            else
            {
                // Collapse
                MoreScroll.Visibility = Visibility.Collapsed;
                ButtonMore.Content = "+ Advance";
                MinHeight = _shrinkHeight;
                MaxHeight = _shrinkHeight;
            }
        }
        private void ButtonMore_Click(object sender, RoutedEventArgs e)
        {
            ToggerHeight();
        }

        private void PhotoFoldersListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (PhotoFoldersListBox.SelectedItems.Count > 0)
            {
                RemovePhotoFolderButton.IsEnabled = true;
            }
            else
            {
                RemovePhotoFolderButton.IsEnabled = false;
            }
        }

        private void ExcludeFoldersListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ExcludeFoldersListBox.SelectedItems.Count > 0)
            {
                RemoveFromExcludeFolder.IsEnabled = true;
            }
            else
            {
                RemoveFromExcludeFolder.IsEnabled = false;
            }
        }
    }

    //public class RadioBoolToIntConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        int integer = (int)value;
    //        if (integer == int.Parse(parameter.ToString()))
    //            return true;
    //        else
    //            return false;
    //    }
    //    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        return parameter;
    //    }
    //}

}
