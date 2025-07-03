using System;
using System.Windows;

namespace DSXGameHelperExtended
{
    public static class ThemeManager
    {
        private const string LightThemePath = "Themes/LightTheme.xaml";
        private const string DarkThemePath = "Themes/DarkTheme.xaml";

        public static void ApplyTheme(string themeMode)
        {
            string themeToApply = themeMode;
            if (themeMode == "System")
            {
                themeToApply = IsSystemInDarkMode() ? "Dark" : "Light";
            }

            string themePath = themeToApply == "Dark" ? DarkThemePath : LightThemePath;
            var dict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };

            for (int i = Application.Current.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var md = Application.Current.Resources.MergedDictionaries[i];
                if (md.Source != null && (md.Source.OriginalString.Contains("LightTheme.xaml") || md.Source.OriginalString.Contains("DarkTheme.xaml")))
                {
                    Application.Current.Resources.MergedDictionaries.RemoveAt(i);
                }
            }
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        public static bool IsSystemInDarkMode()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value != null && value is int intValue)
                        {
                            return intValue == 0;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static string GetCurrentTheme()
        {
            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                if (dict.Source != null)
                {
                    if (dict.Source.OriginalString.Contains("LightTheme.xaml"))
                        return "Light";
                    if (dict.Source.OriginalString.Contains("DarkTheme.xaml"))
                        return "Dark";
                }
            }
            return "Unknown";
        }
    }
}
