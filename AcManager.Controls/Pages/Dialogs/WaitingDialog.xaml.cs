using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Controls;
using AcManager.Tools.Data;
using AcTools.Utils.Helpers;
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

        private bool _shown, _closed;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Report(string value) {
            if (Message == value) return;
            Dispatcher.Invoke(() => {
                if (value != null) {
                    if (!IsVisible && !_shown) {
                        _shown = true;
                        ShowDialogWithoutBlocking();
                    }

                    Message = value;
                } else if (IsVisible && !_closed) {
                    Close();
                    _closed = true;
                }
            });
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Report(double? value) {
            if (Progress.HasValue && value.HasValue && Math.Abs(Progress.Value - value.Value) < 0.0001) return;
            Dispatcher.Invoke(() => {
                if (value != null) {
                    if (!IsVisible && !_shown) {
                        _shown = true;
                        ShowDialogWithoutBlocking();
                    }
                    
                    Progress = value;
                    ProgressIndetermitate = value == 0d;
                } else if (IsVisible && !_closed) {
                    Close();
                    _closed = true;
                }
            });
        }

        public void Report(AsyncProgressEntry value) {
            Dispatcher.Invoke(() => {
                if (value.Message != null) {
                    if (!IsVisible && !_shown) {
                        _shown = true;
                        ShowDialogWithoutBlocking();
                    }

                    Message = value.Message;
                    Progress = value.Progress;
                    ProgressIndetermitate = value.Progress == 0d;
                } else if (IsVisible && !_closed) {
                    Close();
                    _closed = true;
                }
            });
        }

        public void Report(Tuple<string, double?> value) {
            Dispatcher.Invoke(() => {
                if (value.Item1 != null) {
                    if (!IsVisible && !_shown) {
                        _shown = true;
                        ShowDialogWithoutBlocking();
                    }

                    Message = value.Item1;
                    Progress = value.Item2;
                    ProgressIndetermitate = value.Item2 == 0d;
                } else if (IsVisible && !_closed) {
                    Close();
                    _closed = true;
                }
            });
        }

        void IDisposable.Dispose() {
            DisposeHelper.Dispose(ref _cancellationTokenSource);
            Dispatcher.Invoke(() => {
                if (IsVisible && !_closed) {
                    Close();
                    _closed = true;
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
