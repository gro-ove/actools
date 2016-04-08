using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Pages.Windows;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public class CombinedCommand : CommandBase {
        private readonly ICommand _first;
        private readonly ICommand _second;

        public CombinedCommand(ICommand first, ICommand second) {
            _first = first;
            _second = second;
            WeakEventManager<ICommand, EventArgs>.AddHandler(first, "CanExecuteChanged", Handler);
            WeakEventManager<ICommand, EventArgs>.AddHandler(second, "CanExecuteChanged", Handler);
        }

        private void Handler(object sender, EventArgs eventArgs) {
            OnCanExecuteChanged();
        }

        protected override void OnExecute(object parameter) {
            _first.Execute(parameter);
            _second.Execute(parameter);
        }

        public override bool CanExecute(object parameter) {
            return _first.CanExecute(parameter) && _second.CanExecute(parameter);
        }
    }

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

        public class AcRootDirectorySelectorViewModel : NotifyPropertyChanged {
            public bool FirstRun { get; private set; }

            public bool IsValueAcceptable {
                get { return _isValueAcceptable; }
                private set {
                    if (value == _isValueAcceptable) return;
                    _isValueAcceptable = value;
                    OnPropertyChanged();
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
