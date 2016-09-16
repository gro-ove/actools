using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class AppKeyDialog {
        public static bool OptionOfflineMode;

        public const string AppKeyRevokedKey = "AppKeyRevoked";

        public AppKeyDialog() {
            DataContext = new AppKeyDialogViewModel();
            InitializeComponent();

            Buttons = new[] {
                CreateExtraDialogButton(FirstFloor.ModernUI.UiStrings.Ok, Model.ApplyCommand),
                CreateExtraDialogButton(AppStrings.AppKey_GetNewKey, Model.GetNewKeyCommand),
                CancelButton
            };
            OkButton.ToolTip = AppStrings.AppKey_AppWillBeRestarted;

            TextBox.Focus();
            TextBox.SelectAll();
        }

        public static void ShowRevokedMessage() {
            if (ShowMessage(AppStrings.AppKey_KeyRevoked_Message, AppStrings.AppKey_KeyRevoked_Title,
                    MessageBoxButton.YesNoCancel, Application.Current.MainWindow) == MessageBoxResult.Yes) {
                RequestNewKeyUsingEmail();
            }
        }

        public static void RequestNewKeyUsingEmail() {
            var key = ValuesStorage.GetEncryptedString(AppKeyRevokedKey);
            if (key == null) return;

            Process.Start($"mailto:smpcsht@yahoo.com?subject={Uri.EscapeDataString(@"My Key Is Got Revoked")}&body={Uri.EscapeDataString(@"Key: " + key)}");
        }

        private AppKeyDialogViewModel Model => (AppKeyDialogViewModel)DataContext;

        public class AppKeyDialogViewModel : NotifyPropertyChanged, INotifyDataErrorInfo {
            public AppKeyDialogViewModel() {
                var key = AppKeyHolder.Key;
                if (string.IsNullOrWhiteSpace(key)) {
                    key = ValuesStorage.GetEncryptedString(AppKeyRevokedKey);
                    KeyRevoked = key != null;
                }

                Value = key;
            }

            private bool _keyRevoked;

            public bool KeyRevoked {
                get { return _keyRevoked; }
                set {
                    if (Equals(value, _keyRevoked)) return;
                    _keyRevoked = value;
                    OnPropertyChanged();
                    _revokedKeyMessageCommand?.OnCanExecuteChanged();
                }
            }

            private ICommandExt _revokedKeyMessageCommand;

            public ICommand RevokedKeyMessageCommand => _revokedKeyMessageCommand ?? (_revokedKeyMessageCommand = new DelegateCommand(ShowRevokedMessage, () => KeyRevoked));

            private bool _isValueAcceptable = true;

            public bool IsValueAcceptable {
                get { return _isValueAcceptable; }
                private set {
                    if (value == _isValueAcceptable) return;
                    _isValueAcceptable = value;

                    OnPropertyChanged();
                    OnErrorsChanged(nameof(Value));
                    _applyCommand?.OnCanExecuteChanged();
                    _getNewKeyCommand?.OnCanExecuteChanged();
                }
            }

            private bool _offlineModeAvailable;

            public bool OfflineModeAvailable {
                get { return _offlineModeAvailable; }
                set {
                    if (Equals(value, _offlineModeAvailable)) return;
                    _offlineModeAvailable = value;
                    OnPropertyChanged();
                    _offlineModeCommand?.OnCanExecuteChanged();
                }
            }

            private int _attemptsCounter;

            private ICommandExt _tryAgainCommand;

            public ICommand TryAgainCommand => _tryAgainCommand ?? (_tryAgainCommand = new DelegateCommand(() => {
                _attemptsCounter++;
                TestValue();
            }));

            private ICommandExt _offlineModeCommand;

            public ICommand OfflineModeCommand => _offlineModeCommand ?? (_offlineModeCommand = new DelegateCommand(() => {
                OptionOfflineMode = true;
                OfflineModeAvailable = false;
                TestValue();
            }, () => OfflineModeAvailable));

            private bool _internetConnectionRequired;

            public bool InternetConnectionRequired {
                get { return _internetConnectionRequired; }
                set {
                    if (Equals(value, _internetConnectionRequired)) return;
                    _internetConnectionRequired = value;
                    OnPropertyChanged();
                    OnErrorsChanged(nameof(Value));
                    _applyCommand?.OnCanExecuteChanged();
                }
            }

            private string _value;

            public string Value {
                get { return _value; }
                set {
                    if (Equals(value, _value)) return;
                    _value = value;

                    OnPropertyChanged();
                    OnErrorsChanged();
                    _applyCommand?.OnCanExecuteChanged();
                    _getNewKeyCommand?.OnCanExecuteChanged();

                    TestValue();
                }
            }

            private bool _checkingInProgress;

            public bool CheckingInProgress {
                get { return _checkingInProgress; }
                set {
                    if (Equals(value, _checkingInProgress)) return;
                    _checkingInProgress = value;
                    OnPropertyChanged();
                    _applyCommand?.OnCanExecuteChanged();
                }
            }

            private int _testN;

            private async void TestValue() {
                var testN = ++_testN;

                if (string.IsNullOrWhiteSpace(Value)) {
                    IsValueAcceptable = true;
                    CheckingInProgress = false;
                    return;
                }

                CheckingInProgress = true;
                InternetConnectionRequired = false;

                await Task.Delay(50);
                if (testN != _testN) return;

                var value = await InternalUtils.CheckKeyAsync(Value, CmApiProvider.UserAgent);
                if (testN != _testN) return;

                CheckingInProgress = false;

                if (value.HasValue || OptionOfflineMode) {
                    IsValueAcceptable = value ?? true;
                } else {
                    InternetConnectionRequired = true;
                    IsValueAcceptable = false;

                    if (_attemptsCounter == 1) {
                        OfflineModeAvailable = true;
                    }
                }
            }

            private ICommandExt _applyCommand;

            public ICommand ApplyCommand => _applyCommand ?? (_applyCommand = new DelegateCommand(() => {
                ValuesStorage.Remove(AppKeyRevokedKey);
                AppKeyHolder.Instance.SetKey(Value);

                ShowMessage(AppStrings.AppKey_PreRestart_Message, AppStrings.AppKey_PreRestart_Title, MessageBoxButton.OK);
                WindowsHelper.RestartCurrentApplication();
            }, () => IsValueAcceptable && !CheckingInProgress && !InternetConnectionRequired && !string.IsNullOrWhiteSpace(Value)));

            private ICommandExt _getNewKeyCommand;

            public ICommand GetNewKeyCommand => _getNewKeyCommand ?? (_getNewKeyCommand = new DelegateCommand(() => {
                Process.Start("http://acstuff.ru/app/cm/key/get");
            }, () => !IsValueAcceptable || string.IsNullOrWhiteSpace(Value)));

            public IEnumerable GetErrors(string propertyName) {
                return propertyName == nameof(Value) ? (string.IsNullOrWhiteSpace(Value) ? new[] { AppStrings.Common_RequiredValue } :
                    IsValueAcceptable ? null : new[] { InternetConnectionRequired ? AppStrings.AppKey_CannotCheck : AppStrings.AppKey_InvalidKey }) : null;
            }

            public bool HasErrors => string.IsNullOrWhiteSpace(Value) || !IsValueAcceptable;
            public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

            public void OnErrorsChanged([CallerMemberName] string propertyName = null) {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }

        private int _counter;

        private void UIElement_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (++_counter == 10) {
                OptionOfflineMode = true;
            }
        }
    }
}
