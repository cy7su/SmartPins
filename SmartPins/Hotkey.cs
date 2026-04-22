using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SmartPins
{
    public class Hotkey : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        private const int WM_HOTKEY = 0x0312;
        private static int _currentId;
        private readonly int _id;
        private readonly IntPtr _handle;

        public event EventHandler? Pressed;

        public Hotkey(ModifierKeys modifier, Key key)
        {
            _id = ++_currentId;
            _handle = new WindowInteropHelper(Application.Current.MainWindow!).Handle;

            if (!RegisterHotKey(_handle, _id, (uint)modifier, (uint)KeyInterop.VirtualKeyFromKey(key)))
            {
                throw new InvalidOperationException($"Не удалось зарегистрировать горячую клавишу. Ошибка: {GetLastError()}");
            }

            ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
        }

        private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == WM_HOTKEY && msg.wParam.ToInt32() == _id)
            {
                Pressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
        }

        public void Dispose()
        {
            ComponentDispatcher.ThreadPreprocessMessage -= ComponentDispatcher_ThreadPreprocessMessage;
            UnregisterHotKey(_handle, _id);
        }
    }
}