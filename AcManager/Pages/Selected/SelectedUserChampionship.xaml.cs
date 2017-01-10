using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedUserChampionship : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public class ViewModel : SelectedAcObjectViewModel<UserChampionshipObject> {
            public ViewModel([NotNull] UserChampionshipObject acObject) : base(acObject) {
            }

            public override void Unload() {
                base.Unload();
            }

            public UserChampionshipsManager Manager => UserChampionshipsManager.Instance;
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        bool IImmediateContent.ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = UserChampionshipsManager.Instance.GetById(id);
            if (obj == null) return false;

            _id = id;
            _object = obj;
            SetModel();
            return true;
        }

        private UserChampionshipObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await UserChampionshipsManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = UserChampionshipsManager.Instance.GetById(_id);
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            SetModel();
            InitializeComponent();
        }

        private void SetModel() {
            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new InputBinding[] {
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {}
    }
}
