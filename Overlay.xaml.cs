using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WpfApp1
{
    public partial class Overlay : Window
    {
        private DispatcherTimer _refreshScreen = new DispatcherTimer();
        private DispatcherTimer _timer = new DispatcherTimer();
        private Alerts _alerts;

        public Overlay(CookieContainer cookies, string address)
        {
            InitializeComponent();
            _alerts = new Alerts(cookies, address);
            _timer.Interval = TimeSpan.FromMinutes(1);
            _timer.Tick += RefreshNick;
            _timer.Start();
            SetNick();
        }

        public string Url { get; set; }

        #region WinAPI

        [DllImport("User32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("User32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("kernel32.dll")]
        public static extern Int16 GlobalAddAtom(string name);
        [DllImport("kernel32.dll")]
        public static extern Int16 GlobalDeleteAtom(Int16 nAtom);

        private Dictionary<Int16, Action> _globalActions = new Dictionary<short, Action>();
        private IntPtr _windowHandle;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(_windowHandle);
            source.AddHook(WndProc);
        }

        public bool RegisterGlobalHotkey(Action action, System.Windows.Forms.Keys commonKey, params ModifierKeys[] keys)
        {
            uint mod = keys.Cast<uint>().Aggregate((current, modKey) => current | modKey);
            short atom = GlobalAddAtom("OurAmazingApp" + (_globalActions.Count + 1));
            bool status = RegisterHotKey(_windowHandle, atom, mod, (uint)commonKey);

            if (status)
                _globalActions.Add(atom, action);

            return status;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == 0x0312)
            {
                short atom = Int16.Parse(wparam.ToString());
                if (_globalActions.ContainsKey(atom))
                    _globalActions[atom]();
            }

            return IntPtr.Zero;
        }

        public void UnregisterHotkeys()
        {
            foreach (var atom in _globalActions.Keys)
            {
                UnregisterHotKey(_windowHandle, atom);
                GlobalDeleteAtom(atom);
            }
        }

        //[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        //[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        //[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        //[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private static extern IntPtr GetModuleHandle(string lpModuleName);

        //// запуск отлова
        //private static IntPtr SetHook(LowLevelKeyboardProc proc)
        //{
        //    using (var curProcess = Process.GetCurrentProcess())
        //    {
        //        using (var curModule = curProcess.MainModule)
        //        {
        //            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        //        }
        //    }
        //}

        //// отлов и, при необходимости, обработка хоткея
        //private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        //{
        //    if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        //    {
        //        var vkCode = (Keys)Marshal.ReadInt32(lParam);
        //        switch (vkCode)
        //        {
        //            case Keys.MediaNextTrack:
        //                {
        //                    break;
        //                }
        //        }
        //    }
        //    return CallNextHookEx(hookId, nCode, wParam, lParam);
        //}

        public enum GWL : int
        {
            ExStyle = -20
        }
        public enum WS_EX : int
        {
            Transparent = 0x20,
            Layered = 0x80000,
            APPWINDOW = 0x40000,
            NOACTIVATE = 0x08000000
        }

        [DllImport("user32", EntryPoint = "SetWindowLong")]
        static extern int SetWindowLong(IntPtr hWnd, GWL nIndex, WS_EX dsNewLong);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        static readonly IntPtr HWND_TOP = new IntPtr(-1);
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RegisterGlobalHotkey(() => OpenUrl(), System.Windows.Forms.Keys.F3, ModifierKeys.Alt, ModifierKeys.Control);
            RegisterGlobalHotkey(() => ShowForm(), System.Windows.Forms.Keys.F2, ModifierKeys.Alt, ModifierKeys.Control);
            RegisterGlobalHotkey(() => MoveForm(), System.Windows.Forms.Keys.F5, ModifierKeys.Alt, ModifierKeys.Control);
            SetWindowLong(new WindowInteropHelper(this).Handle, GWL.ExStyle, WS_EX.Layered | WS_EX.Transparent | WS_EX.NOACTIVATE);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            _refreshScreen.Stop();
            UnregisterHotkeys();
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MessageBox.Show("Открыть данную тему?", "Вопрос", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                Process.Start(Url);
        }

        private void OpenUrl() =>
            Process.Start(Url);

        private void ShowForm()
        {
            SetWindowLong(new WindowInteropHelper(this).Handle, GWL.ExStyle, WS_EX.Layered | WS_EX.Transparent | WS_EX.NOACTIVATE);
            SetWindowPos(new WindowInteropHelper(this).Handle, HWND_TOP, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0010);
        }

        private void MoveForm() =>
            SetWindowLong(new WindowInteropHelper(this).Handle, GWL.ExStyle, WS_EX.NOACTIVATE);

        private async void SetNick()
        {
            List<ParserData> list = await _alerts.GetLastNickName();
            Url = list[0].Address;
            if (nickName.Text.ToString() != list[0].Title)
                SystemSounds.Asterisk.Play();
            nickName.Text = list[0].Title;
        }

        private void RefreshNick(object sender, EventArgs e) =>
            SetNick();
        #region InWork...
        //public enum GWL
        //{
        //    ExStyle = -20
        //}

        //public enum WS_EX
        //{
        //    Transparent = 0x20,
        //    Layered = 0x80000
        //}

        //public enum LWA
        //{
        //    ColorKey = 0x1,
        //    Alpha = 0x2
        //}

        //[DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        //public static extern int GetWindowLong(IntPtr hWnd, GWL nIndex);

        //[DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        //public static extern int SetWindowLong(IntPtr hWnd, GWL nIndex, int dwNewLong);

        //[DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
        //public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte alpha, LWA dwFlags);


        //private void RefreshOverlay(object sender, EventArgs e)
        //{
        //    SetWindowPos(new WindowInteropHelper(this).Handle, HWND_TOP, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0010);
        //}

        //private void Window_Loaded(object sender, RoutedEventArgs e)
        //{
        //    int wl = GetWindowLong(new WindowInteropHelper(this).Handle, GWL.ExStyle);
        //    wl = wl | 0x80000 | 0x20;
        //    SetWindowLong(new WindowInteropHelper(this).Handle, GWL.ExStyle, wl);
        //    SetLayeredWindowAttributes(new WindowInteropHelper(this).Handle, 0, 128, LWA.Alpha);
        //}
        #endregion
    }
}