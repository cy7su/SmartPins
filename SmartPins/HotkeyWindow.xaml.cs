using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SmartPins
{
    public partial class HotkeyWindow : Window
    {
        private readonly HashSet<ModifierKeys> _mods = new();
        private Key _mainKey = Key.None;
        private bool _listening;

        public string SelectedHotkey { get; private set; } = "";

        public HotkeyWindow()
        {
            InitializeComponent();
            TitleBar.MouseLeftButtonDown += (_, _) => DragMove();
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            Loaded += (_, _) => { _listening = true; Focus(); };
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!_listening) return;
            e.Handled = true;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) _mods.Add(ModifierKeys.Control);
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) _mods.Add(ModifierKeys.Alt);
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) _mods.Add(ModifierKeys.Shift);
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) _mods.Add(ModifierKeys.Windows);

            if (!IsModifier(e.Key)) _mainKey = e.Key;
            Render();
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (!_listening) return;
            e.Handled = true;
            if (e.Key is Key.LeftCtrl or Key.RightCtrl) _mods.Remove(ModifierKeys.Control);
            if (e.Key is Key.LeftAlt or Key.RightAlt) _mods.Remove(ModifierKeys.Alt);
            if (e.Key is Key.LeftShift or Key.RightShift) _mods.Remove(ModifierKeys.Shift);
            if (e.Key is Key.LWin or Key.RWin) _mods.Remove(ModifierKeys.Windows);
            Render();
        }

        private void Render()
        {
            var parts = new List<string>();
            if (_mods.Contains(ModifierKeys.Control)) parts.Add("Ctrl");
            if (_mods.Contains(ModifierKeys.Alt)) parts.Add("Alt");
            if (_mods.Contains(ModifierKeys.Shift)) parts.Add("Shift");
            if (_mods.Contains(ModifierKeys.Windows)) parts.Add("Win");

            string keyName = _mainKey != Key.None ? KeyName(_mainKey) : "";

            if (parts.Count > 0 && keyName != "")
            {
                SelectedHotkey = string.Join("+", parts) + "+" + keyName;
                HotkeyDisplay.Text = SelectedHotkey;
                HotkeyDisplay.Foreground = new SolidColorBrush(Color.FromRgb(0x39, 0xFF, 0x14));
            }
            else if (parts.Count > 0)
            {
                HotkeyDisplay.Text = string.Join("+", parts) + "+...";
                HotkeyDisplay.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            }
            else
            {
                HotkeyDisplay.Text = "press keys...";
                HotkeyDisplay.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
            }
        }

        private static bool IsModifier(Key k) =>
            k is Key.LeftCtrl or Key.RightCtrl or
                 Key.LeftAlt or Key.RightAlt or
                 Key.LeftShift or Key.RightShift or
                 Key.LWin or Key.RWin;

        private static string KeyName(Key k) => k switch
        {
            >= Key.A and <= Key.Z => k.ToString(),
            >= Key.D0 and <= Key.D9 => ((int)k - (int)Key.D0).ToString(),
            >= Key.F1 and <= Key.F12 => k.ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "Num" + ((int)k - (int)Key.NumPad0),
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            _ => k.ToString()
        };

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _mods.Clear();
            _mainKey = Key.None;
            SelectedHotkey = "";
            Render();
            Focus();
        }

        private void PresetHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
            {
                SelectedHotkey = tag;
                HotkeyDisplay.Text = tag;
                HotkeyDisplay.Foreground = new SolidColorBrush(Color.FromRgb(0x39, 0xFF, 0x14));
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedHotkey))
            {
                MessageBox.Show("Сначала выберите комбинацию клавиш.", "SmartPins",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        protected override void OnActivated(EventArgs e) { _listening = true; base.OnActivated(e); }
        protected override void OnDeactivated(EventArgs e) { _listening = false; base.OnDeactivated(e); }
    }
}
