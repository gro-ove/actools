using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.Starters;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedReplayPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedReplayPageViewModel : SelectedAcObjectViewModel<ReplayObject> {
            public SelectedReplayPageViewModel([NotNull] ReplayObject acObject) : base(acObject) { }

            private ICommand _playCommand;

            public ICommand PlayCommand => _playCommand ?? (_playCommand = new RelayCommand(o => {
                using (ReplaysExtensionSetter.OnlyNewIfEnabled()) {
                    Game.Start(AcsStarterFactory.Create(),
                            new Game.StartProperties(new Game.ReplayProperties {
                                Name = SelectedObject.Id,
                                TrackId = SelectedObject.TrackId,
                                TrackConfiguration = SelectedObject.TrackConfiguration
                            }));
                }
            }, o => SelectedObject.Enabled));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private ReplayObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await ReplaysManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = ReplaysManager.Instance.GetById(_id);
        }

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(_model = new SelectedReplayPageViewModel(_object));
            InitializeComponent();
        }

        private SelectedReplayPageViewModel _model;
    }
}
