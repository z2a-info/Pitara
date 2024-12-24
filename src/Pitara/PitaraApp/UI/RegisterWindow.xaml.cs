using CommonProject.Src;
using System;
using System.Windows;
using System.Windows.Input;

namespace PitaraLuceneSearch.UI
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class RegisterWindow : Window
    {
        private AppSettings _appSettings;
        private static ILogger _logger;
        public License License { get => _license; set => _license = value; }
        private License _license;
        public RegisterWindow(UserSettings userSettings, 
            ILogger logger,
            AppSettings appSettings, 
            License license)
        {
            DataContext = license;
            _license = license;
            _logger = logger;
            _appSettings = appSettings;

            this.PreviewKeyDown += DuplicateRemovalWindow_PreviewKeyDown;
            Activated += DuplicateRemovalWindow_Activated;
            InitializeComponent();
        }
        private void DuplicateRemovalWindow_Activated(object sender, EventArgs e)
        {
            LicenseCode.Focus();
            if (string.IsNullOrEmpty(_license.LicenseCode))
            {
                RegisterButton.IsEnabled = false;
            }
        }

        private void DuplicateRemovalWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                btnCancelData_Click(sender, e);
            }
        }

        // Register
        private void btnSaveData_Click(object sender, RoutedEventArgs e)
        {
            Guid result;
            if (!Guid.TryParse(_license.LicenseCode, out result))
            {
                CommonProject.Src.Utils.DisplayMessageBox($"Invalid license code.", this);
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

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var url = "https://getpitara.com/en/purchase-license/";
            Utils.ProcessStartWrapperAsync(url);
        }

        private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox objTextBox = (System.Windows.Controls.TextBox)sender;
            string theText = objTextBox.Text;
            ((License)DataContext).LicenseCode = theText;

            if (string.IsNullOrEmpty(theText))
            {
                if(RegisterButton!= null)
                { 
                    RegisterButton.IsEnabled = false; 
                }
            }
            else
            {
                if (RegisterButton != null)
                { 
                    RegisterButton.IsEnabled = true; 
                }
            }

        }
    }
}
