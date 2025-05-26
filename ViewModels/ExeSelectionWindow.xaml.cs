using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace DSXGameHelperExtended
{
    public partial class ExeSelectionWindow : Window
    {
        public ObservableCollection<ExeFileItem> ExeFiles { get; set; }

        public ExeSelectionWindow(string[] exePaths)
        {
            InitializeComponent();
            ExeFiles = new ObservableCollection<ExeFileItem>(exePaths.Select(path => new ExeFileItem
            {
                FileName = Path.GetFileName(path),
                FullPath = path,
                IsSelected = true
            }));
            lstExeFiles.ItemsSource = ExeFiles;
        }

        public string[] GetSelectedFiles()
        {
            return ExeFiles.Where(f => f.IsSelected).Select(f => f.FullPath).ToArray();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void chkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var file in ExeFiles)
            {
                file.IsSelected = true;
            }
        }

        private void chkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var file in ExeFiles)
            {
                file.IsSelected = false;
            }
        }
    }

    public class ExeFileItem : INotifyPropertyChanged
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}