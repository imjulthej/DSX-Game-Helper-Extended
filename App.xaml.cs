using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
