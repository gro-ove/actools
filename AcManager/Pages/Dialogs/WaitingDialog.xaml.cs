using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class WaitingDialog : INotifyPropertyChanged, IProgress<string>, IDisposable {
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

        private bool _shown;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Report(string value) {
            if (value != null) {
                if (!IsVisible && !_shown) {
                    _shown = true;
                    ShowDialogWithoutBlocking();
                }

                Message = value;
            } else if (IsVisible) {
                Close();
            }
        }

        void IDisposable.Dispose() {
            if (IsVisible) {
                Close();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
