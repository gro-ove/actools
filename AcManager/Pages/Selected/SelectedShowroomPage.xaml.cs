using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Pages.Dialogs;
using AcManager.Properties;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedShowroomPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<ShowroomObject> {
            public ViewModel([NotNull] ShowroomObject acObject) : base(acObject) {}

            private ICommand _updatePreviewCommand;
            public ICommand UpdatePreviewCommand => _updatePreviewCommand ?? (_updatePreviewCommand = new DelegateCommand(UpdatePreview, () => SelectedObject.Enabled));

            private async void UpdatePreview() {
                await Task.Run(() => {
                    const string sphereId = "__sphere";
                    var sphereDirectory = CarsManager.Instance.Directories.GetLocation(sphereId, true);

                    try {
                        using (CarsManager.Instance.IgnoreChanges()) {
                            if (!Directory.Exists(sphereDirectory)) {
                                Directory.CreateDirectory(sphereDirectory);

                                using (var stream = new MemoryStream(BinaryResources.ShowroomPreviewSphere))
                                using (var archive = new ZipArchive(stream)) {
                                    archive.ExtractToDirectory(sphereDirectory);
                                }
                            }

                            var resultDirectory = Showroom.Shot(new Showroom.ShotProperties {
                                AcRoot = AcRootDirectory.Instance.Value,
                                CarId = sphereId,
                                ShowroomId = SelectedObject.Id,
                                SkinIds = new[] { @"0" },
                                Filter = @"default",
                                Mode = Showroom.ShotMode.Fixed,
                                UseBmp = true,
                                DisableWatermark = true,
                                DisableSweetFx = true,
                                MaximizeVideoSettings = true,
                                SpecialResolution = false,
                                Fxaa = true,
                                FixedCameraPosition = @"-1.8,0.8,3",
                                FixedCameraLookAt = @"0,0.5,0",
                                FixedCameraFov = 40,
                            });

                            if (resultDirectory == null) {
                                throw new Exception(AppStrings.Common_ShootingCancelled);
                            }

                            ImageUtils.ApplyPreview(Path.Combine(resultDirectory, "0.bmp"), SelectedObject.PreviewImage, true, null);
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify(AppStrings.Showroom_CannotUpdatePreview, e);
                    } finally {
                        if (Directory.Exists(sphereDirectory)) {
                            Directory.Delete(sphereDirectory, true);
                        }
                    }
                });
            }

            private ICommand _createNewShowroomCommand;

            public ICommand CreateNewShowroomCommand => _createNewShowroomCommand ?? (_createNewShowroomCommand = new DelegateCommand(() => {
                CreateNewShowroom();
            }));

            private void CreateNewShowroom() {
                var dialog = new ShowroomCreateDialog();
                dialog.ShowDialog();
                if (dialog.IsResultOk && dialog.ResultId != null) {
                    // select resultid?
                }
            }
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private ShowroomObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await ShowroomsManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = ShowroomsManager.Instance.GetById(_id);
        }

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.CreateNewShowroomCommand, new KeyGesture(Key.N, ModifierKeys.Control)),
                new InputBinding(_model.SelectedObject.ToggleSoundCommand, new KeyGesture(Key.I, ModifierKeys.Control)),
            });
            InitializeComponent();
        }

        private ViewModel _model;
    }
}
