using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.Pages.Dialogs {
    public partial class WaitingDialog : INotifyPropertyChanged, IProgress<string>, IProgress<double?>, IProgress<Tuple<String, Double?>>, IProgress<AsyncProgressEntry>, IDisposable {
        public static WaitingDialog Create(string reportValue) {
            var w = new WaitingDialog();
            w.Report(reportValue);
            return w;
        }

        private string _message;

        public string Message {
            get { return _message; }
            set {
                if (Equals(value, _message)) return;
                _message = value;
                OnPropertyChanged();
            }
        }

        private double? _progress;

        public double? Progress {
            get { return _progress; }
            set {
                if (Equals(value, _progress)) return;
                _progress = value;
                OnPropertyChanged();
            }
        }

        private bool _progressIndetermitate;

        public bool ProgressIndetermitate {
            get { return _progressIndetermitate; }
            set {
                if (Equals(value, _progressIndetermitate)) return;
                _progressIndetermitate = value;
                OnPropertyChanged();
            }
        }

        public WaitingDialog(string title = null) {
            Title = title;
            DataContext = this;
            InitializeComponent();
            Buttons = new Button[] {};
        }

        public new string Title {
            get { return base.Title; }
            set { base.Title = value ?? "Please, wait…"; }
        }

        private CancellationTokenSource _cancellationTokenSource;

        public CancellationToken CancellationToken {
            get {
                if (_cancellationTokenSource != null) return _cancellationTokenSource.Token;

                _cancellationTokenSource = new CancellationTokenSource();
                Buttons = new[] { CancelButton };
                Height = 280;
                Closing += WaitingDialog_Closing;
                return _cancellationTokenSource.Token;
            }
        }

        private void WaitingDialog_Closing(object sender, CancelEventArgs e) {
            _cancellationTokenSource?.Cancel();
        }

        private bool _loaded;

        private void OnLoaded(object sender, EventArgs eventArgs) {
            if (_disposed) {
                EnsureClosed();
            }
        }

        private bool _shown, _closed, _disposed;

        private async void EnsureShown() {
            if (_disposed) return;

            _loaded = true;
            if (!IsVisible && !_shown) {
                _shown = true;

                await Task.Delay(500);
                if (_closed || _disposed) return;

                await Application.Current.Dispatcher.BeginInvoke((Action)(() => {
                    if (_closed || _disposed) return;

                    try {
                        ShowDialog();
                    } catch (InvalidOperationException) {
                        Logging.Warning("Damn…");
                    }
                }));
            }
        }

        private void EnsureClosed() {
            if (_loaded && !_closed) {
                _closed = true;
                Close();
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Report(string value) {
            if (Message == value) return;
            Dispatcher.Invoke(() => {
                if (value != null) {
                    EnsureShown();
                    Message = value;
                } else {
                    EnsureClosed();
                }
            });
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Report(double? value) {
            if (Progress.HasValue && value.HasValue && Math.Abs(Progress.Value - value.Value) < 0.0001) return;
            Dispatcher.Invoke(() => {
                if (value != null) {
                    EnsureShown();
                    Progress = value;
                    ProgressIndetermitate = value == 0d;
                } else {
                    EnsureClosed();
                }
            });
        }

        public void Report(AsyncProgressEntry value) {
            Dispatcher.Invoke(() => {
                if (value.Message != null) {
                    EnsureShown();
                    Message = value.Message;
                    Progress = value.Progress;
                    ProgressIndetermitate = value.Progress == 0d;
                } else {
                    EnsureClosed();
                }
            });
        }

        public void Report(Tuple<string, double?> value) {
            Dispatcher.Invoke(() => {
                if (value.Item1 != null) {
                    EnsureShown();
                    Message = value.Item1;
                    Progress = value.Item2;
                    ProgressIndetermitate = value.Item2 == 0d;
                } else {
                    EnsureClosed();
                }
            });
        }

        void IDisposable.Dispose() {
            _disposed = true;
            DisposeHelper.Dispose(ref _cancellationTokenSource);
            Dispatcher.Invoke(EnsureClosed);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
