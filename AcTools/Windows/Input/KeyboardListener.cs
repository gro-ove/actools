using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    public class KeyboardListener : IDisposable {
        public event KeyEventHandler KeyUp, KeyDown;

        public KeyboardListener(bool subscribe = true) {
            if (subscribe) {
                Subscribe();
            }
        }

        protected virtual bool RaiseKeyUp(int virtualKeyCode) {
            var e = new KeyEventArgs((Keys)virtualKeyCode);
            KeyUp?.Invoke(this, e);
            return e.Handled;
        }

        protected virtual bool RaiseKeyDown(int virtualKeyCode) {
            var e = new KeyEventArgs((Keys)virtualKeyCode);
            KeyDown?.Invoke(this, e);
            return e.Handled;
        }

        public void Subscribe() {
            SubscribeInner(this);
        }

        public void Unsubscribe() {
            UnsubscribeInner(this);
        }

        public void Dispose() {
            Unsubscribe();
        }

        #region Static part
        private static readonly List<KeyboardListener> Subscribed;

        private static int _hookHandle;
        private static User32.HookProc _hookProc;

        static KeyboardListener() {
            Subscribed = new List<KeyboardListener>(1);
        }

        private static int KeyboardHookProc(int nCode, int wParam, IntPtr lParam) {
            if (nCode >= 0) {
                var khs = (User32.KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(User32.KeyboardHookStruct));
                switch (wParam) {
                    case User32.WM_KEYDOWN:
                    case User32.WM_SYSKEYDOWN:
                        if (Subscribed.Any(x => x.RaiseKeyDown(khs.VirtualKeyCode))) {
                            return -1;
                        }
                        break;

                    case User32.WM_KEYUP:
                    case User32.WM_SYSKEYUP:
                        if (Subscribed.Any(x => x.RaiseKeyUp(khs.VirtualKeyCode))) {
                            return -1;
                        }
                        break;
                }
            }

            return User32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private static void SubscribeInner(KeyboardListener instance) {
            if (Subscribed.Contains(instance)) return;

            Subscribed.Add(instance);
            if (_hookHandle == 0) {
                // ReSharper disable once RedundantDelegateCreation
                _hookProc = new User32.HookProc(KeyboardHookProc);

                // var id = Kernel32.LoadLibrary("User32");
                var id = Kernel32.GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);
                var result = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _hookProc, id, 0);
                _hookHandle = result;

                AcToolsLogging.Write("Subscribed: " + result);
                if (result == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private static void UnsubscribeInner(KeyboardListener instance) {
            if (Subscribed.Remove(instance) && Subscribed.Count == 0 && _hookHandle != 0) {
                var result = User32.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = 0;
                _hookProc = null;

                AcToolsLogging.Write("Unsubscribed: " + result);
                if (result == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        #endregion
    }
}