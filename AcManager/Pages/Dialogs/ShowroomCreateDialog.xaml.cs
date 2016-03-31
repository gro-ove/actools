using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AcManager.Properties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.Dialogs {
    /// <summary>
    /// Interaction logic for ShowroomCreateDialog.xaml
    /// </summary>
    public partial class ShowroomCreateDialog {
        internal class CreatingException : Exception {
            public CreatingException(string msg) : base(msg) {
            }
        }

        public class ShowroomCreateDialogViewModel : NotifyPropertyChanged {
            public ShowroomCreateDialogViewModel() {
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
                }
            }

            private bool _inShadow;

            public bool InShadow {
                get { return _inShadow; }
                set {
                    if (Equals(value, _inShadow)) return;
                    _inShadow = value;
                    OnPropertyChanged();
                }
            }

            private ICommand _selectPanoramaFileCommand;

            public ICommand SelectPanoramaFileCommand => _selectPanoramaFileCommand ?? (_selectPanoramaFileCommand = new RelayCommand(o => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.TextureFilter,
                    FileName = PanoramaFilename,
                    Title = "Select a Panorama File"
                };

                if (dialog.ShowDialog() == true) {
                    PanoramaFilename = dialog.FileName;
                }
            }));

            private ICommand _createCommand;

            public ICommand CreateCommand => _createCommand ?? (_createCommand = new RelayCommand(o => {
                // TODO: async? somehow?
                Create();
            }, o => PanoramaFilename != null && File.Exists(PanoramaFilename)));

            private void Create() {
                if (ResultId == string.Empty) {
                    ResultId = AcStringValues.IdFromName(ResultName);

                    if (ResultId == string.Empty) {
                        for (var i = 1; ; i++) {
                            if (i > 10000) {
                                throw new CreatingException("Error 10000");
                            }

                            var candidate = $"generated_showroom_{i}";
                            if (ShowroomsManager.Instance.GetById(candidate) != null) continue;

                            ResultId = candidate;
                            break;
                        }
                    }
                }

                if (ShowroomsManager.Instance.GetById(ResultId) != null) {
                    throw new CreatingException("ID is taken");
                }

                if (ResultName == string.Empty) {
                    ResultName = AcStringValues.NameFromId(ResultId);
                }

                var location = ShowroomsManager.Instance.Directories.GetLocation(ResultId, true);
                FileUtils.EnsureDirectoryExists(Path.Combine(location, "ui"));

                new IniFile {
                    ["LIGHT"] = {
                        ["LOCK_SUN"] = "1",
                        ["SUN_DIRECTION"] = "1,1,0.8"
                    }
                }.Save(Path.Combine(location, "settings.ini"));

                File.WriteAllText(Path.Combine(location, "ui", "ui_showroom.json"), new JObject {
                    // TODO: add user name to settings and automatically add him here as an author?
                    ["name"] = ResultName,
                    ["tags"] = new JArray {
                        InShadow ? "in shadow" : "lighted",
                        "panorama based"
                    }
                }.ToString());

                using (var unpackedKn5 = new MemoryStream()) {
                    using (var stream = new MemoryStream(BinaryResources.ShowroomPanoramaTemplate))
                    using (var archive = new ZipArchive(stream))
                    using (var entry = archive.GetEntry("0").Open()) {
                        entry.CopyTo(unpackedKn5);
                    }

                    unpackedKn5.Position = 0;

                    var kn5 = Kn5.FromStream(unpackedKn5);
                    kn5.SetTexture("0", PanoramaFilename);
                    kn5.Nodes.First(x => x.Name == "0" && x.NodeClass == Kn5NodeClass.Mesh).CastShadows = InShadow;

                    var material = kn5.Materials.Values.First(x => x.Name == "0");
                    material.GetPropertyByName("ksAmbient").ValueA = InShadow ? 3f : 1f;
                    material.GetPropertyByName("ksDiffuse").ValueA = InShadow ? 0f : 2f;

                    kn5.Save(Path.Combine(location, ResultId + ".kn5"));
                }
            }
        }

        private readonly ShowroomCreateDialogViewModel _model;

        public ShowroomCreateDialog() {
            InitializeComponent();
            DataContext = _model = new ShowroomCreateDialogViewModel();
            Buttons = new[] { OkButton, CancelButton };

            OkButton.Command = new RelayCommand(o => {
                try {
                    _model.CreateCommand.Execute(null);
                } catch (CreatingException ex) {
                    NonfatalError.Notify(ex.Message);
                    return;
                }

                ResultId = _model.ResultId;
                CloseWithResult(MessageBoxResult.OK);
            }, _model.CreateCommand.CanExecute);
        }

        public string ResultId { get; private set; }
    }
}
