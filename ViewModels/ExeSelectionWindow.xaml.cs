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
                IsSelected = false
            }));
            foreach (var item in ExeFiles)
            {
                item.PropertyChanged += ExeFileItem_PropertyChanged;
            }
            lstExeFiles.ItemsSource = ExeFiles;
            UpdateOkButtonState();
        }
        private void ExeFileItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExeFileItem.IsSelected))
            {
                UpdateOkButtonState();
            }
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
            UpdateOkButtonState();
        }

        private void chkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var file in ExeFiles)
            {
                file.IsSelected = false;
            }
            UpdateOkButtonState();
        }
        private void UpdateOkButtonState()
        {
            btnOK.IsEnabled = ExeFiles.Any(f => f.IsSelected);
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
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}