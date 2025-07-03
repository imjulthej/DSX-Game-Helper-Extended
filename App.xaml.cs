using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Text.Json;

namespace DSXGameHelperExtended
{
    public partial class App : Application
    {
        internal SplashScreen splashScreen;
        private static Mutex _mutex;
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            const string appName = "DSXGameHelperExtended";
            bool createdNew;
            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("Application is already running!");
                Current.Shutdown();
                return;
            }

            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSXGameHelperExtended");
            string settingsPath = Path.Combine(appDataPath, "settings.json");
            string themeMode = "System";
            if (File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ThemeMode", out var themeProp))
                    {
                        themeMode = themeProp.GetString() ?? "System";
                    }
                }
                catch { }
            }

            ThemeManager.ApplyTheme(themeMode);

            var splash = new SplashScreen();
            splash.Show();

            var mainWindow = new MainWindow();
            mainWindow.Loaded += (s, args) =>
            {
                splash.CloseSplash();
                mainWindow.Show();
            };

            mainWindow.LoadAsync();
        }
    }
}
