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
            chkSkipConfirmation.IsChecked = settings.SkipLaunchConfirmation;
            chkSkipConfirmation.IsEnabled = settings.EnableDoubleClickLaunch;
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
            bool newStartWithWindows = chkStartWithWindows.IsChecked == true;

            if (settings.StartWithWindows != newStartWithWindows)
            {
                settings.StartWithWindows = newStartWithWindows;
                StartupHelper.SetStartup(newStartWithWindows);
            }
            settings.EnableDoubleClickLaunch = chkDoubleClickLaunch.IsChecked == true;
            settings.SkipLaunchConfirmation = chkDoubleClickLaunch.IsChecked == true && chkSkipConfirmation.IsChecked == true;
            settings.StartWithWindows = chkStartWithWindows.IsChecked == true;
            settings.StartMinimized = chkStartMinimized.IsChecked == true;
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
        private void EnableDoubleClick_Checked(object sender, RoutedEventArgs e)
        {
            bool isDoubleClickEnabled = chkDoubleClickLaunch.IsChecked == true;
            chkSkipConfirmation.IsEnabled = isDoubleClickEnabled;

            if (!isDoubleClickEnabled)
            {
                chkSkipConfirmation.IsChecked = false;
            }
        }
    }
}