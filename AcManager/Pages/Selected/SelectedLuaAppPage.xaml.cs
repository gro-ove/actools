using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.UserControls;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using UIElement = System.Windows.UIElement;

namespace AcManager.Pages.Selected {
    public partial class SelectedLuaAppPage : ILoadableContent, IParametrizedUriContent, INotifyPropertyChanged, IImmediateContent {
        public class ViewModel : SelectedAcObjectViewModel<LuaAppObject> {
            public ViewModel([NotNull] LuaAppObject acObject) : base(acObject) { }
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private LuaAppObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await LuaAppsManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = LuaAppsManager.Instance.GetById(_id);
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            SetModel();
            InitializeComponent();
        }

        bool IImmediateContent.ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = LuaAppsManager.Instance.GetById(id);
            if (obj == null) return false;

            _id = id;
            _object = obj;
            SetModel();
            return true;
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object));
        }

        protected void OnVersionInfoBlockClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;

                if (Keyboard.Modifiers != ModifierKeys.Control) {
                    new ModernPopup {
                        Content = new PopupAuthor((ISelectedAcObjectViewModel)DataContext),
                        PlacementTarget = (UIElement)sender,
                        StaysOpen = false
                    }.IsOpen = true;
                } else if (_model.SelectedObject.Url != null) {
                    WindowsHelper.ViewInBrowser(_model.SelectedObject.Url);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}