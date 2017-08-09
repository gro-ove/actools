using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public class TaskbarHolder : IDisposable {
        internal TaskbarHolder(double priority, Func<Tuple<TaskbarState, double>> periodicCallback) {
            _priority = priority;
            _periodicCallback = periodicCallback;
            _created = Stopwatch.StartNew();

            if (_periodicCallback != null) {
                _timer = new DispatcherTimer {
                    Interval = TimeSpan.FromSeconds(1d / 60d),
                    IsEnabled = true
                };

                _timer.Tick += OnTick;
            }
        }

        private void OnTick(object sender, EventArgs eventArgs) {
            var v = _periodicCallback?.Invoke();
            if (v != null) {
                Set(v.Item1, v.Item2);
            }
        }

        private TaskbarState _state = TaskbarState.Indeterminate;
        private double _value = -1d;

        public void Set(TaskbarState state, double value) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                if (state != _state || value != _value) {
                    _state = state;
                    _value = value;
                    TaskbarService.Update();
                }
            });
        }

        internal bool IsActive => _state != TaskbarState.NoProgress && _created.Elapsed.TotalSeconds > 0.5d;

        internal void CopyTo([CanBeNull] TaskbarProgress progress) {
            progress?.Set(_value * 0.9998 + 0.0001);
            progress?.Set(_state);
        }

        private readonly double _priority;
        private readonly Func<Tuple<TaskbarState, double>> _periodicCallback;
        private readonly DispatcherTimer _timer;
        private Stopwatch _created;

        public void Dispose() {
            if (_timer != null) {
                _timer.IsEnabled = false;
            }

            TaskbarService.Delete(this);
        }

        #region Comparer
        private sealed class PriorityRelationalComparer : Comparer<TaskbarHolder> {
            public override int Compare(TaskbarHolder x, TaskbarHolder y) {
                if (ReferenceEquals(x, y)) return 0;
                if (ReferenceEquals(null, y)) return 1;
                if (ReferenceEquals(null, x)) return -1;
                return x._priority.CompareTo(y._priority);
            }
        }

        public static Comparer<TaskbarHolder> PriorityComparer { get; } = new PriorityRelationalComparer();
        #endregion
    }

    public static class TaskbarService {
        private static readonly List<TaskbarHolder> Holders = new List<TaskbarHolder>();

        [CanBeNull]
        private static TaskbarProgress Progress => LazyProgress.Value;

        private static Lazier<TaskbarProgress> LazyProgress = new Lazier<TaskbarProgress>(() => {
            var window = Application.Current?.MainWindow;
            return window == null ? null : new TaskbarProgress(window);
        }) {
            AutoDispose = true
        };

        static TaskbarService() {
            DpiAwareWindow.NewWindowOpened += (sender, args) => {
                LazyProgress.Reset();
                Update();
            };
        }

        public static TaskbarHolder Create(double priority, Func<Tuple<TaskbarState, double>> periodicCallback = null) {
            var holder = new TaskbarHolder(priority, periodicCallback);
            Holders.AddSorted(holder, TaskbarHolder.PriorityComparer);
            Task.Delay(600).ContinueWith(t => Update());
            return holder;
        }

        internal static void Update() {
            var latest = Holders.LastOrDefault(x => x.IsActive);
            if (latest == null) {
                Progress?.Clear();
            } else {
                latest.CopyTo(Progress);
            }
        }

        internal static void Delete(TaskbarHolder holder) {
            Holders.Remove(holder);
            Update();
        }
    }
}