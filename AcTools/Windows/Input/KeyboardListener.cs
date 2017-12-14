using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    public class KeyboardListener : IDisposable {
        public event EventHandler<VirtualKeyCodeEventArgs> PreviewKeyUp, PreviewKeyDown;
        public event EventHandler<VirtualKeyCodeEventArgs> KeyUp, KeyDown;

        public KeyboardListener(bool subscribe = true) {
            if (subscribe) {
                Subscribe();
            }
        }

        protected virtual bool RaiseKeyUp(int virtualKeyCode) {
            var e = new VirtualKeyCodeEventArgs(virtualKeyCode);
            PreviewKeyUp?.Invoke(this, e);
            if (!e.Handled && !e.SkipMainEvent) {
                KeyUp?.Invoke(this, e);
            }
            return e.Handled;
        }

        protected virtual bool RaiseKeyDown(int virtualKeyCode) {
            var e = new VirtualKeyCodeEventArgs(virtualKeyCode);
            PreviewKeyDown?.Invoke(this, e);
            if (!e.Handled && !e.SkipMainEvent) {
                KeyDown?.Invoke(this, e);
            }
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
        public static readonly int AppEventFlag = Math.Abs("typo4".GetHashCode());

        private static readonly List<KeyboardListener> Subscribed;
        private static int _hookHandle;
        private static User32.HookProc _hookProc;

        static KeyboardListener() {
            Subscribed = new List<KeyboardListener>(1);
        }

        private static readonly int PacketKey = (int)Keys.Packet;

        private static int KeyboardHookProc(int nCode, int wParam, IntPtr lParam) {
            if (nCode >= 0) {
                var khs = (User32.KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(User32.KeyboardHookStruct));
                if (khs.ExtraInfo != AppEventFlag && khs.VirtualKeyCode != PacketKey) {
                    switch (wParam) {
                        case User32.WM_KEYDOWN:
                        case User32.WM_SYSKEYDOWN:
                            for (var i = Subscribed.Count - 1; i >= 0; i--) {
                                if (Subscribed[i].RaiseKeyDown(khs.VirtualKeyCode)) {
                                    return -1;
                                }
                            }
                            break;

                        case User32.WM_KEYUP:
                        case User32.WM_SYSKEYUP:
                            for (var i = Subscribed.Count - 1; i >= 0; i--) {
                                if (Subscribed[i].RaiseKeyUp(khs.VirtualKeyCode)) {
                                    return -1;
                                }
                            }
                            break;
                    }
                }
            }

            return User32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private static void SubscribeInner(KeyboardListener instance) {
            if (Subscribed.Contains(instance)) return;

            Subscribed.Add(instance);
            if (_hookHandle == 0) {
                // ReSharper disable once RedundantDelegateCreation
                // Have to create that delegate to avoid it being sweeped by GC, I guess.
                _hookProc = new User32.HookProc(KeyboardHookProc);
                _hookHandle = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _hookProc,
                        Kernel32.GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
                if (_hookHandle == 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        private static void UnsubscribeInner(KeyboardListener instance) {
            if (Subscribed.Remove(instance) && Subscribed.Count == 0 && _hookHandle != 0) {
                var result = User32.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = 0;
                _hookProc = null;

                if (result == 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
        #endregion

        #region Ignore events sent by app
        public static void IgnoreNextKeyDown(byte virtualKeyCode) {

        }

        public static void IgnoreNextKeyUp(byte virtualKeyCode) {

        }
        #endregion
    }
}