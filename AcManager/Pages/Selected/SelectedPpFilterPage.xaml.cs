using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedPpFilterPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedPpFilterPageViewModel : SelectedAcObjectViewModel<PpFilterObject> {
            public SelectedPpFilterPageViewModel([NotNull] PpFilterObject acObject) : base(acObject) { }

            public PpFiltersManager Manager => PpFiltersManager.Instance;
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private PpFilterObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await PpFiltersManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = PpFiltersManager.Instance.GetById(_id);
        }

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(new SelectedPpFilterPageViewModel(_object));
            /*InputBindings.AddRange(new[] {
                new InputBinding(Model.CreateNewFontCommand, new KeyGesture(Key.N, ModifierKeys.Control)),
                new InputBinding(Model.UsingsRescanCommand, new KeyGesture(Key.U, ModifierKeys.Control)),
                new InputBinding(Model.DisableUnusedCommand, new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift))
            });*/
            InitializeComponent();
        }

        private SelectedPpFilterPageViewModel Model => (SelectedPpFilterPageViewModel)DataContext;
    }
}
