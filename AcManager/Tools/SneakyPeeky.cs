// Goddamn antiviruses don’t let me to add extra bindings easily. Namely, Kaspersky. Whatever,
// it’s users who will have a problem of some keypresses being missed.
// #define PROPER_HOOK_IMPLEMENTATION

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcTools.Windows;
using AcTools.Windows.Input;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools {
#if PROPER_HOOK_IMPLEMENTATION
        // https://gist.github.com/gro-ove/8dee6bf5d6ddcab57e582df6c434baad
        // Both AV and users: do not worry, no compiler will include that code here via the link 🙂
#else
    public class SneakyPeeky : ISneakyPeeky {
        public event EventHandler<SneakyPeekyEventArgs> PreviewSneak, PreviewPeek;
        public event EventHandler<SneakyPeekyEventArgs> Sneak, Peek;

        private void RaiseSneak(int value) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                var e = new SneakyPeekyEventArgs(value);
                PreviewSneak?.Invoke(this, e);
                if (!e.Handled && !e.SkipMainEvent) {
                    Sneak?.Invoke(this, e);
                }
            });
        }

        private void RaisePeek(int value) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                var e = new SneakyPeekyEventArgs(value);
                PreviewPeek?.Invoke(this, e);
                if (!e.Handled && !e.SkipMainEvent) {
                    Peek?.Invoke(this, e);
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
        private static readonly List<SneakyPeeky> Subscribed;

        static SneakyPeeky() {
            Subscribed = new List<SneakyPeeky>(1);
        }

        private static bool _watchForStaticDirty;
        private static Holder[] _watchForStatic;

        private class Holder {
            private readonly Keys _key;
            private readonly SneakyPeeky[] _peekies;
            private bool _isPressed;

            public Holder(Keys key, IEnumerable<SneakyPeeky> peekies) {
                _key = key;
                _peekies = peekies.ToArray();
            }

            public void Update() {
                var p = User32.IsKeyPressed(_key);
                if (p == _isPressed) return;

                _isPressed = p;
                for (var j = _peekies.Length - 1; j >= 0; j--) {
                    var e = _peekies[j];
                    if (p) {
                        e.RaisePeek((int)_key);
                    } else {
                        e.RaiseSneak((int)_key);
                    }
                }
            }
        }

        private static void RebuildWatchForStatic() {
            lock (Subscribed) {
                _watchForStaticDirty = false;
                _watchForStatic = Subscribed.SelectMany(x => x._watchFor.Select(y => new {
                    Key = y,
                    Peeky = x
                })).GroupBy(x => x.Key).Select(x => new Holder(x.Key, x.Select(y => y.Peeky))).ToArray();
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

        private static void SubscribeInner(SneakyPeeky instance) {
            lock (Subscribed) {
                if (Subscribed.Contains(instance)) return;
                Subscribed.Add(instance);
            }

            _watchForStaticDirty = true;

            if (_thread == null) {
                _thread = new Thread(Start) {
                    Name = "CM Keyboard Polling",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                _thread.Start();
            }
        }

        private static void UnsubscribeInner(SneakyPeeky instance) {
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
                Thread.Sleep(5);

#if DEBUG
                if (++iterations >= 300) {
                    Logging.Debug($"Time per tick: {s.Elapsed.TotalMilliseconds / iterations:F2} ms");
                    iterations = 0;
                    s.Restart();
                }
#endif
            }
        }
        #endregion
    }
#endif
}