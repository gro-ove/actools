using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace FirstFloor.ModernUI.Helpers {
    public class KillerOrder<T> : KillerOrder {
        public new T Victim => (T)base.Victim;

        public KillerOrder(T victim, TimeSpan timeout) : base(victim, timeout, default) { }
        public KillerOrder(T victim, TimeSpan timeout, CancellationToken cancellation) : base(victim, timeout, cancellation) { }
    }

    public class KillerOrder : IDisposable {
        public static bool OptionUseDispatcher = false;
        public static TimeSpan OptionInterval = TimeSpan.FromSeconds(2d);

        public readonly object Victim;
        public readonly TimeSpan Timeout;
        public DateTime KillAfter;
        public bool Killed;

        public static KillerOrder<T> Create<T>(T victim, long timeout) {
            return new KillerOrder<T>(victim, TimeSpan.FromMilliseconds(timeout));
        }

        public static KillerOrder<T> Create<T>(T victim, TimeSpan timeout) {
            return new KillerOrder<T>(victim, timeout);
        }

        public static KillerOrder<T> Create<T>(T victim, long timeout, CancellationToken cancellation) {
            return new KillerOrder<T>(victim, TimeSpan.FromMilliseconds(timeout), cancellation);
        }

        public static KillerOrder<T> Create<T>(T victim, TimeSpan timeout, CancellationToken cancellation) {
            return new KillerOrder<T>(victim, timeout, cancellation);
        }

        protected KillerOrder(object victim, TimeSpan timeout, CancellationToken cancellation) {
            Victim = victim;
            Timeout = timeout;
            Delay();

            if (Timeout != TimeSpan.MaxValue) {
                Register(this);
            }

            if (cancellation != default) {
                cancellation.Register(Kill);
            }
        }

        public void Delay() {
            KillAfter = Timeout == TimeSpan.MaxValue ? DateTime.MaxValue : DateTime.Now + Timeout;
        }

        public void Pause() {
            KillAfter = DateTime.MaxValue;
        }

        public void Dispose() {
            lock (StaticLock) {
                _sockets?.Remove(this);
            }

            var disposable = Victim as IDisposable;
            disposable?.Dispose();
        }

        public IProgress<T> DelayingProgress<T>() {
            return new Progress<T>(v => Delay());
        }

        private void Kill() {
            Killed = true;

            switch (Victim) {
                case Socket socket:
                    socket.Close();
                    return;
                case WebClient webClient:
                    webClient.CancelAsync();
                    return;
                case WebRequest webRequest:
                    webRequest.Abort();
                    return;
            }

            var disposable = Victim as IDisposable;
            disposable?.Dispose();
        }

        private static readonly object StaticLock = new object();

        private static List<KillerOrder> _sockets;
        private static DispatcherTimer _dispatcherTimer;
        private static Timer _timer;

        private static void Register(KillerOrder order) {
            lock (StaticLock) {
                if (_sockets == null) {
                    _sockets = new List<KillerOrder>();

                    if (OptionUseDispatcher) {
                        _dispatcherTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher) {
                            Interval = OptionInterval,
                            IsEnabled = true
                        };

                        _dispatcherTimer.Tick += OnDispatcherTick;
                    } else {
                        _timer = new Timer(OnTick, null, OptionInterval, System.Threading.Timeout.InfiniteTimeSpan);
                    }
                }

                _sockets.Add(order);
            }
        }

        private static void InvokeKill(bool sync) {
            lock (StaticLock) {
                if (_sockets.Count == 0) return;

                List<KillerOrder> list = null;
                var now = DateTime.Now;
                foreach (var pair in _sockets) {
                    if (now > pair.KillAfter) {
                        if (list == null) {
                            list = new List<KillerOrder> { pair };
                        } else {
                            list.Add(pair);
                            if (list.Count > 50) break;
                        }
                    }
                }

                if (list != null) {
                    foreach (var tuple in list) {
                        _sockets.Remove(tuple);
                        if (sync) {
                            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).BeginInvoke(DispatcherPriority.Background, (Action)tuple.Kill);
                        } else {
                            tuple.Kill();
                        }
                    }
                }
            }
        }

        private static void OnTick(object state) {
            InvokeKill(true);
            _timer.Change(OptionInterval, System.Threading.Timeout.InfiniteTimeSpan);
        }

        private static void OnDispatcherTick(object sender, EventArgs e) {
            InvokeKill(false);
        }
    }
}