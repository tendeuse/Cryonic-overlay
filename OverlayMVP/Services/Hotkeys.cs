// filename: Services/Hotkeys.cs
// Registers system-wide hotkeys so the overlay can be toggled
// even when EVE Online has focus.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace OverlayMVP.Services
{
    public sealed class HotkeyManager : IDisposable
    {
        // Win32
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modifier flags
        private const uint MOD_ALT     = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT   = 0x0004;
        private const uint MOD_NOREPEAT = 0x4000;

        private const int WM_HOTKEY = 0x0312;

        // ----------------------------------------------------------------
        // Public hotkey IDs — callers subscribe by id
        // ----------------------------------------------------------------
        public const int ID_TOGGLE_VISIBILITY  = 9001;  // Ctrl+Shift+O
        public const int ID_TOGGLE_CLICKTHROUGH = 9002;  // Ctrl+Shift+C
        public const int ID_REPORT_INTEL        = 9003;  // Ctrl+Shift+I
        public const int ID_REPORT_CLEAR        = 9004;  // Ctrl+Shift+X

        private readonly IntPtr _hwnd;
        private readonly HwndSource _source;
        private readonly Dictionary<int, Action> _handlers = new();

        public HotkeyManager(Window window)
        {
            _hwnd   = new WindowInteropHelper(window).EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd)!;
            _source.AddHook(WndProc);

            // Register default overlay hotkeys
            Register(ID_TOGGLE_VISIBILITY,   MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(Key.O));
            Register(ID_TOGGLE_CLICKTHROUGH, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(Key.C));
            Register(ID_REPORT_INTEL,        MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(Key.I));
            Register(ID_REPORT_CLEAR,        MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(Key.X));
        }

        public void SetHandler(int id, Action handler)
            => _handlers[id] = handler;

        private void Register(int id, uint modifiers, uint vk)
            => RegisterHotKey(_hwnd, id, modifiers, vk);

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_handlers.TryGetValue(id, out var handler))
                {
                    handler();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            _source.RemoveHook(WndProc);
            foreach (var id in _handlers.Keys)
                UnregisterHotKey(_hwnd, id);
        }
    }
}
