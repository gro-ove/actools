using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Controls;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class WaitingDialog : INotifyPropertyChanged, IProgress<string>, IDisposable {
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

        public WaitingDialog(string title = "Please, wait…") {
            Title = title;
            DataContext = this;
            InitializeComponent();
            Buttons = new Button[] {};
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
