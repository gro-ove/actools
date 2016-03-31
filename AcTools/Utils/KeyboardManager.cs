using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AcTools.Windows;

namespace AcTools.Utils {
    public class KeyboardManager : IDisposable {
        public event KeyEventHandler KeyUp, KeyDown;
        
        private readonly int _delay;
        
        private int _hookHandle;
        private User32.HookProc _hookProc;

        private int _ignoreDown, _ignoreUp;
        public bool[] Pressed, Unpressed;

        public KeyboardManager(int delay = 20) {
            Pressed = new bool[256];
            _delay = delay;
        }

        private int KeyboardHookProc(int nCode, Int32 wParam, IntPtr lParam) {
            if (nCode >= 0) {
                var khs = (User32.KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(User32.KeyboardHookStruct));
                
                if (wParam == User32.WM_KEYDOWN || wParam == User32.WM_SYSKEYDOWN) {
                    if (Unpressed != null && khs.VirtualKeyCode < 256 && Unpressed[khs.VirtualKeyCode]) {
                        return -1;
                    }

                    if (_ignoreDown > 0) {
                        _ignoreDown--;
                    } else if (KeyDown != null) {
                        var e = new KeyEventArgs((Keys)khs.VirtualKeyCode);
                        KeyDown(this, e);
                        if (e.Handled) { 
                            return -1;
                        }
                        if (khs.VirtualKeyCode < 256){
                            Pressed[khs.VirtualKeyCode] = true;
                        }
                    }
                } else if (wParam == User32.WM_KEYUP || wParam == User32.WM_SYSKEYUP) {
                    if (khs.VirtualKeyCode < 256){
                        Pressed[khs.VirtualKeyCode] = false;
                    }

                    if (_ignoreUp > 0) {
                        _ignoreUp--;
                    } else if (KeyUp != null) {
                        var e = new KeyEventArgs((Keys)khs.VirtualKeyCode);
                        KeyUp(this, e);
                        if (e.Handled) {
                            return -1;
                        }
                    }
                }
            }

            return User32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        public void Subscribe() {
            if (_hookHandle == 0) {
                _hookProc = KeyboardHookProc;

                var id = Kernel32.GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);
                var result = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _hookProc, id, 0);
                _hookHandle = result;

                if (result == 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        public void Unsubscribe() {
            if (_hookHandle != 0) {
                var result = User32.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = 0;
                _hookProc = null;

                if (result == 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
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
    
    public class KeyboardSilencer : KeyboardManager {
        public KeyboardSilencer() {
            KeyDown += (s, e) => { e.Handled = true; };
            KeyUp += (s, e) => { e.Handled = true; };
            Subscribe();
        }

        public void Stop() {
            Unsubscribe();
        }
    }
}
