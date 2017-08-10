using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    // Now, here I’m doing my favourite thing, which is the stretching an own on a globe. Most of UI stuff for AcObjects
    // I made before relies on a fact that most (all) of them are AcCommonObject — objects with files background, so there are stuff like
    // removal, copying, cloning, all that. Here, with remote content, all that stuff is unavailable. So, I copied required bits.

    public partial class SelectedRemoteCarSetupPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public class ViewModel : SelectedAcObjectViewModel {
            public CarObject Car { get; }
            public RemoteCarSetupObject SelectedObject { get; }

            public CarSetupValues SetupValues { get; }

            public ViewModel(CarObject car, [NotNull] RemoteCarSetupObject acObject, [CanBeNull] ViewModel previous) {
                Car = car;
                SelectedObject = acObject;
                SetupValues = new CarSetupValues(car, acObject, previous?.SetupValues);
            }

            public void Unload() {
                SetupValues.Dispose();
            }

            private DelegateCommand _shareCommand;

            public DelegateCommand ShareCommand => _shareCommand ?? (_shareCommand = new DelegateCommand(() => {
                var link = $@"http://acstuff.ru/s/q:thesetupmarket/setup?id={SelectedObject.Id}";
                SharingUiHelper.ShowShared("The Setup Market link", link);
            }));
        }

        private string _carId, _id;
        private CarSetupsRemoteSource _source;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _carId = uri.GetQueryParam("CarId");
            if (_carId == null) throw new ArgumentException(ToolsStrings.Common_CarIdIsMissing);

            _id = uri.GetQueryParam("Id");
            if (_id == null) throw new ArgumentException(ToolsStrings.Common_IdIsMissing);

            _source = uri.GetQueryParamEnum<CarSetupsRemoteSource>("RemoteSource");
        }

        private CarObject _carObject;
        private RemoteCarSetupObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            do {
                _carObject = await CarsManager.Instance.GetByIdAsync(_carId);
                if (_carObject == null) {
                    _object = null;
                    return;
                }

                await Task.Run(() => {
                    _carObject.AcdData?.GetIniFile("car.ini");
                    _carObject.AcdData?.GetIniFile("setup.ini");
                    _carObject.AcdData?.GetIniFile("tyres.ini");
                }, cancellationToken);

                _object = RemoteSetupsManager.GetManager(_source, _carId)?.GetById(_id);
            } while (_carObject.Outdated);
        }

        void ILoadableContent.Load() {
            do {
                _carObject = CarsManager.Instance.GetById(_carId);
                if (_carObject == null) {
                    _object = null;
                    return;
                }

                _object = RemoteSetupsManager.GetManager(_source, _carId)?.GetById(_id);
            } while (_carObject.Outdated);
        }

        void ILoadableContent.Initialize() {
            if (_carObject == null) throw new ArgumentException(AppStrings.Common_CannotFindCarById);
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            SetModel();
            InitializeComponent();
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_carObject, _object, _model));
            InputBindings.AddRange(new InputBinding[] {
                new InputBinding(_model.SelectedObject.ViewInBrowserCommand, new KeyGesture(Key.F, ModifierKeys.Control)),
                new InputBinding(_model.SelectedObject.CopyUrlCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(_model.SelectedObject.InstallCommand, new KeyGesture(Key.S, ModifierKeys.Control)),
                new InputBinding(_model.SelectedObject.InstallCommand, new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Alt)) {
                    CommandParameter = CarSetupObject.GenericDirectory
                },
            });
        }

        public RemoteCarSetupObject SelectedAcObject { get; private set; }

        protected void InitializeAcObjectPage([NotNull] ViewModel model) {
            SelectedAcObject = model.SelectedObject;
            InputBindings.Clear();
            InputBindings.AddRange(new InputBinding[] { });
            DataContext = model;

            if (!_set) {
                _set = true;
                Loaded += OnLoaded;
                Unloaded += OnUnloaded;
            }

            UpdateBindingsLaterAsync().Forget();
        }

        private async Task UpdateBindingsLaterAsync() {
            await Task.Delay(1);
            InputBindingBehavior.UpdateBindings(this);
        }

        private bool _set, _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            ((ViewModel)DataContext).Unload();
        }

        public bool ImmediateChange(Uri uri) {
            var carId = uri.GetQueryParam("CarId");
            if (carId == null || carId != _carId) return false;

            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var source = uri.GetQueryParamEnum<CarSetupsRemoteSource>("RemoteSource");
            if (source == CarSetupsRemoteSource.None) return false;

            var obj = RemoteSetupsManager.GetManager(_source, carId)?.GetById(id);
            if (obj == null) return false;

            _object = obj;
            SetModel();
            return true;
        }

        private ViewModel _model;
    }
}
