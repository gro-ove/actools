using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
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

            Logging.Write($"Installed version for {GetType().Name}: {installedVersion}");
            InstalledVersion = installedVersion;
            if (UpdatePeriod != TimeSpan.Zero || installedVersion == @"0") {
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
            // What the hell is that? What was I thinking? ಠ_ಠ
            await Task.Delay(MathUtils.Random(10, 1000));
            FirstCheck();
        }

        protected virtual void FirstCheck() {
            CheckAndUpdateIfNeeded().Ignore();
        }

        private CancellationTokenSource _periodicCheckCancellation;

        protected virtual void OnCommonSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.CommonSettings.UpdatePeriod)) return;
            RestartPeriodicCheck();
        }

        private static bool IsValidTimeSpan(TimeSpan t) {
            return t != TimeSpan.Zero && t.TotalMilliseconds < int.MaxValue;
        }

        protected void RestartPeriodicCheck() {
            if (_periodicCheckCancellation != null) {
                _periodicCheckCancellation.Cancel();
                _periodicCheckCancellation = null;
            }

            var oldValue = UpdatePeriod;
            UpdatePeriod = GetUpdatePeriod();

            if (!IsValidTimeSpan(oldValue) && IsValidTimeSpan(UpdatePeriod)) {
                CheckAndUpdateIfNeeded().Ignore();
            }

            if (!IsValidTimeSpan(UpdatePeriod)) return;
            _periodicCheckCancellation = new CancellationTokenSource();
            PeriodicCheckAsync(_periodicCheckCancellation.Token).Ignore();
        }

        private async Task PeriodicCheckAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                await Task.Delay(UpdatePeriod, token);
                await CheckAndUpdateIfNeeded();
            }
        }

        private string _latestError;

        public string LatestError {
            get => _latestError;
            set => Apply(value, ref _latestError, () => {
                if (!string.IsNullOrWhiteSpace(value)) LatestMessage = null;
            });
        }

        private string _latestMessage;

        public string LatestMessage {
            get => _latestMessage;
            set => Apply(value, ref _latestMessage, () => {
                if (!string.IsNullOrWhiteSpace(value)) LatestError = null;
            });
        }

        private bool _isGetting;

        public bool IsGetting {
            get => _isGetting;
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
            get => _installedVersion;
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

        protected virtual Task GetCheckDelay() {
            return Task.Delay(500);
        }

        public virtual async Task CheckAndUpdateIfNeeded() {
            if (_checkingInProcess) return;
            _checkingInProcess = true;
            _checkAndUpdateIfNeededCommand?.RaiseCanExecuteChanged();

            LatestError = null;
            LatestMessage = null;

            await GetCheckDelay();

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

        protected virtual void ForcedUpdate() { }

        protected virtual bool CanBeUpdated() {
            return !_checkingInProcess && !IsGetting;
        }

        private AsyncCommand _checkAndUpdateIfNeededCommand;

        public AsyncCommand CheckAndUpdateIfNeededCommand => _checkAndUpdateIfNeededCommand ??
                (_checkAndUpdateIfNeededCommand = new AsyncCommand(() => {
                    ForcedUpdate();
                    return CheckAndUpdateIfNeeded();
                }, CanBeUpdated));

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