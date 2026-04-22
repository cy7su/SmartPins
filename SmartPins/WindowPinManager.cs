using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartPins
{
    [SupportedOSPlatform("windows")]
    public class WindowPinManager : IDisposable
    {
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);
        [DllImport("user32.dll")] private static extern IntPtr CreateIconIndirect(ref ICONINFO info);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] private static extern bool SetSystemCursor(IntPtr hcur, uint id);
        [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO { public bool fIcon; public int xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint OCR_NORMAL = 32512;
        private const uint SPI_SETCURSORS = 0x0057;

        private readonly Dictionary<IntPtr, bool> _pinned = new();
        private readonly Dictionary<IntPtr, PinIndicatorWindow> _indicators = new();
        private readonly Dictionary<IntPtr, PinIconOverlay> _iconOverlays = new();
        private bool _isPinMode;

        public event EventHandler<WindowPinEventArgs>? WindowPinned;
        public event EventHandler<WindowPinEventArgs>? WindowUnpinned;

        public bool HighlightOnlyPinned { get; set; } = false;
        public bool ShowPinIcon { get; set; } = false;

        public bool IsPinMode
        {
            get => _isPinMode;
            set
            {
                _isPinMode = value;
                if (_isPinMode) EnableCursorMode();
                else DisableCursorMode();
            }
        }

        public void PinWindow(IntPtr hwnd)
        {
            if (_pinned.ContainsKey(hwnd)) return;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            _pinned[hwnd] = true;
            if (HighlightOnlyPinned) AddIndicator(hwnd);
            if (ShowPinIcon) AddIconOverlay(hwnd);
            WindowPinned?.Invoke(this, new WindowPinEventArgs(hwnd));
        }

        public void UnpinWindow(IntPtr hwnd)
        {
            if (!_pinned.ContainsKey(hwnd)) return;
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            RemoveIndicator(hwnd);
            RemoveIconOverlay(hwnd);
            _pinned.Remove(hwnd);
            WindowUnpinned?.Invoke(this, new WindowPinEventArgs(hwnd));
        }

        public bool IsWindowPinned(IntPtr hwnd) => _pinned.ContainsKey(hwnd);
        public IEnumerable<IntPtr> GetPinnedWindows() => _pinned.Keys;

        public void UnpinAllWindows()
        {
            foreach (var hwnd in new List<IntPtr>(_pinned.Keys))
                UnpinWindow(hwnd);
        }

        public void RemoveAllPinIndicators()
        {
            foreach (var hwnd in new List<IntPtr>(_indicators.Keys))
                RemoveIndicator(hwnd);
            foreach (var hwnd in new List<IntPtr>(_iconOverlays.Keys))
                RemoveIconOverlay(hwnd);
        }

        private void AddIndicator(IntPtr hwnd)
        {
            if (_indicators.ContainsKey(hwnd)) return;
            var ind = new PinIndicatorWindow(hwnd);
            _indicators[hwnd] = ind;
            ind.Show();
        }

        private void RemoveIndicator(IntPtr hwnd)
        {
            if (_indicators.TryGetValue(hwnd, out var ind))
            {
                try { ind.Close(); } catch { }
                _indicators.Remove(hwnd);
            }
        }

        private void AddIconOverlay(IntPtr hwnd)
        {
            if (_iconOverlays.ContainsKey(hwnd)) return;
            var ov = new PinIconOverlay(hwnd);
            _iconOverlays[hwnd] = ov;
            ov.Show();
        }

        private void RemoveIconOverlay(IntPtr hwnd)
        {
            if (_iconOverlays.TryGetValue(hwnd, out var ov))
            {
                try { ov.Close(); } catch { }
                _iconOverlays.Remove(hwnd);
            }
        }

        public void HandleMouseClick(IntPtr hwnd)
        {
            if (!IsPinMode) return;
            IsPinMode = false;

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;

            var mainHwnd = new WindowInteropHelper(System.Windows.Application.Current.MainWindow!).Handle;
            if (hwnd == mainHwnd) return;

            if (SystemWindowFilter.IsSystemWindow(hwnd)) return;

            if (IsWindowPinned(hwnd)) UnpinWindow(hwnd);
            else PinWindow(hwnd);
        }

        private void EnableCursorMode()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/pin.ico", UriKind.Absolute);
                using var stream = System.Windows.Application.GetResourceStream(uri)!.Stream;
                using var ico = new Icon(stream, 32, 32);
                using var src = ico.ToBitmap();

                const int size = 16;
                using var rotated = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(rotated))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.TranslateTransform(size / 2f, size / 2f);
                    g.RotateTransform(90f);
                    g.DrawImage(src, -size / 2f, -size / 2f, size, size);
                }

                IntPtr hIcon = rotated.GetHicon();
                GetIconInfo(hIcon, out var ii);
                ii.fIcon = false;
                ii.xHotspot = 0;
                ii.yHotspot = 0;
                IntPtr hCursor = CreateIconIndirect(ref ii);
                DeleteObject(ii.hbmMask);
                DeleteObject(ii.hbmColor);
                DestroyIcon(hIcon);

                if (hCursor != IntPtr.Zero)
                    SetSystemCursor(hCursor, OCR_NORMAL);
            }
            catch { }
        }

        [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
        [DllImport("user32.dll")] private static extern IntPtr CopyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int cx, int cy, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, IntPtr lpvBits);

        private const int IDC_ARROW = 32512;
        private const uint DI_NORMAL = 0x3;

        private static IntPtr ComposeCursor(IntPtr hArrow, IntPtr hPin)
        {
            const int sz = 48;
            using var bmp = new Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                IntPtr hdc = g.GetHdc();
                DrawIconEx(hdc, 0, 0, hArrow, 32, 32, 0, IntPtr.Zero, DI_NORMAL);
                DrawIconEx(hdc, 18, 18, hPin, 20, 20, 0, IntPtr.Zero, DI_NORMAL);
                g.ReleaseHdc(hdc);
            }

            IntPtr hIcon = bmp.GetHicon();
            GetIconInfo(hIcon, out var ii);
            ii.fIcon = false;
            ii.xHotspot = 0;
            ii.yHotspot = 0;
            IntPtr hCursor = CreateIconIndirect(ref ii);
            DeleteObject(ii.hbmMask);
            DeleteObject(ii.hbmColor);
            DestroyIcon(hIcon);
            return hCursor;
        }

        private void DisableCursorMode()
        {
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        }

        private static IntPtr CreatePinIconHandle(int size = 32)
        {
            try
            {
                var uri = new Uri("pack://application:,,,/pin.ico", UriKind.Absolute);
                using var stream = System.Windows.Application.GetResourceStream(uri)!.Stream;
                using var ico = new Icon(stream, size, size);
                return ico.ToBitmap().GetHicon();
            }
            catch { return IntPtr.Zero; }
        }

        public void Dispose()
        {
            DisableCursorMode();
            foreach (var ind in _indicators.Values) try { ind.Close(); } catch { }
            _indicators.Clear();
            foreach (var ov in _iconOverlays.Values) try { ov.Close(); } catch { }
            _iconOverlays.Clear();
        }
    }

    internal static class SystemWindowFilter
    {
        [DllImport("user32.dll")] private static extern int GetClassName(IntPtr hWnd, StringBuilder cls, int max);

        private static readonly HashSet<string> BlockedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "textinputhost",                    "shellexperiencehost",              "startmenuexperiencehost",
            "searchhost",                       "searchui",
            "lockapp",
            "screensketch",
            "nvcontainer",                      "nvdisplay.container",
            "nvcplui",
            "rtkuwp",                           "applicationframehost",         };

        private static readonly HashSet<string> BlockedClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "shell_traywnd",                    "progman",                          "workerw",                          "windows.ui.core.corewindow",             "toplevelhwnd",
        };

        public static bool IsSystemWindow(IntPtr hwnd)
        {
            string title = NativeMethods.GetWindowTitle(hwnd);
            string process = NativeMethods.GetProcessName(hwnd);

            if (title == "Program Manager") return true;

            if (BlockedProcesses.Contains(process)) return true;

            var cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);
            if (BlockedClasses.Contains(cls.ToString())) return true;

            if (process.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                var c = cls.ToString();
                if (c is "Shell_TrayWnd" or "Progman" or "WorkerW" or "NotifyIconOverflowWindow")
                    return true;
            }

            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public class PinIndicatorWindow : Window
    {
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newVal);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_NOACTIVATE = 0x8000000;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new(-1);

        private readonly IntPtr _target;
        private readonly DispatcherTimer _sync;

        public PinIndicatorWindow(IntPtr target)
        {
            _target = target;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            IsHitTestVisible = false;

            var border = new System.Windows.Controls.Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
        System.Windows.Media.Color.FromArgb(0xCC, 0x39, 0xFF, 0x14))
            };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(0x39, 0xFF, 0x14),
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.6
            };
            Content = border;

            _sync = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _sync.Tick += SyncPosition;
            _sync.Start();
            SyncPosition(null, EventArgs.Empty);
        }

        private void SyncPosition(object? sender, EventArgs e)
        {
            if (!IsWindow(_target)) { Close(); return; }

            bool minimized = IsIconic(_target);
            if (minimized) { if (IsVisible) Hide(); return; }
            if (!IsVisible) Show();

            GetWindowRect(_target, out RECT r);
            Left = r.Left + 3;
            Top = r.Top - 3;
            Width = (r.Right - r.Left) - 3;
            Height = (r.Bottom - r.Top);

            var myHwnd = new WindowInteropHelper(this).Handle;
            if (myHwnd != IntPtr.Zero)
                SetWindowPos(myHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)exStyle);
        }

        protected override void OnClosed(EventArgs e) { _sync.Stop(); base.OnClosed(e); }
    }

    [SupportedOSPlatform("windows")]
    public class PinIconOverlay : Window
    {
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newVal);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_NOACTIVATE = 0x8000000;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new(-1);

        private readonly IntPtr _target;
        private readonly DispatcherTimer _sync;
        private const int IconSize = 20;
        private new const int Margin = 10;

        public PinIconOverlay(IntPtr target)
        {
            _target = target;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            IsHitTestVisible = false;
            Width = IconSize;
            Height = IconSize;

            try
            {
                var uri = new Uri("pack://application:,,,/pin.ico", UriKind.Absolute);
                using var stream = System.Windows.Application.GetResourceStream(uri)!.Stream;
                var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
                    stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var img = new System.Windows.Controls.Image
                {
                    Source = decoder.Frames[0],
                    Width = IconSize,
                    Height = IconSize,
                    Stretch = System.Windows.Media.Stretch.Uniform
                };
                Content = img;
            }
            catch { }

            _sync = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _sync.Tick += SyncPosition;
            _sync.Start();
            SyncPosition(null, EventArgs.Empty);
        }

        private void SyncPosition(object? sender, EventArgs e)
        {
            if (!IsWindow(_target)) { Close(); return; }

            bool minimized = IsIconic(_target);
            if (minimized) { if (IsVisible) Hide(); return; }
            if (!IsVisible) Show();

            GetWindowRect(_target, out RECT r);
            Left = r.Left + Margin;
            Top = r.Top + Margin;

            var myHwnd = new WindowInteropHelper(this).Handle;
            if (myHwnd != IntPtr.Zero)
                SetWindowPos(myHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)exStyle);
        }

        protected override void OnClosed(EventArgs e) { _sync.Stop(); base.OnClosed(e); }
    }

    public class WindowPinEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; }
        public string WindowTitle { get; }
        public string ProcessName { get; }

        public WindowPinEventArgs(IntPtr hwnd)
        {
            WindowHandle = hwnd;
            WindowTitle = NativeMethods.GetWindowTitle(hwnd);
            ProcessName = NativeMethods.GetProcessName(hwnd);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, StringBuilder cls, int max);

        public static string GetWindowTitle(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string GetProcessName(IntPtr hwnd)
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
            catch { return ""; }
        }
    }
}
