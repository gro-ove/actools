using System;
using System.IO;
using System.Windows.Forms;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigFileValue : PythonAppConfigValue {
        private readonly bool _allowAbsolute;

        public bool DirectoryMode { get; }

        [CanBeNull]
        public string Filter { get; }

        public PythonAppConfigFileValue(bool directoryMode, bool allowAbsolute, [CanBeNull] string filter) {
            _allowAbsolute = allowAbsolute;
            DirectoryMode = directoryMode;
            Filter = filter?.IndexOf('|') == -1 ? filter + @"|" + filter : filter;
        }

        private DelegateCommand _changeFileCommand;

        public DelegateCommand ChangeFileCommand => _changeFileCommand ?? (_changeFileCommand = new DelegateCommand(() => {
            var currentValue = FileUtils.NormalizePath(Path.Combine(AppDirectory, string.IsNullOrEmpty(Value) ? "." : Value));

            try {
                if (DirectoryMode) {
                    var dialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        SelectedPath = Directory.Exists(currentValue) ? currentValue : AppDirectory
                    };

                    if (dialog.ShowDialog() == DialogResult.OK) {
                        Value = RelativeToApp(dialog.SelectedPath);
                    }
                } else {
                    Logging.Debug(Filter);

                    var dialog = new OpenFileDialog {
                        Filter = Filter ?? DialogFilterPiece.AllFiles.WinFilter,
                        InitialDirectory = Path.GetDirectoryName(currentValue) ?? AppDirectory,
                        FileName = Path.GetFileName(currentValue)
                    };

                    if (dialog.ShowDialog() == true) {
                        Value = RelativeToApp(dialog.FileName);
                    }
                }
            } catch (ArgumentException ex) {
                NonfatalError.Notify("Can’t select file", ex);
            } catch (Exception ex) {
                NonfatalError.Notify("Can’t change value", ex);
            }

            string RelativeToApp(string value) {
                var relative = FileUtils.GetRelativePath(value, AppDirectory);
                if (Path.IsPathRooted(relative) && !_allowAbsolute) {
                    throw new InformativeException("Can’t set the absolute path",
                            "Please, pick a file within app’s directory or at least located on the same drive.");
                }
                return relative;
            }
        }));
    }
}