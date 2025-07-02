using System.Reflection;
using System.Windows;
using System.Windows.Media.Animation;

namespace DSXGameHelperExtended
{
    public partial class SplashScreen : Window
    {
        public string VersionInfo { get; } = GetVersionInfo();

        public SplashScreen()
        {
            InitializeComponent();
            DataContext = this;
        }

        private static string GetVersionInfo()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"v{version.Major}.{version.Minor}.{version.Build}";
        }

        public void UpdateProgress(int progress, string status)
        {
            Dispatcher.Invoke(() =>
            {
                if (!progressBar.IsIndeterminate)
                {
                    progressBar.Value = progress;
                }
                statusText.Text = status;
            });
        }
        public void CloseSplash()
        {
            var fadeOut = this.FindResource("FadeOutAnimation") as Storyboard;

            if (fadeOut != null)
            {
                fadeOut.Completed += (s, _) =>
                {
                    this.Close();
                };
                fadeOut.Begin(this);
            }
            else
            {
                this.Close();
            }
        }
    }
}