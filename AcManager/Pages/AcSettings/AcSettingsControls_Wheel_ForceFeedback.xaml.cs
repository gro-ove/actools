using System;
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
                Logging.Here();
                DisposeHelper.Dispose(ref _wrapper);
                if (mainWindow != null) {
                    mainWindow.Drop -= OnMainWindowDrop;
                }
            });
        }

        private void OnMainWindowDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(DataFormats.UnicodeText)) return;

            Focus();
            var inputFile = e.GetInputFiles().FirstOrDefault(x => x.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(".lut", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(x), "LUTLibrary.dll", StringComparison.OrdinalIgnoreCase));
            if (inputFile != null) {
                e.Handled = true;

                if (!string.Equals(Path.GetFileName(inputFile), "LUTLibrary.dll", StringComparison.OrdinalIgnoreCase)) {
                    Model.Import(inputFile);
                } else {
                    LutLibraryWrapper.Install(inputFile);
                }
            }
        }

        public class LutLibraryWrapper : IDisposable {
            private readonly string _library;

            private LutLibraryWrapper(string library) {
                if (FileUtils.Unblock(library)) {
                    Logging.Warning("Library unblocked");
                }

                _library = library;
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                try {
                    Test();
                } catch (Exception e) {
                    Logging.Warning(e);
                    Dispose();
                }
            }

            public Lut ToLut(string csvFilename) {
                var lut = LUTLibrary.LUTReader.Read(csvFilename);
                lut.getMaxDeltaX();

                // ReSharper disable once ObjectCreationAsStatement
                new LUTLibrary.LUTCalculator(lut);

                var force = (double)lut.getMaxForce();
                return lut.getForce().OfType<int>().Zip(lut.getLut2().OfType<double>(),
                        (x, i) => new LutPoint(x / force, Math.Round(i / force, 3))).ToLut();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void Test() {
                var lut = new LUTLibrary.LUT();
                lut.getMaxForce();
            }

            public void Dispose() {
                AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            }

            private Assembly _assembly;

            private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
                var name = new AssemblyName(args.Name);
                if (name.Name == "LUTLibrary") {
                    if (_assembly == null) {
                        try {
                            _assembly = Assembly.LoadFrom(_library);
                        } catch (Exception e) {
                            Logging.Error(e);
                        }
                    }

                    return _assembly;
                }

                return null;
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
                    Toast.Show("LUTLibrary Installed", "One thing left now is to restart CM. Do it now?", WindowsHelper.RestartCurrentApplication);
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

                    if (!name.EndsWith(".lut", StringComparison.OrdinalIgnoreCase)) {
                        name += ".lut";
                    }

                    var lut = new LutDataFile();
                    lut.Values.AddRange(_wrapper.ToLut(filename));
                    lut.Save(Path.Combine(AcPaths.GetDocumentsCfgDirectory(), name));
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
                if (filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) {
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
