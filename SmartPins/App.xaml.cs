using Hardcodet.Wpf.TaskbarNotification;
using MaterialDesignThemes.Wpf;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows;

namespace SmartPins
{
    [SupportedOSPlatform("windows")]
    public partial class App : Application
    {
        private TaskbarIcon? _trayIcon;
        internal static WindowPinManager PinManager { get; private set; } = null!;
        internal static MouseHook MouseHook { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(Theme.Dark);
            paletteHelper.SetTheme(theme);

            PinManager = new WindowPinManager();
            MouseHook = new MouseHook(PinManager);

            PinManager.WindowPinned += (_, ev) => ShowBalloon("Pinned", $"[PIN] {ev.WindowTitle}", BalloonIcon.Info);
            PinManager.WindowUnpinned += (_, ev) => ShowBalloon("Unpinned", $"[OFF] {ev.WindowTitle}", BalloonIcon.Info);

            CreateTrayIcon();
        }

        private void ShowBalloon(string title, string msg, BalloonIcon icon) =>
            Dispatcher.Invoke(() => _trayIcon?.ShowBalloonTip(title, msg, icon));

        private System.Windows.Controls.TextBlock? _pinnedCountBlock;

        private void CreateTrayIcon()
        {
            Icon appIcon;
            try
            {
                var uri = new Uri("pack://application:,,,/icon.ico", UriKind.Absolute);
                using var stream = GetResourceStream(uri)!.Stream;
                appIcon = new Icon(stream);
            }
            catch { appIcon = SystemIcons.Application; }

            _trayIcon = new TaskbarIcon
            {
                Icon = appIcon,
                ToolTipText = "SmartPins",
                Visibility = Visibility.Visible
            };

            var menu = new System.Windows.Controls.ContextMenu
            {
                Style = (Style)Resources["TrayMenuStyle"]
            };

            // ── header (non-clickable) ───────────────────────────────────
            _pinnedCountBlock = MakeText("0", "#FF39FF14", 11);
            var headerPanel = Row(
                MakeIcon(PackIconKind.Pin, "#FF39FF14", 14),
                MakeText("SmartPins", "#FF39FF14", 13, bold: true),
                MakeText("  pin:", "#FF444444", 11),
                _pinnedCountBlock
            );
            menu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = headerPanel,
                IsEnabled = false,
                Style = (Style)Resources["TrayItemStyle"]
            });

            menu.Items.Add(Separator());

            // ── actions ──────────────────────────────────────────────────
            menu.Items.Add(Item(PackIconKind.Monitor, "Show panel", "#FF39FF14", () =>
            {
                if (MainWindow is MainWindow mw) mw.ShowFromTray();
                else { MainWindow?.Show(); MainWindow!.WindowState = WindowState.Normal; MainWindow.Activate(); }
            }));

            menu.Items.Add(Item(PackIconKind.CursorPointer, "Pin by cursor", "#FF00FFFF", () =>
                PinManager.IsPinMode = true));

            menu.Items.Add(Item(PackIconKind.PinOff, "Unpin all", "#FFFF3B3B", () =>
                PinManager.UnpinAllWindows()));

            menu.Items.Add(Separator());

            menu.Items.Add(Item(PackIconKind.Cog, "Settings", "#FF666666", () =>
            {
                if (MainWindow is MainWindow mw) { mw.ShowFromTray(); mw.OpenSettings(); }
            }));

            menu.Items.Add(Separator());

            menu.Items.Add(Item(PackIconKind.ExitToApp, "Exit", "#FF666666", Shutdown));

            _trayIcon.ContextMenu = menu;
            _trayIcon.TrayMouseDoubleClick += (_, _) =>
            {
                if (MainWindow is MainWindow mw) mw.ShowFromTray();
            };
            _trayIcon.TrayMiddleMouseDown += (_, _) => PinManager.IsPinMode = true;

            menu.Opened += (_, _) =>
            {
                if (_pinnedCountBlock != null)
                    _pinnedCountBlock.Text = PinManager.GetPinnedWindows().Count().ToString();
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private System.Windows.Controls.MenuItem Item(
            PackIconKind icon, string label, string iconColor, Action onClick)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = Row(MakeIcon(icon, iconColor, 14), MakeText(label, "#FFAAAAAA", 12)),
                Style = (Style)Resources["TrayItemStyle"]
            };
            item.Click += (_, _) => onClick();
            return item;
        }

        private static System.Windows.Controls.StackPanel Row(
            params System.Windows.UIElement[] children)
        {
            var p = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            foreach (var c in children) p.Children.Add(c);
            return p;
        }

        private static PackIcon MakeIcon(PackIconKind kind, string hex, double size) => new()
        {
            Kind = kind,
            Width = size,
            Height = size,
            Foreground = Brush(hex),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new System.Windows.Thickness(0, 0, 8, 0)
        };

        private static System.Windows.Controls.TextBlock MakeText(
            string text, string hex, double size, bool bold = false) => new()
            {
                Text = text,
                Foreground = Brush(hex),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = size,
                FontWeight = bold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

        private static System.Windows.Controls.Separator Separator() => new()
        {
            Background = Brush("#FF2A2A2A"),
            Margin = new System.Windows.Thickness(10, 2, 10, 2),
            Height = 1
        };

        private static System.Windows.Media.SolidColorBrush Brush(string hex) =>
            new(System.Windows.Media.ColorConverter.ConvertFromString(hex) is System.Windows.Media.Color c
                ? c : System.Windows.Media.Colors.Gray);

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            MouseHook.Dispose();
            PinManager.UnpinAllWindows();
            PinManager.Dispose();
            base.OnExit(e);
        }
    }
}
