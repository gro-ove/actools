using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.ContentTools {
    public enum Stage {
        Loading, Ready, Empty, Error
    }

    public abstract class ContentTool : Switch, INotifyPropertyChanged, IParametrizedUriContent {
        #region Loading
        private CancellationTokenSource _cancellation;

        private async Task Load() {
            try {
                CurrentStage = Stage.Loading;

                bool result;
                using (_cancellation = new CancellationTokenSource()) {
                    result = await LoadAsyncOverride(new Progress<AsyncProgressEntry>(entry => {
                        ActionExtension.InvokeInMainThread(() => {
                            ProgressValue = entry;
                        });
                    }), _cancellation.Token);
                }

                _cancellation = null;
                CurrentStage = result ? Stage.Ready : Stage.Empty;
            } catch (Exception e) {
                _cancellation = null;
                CurrentStage = Stage.Error;
                Error = e.Message;
                Logging.Error(e);
            }
        }

        protected abstract Task<bool> LoadAsyncOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);

        private Stage _currentStage;

        public Stage CurrentStage {
            get { return _currentStage; }
            private set {
                if (Equals(value, _currentStage)) return;
                _currentStage = value;
                OnPropertyChanged();
            }
        }

        private string _error;

        public string Error {
            get { return _error; }
            set {
                if (Equals(value, _error)) return;
                _error = value;
                OnPropertyChanged();
            }
        }

        private AsyncProgressEntry _progressValue;

        public AsyncProgressEntry ProgressValue {
            get { return _progressValue; }
            private set {
                if (Equals(value, _progressValue)) return;
                _progressValue = value;
                OnPropertyChanged();
            }
        }
        #endregion

        public void OnUri(Uri uri) {
            InitializeOverride(uri);
            DataContext = this;

            SetBinding(ValueProperty, new Binding {
                Source = this,
                Path = new PropertyPath(nameof(CurrentStage))
            });

            this.OnActualUnload(() => {
                _cancellation?.Cancel();
                DisposeOverride();
            });

            Load().Forget();
        }

        /// <summary>
        /// Call InitializeComponent() here.
        /// </summary>
        protected abstract void InitializeOverride(Uri uri);

        protected virtual void DisposeOverride() {}

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}