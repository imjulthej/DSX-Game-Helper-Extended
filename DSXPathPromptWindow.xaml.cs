using System.Windows;

namespace DSXGameHelperExtended
{
    public partial class DSXPathPromptWindow : Window
    {
        public enum DSXPromptResult { Auto, Manual, Cancel }
        public DSXPromptResult Result { get; private set; } = DSXPromptResult.Cancel;

        public DSXPathPromptWindow()
        {
            InitializeComponent();
        }

        private void Auto_Click(object sender, RoutedEventArgs e)
        {
            Result = DSXPromptResult.Auto;
            DialogResult = true;
            Close();
        }

        private void Manual_Click(object sender, RoutedEventArgs e)
        {
            Result = DSXPromptResult.Manual;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = DSXPromptResult.Cancel;
            DialogResult = false;
            Close();
        }
    }
}