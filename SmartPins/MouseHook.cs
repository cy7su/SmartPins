using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Interop;

namespace SmartPins
{
    [SupportedOSPlatform("windows")]
    public class MouseHook : IDisposable
    {
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? lpModuleName);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT pt);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int n);

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const uint GA_ROOT = 2;

        public event EventHandler<MouseClickEventArgs>? MouseClick;

        private readonly LowLevelMouseProc _proc;
        private readonly WindowPinManager _mgr;
        private IntPtr _hook;

        public MouseHook(WindowPinManager mgr)
        {
            _mgr = mgr;
            _proc = HookCallback;
            using var cur = System.Diagnostics.Process.GetCurrentProcess();
            using var mod = cur.MainModule!;
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(mod.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN && _mgr.IsPinMode)
            {
                var s = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
                var child = WindowFromPoint(s.pt);
                var hwnd = child != IntPtr.Zero ? GetAncestor(child, GA_ROOT) : IntPtr.Zero;
                if (hwnd == IntPtr.Zero) hwnd = child;

                if (hwnd != IntPtr.Zero && IsWindow(hwnd))
                {
                    var mainHwnd = new WindowInteropHelper(System.Windows.Application.Current.MainWindow!).Handle;
                    if (hwnd != mainHwnd)
                    {
                        var sb = new StringBuilder(256);
                        GetWindowText(hwnd, sb, 256);
                        if (!string.IsNullOrWhiteSpace(sb.ToString()) && sb.ToString() != "Program Manager")
                        {
                            MouseClick?.Invoke(this, new MouseClickEventArgs(hwnd, s.pt));
                            _mgr.HandleMouseClick(hwnd);
                            return (IntPtr)1;
                        }
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose() => UnhookWindowsHookEx(_hook);
    }

    public class MouseClickEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; }
        public MouseHook.POINT Point { get; }
        public MouseClickEventArgs(IntPtr hwnd, MouseHook.POINT pt) { WindowHandle = hwnd; Point = pt; }
    }
}
