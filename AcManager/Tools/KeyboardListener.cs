using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcTools.Windows;
using AcTools.Windows.Input;
using FirstFloor.ModernUI;

#if DEBUG
using System.Diagnostics;
using FirstFloor.ModernUI.Helpers;
#endif

namespace AcManager.Tools {
    public class KeyboardListener : IKeyboardListener {
        public event EventHandler<KeyboardEventArgs> PreviewKeyUp, PreviewKeyDown;
        public event EventHandler<KeyboardEventArgs> KeyUp, KeyDown;

        private void RaiseKeyUp(int value) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                var e = new KeyboardEventArgs(value);
                PreviewKeyUp?.Invoke(this, e);
                if (!e.Handled) {
                    KeyUp?.Invoke(this, e);
                }
            });
        }

        private void RaiseKeyDown(int value) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                var e = new KeyboardEventArgs(value);
                PreviewKeyDown?.Invoke(this, e);
                if (!e.Handled) {
                    KeyDown?.Invoke(this, e);
                }
            });
        }

        // Just look at that! Now, during the race, CM doesn’t watch for ALL keyboard events, just for
        // the ones you binded. Don’t worry to type any passwords you need.
        private Keys[] _watchFor = new Keys[0];

        public void WatchFor(IEnumerable<Keys> key) {
            _watchForStaticDirty = true;
            _watchFor = key.ToArray();
        }

        public async void Subscribe() {
            await Task.Yield();
            SubscribeInner(this);
        }

        public async void Unsubscribe() {
            await Task.Yield();
            UnsubscribeInner(this);
        }

        public void Dispose() {
            Unsubscribe();
        }

        #region Static part
        private static readonly List<KeyboardListener> Subscribed;

        static KeyboardListener() {
            Subscribed = new List<KeyboardListener>(1);
        }

        private static bool _watchForStaticDirty;
        private static Holder[] _watchForStatic;

        private class Holder {
            private readonly Keys _key;
            private readonly KeyboardListener[] _listeners;
            private bool _isPressed;

            public Holder(Keys key, IEnumerable<KeyboardListener> listeners) {
                _key = key;
                _listeners = listeners.ToArray();
            }

            public void Update() {
                var p = User32.IsAsyncKeyPressed(_key);
                if (p == _isPressed) return;

                _isPressed = p;
                for (var j = _listeners.Length - 1; j >= 0; j--) {
                    var e = _listeners[j];
                    if (p) {
                        e.RaiseKeyDown((int)_key);
                    } else {
                        e.RaiseKeyUp((int)_key);
                    }
                }
            }
        }

        private static void RebuildWatchForStatic() {
            lock (Subscribed) {
                _watchForStaticDirty = false;
                _watchForStatic = Subscribed.SelectMany(x => x._watchFor.Select(y => new {
                    Key = y,
                    Listener = x
                })).GroupBy(x => x.Key).Select(x => new Holder(x.Key, x.Select(y => y.Listener))).ToArray();
            }
        }

        private static void OnTick() {
            if (_watchForStaticDirty) {
                RebuildWatchForStatic();
            }

            for (var i = _watchForStatic.Length - 1; i >= 0; i--) {
                _watchForStatic[i].Update();
            }
        }

        private static void SubscribeInner(KeyboardListener instance) {
            lock (Subscribed) {
                if (Subscribed.Contains(instance)) return;
                Subscribed.Add(instance);
            }

            _watchForStaticDirty = true;

            if (_thread == null) {
                _thread = new Thread(Start) {
                    Name = "CM Keyboard Polling",
                    IsBackground = true
                };
                _thread.Start();
            }
        }

        private static void UnsubscribeInner(KeyboardListener instance) {
            lock (Subscribed) {
                if (Subscribed.Remove(instance)) {
                    _watchForStaticDirty = true;
                    if (Subscribed.Count == 0 && _thread != null) {
                        _currentThread++;
                        _thread = null;
                    }
                }
            }
        }

        private static Thread _thread;
        private static int _currentThread;

        private static void Start() {
            var thread = ++_currentThread;

#if DEBUG
            var s = new Stopwatch();
            var iterations = 0;
#endif

            while (thread == _currentThread) {
#if DEBUG
                s.Start();
                OnTick();
                s.Stop();
#else
                OnTick();
#endif
                Thread.Sleep(20);

#if DEBUG
                if (++iterations >= 3000) {
                    Logging.Debug($"Time per tick: {s.Elapsed.TotalMilliseconds / iterations:F3} ms");
                    iterations = 0;
                    s.Restart();
                }
#endif
            }
        }
        #endregion
    }
}