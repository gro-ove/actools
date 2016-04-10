using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using AcManager.Pages.Windows;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class AcRootDirectorySelector {
        private AcRootDirectorySelectorViewModel Model => (AcRootDirectorySelectorViewModel)DataContext;

        public AcRootDirectorySelector() {
            InitializeComponent();
            DataContext = new AcRootDirectorySelectorViewModel();

            Buttons = new[] {
                CreateExtraDialogButton(FirstFloor.ModernUI.Resources.Ok, new CombinedCommand(Model.ApplyCommand, new RelayCommand(o => {
                    new MainWindow().Show();
                    CloseWithResult(MessageBoxResult.OK);
                }))),
                CancelButton
            };
        }

        public class AcRootDirectorySelectorViewModel : NotifyPropertyChanged, INotifyDataErrorInfo {
            public bool FirstRun { get; private set; }

            public bool IsValueAcceptable {
                get { return _isValueAcceptable; }
                private set {
                    if (value == _isValueAcceptable) return;
                    _isValueAcceptable = value;
                    OnPropertyChanged();
                    OnErrorsChanged(nameof(Value));
                    ApplyCommand.OnCanExecuteChanged();
                }
            }
            
            private bool _isValueAcceptable;

            private string _value;

            public string Value {
                get { return _value; }
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                    OnErrorsChanged();
                    IsValueAcceptable = AcRootDirectory.CheckDirectory(_value);
                }
            }

            private RelayCommand _applyCommand;

            public RelayCommand ApplyCommand => _applyCommand ?? (_applyCommand = new RelayCommand(o => {
                AcRootDirectory.Instance.Value = Value;
            }, o => IsValueAcceptable));

            public AcRootDirectorySelectorViewModel() {
                FirstRun = ValuesStorage.GetBool("_second_run") == false;
                if (FirstRun) {
                    ValuesStorage.Set("_second_run", true);
                }

                Value = AcRootDirectory.Instance.IsReady ? AcRootDirectory.Instance.Value : AcRootDirectory.TryToFind();
            }

            public IEnumerable GetErrors(string propertyName) {
                return propertyName == nameof(Value) ? (string.IsNullOrWhiteSpace(Value) ? new[] { "Required value" } :
                        IsValueAcceptable ? null : new[] { "Folder is unacceptable" }) : null;
            }

            public bool HasErrors => string.IsNullOrWhiteSpace(Value) || !IsValueAcceptable;
            public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

            public void OnErrorsChanged([CallerMemberName] string propertyName = null) {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }

        private void Button_OnClick(object sender, RoutedEventArgs e) {
            var dialog = new FolderBrowserDialog {
                ShowNewFolderButton = false,
                SelectedPath = Model.Value
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Model.Value = dialog.SelectedPath;
            }
        }
    }
}
