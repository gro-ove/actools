using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AcManager.Properties;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.Dialogs {
    public partial class ShowroomCreateDialog {
        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() {
                ResultId = ResultName = string.Empty;
            }

            private string _resultId;

            public string ResultId {
                get { return _resultId; }
                set {
                    if (Equals(value, _resultId)) return;
                    _resultId = AcStringValues.IdFromName(value.Trim());
                    OnPropertyChanged();

                    if (ResultName == null) {
                        ResultName = AcStringValues.NameFromId(value);
                    }
                }
            }

            private string _resultName;

            public string ResultName {
                get { return _resultName; }
                set {
                    if (Equals(value, _resultName)) return;
                    _resultName = value.Trim();
                    OnPropertyChanged();

                    if (ResultId == null) {
                        ResultId = AcStringValues.IdFromName(value);
                    }
                }
            }

            private string _panoramaFilename;

            public string PanoramaFilename {
                get { return _panoramaFilename; }
                set {
                    if (Equals(value, _panoramaFilename)) return;
                    _panoramaFilename = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }

            private bool _inShadow;

            public bool InShadow {
                get { return _inShadow; }
                set => Apply(value, ref _inShadow);
            }

            private ICommand _selectPanoramaFileCommand;

            public ICommand SelectPanoramaFileCommand => _selectPanoramaFileCommand ?? (_selectPanoramaFileCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.TexturesFilter,
                    FileName = PanoramaFilename,
                    Title = AppStrings.CreateShowroom_SelectPanorama_Title
                };

                if (dialog.ShowDialog() == true) {
                    PanoramaFilename = dialog.FileName;
                }
            }));

            private ICommand _createCommand;

            // TODO: async
            public ICommand CreateCommand => _createCommand ?? (_createCommand = new DelegateCommand(Create, () => PanoramaFilename != null && File.Exists(PanoramaFilename)));

            private void Create() {
                if (ResultId == string.Empty) {
                    ResultId = AcStringValues.IdFromName(ResultName);

                    if (ResultId == string.Empty) {
                        for (var i = 1; ; i++) {
                            if (i > 10000) throw new Exception();

                            var candidate = $"generated_showroom_{i}";
                            if (ShowroomsManager.Instance.GetById(candidate) != null) continue;

                            ResultId = candidate;
                            break;
                        }
                    }
                }

                if (ShowroomsManager.Instance.GetById(ResultId) != null) {
                    throw new InformativeException(ToolsStrings.Common_IdIsTaken, ToolsStrings.Common_IdIsTaken_Commentary);
                }

                if (ResultName == string.Empty) {
                    ResultName = AcStringValues.NameFromId(ResultId);
                }

                var location = ShowroomsManager.Instance.Directories?.GetLocation(ResultId, true) ?? string.Empty;
                FileUtils.EnsureDirectoryExists(Path.Combine(location, "ui"));

                new IniFile {
                    ["LIGHT"] = {
                        ["LOCK_SUN"] = @"1",
                        ["SUN_DIRECTION"] = @"1,1,0.8"
                    }
                }.Save(Path.Combine(location, "settings.ini"));

                File.WriteAllText(Path.Combine(location, @"ui", @"ui_showroom.json"), new JObject {
                    [@"author"] = SettingsHolder.Sharing.SharingName,
                    [@"name"] = ResultName,
                    [@"tags"] = new JArray {
                        InShadow ? @"in shadow" : @"lighted",
                        @"panorama based"
                    }
                }.ToString());

                try {
                    using (var unpackedKn5 = new MemoryStream()) {
                        using (var stream = new MemoryStream(BinaryResources.ShowroomPanoramaTemplate))
                        using (var archive = new ZipArchive(stream)) {
                            var entry = archive.GetEntry(@"0");
                            if (entry == null) {
                                throw new Exception("Unexpected exception, base model is missing");
                            }

                            using (var entryStream = entry.Open()) {
                                entryStream.CopyTo(unpackedKn5);
                            }
                        }

                        unpackedKn5.Position = 0;

                        var kn5 = Kn5.FromStream(unpackedKn5);
                        kn5.SetTexture("0", PanoramaFilename);
                        kn5.Nodes.First(x => x.Name == @"0" && x.NodeClass == Kn5NodeClass.Mesh).CastShadows = InShadow;

                        var material = kn5.Materials.Values.First(x => x.Name == @"0");

                        var ambient = material.GetPropertyByName("ksAmbient");
                        if (ambient != null) ambient.ValueA = InShadow ? 3f : 1f;

                        var diffuse = material.GetPropertyByName("ksDiffuse");
                        if (diffuse != null) diffuse.ValueA = InShadow ? 0f : 2f;

                        // TODO
                        kn5.Save(Path.Combine(location, ResultId + ".kn5"));
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t create showroom", e);
                }
            }
        }

        private readonly ViewModel _model;

        public ShowroomCreateDialog() {
            InitializeComponent();
            DataContext = _model = new ViewModel();
            Buttons = new[] { OkButton, CancelButton };

            OkButton.Command = new DelegateCommand(() => {
                try {
                    _model.CreateCommand.Execute(null);
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.CreateShowroom_CannotCreate, e);
                    return;
                }

                ResultId = _model.ResultId;
                CloseWithResult(MessageBoxResult.OK);
            }, () => _model.CreateCommand.CanExecute(null));
        }

        public string ResultId { get; private set; }
    }
}
