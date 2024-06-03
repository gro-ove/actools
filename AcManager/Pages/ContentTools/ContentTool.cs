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
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.ContentTools {
    public enum Stage {
        Loading, Ready, Empty, Error
    }

    public abstract class ContentTool : Switch, IInvokingNotifyPropertyChanged, IParametrizedUriContent {
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
            get => _currentStage;
            private set => this.Apply(value, ref _currentStage);
        }

        private string _error;

        public string Error {
            get => _error;
            set => this.Apply(value, ref _error);
        }

        private AsyncProgressEntry _progressValue;

        public AsyncProgressEntry ProgressValue {
            get => _progressValue;
            private set => this.Apply(value, ref _progressValue);
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

            Load().Ignore();
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

        void IInvokingNotifyPropertyChanged.OnPropertyChanged(string propertyName) {
            OnPropertyChanged(propertyName);
        }

        protected bool Apply<T>(T value, ref T backendValue, Action onChangeCallback = null, [CallerMemberName] string propertyName = null) {
            return NotifyPropertyChangedExtension.Apply(this, value, ref backendValue, onChangeCallback, propertyName);
        }

        protected bool Apply<T>(T value, StoredValue<T> backendValue, Action onChangeCallback = null, [CallerMemberName] string propertyName = null) {
            return NotifyPropertyChangedExtension.Apply(this, value, backendValue, onChangeCallback, propertyName);
        }
    }
}