using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedPythonAppPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedPythonAppPageViewModel : SelectedAcObjectViewModel<PythonAppObject> {
            public SelectedPythonAppPageViewModel([NotNull] PythonAppObject acObject) : base(acObject) {
                IsActivated = AcSettingsHolder.Python.IsActivated(SelectedObject.Id);
                AcSettingsHolder.Python.PropertyChanged += Python_PropertyChanged;
            }

            private void Python_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(AcSettingsHolder.PythonSettings.Apps)) {
                    IsActivated = AcSettingsHolder.Python.IsActivated(SelectedObject.Id);
                }
            }

            public override void Unload() {
                AcSettingsHolder.Python.PropertyChanged -= Python_PropertyChanged;
                base.Unload();
            }

            public PythonAppsManager Manager => PythonAppsManager.Instance;

            private RelayCommand _testCommand;

            public RelayCommand TestCommand => _testCommand ?? (_testCommand = new RelayCommand(o => {
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
                throw new Exception("ID is missing");
            }
        }

        private PythonAppObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await PythonAppsManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = PythonAppsManager.Instance.GetById(_id);
        }

        private SelectedPythonAppPageViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(_model = new SelectedPythonAppPageViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control))
            });
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {}
    }
}
