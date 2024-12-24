using CommonProject.Src;
using System;
using System.Windows;
using System.Windows.Input;

namespace PitaraLuceneSearch.UI
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public class FavoriteQuery
    {
        public string Query { get; set; }
        public string QueryDescription { get; set; }
    }
    public partial class AddToFAvoriteWindow : Window
    {
        public FavoriteQuery FavoriteString;
        private static ILogger _logger;
        public AddToFAvoriteWindow(ILogger logger, FavoriteQuery favString)
        {
            _logger = logger;
            DataContext = favString;
            FavoriteString = favString;
            this.PreviewKeyDown += AddToFAvoriteWindow_PreviewKeyDown;
            Activated += AddToFAvoriteWindow_Activated;
            Loaded += AddToFAvoriteWindow_Loaded;
            InitializeComponent();
        }

        private void AddToFAvoriteWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FavDescription.Focus();
        }

        private void AddToFAvoriteWindow_Activated(object sender, EventArgs e)
        {
        }

        private void AddToFAvoriteWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
                this.Close();
            }
        }

        // Save
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FavoriteString.QueryDescription = FavDescription.Text;
            if (string.IsNullOrEmpty(FavoriteString.QueryDescription))
            {
                Utils.DisplayMessageBox("Please enter name of your bookmark", this);
                return;
            }
            this.DialogResult = true;
            this.Close();
        }
    }
}
