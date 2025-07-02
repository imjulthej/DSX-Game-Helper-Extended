using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DSXGameHelperExtended
{
    public partial class MainWindow : INotifyPropertyChanged
    {
        private BackgroundWorker loadingWorker;
        private int totalGamesToLoad;
        private int gamesLoaded;
        private ObservableCollection<GameInfo> gamePaths;
        private Timer processCheckTimer;
        private int checkInterval;
        private static readonly string AppDataFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DSXGameHelperExtended"
        );
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolderPath, "settings.json");
        private bool _hasSelectedGames;
        public bool HasSelectedGames
        {
            get => _hasSelectedGames;
            set
            {
                if (_hasSelectedGames != value)
                {
                    _hasSelectedGames = value;
                    OnPropertyChanged(nameof(HasSelectedGames));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private Settings appSettings;

        public string dsxExecutablePath { get; private set; }
        private TaskbarIcon taskbarIcon;
        private readonly Uri iconIdle = new("pack://application:,,,/Assets/controller.ico");
        private readonly Uri iconRunning = new("pack://application:,,,/Assets/icon_running.ico");
        private readonly Uri iconError = new("pack://application:,,,/Assets/icon_error.ico");

        private void SetTrayIcon(Uri iconUri)
        {
            if (taskbarIcon != null)
            {
                taskbarIcon.IconSource = new BitmapImage(iconUri);
            }
        }

        public MainWindow()
        {
            ShowInTaskbar = false;
            Visibility = Visibility.Hidden;
            InitializeComponent();
            DataContext = this;
            gamePaths = new ObservableCollection<GameInfo>();
            lvGames.ItemsSource = gamePaths;
            InitializeLoadingWorker();
            appSettings = LoadSettings();

            if (appSettings.StartWithWindows != StartupHelper.IsStartupEnabled())
            {
                StartupHelper.SetStartup(appSettings.StartWithWindows);
            }

            if (appSettings.StartMinimized)
            {
                WindowState = WindowState.Minimized;
                Hide();
            }

            if (appSettings.GamePaths != null)
            {
                var validGames = appSettings.GamePaths
                    .Where(g => g != null && !string.IsNullOrWhiteSpace(g.GamePath))
                    .ToList();

                foreach (var game in validGames)
                {
                    try
                    {
                        if (!gamePaths.Any(g =>
                            string.Equals(g.GamePath, game.GamePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            LoadGameIcon(game);
                            game.PropertyChanged += GameInfo_PropertyChanged;
                            gamePaths.Add(game);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading game {game.GamePath}: {ex.Message}");
                    }
                }
            }
            chkSelectAll.IsChecked = false;

            InitializeCollectionView();
            InitializeTimer();
            UpdateStatus("Ready. No game running.");
        }
        private void InitializeLoadingWorker()
        {
            loadingWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = false
            };

            loadingWorker.DoWork += LoadingWorker_DoWork;
            loadingWorker.ProgressChanged += LoadingWorker_ProgressChanged;
            loadingWorker.RunWorkerCompleted += LoadingWorker_RunWorkerCompleted;
        }

        public void LoadAsync()
        {
            loadingWorker.RunWorkerAsync();
        }

        private void LoadingWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            loadingWorker.ReportProgress(0, "Loading settings...");

            if (appSettings.StartWithWindows != StartupHelper.IsStartupEnabled())
            {
                StartupHelper.SetStartup(appSettings.StartWithWindows);
            }

            loadingWorker.ReportProgress(10, "Initializing game list...");

            totalGamesToLoad = appSettings.GamePaths?.Count ?? 0;
            gamesLoaded = 0;

            if (appSettings.GamePaths != null)
            {
                int batchSize = 10;
                for (int i = 0; i < appSettings.GamePaths.Count; i += batchSize)
                {
                    var batch = appSettings.GamePaths.Skip(i).Take(batchSize).ToList();
                    Dispatcher.Invoke(() => LoadGameBatch(batch));

                    gamesLoaded += batch.Count;
                    int progress = 10 + (int)((double)gamesLoaded / totalGamesToLoad * 80);
                    loadingWorker.ReportProgress(progress, $"Loading games... ({gamesLoaded}/{totalGamesToLoad})");

                    Thread.Sleep(50);
                }
            }

            loadingWorker.ReportProgress(95, "Finalizing...");
        }

        private void LoadGameBatch(List<GameInfo> batch)
        {
            foreach (var game in batch)
            {
                var existingGame = gamePaths.FirstOrDefault(g =>
                    g.GamePath.Equals(game.GamePath, StringComparison.OrdinalIgnoreCase));

                if (existingGame == null)
                {
                    LoadGameIcon(game);
                    game.PropertyChanged += GameInfo_PropertyChanged;
                    Dispatcher.Invoke(() => gamePaths.Add(game));
                }
                else
                {
                    existingGame.GameName = game.GameName;
                    existingGame.CustomIconPath = game.CustomIconPath;
                    if (existingGame.IconSource == null)
                    {
                        LoadGameIcon(existingGame);
                    }
                }
            }
        }

        private void LoadingWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var splash = (Application.Current as App)?.splashScreen;
            if (splash != null)
            {
                splash.UpdateProgress(e.ProgressPercentage, e.UserState as string);
            }
        }

        private void LoadingWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.ShowInTaskbar = true;
            this.Visibility = Visibility.Visible;
            InitializeCollectionView();
            UpdateSelectedGamesStatus();
            InitializeTimer();
            UpdateStatus("Ready. No game running.");

            taskbarIcon = new TaskbarIcon();
            SetTrayIcon(iconIdle);
            taskbarIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/controller.ico"));
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

            if (!appSettings.StartMinimized)
            {
                this.Visibility = Visibility.Visible;
            }
            else
            {
                WindowState = WindowState.Minimized;
                Hide();
            }
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
                    return new Settings();
                }

                if (!File.Exists(SettingsFilePath))
                {
                    return new Settings();
                }

                string settingsJson = File.ReadAllText(SettingsFilePath);
                if (string.IsNullOrWhiteSpace(settingsJson))
                {
                    return new Settings();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    Converters = { new GameInfoConverter() }
                };

                var settings = JsonSerializer.Deserialize<Settings>(settingsJson, options) ?? new Settings();

                if (settings.GamePaths != null)
                {
                    foreach (var game in settings.GamePaths)
                    {
                        game.IsSelected = false;
                    }

                    settings.GamePaths = settings.GamePaths
                        .Where(g => g != null && !string.IsNullOrWhiteSpace(g.GamePath))
                        .ToList();
                }

                return settings;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading settings: {ex.Message}");
                return new Settings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                appSettings.GamePaths = gamePaths
                    .Where(g => g != null && !string.IsNullOrWhiteSpace(g.GamePath))
                    .ToList();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new GameInfoConverter() }
                };

                string settingsJson = JsonSerializer.Serialize(appSettings, options);

                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath));

                string tempPath = Path.Combine(AppDataFolderPath, "temp_settings.json");
                File.WriteAllText(tempPath, settingsJson);

                File.Delete(SettingsFilePath);
                File.Move(tempPath, SettingsFilePath);
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
        public void UpdateSelectedGamesStatus()
        {
            HasSelectedGames = gamePaths?.Any(g => g.IsSelected) ?? false;
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
                    GameName = Path.GetFileNameWithoutExtension(openFileDialog.FileName),
                    IsSelected = false,
                };

                gameInfo.IconSource = GetIconFromExePath(gameInfo.GamePath);
                gameInfo.PropertyChanged += GameInfo_PropertyChanged;

                gamePaths.Add(gameInfo);
                SaveSettings();
                UpdateStatus($"Game added: {gameInfo.GameName}");
            }
        }

        private void btnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = gamePaths.Where(g => g.IsSelected).ToList();

            string confirmationMessage;
            if (selectedGames.Count == 1)
            {
                confirmationMessage = $"Are you sure you want to remove {selectedGames[0].GameName}?";
            }
            else
            {
                confirmationMessage = $"Are you sure you want to remove {selectedGames.Count} games?";
            }

            if (MessageBox.Show(confirmationMessage, "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                foreach (var game in selectedGames)
                {
                    gamePaths.Remove(game);
                }

                SaveSettings();
                UpdateSelectedGamesStatus();

                if (selectedGames.Count == 1)
                {
                    UpdateStatus($"Game removed: {selectedGames[0].GameName}");
                }
                else
                {
                    UpdateStatus($"{selectedGames.Count} games removed.");
                }

                chkSelectAll.IsChecked = false;
            }
        }

        private void chkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var game in gamePaths)
            {
                game.IsSelected = true;
            }
            UpdateSelectedGamesStatus();
        }

        private void chkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var game in gamePaths)
            {
                game.IsSelected = false;
            }
            UpdateSelectedGamesStatus();
        }

        private void CheckRunningGames(object state)
        {
            try
            {
                var runningProcesses = Process.GetProcesses();
                var runningProcessNames = runningProcesses.Select(p => p.ProcessName.ToLower()).ToList();

                bool anyGameRunning = false;
                string runningGameName = null;

                foreach (var gameInfo in gamePaths)
                {
                    string gameProcessName = Path.GetFileNameWithoutExtension(gameInfo.GamePath).ToLower();

                    if (runningProcessNames.Contains(gameProcessName))
                    {
                        anyGameRunning = true;
                        runningGameName = gameInfo.GameName;
                        break;
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    if (anyGameRunning)
                    {
                        SetTrayIcon(iconRunning);
                        UpdateStatus($"Game detected: {runningGameName}", isLeft: true);
                        EnsureDSXIsRunning();
                    }
                    else
                    {
                        EnsureDSXIsNotRunning();
                        SetTrayIcon(iconIdle);
                        UpdateStatus("No game running.", isLeft: true);
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"Error checking games: {ex.Message}", isLeft: true);
                    SetTrayIcon(iconError);
                });
            }
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

            Process.Start(appSettings.DSXExecutablePath);
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
                            IconSource = GetIconFromExePath(exePath),
                            IsSelected = false
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

        public static ImageSource GetIconFromExePath(string exePath)
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
            if (e.PropertyName == nameof(GameInfo.IsSelected))
            {
                UpdateSelectedGamesStatus();
            }
            SaveSettings();
        }
        private void lvGames_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (lvGames.SelectedItem is GameInfo selectedGame)
            {
                ContextMenu contextMenu = new ContextMenu();

                MenuItem editItem = new MenuItem { Header = "Edit Game Name" };
                editItem.Click += (s, args) => OpenEditNameDialog(selectedGame);

                MenuItem changeIconItem = new MenuItem
                {
                    Header = "Change Icon",
                    DataContext = selectedGame
                };
                changeIconItem.Click += ChangeIcon_Click;

                MenuItem resetIconItem = new MenuItem
                {
                    Header = "Reset Icon",
                    DataContext = selectedGame
                };
                resetIconItem.Click += ResetIcon_Click;

                contextMenu.Items.Add(editItem);
                contextMenu.Items.Add(changeIconItem);
                contextMenu.Items.Add(resetIconItem);

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
                if (!appSettings.SkipLaunchConfirmation)
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to launch {selectedGame.GameName}?",
                        "Confirm Launch",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (result != MessageBoxResult.Yes)
                    {
                        UpdateStatus("Launch canceled by user.");
                        return;
                    }
                }

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
                            IconSource = GetIconFromExePath(path),
                            IsSelected = false
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

        private bool isAscending = true;
        private ICollectionView gamePathsView;
        private string currentSearchText = "";

        private void InitializeCollectionView()
        {
            if (lvGames.ItemsSource != null)
            {
                gamePathsView = CollectionViewSource.GetDefaultView(lvGames.ItemsSource);
                gamePathsView.Filter = GameFilter;
            }
        }

        private bool GameFilter(object item)
        {
            if (string.IsNullOrEmpty(currentSearchText))
                return true;

            return item is GameInfo game &&
                   game.GameName.Contains(currentSearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            currentSearchText = txtSearch.Text;
            if (gamePathsView == null)
            {
                InitializeCollectionView();
            }
            gamePathsView?.Refresh();
        }

        private void GameName_HeaderClick(object sender, RoutedEventArgs e)
        {
            isAscending = !isAscending;

            ICollectionView view = CollectionViewSource.GetDefaultView(lvGames.ItemsSource);
            view.SortDescriptions.Clear();

            view.SortDescriptions.Add(new SortDescription("GameName",
                isAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
        }
        public static ImageSource LoadImageFromFile(string filePath)
        {
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(filePath);
                image.EndInit();
                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }
        private void ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is GameInfo gameInfo)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image files (*.ico;*.png;*.jpg)|*.ico;*.png;*.jpg|All files (*.*)|*.*",
                    Title = "Select new icon"
                };

                if (dialog.ShowDialog() == true)
                {
                    var newIcon = LoadImageFromFile(dialog.FileName);
                    if (newIcon != null)
                    {
                        gameInfo.CustomIconPath = dialog.FileName;
                        gameInfo.IconSource = newIcon;
                        SaveSettings();
                        UpdateStatus($"Icon changed for {gameInfo.GameName}");
                    }
                    else
                    {
                        MessageBox.Show("Failed to load the selected image.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void LoadGameIcon(GameInfo gameInfo)
        {
            if (!string.IsNullOrEmpty(gameInfo.CustomIconPath) && File.Exists(gameInfo.CustomIconPath))
            {
                var customIcon = LoadImageFromFile(gameInfo.CustomIconPath);
                if (customIcon != null)
                {
                    gameInfo.IconSource = customIcon;
                    return;
                }
            }
            
            gameInfo.IconSource = GetIconFromExePath(gameInfo.GamePath);
        }
        private void ResetIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is GameInfo gameInfo)
            {
                gameInfo.CustomIconPath = null;
                gameInfo.IconSource = GetIconFromExePath(gameInfo.GamePath);
                SaveSettings();
                UpdateStatus($"Icon reset for {gameInfo.GameName}");
            }
        }
        private void PrioritizeVisibleItems()
        {
            if (lvGames.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                Dispatcher.BeginInvoke(new Action(PrioritizeVisibleItems), DispatcherPriority.Background);
                return;
            }

            var scrollViewer = FindVisualChild<ScrollViewer>(lvGames);
            if (scrollViewer == null) return;

            double viewportHeight = scrollViewer.ViewportHeight;
            double verticalOffset = scrollViewer.VerticalOffset;

            int firstVisibleIndex = (int)(verticalOffset);
            int lastVisibleIndex = (int)(verticalOffset + viewportHeight);

            for (int i = firstVisibleIndex; i <= lastVisibleIndex && i < gamePaths.Count; i++)
            {
                var game = gamePaths[i];
                if (game.IconSource == null)
                {
                    game.LoadIcon();
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
        protected override void OnClosed(EventArgs e)
        {
            taskbarIcon?.Dispose();
            base.OnClosed(e);
        }
    }
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            _execute();
        }
    }
    public class GameInfoConverter : JsonConverter<GameInfo>
    {
        public override GameInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                var root = doc.RootElement;
                var game = new GameInfo();

                if (root.TryGetProperty("gameName", out var name))
                    game.GameName = name.GetString() ?? string.Empty;

                if (root.TryGetProperty("gamePath", out var path))
                    game.GamePath = path.GetString() ?? string.Empty;

                if (root.TryGetProperty("customIconPath", out var iconPath))
                    game.CustomIconPath = iconPath.GetString() ?? string.Empty;

                if (root.TryGetProperty("isSelected", out var selected))
                    game.IsSelected = selected.GetBoolean();

                return game;
            }
        }

        public override void Write(Utf8JsonWriter writer, GameInfo value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("gameName", value.GameName);
            writer.WriteString("gamePath", value.GamePath);
            writer.WriteString("customIconPath", value.CustomIconPath);
            writer.WriteBoolean("isSelected", value.IsSelected);
            writer.WriteEndObject();
        }
    }
    public class GameInfo : INotifyPropertyChanged
    {
        private string _gameName = string.Empty;
        private string _gamePath = string.Empty;
        private string _customIconPath = string.Empty;
        private bool _isSelected = false;
        private ImageSource _iconSource;
        private bool _isIconLoaded;

        [JsonPropertyName("gameName")]
        public string GameName
        {
            get => _gameName;
            set
            {
                if (_gameName != value)
                {
                    _gameName = value ?? string.Empty;
                    OnPropertyChanged(nameof(GameName));
                    AutoSave();
                }
            }
        }

        [JsonPropertyName("gamePath")]
        public string GamePath
        {
            get => _gamePath;
            set
            {
                if (_gamePath != value)
                {
                    _gamePath = value ?? string.Empty;
                    OnPropertyChanged(nameof(GamePath));
                    AutoSave();
                }
            }
        }

        public string CustomIconPath
        {
            get => _customIconPath;
            set
            {
                if (_customIconPath != value)
                {
                    _customIconPath = value;
                    OnPropertyChanged(nameof(CustomIconPath));
                    AutoSave();
                }
            }
        }

        [JsonIgnore]
        public ImageSource IconSource
        {
            get
            {
                if (!_isIconLoaded && !string.IsNullOrEmpty(GamePath))
                {
                    _isIconLoaded = true;
                    Task.Run(() => LoadIcon());
                }
                return _iconSource;
            }
            set
            {
                if (_iconSource != value)
                {
                    _iconSource = value;
                    OnPropertyChanged(nameof(IconSource));
                }
            }
        }
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        mainWindow?.UpdateSelectedGamesStatus();
                    });
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

        public void LoadIcon()
        {
            if (_isIconLoaded) return;

            _isIconLoaded = true;
            Task.Run(() =>
            {
                if (!string.IsNullOrEmpty(CustomIconPath) && File.Exists(CustomIconPath))
                {
                    var customIcon = MainWindow.LoadImageFromFile(CustomIconPath);
                    if (customIcon != null)
                    {
                        Application.Current.Dispatcher.Invoke(() => IconSource = customIcon);
                        return;
                    }
                }

                var icon = MainWindow.GetIconFromExePath(GamePath);
                Application.Current.Dispatcher.Invoke(() => IconSource = icon);
            });
        }

        public override bool Equals(object obj)
        {
            return obj is GameInfo other &&
                   string.Equals(GamePath, other.GamePath, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(GamePath);
        }
        [JsonConstructor]
        public GameInfo()
        {
            _gameName = string.Empty;
            _gamePath = string.Empty;
            _customIconPath = string.Empty;
        }
    }

    public class Settings
    {
        public Settings()
        {
            CheckInterval = 1;
            HasPromptedForDSXPath = false;
            EnableDoubleClickLaunch = false;
            NotifyOnStart = false;
            NotifyOnStop = false;
            NotifyOnError = false;
            NotifyOnUpdate = false;
        }
        public string LastUsedDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        public List<GameInfo> GamePaths { get; set; } = new List<GameInfo>();
        public string DSXExecutablePath { get; set; }
        public int CheckInterval { get; set; } = 1;
        public bool HasPromptedForDSXPath { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        public bool NotifyOnStart { get; set; } = true;
        public bool NotifyOnStop { get; set; } = true;
        public bool NotifyOnError { get; set; } = true;
        public bool NotifyOnUpdate { get; set; } = true;
        public string LastNotifiedVersion { get; set; } = "";
        public bool EnableDoubleClickLaunch { get; set; } = true;
        public bool SkipLaunchConfirmation { get; set; } = false;
    }

    public static class StartupHelper
    {
        private const string AppName = "DSXGameHelperExtended";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (enable)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to set startup: {ex.Message}", "Startup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return key?.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
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
