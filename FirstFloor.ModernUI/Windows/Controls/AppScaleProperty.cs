using System;
using System.Linq;
using System.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class AppScaleProperty : NotifyPropertyChanged {
        private readonly Busy _busy = new Busy();

        private bool _scaleLoaded;
        private double _scale;

        private void EnsureLoaded() {
            if (!_scaleLoaded) {
                _scaleLoaded = true;
                _scale = _delayed = ValuesStorage.GetDouble("__uiScale_2", 1d);
            }

            if (_scale < 0.1 || _scale > 4d || double.IsNaN(_scale) || double.IsInfinity(_scale)) {
                _scale = _delayed = 1d;
            }
        }

        public double Scale {
            get {
                EnsureLoaded();
                return _scale;
            }
            set {
                EnsureLoaded();
                value = Math.Min(Math.Max(value, 0.2), 10d);
                if (Equals(value, _scale)) return;
                var delta = value / _scale;
                _scale = value;
                _delayed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Delayed));
                ValuesStorage.Set("__uiScale_2", value);

                foreach (var window in Application.Current.Windows.OfType<DpiAwareWindow>()) {
                    window.UpdateSizeLimits();
                    window.Width *= delta;
                    window.Height *= delta;
                }
            }
        }

        private double _delayed;

        public double Delayed {
            get {
                EnsureLoaded();
                return _delayed;
            }
            set {
                EnsureLoaded();
                if (Equals(value, _delayed)) return;
                _delayed = value;
                OnPropertyChanged(nameof(Delayed));
                _busy.DoDelay(() => Scale = _delayed, 500);
            }
        }
    }
}