using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    public class KeyboardManager : IDisposable {
        public event KeyEventHandler KeyUp, KeyDown;

        private readonly int _delay;

        private int _hookHandle;

        private int _ignoreDown, _ignoreUp;
        public bool[] Pressed, Unpressed;

        public KeyboardManager(int delay = 20) {
            Pressed = new bool[256];
            _delay = delay;
        }

        private int KeyboardHookProc(int nCode, int wParam, IntPtr lParam) {
            if (nCode != User32.HC_ACTION) return User32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

            var keyboardEvent = (User32.KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(User32.KeyboardHookStruct));
            var virtualKeyCode = keyboardEvent.VirtualKeyCode;

            if (wParam == User32.WM_KEYDOWN || wParam == User32.WM_SYSKEYDOWN) {
                if (Unpressed != null && virtualKeyCode < 256 && Unpressed[virtualKeyCode]) {
                    return -1;
                }

                if (_ignoreDown > 0) {
                    _ignoreDown--;
                } else if (KeyDown != null) {
                    var e = new KeyEventArgs((Keys)virtualKeyCode);
                    KeyDown(this, e);
                    if (e.Handled) {
                        return -1;
                    }

                    if (virtualKeyCode < 256) {
                        Pressed[virtualKeyCode] = true;
                    }
                }
            } else if (wParam == User32.WM_KEYUP || wParam == User32.WM_SYSKEYUP) {
                if (virtualKeyCode < 256) {
                    Pressed[virtualKeyCode] = false;
                }

                if (_ignoreUp > 0) {
                    _ignoreUp--;
                } else if (KeyUp != null) {
                    var e = new KeyEventArgs((Keys)virtualKeyCode);
                    KeyUp(this, e);
                    if (e.Handled) {
                        return -1;
                    }
                }
            }

            return User32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private User32.HookProc _hookProc;

        public void Subscribe() {
            if (_hookHandle != 0) return;

            // ReSharper disable once RedundantDelegateCreation
            _hookProc = new User32.HookProc(KeyboardHookProc);

            using (var process = Process.GetCurrentProcess()) {
                using (var module = process.MainModule) {
                    _hookHandle = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _hookProc,
                                                          Kernel32.GetModuleHandle(module.ModuleName), 0);
                }
            }

            if (_hookHandle == 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Unsubscribe() {
            if (_hookHandle == 0) return;

            var result = User32.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;

            if (result == 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void SendWait(params string[] args) {
            foreach (var keys in args) {
                _ignoreDown += keys.Length;
                _ignoreUp += keys.Length;
                SendKeys.SendWait(keys);
            }
        }

        public void Press(params Keys[] values) {
            foreach (var t in values) {
                _ignoreDown++;
                User32.keybd_event((byte)t, 0x45, User32.KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                Thread.Sleep(_delay);
            }

            for (var i = values.Length - 1; i >= 0; i--) {
                _ignoreUp++;
                User32.keybd_event((byte)values[i], 0x45, User32.KEYEVENTF_EXTENDEDKEY | User32.KEYEVENTF_KEYUP, (UIntPtr)0);
                Thread.Sleep(_delay);
            }
        }

        public void Unpress(params Keys[] values) {
            for (var i = values.Length - 1; i >= 0; i--) {
                _ignoreUp++;
                User32.keybd_event((byte)values[i], 0x45, User32.KEYEVENTF_EXTENDEDKEY | User32.KEYEVENTF_KEYUP, (UIntPtr)0);
                Thread.Sleep(_delay);
            }
        }

        public void UnpressAll() {
            Unpressed = (bool[])Pressed.Clone();
            for (var i = 0; i < Pressed.Length; i++) {
                if (!Pressed[i]) continue;
                _ignoreUp++;
                Pressed[i] = false;
                User32.keybd_event((byte)i, 0x45, User32.KEYEVENTF_EXTENDEDKEY | User32.KEYEVENTF_KEYUP, (UIntPtr)0);
            }
        }

        public void Dispose() {
            Unsubscribe();
        }
    }

}
