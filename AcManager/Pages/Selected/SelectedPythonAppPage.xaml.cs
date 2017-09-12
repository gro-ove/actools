using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace AcManager.Pages.Selected {
    public partial class SelectedPythonAppPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public class ViewModel : SelectedAcObjectViewModel<PythonAppObject> {
            public PythonAppConfigs Configs { get; }

            public ViewModel([NotNull] PythonAppObject acObject) : base(acObject) {
                IsActivated = AcSettingsHolder.Python.IsActivated(SelectedObject.Id);
                AcSettingsHolder.Python.PropertyChanged += OnPythonPropertyChanged;
                Configs = acObject.GetAppConfigs();
            }

            private void OnPythonPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(PythonSettings.Apps)) {
                    IsActivated = AcSettingsHolder.Python.IsActivated(SelectedObject.Id);
                }
            }

            public override void Unload() {
                Configs.Dispose();
                AcSettingsHolder.Python.PropertyChanged -= OnPythonPropertyChanged;
                base.Unload();
            }

            public PythonAppsManager Manager => PythonAppsManager.Instance;

            private CommandBase _testCommand;

            public ICommand TestCommand => _testCommand ?? (_testCommand = new DelegateCommand(() => {
                //var car = CarsManager.Instance.GetDefault();
                //CarOpenInShowroomDialog.Run(car, car?.SelectedSkin?.Id, SelectedObject.AcId);
            }));

            private bool _isActivated;

            public bool IsActivated {
                get { return _isActivated; }
                set {
                    if (Equals(value, _isActivated)) return;
                    _isActivated = value;
                    OnPropertyChanged();
                    AcSettingsHolder.Python.SetActivated(SelectedObject.Id, value);
                }
            }
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private PythonAppObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await PythonAppsManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = PythonAppsManager.Instance.GetById(_id);
        }

        public bool ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = PythonAppsManager.Instance.GetById(id);
            if (obj == null) return false;

            _id = id;
            _object = obj;
            SetModel();
            return true;
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);
            SetModel();
            InitializeComponent();
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control))
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {}

        private void OnFileButtonClick(object sender, RoutedEventArgs e) {
            if (!(((FrameworkElement)sender).DataContext is PythonAppConfigFileValue entry)) return;

            try {
                if (entry.DirectoryMode) {
                    var dialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        SelectedPath = entry.Value
                    };

                    if (dialog.ShowDialog() == DialogResult.OK) {
                        entry.Value = dialog.SelectedPath;
                    }
                } else {
                    var directory = Path.GetDirectoryName(entry.Value);
                    if (string.IsNullOrWhiteSpace(directory)) {
                        directory = _model.SelectedObject.Location;
                    }

                    var dialog = new OpenFileDialog {
                        Filter = entry.Filter ?? DialogFilterPiece.AllFiles.WinFilter,
                        InitialDirectory = directory,
                        FileName = Path.GetFileName(entry.Value) ?? ""
                };

                    if (dialog.ShowDialog() == true) {
                        entry.Value = dialog.FileName;
                    }
                }
            } catch (ArgumentException ex) {
                NonfatalError.Notify("Can’t open dialog", ex);
            }
        }
    }
}
