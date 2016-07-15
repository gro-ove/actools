using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Windows.Threading;

namespace FirstFloor.ModernUI.Helpers {
    public class KillerOrder<T> : KillerOrder {
        public new T Victim => (T)base.Victim;

        public KillerOrder(T victim, TimeSpan timeout) : base(victim, timeout) { }
    }

    public class KillerOrder : IDisposable {
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

        public KillerOrder(object victim, TimeSpan timeout) {
            Victim = victim;
            Timeout = timeout;
            KillAfter = DateTime.Now + timeout;

            Register(this);
        }

        public void Delay() {
            KillAfter = DateTime.Now + Timeout;
        }

        public void Dispose() {
            _sockets?.Remove(this);

            var disposable = Victim as IDisposable;
            disposable?.Dispose();
        }

        private void Kill() {
            Killed = true;

            var socket = Victim as Socket;
            if (socket != null) {
                socket.Close();
                return;
            }

            var webClient = Victim as WebClient;
            if (webClient != null) {
                webClient.CancelAsync();
                return;
            }

            var webRequest = Victim as WebRequest;
            if (webRequest != null) {
                webRequest.Abort();
                return;
            }

            var disposable = Victim as IDisposable;
            disposable?.Dispose();
        }

        private static List<KillerOrder> _sockets;
        private static DispatcherTimer _timer;

        private static void Register(KillerOrder order) {
            if (_sockets == null) {
                _sockets = new List<KillerOrder>(2000);
                _timer = new DispatcherTimer {
                    Interval = TimeSpan.FromMilliseconds(100d),
                    IsEnabled = true
                };

                _timer.Tick += Timer_Tick;
            }
            
            _sockets.Add(order);
        }

        private static void Timer_Tick(object sender, EventArgs e) {
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
                    tuple.Kill();
                }
            }
        }
    }
}