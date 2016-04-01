using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarSkinPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedCarSkinPageViewModel : SelectedAcObjectViewModel<CarSkinObject> {
            public CarObject Car { get; }

            public SelectedCarSkinPageViewModel(CarObject car, [NotNull] CarSkinObject acObject) : base(acObject) {
                Car = car;
            }

            private ICommand _updatePreviewCommand;
            public ICommand UpdatePreviewCommand => _updatePreviewCommand ?? (_updatePreviewCommand = new RelayCommand(o => {
                UpdatePreview();
            }, o => SelectedObject.Enabled));

            private void UpdatePreview() {
                new CarUpdatePreviewsDialog(Car, new[] { SelectedObject.Id },
                    SelectedCarPage.SelectedCarPageViewModel.GetAutoUpdatePreviewsDialogMode()).ShowDialog();
            }
        }

        private string _carId, _id;
        
        void IParametrizedUriContent.OnUri(Uri uri) {
            _carId = uri.GetQueryParam("CarId");
            if (_carId == null) throw new ArgumentException("Car ID is missing");

            _id = uri.GetQueryParam("Id");
            if (_id == null) throw new ArgumentException("ID is missing");
        }

        private CarObject _carObject;
        private CarSkinObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _carObject = await CarsManager.Instance.GetByIdAsync(_carId);
            if (_carObject == null) return;
            _object = await _carObject.SkinsManager.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _carObject = CarsManager.Instance.GetById(_carId);
            _object = _carObject?.SkinsManager.GetById(_id);
        }

        void ILoadableContent.Initialize() {
            if (_carObject == null) throw new ArgumentException("Can't find car with provided ID");
            if (_object == null) throw new ArgumentException("Can't find object with provided ID");

            InitializeAcObjectPage(_model = new SelectedCarSkinPageViewModel(_carObject, _object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewCommand, new KeyGesture(Key.P, ModifierKeys.Control))
            });
            InitializeComponent();
        }

        private SelectedCarSkinPageViewModel _model;
    }
}
