using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartPins
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")] private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private const int WM_GETICON = 0x7F;
        private const int GCL_HICON = -14;

        private readonly WindowPinManager _pinManager;
        private readonly DispatcherTimer _refreshTimer;
        private Hotkey? _pinHotkey;
        private List<string> _blacklistPatterns = new();

        public ObservableCollection<WindowInfo> Windows { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _pinManager = App.PinManager;
            _pinManager.WindowPinned += (_, _) => Dispatcher.Invoke(RefreshWindowsList);
            _pinManager.WindowUnpinned += (_, _) => Dispatcher.Invoke(RefreshWindowsList);

            App.MouseHook.MouseClick += OnMouseClick;

            TitleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _refreshTimer.Tick += (_, _) => RefreshWindowsList();
            _refreshTimer.Start();

            LoadAppIcon();
            LoadAndApplySettings();
            RefreshWindowsList();
        }

        private void LoadAppIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/icon.ico", UriKind.Absolute);
                var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
                if (stream == null)
                {
                    var icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "icon.ico");
                    if (!System.IO.File.Exists(icoPath)) return;
                    stream = System.IO.File.OpenRead(icoPath);
                }
                using var ico = new System.Drawing.Icon(stream, 18, 18);
                var src = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(18, 18));
                src.Freeze();
                AppIcon.Source = src;
                stream.Dispose();
            }
            catch { }
        }

        private void LoadAndApplySettings()
        {
            var s = AppSettings.Load();
            ApplyHotkey(s.Hotkey);
            _pinManager.HighlightOnlyPinned = s.HighlightOnlyPinned;
            _pinManager.ShowPinIcon = s.ShowPinIcon;
            UpdateHotkeyBadge(s.Hotkey);
            _blacklistPatterns = s.BlacklistPatterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim().ToLowerInvariant())
                .ToList();
        }

        private void ApplyHotkey(string hotkeyString)
        {
            try
            {
                _pinHotkey?.Dispose();
                var (mod, key) = ParseHotkeyString(hotkeyString);
                _pinHotkey = new Hotkey(mod, key);
                _pinHotkey.Pressed += (_, _) => ToggleActiveWindow();
            }
            catch { }
        }

        private void UpdateHotkeyBadge(string hotkey)
        {
            if (CurrentHotkeyText != null)
                CurrentHotkeyText.Text = hotkey;
        }

        private void RefreshWindowsList()
        {
            var myHandle = new WindowInteropHelper(this).Handle;
            Windows.Clear();
            int pinned = 0, visible = 0;

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                int len = GetWindowTextLength(hWnd);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;
                if (hWnd == myHandle) return true;
                if (SystemWindowFilter.IsSystemWindow(hWnd)) return true;

                string processName = NativeMethods.GetProcessName(hWnd);
                if (_blacklistPatterns.Any(p => processName.Contains(p, StringComparison.OrdinalIgnoreCase))) return true;

                bool isPinned = _pinManager.IsWindowPinned(hWnd);
                if (isPinned) pinned++;
                visible++;

                var green = new SolidColorBrush(Color.FromRgb(0x39, 0xFF, 0x14));
                var dimColor = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
                var textColor = new SolidColorBrush(isPinned
                    ? Color.FromRgb(0xCC, 0xFF, 0xCC)
                    : Color.FromRgb(0xCC, 0xCC, 0xCC));

                Windows.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    Icon = GetWindowIcon(hWnd),
                    IsPinned = isPinned,
                    PinButtonText = isPinned ? "off" : "pin",
                    PinButtonIcon = isPinned ? "PinOff" : "Pin",
                    PinIndicatorIcon = isPinned ? "Pin" : "CircleOutline",
                    PinColor = isPinned ? green : dimColor,
                    TitleColor = textColor,
                    PinCommand = new RelayCommand(() => TogglePin(hWnd))
                });
                return true;
            }, IntPtr.Zero);

            PinnedWindowsCount.Text = pinned.ToString();
            VisibleWindowsCount.Text = visible.ToString();
        }

        private void TogglePin(IntPtr hWnd)
        {
            if (_pinManager.IsWindowPinned(hWnd))
                _pinManager.UnpinWindow(hWnd);
            else
                _pinManager.PinWindow(hWnd);
        }

        private ImageSource? GetWindowIcon(IntPtr hWnd)
        {
            try
            {
                IntPtr hIcon = SendMessage(hWnd, WM_GETICON, 1, 0);
                if (hIcon == IntPtr.Zero) hIcon = GetClassLongPtr(hWnd, GCL_HICON);
                if (hIcon == IntPtr.Zero) return null;
                var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(16, 16));
                src.Freeze();
                return src;
            }
            catch { return null; }
        }

        public void ToggleActiveWindow()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || hwnd == new WindowInteropHelper(this).Handle) return;

            if (_pinManager.IsWindowPinned(hwnd))
                _pinManager.UnpinWindow(hwnd);
            else
                _pinManager.PinWindow(hwnd);
        }

        private void OnMouseClick(object? sender, MouseClickEventArgs e)
        {
            if (_pinManager.IsPinMode)
                _pinManager.HandleMouseClick(e.WindowHandle);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject src)
            {
                var p = src;
                while (p != null) { if (p is System.Windows.Controls.Button) return; p = VisualTreeHelper.GetParent(p); }
            }
            DragMove();
        }

        private void TrayPinMode_Click(object sender, RoutedEventArgs e) => _pinManager.IsPinMode = true;
        private void PinActiveWindow_Click(object sender, RoutedEventArgs e) => ToggleActiveWindow();
        private void PinWithCursor_Click(object sender, RoutedEventArgs e) => _pinManager.IsPinMode = true;
        private void UnpinAll_Click(object sender, RoutedEventArgs e) => _pinManager.UnpinAllWindows();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => HideToTray();

        private void HideToTray()
        {
            ShowInTaskbar = false;
            Hide();
        }

        public void ShowFromTray()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        public void OpenSettings() => SettingsButton_Click(this, new RoutedEventArgs());

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var current = AppSettings.Load();
            var dlg = new SettingsWindow(current) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            var s = dlg.Result;
            s.Save();
            _pinManager.HighlightOnlyPinned = s.HighlightOnlyPinned;
            _pinManager.ShowPinIcon = s.ShowPinIcon;
            _blacklistPatterns = s.BlacklistPatterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim().ToLowerInvariant())
                .ToList();
            ApplyHotkey(s.Hotkey);
            UpdateHotkeyBadge(s.Hotkey);
            FlashStatus($"// settings saved — hotkey: {s.Hotkey}");
        }

        private void AddToBlacklist_Click(object sender, RoutedEventArgs e)
        {
            if (WindowListBox.SelectedItem is not WindowInfo info) return;
            string processName = NativeMethods.GetProcessName(info.Handle);
            if (string.IsNullOrWhiteSpace(processName)) return;

            var s = AppSettings.Load();
            if (!s.BlacklistPatterns.Any(p => p.Equals(processName, StringComparison.OrdinalIgnoreCase)))
            {
                s.BlacklistPatterns.Add(processName);
                s.Save();
                _blacklistPatterns = s.BlacklistPatterns
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim().ToLowerInvariant())
                    .ToList();
                RefreshWindowsList();
                FlashStatus($"// blacklist: {processName}");
            }
        }

        private void FlashStatus(string msg)
        {
            StatusText.Text = msg;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (_, _) => { StatusText.Text = ""; t.Stop(); };
            t.Start();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _refreshTimer.Stop();
            _pinHotkey?.Dispose();
            _pinManager.UnpinAllWindows();
            base.OnClosing(e);
        }

        internal static (ModifierKeys modifiers, Key key) ParseHotkeyString(string s)
        {
            var parts = s.Split('+');
            var mod = ModifierKeys.None;
            var key = Key.None;
            foreach (var p in parts)
            {
                switch (p.Trim().ToUpperInvariant())
                {
                    case "CTRL": mod |= ModifierKeys.Control; break;
                    case "ALT": mod |= ModifierKeys.Alt; break;
                    case "SHIFT": mod |= ModifierKeys.Shift; break;
                    case "WIN": mod |= ModifierKeys.Windows; break;
                    default:
                        if (Enum.TryParse<Key>(p.Trim(), true, out var k)) key = k;
                        break;
                }
            }
            return (mod, key);
        }
    }

    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = "";
        public ImageSource? Icon { get; set; }
        public bool IsPinned { get; set; }
        public string PinButtonText { get; set; } = "pin";
        public string PinButtonIcon { get; set; } = "Pin";
        public string PinIndicatorIcon { get; set; } = "CircleOutline";
        public Brush PinColor { get; set; } = Brushes.Gray;
        public Brush TitleColor { get; set; } = Brushes.LightGray;
        public ICommand? PinCommand { get; set; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _exec;
        public RelayCommand(Action exec) => _exec = exec;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? _) => true;
        public void Execute(object? _) => _exec();
    }
}
