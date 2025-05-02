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
using System.Threading.Tasks;
using System.Windows.Media.Animation;

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
        private readonly Uri iconIdle = new("pack://application:,,,/Assets/icon_idle.ico");
        private readonly Uri iconRunning = new("pack://application:,,,/Assets/icon_running.ico");
        private readonly Uri iconError = new("pack://application:,,,/Assets/icon_error.ico");

        private void SetTrayIcon(Uri iconUri)
        {
            taskbarIcon.IconSource = new BitmapImage(iconUri);
        }

        public MainWindow()
        {
            InitializeComponent();
            appSettings = LoadSettings();
            if (appSettings.StartMinimized)
            {
                WindowState = WindowState.Minimized;
                Hide();
            }
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
            SetTrayIcon(iconIdle);
            taskbarIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/controller.ico"));
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
            taskbarIcon.TrayBalloonTipClicked += (s, e) =>
            {
                if (!string.IsNullOrEmpty(pendingNotificationUrl))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pendingNotificationUrl,
                        UseShellExecute = true
                    });
                    pendingNotificationUrl = null;
                }
            };

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
            checkInterval = appSettings.CheckInterval * 1000;
            processCheckTimer = new Timer(CheckRunningGames, null, 0, checkInterval);
        }
        public void ShowNotification(string title, string message)
        {
            if (taskbarIcon == null) return;
            taskbarIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
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

        public void SaveSettings()
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
                UpdateSelectedCountStatus();
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
                UpdateSelectedCountStatus();
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
                SetTrayIcon(iconError);
                UpdateStatus("Game running but no DSX path selected.");
                return;
            }

            if (!File.Exists(appSettings.DSXExecutablePath))
            {
                SetTrayIcon(iconError);
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
            SetTrayIcon(iconRunning);
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
            SetTrayIcon(iconIdle);
        }

        private bool IsProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Any();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            processCheckTimer.Change(0, checkInterval);

            if (!appSettings.HasPromptedForDSXPath &&
                (string.IsNullOrWhiteSpace(appSettings.DSXExecutablePath) || !File.Exists(appSettings.DSXExecutablePath)))
            {
                PromptForDSXPath();
            }
            if (appSettings.NotifyOnUpdate)
            {
                _ = CheckForUpdatesAsync();
            }
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
        private string pendingNotificationUrl = null;

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

        private void PromptForDSXPath()
        {
            appSettings.HasPromptedForDSXPath = true;
            SaveSettings();

            var prompt = new DSXPathPromptWindow { Owner = this };
            bool? result = prompt.ShowDialog();

            switch (prompt.Result)
            {
                case DSXPathPromptWindow.DSXPromptResult.Auto:
                    string foundPath = SearchForDSXExecutable();
                    if (!string.IsNullOrEmpty(foundPath))
                    {
                        appSettings.DSXExecutablePath = foundPath;
                        SaveSettings();
                        MessageBox.Show("DSX found and path set!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Could not find DSX automatically.", "Search Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    break;

                case DSXPathPromptWindow.DSXPromptResult.Manual:
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "Executable files (*.exe)|*.exe",
                        Title = "Select DualSenseX Executable"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        appSettings.DSXExecutablePath = dialog.FileName;
                        SaveSettings();
                        MessageBox.Show("DSX path set manually!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;

                case DSXPathPromptWindow.DSXPromptResult.Cancel:
                default:
                    break;
            }
        }

        private string SearchForDSXExecutable()
        {
            string[] commonDirs =
            {
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam", "steamapps", "common"),
    };

            foreach (string dir in commonDirs)
            {
                try
                {
                    var exePaths = Directory.GetFiles(dir, "DSX.exe", SearchOption.AllDirectories);
                    if (exePaths.Length > 0)
                        return exePaths[0];
                }
                catch { }
            }

            return null;
        }
        public string SelectDSXPathManually()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe",
                Title = "Select DualSenseX Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                appSettings.DSXExecutablePath = dialog.FileName;
                SaveSettings();
                return dialog.FileName;
            }

            return null;
        }

        public async Task CheckForUpdatesAsync()
        {
            const string repo = "imjulthej/DSX-Game-Helper-Extended";
            const string releasesUrl = $"https://api.github.com/repos/{repo}/releases/latest";

            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DSXGameHelperExtended");

            try
            {
                var response = await client.GetStringAsync(releasesUrl);
                using var doc = System.Text.Json.JsonDocument.Parse(response);
                string latestVersion = doc.RootElement.GetProperty("tag_name").GetString();
                string currentVersion = VersionHelper.GetAppVersion();

                if (IsNewerVersion(latestVersion, currentVersion) && latestVersion != appSettings.LastNotifiedVersion)
                {
                    appSettings.LastNotifiedVersion = latestVersion;
                    SaveSettings();

                    if (appSettings.NotifyOnUpdate)
                    {
                        pendingNotificationUrl = $"https://github.com/{repo}/releases/latest";
                        ShowNotification("Update Available", $"New version {latestVersion} is available. Click to download.");
                    }
                    else
                    {
                        var result = MessageBox.Show(
                            $"Version {latestVersion} is available. Do you want to open the download page?",
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information
                        );

                        if (result == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = $"https://github.com/{repo}/releases/latest",
                                UseShellExecute = true
                            });
                        }
                    }
                }
                else
                {
                    MessageBox.Show("You are already using the latest version.", "No Update Found", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                if (appSettings.NotifyOnError)
                {
                    ShowNotification("No Releases Found", "No GitHub release was found for this project.");
                }
            }
            catch (Exception ex)
            {
                if (appSettings.NotifyOnError)
                {
                    ShowNotification("Update Check Failed", ex.Message);
                }
            }
        }
        private bool IsNewerVersion(string latest, string current)
        {
            Version latestV = new Version(latest.TrimStart('v'));
            Version currentV = new Version(current);
            return latestV > currentV;
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(appSettings) { Owner = this };
            settingsWindow.ShowDialog();
        }
        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(appSettings)
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
        }
        private void lvGames_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!appSettings.EnableDoubleClickLaunch)
                return;

            if (lvGames.SelectedItem is GameInfo selectedGame && File.Exists(selectedGame.GamePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = selectedGame.GamePath,
                        UseShellExecute = true
                    });

                    UpdateStatus($"Launched: {selectedGame.GameName}");

                    if (appSettings.NotifyOnStart)
                    {
                        ShowNotification("Game Launched", $"{selectedGame.GameName} has been launched.");
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Failed to launch: {ex.Message}");
                    if (appSettings.NotifyOnError)
                    {
                        ShowNotification("Launch Failed", ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("The game's executable could not be found.", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool hasExe = files.Any(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                if (hasExe)
                {
                    e.Effects = DragDropEffects.Copy;

                    if (dropOverlay.Visibility != Visibility.Visible)
                    {
                        dropOverlay.Opacity = 0;
                        dropOverlay.Visibility = Visibility.Visible;

                        var fadeIn = (Storyboard)FindResource("FadeInOverlay");
                        fadeIn.Begin();
                    }

                    return;
                }
            }

            if (dropOverlay.Visibility == Visibility.Visible)
            {
                var fadeOut = (Storyboard)FindResource("FadeOutOverlay");
                fadeOut.Completed += (s, _) =>
                {
                    dropOverlay.Visibility = Visibility.Collapsed;
                    dropOverlay.Opacity = 0;
                };
                fadeOut.Begin();
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] droppedFiles)
            {
                var exeFiles = droppedFiles
                    .Where(path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (exeFiles.Length == 0)
                {
                    MessageBox.Show("Only .exe files are supported.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int addedCount = 0;
                foreach (var path in exeFiles)
                {
                    string name = Path.GetFileNameWithoutExtension(path);

                    if (!gamePaths.Any(g => g.GameName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        var gameInfo = new GameInfo
                        {
                            GamePath = path,
                            GameName = name,
                            IconSource = GetIconFromExePath(path)
                        };

                        gameInfo.PropertyChanged += GameInfo_PropertyChanged;
                        gamePaths.Add(gameInfo);
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    SaveSettings();
                    UpdateStatus($"Added {addedCount} game(s) from drag-and-drop.");
                }
                else
                {
                    UpdateStatus("No new games were added.");
                }
            }
            dropOverlay.Visibility = Visibility.Collapsed;
            var fadeOut = (Storyboard)FindResource("FadeOutOverlay");
            fadeOut.Completed += (s, _) =>
            {
                dropOverlay.Visibility = Visibility.Collapsed;
                dropOverlay.Opacity = 0;
            };
            fadeOut.Begin();
        }
        private void Window_PreviewDragLeave(object sender, DragEventArgs e)
        {
            if (dropOverlay.Visibility == Visibility.Visible)
            {
                var fadeOut = (Storyboard)FindResource("FadeOutOverlay");
                fadeOut.Completed += (s, _) =>
                {
                    dropOverlay.Visibility = Visibility.Collapsed;
                    dropOverlay.Opacity = 0;
                };
                fadeOut.Begin();
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
            HasPromptedForDSXPath = false;
            EnableDoubleClickLaunch = false;
        }
        public string LastUsedDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        public List<GameInfo> GamePaths { get; set; } = new List<GameInfo>();
        public string DSXExecutablePath { get; set; }
        public int DSXVersionIndex { get; set; }
        public int CheckInterval { get; set; } = 1; // Default to 5 seconds
        public bool HasPromptedForDSXPath { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        public bool NotifyOnStart { get; set; } = true;
        public bool NotifyOnStop { get; set; } = true;
        public bool NotifyOnError { get; set; } = true;
        public bool NotifyOnUpdate { get; set; } = true;
        public string LastNotifiedVersion { get; set; } = "";
        public bool EnableDoubleClickLaunch { get; set; } = true;
    }

    public static class StartupHelper
    {
        private const string AppName = "DSXGameHelperExtended";

        public static void SetStartup(bool enable)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (enable)
                    key.SetValue(AppName, $"\"{System.Reflection.Assembly.GetExecutingAssembly().Location}\"");
                else
                    key.DeleteValue(AppName, false);
            }
        }

        public static bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
            {
                return key?.GetValue(AppName) != null;
            }
        }
    }
}

public static class VersionHelper
{
    public static string GetAppVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
    }
}
