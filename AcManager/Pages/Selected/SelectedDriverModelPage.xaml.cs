using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.CustomShowroom;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Selected {
    public partial class SelectedDriverModelPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public class ViewModel : SelectedAcObjectViewModel<DriverModelObject> {
            public ViewModel([NotNull] DriverModelObject acObject) : base(acObject) { }

            public DriverModelsManager Manager => DriverModelsManager.Instance;

            private CommandBase _usingsRescanCommand;

            public ICommand UsingsRescanCommand => _usingsRescanCommand ?? (_usingsRescanCommand = new AsyncCommand(async () => {
                List<string> missing;
                using (var waiting = new WaitingDialog()) {
                    missing = await DriverModelsManager.Instance.UsingsRescan(waiting, waiting.CancellationToken);
                }

                if (missing?.Any() == true) {
                    ModernDialog.ShowMessage(missing.JoinToString(@", "), AppStrings.DriverModel_MissingDriverModels, MessageBoxButton.OK);
                }
            }));

            private CommandBase _disableUnusedCommand;

            public ICommand DisableUnusedCommand => _disableUnusedCommand ?? (_disableUnusedCommand = new AsyncCommand(async () => {
                using (var waiting = new WaitingDialog(ToolsStrings.Common_Scanning)) {
                    await DriverModelsManager.Instance.UsingsRescan(waiting, waiting.CancellationToken);
                    if (waiting.CancellationToken.IsCancellationRequested) return;

                    waiting.Title = null;
                    var toDisable = DriverModelsManager.Instance.LoadedOnly.Where(x => x.Enabled && x.UsingsCarsIds.Length == 0).ToList();
                    foreach (var font in toDisable) {
                        waiting.Report(string.Format(AppStrings.Common_DisablingFormat, font.DisplayName));
                        font.ToggleCommand.Execute(null);
                        await Task.Delay(500, waiting.CancellationToken);
                        if (waiting.CancellationToken.IsCancellationRequested) break;
                    }
                }
            }));

            private AsyncCommand _openInCustomShowroomCommand;

            public AsyncCommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ??
                    (_openInCustomShowroomCommand = new AsyncCommand(() => CustomShowroomWrapper.StartAsync(SelectedObject.Location)));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private DriverModelObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await DriverModelsManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = DriverModelsManager.Instance.GetById(_id);
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

            var obj = DriverModelsManager.Instance.GetById(id);
            if (obj == null) return false;

            _id = id;
            _object = obj;
            SetModel();
            return true;
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(Model.UsingsRescanCommand, new KeyGesture(Key.U, ModifierKeys.Control)),
                new InputBinding(Model.DisableUnusedCommand, new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift))
            });
        }

        private ViewModel Model => (ViewModel)DataContext;
    }
}
