using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;

namespace SmartPins
{
    [SupportedOSPlatform("windows")]
    public partial class SettingsWindow : Window
    {
        private string _pendingHotkey;
        public AppSettings Result { get; private set; }

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();
            Result = new AppSettings
            {
                Hotkey = current.Hotkey,
                HighlightOnlyPinned = current.HighlightOnlyPinned,
                ShowPinIcon = current.ShowPinIcon,
                BlacklistPatterns = new List<string>(current.BlacklistPatterns)
            };
            _pendingHotkey = current.Hotkey;

            HighlightToggle.IsChecked = current.HighlightOnlyPinned;
            PinIconToggle.IsChecked = current.ShowPinIcon;
            HotkeyLabel.Text = current.Hotkey;

            foreach (var p in current.BlacklistPatterns)
                BlacklistBox.Items.Add(p);

            TitleBar.MouseLeftButtonDown += (_, _) => DragMove();
            LoadAppIcon();
        }

        private void LoadAppIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/icon.ico", UriKind.Absolute);
                using var stream = System.Windows.Application.GetResourceStream(uri)!.Stream;
                using var ico = new System.Drawing.Icon(stream, 18, 18);
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    ico.Handle, System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(18, 18));
                src.Freeze();
                AppIcon.Source = src;
            }
            catch { }
        }

        private void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new HotkeyWindow { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedHotkey))
            {
                _pendingHotkey = dlg.SelectedHotkey;
                HotkeyLabel.Text = _pendingHotkey;
            }
        }

        private void AddBlacklist_Click(object sender, RoutedEventArgs e)
        {
            var pattern = BlacklistInput.Text.Trim();
            if (string.IsNullOrEmpty(pattern)) return;
            BlacklistInput.BorderBrush = null;
            if (!BlacklistBox.Items.Contains(pattern))
                BlacklistBox.Items.Add(pattern);
            BlacklistInput.Clear();
        }

        private void BlacklistBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && BlacklistBox.SelectedItem != null)
                BlacklistBox.Items.Remove(BlacklistBox.SelectedItem);
        }

        private void RemoveBlacklist_Click(object sender, RoutedEventArgs e)
        {
            if (BlacklistBox.SelectedItem != null)
                BlacklistBox.Items.Remove(BlacklistBox.SelectedItem);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Result.Hotkey = _pendingHotkey;
            Result.HighlightOnlyPinned = HighlightToggle.IsChecked == true;
            Result.ShowPinIcon = PinIconToggle.IsChecked == true;
            Result.BlacklistPatterns = BlacklistBox.Items.Cast<string>().ToList();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
