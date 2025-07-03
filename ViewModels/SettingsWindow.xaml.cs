using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

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

            switch (settings.ThemeMode)
            {
                case "Light":
                    radioLight.IsChecked = true;
                    break;
                case "Dark":
                    radioDark.IsChecked = true;
                    break;
                default:
                    radioSystem.IsChecked = true;
                    break;
            }

            radioLight.Checked += ThemeRadio_Checked;
            radioDark.Checked += ThemeRadio_Checked;
            radioSystem.Checked += ThemeRadio_Checked;
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (radioLight.IsChecked == true)
                settings.ThemeMode = "Light";
            else if (radioDark.IsChecked == true)
                settings.ThemeMode = "Dark";
            else
                settings.ThemeMode = "System";

            ThemeManager.ApplyTheme(settings.ThemeMode);

            MainWindow main = Application.Current.MainWindow as MainWindow;
            if (main != null)
            {
                main.SaveSettings();
            }
            else
            {
                SaveSettingsToFile(settings);
            }
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

            if (radioLight.IsChecked == true)
                settings.ThemeMode = "Light";
            else if (radioDark.IsChecked == true)
                settings.ThemeMode = "Dark";
            else
                settings.ThemeMode = "System";

            StartupHelper.SetStartup(settings.StartWithWindows);

            MainWindow main = Application.Current.MainWindow as MainWindow;
            main?.SaveSettings();
            ThemeManager.ApplyTheme(settings.ThemeMode);

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

        private void SaveSettingsToFile(Settings settings)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new GameInfoConverter() }
                };

                string settingsJson = JsonSerializer.Serialize(settings, options);
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DSXGameHelperExtended");
                string settingsPath = Path.Combine(appDataPath, "settings.json");

                Directory.CreateDirectory(appDataPath);
                File.WriteAllText(settingsPath, settingsJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}