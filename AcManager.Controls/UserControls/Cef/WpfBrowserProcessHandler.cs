using System;
using System.Timers;
using System.Windows.Threading;

namespace AcManager.Controls.UserControls.Cef {
    internal class WpfBrowserProcessHandler : BrowserProcessHandler {
        private Timer _timer;
        private Dispatcher _dispatcher;

        public WpfBrowserProcessHandler(Dispatcher dispatcher) {
            _timer = new Timer { Interval = MaxTimerDelay, AutoReset = true };
            _timer.Start();
            _timer.Elapsed += TimerTick;

            _dispatcher = dispatcher;
            _dispatcher.ShutdownStarted += DispatcherShutdownStarted;
        }

        protected override void OnMaxTimerDelayChanged(int newValue) {
            _timer.Interval = newValue;
        }

        private void DispatcherShutdownStarted(object sender, EventArgs e) {
            _timer?.Stop();
        }

        private void TimerTick(object sender, EventArgs e) {
            _dispatcher.BeginInvoke((Action)CefSharp.Cef.DoMessageLoopWork, DispatcherPriority.Render);
        }

        protected override void OnScheduleMessagePumpWork(int delay) {
            if (delay <= 0) {
                _dispatcher.BeginInvoke((Action)CefSharp.Cef.DoMessageLoopWork, DispatcherPriority.Normal);
            }
        }

        public override void Dispose() {
            if (_dispatcher != null) {
                _dispatcher.ShutdownStarted -= DispatcherShutdownStarted;
                _dispatcher = null;
            }

            if (_timer != null) {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}