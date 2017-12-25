using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Pages.Dialogs;
using AcManager.Properties;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Processes;
using AcTools.Render.Base.Utils;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using SlimDX;

namespace AcManager.Pages.Selected {
    public partial class SelectedShowroomPage : ILoadableContent, IParametrizedUriContent {
        private static void FixVertices(Kn5Node node, Matrix parentMatrix) {
            if (node.NodeClass == Kn5NodeClass.Base) {
                var localMatrix = node.Transform.ToMatrix() * parentMatrix;
                foreach (var child in node.Children) {
                    FixVertices(child, localMatrix);
                }
            } else {
                var normal = Vector3.TransformNormal(Vector3.UnitY, Matrix.Invert(parentMatrix));
                for (var i = 0; i < node.Vertices.Length; i++) {
                    FixVertice(ref node.Vertices[i], normal);
                }
            }
        }

        private static void FixVertice(ref Kn5Node.Vertice v, Vector3 normalToSet) {
            v.Normal[0] = normalToSet.X;
            v.Normal[1] = normalToSet.Y;
            v.Normal[2] = normalToSet.Z;
        }

        public static async Task FixLightingAsync(ShowroomObject showroom) {
            try {
                using (WaitingDialog.Create("Updating model…")) {
                    await Task.Run(() => FixLighting(showroom));
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t update model", e);
            }
        }

        public static void FixLighting(ShowroomObject showroom) {
            var kn5Filename = showroom.Kn5Filename;
            using (var newFile = FileUtils.RecycleOriginal(kn5Filename)) {
                var kn5 = Kn5.FromFile(kn5Filename);
                FixVertices(kn5.RootNode, Matrix.Identity);
                kn5.Save(newFile.Filename);
            }
        }

        private const string PreviewSphereId = "__sphere";

        public static void RemovePreviewSphere() {
            var sphereDirectory = CarsManager.Instance.Directories.GetLocation(PreviewSphereId, true);
            try {
                if (Directory.Exists(sphereDirectory)) {
                    Directory.Delete(sphereDirectory, true);
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public static void UpdatePreview(ShowroomObject showroom, bool keepSphere) {
            var sphereDirectory = CarsManager.Instance.Directories.GetLocation(PreviewSphereId, true);

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
                        CarId = PreviewSphereId,
                        ShowroomId = showroom.Id,
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
                        TemporaryDirectory = SettingsHolder.Content.TemporaryFilesLocationValue,
                    });

                    if (resultDirectory == null) {
                        throw new Exception(AppStrings.Common_ShootingCancelled);
                    }

                    ImageUtils.ApplyPreview(Path.Combine(resultDirectory, "0.bmp"), showroom.PreviewImage, true, null);
                }
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Showroom_CannotUpdatePreview, e);
            } finally {
                if (!keepSphere) {
                    RemovePreviewSphere();
                }
            }
        }

        public class ViewModel : SelectedAcObjectViewModel<ShowroomObject> {
            public ViewModel([NotNull] ShowroomObject acObject) : base(acObject) {}

            private AsyncCommand _fixLightingCommand;

            public AsyncCommand FixLightingCommand => _fixLightingCommand ?? (_fixLightingCommand = new AsyncCommand(() => FixLightingAsync(SelectedObject), () => {
                var fi = new FileInfo(SelectedObject.Kn5Filename);
                return fi.Exists && fi.Length > 1e6;
            }));

            private AsyncCommand _updatePreviewCommand;

            public AsyncCommand UpdatePreviewCommand => _updatePreviewCommand ?? (_updatePreviewCommand = new AsyncCommand(UpdatePreviewAsync,
                    () => SelectedObject.Enabled));

            private Task UpdatePreviewAsync() {
                return Task.Run(() => UpdatePreview(SelectedObject, false));
            }

            private DelegateCommand _createNewShowroomCommand;

            public DelegateCommand CreateNewShowroomCommand => _createNewShowroomCommand ??
                    (_createNewShowroomCommand = new DelegateCommand(CreateNewShowroom));

            private static void CreateNewShowroom() {
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
