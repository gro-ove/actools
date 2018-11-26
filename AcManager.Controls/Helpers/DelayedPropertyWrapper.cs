using System;
using System.Windows.Threading;

namespace AcManager.Controls.Helpers {
    public class DelayedPropertyWrapper<T> {
        public readonly TimeSpan Interval;

        private T _value, _unappliedValue;
        private bool _hasUnappliedValue;

        private readonly Action<T> _changed;

        public DelayedPropertyWrapper(Action<T> changed, TimeSpan inteval) {
            _changed = changed;
            Interval = inteval;
        }

        public DelayedPropertyWrapper(Action<T> changed)
                : this(changed, TimeSpan.FromMilliseconds(250)) { }

        public T Value {
            get { return _value; }
            set {
                if (Equals(value, _hasUnappliedValue ? _unappliedValue : _value)) return;

                var now = DateTime.Now;
                if (Interval == TimeSpan.Zero || now - _previousChange > Interval) {
                    _unappliedValue = default;
                    _hasUnappliedValue = false;
                    _value = value;
                    _changed(value);
                } else {
                    _unappliedValue = value;
                    _hasUnappliedValue = true;
                    ResetTimer();
                }

                _previousChange = now;
            }
        }

        public void ForceValue(T newValue) {
            if (Equals(newValue, _value)) return;
            _timer?.Stop();
            _value = newValue;
            _unappliedValue = default;
            _hasUnappliedValue = false;
            _changed(_value);
            _previousChange = DateTime.Now;
        }

        private DateTime _previousChange;
        private DispatcherTimer _timer;

        private void ResetTimer() {
            if (_timer == null) {
                _timer = new DispatcherTimer { Interval = Interval };
                _timer.Tick += (o, eventArgs) => {
                    _timer.Stop();
                    _value = _unappliedValue;
                    _unappliedValue = default;
                    _hasUnappliedValue = false;
                    _changed(_value);
                    _previousChange = DateTime.Now;
                };
            } else {
                _timer.Stop();
            }

            _timer.Start();
        }
    }
}
