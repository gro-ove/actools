using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedFontPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedFontPageViewModel : SelectedAcObjectViewModel<FontObject> {
            public SelectedFontPageViewModel([NotNull] FontObject acObject) : base(acObject) { }
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private FontObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await FontsManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = FontsManager.Instance.GetById(_id);
        }

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can't find object with provided ID");

            InitializeAcObjectPage(new SelectedFontPageViewModel(_object));
            InitializeComponent();
        }
    }
}
