using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Wheel_ForceFeedback {
        [CanBeNull]
        private LutLibraryWrapper _wrapper;

        public ViewModel Model => (ViewModel)DataContext;

        public AcSettingsControls_Wheel_ForceFeedback() {
            try {
                _wrapper = LutLibraryWrapper.Initialize();
            } catch (Exception) {
                // ignored
            }

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null) {
                mainWindow.Drop += OnMainWindowDrop;
            }

            DataContext = new ViewModel(_wrapper);
            InitializeComponent();

            if (_wrapper == null) {
                ImportCsvButton.Visibility = Visibility.Collapsed;
            } else {
                LutLibraryMessage.Visibility = Visibility.Collapsed;
            }

            this.OnActualUnload(() => {
                if (mainWindow != null) {
                    mainWindow.Drop -= OnMainWindowDrop;
                }
            });

            this.AddWidthCondition(900).Add(v => Grid.Columns = v ? 2 : 1);
        }

        private void OnMainWindowDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(DataFormats.UnicodeText)) return;

            Focus();
            var inputFile = e.GetInputFiles().FirstOrDefault(x => x.EndsWith(@".csv", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(@".lut", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(x), @"LUTLibrary.dll", StringComparison.OrdinalIgnoreCase));
            if (inputFile != null) {
                e.Handled = true;

                if (!string.Equals(Path.GetFileName(inputFile), @"LUTLibrary.dll", StringComparison.OrdinalIgnoreCase)) {
                    Model.Import(inputFile);
                } else {
                    LutLibraryWrapper.Install(inputFile);
                }
            }
        }

        public class LutLibraryWrapper {
            private readonly Assembly _assembly;

            private LutLibraryWrapper(string library) {
                if (FileUtils.Unblock(library)) {
                    Logging.Warning("Library unblocked");
                }

                _assembly = Assembly.LoadFrom(library);

                try {
                    Test();
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            [CanBeNull]
            public Lut ToLut(string csvFilename) {
                var lut = _assembly.GetType("LUTLibrary.LUT");
                var lutInstance = _assembly.GetType("LUTLibrary.LUTReader").GetMethod("Read")?.Invoke(null, new object[]{ csvFilename });
                lut.GetMethod("getMaxDeltaX")?.Invoke(lutInstance, new object[0]);
                Activator.CreateInstance(_assembly.GetType("LUTLibrary.LUTCalculator"), lutInstance);
                var forceList = (ArrayList)lut.GetMethod("getForce")?.Invoke(lutInstance, new object[0]);
                var lut2List = (ArrayList)lut.GetMethod("getLut2")?.Invoke(lutInstance, new object[0]);
                var force = (double?)(int?)lut.GetMethod("getMaxForce")?.Invoke(lutInstance, new object[0]) ?? 0d;
                return forceList?.OfType<int>().Zip(lut2List?.OfType<double>() ?? new double[0],
                        (x, i) => new LutPoint(x / force, Math.Round(i / force, 3))).ToLut();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void Test() {
                var lut = _assembly.GetType("LUTLibrary.LUT");
                Logging.Debug((int?)lut.GetMethod("getMaxForce")?.Invoke(Activator.CreateInstance(lut, new object[0]), new object[0]));
            }

            [MethodImpl(MethodImplOptions.NoInlining), CanBeNull]
            public static LutLibraryWrapper Initialize() {
                foreach (var candidate in new[] {
                    Path.Combine(MainExecutingFile.Directory, "LUTLibrary.dll"),
                    FilesStorage.Instance.GetFilename("Plugins", "LUTLibrary.dll")
                }) {
                    if (File.Exists(candidate)) {
                        Logging.Debug(candidate);
                        return new LutLibraryWrapper(candidate);
                    }
                }

                return null;
            }

            public static void Install(string inputFile) {
                try {
                    var destination = FilesStorage.Instance.GetFilename("Plugins", "LUTLibrary.dll");
                    if (File.Exists(destination) && new FileInfo(destination).Length == new FileInfo(inputFile).Length) return;

                    File.Copy(inputFile, destination, true);
                    Toast.Show("LUTLibrary installed", "One thing left now is to restart CM. Do it now?", WindowsHelper.RestartCurrentApplication);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t install LUTLibrary", e);
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            [CanBeNull]
            private readonly LutLibraryWrapper _wrapper;

            internal ViewModel(LutLibraryWrapper wrapper) {
                _wrapper = wrapper;
            }

            public void SwitchFfPostProcessLutName(string name) {
                FfPostProcess.Enabled = true;
                FfPostProcess.Type = FfPostProcess.Types.GetById("LUT");
                FfPostProcess.LutName = name;
            }

            private void ImportCsv(string filename) {
                try {
                    if (_wrapper == null) {
                        throw new InformativeException("Can’t import CSV-file",
                                "LUTLibrary.dll missing or can’t be loaded.");
                    }

                    var name = Prompt.Show("Choose a name for the new LUT setting:", "New LUT",
                            Path.GetFileNameWithoutExtension(filename) + ".lut", "?", required: true, maxLength: 120);
                    if (string.IsNullOrWhiteSpace(name)) return;

                    if (!name.EndsWith(@".lut", StringComparison.OrdinalIgnoreCase)) {
                        name += @".lut";
                    }

                    var lutData = new LutDataFile();
                    var lut = _wrapper.ToLut(filename);
                    if (lut == null) {
                        throw new Exception(@"Expected field or method is missing");
                    }

                    lutData.Values.AddRange(lut);
                    lutData.Save(Path.Combine(AcPaths.GetDocumentsCfgDirectory(), name));
                    SwitchFfPostProcessLutName(name);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t import CSV-file", e);
                }
            }

            private void ImportLut(string filename) {
                try {
                    string name;
                    if (!FileUtils.ArePathsEqual(Path.GetDirectoryName(filename), AcPaths.GetDocumentsCfgDirectory())) {
                        name = Prompt.Show("Choose a name for the new LUT setting:", "New LUT",
                                Path.GetFileNameWithoutExtension(filename) + ".lut", "?", required: true, maxLength: 120);
                        if (string.IsNullOrWhiteSpace(name)) return;

                        if (!name.EndsWith(".lut", StringComparison.OrdinalIgnoreCase)) {
                            name += ".lut";
                        }

                        File.Copy(filename, Path.Combine(AcPaths.GetDocumentsCfgDirectory(), name), true);
                    } else {
                        name = Path.GetFileName(filename);
                    }

                    SwitchFfPostProcessLutName(name);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t import LUT-file", e);
                }
            }

            public void Import(string filename) {
                if (filename.EndsWith(@".csv", StringComparison.OrdinalIgnoreCase)) {
                    ImportCsv(filename);
                } else {
                    ImportLut(filename);
                }
            }

            private DelegateCommand _importLutCommand;

            public DelegateCommand ImportLutCommand => _importLutCommand ?? (_importLutCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.LutFilter,
                    Title = "Select LUT-file",
                    InitialDirectory = AcPaths.GetDocumentsCfgDirectory(),
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() == true) {
                    Import(dialog.FileName);
                }
            }));

            private DelegateCommand _importCsvCommand;

            public DelegateCommand ImportCsvCommand => _importCsvCommand ?? (_importCsvCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.CsvFilter,
                    Title = "Select CSV-file From WheelCheck",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() == true) {
                    Import(dialog.FileName);
                }
            }));

            public ControlsSettings Controls => AcSettingsHolder.Controls;

            public SystemSettings System => AcSettingsHolder.System;

            public FfPostProcessSettings FfPostProcess => AcSettingsHolder.FfPostProcess;
        }
    }
}
