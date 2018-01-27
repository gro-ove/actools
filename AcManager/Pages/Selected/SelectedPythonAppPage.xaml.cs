using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedPythonAppPage : ILoadableContent, IParametrizedUriContent, IImmediateContent, ILocalKeyBindings {
        public class ViewModel : SelectedAcObjectViewModel<PythonAppObject> {
            [NotNull]
            public PythonAppConfigs Configs { get; }

            [NotNull]
            public IReadOnlyList<PythonAppConfigKeyValue> KeyValues { get; }

            public ViewModel([NotNull] PythonAppObject acObject) : base(acObject) {
                IsActivated = AcSettingsHolder.Python.IsActivated(SelectedObject.Id);
                AcSettingsHolder.Python.PropertyChanged += OnPythonPropertyChanged;
                Configs = acObject.GetAppConfigs();
                KeyValues = Configs.SelectMany(x => x.Sections).SelectMany(x => x)
                                   .OfType<PythonAppConfigKeyValue>().ToList();
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

            private CommandBase _testCommand;

            public ICommand TestCommand => _testCommand ?? (_testCommand = new DelegateCommand(() => {
                //var car = CarsManager.Instance.GetDefault();
                //CarOpenInShowroomDialog.Run(car, car?.SelectedSkin?.Id, SelectedObject.AcId);
            }));

            private bool _isActivated;

            public bool IsActivated {
                get => _isActivated;
                set {
                    if (Equals(value, _isActivated)) return;
                    _isActivated = value;
                    OnPropertyChanged();
                    AcSettingsHolder.Python.SetActivated(SelectedObject.Id, value);
                }
            }

            private DelegateCommand _changeIconCommand;

            public DelegateCommand ChangeIconCommand => _changeIconCommand ?? (_changeIconCommand = new DelegateCommand(() => {
                AppIconEditor.RunAsync(SelectedObject).Forget();
            }));
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

        public SelectedPythonAppPage() {
            KeyBindingsController = new LocalKeyBindingsController(this);
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
            KeyBindingsController.Set(_model.KeyValues);
            InputBindings.AddRange(new[] {
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control))
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) { }

        private void OnIconClick(object sender, MouseButtonEventArgs e) {
            AppIconEditor.RunAsync(_model.SelectedObject).Forget();
        }

        private void OnWindowIconClick(object sender, MouseButtonEventArgs e) {
            AppIconEditor.RunAsync((PythonAppWindow)((FrameworkElement)sender).DataContext).Forget();
        }

        public LocalKeyBindingsController KeyBindingsController { get; }
    }
}