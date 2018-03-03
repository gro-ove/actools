using System;
using System.ComponentModel;
using AcManager.Tools.Helpers;
using CefSharp;

namespace AcManager.Controls.UserControls.Cef {
    internal class BrowserProcessHandler : IBrowserProcessHandler {
        protected int MaxTimerDelay = 1000 / (SettingsHolder.Plugins.Cef60Fps ? 60 : 30);

        public BrowserProcessHandler() {
            SettingsHolder.Plugins.PropertyChanged += OnPluginsSettingChanged;
        }

        protected virtual void OnMaxTimerDelayChanged(int newValue) {
            MaxTimerDelay = newValue;
        }

        private void OnPluginsSettingChanged(object o, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(SettingsHolder.Plugins.Cef60Fps)) {
                OnMaxTimerDelayChanged(1000 / (SettingsHolder.Plugins.Cef60Fps ? 60 : 30));
            }
        }

        void IBrowserProcessHandler.OnContextInitialized() { }

        void IBrowserProcessHandler.OnScheduleMessagePumpWork(long delay) {
            OnScheduleMessagePumpWork((int)Math.Min(MaxTimerDelay, delay));
        }

        protected virtual void OnScheduleMessagePumpWork(int delay) { }

        public virtual void Dispose() { }
    }
}