using System.Windows;

namespace DSXGameHelperExtended
{
    public partial class SettingsWindow : Window
    {
        private readonly Settings settings;

        public SettingsWindow(Settings currentSettings)
        {
            InitializeComponent();
            settings = currentSettings;

            chkStartWithWindows.IsChecked = settings.StartWithWindows;
            chkStartMinimized.IsChecked = settings.StartMinimized;
            chkDoubleClickLaunch.IsChecked = settings.EnableDoubleClickLaunch;
            txtDSXPath.Text = settings.DSXExecutablePath;

            chkNotifyStart.IsChecked = settings.NotifyOnStart;
            chkNotifyStop.IsChecked = settings.NotifyOnStop;
            chkNotifyError.IsChecked = settings.NotifyOnError;
            chkNotifyUpdate.IsChecked = settings.NotifyOnUpdate;
        }

        private void BrowseDSXPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe",
                Title = "Select DualSenseX Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                txtDSXPath.Text = dialog.FileName;
                settings.DSXExecutablePath = dialog.FileName;
                Save();
            }
        }
        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            await (Application.Current.MainWindow as MainWindow)?.CheckForUpdatesAsync();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            settings.StartWithWindows = chkStartWithWindows.IsChecked == true;
            settings.StartMinimized = chkStartMinimized.IsChecked == true;
            settings.EnableDoubleClickLaunch = chkDoubleClickLaunch.IsChecked == true;
            settings.DSXExecutablePath = txtDSXPath.Text;

            settings.NotifyOnStart = chkNotifyStart.IsChecked == true;
            settings.NotifyOnStop = chkNotifyStop.IsChecked == true;
            settings.NotifyOnError = chkNotifyError.IsChecked == true;
            settings.NotifyOnUpdate = chkNotifyUpdate.IsChecked == true;

            StartupHelper.SetStartup(settings.StartWithWindows);

            MainWindow main = Application.Current.MainWindow as MainWindow;
            main?.SaveSettings();

            base.OnClosing(e);
        }

        private void Save()
        {
            MainWindow main = Application.Current.MainWindow as MainWindow;
            main?.SaveSettings();
        }
    }
}