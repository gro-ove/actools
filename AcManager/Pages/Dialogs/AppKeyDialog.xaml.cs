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
                CreateExtraDialogButton(FirstFloor.ModernUI.Resources.Ok, Model.ApplyCommand),
                CreateExtraDialogButton(@"Get a New Key", Model.GetNewKeyCommand),
                CancelButton
            };
            OkButton.ToolTip = "App will be restarted";

            TextBox.Focus();
            TextBox.SelectAll();
        }

        public static void ShowRevokedMessage() {
            if (ShowMessage("Sorry, but your key was compromised and got revoked. Would you like to contact us to get another one?", "Key Revoked",
                    MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes) {
                RequestNewKeyUsingEmail();
                WindowsHelper.RestartCurrentApplication();
            }
        }

        public static void RequestNewKeyUsingEmail() {
            var key = ValuesStorage.GetEncryptedString(AppKeyRevokedKey);
            if (key == null) return;

            Process.Start($"mailto:smpcsht@yahoo.com?subject={Uri.EscapeDataString("My Key Is Got Revoked")}&body={Uri.EscapeDataString("Key: " + key)}");
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
                }
            }

            private RelayCommand _revokedKeyMessageCommand;

            public RelayCommand RevokedKeyMessageCommand => _revokedKeyMessageCommand ?? (_revokedKeyMessageCommand = new RelayCommand(o => {
                ShowRevokedMessage();
            }, o => KeyRevoked));

            private bool _isValueAcceptable = true;

            public bool IsValueAcceptable {
                get { return _isValueAcceptable; }
                private set {
                    if (value == _isValueAcceptable) return;
                    _isValueAcceptable = value;

                    OnPropertyChanged();
                    OnErrorsChanged(nameof(Value));
                    ApplyCommand.OnCanExecuteChanged();
                    GetNewKeyCommand.OnCanExecuteChanged();
                }
            }

            private bool _internetConnectionRequired;

            public bool InternetConnectionRequired {
                get { return _internetConnectionRequired; }
                set {
                    if (Equals(value, _internetConnectionRequired)) return;
                    _internetConnectionRequired = value;
                    OnPropertyChanged();
                    OnErrorsChanged(nameof(Value));
                    ApplyCommand.OnCanExecuteChanged();
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
                    ApplyCommand.OnCanExecuteChanged();
                    GetNewKeyCommand.OnCanExecuteChanged();

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
                    ApplyCommand.OnCanExecuteChanged();
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
                }
            }

            private RelayCommand _applyCommand;

            public RelayCommand ApplyCommand => _applyCommand ?? (_applyCommand = new RelayCommand(o => {
                ValuesStorage.Remove(AppKeyRevokedKey);
                AppKeyHolder.Instance.SetKey(Value);

                ShowMessage("Now app will be restarted, but it shouldn’t take long. Thanks again for your support!\n\n[i]Please, don’t share your key, otherwise it might get compromised.[/i]", "Thank You!", MessageBoxButton.OK);
                WindowsHelper.RestartCurrentApplication();
            }, o => IsValueAcceptable && !CheckingInProgress && !InternetConnectionRequired && !string.IsNullOrWhiteSpace(Value)));

            private RelayCommand _getNewKeyCommand;

            public RelayCommand GetNewKeyCommand => _getNewKeyCommand ?? (_getNewKeyCommand = new RelayCommand(o => {
                Process.Start("http://acstuff.ru/app/cm/key/get");
            }, o => !IsValueAcceptable || string.IsNullOrWhiteSpace(Value)));

            public IEnumerable GetErrors(string propertyName) {
                return propertyName == nameof(Value) ? (string.IsNullOrWhiteSpace(Value) ? new[] { "Required value" } :
                    IsValueAcceptable ? null : new[] { InternetConnectionRequired ? "Internet connection is required" : "Key is invalid" }) : null;
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
