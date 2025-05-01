using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Media;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace DSXGameHelperExtended
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<GameInfo> gamePaths;
        private Timer processCheckTimer;
        private int checkInterval;
        private static readonly string AppDataFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DSXGameHelperExtended"
        );
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolderPath, "settings.json");

        private Settings appSettings;

        public string dsxExecutablePath { get; private set; }
        private TaskbarIcon taskbarIcon;


        public MainWindow()
        {
            InitializeComponent();
            appSettings = LoadSettings();
            gamePaths = new ObservableCollection<GameInfo>(appSettings.GamePaths);
            lvGames.ItemsSource = gamePaths;
            foreach (var game in gamePaths)
            {
                game.PropertyChanged += GameInfo_PropertyChanged;
            }

            DataContext = appSettings;
            InitializeTimer();
            UpdateStatus("Ready. No game running.");


            taskbarIcon = new TaskbarIcon();
            taskbarIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/controller.ico")); // Replace with your own icon path
            taskbarIcon.ToolTipText = "DSX Game Helper Extended";
            taskbarIcon.TrayMouseDoubleClick += TaskbarIcon_DoubleClick;

            ContextMenu contextMenu = new ContextMenu();
            MenuItem openMenuItem = new MenuItem() { Header = "Open" };
            openMenuItem.Click += (sender, e) => OpenMainWindow(sender);
            MenuItem exitMenuItem = new MenuItem() { Header = "Exit" };
            exitMenuItem.Click += (sender, e) => Close();
            contextMenu.Items.Add(openMenuItem);
            contextMenu.Items.Add(exitMenuItem);

            taskbarIcon.ContextMenu = contextMenu;

        }

        private void TaskbarIcon_DoubleClick(object sender, RoutedEventArgs e)
        {
            OpenMainWindow(sender);
        }

        private void OpenMainWindow(object sender)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }


        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }


        private void InitializeTimer()
        {
            checkInterval = appSettings.CheckInterval * 1000; // Convert to milliseconds
            processCheckTimer = new Timer(CheckRunningGames, null, 0, checkInterval);
        }

        private Settings LoadSettings()
        {
            try
            {
                if (!Directory.Exists(AppDataFolderPath))
                {
                    Directory.CreateDirectory(AppDataFolderPath);
                }

                if (File.Exists(SettingsFilePath))
                {
                    string settingsJson = File.ReadAllText(SettingsFilePath);
                    Settings settings = JsonSerializer.Deserialize<Settings>(settingsJson);

                    dsxExecutablePath = settings?.DSXExecutablePath;
                    foreach (var game in settings.GamePaths)
                    {
                        game.IconSource = GetIconFromExePath(game.GamePath);
                        game.PropertyChanged += GameInfo_PropertyChanged;
                    }
                    return settings ?? new Settings();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading settings: {ex.Message}");
            }

            return new Settings();
        }

        private void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(AppDataFolderPath))
                {
                    Directory.CreateDirectory(AppDataFolderPath);
                }

                appSettings.GamePaths = gamePaths.ToList();
                string settingsJson = JsonSerializer.Serialize(appSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, settingsJson);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error saving settings: {ex.Message}");
            }
        }

        private void UpdateStatus(string message, bool isLeft = true)
        {
            Dispatcher.Invoke(() =>
            {
                if (isLeft)
                    txtStatusLeft.Text = message;
                else
                    txtStatusRight.Text = message;
            });
        }

        private void btnAddGame_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                InitialDirectory = appSettings.LastUsedDirectory
            };

            if (openFileDialog.ShowDialog() == true)
            {
                appSettings.LastUsedDirectory = Path.GetDirectoryName(openFileDialog.FileName);

                var gameInfo = new GameInfo
                {
                    GamePath = openFileDialog.FileName,
                    GameName = Path.GetFileNameWithoutExtension(openFileDialog.FileName)
                };

                gameInfo.IconSource = GetIconFromExePath(gameInfo.GamePath);
                gameInfo.PropertyChanged += GameInfo_PropertyChanged;

                gamePaths.Add(gameInfo);
                SaveSettings();
                UpdateStatus($"Game added: {gameInfo.GameName}");
            }
        }

        private void btnRemoveGame_Click(object sender, RoutedEventArgs e)
        {
            if (lvGames.SelectedItem is GameInfo selectedGame)
            {
                gamePaths.Remove(selectedGame);
                SaveSettings();
                UpdateStatus($"Game removed: {selectedGame.GameName}");
            }
            else
            {
                MessageBox.Show("Please select a game to remove.", "No Game Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = gamePaths.Where(g => g.IsSelected).ToList();

            if (selectedGames.Count == 0)
            {
                MessageBox.Show("No games selected to remove.", "Remove Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to remove {selectedGames.Count} game(s)?", "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                foreach (var game in selectedGames)
                {
                    gamePaths.Remove(game);
                }

                SaveSettings();
                UpdateStatus($"{selectedGames.Count} game(s) removed.");

                chkSelectAll.IsChecked = false;
            }
        }

        private void chkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var game in gamePaths)
            {
                game.IsSelected = true;
            }
            UpdateSelectedCountStatus();
        }

        private void chkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var game in gamePaths)
            {
                game.IsSelected = false;
            }
            UpdateSelectedCountStatus();
        }

        public void UpdateSelectedCountStatus()
        {
            int selectedCount = gamePaths.Count(g => g.IsSelected);
            string msg = selectedCount == 0 ? "No game selected." : $"{selectedCount} game(s) selected.";
            UpdateStatus(msg, isLeft: false);
        }

        private void CheckRunningGames(object state)
        {
            var runningProcessNames = Process.GetProcesses().Select(p => p.ProcessName).ToList();
            bool anyGameRunning = gamePaths.Any(gameInfo => runningProcessNames.Contains(gameInfo.GameName));

            Dispatcher.Invoke(() =>
            {
                if (anyGameRunning)
                {
                    EnsureDSXIsRunning();
                }
                else
                {
                    EnsureDSXIsNotRunning();
                    UpdateStatus("No game running.", isLeft: true);
                }
            });
        }

        private void EnsureDSXIsRunning()
        {
            string dsxProcessName = Path.GetFileNameWithoutExtension(appSettings.DSXExecutablePath);
            if (!IsProcessRunning(dsxProcessName))
            {
                StartDSX();
            }
        }

        private void StartDSX()
        {
            if (string.IsNullOrWhiteSpace(appSettings.DSXExecutablePath))
            {
                UpdateStatus("Game running but no DSX path selected.");
                return;
            }

            if (!File.Exists(appSettings.DSXExecutablePath))
            {
                UpdateStatus("Invalid DSX Path.");
                return;
            }

            if (appSettings.DSXVersionIndex == 0) // DSX v1 (FREE)
            {
                Process.Start(appSettings.DSXExecutablePath);
            }
            else // DSX v2/v3 (STEAM)
            {
                Process.Start("explorer", "steam://rungameid/1812620");
            }
            UpdateStatus("DSX started with game.");
        }

        private void EnsureDSXIsNotRunning()
        {
            string[] processNamesToKill = { "DSX", "DualSenseX" };

            foreach (string processName in processNamesToKill)
            {
                try
                {
                    var dsxProcesses = Process.GetProcesses().Where(p => p.ProcessName.Contains(processName) && !p.ProcessName.Contains("DSXGame"));

                    foreach (var dsxProcess in dsxProcesses)
                    {
                        try
                        {
                            dsxProcess.Kill();
                            dsxProcess.WaitForExit();
                            UpdateStatus($"{dsxProcess.ProcessName} (PID: {dsxProcess.Id}) stopped.");
                        }
                        catch (Exception ex)
                        {
                            UpdateStatus($"Error stopping {dsxProcess.ProcessName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error getting processes for {processName}: {ex.Message}");
                }
            }
        }

        private bool IsProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Any();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            processCheckTimer.Change(0, checkInterval);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            processCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
            EnsureDSXIsNotRunning();
        }

        private void btnScanFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = folderDialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK && Directory.Exists(folderDialog.SelectedPath))
            {
                string selectedFolder = folderDialog.SelectedPath;
                var allExeFiles = Directory.GetFiles(selectedFolder, "*.exe", SearchOption.AllDirectories);
                var exeFiles = FilterExecutableFiles(allExeFiles);

                if (exeFiles.Length == 0)
                {
                    System.Windows.MessageBox.Show("No .exe files found in this folder.", "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var selectedFiles = ShowExeSelectionDialog(exeFiles);

                int addedCount = 0;

                foreach (var exePath in selectedFiles)
                {
                    string gameName = Path.GetFileNameWithoutExtension(exePath);

                    if (!gamePaths.Any(g => g.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var gameInfo = new GameInfo
                        {
                            GamePath = exePath,
                            GameName = gameName,
                            IconSource = GetIconFromExePath(exePath)
                        };

                        gameInfo.PropertyChanged += GameInfo_PropertyChanged;
                        gamePaths.Add(gameInfo);
                        addedCount++;
                    }
                }

                SaveSettings();
                UpdateStatus($"Scan complete. {addedCount} game(s) added.");
            }
        }

        private string[] ShowExeSelectionDialog(string[] exePaths)
        {
            var window = new ExeSelectionWindow(exePaths);
            bool? result = window.ShowDialog();

            if (result == true)
            {
                return window.GetSelectedFiles();
            }

            return Array.Empty<string>();
        }

        private string[] FilterExecutableFiles(string[] exePaths)
        {
            string[] unwantedKeywords = new string[]
            {
        "setup", "handler", "support", "process", "tool", "setter", "browser",
        "debug", "crash", "background", "updater", "install", "service",
        "render", "entry", "launch", "profile", "diagnose", "miniticketdbg",
        "restarter", "ue3", "unreal", "system", "report", "udk", "stats",
        "vcredist", "registry", "java", "shader", "unins", "eos", "benchmark",
        "vc_redist", "backup", "trial", "cleanup", "activation", "touchup",
        "webhelper"
            };

            List<string> filteredList = new List<string>();

            foreach (var exePath in exePaths)
            {
                string fileNameLower = Path.GetFileName(exePath).ToLowerInvariant();

                if (fileNameLower.Contains("launch") && !fileNameLower.Contains("launcher"))
                    continue;

                bool hasUnwantedKeyword = unwantedKeywords.Any(keyword => fileNameLower.Contains(keyword));

                if (hasUnwantedKeyword)
                    continue;

                filteredList.Add(exePath);
            }

            return filteredList.ToArray();
        }

        private ImageSource GetIconFromExePath(string exePath)
        {
            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                {
                    if (icon != null)
                    {
                        return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private void GameInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SaveSettings();
        }

        private void lvGames_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (lvGames.SelectedItem is GameInfo selectedGame)
            {
                ContextMenu contextMenu = new ContextMenu();
                MenuItem editItem = new MenuItem { Header = "Edit Game Name" };
                editItem.Click += (s, args) => OpenEditNameDialog(selectedGame);
                contextMenu.Items.Add(editItem);

                contextMenu.IsOpen = true;
            }
        }

        private void OpenEditNameDialog(GameInfo game)
        {
            var dialog = new EditGameNameWindow(game.GameName)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.EditedName))
            {
                game.GameName = dialog.EditedName;
            }
        }
    }

    public class GameInfo : INotifyPropertyChanged
    {
        private string gameName;
        private string gamePath;
        private ImageSource iconSource;

        public string GameName
        {
            get => gameName;
            set
            {
                if (gameName != value)
                {
                    gameName = value;
                    OnPropertyChanged(nameof(GameName));
                    AutoSave(); // Trigger save when changed
                }
            }
        }

        public string GamePath
        {
            get => gamePath;
            set
            {
                if (gamePath != value)
                {
                    gamePath = value;
                    OnPropertyChanged(nameof(GamePath));
                    AutoSave();
                }
            }
        }

        [JsonIgnore]
        public ImageSource IconSource
        {
            get => iconSource;
            set
            {
                if (iconSource != value)
                {
                    iconSource = value;
                    OnPropertyChanged(nameof(IconSource));
                }
            }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    (Application.Current.MainWindow as MainWindow)?.UpdateSelectedCountStatus();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AutoSave()
        {
        }
    }

    public class Settings
    {
        public Settings()
        {
            DSXVersionIndex = 0; // Default to DSX v1 (FREE)
            CheckInterval = 1;  // Default to 1 second

        }
        public string LastUsedDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        public List<GameInfo> GamePaths { get; set; } = new List<GameInfo>();
        public string DSXExecutablePath { get; set; }
        public int DSXVersionIndex { get; set; }
        public int CheckInterval { get; set; } = 1; // Default to 5 seconds
    }
}
