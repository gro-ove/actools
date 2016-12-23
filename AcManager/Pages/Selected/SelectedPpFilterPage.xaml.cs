using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
// using AcManager.Tools.TextEditing;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedPpFilterPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<PpFilterObject> {
            public ViewModel([NotNull] PpFilterObject acObject) : base(acObject) { }

            public PpFiltersManager Manager => PpFiltersManager.Instance;

            private CommandBase _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(() => {
                var data = SelectedObject.Content ?? FileUtils.ReadAllText(SelectedObject.Location);
                return SharingUiHelper.ShareAsync(SharedEntryType.PpFilter, SelectedObject.Name, null, data);
            }));

            private CommandBase _testCommand;

            public ICommand TestCommand => _testCommand ?? (_testCommand = new DelegateCommand(() => {
                var car = CarsManager.Instance.GetDefault();
                CarOpenInShowroomDialog.Run(car, car?.SelectedSkin?.Id, SelectedObject.AcId);
            }));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private PpFilterObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await PpFiltersManager.Instance.GetByIdAsync(_id);
            _object?.PrepareForEditing();
        }

        void ILoadableContent.Load() {
            _object = PpFiltersManager.Instance.GetById(_id);
            _object?.PrepareForEditing();
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
            });
            InitializeComponent();

            /*TextEditor.SetAsIniEditor(v => { _object.Content = v; });
            TextEditor.SetDocument(_object.Content);
            _object.PropertyChanged += SelectedObject_PropertyChanged;*/
        }

        /*private void SelectedObject_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (TextEditor.IsBusy()) return;
            if (e.PropertyName == nameof(_object.Content)) {
                TextEditor.SetDocument(_object.Content);
            }
        }*/

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            //_object.PropertyChanged -= SelectedObject_PropertyChanged;
        }
    }
}
