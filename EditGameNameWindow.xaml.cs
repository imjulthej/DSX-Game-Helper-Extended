using System.Windows;
using System.Windows.Input;

namespace DSXGameHelperExtended
{
    public partial class EditGameNameWindow : Window
    {
        public string EditedName { get; private set; }

        public EditGameNameWindow(string currentName)
        {
            InitializeComponent();
            txtGameName.Text = currentName;
            txtGameName.Focus();
            txtGameName.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            EditedName = txtGameName.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void txtGameName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Ok_Click(sender, e);
            }
        }
    }
}