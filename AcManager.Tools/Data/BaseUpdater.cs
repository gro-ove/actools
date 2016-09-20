using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
// ReSharper disable VirtualMemberCallInConstructor

namespace AcManager.Tools.Data {
    public abstract class BaseUpdater : NotifyPropertyChanged {
        public event EventHandler Updated;

        protected TimeSpan UpdatePeriod { get; private set; }

        protected BaseUpdater([CanBeNull] string installedVersion) {
            UpdatePeriod = GetUpdatePeriod();
            SetListener();

            InstalledVersion = installedVersion;
            if (UpdatePeriod != TimeSpan.Zero) {
                CheckUpdateABitLater();
            }
        }

        /// <summary>
        /// Will be called in BaseUpdater constructor!
        /// </summary>
        protected virtual void SetListener() {
            SettingsHolder.Common.PropertyChanged += OnCommonSettingsChanged;
        }

        /// <summary>
        /// Will be called in BaseUpdater constructor!
        /// </summary>
        protected virtual TimeSpan GetUpdatePeriod() {
            return SettingsHolder.Common.UpdatePeriod.TimeSpan;
        }

        private async void CheckUpdateABitLater() {
            await Task.Delay(MathUtils.Random(10, 1000));
            FirstCheck();
        }

        protected virtual void FirstCheck() {
            CheckAndUpdateIfNeeded().Forget();
        }
        
        private CancellationTokenSource _periodicCheckCancellation;

        protected virtual void OnCommonSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.CommonSettings.UpdatePeriod)) return;
            RestartPeriodicCheck();
        }

        protected void RestartPeriodicCheck() {
            if (_periodicCheckCancellation != null) {
                _periodicCheckCancellation.Cancel();
                _periodicCheckCancellation = null;
            }

            var oldValue = UpdatePeriod;
            UpdatePeriod = GetUpdatePeriod();
            if (oldValue == TimeSpan.Zero && UpdatePeriod != TimeSpan.Zero) {
                CheckAndUpdateIfNeeded().Forget();
            }

            if (UpdatePeriod == TimeSpan.Zero) return;
            _periodicCheckCancellation = new CancellationTokenSource();
            PeriodicCheckAsync(_periodicCheckCancellation.Token).Forget();
        }

        private async Task PeriodicCheckAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                await Task.Delay(UpdatePeriod, token);
                await CheckAndUpdateIfNeeded();
            }
        }

        private string _latestError;

        public string LatestError {
            get { return _latestError; }
            set {
                if (Equals(value, _latestError)) return;
                _latestError = value;
                OnPropertyChanged();
            }
        }

        private bool _isGetting;

        public bool IsGetting {
            get { return _isGetting; }
            set {
                if (Equals(value, _isGetting)) return;
                _isGetting = value;
                OnPropertyChanged();
                _checkAndUpdateIfNeededCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _installedVersion;

        [CanBeNull]
        public string InstalledVersion {
            get { return _installedVersion; }
            set {
                if (Equals(value, _installedVersion)) return;
                _installedVersion = value;
                OnPropertyChanged();

                // specially for LocaleUpdater
                // TODO: mistake in OOP terms
                _checkAndUpdateIfNeededCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _checkingInProcess;

        public virtual async Task CheckAndUpdateIfNeeded() {
            if (_checkingInProcess) return;
            _checkingInProcess = true;
            _checkAndUpdateIfNeededCommand?.RaiseCanExecuteChanged();

            LatestError = null;

            await Task.Delay(500);

            try {
                if (await CheckAndUpdateIfNeededInner()) {
                    OnUpdated();
                }
            } catch (InformativeException e) {
                LatestError = e.ToSingleString();
            } catch (Exception e) {
                LatestError = ToolsStrings.Common_UnhandledError_Commentary;
                Logging.Warning(e);
            } finally {
                _checkingInProcess = false;
                _checkAndUpdateIfNeededCommand?.RaiseCanExecuteChanged();
            }
        }

        protected virtual bool CanBeUpdated() {
            return !_checkingInProcess && !IsGetting;
        }

        private CommandBase _checkAndUpdateIfNeededCommand;

        public ICommand CheckAndUpdateIfNeededCommand => _checkAndUpdateIfNeededCommand ??
                (_checkAndUpdateIfNeededCommand = new DelegateCommand(() => CheckAndUpdateIfNeeded().Forget(), CanBeUpdated));

        /// <summary>
        /// Check and install/prepare update.
        /// </summary>
        /// <returns>True if there is an update</returns>
        protected abstract Task<bool> CheckAndUpdateIfNeededInner();

        protected virtual void OnUpdated() {
            Updated?.Invoke(this, EventArgs.Empty);
        }
    }
}